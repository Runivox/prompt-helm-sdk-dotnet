using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PromptHelm.Sdk.Internal;

namespace PromptHelm.Sdk;

/// <summary>
/// Default implementation of <see cref="IPromptHelmClient"/>. Thread-safe; reuse one
/// instance per process. When constructed standalone an internal <see cref="HttpClient"/>
/// is created and disposed by <see cref="Dispose"/>; when constructed via DI with an
/// externally supplied <see cref="HttpClient"/>, that client is left untouched on dispose.
/// </summary>
public sealed class PromptHelmClient : IPromptHelmClient
{
    private const string ApiKeyPrefix = "phk_";
    private const int ApiKeyLength = 36;
    private const string ExecutePath = "/api/v1/gateway/execute";
    private const string StreamPath = "/api/v1/gateway/stream";

    private static readonly Regex ApiKeyPattern = new(
        @"^phk_[0-9a-fA-F]{32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly PromptHelmConfig _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly RetryPolicy _retryPolicy;
    private readonly string _userAgent;
    private readonly Uri _executeUri;
    private readonly Uri _streamUri;
    private bool _disposed;

    public PromptHelmClient(PromptHelmConfig config)
        : this(config, httpClient: null) { }

    public PromptHelmClient(PromptHelmConfig config, HttpClient? httpClient)
    {
        _config = ValidateConfig(config);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();

        // Only assign Timeout when we own the client; an externally supplied
        // HttpClient may have shared state and a deliberately set timeout.
        if (_ownsHttpClient)
        {
            _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }

        string baseUrl = _config.BaseUrl.TrimEnd('/');
        _executeUri = new Uri(baseUrl + ExecutePath, UriKind.Absolute);
        _streamUri = new Uri(baseUrl + StreamPath, UriKind.Absolute);
        _userAgent = BuildUserAgent(_config.UserAgent);

        _retryPolicy = new RetryPolicy(_config.MaxRetries, IsRetryable);
    }

    public Task<ExecuteResponse> ExecuteAsync(
        ExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        EnsureNotDisposed();

        return _retryPolicy.ExecuteAsync(
            ct => ExecuteOnceAsync(request, ct),
            cancellationToken);
    }

    public IAsyncEnumerable<StreamEvent> StreamAsync(
        ExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        EnsureNotDisposed();
        return StreamInternalAsync(request, cancellationToken);
    }

    private async Task<ExecuteResponse> ExecuteOnceAsync(
        ExecuteRequest request,
        CancellationToken externalToken)
    {
        using CancellationTokenSource timeoutCts = new(_config.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            timeoutCts.Token);

        using HttpRequestMessage message = BuildRequest(_executeUri, request, accept: "application/json");
        try
        {
            using HttpResponseMessage response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseContentRead, linked.Token)
                .ConfigureAwait(false);

            string body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw ErrorParser.Parse((int)response.StatusCode, body);
            }

            ExecuteResponse? parsed = JsonSerializer.Deserialize<ExecuteResponse>(body, SerializerOptions);
            if (parsed is null)
            {
                throw new ApiException(
                    (int)response.StatusCode,
                    code: null,
                    correlationId: TryReadCorrelationId(response),
                    "Server returned an empty response body.");
            }
            return parsed;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested)
        {
            throw new PromptHelmTimeoutException(_config.Timeout);
        }
    }

    private async IAsyncEnumerable<StreamEvent> StreamInternalAsync(
        ExecuteRequest request,
        [EnumeratorCancellation] CancellationToken externalToken)
    {
        using CancellationTokenSource timeoutCts = new(_config.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            HttpRequestMessage message = BuildRequest(_streamUri, request, accept: "text/event-stream");
            response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested)
        {
            throw new PromptHelmTimeoutException(_config.Timeout);
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = response.Content is null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw ErrorParser.Parse((int)response.StatusCode, errorBody);
            }

            if (response.Content is null)
            {
                throw new ApiException(
                    (int)response.StatusCode,
                    code: null,
                    correlationId: TryReadCorrelationId(response),
                    "Server returned an empty stream body.");
            }

            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: false);
            SseParser parser = new();

            char[] buffer = new char[4096];
            while (true)
            {
                int read;
                try
                {
                    read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested)
                {
                    throw new PromptHelmTimeoutException(_config.Timeout);
                }

                if (read == 0)
                {
                    foreach (string frame in parser.Flush())
                    {
                        StreamEvent? tail = SseParser.ParseEvent(frame);
                        if (tail is null)
                        {
                            continue;
                        }
                        if (tail is ErrorEvent err)
                        {
                            throw new ApiException(500, err.ErrorCode, err.RequestId, err.Message);
                        }
                        yield return tail;
                    }
                    yield break;
                }

                string chunk = new(buffer, 0, read);
                foreach (string frame in parser.Feed(chunk))
                {
                    StreamEvent? evt = SseParser.ParseEvent(frame);
                    if (evt is null)
                    {
                        continue;
                    }
                    if (evt is ErrorEvent err)
                    {
                        throw new ApiException(500, err.ErrorCode, err.RequestId, err.Message);
                    }
                    yield return evt;
                }

                linked.Token.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            response.Dispose();
        }
    }

    private HttpRequestMessage BuildRequest(Uri uri, ExecuteRequest request, string accept)
    {
        string json = JsonSerializer.Serialize(request, SerializerOptions);
        var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        message.Headers.Accept.Clear();
        message.Headers.Accept.ParseAdd(accept);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        message.Headers.UserAgent.Clear();
        message.Headers.UserAgent.ParseAdd(_userAgent);
        return message;
    }

    private static bool IsRetryable(Exception ex) => ex switch
    {
        PromptHelmTimeoutException => false,
        AuthenticationException => false,
        AuthorizationException => false,
        NotFoundException => false,
        RateLimitException => false,
        ApiException api => api.StatusCode >= 500 && api.StatusCode <= 599,
        OperationCanceledException => false,
        HttpRequestException => true,
        IOException => true,
        _ => false,
    };

    private static PromptHelmConfig ValidateConfig(PromptHelmConfig config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException(
                "PromptHelm: `ApiKey` is required. Provide a PromptHelm API key starting with `phk_`.",
                nameof(config));
        }
        if (config.ApiKey.Length != ApiKeyLength
            || !config.ApiKey.StartsWith(ApiKeyPrefix, StringComparison.Ordinal)
            || !ApiKeyPattern.IsMatch(config.ApiKey))
        {
            throw new ArgumentException(
                "PromptHelm: `ApiKey` must start with `phk_` followed by 32 hex characters.",
                nameof(config));
        }
        if (string.IsNullOrWhiteSpace(config.BaseUrl)
            || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"PromptHelm: `BaseUrl` is not a valid http(s) URL: {config.BaseUrl}",
                nameof(config));
        }
        if (config.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "PromptHelm: `Timeout` must be a positive duration.",
                nameof(config));
        }
        if (config.MaxRetries < 0)
        {
            throw new ArgumentException(
                "PromptHelm: `MaxRetries` must be a non-negative integer.",
                nameof(config));
        }
        return config;
    }

    private static string BuildUserAgent(string? prefix)
    {
        Version? sdkVersion = typeof(PromptHelmClient).GetTypeInfo().Assembly.GetName().Version;
        string version = sdkVersion is null ? "0.0.0" : $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
        string baseUa = $"prompt-helm-sdk-dotnet/{version}";
        return string.IsNullOrWhiteSpace(prefix) ? baseUa : $"{prefix} {baseUa}";
    }

    private static string? TryReadCorrelationId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-correlation-id", out IEnumerable<string>? values))
        {
            foreach (string v in values)
            {
                return v;
            }
        }
        return null;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PromptHelmClient));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

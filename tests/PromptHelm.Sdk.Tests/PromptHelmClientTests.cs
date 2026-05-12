using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class PromptHelmClientTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public void Constructor_RejectsMissingApiKey()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PromptHelmClient(new PromptHelmConfig()));
        Assert.Contains("ApiKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsBadPrefix()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new PromptHelmClient(new PromptHelmConfig { ApiKey = "abc_0123456789abcdef0123456789abcdef" }));
        Assert.Contains("phk_", ex.Message);
    }

    [Fact]
    public void Constructor_RejectsBadLength()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new PromptHelmClient(new PromptHelmConfig { ApiKey = "phk_short" }));
        Assert.Contains("32 hex", ex.Message);
    }

    [Fact]
    public void Constructor_RejectsNonHex()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new PromptHelmClient(new PromptHelmConfig { ApiKey = "phk_zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz" }));
        Assert.Contains("32 hex", ex.Message);
    }

    [Fact]
    public void Constructor_AcceptsValidKey()
    {
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey });
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_RejectsInvalidBaseUrl()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, BaseUrl = "not-a-url" }));
        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public void Constructor_RejectsZeroTimeout()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, Timeout = TimeSpan.Zero }));
        Assert.Contains("Timeout", ex.Message);
    }

    [Fact]
    public void Constructor_RejectsNegativeMaxRetries()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = -1 }));
        Assert.Contains("MaxRetries", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_Throws()
    {
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey });
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExecuteAsync(null!));
    }

    [Fact]
    public async Task Disposed_Client_Throws()
    {
        var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey });
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
    }

    [Fact]
    public async Task UserAgent_PrefixIsApplied()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse()));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, UserAgent = "my-app/1.0" },
            http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        string ua = string.Join(" ", handler.Requests[0].Headers.UserAgent.ToString());
        Assert.Contains("my-app/1.0", ua);
        Assert.Contains("prompt-helm-sdk-dotnet/", ua);
    }

    [Fact]
    public async Task AuthorizationHeader_UsesBearerScheme()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse()));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        var auth = handler.Requests[0].Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal(ValidKey, auth.Parameter);
    }

    private static string BasicResponse() =>
        """
        {"id":"e1","output":"hi","model":"gpt-4o","inputTokens":1,"outputTokens":1,"totalTokens":2,"latencyMs":10,"cost":0.0001,"timestamp":"2026-01-01T00:00:00Z"}
        """;
}

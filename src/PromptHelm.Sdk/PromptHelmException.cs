using System;

namespace PromptHelm.Sdk;

/// <summary>
/// Base type for all errors returned by the PromptHelm gateway.
/// </summary>
public abstract class PromptHelmException : Exception
{
    /// <summary>HTTP status code from the error envelope (<c>statusCode</c>).</summary>
    public int StatusCode { get; }

    /// <summary>Machine-readable error code from the envelope (<c>errorCode</c>).</summary>
    public string? ErrorCode { get; }

    /// <summary>Server-generated request identifier from the envelope (<c>requestId</c>).</summary>
    public string? RequestId { get; }

    protected PromptHelmException(int statusCode, string? errorCode, string? requestId, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RequestId = requestId;
    }
}

/// <summary>HTTP 401 — invalid or missing API key.</summary>
public sealed class AuthenticationException : PromptHelmException
{
    public AuthenticationException(int statusCode, string? errorCode, string? requestId, string message)
        : base(statusCode, errorCode, requestId, message) { }
}

/// <summary>HTTP 403 — caller is authenticated but lacks permission.</summary>
public sealed class AuthorizationException : PromptHelmException
{
    public AuthorizationException(int statusCode, string? errorCode, string? requestId, string message)
        : base(statusCode, errorCode, requestId, message) { }
}

/// <summary>HTTP 404 — requested prompt or resource does not exist.</summary>
public sealed class NotFoundException : PromptHelmException
{
    public NotFoundException(int statusCode, string? errorCode, string? requestId, string message)
        : base(statusCode, errorCode, requestId, message) { }
}

/// <summary>HTTP 429 — rate limit exceeded.</summary>
public sealed class RateLimitException : PromptHelmException
{
    public RateLimitException(int statusCode, string? errorCode, string? requestId, string message)
        : base(statusCode, errorCode, requestId, message) { }
}

/// <summary>
/// Generic API error. Used for HTTP 5xx responses, unknown 4xx codes,
/// and stream <c>error</c> events.
/// </summary>
public sealed class ApiException : PromptHelmException
{
    public ApiException(int statusCode, string? errorCode, string? requestId, string message)
        : base(statusCode, errorCode, requestId, message) { }
}

/// <summary>
/// Raised when a request exceeds the configured timeout. Distinct from
/// <see cref="OperationCanceledException"/> caused by an external token.
/// </summary>
public sealed class PromptHelmTimeoutException : Exception
{
    public TimeSpan Timeout { get; }

    public PromptHelmTimeoutException(TimeSpan timeout)
        : base($"Request timed out after {timeout.TotalMilliseconds:F0}ms")
    {
        Timeout = timeout;
    }

    public PromptHelmTimeoutException(TimeSpan timeout, string message)
        : base(message)
    {
        Timeout = timeout;
    }
}

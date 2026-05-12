using System;

namespace PromptHelm.Sdk;

/// <summary>
/// Base type for all errors returned by the PromptHelm gateway.
/// </summary>
public abstract class PromptHelmException : Exception
{
    public int StatusCode { get; }
    public string? Code { get; }
    public string? CorrelationId { get; }

    protected PromptHelmException(int statusCode, string? code, string? correlationId, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        CorrelationId = correlationId;
    }
}

/// <summary>HTTP 401 — invalid or missing API key.</summary>
public sealed class AuthenticationException : PromptHelmException
{
    public AuthenticationException(int statusCode, string? code, string? correlationId, string message)
        : base(statusCode, code, correlationId, message) { }
}

/// <summary>HTTP 403 — caller is authenticated but lacks permission.</summary>
public sealed class AuthorizationException : PromptHelmException
{
    public AuthorizationException(int statusCode, string? code, string? correlationId, string message)
        : base(statusCode, code, correlationId, message) { }
}

/// <summary>HTTP 404 — requested prompt or resource does not exist.</summary>
public sealed class NotFoundException : PromptHelmException
{
    public NotFoundException(int statusCode, string? code, string? correlationId, string message)
        : base(statusCode, code, correlationId, message) { }
}

/// <summary>HTTP 429 — rate limit exceeded.</summary>
public sealed class RateLimitException : PromptHelmException
{
    public RateLimitException(int statusCode, string? code, string? correlationId, string message)
        : base(statusCode, code, correlationId, message) { }
}

/// <summary>
/// Generic API error. Used for HTTP 5xx responses, unknown 4xx codes,
/// and stream <c>error</c> events.
/// </summary>
public sealed class ApiException : PromptHelmException
{
    public ApiException(int statusCode, string? code, string? correlationId, string message)
        : base(statusCode, code, correlationId, message) { }
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

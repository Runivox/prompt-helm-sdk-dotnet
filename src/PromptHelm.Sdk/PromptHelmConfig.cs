namespace PromptHelm.Sdk;

/// <summary>
/// Configuration for <see cref="PromptHelmClient"/>. Construct once per process and reuse.
/// Mutable so it can be populated through the DI <c>AddPromptHelm</c> options callback.
/// </summary>
public sealed class PromptHelmConfig
{
    /// <summary>
    /// PromptHelm API key. Must start with <c>phk_</c> and be 36 characters long
    /// (4-character prefix + 32 hex characters).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the PromptHelm gateway. Defaults to the public production URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.prompthelm.app";

    /// <summary>
    /// Per-request timeout. Applied for both unary execute and streaming calls.
    /// Defaults to 60 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of retries on transient (5xx / network) failures.
    /// Authentication, authorization, not-found, rate-limit, and timeout errors are not retried.
    /// Defaults to 2.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Optional User-Agent prefix prepended to the SDK identifier.
    /// Use this to identify the application calling PromptHelm.
    /// </summary>
    public string? UserAgent { get; set; }
}

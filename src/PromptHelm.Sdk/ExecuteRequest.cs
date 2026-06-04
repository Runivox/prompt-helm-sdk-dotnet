using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PromptHelm.Sdk;

/// <summary>
/// Request body sent to <c>POST /api/v1/gateway/execute</c> and <c>POST /api/v1/gateway/stream</c>.
/// At minimum either <see cref="PromptSlug"/> or <see cref="PromptId"/> must be supplied,
/// or both <see cref="System"/> and <see cref="User"/>.
/// </summary>
public sealed record ExecuteRequest
{
    [JsonPropertyName("promptSlug")]
    public string? PromptSlug { get; init; }

    [JsonPropertyName("promptId")]
    public string? PromptId { get; init; }

    [JsonPropertyName("variables")]
    public Dictionary<string, string>? Variables { get; init; }

    [JsonPropertyName("system")]
    public string? System { get; init; }

    [JsonPropertyName("user")]
    public string? User { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("topP")]
    public double? TopP { get; init; }

    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; init; }

    /// <summary>
    /// Environment to resolve the prompt version from. The gateway accepts only
    /// <see cref="PromptEnvironments.Production"/> (<c>"production"</c>) or
    /// <see cref="PromptEnvironments.Development"/> (<c>"development"</c>).
    /// When omitted, the latest version is used.
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    [JsonPropertyName("timeoutMs")]
    public long? TimeoutMs { get; init; }
}

/// <summary>
/// Valid values for <see cref="ExecuteRequest.Environment"/>. The PromptHelm
/// gateway recognizes exactly these two environments; there is no
/// <c>staging</c>/<c>main</c>/<c>dev</c> triad.
/// </summary>
public static class PromptEnvironments
{
    /// <summary>The <c>production</c> environment.</summary>
    public const string Production = "production";

    /// <summary>The <c>development</c> environment.</summary>
    public const string Development = "development";
}

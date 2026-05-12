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
    /// Environment branch to resolve. <c>"production"</c> or <c>"development"</c>.
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    [JsonPropertyName("timeoutMs")]
    public long? TimeoutMs { get; init; }
}

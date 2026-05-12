using System;
using System.Text.Json.Serialization;

namespace PromptHelm.Sdk;

/// <summary>
/// Response payload from <c>POST /api/v1/gateway/execute</c>.
/// </summary>
public sealed record ExecuteResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("inputTokens")] int InputTokens,
    [property: JsonPropertyName("outputTokens")] int OutputTokens,
    [property: JsonPropertyName("totalTokens")] int TotalTokens,
    [property: JsonPropertyName("latencyMs")] int LatencyMs,
    [property: JsonPropertyName("cost")] double Cost,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);

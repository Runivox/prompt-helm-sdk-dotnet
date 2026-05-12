namespace PromptHelm.Sdk;

/// <summary>
/// Discriminated union of events emitted by the streaming gateway.
/// </summary>
public abstract record StreamEvent;

/// <summary>
/// Incremental text chunk produced by the model.
/// </summary>
public sealed record ChunkEvent(string Content) : StreamEvent;

/// <summary>
/// Terminal event emitted once generation finishes successfully.
/// Token counts and cost reflect the full request.
/// </summary>
public sealed record DoneEvent(
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    double Cost,
    string Model,
    int LatencyMs) : StreamEvent;

/// <summary>
/// Terminal event emitted when the gateway aborts the stream with an error.
/// </summary>
public sealed record ErrorEvent(string ErrorCode, string Message, string? RequestId) : StreamEvent;

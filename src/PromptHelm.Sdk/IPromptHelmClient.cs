using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHelm.Sdk;

/// <summary>
/// Public surface of the PromptHelm SDK. Inject via DI or new up directly.
/// </summary>
public interface IPromptHelmClient : IDisposable
{
    /// <summary>
    /// Execute a managed prompt and return the full response once generation completes.
    /// </summary>
    Task<ExecuteResponse> ExecuteAsync(
        ExecuteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream a managed prompt response as a sequence of events
    /// (<see cref="ChunkEvent"/> followed by a terminal <see cref="DoneEvent"/>).
    /// </summary>
    IAsyncEnumerable<StreamEvent> StreamAsync(
        ExecuteRequest request,
        CancellationToken cancellationToken = default);
}

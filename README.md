# PromptHelm .NET SDK

[![NuGet](https://img.shields.io/nuget/v/PromptHelm.Sdk.svg)](https://www.nuget.org/packages/PromptHelm.Sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Official .NET SDK for [PromptHelm](https://prompthelm.app) - the LLMOps control
plane for managed prompts, multi-provider routing, and execution analytics.

Call your versioned prompts from any .NET application without baking model
choices, provider keys, or pricing logic into your code.

- Multi-target: `net8.0` and `netstandard2.0` (works on .NET 6+, .NET Framework
  4.6.1+, Unity, Mono, Xamarin)
- Strongly typed `HttpClient` + `System.Text.Json` (no Newtonsoft, no reflection
  surprises)
- DI-friendly via `IPromptHelmClient` and `AddPromptHelm`
- Streaming via `IAsyncEnumerable<StreamEvent>`
- Exponential-backoff retry on transient (5xx / network) failures
- Typed exception hierarchy mapped to PromptHelm error codes

> The API-token surface is exactly two endpoints: `POST /api/v1/gateway/execute`
> (`ExecuteAsync`) and `POST /api/v1/gateway/stream` (`StreamAsync`). There is no
> token-callable prompt-fetch, version-listing, or telemetry endpoint — resolve a
> saved prompt by passing `PromptSlug`/`PromptId` (plus an optional
> `Environment`) into the execute/stream call.

## Install

```bash
dotnet add package PromptHelm.Sdk
```

## Quickstart

```csharp
using PromptHelm.Sdk;

using var client = new PromptHelmClient(new PromptHelmConfig
{
    ApiKey = Environment.GetEnvironmentVariable("PROMPTHELM_API_KEY")!,
});

ExecuteResponse response = await client.ExecuteAsync(new ExecuteRequest
{
    PromptSlug = "support-summary",
    Variables = new Dictionary<string, string>
    {
        ["ticket_id"] = "T-1042",
        ["customer"] = "Acme",
    },
    Environment = "production",
});

Console.WriteLine(response.Output);
Console.WriteLine($"{response.TotalTokens} tokens, ${response.Cost:F4}");
```

## Streaming

```csharp
await foreach (StreamEvent evt in client.StreamAsync(new ExecuteRequest
{
    PromptSlug = "support-summary",
    Variables = new() { ["ticket_id"] = "T-1042" },
}))
{
    switch (evt)
    {
        case ChunkEvent chunk:
            Console.Write(chunk.Content);
            break;
        case DoneEvent done:
            Console.WriteLine();
            Console.WriteLine($"{done.TotalTokens} tokens, ${done.Cost:F4}");
            break;
    }
}
```

## Environments

`ExecuteRequest.Environment` selects which version of a saved prompt to resolve.
The gateway accepts only `"production"` and `"development"` — there is no
`staging`/`main`/`dev` triad. Use the `PromptEnvironments` constants to avoid
typos. When omitted, the latest version is used.

```csharp
new ExecuteRequest
{
    PromptSlug = "support-summary",
    Environment = PromptEnvironments.Production, // or PromptEnvironments.Development
};
```

## Dependency injection

```csharp
using PromptHelm.Sdk;
using PromptHelm.Sdk.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPromptHelm(opts =>
{
    opts.ApiKey = builder.Configuration["PromptHelm:ApiKey"]!;
    opts.UserAgent = "checkout-service/1.4.2";
});

// Then inject IPromptHelmClient anywhere:
public class SummaryService(IPromptHelmClient promptHelm)
{
    public Task<ExecuteResponse> SummarizeAsync(string ticketId) =>
        promptHelm.ExecuteAsync(new ExecuteRequest
        {
            PromptSlug = "support-summary",
            Variables = new() { ["ticket_id"] = ticketId },
        });
}
```

The DI extension wires the client to `IHttpClientFactory`, so connection
pooling, DNS refresh, and `DelegatingHandler` chains all work as expected.

## Configuration

| Property      | Default                          | Description                                                                  |
| ------------- | -------------------------------- | ---------------------------------------------------------------------------- |
| `ApiKey`      | (required)                       | Your PromptHelm API key. Must start with `phk_` and be 36 chars total.        |
| `BaseUrl`     | `https://api.prompthelm.app`     | Override only for staging or self-hosted gateways.                           |
| `Timeout`     | `60s`                            | Per-request deadline. Both unary and streaming calls observe it.             |
| `MaxRetries`  | `2`                              | Transient (5xx / network) retries. Auth, 4xx, and timeout errors never retry. |
| `UserAgent`   | `null`                           | Optional prefix for the SDK's User-Agent header.                             |

## Error handling

All gateway errors derive from `PromptHelmException`:

| Exception                        | When it is thrown                       |
| -------------------------------- | --------------------------------------- |
| `AuthenticationException`        | HTTP 401 - invalid or missing API key.  |
| `AuthorizationException`         | HTTP 403 - missing scope.               |
| `NotFoundException`              | HTTP 404 - prompt slug or id not found. |
| `RateLimitException`             | HTTP 429 - rate limit exceeded.         |
| `ApiException`                   | HTTP 5xx, unknown 4xx, stream errors.   |
| `PromptHelmTimeoutException`     | The configured `Timeout` elapsed.       |

`OperationCanceledException` propagates when the caller cancels via
`CancellationToken`.

Every `PromptHelmException` surfaces the fields from the gateway's error
envelope (`{ statusCode, errorCode, message, timestamp, requestId }`):
`StatusCode`, `ErrorCode`, `Message`, and `RequestId`. Include `RequestId` when
contacting support so the request can be traced.

```csharp
try
{
    await client.ExecuteAsync(request);
}
catch (RateLimitException ex)
{
    logger.LogWarning("Rate limited (request {RequestId}): {Message}",
        ex.RequestId, ex.Message);
}
catch (PromptHelmException ex)
{
    logger.LogError(ex, "PromptHelm {ErrorCode} (request {RequestId}): {Message}",
        ex.ErrorCode, ex.RequestId, ex.Message);
    throw;
}
```

## Cancellation

Every async method takes an optional `CancellationToken`. Cancelling the token
aborts the in-flight HTTP request without waiting for `Timeout` to elapse.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
ExecuteResponse response = await client.ExecuteAsync(request, cts.Token);
```

## Multi-targeting notes

| Target               | Supports                                                        |
| -------------------- | --------------------------------------------------------------- |
| `net8.0`             | First-class. Uses native `IAsyncEnumerable`.                    |
| `netstandard2.0`     | Adds `Microsoft.Bcl.AsyncInterfaces` polyfill for streaming.    |

The package only depends on `System.Text.Json`,
`Microsoft.Extensions.DependencyInjection.Abstractions`, and
`Microsoft.Extensions.Http`. No transitive Newtonsoft.Json.

## Releasing

GitHub Actions handles the publish flow. Tag a release and the workflow runs
`dotnet pack` and pushes both `.nupkg` and `.snupkg` to NuGet.org.

```bash
# 1. Bump <Version> in src/PromptHelm.Sdk/PromptHelm.Sdk.csproj
# 2. Update CHANGELOG.md
git commit -am "chore: release v0.2.0"
git tag v0.2.0
git push --follow-tags
```

The CI release job needs a repository secret called `NUGET_API_KEY` containing a
push-scoped key from <https://www.nuget.org/account/apikeys>. Scope the key to
the `PromptHelm.Sdk` package id only.

## Local development

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
dotnet pack src/PromptHelm.Sdk/PromptHelm.Sdk.csproj -c Release -o nupkgs
```

## License

[MIT](LICENSE)

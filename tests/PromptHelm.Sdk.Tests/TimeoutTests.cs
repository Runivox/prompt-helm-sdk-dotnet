using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class TimeoutTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Execute_TimeoutFires_ThrowsPromptHelmTimeoutException()
    {
        var handler = new MockHttpMessageHandler(async (_, _, ct) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (TaskCanceledException)
            {
                // forward the cancellation
            }
            ct.ThrowIfCancellationRequested();
            return MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig
            {
                ApiKey = ValidKey,
                Timeout = TimeSpan.FromMilliseconds(50),
                MaxRetries = 0,
            },
            http);

        await Assert.ThrowsAsync<PromptHelmTimeoutException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
    }

    [Fact]
    public async Task Execute_ExternalCancellation_ThrowsOperationCanceled()
    {
        var handler = new MockHttpMessageHandler(async (_, _, ct) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (TaskCanceledException)
            {
            }
            ct.ThrowIfCancellationRequested();
            return MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 },
            http);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }, cts.Token));
    }
}

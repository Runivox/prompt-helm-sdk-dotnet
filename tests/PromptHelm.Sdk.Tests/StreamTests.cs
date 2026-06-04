using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class StreamTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Stream_EmitsChunksThenDone()
    {
        const string body =
            "data: {\"type\":\"chunk\",\"content\":\"Hello\"}\n\n" +
            "data: {\"type\":\"chunk\",\"content\":\" world\"}\n\n" +
            "data: {\"type\":\"done\",\"inputTokens\":3,\"outputTokens\":2,\"totalTokens\":5,\"cost\":0.0001,\"model\":\"gpt-4o\",\"latencyMs\":120}\n\n";

        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.EventStream(body));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var events = new List<StreamEvent>();
        await foreach (StreamEvent evt in client.StreamAsync(new ExecuteRequest { PromptSlug = "x" }))
        {
            events.Add(evt);
        }

        Assert.Equal(3, events.Count);
        Assert.Equal("Hello", Assert.IsType<ChunkEvent>(events[0]).Content);
        Assert.Equal(" world", Assert.IsType<ChunkEvent>(events[1]).Content);
        var done = Assert.IsType<DoneEvent>(events[2]);
        Assert.Equal(5, done.TotalTokens);
        Assert.Equal("gpt-4o", done.Model);
    }

    [Fact]
    public async Task Stream_PostsToStreamUrlWithEventStreamAccept()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.EventStream(
            "data: {\"type\":\"done\",\"inputTokens\":0,\"outputTokens\":0,\"totalTokens\":0,\"cost\":0,\"model\":\"m\",\"latencyMs\":0}\n\n"));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await foreach (var _ in client.StreamAsync(new ExecuteRequest { PromptSlug = "x" }))
        {
        }

        Assert.EndsWith("/api/v1/gateway/stream", handler.Requests[0].RequestUri!.ToString());
        string accept = string.Join(",", handler.Requests[0].Headers.Accept);
        Assert.Contains("text/event-stream", accept);
    }

    [Fact]
    public async Task Stream_ErrorEvent_ThrowsApiException()
    {
        const string body =
            "data: {\"type\":\"chunk\",\"content\":\"partial\"}\n\n" +
            "data: {\"type\":\"error\",\"errorCode\":\"E_UPSTREAM\",\"message\":\"provider down\",\"requestId\":\"r-9\"}\n\n";

        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.EventStream(body));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var consumed = new List<StreamEvent>();
        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (StreamEvent evt in client.StreamAsync(new ExecuteRequest { PromptSlug = "x" }))
            {
                consumed.Add(evt);
            }
        });

        Assert.Single(consumed);
        Assert.Equal("E_UPSTREAM", ex.ErrorCode);
        Assert.Equal("r-9", ex.RequestId);
        Assert.Equal("provider down", ex.Message);
    }

    [Fact]
    public async Task Stream_NonOk_ThrowsTypedError()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized,
            """{"statusCode":401,"errorCode":"UNAUTHORIZED","message":"bad","timestamp":"2026-06-05T00:00:00.000Z","requestId":"r-1"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await Assert.ThrowsAsync<AuthenticationException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(new ExecuteRequest { PromptSlug = "x" }))
            {
            }
        });
    }
}

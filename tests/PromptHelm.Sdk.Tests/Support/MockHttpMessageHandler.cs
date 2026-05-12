using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHelm.Sdk.Tests.Support;

/// <summary>
/// Lightweight HttpMessageHandler stub. The handler invokes the supplied
/// responder for each call and records every request it receives.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;
    public List<HttpRequestMessage> Requests { get; } = new();
    public int CallCount { get; private set; }

    public MockHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((req, _, _) => Task.FromResult(responder(req)))
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        int call = CallCount++;
        return await _responder(request, call, cancellationToken).ConfigureAwait(false);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage EventStream(string body)
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, "text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}

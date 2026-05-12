using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class ExecuteTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Execute_ParsesSuccessResponse()
    {
        const string body =
            """
            {"id":"exec_1","output":"hello","model":"gpt-4o","inputTokens":3,"outputTokens":5,"totalTokens":8,"latencyMs":250,"cost":0.0042,"timestamp":"2026-05-13T10:00:00Z"}
            """;
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(HttpStatusCode.OK, body));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        ExecuteResponse response = await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "summary" });

        Assert.Equal("exec_1", response.Id);
        Assert.Equal("hello", response.Output);
        Assert.Equal("gpt-4o", response.Model);
        Assert.Equal(3, response.InputTokens);
        Assert.Equal(5, response.OutputTokens);
        Assert.Equal(8, response.TotalTokens);
        Assert.Equal(250, response.LatencyMs);
        Assert.Equal(0.0042, response.Cost);
    }

    [Fact]
    public async Task Execute_PostsToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse()));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, BaseUrl = "https://api.prompthelm.app" },
            http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        Assert.Equal("POST", handler.Requests[0].Method.Method);
        Assert.Equal(
            "https://api.prompthelm.app/api/v1/gateway/execute",
            handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Execute_TrailingSlashOnBaseUrl_IsNormalized()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse()));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, BaseUrl = "https://api.prompthelm.app/" },
            http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        Assert.Equal(
            "https://api.prompthelm.app/api/v1/gateway/execute",
            handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Execute_SendsAcceptJsonHeader()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse()));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        string accept = string.Join(",", handler.Requests[0].Headers.Accept);
        Assert.Contains("application/json", accept);
    }

    [Fact]
    public async Task Execute_SerializesRequestBodyCamelCase()
    {
        string capturedBody = string.Empty;
        var handler = new MockHttpMessageHandler(async (req, _, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse());
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await client.ExecuteAsync(new ExecuteRequest
        {
            PromptSlug = "summary",
            MaxTokens = 256,
            Temperature = 0.2,
            Environment = "production",
        });

        Assert.Contains("\"promptSlug\":\"summary\"", capturedBody);
        Assert.Contains("\"maxTokens\":256", capturedBody);
        Assert.Contains("\"temperature\":0.2", capturedBody);
        Assert.Contains("\"environment\":\"production\"", capturedBody);
    }

    [Fact]
    public async Task Execute_NullPropertiesAreOmitted()
    {
        string capturedBody = string.Empty;
        var handler = new MockHttpMessageHandler(async (req, _, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return MockHttpMessageHandler.Json(HttpStatusCode.OK, BasicResponse());
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "summary" });

        Assert.DoesNotContain("\"promptId\"", capturedBody);
        Assert.DoesNotContain("\"variables\"", capturedBody);
        Assert.DoesNotContain("\"system\"", capturedBody);
    }

    private static string BasicResponse() =>
        """
        {"id":"e1","output":"hi","model":"gpt-4o","inputTokens":1,"outputTokens":1,"totalTokens":2,"latencyMs":10,"cost":0.0001,"timestamp":"2026-01-01T00:00:00Z"}
        """;
}

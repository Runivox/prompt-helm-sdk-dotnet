using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class ErrorTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Status401_MapsToAuthenticationException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized,
            """{"statusCode":401,"errorCode":"UNAUTHORIZED","message":"bad key","timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-1"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("UNAUTHORIZED", ex.ErrorCode);
        Assert.Equal("req-1", ex.RequestId);
        Assert.Equal("bad key", ex.Message);
    }

    [Fact]
    public async Task Status403_MapsToAuthorizationException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.Forbidden,
            """{"statusCode":403,"errorCode":"FORBIDDEN","message":"no access","timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-2"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var ex = await Assert.ThrowsAsync<AuthorizationException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("no access", ex.Message);
    }

    [Fact]
    public async Task Status404_MapsToNotFoundException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.NotFound,
            """{"statusCode":404,"errorCode":"PROMPT_VERSION_NOT_FOUND","message":"prompt missing","timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-3"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("PROMPT_VERSION_NOT_FOUND", ex.ErrorCode);
        Assert.Equal("req-3", ex.RequestId);
    }

    [Fact]
    public async Task Status429_MapsToRateLimitException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            (HttpStatusCode)429,
            """{"statusCode":429,"errorCode":"TOO_MANY_REQUESTS","message":"slow down","timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-4"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 }, http);

        var ex = await Assert.ThrowsAsync<RateLimitException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task Status500_MapsToApiException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.InternalServerError,
            """{"statusCode":500,"errorCode":"INTERNAL_ERROR","message":"oops","timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-5"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 }, http);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(500, ex.StatusCode);
        Assert.Equal("oops", ex.Message);
    }

    [Fact]
    public async Task ArrayMessage_IsJoinedIntoSingleString()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.BadRequest,
            """{"statusCode":400,"errorCode":"VALIDATION_ERROR","message":["promptId must be a string","temperature must not be greater than 2"],"timestamp":"2026-06-05T00:00:00.000Z","requestId":"req-6"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 }, http);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("VALIDATION_ERROR", ex.ErrorCode);
        Assert.Equal("req-6", ex.RequestId);
        Assert.Contains("promptId must be a string", ex.Message);
        Assert.Contains("temperature must not be greater than 2", ex.Message);
    }

    [Fact]
    public async Task NonJsonError_FallsBackToDefaultMessage()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>nginx</html>"),
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 }, http);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("internal error", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}

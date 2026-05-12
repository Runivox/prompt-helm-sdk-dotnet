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
            """{"statusCode":401,"error":"Unauthorized","message":"bad key","code":"AUTH_001","correlationId":"c-1"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("AUTH_001", ex.Code);
        Assert.Equal("c-1", ex.CorrelationId);
        Assert.Equal("bad key", ex.Message);
    }

    [Fact]
    public async Task Status403_MapsToAuthorizationException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            HttpStatusCode.Forbidden,
            """{"statusCode":403,"error":"Forbidden","message":"no access"}"""));
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
            """{"statusCode":404,"error":"NotFound","message":"prompt missing"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey }, http);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Status429_MapsToRateLimitException()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            (HttpStatusCode)429,
            """{"statusCode":429,"error":"TooMany","message":"slow down"}"""));
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
            """{"statusCode":500,"error":"Internal","message":"oops"}"""));
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 0 }, http);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(500, ex.StatusCode);
        Assert.Equal("oops", ex.Message);
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

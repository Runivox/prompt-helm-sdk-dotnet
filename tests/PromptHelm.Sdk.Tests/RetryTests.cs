using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PromptHelm.Sdk.Internal;
using PromptHelm.Sdk.Tests.Support;

namespace PromptHelm.Sdk.Tests;

public class RetryTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Retries_Transient5xx_UntilSuccess()
    {
        int attempts = 0;
        var handler = new MockHttpMessageHandler((_, call, _) =>
        {
            attempts++;
            if (call < 2)
            {
                return Task.FromResult(MockHttpMessageHandler.Json(
                    HttpStatusCode.InternalServerError,
                    """{"statusCode":500,"error":"Internal","message":"flaky"}"""));
            }
            return Task.FromResult(MockHttpMessageHandler.Json(
                HttpStatusCode.OK,
                """{"id":"ok","output":"hi","model":"m","inputTokens":1,"outputTokens":1,"totalTokens":2,"latencyMs":1,"cost":0,"timestamp":"2026-01-01T00:00:00Z"}"""));
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 2 },
            http);

        ExecuteResponse response = await client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" });

        Assert.Equal("ok", response.Id);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task DoesNotRetry_4xxClientError()
    {
        int attempts = 0;
        var handler = new MockHttpMessageHandler((_, _, _) =>
        {
            attempts++;
            return Task.FromResult(MockHttpMessageHandler.Json(
                HttpStatusCode.Unauthorized,
                """{"statusCode":401,"error":"Unauthorized","message":"no"}"""));
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 5 },
            http);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task GivesUpAfterMaxRetries()
    {
        int attempts = 0;
        var handler = new MockHttpMessageHandler((_, _, _) =>
        {
            attempts++;
            return Task.FromResult(MockHttpMessageHandler.Json(
                HttpStatusCode.BadGateway,
                """{"statusCode":502,"error":"Bad","message":"down"}"""));
        });
        using var http = new HttpClient(handler);
        using var client = new PromptHelmClient(
            new PromptHelmConfig { ApiKey = ValidKey, MaxRetries = 1 },
            http);

        await Assert.ThrowsAsync<ApiException>(() =>
            client.ExecuteAsync(new ExecuteRequest { PromptSlug = "x" }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void RetryPolicy_BackoffGrowsExponentially()
    {
        var policy = new RetryPolicy(
            maxRetries: 3,
            isRetryable: _ => true,
            random: () => 0.0,
            baseDelayMs: 100,
            maxDelayMs: 10_000);

        Assert.Equal(100, policy.ComputeBackoff(0));
        Assert.Equal(200, policy.ComputeBackoff(1));
        Assert.Equal(400, policy.ComputeBackoff(2));
        Assert.Equal(800, policy.ComputeBackoff(3));
    }

    [Fact]
    public void RetryPolicy_BackoffCappedAtMax()
    {
        var policy = new RetryPolicy(
            maxRetries: 10,
            isRetryable: _ => true,
            random: () => 0.0,
            baseDelayMs: 1000,
            maxDelayMs: 4000);

        Assert.Equal(4000, policy.ComputeBackoff(10));
    }

    [Fact]
    public async Task RetryPolicy_NotRetryablePredicate_RethrowsImmediately()
    {
        var policy = new RetryPolicy(
            maxRetries: 5,
            isRetryable: _ => false,
            delay: (_, _) => Task.CompletedTask);

        int calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new InvalidOperationException();
            }, CancellationToken.None));
        Assert.Equal(1, calls);
    }
}

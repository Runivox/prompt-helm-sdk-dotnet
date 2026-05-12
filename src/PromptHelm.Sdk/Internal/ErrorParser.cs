using System.Text.Json;

namespace PromptHelm.Sdk.Internal;

internal static class ErrorParser
{
    public static PromptHelmException Parse(int status, string? rawBody)
    {
        string? code = null;
        string? correlationId = null;
        string? message = null;

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(rawBody!);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("message", out JsonElement msg)
                        && msg.ValueKind == JsonValueKind.String)
                    {
                        message = msg.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("code", out JsonElement codeEl)
                        && codeEl.ValueKind == JsonValueKind.String)
                    {
                        code = codeEl.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("correlationId", out JsonElement corr)
                        && corr.ValueKind == JsonValueKind.String)
                    {
                        correlationId = corr.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Body wasn't JSON. Fall through to default messaging.
            }
        }

        message ??= FallbackMessage(status);

        return status switch
        {
            401 => new AuthenticationException(status, code, correlationId, message),
            403 => new AuthorizationException(status, code, correlationId, message),
            404 => new NotFoundException(status, code, correlationId, message),
            429 => new RateLimitException(status, code, correlationId, message),
            _ => new ApiException(status, code, correlationId, message),
        };
    }

    private static string FallbackMessage(int status) => status switch
    {
        401 => "Authentication failed. Check that your API key is valid and not revoked.",
        403 => "You do not have permission to perform this action.",
        404 => "The requested prompt or resource was not found.",
        429 => "Rate limit exceeded. Slow down requests or upgrade your plan.",
        _ when status >= 500 => "PromptHelm encountered an internal error. The request can be retried.",
        _ => $"Request failed with status {status}.",
    };
}

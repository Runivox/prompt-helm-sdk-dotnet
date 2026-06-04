using System.Text.Json;

namespace PromptHelm.Sdk.Internal;

internal static class ErrorParser
{
    public static PromptHelmException Parse(int status, string? rawBody)
    {
        // Error envelope (GlobalExceptionFilter): { statusCode, errorCode, message, timestamp, requestId }.
        // `message` may be a JSON string or an array of strings (class-validator output).
        string? errorCode = null;
        string? requestId = null;
        string? message = null;

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(rawBody!);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("message", out JsonElement msg))
                    {
                        message = ReadMessage(msg);
                    }
                    if (doc.RootElement.TryGetProperty("errorCode", out JsonElement codeEl)
                        && codeEl.ValueKind == JsonValueKind.String)
                    {
                        errorCode = codeEl.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("requestId", out JsonElement rid)
                        && rid.ValueKind == JsonValueKind.String)
                    {
                        requestId = rid.GetString();
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
            401 => new AuthenticationException(status, errorCode, requestId, message),
            403 => new AuthorizationException(status, errorCode, requestId, message),
            404 => new NotFoundException(status, errorCode, requestId, message),
            429 => new RateLimitException(status, errorCode, requestId, message),
            _ => new ApiException(status, errorCode, requestId, message),
        };
    }

    private static string? ReadMessage(JsonElement msg)
    {
        if (msg.ValueKind == JsonValueKind.String)
        {
            return msg.GetString();
        }
        if (msg.ValueKind == JsonValueKind.Array)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (JsonElement item in msg.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? part = item.GetString();
                    if (!string.IsNullOrEmpty(part))
                    {
                        parts.Add(part!);
                    }
                }
            }
            if (parts.Count > 0)
            {
                return string.Join("; ", parts);
            }
        }
        return null;
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

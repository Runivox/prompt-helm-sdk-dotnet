using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PromptHelm.Sdk.Internal;

/// <summary>
/// Incremental Server-Sent Events frame parser. Extracts <c>data:</c> field
/// payloads delimited by blank lines. Comment lines (starting with <c>:</c>)
/// and non-data fields are ignored.
/// </summary>
internal sealed class SseParser
{
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _dataLines = new();

    public List<string> Feed(string chunk)
    {
        _buffer.Append(chunk);
        var frames = new List<string>();

        while (true)
        {
            (int index, int length) = IndexOfLineEnd();
            if (index < 0)
            {
                break;
            }

            string line = _buffer.ToString(0, index);
            _buffer.Remove(0, index + length);

            if (line.Length == 0)
            {
                if (_dataLines.Count > 0)
                {
                    frames.Add(string.Join("\n", _dataLines));
                    _dataLines.Clear();
                }
            }
            else if (!line.StartsWith(":"))
            {
                int colon = line.IndexOf(':');
                string field;
                string value;
                if (colon < 0)
                {
                    field = line;
                    value = string.Empty;
                }
                else
                {
                    field = line.Substring(0, colon);
                    value = line.Substring(colon + 1);
                    if (value.Length > 0 && value[0] == ' ')
                    {
                        value = value.Substring(1);
                    }
                }

                if (field == "data")
                {
                    _dataLines.Add(value);
                }
            }
        }

        return frames;
    }

    public List<string> Flush()
    {
        if (_dataLines.Count == 0)
        {
            return new List<string>();
        }

        var frame = new List<string> { string.Join("\n", _dataLines) };
        _dataLines.Clear();
        return frame;
    }

    private (int Index, int Length) IndexOfLineEnd()
    {
        string s = _buffer.ToString();
        int crlf = s.IndexOf("\r\n", System.StringComparison.Ordinal);
        int lf = s.IndexOf('\n');
        int cr = s.IndexOf('\r');

        if (crlf >= 0 && (lf < 0 || crlf <= lf) && (cr < 0 || crlf <= cr))
        {
            return (crlf, 2);
        }
        if (lf >= 0 && (cr < 0 || lf < cr))
        {
            return (lf, 1);
        }
        if (cr >= 0)
        {
            return (cr, 1);
        }
        return (-1, 0);
    }

    /// <summary>
    /// Parse a single SSE <c>data:</c> payload into a typed <see cref="StreamEvent"/>.
    /// Returns <c>null</c> for empty frames, the literal <c>[DONE]</c> sentinel,
    /// malformed JSON, or unrecognized event types.
    /// </summary>
    public static StreamEvent? ParseEvent(string data)
    {
        string trimmed = data.Trim();
        if (trimmed.Length == 0 || trimmed == "[DONE]")
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("type", out JsonElement typeEl)
                || typeEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? type = typeEl.GetString();
            return type switch
            {
                "chunk" => ParseChunk(doc.RootElement),
                "done" => ParseDone(doc.RootElement),
                "error" => ParseError(doc.RootElement),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChunkEvent? ParseChunk(JsonElement root)
    {
        if (!root.TryGetProperty("content", out JsonElement content)
            || content.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return new ChunkEvent(content.GetString() ?? string.Empty);
    }

    private static DoneEvent? ParseDone(JsonElement root)
    {
        if (!TryGetInt(root, "inputTokens", out int input)
            || !TryGetInt(root, "outputTokens", out int output)
            || !TryGetInt(root, "totalTokens", out int total)
            || !TryGetDouble(root, "cost", out double cost)
            || !TryGetInt(root, "latencyMs", out int latencyMs)
            || !root.TryGetProperty("model", out JsonElement model)
            || model.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return new DoneEvent(input, output, total, cost, model.GetString() ?? string.Empty, latencyMs);
    }

    private static ErrorEvent? ParseError(JsonElement root)
    {
        if (!root.TryGetProperty("errorCode", out JsonElement code)
            || code.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("message", out JsonElement message)
            || message.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? requestId = null;
        if (root.TryGetProperty("requestId", out JsonElement rid)
            && rid.ValueKind == JsonValueKind.String)
        {
            requestId = rid.GetString();
        }

        return new ErrorEvent(
            code.GetString() ?? string.Empty,
            message.GetString() ?? string.Empty,
            requestId);
    }

    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Number)
        {
            return false;
        }
        return el.TryGetInt32(out value);
    }

    private static bool TryGetDouble(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Number)
        {
            return false;
        }
        return el.TryGetDouble(out value);
    }
}

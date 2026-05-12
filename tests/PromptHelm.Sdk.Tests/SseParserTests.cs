using PromptHelm.Sdk.Internal;

namespace PromptHelm.Sdk.Tests;

public class SseParserTests
{
    [Fact]
    public void Feed_SplitsByBlankLine()
    {
        var parser = new SseParser();
        var frames = parser.Feed("data: hello\n\ndata: world\n\n");
        Assert.Equal(2, frames.Count);
        Assert.Equal("hello", frames[0]);
        Assert.Equal("world", frames[1]);
    }

    [Fact]
    public void Feed_HandlesIncrementalChunks()
    {
        var parser = new SseParser();
        Assert.Empty(parser.Feed("data: he"));
        Assert.Empty(parser.Feed("llo\n"));
        var frames = parser.Feed("\n");
        Assert.Single(frames);
        Assert.Equal("hello", frames[0]);
    }

    [Fact]
    public void Feed_IgnoresCommentLines()
    {
        var parser = new SseParser();
        var frames = parser.Feed(": keep-alive\ndata: payload\n\n");
        Assert.Single(frames);
        Assert.Equal("payload", frames[0]);
    }

    [Fact]
    public void Feed_HandlesCRLF()
    {
        var parser = new SseParser();
        var frames = parser.Feed("data: alpha\r\n\r\n");
        Assert.Single(frames);
        Assert.Equal("alpha", frames[0]);
    }

    [Fact]
    public void ParseEvent_ChunkType()
    {
        var evt = SseParser.ParseEvent("""{"type":"chunk","content":"hi"}""");
        var chunk = Assert.IsType<ChunkEvent>(evt);
        Assert.Equal("hi", chunk.Content);
    }

    [Fact]
    public void ParseEvent_DoneType()
    {
        var evt = SseParser.ParseEvent(
            """{"type":"done","inputTokens":10,"outputTokens":20,"totalTokens":30,"cost":0.001,"model":"gpt-4o","latencyMs":250}""");
        var done = Assert.IsType<DoneEvent>(evt);
        Assert.Equal(10, done.InputTokens);
        Assert.Equal(20, done.OutputTokens);
        Assert.Equal(30, done.TotalTokens);
        Assert.Equal(0.001, done.Cost);
        Assert.Equal("gpt-4o", done.Model);
        Assert.Equal(250, done.LatencyMs);
    }

    [Fact]
    public void ParseEvent_ErrorType()
    {
        var evt = SseParser.ParseEvent(
            """{"type":"error","errorCode":"E_PROVIDER","message":"upstream","requestId":"r-1"}""");
        var err = Assert.IsType<ErrorEvent>(evt);
        Assert.Equal("E_PROVIDER", err.ErrorCode);
        Assert.Equal("upstream", err.Message);
        Assert.Equal("r-1", err.RequestId);
    }

    [Fact]
    public void ParseEvent_DoneSentinel_ReturnsNull()
    {
        Assert.Null(SseParser.ParseEvent("[DONE]"));
        Assert.Null(SseParser.ParseEvent(""));
    }

    [Fact]
    public void ParseEvent_MalformedJson_ReturnsNull()
    {
        Assert.Null(SseParser.ParseEvent("not-json"));
        Assert.Null(SseParser.ParseEvent("""{"type":"unknown"}"""));
    }
}

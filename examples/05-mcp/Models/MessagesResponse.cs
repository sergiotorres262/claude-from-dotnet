namespace ClaudeFromDotnet.E05Mcp.Models;

public record MessagesResponse(
    string Id,
    string Type,
    string Role,
    string Model,
    string? StopReason,
    string? StopSequence,
    ContentBlock[] Content,
    Usage Usage);

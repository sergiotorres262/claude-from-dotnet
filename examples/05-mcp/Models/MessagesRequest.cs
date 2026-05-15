namespace ClaudeFromDotnet.E05Mcp.Models;

// Request a POST /v1/messages con tools.
public record MessagesRequest(
    string Model,
    int MaxTokens,
    Message[] Messages,
    ToolDefinition[]? Tools = null,
    string? System = null);

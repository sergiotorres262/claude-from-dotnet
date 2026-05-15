namespace ClaudeFromDotnet.E04ToolUse.Models;

// Request a POST /v1/messages con tools activadas.
// Si Tools es null, la peticion se comporta como en los ejemplos 01-03.
// Doc: https://docs.claude.com/en/docs/agents-and-tools/tool-use
public record MessagesRequest(
    string Model,
    int MaxTokens,
    Message[] Messages,
    ToolDefinition[]? Tools = null,
    string? System = null);

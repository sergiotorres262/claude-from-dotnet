namespace ClaudeFromDotnet.E06Rag.Models;

// Respuesta de POST /v1/messages. Mismo shape que en los ejemplos 01-03.
public record MessagesResponse(
    string Id,
    string Type,
    string Role,
    string Model,
    string? StopReason,
    string? StopSequence,
    ContentBlock[] Content,
    Usage Usage);

public record ContentBlock(string Type, string? Text);

public record Usage(int InputTokens, int OutputTokens);

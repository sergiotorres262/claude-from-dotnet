namespace ClaudeFromDotnet.E04ToolUse.Models;

// Respuesta completa (no streaming) de POST /v1/messages.
// Mismo shape que en los ejemplos anteriores; ahora Content es siempre
// ContentBlock[] (puede mezclar bloques de texto y tool_use).
public record MessagesResponse(
    string Id,
    string Type,
    string Role,
    string Model,
    string? StopReason,
    string? StopSequence,
    ContentBlock[] Content,
    Usage Usage);

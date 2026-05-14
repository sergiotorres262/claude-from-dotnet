namespace ClaudeFromDotnet.E03StructuredOutput.Models;

// Response completa (no streaming) de POST /v1/messages.
// Mismo shape que en el ejemplo 01.
public record MessagesResponse(
    string Id,
    string Type,
    string Role,
    string Model,
    string? StopReason,
    string? StopSequence,
    ContentBlock[] Content,
    Usage Usage);

// Bloque de contenido devuelto. En este ejemplo solo nos importa Type="text".
public record ContentBlock(string Type, string? Text);

// Conteo de tokens consumidos en la llamada.
public record Usage(int InputTokens, int OutputTokens);

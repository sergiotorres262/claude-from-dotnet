namespace ClaudeFromDotnet.E02Streaming.Models;

// Forma minima de los eventos SSE de Anthropic. Solo deserializamos los
// campos que necesitamos en este ejemplo; el resto se ignora.
//
// Eventos posibles: message_start, content_block_start, content_block_delta,
// content_block_stop, message_delta, message_stop, ping, error.
// Ver docs/api-reference-resumen.md.
public record StreamEvent(
    string Type,
    int? Index = null,
    StreamDelta? Delta = null,
    StreamUsage? Usage = null,
    StreamMessage? Message = null);

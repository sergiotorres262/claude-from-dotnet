namespace ClaudeFromDotnet.E02Streaming.Models;

// Wrapper del campo "message" del evento message_start. Solo nos interesa
// el usage inicial (input_tokens) — el resto de metadata se ignora.
public record StreamMessage(StreamUsage? Usage = null);

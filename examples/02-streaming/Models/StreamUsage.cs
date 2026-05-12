namespace ClaudeFromDotnet.E02Streaming.Models;

// Tokens consumidos. En message_start trae los input_tokens; en message_delta
// llegan los output_tokens finales acumulados.
public record StreamUsage(int? InputTokens = null, int? OutputTokens = null);

namespace ClaudeFromDotnet.E02Streaming.Models;

// En content_block_delta: Type="text_delta" + Text=<chunk incremental>.
// En message_delta: StopReason="end_turn"|"max_tokens"|... (Text es null).
public record StreamDelta(
    string? Type = null,
    string? Text = null,
    string? StopReason = null);

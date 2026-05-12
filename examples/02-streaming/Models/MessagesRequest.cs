namespace ClaudeFromDotnet.E02Streaming.Models;

// Request a POST /v1/messages con stream=true.
// Doc: https://docs.claude.com/en/api/messages-streaming
public record MessagesRequest(
    string Model,
    int MaxTokens,
    Message[] Messages,
    bool Stream = true,
    string? System = null);

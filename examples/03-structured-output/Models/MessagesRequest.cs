namespace ClaudeFromDotnet.E03StructuredOutput.Models;

// Request a POST /v1/messages
// Doc: https://docs.claude.com/en/api/messages
public record MessagesRequest(
    string Model,
    int MaxTokens,
    Message[] Messages,
    string? System = null);

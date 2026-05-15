namespace ClaudeFromDotnet.E06Rag.Models;

// Request a POST /v1/messages. System es donde inyectamos el contexto RAG.
public record MessagesRequest(
    string Model,
    int MaxTokens,
    Message[] Messages,
    string? System = null);

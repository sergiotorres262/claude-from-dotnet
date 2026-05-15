namespace ClaudeFromDotnet.E06Rag.Embeddings;

// Respuesta de /v1/embeddings.
//   Data[i].Index   -> indice del input correspondiente (en peticiones batch).
//   Data[i].Embedding -> vector de floats (1536 dims para text-embedding-3-small).
public record EmbeddingsResponse(
    string Object,
    EmbeddingObject[] Data,
    string Model,
    EmbeddingsUsage? Usage);

public record EmbeddingObject(string Object, int Index, float[] Embedding);

public record EmbeddingsUsage(int PromptTokens, int TotalTokens);

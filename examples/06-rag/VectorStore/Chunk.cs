namespace ClaudeFromDotnet.E06Rag.VectorStore;

// Una unidad indexada en el vector store: el vector (embedding), el texto
// original que lo generó y la fuente (nombre de fichero) para poder
// citarla en la respuesta.
public record Chunk(float[] Vector, string Text, string Source);

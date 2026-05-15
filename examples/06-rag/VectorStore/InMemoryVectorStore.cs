namespace ClaudeFromDotnet.E06Rag.VectorStore;

// Vector store en memoria: una List<Chunk> y un Search por similitud coseno
// con O(N) por consulta. Suficiente para un puñado de chunks didacticos;
// en produccion se usa una vector DB de verdad (pgvector, Qdrant, etc.).
internal sealed class InMemoryVectorStore
{
    private readonly List<Chunk> _chunks = new();

    public int Count => _chunks.Count;

    public void Add(Chunk chunk) => _chunks.Add(chunk);

    public void AddRange(IEnumerable<Chunk> chunks) => _chunks.AddRange(chunks);

    public Chunk[] Search(float[] query, int topK)
    {
        return _chunks
            .Select(c => (chunk: c, score: CosineSimilarity(query, c.Vector)))
            .OrderByDescending(t => t.score)
            .Take(topK)
            .Select(t => t.chunk)
            .ToArray();
    }

    // Similitud coseno: dot(a,b) / (||a|| * ||b||). Rango [-1, 1]; 1 = misma
    // direccion = mismo "significado" segun el modelo de embeddings.
    //
    // text-embedding-3-small ya devuelve vectores normalizados (||v|| = 1),
    // asi que en la practica el dot product solo bastaria. Recalculamos la
    // norma por defensa: si en el futuro cambias de modelo y olvidas que
    // no normaliza, el codigo sigue siendo correcto.
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException(
                $"Vectores de distinta dimension: {a.Length} vs {b.Length}");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0f;
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}

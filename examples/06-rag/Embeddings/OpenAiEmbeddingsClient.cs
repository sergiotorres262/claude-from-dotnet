using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ClaudeFromDotnet.E06Rag.Embeddings;

// Cliente minimo de OpenAI Embeddings.
//   - Endpoint: POST https://api.openai.com/v1/embeddings
//   - Modelo:   text-embedding-3-small (1536 dims, ~$0.02 / millon de tokens)
//   - Auth:     header Authorization: Bearer <OPENAI_API_KEY>
//
// Soporta peticiones individuales (EmbedAsync) y batch (EmbedBatchAsync).
// Para indexar prefiere SIEMPRE batch: una sola llamada vs N.
//
// Doc: https://platform.openai.com/docs/api-reference/embeddings
internal sealed class OpenAiEmbeddingsClient : IDisposable
{
    private const string EmbeddingsUrl = "https://api.openai.com/v1/embeddings";
    private const string EmbeddingsModel = "text-embedding-3-small";

    private readonly HttpClient _http;

    public OpenAiEmbeddingsClient(string apiKey)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var batch = await EmbedBatchAsync(new[] { text });
        return batch[0];
    }

    public async Task<float[][]> EmbedBatchAsync(string[] texts)
    {
        if (texts.Length == 0) return Array.Empty<float[]>();

        var request = new EmbeddingsRequest(EmbeddingsModel, texts);
        using var response = await _http.PostAsJsonAsync(EmbeddingsUrl, request, JsonOpts.Default);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"OpenAI respondio {(int)response.StatusCode} {response.ReasonPhrase}. Cuerpo: {body}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<EmbeddingsResponse>(JsonOpts.Default);
        if (parsed?.Data is null || parsed.Data.Length == 0)
        {
            throw new InvalidOperationException("OpenAI devolvio una respuesta sin embeddings.");
        }

        // OpenAI no garantiza el orden de Data en respuestas batch — ordenamos
        // por Index para que cuadre con el orden del input.
        return parsed.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToArray();
    }

    public void Dispose() => _http.Dispose();
}

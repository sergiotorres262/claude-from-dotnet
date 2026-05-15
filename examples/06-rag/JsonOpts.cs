using System.Text.Json;

namespace ClaudeFromDotnet.E06Rag;

internal static class JsonOpts
{
    // Tanto Anthropic como OpenAI usan snake_case en sus payloads
    // (max_tokens, prompt_tokens, total_tokens, input_tokens, ...).
    // Reusamos un unico JsonSerializerOptions para los dos.
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

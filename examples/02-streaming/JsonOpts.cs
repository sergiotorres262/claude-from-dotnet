using System.Text.Json;

namespace ClaudeFromDotnet.E02Streaming;

internal static class JsonOpts
{
    // Anthropic usa snake_case en su API (max_tokens, stop_reason, input_tokens...).
    // Centralizamos las opciones para usarlas en serializar y deserializar.
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

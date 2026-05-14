using System.Text.Json;

namespace ClaudeFromDotnet.E03StructuredOutput;

internal static class JsonOpts
{
    // Para el envoltorio de la API de Anthropic: snake_case
    // (max_tokens, stop_reason, input_tokens, ...).
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Para el JSON que el modelo nos devuelve segun nuestro propio esquema:
    // camelCase (nombre, edad, email, empresa, cargo).
    // Es independiente del envoltorio de Anthropic — es schema NUESTRO.
    public static readonly JsonSerializerOptions Schema = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

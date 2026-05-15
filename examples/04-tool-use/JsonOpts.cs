using System.Text.Json;

namespace ClaudeFromDotnet.E04ToolUse;

internal static class JsonOpts
{
    // Anthropic usa snake_case (max_tokens, stop_reason, tool_use_id, input_schema, ...).
    // WhenWritingNull es clave para ContentBlock: cada subtipo (text, tool_use,
    // tool_result) tiene unos campos rellenos y otros null; los null no deben
    // viajar al wire.
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

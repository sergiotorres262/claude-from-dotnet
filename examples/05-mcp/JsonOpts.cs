using System.Text.Json;

namespace ClaudeFromDotnet.E05Mcp;

internal static class JsonOpts
{
    // Para el envoltorio de la API de Anthropic: snake_case
    // (max_tokens, stop_reason, tool_use_id, input_schema, ...).
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Para el wire de JSON-RPC 2.0 / MCP: camelCase
    // (jsonrpc, id, method, params, result, error, code, message, data,
    //  protocolVersion, clientInfo, inputSchema, isError, ...).
    public static readonly JsonSerializerOptions Mcp = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

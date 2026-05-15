using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E05Mcp.McpClient;

// Records del protocolo. Por excepcion a la regla "un record por archivo",
// los agrupamos aqui porque conceptualmente forman el "wire format" de MCP
// y se leen mejor juntos.

// ===== JSON-RPC 2.0 envelopes =====

// Envelope de peticion. Id null => notificacion (no espera respuesta).
public record JsonRpcRequest(
    string Jsonrpc,
    int? Id,
    string Method,
    JsonNode? Params);

// Envelope de respuesta. Tiene Result o Error, nunca los dos.
public record JsonRpcResponse(
    string Jsonrpc,
    int? Id,
    JsonNode? Result,
    JsonRpcError? Error);

// Estructura de error JSON-RPC. Code es el codigo numerico, ej -32601
// (Method not found), -32602 (Invalid params), etc.
public record JsonRpcError(int Code, string Message, JsonNode? Data = null);

// ===== MCP-specific payloads =====

// Tool tal como la expone un MCP server en tools/list. InputSchema es
// JSON Schema; lo mantenemos como JsonNode para reenviarlo a Anthropic
// sin tocarlo.
public record McpToolDefinition(
    string Name,
    string Description,
    JsonNode InputSchema);

// Resultado de tools/call. Content lleva los bloques (texto, imagen, etc.);
// nosotros solo usamos los de tipo "text". IsError indica error de
// ejecucion (no de protocolo) — los errores de protocolo viajan como
// JsonRpcError en el envelope.
public record McpCallToolResult(
    McpContentBlock[] Content,
    bool IsError);

// Bloque de contenido devuelto por una tool MCP. Hay mas tipos
// (image, audio, resource_link, resource) pero aqui solo manejamos text.
public record McpContentBlock(string Type, string? Text);

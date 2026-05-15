using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E04ToolUse.Tools;

// Contrato de una tool que el modelo puede invocar.
//
// - Name e InputSchema acaban en la ToolDefinition que mandamos a Anthropic.
// - ExecuteAsync recibe el JSON de "input" tal cual lo decide el modelo y
//   devuelve un string que se envia como tool_result.Content. Lo dejamos como
//   string a proposito: simple y suficiente para texto, numeros o JSON
//   serializado a mano.
public interface IClaudeTool
{
    string Name { get; }
    string Description { get; }
    JsonNode InputSchema { get; }
    Task<string> ExecuteAsync(JsonNode input);
}

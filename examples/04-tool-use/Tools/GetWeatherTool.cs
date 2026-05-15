using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E04ToolUse.Tools;

// Mock pedagogico: ignora la ciudad y devuelve siempre el mismo tiempo.
// En un caso real, aqui llamarias a una API meteorologica (AEMET, OpenWeather...).
internal sealed class GetWeatherTool : IClaudeTool
{
    // El input_schema es JSON Schema. Anthropic lo usa para validar lo que
    // el modelo envia en el campo "input" del tool_use.
    private static readonly JsonNode Schema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "ciudad": {
              "type": "string",
              "description": "Nombre de la ciudad de la que se quiere el tiempo."
            }
          },
          "required": ["ciudad"]
        }
        """)!;

    public string Name => "get_weather";
    public string Description => "Devuelve el tiempo actual en una ciudad dada.";
    public JsonNode InputSchema => Schema;

    public Task<string> ExecuteAsync(JsonNode input) =>
        Task.FromResult("Soleado, 22°C");
}

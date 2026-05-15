using System.Globalization;
using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E04ToolUse.Tools;

// Suma dos numeros. Util para que el modelo se acostumbre a delegar calculo
// en codigo determinista en lugar de hacerlo "a ojo" (donde a veces falla).
internal sealed class AddNumbersTool : IClaudeTool
{
    private static readonly JsonNode Schema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "a": { "type": "number", "description": "Primer numero." },
            "b": { "type": "number", "description": "Segundo numero." }
          },
          "required": ["a", "b"]
        }
        """)!;

    public string Name => "add_numbers";
    public string Description => "Suma dos numeros y devuelve el resultado.";
    public JsonNode InputSchema => Schema;

    public Task<string> ExecuteAsync(JsonNode input)
    {
        // GetValue<double> tolera tanto numeros enteros como decimales en el JSON.
        var a = input["a"]!.GetValue<double>();
        var b = input["b"]!.GetValue<double>();
        return Task.FromResult((a + b).ToString(CultureInfo.InvariantCulture));
    }
}

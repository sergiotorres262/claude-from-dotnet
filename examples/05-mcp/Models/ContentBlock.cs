using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E05Mcp.Models;

// Bloque polimorfico de contenido. Mismo shape que en el ejemplo 04.
// Subtipos:
//   Type = "text"        -> Text
//   Type = "tool_use"    -> Id, Name, Input
//   Type = "tool_result" -> ToolUseId, Content
public record ContentBlock(
    string Type,
    string? Text = null,
    string? Id = null,
    string? Name = null,
    JsonNode? Input = null,
    string? ToolUseId = null,
    string? Content = null);

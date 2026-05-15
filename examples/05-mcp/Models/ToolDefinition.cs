using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E05Mcp.Models;

// Definicion de una tool que se manda a Anthropic.
// En este ejemplo, los valores vienen de un McpToolDefinition tras pedir
// tools/list al MCP server.
public record ToolDefinition(
    string Name,
    string Description,
    JsonNode InputSchema);

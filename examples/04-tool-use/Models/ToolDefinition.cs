using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E04ToolUse.Models;

// Definicion de una tool que se manda en el campo "tools" de la request.
// Anthropic la combina con un system prompt automatico que le explica al
// modelo cuando puede invocarla.
//
// InputSchema es un JSON Schema (subset) describiendo los parametros de
// la tool. Lo modelamos como JsonNode para no acoplarnos a un shape concreto.
//
// Doc: https://docs.claude.com/en/docs/agents-and-tools/tool-use
public record ToolDefinition(
    string Name,
    string Description,
    JsonNode InputSchema);

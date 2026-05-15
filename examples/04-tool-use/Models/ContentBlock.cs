using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E04ToolUse.Models;

// Bloque polimorfico de contenido. Un solo record con campos opcionales,
// discriminados por el valor de Type. Al serializar, los campos null se
// omiten (ver JsonOpts.Default -> WhenWritingNull), asi que cada subtipo
// solo viaja con sus campos relevantes.
//
// Subtipos que usamos en este ejemplo:
//   Type = "text"        -> Text rellena el bloque (mensajes del user y
//                            respuestas del modelo en lenguaje natural).
//   Type = "tool_use"    -> Id, Name, Input (el modelo pide ejecutar una tool).
//   Type = "tool_result" -> ToolUseId, Content (le devolvemos el resultado).
//
// Doc: https://docs.claude.com/en/docs/agents-and-tools/tool-use
public record ContentBlock(
    string Type,
    string? Text = null,
    string? Id = null,
    string? Name = null,
    JsonNode? Input = null,
    string? ToolUseId = null,
    string? Content = null);

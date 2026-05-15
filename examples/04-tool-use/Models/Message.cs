namespace ClaudeFromDotnet.E04ToolUse.Models;

// A partir del ejemplo 04 (tool use), Content ya NO es un string suelto: siempre
// es un array de ContentBlock. Cada bloque puede ser texto, una invocacion de
// tool por parte del modelo (tool_use) o el resultado que le devolvemos
// (tool_result).
public record Message(string Role, ContentBlock[] Content);

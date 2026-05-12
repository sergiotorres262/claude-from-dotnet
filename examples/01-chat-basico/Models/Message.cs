namespace ClaudeFromDotnet.E01ChatBasico.Models;

// Bloque de conversación. En el ejemplo 01 usamos Content como string (texto plano).
// A partir del ejemplo 04 (tool use) se usa una versión con bloques tipados.
public record Message(string Role, string Content);

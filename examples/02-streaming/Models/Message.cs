namespace ClaudeFromDotnet.E02Streaming.Models;

// Mismo shape que en el ejemplo 01: rol + texto plano.
// A partir del ejemplo 04 (tool use) se usa una version con bloques tipados.
public record Message(string Role, string Content);

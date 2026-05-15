namespace ClaudeFromDotnet.E06Rag.Models;

// Vuelta al shape simple del ejemplo 01: Content como string.
// No necesitamos bloques polimorficos aqui — no hay tool use.
public record Message(string Role, string Content);

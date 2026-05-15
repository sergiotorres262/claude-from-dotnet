namespace ClaudeFromDotnet.E05Mcp.Models;

// Mismo shape que en el ejemplo 04: Content siempre es array de ContentBlock.
public record Message(string Role, ContentBlock[] Content);

namespace ClaudeFromDotnet.E03StructuredOutput.Schemas;

// Esquema del JSON que pedimos a Claude que devuelva.
// El system prompt en Program.cs describe esta forma en lenguaje natural y
// nosotros la deserializamos con JsonNamingPolicy.CamelCase:
//   Nombre  -> "nombre"
//   Edad    -> "edad"
//   Email   -> "email"
//   Empresa -> "empresa"
//   Cargo   -> "cargo"
public record Persona(
    string Nombre,
    int Edad,
    string Email,
    string Empresa,
    string? Cargo);

# claude-from-dotnet

Guía práctica para integrar la API de Anthropic Claude desde **C# / .NET**.

**Los 6 ejemplos están completos.** Roadmap futuro: streaming de tool calls, MCP server propio, caching, evaluaciones automáticas.

---

## ¿Qué vas a encontrar aquí?

Ejemplos reales, listos para producción, de cómo conectar tu backend .NET con la API de Claude:

- Llamada básica a `messages.create` desde C#
- Streaming de respuestas (Server-Sent Events) sin librerías raras
- Structured output con JSON Schema
- Tool use: que Claude llame a tus métodos
- MCP (Model Context Protocol) desde .NET
- RAG mínimo: embeddings + búsqueda + contexto
- Manejo de errores, reintentos y rate limits sin romper producción

Todo con `HttpClient` directo. Nada de SDKs no oficiales.

---

## ¿Para quién es esto?

Desarrolladores .NET que ya saben C# y quieren empezar a meter LLMs en su software **sin aprender Python ni cambiar de stack**.

---

## Estado

| Ejemplo | Estado |
|---|---|
| [01-chat-basico](examples/01-chat-basico/) | ✅ |
| [02-streaming](examples/02-streaming/) | ✅ |
| [03-structured-output](examples/03-structured-output/) | ✅ |
| [04-tool-use](examples/04-tool-use/) | ✅ |
| [05-mcp](examples/05-mcp/) | ✅ |
| [06-rag](examples/06-rag/) | ✅ |

---

## Empezar

```bash
git clone https://github.com/sergiotorres262/claude-from-dotnet.git
cd claude-from-dotnet/examples/01-chat-basico
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

Necesitas **.NET 8 SDK** instalado y una **API key de Anthropic** (la consigues en [console.anthropic.com](https://console.anthropic.com)).

---

## Autor

**Sergio Ojeda Torres** — Desarrollador .NET · Integración de IA en productos

[LinkedIn](https://www.linkedin.com/in/sergio-ojeda-torres/)

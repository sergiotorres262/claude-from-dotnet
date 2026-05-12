# claude-from-dotnet

Guía práctica para integrar la API de Anthropic Claude desde **C# / .NET**.

**En construcción** — los primeros ejemplos llegan en las próximas semanas.

---

## ¿Qué vas a encontrar aquí?

Ejemplos reales, listos para producción, de cómo conectar tu backend .NET con la API de Claude:

- Llamada básica a `messages.create` desde C#
- - Streaming de respuestas (Server-Sent Events) sin librerías raras
  - - Structured output con JSON Schema
    - - Tool use: que Claude llame a tus métodos
      - - MCP (Model Context Protocol) desde .NET
        - - RAG mínimo: embeddings + búsqueda + contexto
          - - Manejo de errores, reintentos y rate limits sin romper producción
           
            - Todo con `HttpClient` directo. Nada de SDKs no oficiales.
           
            - ---

            ## ¿Para quién es esto?

            Desarrolladores .NET que ya saben C# y quieren empezar a meter LLMs en su software **sin aprender Python ni cambiar de stack**.

            ---

            ## Estado

            | Ejemplo | Estado |
            |---|---|
            | 01-chat-basico | coming soon |
            | 02-streaming | coming soon |
            | 03-structured-output | coming soon |
            | 04-tool-use | coming soon |
            | 05-mcp | coming soon |
            | 06-rag | coming soon |

            ---

            ## Autor

            **Sergio Ojeda Torres** — Desarrollador .NET · Integración de IA en productos

            [LinkedIn](https://www.linkedin.com/in/sergio-ojeda-torres/)
            

# claude-from-dotnet

Guía práctica de ejemplos C# / .NET para integrar la API de Anthropic Claude usando HttpClient directo, sin SDKs no oficiales.

## Commands

- `dotnet build` — Compila toda la solution
- `dotnet run --project examples/01-chat-basico` — Corre un ejemplo concreto
- `dotnet format` — Aplica el estilo de código del .editorconfig
- `dotnet sln add examples/NN-nuevo-ejemplo/NN-nuevo-ejemplo.csproj` — Añade un nuevo ejemplo a la solution

## Tech Stack

.NET 8 + C# 12 + HttpClient (built-in) + System.Text.Json (built-in) + dotenv.net (única dependencia común). Sin frameworks web. Cada ejemplo es una console app independiente.

## Architecture

### Directory Structure
- `examples/NN-nombre/` — Un ejemplo autónomo por carpeta. Cada uno tiene su `.csproj`, `Program.cs`, `Models/`, `.env.example`, `README.md`.
- `docs/` — Setup, glosario, resumen API. Documentación transversal.
- `shared/ClaudeFromDotnet.Shared/` — Solo si la duplicación entre ejemplos se vuelve insostenible (no antes del ejemplo 04).
- `.github/workflows/build.yml` — CI que verifica que toda la solution compila.

### Data Flow
1. Cada ejemplo carga `.env` con `DotEnv.Load()` al arranque
2. Lee `ANTHROPIC_API_KEY` con `Environment.GetEnvironmentVariable`
3. Crea `HttpClient` con `BaseAddress = https://api.anthropic.com` y headers `x-api-key` + `anthropic-version`
4. Serializa request con `System.Text.Json`
5. POST a `/v1/messages`
6. Deserializa response (o lee stream SSE en el ejemplo 02)
7. Imprime resultado por consola

### Key Patterns
- **Cada ejemplo es totalmente self-contained.** Un lector puede copiar UNA carpeta de `examples/` fuera del repo y seguirá funcionando.
- **Records inmutables** para todos los DTOs de la API. Nada de clases con setters.
- **Nullable reference types habilitados** en todos los `.csproj`. Tratar warnings como errores.
- **HttpClient como singleton** dentro de cada Program.cs (no inyectado, son apps de un solo uso).
- **Async/await en todas las llamadas a la API.** Nunca `.Result` ni `.Wait()`.

## Code Organization Rules

1. **`Program.cs` corto.** `Main` no más de 30 líneas. Si crece, extraer métodos estáticos en el mismo archivo o clases en `Services/`.
2. **Un record por archivo.** Modelos en `Models/`. Cada record en su archivo.
3. **File-scoped namespaces.** `namespace ClaudeFromDotnet.E01ChatBasico;` al principio del archivo, sin llaves.
4. **No comments obvios.** Comentar el por qué, no el qué.
5. **README al lado del código.** Cada `examples/NN-*/` tiene su `README.md` con la plantilla estándar.

## Environment Variables

| Variable | Descripción |
|----------|-------------|
| `ANTHROPIC_API_KEY` | API key de Anthropic. Obligatoria en todos los ejemplos. |
| `ANTHROPIC_MODEL` | Modelo (opcional, default `claude-sonnet-4-6`). |
| `OPENAI_API_KEY` | Solo ejemplo 06 (embeddings). |

## Reglas No Negociables

1. **NUNCA un SDK no oficial de Anthropic.** Solo HttpClient directo. La pedagogía del repo depende de esto.
2. **NUNCA commitear `.env`.** Está en `.gitignore` raíz. `.env.example` sí, `.env` no.
3. **NUNCA hardcodear API keys.** Ni en strings, ni en comentarios, ni en tests. Si pasa, rotar la key inmediatamente en console.anthropic.com.
4. **Cada NuGet añadido necesita justificación** en el README del ejemplo. Si se puede hacer con built-in, se hace con built-in.
5. **README de cada ejemplo es obligatorio** y sigue la plantilla de Sección 6 del blueprint.
6. **El idioma del repo es español.** Code comments, READMEs y commit messages en español. Excepción: los nombres de tipos de la API de Anthropic se mantienen en inglés (`MessagesRequest`, `StopReason`, etc.).

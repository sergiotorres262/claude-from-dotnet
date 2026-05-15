# 05 - MCP (Model Context Protocol)

> Claude invoca tools que no están definidas en tu código — vienen de un **MCP server** externo (filesystem, GitHub, Postgres, etc.). Reutilizamos el loop del ejemplo 04 pero las tools se descubren y ejecutan vía stdio + JSON-RPC 2.0.

## Qué aprendes

- Qué es **MCP** y por qué Anthropic lo impulsa como protocolo abierto para conectar LLMs con sistemas externos.
- Cómo lanzar un MCP server como proceso hijo y hablar con él por **stdio** (`stdin` / `stdout`).
- Cómo implementar un cliente JSON-RPC 2.0 minimalista en .NET con `System.Diagnostics.Process` + `ConcurrentDictionary<int, TaskCompletionSource<JsonNode>>`.
- El handshake MCP: `initialize` → respuesta → `notifications/initialized` → `tools/list` → `tools/call`.
- Cómo mapear `McpToolDefinition` → `ToolDefinition` de Anthropic (mismo `input_schema`, distintas conventions de naming JSON).
- Por qué `stdio` y no HTTP/SSE para servers locales.

## Prerrequisitos

- **.NET 8 SDK**
- **Node.js + npx** instalados y en el PATH. Comprueba con `node --version` y `npx --version`. La primera ejecución `npx -y @modelcontextprotocol/server-filesystem ...` descarga el paquete (~10 s).

## Cómo correrlo

```bash
cd examples/05-mcp
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

La carpeta `sandbox/` se crea automáticamente al lado del proyecto y se siembra con 3 ficheros de ejemplo. Está en `.gitignore`.

## Salida esperada (resumida)

```
[sandbox] D:\...\examples\05-mcp\sandbox
[mcp-stderr] Secure MCP Filesystem Server running on stdio
[mcp] conectado a secure-filesystem-server v1.0.0

[mcp] N tools descubiertas:
  - read_file
  - write_file
  - list_directory
  - ... (varias mas)

--- iteracion 1 ---
  Claude pide MCP tool 'list_directory' con args: {"path":"..."}
  -> resultado MCP (200 chars): [FILE] notas-reunion.md ...

--- iteracion 2 ---
  Claude pide MCP tool 'read_file' con args: {"path":".../notas-reunion.md"}
  -> resultado MCP (180 chars): # Notas de la reunión ...
  ...

--- iteracion 3 ---

--- Respuesta final de claude-sonnet-4-6 ---
En la carpeta hay 3 ficheros: notas de reunión, roadmap Q3 y un readme...
---
Tokens input: 4500 / output: 220
```

## El flujo

```
┌───────────────────┐                    ┌──────────────────────────┐
│  Anthropic API    │                    │  proceso hijo: npx       │
│  (HTTPS)          │                    │  @modelcontextprotocol/  │
│                   │                    │  server-filesystem       │
└────────┬──────────┘                    └────────┬─────────────────┘
         │                                        │
         │ POST /v1/messages                      │ stdin  (peticiones)
         │ con tools[]                            │ stdout (respuestas)
         │ (HttpClient)                           │ JSON-RPC 2.0, una
         │                                        │ por linea, UTF-8
         ▼                                        ▼
         ┌────────────────────────────────────────────┐
         │       tu programa .NET 8 (05-mcp)          │
         │                                            │
         │   HttpClient                  McpClient    │
         │   (Program.cs)  ◀── pivote ─▶ (McpClient/) │
         │                                            │
         │   loop tool_use ↔ tool_result heredado del │
         │   ejemplo 04. Cada tool_use de Claude se   │
         │   traduce a un tools/call por stdio.       │
         └────────────────────────────────────────────┘
```

## Cómo funciona (paso a paso)

1. **Arranque y sandbox.** [Program.cs](Program.cs) calcula la carpeta del proyecto a partir de `AppContext.BaseDirectory`, crea `sandbox/` si no existe y siembra 3 archivos `.md` / `.txt` de ejemplo (idempotente: si ya están, no toca nada).

2. **Lanzar el MCP server.** El constructor de [McpClient](McpClient/McpClient.cs) hace `Process.Start("npx", "-y", "@modelcontextprotocol/server-filesystem", sandboxPath)`. En Windows lo envuelve en `cmd.exe /c` porque `npx` es un `.cmd` que `CreateProcess` no resuelve directo. Redirige `stdin`/`stdout`/`stderr`.

3. **Handshake MCP.** `McpClient.InitializeAsync()`:
   - Manda `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"claude-from-dotnet","version":"0.1.0"}}}` y espera respuesta.
   - Tras recibirla, manda la notificación `{"jsonrpc":"2.0","method":"notifications/initialized"}` (sin `id`, no espera respuesta).

4. **Reader loop.** En cuanto arranca el proceso, una `Task.Run` lee líneas de `stdout`. Cada línea es un envelope JSON-RPC completo. Empareja `id` con la `TaskCompletionSource<JsonNode>` pendiente en un `ConcurrentDictionary`. Las notificaciones del server (sin `id`) las ignoramos. Otro `Task.Run` reenvía `stderr` a `Console.Error` con prefijo `[mcp-stderr]`.

5. **`tools/list`.** Pedimos la lista, deserializamos a `McpToolDefinition[]` con `JsonOpts.Mcp` (camelCase). Imprimimos los nombres.

6. **Mapeo a Anthropic.** Cada `McpToolDefinition` se convierte directamente en una `ToolDefinition` de Anthropic (mismo `name`, misma `description`, mismo `input_schema`). Como JSON Schema es JSON Schema, lo pasamos verbatim como `JsonNode`. La única diferencia es la **convención de naming**: MCP usa `inputSchema` (camelCase), Anthropic `input_schema` (snake_case). Por eso hay dos `JsonOpts` distintos en [JsonOpts.cs](JsonOpts.cs).

7. **Loop tool_use ↔ tool_result.** Idéntico al ejemplo 04: POST → si `stop_reason="tool_use"` ejecutamos las tools en orden y devolvemos los `tool_result` en un único mensaje user. Diferencia única: el "execute" ahora es `await mcp.CallToolAsync(name, input)`, que internamente manda `tools/call` y aplana los bloques `text` del result en un string.

8. **Shutdown.** `await using` dispara `McpClient.DisposeAsync` al salir: cierra `stdin` para señalizar shutdown, da 2 s al proceso para terminar limpio, lo mata si se atasca.

## MCP servers de referencia para probar

| Server | Comando | Env vars / args | Para qué |
|---|---|---|---|
| filesystem | `npx -y @modelcontextprotocol/server-filesystem <path>` | path absoluto como argumento | Listar, leer, escribir archivos del path indicado |
| github | `npx -y @modelcontextprotocol/server-github` | `GITHUB_PERSONAL_ACCESS_TOKEN` | Issues, PRs, repos vía API de GitHub |
| postgres | `npx -y @modelcontextprotocol/server-postgres <connection-string>` | connection string como argumento | Queries de solo-lectura sobre una base Postgres |
| memory | `npx -y @modelcontextprotocol/server-memory` | — | Knowledge graph en memoria, útil para sesiones con estado |
| fetch | `uvx mcp-server-fetch` (Python, no Node) | — | Descarga URLs y las convierte a markdown |

Para cambiar de server en este ejemplo, edita la línea del `new McpClient(...)` en [Program.cs](Program.cs).

## Por qué `stdio` y no HTTP/SSE

MCP define varios transportes en su spec (stdio, HTTP streaming, etc.). Para **servers locales** stdio es el más simple:

- No hay puerto que reservar ni TLS que configurar.
- El proceso hijo muere si el padre muere — cero zombies por accidente.
- Mismo modelo que LSP (Language Server Protocol), del que MCP toma muchas ideas.
- El framing es trivial: una línea = un mensaje JSON-RPC.

HTTP/SSE tiene sentido para servers **remotos** o **multitenant**, donde varios clientes comparten el mismo server. No es el caso de esta demo.

## Errores comunes

- **`No se pudo lanzar el proceso MCP: npx`** → `node` / `npx` no están en el PATH del proceso. Comprueba con `node --version` y `npx --version`. En Windows, asegúrate de que el `npm` global está en el PATH.
- **El server tarda en arrancar la primera vez** → `npx -y` descarga el paquete del registry de npm. Es normal que tarde 10-20 s en la primera ejecución y sea instantáneo después.
- **`[mcp-stdout-invalid-json] ...`** → algo en el server está escribiendo a `stdout` que no es JSON-RPC (logs sueltos, banners). Eso ROMPE el protocolo: stdio MCP exige que solo viajen JSON-RPC messages por stdout, los logs van a stderr. Si pasa, abre issue en el repo del server.
- **`JSON-RPC error -32601: Method not found`** → estás llamando a un método que el server no implementa. Confirma la spec MCP y los métodos que anuncia en `capabilities` la respuesta de `initialize`.
- **`JSON-RPC error -32602: Invalid params`** → el `arguments` que mandaste no cuadra con el `inputSchema` de la tool. Mira lo que Claude está enviando en el log `Claude pide MCP tool '...' con args: ...`.
- **Cuelgue al cerrar** → si el server no responde a `stdin.Close()` y no termina en 2 s, `DisposeAsync` lo mata con `Kill(entireProcessTree: true)`.
- **El proceso npx queda zombie tras Ctrl+C** → `Kill(entireProcessTree: true)` cubre la cadena `cmd → npx → node → server`. Si aún ves procesos huérfanos, mátalos a mano y abre issue.

## Siguiente paso

El ejemplo `06-rag` cierra la serie con **RAG mínimo**: embeddings (vía OpenAI), búsqueda por similitud coseno en memoria y inyección del contexto en el system prompt. Sin MCP esta vez — el siguiente paso natural en producción sería un MCP server propio que exponga la búsqueda RAG como tool.

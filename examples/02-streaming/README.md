# 02 - Streaming SSE

> Recibe la respuesta de Claude token a token usando Server-Sent Events, en vez de esperar al JSON completo.

## Qué aprendes

- Qué es SSE (Server-Sent Events) y cómo lo usa Anthropic para streaming.
- Cómo evitar que `HttpClient` buferee el cuerpo entero: `HttpCompletionOption.ResponseHeadersRead`.
- Cómo parsear el formato `event: ... \n data: ... \n\n` línea a línea sin librerías de terceros.
- Cómo escribir un `IAsyncEnumerable<T>` con `yield return` en un método `async`.
- Qué eventos manda Anthropic (`message_start`, `content_block_delta`, `message_delta`, `message_stop`, `ping`) y qué información sacar de cada uno.

## Cómo correrlo

```bash
cd examples/02-streaming
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

Necesitas **.NET 8 SDK** instalado. Comprueba con `dotnet --list-sdks`.

## Salida esperada

```
--- Streaming de claude-sonnet-4-6 ---
Server-Sent Events (SSE) es un estándar HTTP donde el servidor empuja
eventos al cliente sobre una única conexión... [el texto aparece a trozos]
---
stop_reason: end_turn
Tokens input: 42 / output: 130
```

Lo importante: al correrlo verás el texto **apareciendo gradualmente**, no de golpe. Eso es el efecto streaming.

## Cómo funciona (paso a paso)

1. **Request con `stream: true` y `Accept: text/event-stream`.** El [MessagesRequest](Models/MessagesRequest.cs) lleva ahora un `Stream = true`. En el header se cambia `application/json` (del ejemplo 01) por `text/event-stream`. Con eso le decimos a Anthropic que queremos SSE.

2. **`HttpCompletionOption.ResponseHeadersRead` al hacer el POST.** Es la línea clave en [Program.cs](Program.cs) (método `StreamAsync`). Sin esta opción, `HttpClient` espera a tener el cuerpo entero en memoria antes de devolverte el control — perdiendo todo el efecto streaming. Con ella, en cuanto llegan los headers ya puedes leer el `Stream`.

3. **`SseReader.ReadAsync`** ([Sse/SseReader.cs](Sse/SseReader.cs)) es un parser manual del formato SSE:
   - Lee línea a línea con `StreamReader.ReadLineAsync()`.
   - `event: <nombre>` guarda el tipo del evento siguiente.
   - `data: <json>` acumula el payload.
   - Línea en blanco = fin del evento → emite un `SseEvent(EventType, Data)`.
   - Implementado como `async IAsyncEnumerable<SseEvent>` con `yield return`, así el consumidor itera con `await foreach` sin cargar todo en memoria.

4. **Bucle de eventos en `ConsumeStreamAsync`.** Por cada `SseEvent`, deserializamos el `Data` a [StreamEvent](Models/StreamEvent.cs) y switcheamos por `Type`:
   - `message_start` → leemos `input_tokens` del usage inicial.
   - `content_block_delta` → escribimos el `delta.text` por consola con `Console.Write` (sin newline).
   - `message_delta` → guardamos `stop_reason` y `output_tokens` finales.
   - `message_stop` → fin del stream (no extraemos nada).
   - `ping` → keep-alive de Anthropic, lo ignoramos.

5. **Resumen final.** Al terminar el `await foreach`, salto de línea + `stop_reason` + tokens consumidos.

## Por qué SSE y no WebSockets

SSE es **unidireccional** (servidor → cliente), sobre HTTP normal, sin handshake especial. Para streaming de tokens de un LLM es perfecto: el cliente manda una petición y el servidor le va empujando trozos. WebSockets sería overkill — ese protocolo brilla cuando hay tráfico full-duplex (chat de varios, juegos en tiempo real, colaboración en vivo).

Ventajas concretas para una API como Anthropic:

- Pasa por cualquier proxy/CDN sin configuración especial.
- Reconectar es trivial (no hay sesión binaria que mantener).
- Cabe en un `curl` o `HttpClient` sin librerías extra.

## Errores comunes

- **No se ve nada hasta el final.** Te olvidaste de `HttpCompletionOption.ResponseHeadersRead` o el output está siendo buffereado por la consola del IDE. Prueba a correrlo desde una terminal real (PowerShell, bash, zsh).
- **`401 Unauthorized`** → la key del `.env` no es válida o está vacía. Genera otra en [console.anthropic.com](https://console.anthropic.com).
- **`Error parseando un evento SSE`** → o el formato cambió en Anthropic, o el `JsonOpts` no tiene `SnakeCaseLower` (Anthropic manda `input_tokens`, no `inputTokens`).
- **El texto aparece de golpe en lugar de gradual** → puede ser tu terminal haciendo flush por bloque grande. Sube `max_tokens` y pide una respuesta más larga para que sea evidente.
- **`429 Too Many Requests`** → rate limit. Espera 30 segundos y reintenta. Manejo robusto con reintentos llegará en el ejemplo 06 con Polly.

## Siguiente paso

El ejemplo `03-structured-output` introduce cómo forzar a Claude a devolver JSON deserializable a un `record` de C#. Pasamos de procesar texto libre a procesar datos tipados.

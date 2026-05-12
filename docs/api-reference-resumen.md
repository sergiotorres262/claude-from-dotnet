# Resumen de la API de Anthropic

Este es un resumen propio, en español, de los endpoints y estructuras que se usan en el repo. La doc oficial completa está en [docs.claude.com/en/api](https://docs.claude.com/en/api) — si hay discrepancia, manda la oficial.

---

## Autenticación y headers comunes

Toda llamada a la API necesita estos headers:

| Header | Valor | Notas |
|---|---|---|
| `x-api-key` | `sk-ant-api03-...` | Tu API key. Se lee de `.env` en los ejemplos. |
| `anthropic-version` | `2023-06-01` | Versión del API. Si Anthropic la cambia, este string es el único que toca cambiar en `Program.cs`. |
| `content-type` | `application/json` | Obligatorio en POST. `HttpClient.PostAsJsonAsync` lo añade solo. |

Base URL: `https://api.anthropic.com`.

---

## `POST /v1/messages`

Es prácticamente **el único endpoint** que usa el repo. Sirve para chat básico, streaming, structured output, tool use y MCP — cambia el body, no el endpoint.

### Request body (campos relevantes)

| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `model` | string | Sí | Ej: `claude-sonnet-4-6`. Lista en [models](https://docs.claude.com/en/docs/about-claude/models). |
| `max_tokens` | int | Sí | Tope de tokens de la respuesta. Si te quedas corto, `stop_reason` = `max_tokens`. |
| `messages` | array | Sí | Historial de la conversación. Cada item es `{ role: "user" \| "assistant", content: string \| ContentBlock[] }`. |
| `system` | string | No | System prompt. Define rol, tono, restricciones. |
| `stream` | bool | No | `true` → respuesta como SSE (ejemplo 02). Default `false`. |
| `temperature` | number | No | `0.0` - `1.0`. Más alto = más creativo. Default depende del modelo. |
| `tools` | array | No | Tools que el modelo puede usar (ejemplo 04+). |
| `tool_choice` | object | No | Fuerza al modelo a usar (o no) una tool concreta. |
| `stop_sequences` | string[] | No | Strings que cortan la generación si aparecen. |

### Response body (no streaming)

```json
{
  "id": "msg_01XFDU...",
  "type": "message",
  "role": "assistant",
  "model": "claude-sonnet-4-6",
  "content": [
    { "type": "text", "text": "Soy Claude..." }
  ],
  "stop_reason": "end_turn",
  "stop_sequence": null,
  "usage": {
    "input_tokens": 40,
    "output_tokens": 83
  }
}
```

| Campo | Notas |
|---|---|
| `id` | Identificador único de la respuesta. |
| `type` | Siempre `"message"` para este endpoint. |
| `role` | Siempre `"assistant"` (el modelo nunca devuelve mensajes de `user`). |
| `model` | El modelo que efectivamente respondió (suele coincidir con el solicitado). |
| `content` | Array de bloques. Hoy hay tres tipos: `text`, `tool_use`, `thinking` (este último solo en modelos con extended thinking). |
| `stop_reason` | Ver [glosario](glosario.md#stop-reason). |
| `usage` | Tokens consumidos. Lo que te van a cobrar. |

### Mapeo a tipos C# (ejemplo 01)

Anthropic usa **snake_case** en JSON. Lo manejamos con `JsonNamingPolicy.SnakeCaseLower` centralizado en `JsonOpts.cs`. Los `record` de C# se mantienen en `PascalCase`:

| JSON (Anthropic) | C# (record) |
|---|---|
| `max_tokens` | `MaxTokens` |
| `stop_reason` | `StopReason` |
| `input_tokens` | `InputTokens` |
| `output_tokens` | `OutputTokens` |
| `tool_use` | `ToolUse` (cuando aparezca en ejemplos posteriores) |

---

## Streaming sobre `/v1/messages` (ejemplo 02)

Con `stream: true` en el body **y** `Accept: text/event-stream` en los headers, la API te devuelve **Server-Sent Events** en vez del JSON completo.

Formato del stream:

```
event: message_start
data: {"type":"message_start","message":{...}}

event: content_block_start
data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

event: content_block_delta
data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hola"}}

event: content_block_delta
data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" Claude"}}

event: content_block_stop
data: {"type":"content_block_stop","index":0}

event: message_delta
data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":83}}

event: message_stop
data: {"type":"message_stop"}
```

Eventos clave:

| Evento | Para qué |
|---|---|
| `message_start` | Llega una vez, con la metadata inicial del mensaje. |
| `content_block_start` | Empieza un bloque (texto o tool_use). |
| `content_block_delta` | Trocito incremental. Lo concatenas según llega. |
| `content_block_stop` | Cierre del bloque. |
| `message_delta` | Cambios a nivel mensaje (típicamente `stop_reason` y `usage` final). |
| `message_stop` | Cierre del stream. |
| `ping` | Keep-alive. Ignóralo. |
| `error` | Error en medio del stream. Cierra y propaga. |

El [ejemplo 02](../examples/02-streaming/) lo parsea línea a línea sin librerías.

---

## Tool use sobre `/v1/messages` (ejemplo 04)

En el request añades:

```json
"tools": [
  {
    "name": "get_weather",
    "description": "Devuelve el tiempo actual de una ciudad",
    "input_schema": {
      "type": "object",
      "properties": {
        "city": { "type": "string" }
      },
      "required": ["city"]
    }
  }
]
```

Si el modelo decide usarla, la respuesta trae un bloque tipo `tool_use`:

```json
{
  "content": [
    { "type": "text", "text": "Voy a consultar el tiempo." },
    {
      "type": "tool_use",
      "id": "toolu_01A09q...",
      "name": "get_weather",
      "input": { "city": "Madrid" }
    }
  ],
  "stop_reason": "tool_use"
}
```

Tu código ejecuta la tool, monta un mensaje de `role: "user"` con un bloque `tool_result`:

```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_01A09q...",
      "content": "Soleado, 22ºC"
    }
  ]
}
```

Y vuelves a llamar a `/v1/messages` con el historial actualizado. Repite hasta `stop_reason: end_turn`.

---

## Errores

Códigos HTTP que vas a ver:

| Código | Significado | Qué hacer |
|---|---|---|
| `400` | Request mal formado | Problema tuyo. La respuesta incluye `error.message` con detalle. |
| `401` | API key inválida o vacía | Revisar `.env`, generar otra key en console.anthropic.com. |
| `403` | Sin permiso para ese modelo | Tu cuenta puede no tener acceso a ese modelo. Prueba `claude-sonnet-4-6`. |
| `429` | Rate limit | Esperar y reintentar. Mira el header `retry-after`. Con backoff exponencial está la cosa. |
| `500` | Error interno de Anthropic | Reintentar con backoff. |
| `529` | Sobrecarga temporal | Reintentar con backoff. |

Formato del body de error:

```json
{
  "type": "error",
  "error": {
    "type": "invalid_request_error",
    "message": "Field required: max_tokens"
  }
}
```

En el [ejemplo 01](../examples/01-chat-basico/) lanzamos `HttpRequestException` con el body completo si no es 2xx. A partir del ejemplo 02 introducimos reintentos manuales; en el ejemplo 06 se sustituye por Polly.

---

## Qué endpoints NO usa el repo

Para evitar dudas, lo que aparece en la doc oficial pero no toca en estos ejemplos:

- `POST /v1/messages/batches` — batch API (asíncrono, descuento del 50%). Útil en producción para cargas no interactivas.
- `POST /v1/messages/count_tokens` — contar tokens antes de mandar. Útil para validar `max_tokens`.
- Endpoints de **Files API**, **Embeddings**, **Vision** específicos — los usamos solo donde aporten al ejemplo (vision quizá en un futuro 07; embeddings los hacemos contra OpenAI en el 06).

# 01 - Chat básico

> Primera llamada a la API de Anthropic Claude desde .NET con `HttpClient` directo. Sin SDKs.

## Qué aprendes

- Cómo autenticar contra Anthropic con los headers `x-api-key` y `anthropic-version`
- Cómo serializar/deserializar el JSON de `/v1/messages` con `System.Text.Json`
- Cómo cargar la API key desde un `.env` con `dotenv.net`
- Cómo modelar `MessagesRequest` / `MessagesResponse` con `record` de C# 12

## Cómo correrlo

```bash
cd examples/01-chat-basico
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

Necesitas **.NET 8 SDK** instalado. Comprueba con `dotnet --list-sdks`.

## Salida esperada

```
--- Respuesta de claude-sonnet-4-6 (stop_reason: end_turn) ---
Soy Claude, un asistente de IA de Anthropic. Un dev .NET me llama haciendo
POST a https://api.anthropic.com/v1/messages con los headers x-api-key y
anthropic-version, enviando JSON con model, max_tokens y messages.
---
Tokens input: 42 / output: 60
```

(El texto exacto cambia entre llamadas. Lo importante es que veas una respuesta del modelo y el conteo de tokens.)

## Cómo funciona (paso a paso)

1. `DotEnv.Load()` carga el `.env` del directorio actual en `Environment` variables.
2. Leemos `ANTHROPIC_API_KEY` y `ANTHROPIC_MODEL` (este último con default a `claude-sonnet-4-6`).
3. Creamos un `HttpClient` con `BaseAddress = https://api.anthropic.com` y los headers obligatorios (`x-api-key`, `anthropic-version: 2023-06-01`).
4. Construimos un `MessagesRequest` con un único mensaje de rol `user`.
5. `PostAsJsonAsync("/v1/messages", request, JsonOpts.Default)` serializa con snake_case (Anthropic usa snake_case en su API).
6. Si la respuesta no es 2xx, leemos el body como string y lanzamos `HttpRequestException` con todo el detalle.
7. Si es 2xx, deserializamos a `MessagesResponse` e imprimimos los bloques de texto y los tokens consumidos.

## Errores comunes

- **`401 Unauthorized`** — la key del `.env` no es válida o está vacía. Genera una nueva en [console.anthropic.com](https://console.anthropic.com) → Settings → API Keys.
- **`400 Bad Request` con `model: ...`** — el modelo no existe. Mira la lista en [docs.claude.com/en/docs/about-claude/models](https://docs.claude.com/en/docs/about-claude/models).
- **`429 Too Many Requests`** — rate limit. Espera unos segundos y reintenta. En el ejemplo 02 vemos cómo manejar esto bien.
- **Respuesta vacía** — revisa que `max_tokens` sea razonable (1024 va de sobra para este ejemplo).
- **`Falta ANTHROPIC_API_KEY en .env`** — no has copiado `.env.example` a `.env` o lo has dejado vacío.

## Siguiente paso

El ejemplo `02-streaming` introduce respuestas SSE: en vez de esperar a la respuesta completa, los tokens llegan según se generan. Útil para chats en tiempo real.

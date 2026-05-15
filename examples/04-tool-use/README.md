# 04 - Tool use

> Claude decide qué funciones de tu código C# debe invocar para responder. Tú las ejecutas, le devuelves el resultado, y el modelo continúa.

## Qué aprendes

- Cómo definir **tools** con un `input_schema` JSON Schema y mandarlas en el campo `tools` de la request.
- Cómo Claude responde con `stop_reason: "tool_use"` y bloques `tool_use` en lugar de texto cuando decide llamar a una función.
- Cómo ejecutar la tool en C# y devolver el resultado en un único mensaje de rol `user` con bloques `tool_result`.
- Por qué el **orden** de los `tool_result` importa y por qué van todos en **un solo** mensaje.
- Cómo implementar el loop `tool_use ↔ tool_result` con un tope defensivo de iteraciones.
- Por qué `Content` deja de ser un `string` y pasa a ser `ContentBlock[]` a partir de este ejemplo.

## Cómo correrlo

```bash
cd examples/04-tool-use
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

Necesitas **.NET 8 SDK** instalado.

## Salida esperada

```
--- iteracion 1 ---
  Claude pide tool 'get_weather' con input: {"ciudad":"Madrid"}
  -> resultado: Soleado, 22°C
  Claude pide tool 'add_numbers' con input: {"a":17,"b":25}
  -> resultado: 42

--- iteracion 2 ---

--- Respuesta final de claude-sonnet-4-6 ---
En Madrid hace soleado y 22°C. Y 17 + 25 = 42.
---
Tokens input: 1234 / output: 60
```

(Los tokens y el wording exacto cambian entre llamadas. Lo importante es ver dos iteraciones: la primera ejecuta tools, la segunda cierra con `end_turn`.)

## El loop `tool_use` ↔ `tool_result`

```
   ┌───────────────────────────┐
   │  user: "que tiempo hace…" │
   └─────────────┬─────────────┘
                 │ POST /v1/messages  (con tools[])
                 ▼
   ┌───────────────────────────┐
   │  assistant                │     stop_reason = tool_use
   │  Content: [               │   ◀──────────────────────────┐
   │    tool_use(get_weather), │                              │
   │    tool_use(add_numbers)  │                              │
   │  ]                        │                              │
   └─────────────┬─────────────┘                              │
                 │                                            │
                 ▼                                            │
   ┌───────────────────────────┐                              │
   │ TU código C# ejecuta cada │                              │
   │ tool_use en orden y arma  │                              │
   │ los tool_result.          │                              │
   └─────────────┬─────────────┘                              │
                 │                                            │
                 ▼                                            │
   ┌───────────────────────────┐                              │
   │  user (un solo mensaje)   │                              │
   │  Content: [               │                              │
   │    tool_result(id1, ok),  │                              │
   │    tool_result(id2, 42)   │                              │
   │  ]                        │                              │
   └─────────────┬─────────────┘                              │
                 │ POST /v1/messages  (mismo tools[])         │
                 ▼                                            │
   ┌───────────────────────────┐                              │
   │  assistant                │  stop_reason = tool_use? ───┘
   │  texto o mas tool_use     │
   └─────────────┬─────────────┘
                 │ end_turn? → imprimir y salir
                 ▼
              (fin)
```

## Cómo funciona (paso a paso)

1. **Definición de las tools.** Hay dos en [Tools/](Tools/): `GetWeatherTool` (mock, devuelve `"Soleado, 22°C"`) y `AddNumbersTool` (suma `a + b`). Cada una implementa [`IClaudeTool`](Tools/IClaudeTool.cs) con `Name`, `Description`, `InputSchema` (un `JsonNode` con JSON Schema) y `ExecuteAsync(JsonNode input)`.

2. **Registro y mando a Anthropic.** En [Program.cs](Program.cs), `BuildToolRegistry` crea un `Dictionary<string, IClaudeTool>` por nombre. A partir del diccionario construimos un `ToolDefinition[]` que va en el campo `tools` de la `MessagesRequest`.

3. **Primer POST.** Mandamos el user prompt como `ContentBlock(Type: "text", Text: …)`. Anthropic, viendo las tools disponibles, decide invocar `get_weather` y `add_numbers` y responde con `stop_reason = "tool_use"` y dos `ContentBlock(Type: "tool_use", Id, Name, Input)` en `Content`.

4. **Ejecución local.** `ExecuteToolUsesAsync` recorre los bloques del assistant **en orden**, busca la tool en el diccionario por nombre, ejecuta `ExecuteAsync(input)` y arma un `ContentBlock(Type: "tool_result", ToolUseId: id, Content: resultado)`.

5. **Mensaje de rol `user` con todos los `tool_result`.** Los metemos en un **único** `Message("user", tool_results)` y lo añadimos al historial. **No** se mandan en mensajes separados: la API exige que todos los resultados de los `tool_use` de un mismo turno asistente vengan juntos en el siguiente turno user.

6. **Segundo POST.** Mandamos el historial completo (user inicial + assistant con tool_use + user con tool_result). Claude lee los resultados y normalmente responde con un bloque `text` y `stop_reason = "end_turn"`. Imprimimos el texto y salimos.

7. **Tope defensivo.** Si el loop no converge a `end_turn` en `MaxIterations = 5` rondas (el modelo se pone a invocar tools en bucle, raro pero posible), salimos con error en vez de quemar tokens.

## Por qué los `tool_result` van en UN solo mensaje

La API de Anthropic exige que los turnos de la conversación alternen estrictamente `user → assistant → user → assistant → …`. Si una respuesta del assistant trae N bloques `tool_use`, el **siguiente** mensaje user debe llevar exactamente N bloques `tool_result`, todos en su `Content`. Mandarlos en mensajes separados rompe la alternancia y devuelve un `400`.

## Por qué el orden de los `tool_result` importa

Aunque cada `tool_result` se asocia a su `tool_use` por `tool_use_id`, la API espera los resultados **en el mismo orden** que los `tool_use` originales. Es la convención documentada y evita ambigüedades cuando el modelo razona sobre los resultados. Saltarse el orden a veces "funciona", a veces no — no merece la pena depender de eso.

## Por qué `Content` ya no es `string`

En los ejemplos 01-03 el `Content` de un `Message` era un `string` plano: el modelo siempre devolvía texto y nosotros mandábamos texto. Con tools, ese contrato se rompe: una respuesta del assistant puede ser `[text, tool_use, tool_use]`, y un turno user puede ser `[tool_result, tool_result]`. Por eso a partir del 04 modelamos `Content` como `ContentBlock[]` siempre, con un único `record` polimórfico de campos opcionales (`Text`, `Id`, `Name`, `Input`, `ToolUseId`, `Content`) discriminado por `Type`. Los campos `null` se omiten al serializar (`JsonOpts.Default` con `WhenWritingNull`), así cada bloque viaja solo con sus campos relevantes.

## Errores comunes

- **`400 Bad Request: tool_use_id does not match…`** → el orden de los `tool_result` no coincide con el de los `tool_use`, o se te coló un `id` mal copiado. Revisa `ExecuteToolUsesAsync`.
- **`400 Bad Request: missing tool_result`** → la respuesta del assistant pidió N tools y mandaste menos de N resultados.
- **El modelo no invoca tools y responde directo** → la `Description` de la tool no es clara, o el user prompt no requiere de la tool. Refina ambos.
- **`Exception en ExecuteAsync`** → el modelo te mandó un `input` que no cuadra con tu schema. Captura, devuelve un mensaje de error como `tool_result.Content` y deja que Claude lo lea y se corrija.
- **Loop infinito** → el tope `MaxIterations` te salva. Si saltó, revisa por qué el modelo no llega a `end_turn`: a veces son tools que se llaman mutuamente sin parar.

## Siguiente paso

El ejemplo `05-mcp` lleva este loop un paso más allá: en vez de definir las tools en C# a mano, las descubrimos en tiempo de arranque desde un **MCP server** externo (filesystem, GitHub, etc.) y reusamos exactamente este loop para invocarlas.

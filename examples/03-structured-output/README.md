# 03 - Structured output

> Le pides a Claude que devuelva JSON con un esquema concreto y lo deserializas a un `record` de C# inmutable.

## Qué aprendes

- Cómo guiar al modelo con un system prompt para que devuelva **solo JSON** (sin markdown ni texto extra).
- Cómo deserializar la salida a un `record` con `System.Text.Json` + `JsonNamingPolicy.CamelCase`.
- Por qué un system prompt **no es 100 % fiable** y qué hacer cuando el modelo se rebela: capturar `JsonException` y loggear el texto crudo.
- Limitaciones de este enfoque frente a `tool_choice`, que es la alternativa robusta y se ve en el ejemplo 04.

## Cómo correrlo

```bash
cd examples/03-structured-output
cp .env.example .env
# editar .env y meter tu ANTHROPIC_API_KEY
dotnet run
```

Necesitas **.NET 8 SDK** instalado. Comprueba con `dotnet --list-sdks`.

## Salida esperada

```
--- Persona extraida (claude-sonnet-4-6, stop_reason: end_turn) ---
Nombre  : Laura Martín
Edad    : 35
Email   : laura.martin@acme.es
Empresa : Acme Iberia
Cargo   : Head of Product
---
Tokens input: 240 / output: 70
```

Los conteos de tokens varían entre llamadas. Lo importante es que el record `Persona` aparezca con los campos extraídos de la firma de email del [Program.cs](Program.cs).

## Cómo funciona (paso a paso)

1. **System prompt con esquema literal.** En [Program.cs](Program.cs) hay una constante `SystemPrompt` que describe el JSON exacto que esperamos: `{"nombre": "...", "edad": ..., "email": "...", "empresa": "...", "cargo": "... | null"}`. Le dejamos claras dos cosas: nada de texto extra y nada de envolverlo en bloques de código markdown.

2. **User prompt = firma de email en castellano.** `"Un saludo, Laura Martín — Head of Product en Acme Iberia · laura.martin@acme.es · 35 años"`. El modelo tiene que destilar de ahí los cinco campos.

3. **POST a `/v1/messages` con los mismos headers que el ejemplo 01** (`x-api-key`, `anthropic-version: 2023-06-01`, `Accept: application/json`). Aquí no hay streaming.

4. **Dos `JsonSerializerOptions` distintos** (ver [JsonOpts.cs](JsonOpts.cs)):
   - `JsonOpts.Default` → `SnakeCaseLower`. Para serializar la request y deserializar el envoltorio de Anthropic (`max_tokens`, `stop_reason`, `input_tokens`...).
   - `JsonOpts.Schema` → `CamelCase`. Para deserializar el JSON que **el modelo** devuelve dentro de `Content[0].Text`. Como el esquema es nuestro, lo elegimos camelCase para que case con los nombres `PascalCase` del record [Persona](Schemas/PersonaSchema.cs).

5. **Extracción y deserialización.** Sacamos `response.Content[0].Text`, le hacemos `Trim()` y pasamos a `JsonSerializer.Deserialize<Persona>(rawText, JsonOpts.Schema)`. Si el modelo se portó bien, sale un `Persona` poblado.

6. **Si el modelo se rebela**, capturamos `JsonException`, imprimimos el texto crudo por `stderr` y salimos con exit code `4`. Sin intentar recuperarnos — la pedagogía del ejemplo es justo esa: enseñar que el system prompt es frágil para esto.

## Por qué este enfoque NO es robusto

- Modelos pequeños o con temperatura alta a veces ignoran las instrucciones.
- El modelo puede envolver el JSON en ` ```json ... ``` ` aunque le digas que no.
- Puede añadir un "Aquí tienes el JSON:" antes.
- Puede devolver `"35"` en vez de `35` para la edad y rompes la deserialización a `int`.
- Puede inventarse claves nuevas o cambiar `cargo` por `puesto`.

En todos esos casos este ejemplo **falla rápido** y te imprime el raw, en vez de pretender que todo va bien.

## `tool_choice` como alternativa robusta (ejemplo 04)

Anthropic documenta una técnica donde defines una "tool" con su `input_schema` (JSON Schema de verdad) y luego con `tool_choice: { "type": "tool", "name": "..." }` **fuerzas** al modelo a invocarla. Como la única forma de responder es llamar a la tool, el modelo no puede no devolver JSON estructurado — y además el JSON se valida contra el schema antes de llegarte. Lo vemos en el ejemplo `04-tool-use`.

Resumen: **`system prompt` es ergonómico para prototipos. `tool_choice` es para producción.**

## Errores comunes

- **`JsonException: ... is not a JSON token`** → el modelo metió texto extra antes/después del JSON. Mira el raw que imprime el programa por `stderr`.
- **El modelo envolvió en ` ```json `** → mismo síntoma. Solución correcta: usar `tool_choice` (ejemplo 04). Solución pirata: hacer `Trim('`').Replace("```json","").Replace("```","")` — funciona pero es frágil.
- **Edad como string `"35"`** → fuerza con `tool_choice` o cambia `Edad` a `string` y parsea aparte.
- **`401 Unauthorized`** → revisa `ANTHROPIC_API_KEY` en `.env`.
- **El modelo se inventa una empresa que no aparece en el texto** → es alucinación clásica de LLM. Reduce con `temperature: 0` (se ve en ejemplos posteriores) y validaciones de negocio en C# antes de aceptar el resultado.

## Siguiente paso

El ejemplo `04-tool-use` introduce el loop `tool_use ↔ tool_result`: definimos tools en C#, Claude las invoca cuando las necesita, ejecutamos código local, devolvemos resultado, Claude sigue. Y de paso resolvemos el problema de structured output de este ejemplo con `tool_choice`.

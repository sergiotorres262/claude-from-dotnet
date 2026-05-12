# Glosario

Términos que vas a ver una y otra vez en el repo y en la documentación de Anthropic. Cada entrada es lo justo para que entiendas el ejemplo, no un tratado.

---

## Token

Unidad mínima de texto que procesa un LLM. **No es lo mismo que una palabra**: puede ser un trozo (`"computa"` + `"ción"`), un signo (`"."`), una palabra entera o incluso un solo caracter. Como regla de bolsillo, en inglés ≈ 4 caracteres ≈ ¾ de palabra. En español tira a parecido.

Importa por dos razones:

1. **Te cobran por token** (input + output, con tarifas distintas).
2. Cada modelo tiene un **límite de tokens por petición** (`max_tokens`) y un **límite de contexto total** (los del prompt + los de la respuesta).

En el repo, todos los ejemplos imprimen al final `input_tokens` y `output_tokens` para que veas qué cuesta cada llamada.

---

## Modelo

La versión concreta de Claude que llamas. En el momento de escribir esto los principales son:

- `claude-opus-4-7` — el más capaz, más caro y más lento.
- `claude-sonnet-4-6` — el balance recomendado por defecto. Es el que usamos en los ejemplos.
- `claude-haiku-4-5` — el más barato y rápido, menos capaz.

La lista oficial está en [docs.claude.com/en/docs/about-claude/models](https://docs.claude.com/en/docs/about-claude/models). El nombre del modelo se manda como string en el campo `model` de cada petición.

---

## System prompt

Instrucciones que pones al modelo **antes** de que empiece la conversación. No es un mensaje del usuario: es el contexto que define el rol, el tono, las restricciones, el formato de salida, etc.

Ejemplo: "Eres un asistente que responde solo en español, en menos de 3 frases, sin usar emojis".

En la API de Anthropic se envía en el campo `system` (string) del request a `/v1/messages`, separado de `messages` (que es el historial de la conversación).

---

## Streaming

Modo en el que el modelo te devuelve la respuesta **token a token según la genera**, en vez de esperar a terminar y mandártela completa. Se transporta sobre **SSE (Server-Sent Events)**: una conexión HTTP que va emitiendo eventos de tipo `event: ... \n data: ...\n\n`.

Útil para chats en vivo (el usuario ve el texto aparecer) y para abortar la generación pronto si no te interesa.

En el repo lo ves en el [ejemplo 02](../examples/02-streaming/) (cuando esté), parseando el SSE manualmente para que entiendas qué hace por debajo.

---

## Tool use

Mecanismo por el que **el modelo le pide a tu código que ejecute una función** y le devuelva el resultado. Tú declaras las herramientas disponibles (nombre, descripción, JSON schema de los argumentos) en el campo `tools` del request. Claude decide si la necesita y, si la pide, te devuelve un bloque `tool_use` con los argumentos. Tu código la ejecuta y le manda un `tool_result`. Loop hasta que termine.

Ejemplos: consultar el tiempo de una ciudad, leer/escribir archivos, llamar a tu base de datos, lanzar un cálculo.

En el repo se ve en el [ejemplo 04](../examples/04-tool-use/) (cuando esté).

---

## MCP (Model Context Protocol)

Protocolo abierto, definido por Anthropic, para conectar modelos a **servidores de herramientas externas** sin tener que cablear cada integración a mano. Un MCP server expone tools (y opcionalmente resources, prompts) que cualquier cliente compatible —incluida la API de Claude— puede descubrir y usar.

Idea: en vez de implementar cada tool dentro de tu app, lanzas un MCP server (por ejemplo `@modelcontextprotocol/server-filesystem`) y tu cliente se conecta vía stdio o HTTP. Las tools del server aparecen automáticamente.

Especificación oficial: [modelcontextprotocol.io](https://modelcontextprotocol.io). En el repo: [ejemplo 05](../examples/05-mcp/) (cuando esté).

---

## RAG (Retrieval-Augmented Generation)

Patrón en el que **antes** de mandar la pregunta al modelo, **buscas en una base de conocimiento propia** (documentos, FAQs, base de datos vectorial) los trozos relevantes y se los pegas al system prompt como contexto. Así el modelo responde basándose en tus datos, no solo en lo que aprendió en entrenamiento.

Pipeline típico:

1. **Indexación (una vez):** trocear tus documentos en chunks → calcular embeddings de cada chunk → guardarlos en un vector store.
2. **Consulta (cada pregunta):** calcular embedding de la pregunta → buscar los N chunks más similares en el vector store → meterlos en el system prompt → pedir respuesta al modelo.

Cuándo NO usar RAG: si todo tu contexto cabe en la ventana del modelo (Claude maneja contextos enormes), a veces es más simple meter todo de golpe.

En el repo: [ejemplo 06](../examples/06-rag/) (cuando esté), con un vector store en memoria.

---

## Embedding

Vector de números (típicamente 256-3072 dimensiones) que representa el **significado** de un trozo de texto. Dos textos parecidos en significado tienen vectores cercanos en distancia coseno; dos textos muy distintos están lejos.

Lo generas con un modelo de embeddings (separado del LLM de chat). Anthropic **no tiene** modelo propio de embeddings — para el ejemplo 06 usamos `text-embedding-3-small` de OpenAI por practicidad.

---

## Vector similarity (similitud coseno)

Forma estándar de medir cómo de parecidos son dos embeddings. Calcula el coseno del ángulo entre los dos vectores: rango `[-1, 1]`, donde `1` = idénticos, `0` = no relacionados, `-1` = opuestos.

Fórmula:

```
cos(A, B) = (A · B) / (||A|| · ||B||)
```

En el ejemplo 06 lo implementamos a mano sobre `float[]` para que veas que no hay magia.

---

## SSE (Server-Sent Events)

Estándar HTTP para que un servidor empuje eventos a un cliente sobre una sola conexión. Cada evento es texto plano con formato:

```
event: content_block_delta
data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hola"}}

event: message_stop
data: {"type":"message_stop"}
```

Líneas vacías separan eventos. El cliente lee línea a línea y parsea. La API de Anthropic usa SSE para el modo streaming. Lo ves en el [ejemplo 02](../examples/02-streaming/).

---

## Stop reason

Por qué terminó el modelo de generar. Los más habituales:

- `end_turn` — terminó normalmente, ya dijo lo que quería decir.
- `max_tokens` — se quedó sin tokens del límite que pediste. Indica que la respuesta está truncada — sube `max_tokens` o partela en varias llamadas.
- `tool_use` — pidió ejecutar una tool. Tu código debe ejecutarla y seguir el loop.
- `stop_sequence` — encontró una de las stop sequences que le pasaste.

Lo devuelve la API en `stop_reason` de la respuesta. En el ejemplo 01 lo imprimimos en pantalla para que veas el más común (`end_turn`).

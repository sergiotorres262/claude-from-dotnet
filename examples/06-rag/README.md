# 06 - RAG mínimo

> Indexamos un corpus de archivos `.md` con embeddings de OpenAI, buscamos los chunks más relevantes para cada pregunta por similitud coseno, y se los inyectamos a Claude como contexto en el system prompt.

## Qué aprendes

- Qué es **RAG** (Retrieval-Augmented Generation) y por qué se usa.
- Cómo llamar a la API de **embeddings** de OpenAI con `HttpClient` directo.
- Cómo implementar un **vector store** en memoria con búsqueda por **similitud coseno**.
- Cómo construir un **system prompt con contexto** recuperado y citar las fuentes.
- Cuándo NO usar RAG.

## Prerrequisitos

- **.NET 8 SDK**
- **API key de Anthropic** — para llamar a Claude.
- **API key de OpenAI** — para generar embeddings con `text-embedding-3-small`. Cuesta del orden de **$0.02 por millón de tokens**; este ejemplo consume <1000 tokens por ejecución.

## Cómo correrlo

```bash
cd examples/06-rag
cp .env.example .env
# editar .env y meter ANTHROPIC_API_KEY y OPENAI_API_KEY
dotnet run
```

## Salida esperada (resumida)

```
[index] 3 archivos -> 9 chunks. Generando embeddings...
[index] indexados 9 chunks de 3 archivos.

======================================================================
PREGUNTA: ¿Cuántos días de vacaciones tengo al año?
======================================================================
Fuentes recuperadas (top 3):
  - politica-vacaciones.md
  - manual-onboarding.md
  - faq-soporte.md

Tienes 23 días laborables de vacaciones por año natural, más los festivos
oficiales de tu Comunidad Autónoma.

======================================================================
PREGUNTA: ¿Cómo me configuro la VPN el primer día?
======================================================================
Fuentes recuperadas (top 3):
  - faq-soporte.md
  - manual-onboarding.md
  - politica-vacaciones.md

Si la VPN no te conecta, abre el cliente Tailscale, pulsa "Logout"...
```

## ¿Qué es RAG?

**Retrieval-Augmented Generation** es un patrón con tres pasos:

1. **Indexado.** Cortas un corpus de documentos en trozos pequeños ("chunks") y conviertes cada chunk en un vector numérico (embedding) con un modelo entrenado para que textos similares queden cerca en ese espacio vectorial. Guardas los vectores en un store.
2. **Recuperación.** Cuando llega una pregunta, conviertes la pregunta en un embedding con el mismo modelo y buscas los chunks cuyo vector está más cerca (top-K, ej. 3-5).
3. **Generación con contexto.** Pasas esos chunks al LLM como contexto en el system prompt y le pides que responda usando solo esa información. Así el modelo cita tu corpus en vez de alucinar.

Es la forma estándar de hacer que un LLM hable con confianza sobre datos privados (manuales internos, base de conocimiento de soporte, documentación técnica) sin reentrenar nada.

## Similitud coseno

Mide el ángulo entre dos vectores. La fórmula es:

```
cos(a, b) = dot(a, b) / (||a|| * ||b||)
```

donde `dot` es el producto escalar y `||v||` es la norma euclídea (raíz cuadrada de la suma de cuadrados). El resultado vive en `[-1, 1]`:

- `1` → vectores apuntando exactamente en la misma dirección (textos con el mismo "significado" según el modelo de embeddings).
- `0` → vectores ortogonales (sin relación semántica).
- `-1` → direcciones opuestas (muy raro con embeddings de texto modernos).

**Intuición geométrica:** dos textos sobre el mismo tema apuntan al mismo "rincón" del espacio vectorial. El coseno mide cuánto coinciden esos rincones, ignorando la magnitud del vector (que para texto rara vez es informativa).

El código vive en [VectorStore/InMemoryVectorStore.cs](VectorStore/InMemoryVectorStore.cs) como `CosineSimilarity(float[], float[])`. Es 12 líneas, sin librerías.

## Cuándo NO usar RAG

RAG es la herramienta correcta cuando el corpus es **grande, cambia con frecuencia o contiene datos sensibles** que no quieres mandar entero en cada llamada. Pero hay tres escenarios donde es contraproducente:

1. **El corpus cabe en el context window.** `claude-sonnet-4-6` tiene 200k tokens de contexto — esos 3 archivos del ejemplo caben sin despeinarse. Si todo tu corpus son 30 páginas estables, mete las 30 páginas en cada llamada y olvídate de pipelines. Es más simple, no requiere infraestructura y el modelo ve TODA la información en lugar del top-K.
2. **El coste de embeddings supera el de meter todo en cada llamada.** Si haces 10 consultas al día sobre 50 documentos, RAG te ahorra muy poco y añade complejidad. Hazlo cuando hagas miles de consultas o el corpus sea de miles de páginas.
3. **La actualidad de los datos es crítica.** RAG requiere reindexar cuando los documentos cambian. Si tus datos cambian cada minuto (precios en vivo, stocks, marcadores deportivos), una **tool** que consulta la fuente en tiempo real (ejemplo 04 / 05) es mejor.

## Producción

Lo de este ejemplo es **didáctico**. En un sistema real cambias dos cosas:

- **Vector store de verdad.** [pgvector](https://github.com/pgvector/pgvector) si ya tienes Postgres, [Qdrant](https://qdrant.tech), [Pinecone](https://www.pinecone.io) o [Weaviate](https://weaviate.io) si quieres dedicado. Te dan búsqueda aproximada en O(log N), filtros por metadatos, persistencia y replicación.
- **Embeddings con mejor relación precio/calidad según el momento.** A día de hoy las opciones razonables son:
  - **OpenAI `text-embedding-3-large`** — 3072 dims, mejor calidad que `-small`, ~6x más caro.
  - **Voyage AI `voyage-3` / `voyage-3-large`** — punteros en benchmarks, especialmente para retrieval.
  - **Cohere `embed-multilingual-v3.0`** — fuerte en multilenguaje.
  - **Modelos open-source** (BGE, E5, GTE) si quieres self-hostear con `Ollama` o similar.

Compara periódicamente: el ranking cambia cada 6-12 meses. Sitios como [MTEB](https://huggingface.co/spaces/mteb/leaderboard) ayudan a decidir.

Otras mejoras típicas de producción:

- **Chunking inteligente** (semántico, no por `\n\n` ciego): mantener encabezados, respetar fronteras de oraciones.
- **Rerank** del top-K con un cross-encoder antes de pasarlo al LLM.
- **Hybrid search**: combinar coseno con BM25 (búsqueda léxica) para queries con nombres propios o IDs.
- **Citas con offsets** dentro del chunk para que la UI pueda hacer "ir a la fuente".

## Errores comunes

- **`OpenAI respondio 401`** → la `OPENAI_API_KEY` del `.env` no es válida. Genera otra en [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
- **`OpenAI respondio 429`** → te han limitado por rate. Espera 30 s y vuelve a probar; o sube tu tier en la consola.
- **El programa indexa 0 chunks** → la carpeta `data/` está vacía o los `.md` no tienen párrafos con al menos 50 caracteres. Comprueba `MinChunkChars` en [Program.cs](Program.cs).
- **Las fuentes recuperadas no tienen sentido para la pregunta** → tu chunking es demasiado grueso (todo el archivo en un solo chunk = embedding promedio inútil) o demasiado fino (chunks de 1 frase pierden contexto). Apunta a 200-500 tokens por chunk como regla de pulgar.
- **`No tengo esa información`** → en realidad **es éxito**: el modelo respeta el system prompt y no inventa. Significa que tu chunking o el corpus no cubre la pregunta. Es la respuesta correcta.
- **Diferencias de dimensión entre vectores** → estás mezclando modelos de embeddings. Reindexa todo con el mismo modelo.

## Siguiente paso

Los 6 ejemplos están cerrados. Como ejercicio extra, prueba:

1. Conectar este vector store a un MCP server propio (ejemplo 05) para exponer la búsqueda RAG como tool.
2. Cambiar a `pgvector` con Postgres real.
3. Añadir streaming (ejemplo 02) a la respuesta para que el chat se vea fluido.
4. Implementar rerank con `Cohere Rerank` antes de pasar los chunks al modelo.

namespace ClaudeFromDotnet.E06Rag.Embeddings;

// Request a POST https://api.openai.com/v1/embeddings.
// Input puede ser un string o un string[]: si pasamos un array, OpenAI nos
// devuelve los embeddings en una sola llamada (eficiente para indexar).
//
// Lo tipamos como object para que System.Text.Json serialice segun el tipo
// runtime: string -> "x", string[] -> ["x","y"]. Asi el mismo record sirve
// para los dos modos.
//
// Doc: https://platform.openai.com/docs/api-reference/embeddings/create
public record EmbeddingsRequest(string Model, object Input);

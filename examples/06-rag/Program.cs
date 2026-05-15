using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClaudeFromDotnet.E06Rag.Embeddings;
using ClaudeFromDotnet.E06Rag.Models;
using ClaudeFromDotnet.E06Rag.VectorStore;
using dotenv.net;

namespace ClaudeFromDotnet.E06Rag;

internal static class Program
{
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    // Tamano minimo (chars) de un parrafo para indexarlo. Filtra titulares
    // sueltos del Markdown que no aportan al recuperar.
    private const int MinChunkChars = 50;
    private const int TopK = 3;

    // Preguntas predefinidas para que la salida sea reproducible.
    private static readonly string[] Preguntas =
    [
        "¿Cuántos días de vacaciones tengo al año?",
        "¿Cómo me configuro la VPN el primer día?",
        "¿Quién me asignan como mentor y cuánto dura la mentoría?",
        "¿Cuál es el preaviso mínimo para pedir vacaciones?",
    ];

    private static async Task<int> Main()
    {
        DotEnv.Load();

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(anthropicKey))
        {
            Console.Error.WriteLine("Falta ANTHROPIC_API_KEY en .env.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(openaiKey))
        {
            Console.Error.WriteLine("Falta OPENAI_API_KEY en .env (necesaria para los embeddings).");
            return 1;
        }

        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";

        using var anthropic = BuildAnthropicClient(anthropicKey);
        using var embeddings = new OpenAiEmbeddingsClient(openaiKey);
        var store = new InMemoryVectorStore();

        try
        {
            await IndexAsync(store, embeddings);
            await QueryLoopAsync(anthropic, embeddings, store, model);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"\nError HTTP: {ex.Message}");
            return 2;
        }
    }

    private static HttpClient BuildAnthropicClient(string apiKey)
    {
        var http = new HttpClient { BaseAddress = new Uri(AnthropicBaseUrl) };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    // ===== Indexado =====

    private static async Task IndexAsync(
        InMemoryVectorStore store,
        OpenAiEmbeddingsClient embeddings)
    {
        var dataDir = ResolveDataDir();
        var files = Directory.GetFiles(dataDir, "*.md");
        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No hay archivos .md en {dataDir}. Comprueba la carpeta data/.");
        }

        var pendingChunks = new List<(string Text, string Source)>();
        foreach (var path in files)
        {
            var source = Path.GetFileName(path);
            var raw = await File.ReadAllTextAsync(path);
            foreach (var chunk in ChunkByBlankLines(raw))
            {
                pendingChunks.Add((chunk, source));
            }
        }

        Console.WriteLine($"[index] {files.Length} archivos -> {pendingChunks.Count} chunks. Generando embeddings...");

        var vectors = await embeddings.EmbedBatchAsync(pendingChunks.Select(c => c.Text).ToArray());

        for (var i = 0; i < pendingChunks.Count; i++)
        {
            store.Add(new Chunk(vectors[i], pendingChunks[i].Text, pendingChunks[i].Source));
        }

        Console.WriteLine($"[index] indexados {store.Count} chunks de {files.Length} archivos.");
    }

    // Chunking simple: divide por dobles saltos de linea. Cada parrafo = un
    // chunk. Filtra trozos muy cortos (lineas de titulo Markdown sueltas).
    private static IEnumerable<string> ChunkByBlankLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var parts = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length >= MinChunkChars) yield return trimmed;
        }
    }

    // Localiza data/ subiendo desde bin/Debug/net8.0/ hasta el csproj.
    private static string ResolveDataDir()
    {
        var projectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        return Path.Combine(projectDir, "data");
    }

    // ===== Consulta =====

    private static async Task QueryLoopAsync(
        HttpClient anthropic,
        OpenAiEmbeddingsClient embeddings,
        InMemoryVectorStore store,
        string model)
    {
        foreach (var pregunta in Preguntas)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"PREGUNTA: {pregunta}");
            Console.WriteLine(new string('=', 70));

            var queryVector = await embeddings.EmbedAsync(pregunta);
            var topChunks = store.Search(queryVector, TopK);

            Console.WriteLine($"Fuentes recuperadas (top {topChunks.Length}):");
            foreach (var c in topChunks)
            {
                Console.WriteLine($"  - {c.Source}");
            }

            var systemPrompt = BuildSystemPrompt(topChunks);
            var answer = await AskAnthropicAsync(anthropic, model, systemPrompt, pregunta);
            Console.WriteLine();
            Console.WriteLine(answer);
        }
    }

    private static string BuildSystemPrompt(Chunk[] chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres un asistente de soporte interno de Acme Iberia. Responde usando ÚNICAMENTE la información del contexto siguiente. Si la respuesta no está en el contexto, di literalmente \"No tengo esa información\".");
        sb.AppendLine();
        sb.AppendLine("CONTEXTO:");
        foreach (var c in chunks)
        {
            sb.AppendLine($"[Fuente: {c.Source}]");
            sb.AppendLine(c.Text);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static async Task<string> AskAnthropicAsync(
        HttpClient anthropic,
        string model,
        string systemPrompt,
        string userPrompt)
    {
        var request = new MessagesRequest(
            Model: model,
            MaxTokens: 512,
            Messages: new[] { new Message("user", userPrompt) },
            System: systemPrompt);

        using var httpResponse = await anthropic.PostAsJsonAsync(MessagesEndpoint, request, JsonOpts.Default);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic respondio {(int)httpResponse.StatusCode}. Cuerpo: {body}");
        }

        var parsed = await httpResponse.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts.Default);
        if (parsed is null) return "(respuesta vacia)";

        var sb = new StringBuilder();
        foreach (var block in parsed.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                sb.AppendLine(block.Text);
            }
        }
        return sb.ToString().TrimEnd();
    }
}

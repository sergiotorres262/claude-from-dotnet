using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaudeFromDotnet.E05Mcp.McpClient;
using ClaudeFromDotnet.E05Mcp.Models;
using dotenv.net;

namespace ClaudeFromDotnet.E05Mcp;

internal static class Program
{
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxIterations = 5;

    private const string UserPrompt =
        "Lista los archivos de tu carpeta de trabajo y resúmeme qué hay en cada uno.";

    private static async Task<int> Main()
    {
        DotEnv.Load();

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Falta ANTHROPIC_API_KEY en .env (copia .env.example a .env y mete tu key).");
            return 1;
        }

        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";
        var sandboxPath = EnsureSandbox();
        Console.WriteLine($"[sandbox] {sandboxPath}");

        using var http = BuildHttpClient(apiKey);

        await using var mcp = new McpClient.McpClient(
            "npx", "-y", "@modelcontextprotocol/server-filesystem", sandboxPath);

        try
        {
            await mcp.InitializeAsync();
            var anthropicTools = await DiscoverToolsAsync(mcp);

            var messages = new List<Message>
            {
                new("user", new[] { new ContentBlock(Type: "text", Text: UserPrompt) })
            };
            return await RunLoopAsync(http, model, messages, anthropicTools, mcp);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"\nError HTTP llamando a Anthropic: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nError inesperado: {ex.Message}");
            return 99;
        }
    }

    private static HttpClient BuildHttpClient(string apiKey)
    {
        var http = new HttpClient { BaseAddress = new Uri(AnthropicBaseUrl) };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    // Resuelve la carpeta del proyecto a partir del bin/Debug/net8.0/, crea
    // sandbox/ si no existe y siembra unos ficheros de ejemplo idempotentes.
    private static string EnsureSandbox()
    {
        var projectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var sandbox = Path.Combine(projectDir, "sandbox");
        Directory.CreateDirectory(sandbox);

        var samples = new Dictionary<string, string>
        {
            ["notas-reunion.md"] =
                "# Notas de la reunión semanal\n\n" +
                "- Lanzar la beta privada el 15 de junio.\n" +
                "- Migrar el backend a .NET 8 LTS antes de Q3.\n" +
                "- Onboarding de la nueva incorporación.\n",
            ["roadmap.md"] =
                "# Roadmap Q3\n\n" +
                "1. RAG sobre la base de conocimiento interna.\n" +
                "2. Integración real con MCP en producción.\n" +
                "3. Dashboard de consumo de tokens.\n",
            ["readme.txt"] =
                "Carpeta de trabajo del equipo. Cualquier nota o documento corto va aquí.\n",
        };

        foreach (var (name, content) in samples)
        {
            var path = Path.Combine(sandbox, name);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content);
            }
        }

        return sandbox;
    }

    private static async Task<ToolDefinition[]> DiscoverToolsAsync(McpClient.McpClient mcp)
    {
        var mcpTools = await mcp.ListToolsAsync();
        Console.WriteLine($"\n[mcp] {mcpTools.Length} tools descubiertas:");
        foreach (var t in mcpTools)
        {
            Console.WriteLine($"  - {t.Name}");
        }

        // Pasamos tal cual nombre + descripcion + JSON Schema a Anthropic.
        return mcpTools
            .Select(t => new ToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToArray();
    }

    private static async Task<int> RunLoopAsync(
        HttpClient http,
        string model,
        List<Message> messages,
        ToolDefinition[] toolDefs,
        McpClient.McpClient mcp)
    {
        for (var iter = 1; iter <= MaxIterations; iter++)
        {
            Console.WriteLine($"\n--- iteracion {iter} ---");

            var request = new MessagesRequest(
                Model: model,
                MaxTokens: 1024,
                Messages: messages.ToArray(),
                Tools: toolDefs);

            var response = await PostAsync(http, request);
            messages.Add(new Message("assistant", response.Content));

            if (response.StopReason == "end_turn")
            {
                PrintFinalText(response);
                return 0;
            }

            if (response.StopReason != "tool_use")
            {
                Console.Error.WriteLine($"stop_reason inesperado: {response.StopReason}");
                return 5;
            }

            var toolResults = await ExecuteToolUsesAsync(response.Content, mcp);
            messages.Add(new Message("user", toolResults));
        }

        Console.Error.WriteLine($"\nTope de {MaxIterations} iteraciones alcanzado sin llegar a end_turn.");
        return 4;
    }

    private static async Task<MessagesResponse> PostAsync(HttpClient http, MessagesRequest request)
    {
        using var httpResponse = await http.PostAsJsonAsync(MessagesEndpoint, request, JsonOpts.Default);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic respondio {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}. Cuerpo: {body}");
        }
        var parsed = await httpResponse.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts.Default);
        return parsed ?? throw new InvalidOperationException("Respuesta vacia de Anthropic.");
    }

    // Mantiene el orden de los tool_use de la respuesta del modelo. Cada
    // tool_use se ejecuta contra el MCP server.
    private static async Task<ContentBlock[]> ExecuteToolUsesAsync(
        ContentBlock[] assistantBlocks,
        McpClient.McpClient mcp)
    {
        var results = new List<ContentBlock>();
        foreach (var block in assistantBlocks)
        {
            if (block.Type != "tool_use") continue;

            var name = block.Name ?? "?";
            var id = block.Id
                ?? throw new InvalidOperationException($"tool_use sin id en bloque '{name}'");
            var input = block.Input ?? new JsonObject();

            Console.WriteLine($"  Claude pide MCP tool '{name}' con args: {Truncate(input.ToJsonString(), 200)}");

            string toolResult;
            try
            {
                toolResult = await mcp.CallToolAsync(name, input);
                Console.WriteLine($"  -> resultado MCP ({toolResult.Length} chars): {Truncate(toolResult, 200)}");
            }
            catch (Exception ex)
            {
                toolResult = $"Error llamando a la MCP tool '{name}': {ex.Message}";
                Console.WriteLine($"  -> {toolResult}");
            }

            results.Add(new ContentBlock(
                Type: "tool_result",
                ToolUseId: id,
                Content: toolResult));
        }
        return results.ToArray();
    }

    private static void PrintFinalText(MessagesResponse response)
    {
        Console.WriteLine($"\n--- Respuesta final de {response.Model} ---");
        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                Console.WriteLine(block.Text);
            }
        }
        Console.WriteLine("---");
        Console.WriteLine($"Tokens input: {response.Usage.InputTokens} / output: {response.Usage.OutputTokens}");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + $"... ({s.Length - max} chars mas)";
}

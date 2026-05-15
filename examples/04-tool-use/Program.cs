using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaudeFromDotnet.E04ToolUse.Models;
using ClaudeFromDotnet.E04ToolUse.Tools;
using dotenv.net;

namespace ClaudeFromDotnet.E04ToolUse;

internal static class Program
{
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    // Tope defensivo: si el loop no converge a end_turn en N iteraciones,
    // salimos con error en vez de quemarnos tokens en bucle.
    private const int MaxIterations = 5;

    private const string UserPrompt =
        "¿Qué tiempo hace en Madrid y cuánto es 17 + 25?";

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
        var tools = BuildToolRegistry();
        var toolDefs = tools.Values
            .Select(t => new ToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToArray();

        using var http = BuildHttpClient(apiKey);

        var messages = new List<Message>
        {
            new("user", new[] { new ContentBlock(Type: "text", Text: UserPrompt) })
        };

        try
        {
            return await RunLoopAsync(http, model, messages, toolDefs, tools);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"\nError HTTP llamando a Anthropic: {ex.Message}");
            return 2;
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

    private static IDictionary<string, IClaudeTool> BuildToolRegistry()
    {
        IClaudeTool[] all = [new GetWeatherTool(), new AddNumbersTool()];
        return all.ToDictionary(t => t.Name, t => t);
    }

    private static async Task<int> RunLoopAsync(
        HttpClient http,
        string model,
        List<Message> messages,
        ToolDefinition[] toolDefs,
        IDictionary<string, IClaudeTool> tools)
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

            var toolResults = await ExecuteToolUsesAsync(response.Content, tools);
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

    // El orden de los tool_result en la siguiente request DEBE coincidir con el
    // orden de los tool_use de la respuesta del modelo. Recorremos los bloques
    // en orden y vamos acumulando los resultados.
    private static async Task<ContentBlock[]> ExecuteToolUsesAsync(
        ContentBlock[] assistantBlocks,
        IDictionary<string, IClaudeTool> tools)
    {
        var results = new List<ContentBlock>();

        foreach (var block in assistantBlocks)
        {
            if (block.Type != "tool_use") continue;

            var name = block.Name ?? "?";
            var id = block.Id
                ?? throw new InvalidOperationException($"tool_use sin id en bloque '{name}'");
            var input = block.Input ?? new JsonObject();

            Console.WriteLine($"  Claude pide tool '{name}' con input: {input.ToJsonString()}");

            var toolResult = await TryExecuteAsync(name, input, tools);
            Console.WriteLine($"  -> resultado: {toolResult}");

            results.Add(new ContentBlock(
                Type: "tool_result",
                ToolUseId: id,
                Content: toolResult));
        }

        return results.ToArray();
    }

    private static async Task<string> TryExecuteAsync(
        string name,
        JsonNode input,
        IDictionary<string, IClaudeTool> tools)
    {
        if (!tools.TryGetValue(name, out var tool))
        {
            return $"Error: tool '{name}' no esta registrada en el cliente.";
        }
        try
        {
            return await tool.ExecuteAsync(input);
        }
        catch (Exception ex)
        {
            return $"Error ejecutando '{name}': {ex.Message}";
        }
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
}

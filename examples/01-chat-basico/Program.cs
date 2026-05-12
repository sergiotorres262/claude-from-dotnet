using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeFromDotnet.E01ChatBasico.Models;
using dotenv.net;

namespace ClaudeFromDotnet.E01ChatBasico;

internal static class Program
{
    // Endpoint oficial de la API de Anthropic
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";

    // Header de versión obligatorio. Si Anthropic publica una versión nueva, este string es el que cambia.
    // Doc: https://docs.claude.com/en/api/versioning
    private const string AnthropicVersion = "2023-06-01";

    private static async Task<int> Main()
    {
        // Carga .env del directorio actual (donde corras `dotnet run`)
        DotEnv.Load();

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Falta ANTHROPIC_API_KEY en .env (copia .env.example a .env y mete tu key).");
            return 1;
        }

        // Modelo por defecto: claude-sonnet-4-6. Se puede sobreescribir desde .env.
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";

        using var http = new HttpClient
        {
            BaseAddress = new Uri(AnthropicBaseUrl)
        };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var request = new MessagesRequest(
            Model: model,
            MaxTokens: 1024,
            Messages: new[]
            {
                new Message(Role: "user", Content: "Hola Claude. En 2 frases: ¿qué eres y cómo te llama un dev .NET por HTTP?")
            });

        try
        {
            var response = await SendAsync(http, request);
            PrintResponse(response);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error HTTP llamando a Anthropic: {ex.Message}");
            return 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parseando la respuesta JSON: {ex.Message}");
            return 3;
        }
    }

    private static async Task<MessagesResponse> SendAsync(HttpClient http, MessagesRequest request)
    {
        using var httpResponse = await http.PostAsJsonAsync(MessagesEndpoint, request, JsonOpts.Default);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic respondió {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}. Cuerpo: {body}");
        }

        var parsed = await httpResponse.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts.Default);
        return parsed ?? throw new JsonException("La respuesta de Anthropic vino vacía o no se pudo deserializar.");
    }

    private static void PrintResponse(MessagesResponse response)
    {
        Console.WriteLine($"--- Respuesta de {response.Model} (stop_reason: {response.StopReason}) ---");

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

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClaudeFromDotnet.E02Streaming.Models;
using ClaudeFromDotnet.E02Streaming.Sse;
using dotenv.net;

namespace ClaudeFromDotnet.E02Streaming;

internal static class Program
{
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";
    // Header de version obligatorio. Doc: https://docs.claude.com/en/api/versioning
    private const string AnthropicVersion = "2023-06-01";

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

        using var http = new HttpClient { BaseAddress = new Uri(AnthropicBaseUrl) };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        // En modo streaming Anthropic responde con SSE en lugar de JSON.
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var request = new MessagesRequest(
            Model: model,
            MaxTokens: 1024,
            Messages: new[]
            {
                new Message("user",
                    "Explicame en 5-6 frases que es Server-Sent Events y por que la API de Anthropic lo usa para streaming, dirigido a un dev .NET.")
            });

        try
        {
            await StreamAsync(http, request);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"\nError HTTP llamando a Anthropic: {ex.Message}");
            return 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"\nError parseando un evento SSE: {ex.Message}");
            return 3;
        }
    }

    private static async Task StreamAsync(HttpClient http, MessagesRequest request)
    {
        // Serializamos a mano (en vez de PostAsJsonAsync) para tener control fino
        // sobre el StringContent y poder pasar HttpCompletionOption.ResponseHeadersRead.
        var bodyJson = JsonSerializer.Serialize(request, JsonOpts.Default);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };

        // CLAVE: sin ResponseHeadersRead, HttpClient buferea el cuerpo entero
        // antes de devolvernos el control y perdemos el efecto streaming.
        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic respondio {(int)response.StatusCode} {response.ReasonPhrase}. Cuerpo: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        await ConsumeStreamAsync(stream, request.Model);
    }

    private static async Task ConsumeStreamAsync(Stream stream, string requestedModel)
    {
        var stopReason = "(desconocido)";
        var inputTokens = 0;
        var outputTokens = 0;

        Console.WriteLine($"--- Streaming de {requestedModel} ---");

        await foreach (var sse in SseReader.ReadAsync(stream))
        {
            // ping = keep-alive de Anthropic, no acarrea datos utiles.
            if (sse.EventType == "ping") continue;

            var evt = JsonSerializer.Deserialize<StreamEvent>(sse.Data, JsonOpts.Default);
            if (evt is null) continue;

            switch (evt.Type)
            {
                case "message_start":
                    inputTokens = evt.Message?.Usage?.InputTokens ?? 0;
                    break;

                case "content_block_delta":
                    // El texto incremental llega aqui. Sin newline: lo iremos
                    // pintando en la misma linea segun va llegando.
                    if (evt.Delta?.Text is { } chunk)
                    {
                        Console.Write(chunk);
                    }
                    break;

                case "message_delta":
                    if (evt.Delta?.StopReason is { } sr) stopReason = sr;
                    if (evt.Usage?.OutputTokens is { } ot) outputTokens = ot;
                    break;

                // content_block_start, content_block_stop, message_stop:
                // no nos aportan datos relevantes en este ejemplo.
            }
        }

        Console.WriteLine();
        Console.WriteLine("---");
        Console.WriteLine($"stop_reason: {stopReason}");
        Console.WriteLine($"Tokens input: {inputTokens} / output: {outputTokens}");
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeFromDotnet.E03StructuredOutput.Models;
using ClaudeFromDotnet.E03StructuredOutput.Schemas;
using dotenv.net;

namespace ClaudeFromDotnet.E03StructuredOutput;

internal static class Program
{
    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string MessagesEndpoint = "/v1/messages";
    // Header de version obligatorio. Doc: https://docs.claude.com/en/api/versioning
    private const string AnthropicVersion = "2023-06-01";

    // Pedimos el JSON exacto. El modelo NO siempre obedece — esa es la
    // limitacion del enfoque "system prompt". En el ejemplo 04 lo forzamos
    // de verdad con tool_choice.
    private const string SystemPrompt = """
        Eres un extractor de datos estructurados. El usuario te enviara una firma de
        email en lenguaje natural. Tu unica tarea es responder con un JSON valido que
        se ajuste EXACTAMENTE al siguiente esquema, sin texto adicional, sin explicar
        nada y sin envolverlo en bloques de codigo markdown (nada de ```json ... ```).

        {
          "nombre": "<nombre completo>",
          "edad": <numero entero>,
          "email": "<correo>",
          "empresa": "<nombre de la empresa>",
          "cargo": "<puesto>" | null
        }

        Reglas:
        - Si la edad no aparece, devuelve 0.
        - Si el cargo no aparece, devuelve null en ese campo.
        - No anadas claves adicionales. Solo el JSON, nada mas.
        """;

    private const string UserPrompt =
        "Un saludo,\nLaura Martín — Head of Product en Acme Iberia · laura.martin@acme.es · 35 años";

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
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await SendAsync(http, BuildRequest(model));
            return ParseAndPrintPersona(response);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error HTTP llamando a Anthropic: {ex.Message}");
            return 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parseando el envoltorio JSON de Anthropic: {ex.Message}");
            return 3;
        }
    }

    private static MessagesRequest BuildRequest(string model) => new(
        Model: model,
        MaxTokens: 1024,
        System: SystemPrompt,
        Messages: new[] { new Message("user", UserPrompt) });

    private static async Task<MessagesResponse> SendAsync(HttpClient http, MessagesRequest request)
    {
        using var httpResponse = await http.PostAsJsonAsync(MessagesEndpoint, request, JsonOpts.Default);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic respondio {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}. Cuerpo: {body}");
        }

        var parsed = await httpResponse.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts.Default);
        return parsed ?? throw new JsonException("La respuesta de Anthropic vino vacia o no se pudo deserializar.");
    }

    private static int ParseAndPrintPersona(MessagesResponse response)
    {
        var rawText = ExtractText(response);

        try
        {
            var persona = JsonSerializer.Deserialize<Persona>(rawText, JsonOpts.Schema);
            if (persona is null)
            {
                Console.Error.WriteLine("La deserializacion devolvio null. Texto crudo recibido:");
                Console.Error.WriteLine(rawText);
                return 4;
            }
            PrintPersona(persona, response);
            return 0;
        }
        catch (JsonException ex)
        {
            // Aqui es donde se ve la fragilidad del enfoque system-prompt:
            // si el modelo ignoro la regla y devolvio markdown o texto extra,
            // System.Text.Json revienta. No intentamos recuperarnos a proposito.
            Console.Error.WriteLine($"No pude deserializar la respuesta como Persona: {ex.Message}");
            Console.Error.WriteLine("Texto crudo recibido del modelo:");
            Console.Error.WriteLine(rawText);
            return 4;
        }
    }

    private static string ExtractText(MessagesResponse response)
    {
        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                return block.Text.Trim();
            }
        }
        throw new JsonException("La respuesta no contenia ningun bloque de tipo 'text'.");
    }

    private static void PrintPersona(Persona p, MessagesResponse response)
    {
        Console.WriteLine($"--- Persona extraida ({response.Model}, stop_reason: {response.StopReason}) ---");
        Console.WriteLine($"Nombre  : {p.Nombre}");
        Console.WriteLine($"Edad    : {p.Edad}");
        Console.WriteLine($"Email   : {p.Email}");
        Console.WriteLine($"Empresa : {p.Empresa}");
        Console.WriteLine($"Cargo   : {p.Cargo ?? "(no especificado)"}");
        Console.WriteLine("---");
        Console.WriteLine($"Tokens input: {response.Usage.InputTokens} / output: {response.Usage.OutputTokens}");
    }
}

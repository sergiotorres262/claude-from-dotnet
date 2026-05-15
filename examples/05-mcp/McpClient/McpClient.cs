using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeFromDotnet.E05Mcp.McpClient;

// Cliente MCP minimalista sobre transporte stdio + JSON-RPC 2.0.
//
// - Lanza un proceso hijo (ej. `npx @modelcontextprotocol/server-filesystem`).
// - Escribe peticiones JSON-RPC en su stdin, una por linea.
// - Lee respuestas de su stdout, una por linea.
// - Empareja request <-> response por el campo "id" via ConcurrentDictionary
//   de TaskCompletionSource.
// - Reenvia stderr del server a Console.Error con prefijo [mcp-stderr].
//
// NO implementa: sampling, progress notifications, cancelaciones, resources,
// prompts. Solo lo necesario para el loop de tools de este ejemplo.
//
// Spec: https://modelcontextprotocol.io/specification/2025-11-25
public sealed class McpClient : IAsyncDisposable
{
    // Version del protocolo MCP que pedimos en initialize. Si el server
    // soporta otra, devolvera la que el use y nos toca decidir si seguimos.
    private const string ProtocolVersion = "2025-11-25";

    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly Task _stdoutReader;
    private readonly Task _stderrReader;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode>> _pending = new();
    private int _nextId;
    private volatile bool _disposed;

    public McpClient(string command, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // En Windows, `npx` (y otros wrappers de node) son .cmd. CreateProcess
        // sin shell no los resuelve, asi que delegamos en cmd /c.
        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
            foreach (var a in args) psi.ArgumentList.Add(a);
        }
        else
        {
            psi.FileName = command;
            foreach (var a in args) psi.ArgumentList.Add(a);
        }

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"No se pudo lanzar el proceso MCP: {command}");

        // StreamWriter con UTF-8 sin BOM. AutoFlush para no tener que pensar
        // en flushear despues de cada WriteLine.
        _stdin = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

        _stdoutReader = Task.Run(ReadStdoutLoopAsync);
        _stderrReader = Task.Run(ReadStderrLoopAsync);
    }

    // Handshake: manda `initialize`, espera respuesta y luego envia la
    // notificacion `notifications/initialized`. Hasta que esto termine, el
    // server no deberia aceptar otras peticiones.
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var initParams = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new JsonObject(), // cliente sin capabilities extra
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "claude-from-dotnet",
                ["version"] = "0.1.0",
            },
        };

        var result = await SendRequestAsync("initialize", initParams, cancellationToken);

        var serverInfo = result?["serverInfo"];
        var serverName = serverInfo?["name"]?.GetValue<string>() ?? "(server)";
        var serverVersion = serverInfo?["version"]?.GetValue<string>() ?? "?";
        Console.WriteLine($"[mcp] conectado a {serverName} v{serverVersion}");

        await SendNotificationAsync("notifications/initialized");
    }

    // Lista las tools que expone el server. Devuelve un array tipado.
    public async Task<McpToolDefinition[]> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);

        if (result?["tools"] is not JsonArray toolsArray)
        {
            return Array.Empty<McpToolDefinition>();
        }

        var tools = toolsArray.Deserialize<McpToolDefinition[]>(JsonOpts.Mcp);
        return tools ?? Array.Empty<McpToolDefinition>();
    }

    // Llama a una tool y devuelve la concatenacion de sus bloques de texto.
    // Si IsError viene true, prefijamos el resultado para que Claude lo vea.
    public async Task<string> CallToolAsync(
        string name,
        JsonNode arguments,
        CancellationToken cancellationToken = default)
    {
        var p = new JsonObject
        {
            ["name"] = name,
            // DeepClone para evitar "node already has a parent" si el caller
            // mantiene una referencia al JsonNode original.
            ["arguments"] = arguments.DeepClone(),
        };

        var result = await SendRequestAsync("tools/call", p, cancellationToken);
        return FlattenTextContent(result);
    }

    private static string FlattenTextContent(JsonNode? result)
    {
        if (result?["content"] is not JsonArray content)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var block in content)
        {
            if (block?["type"]?.GetValue<string>() != "text") continue;
            var text = block?["text"]?.GetValue<string>();
            if (string.IsNullOrEmpty(text)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(text);
        }

        var isError = result["isError"]?.GetValue<bool>() ?? false;
        return isError ? $"[isError=true] {sb}" : sb.ToString();
    }

    private async Task<JsonNode?> SendRequestAsync(
        string method,
        JsonNode? @params,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (@params is not null)
        {
            envelope["params"] = @params.DeepClone();
        }

        await WriteLineAsync(envelope.ToJsonString());

        await using var reg = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        return await tcs.Task;
    }

    private async Task SendNotificationAsync(string method, JsonNode? @params = null)
    {
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };
        if (@params is not null)
        {
            envelope["params"] = @params.DeepClone();
        }

        await WriteLineAsync(envelope.ToJsonString());
    }

    private async Task WriteLineAsync(string json)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpClient));
        await _stdin.WriteLineAsync(json);
    }

    // Bucle de lectura de stdout. Cada linea = un JSON-RPC message completo.
    private async Task ReadStdoutLoopAsync()
    {
        try
        {
            string? line;
            while ((line = await _process.StandardOutput.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                DispatchIncomingLine(line);
            }
        }
        catch (Exception ex)
        {
            FailAllPending(ex);
        }
        finally
        {
            FailAllPending(new IOException("stdout del MCP server se cerro."));
        }
    }

    private void DispatchIncomingLine(string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[mcp-stdout-invalid-json] {ex.Message} :: {line}");
            return;
        }
        if (node is null) return;

        var id = TryGetIntId(node["id"]);
        if (id is null)
        {
            // Notificacion del server (sin id). Las ignoramos en este ejemplo.
            return;
        }

        if (!_pending.TryRemove(id.Value, out var tcs)) return;

        if (node["error"] is JsonNode err)
        {
            var code = err["code"]?.GetValue<int>() ?? 0;
            var msg = err["message"]?.GetValue<string>() ?? "(sin mensaje)";
            tcs.TrySetException(new InvalidOperationException(
                $"JSON-RPC error {code}: {msg}"));
        }
        else
        {
            tcs.TrySetResult(node["result"] ?? new JsonObject());
        }
    }

    private static int? TryGetIntId(JsonNode? idNode)
    {
        if (idNode is null) return null;
        try { return idNode.GetValue<int>(); }
        catch { return null; }
    }

    private async Task ReadStderrLoopAsync()
    {
        try
        {
            string? line;
            while ((line = await _process.StandardError.ReadLineAsync()) is not null)
            {
                Console.Error.WriteLine($"[mcp-stderr] {line}");
            }
        }
        catch
        {
            // El proceso ha muerto o el pipe se ha cerrado. Nada que reportar.
        }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetException(ex);
        }
        _pending.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Cerrar stdin senaliza al server que toca apagar (segun la spec).
        try { _stdin.Close(); } catch { /* ignore */ }

        // Damos 2 segundos al proceso para salir limpio antes de matarlo.
        try
        {
            using var killTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _process.WaitForExitAsync(killTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }
        catch
        {
            // ignore
        }

        FailAllPending(new ObjectDisposedException(nameof(McpClient)));

        // Esperar a que las tareas de IO terminen (con tope), si no, seguimos.
        await Task.WhenAny(
            Task.WhenAll(_stdoutReader, _stderrReader),
            Task.Delay(1000));

        _process.Dispose();
    }
}

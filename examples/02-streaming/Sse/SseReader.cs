using System.Runtime.CompilerServices;
using System.Text;

namespace ClaudeFromDotnet.E02Streaming.Sse;

// Parser manual de Server-Sent Events. Lee linea a linea y emite un evento
// cada vez que encuentra una linea en blanco (separador estandar de SSE).
//
// Formato del transporte:
//
//   event: <nombre>
//   data: <payload>
//   <linea-en-blanco>
//
// Spec: https://html.spec.whatwg.org/multipage/server-sent-events.html
//
// A proposito sin librerias de terceros: la pedagogia del repo es que se
// vea que SSE es solo texto sobre HTTP, no magia.
internal static class SseReader
{
    public static async IAsyncEnumerable<SseEvent> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        string? eventType = null;
        var dataBuilder = new StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                // Linea en blanco = fin del evento actual.
                if (eventType is not null && dataBuilder.Length > 0)
                {
                    yield return new SseEvent(eventType, dataBuilder.ToString());
                }
                eventType = null;
                dataBuilder.Clear();
                continue;
            }

            // Lineas que empiezan con ':' son comentarios o keep-alives. Ignorar.
            if (line[0] == ':') continue;

            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            var field = line[..colonIdx];
            var value = line[(colonIdx + 1)..];
            // El espacio opcional tras los ':' del estandar SSE.
            if (value.Length > 0 && value[0] == ' ') value = value[1..];

            if (field == "event")
            {
                eventType = value;
            }
            else if (field == "data")
            {
                // Si hay varias lineas data: se concatenan con '\n' (estandar SSE).
                // Anthropic en la practica solo manda una por evento.
                if (dataBuilder.Length > 0) dataBuilder.Append('\n');
                dataBuilder.Append(value);
            }
            // Otros campos del estandar (id, retry) los ignoramos.
        }

        // Evento final que pudiera quedar sin linea en blanco de cierre.
        if (eventType is not null && dataBuilder.Length > 0)
        {
            yield return new SseEvent(eventType, dataBuilder.ToString());
        }
    }
}

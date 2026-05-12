namespace ClaudeFromDotnet.E02Streaming.Sse;

// Evento SSE crudo tal como llega del transporte: nombre del evento + payload.
// El payload (Data) es un string JSON; quien lo consuma lo deserializa al
// tipo concreto (en este ejemplo, a StreamEvent).
public record SseEvent(string EventType, string Data);

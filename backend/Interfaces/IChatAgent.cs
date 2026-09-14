namespace Trippy.Backend.Interfaces;

/// <summary>
/// SSE-friendly events emitted by the chat agent loop.
/// Event names match the frontend chatStream.ts parser.
/// </summary>
public sealed record AgentStreamEvent(string Event, object Data);

public interface IChatAgent
{
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string message,
        string? conversationId,
        string? itineraryId = null,
        CancellationToken ct = default);
}

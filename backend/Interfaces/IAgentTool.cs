namespace Trippy.Backend.Interfaces;

/// <summary>
/// Marker for a single agent tool. Register new tools in DI and they are discovered at runtime.
/// Keep tools small: one responsibility each.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }

    /// <summary>JSON Schema object for OpenAI function parameters.</summary>
    object ParameterSchema { get; }

    Task<string> ExecuteAsync(string argumentsJson, string conversationId, CancellationToken ct = default);
}

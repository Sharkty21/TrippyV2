using System.ComponentModel.DataAnnotations;

namespace Trippy.Backend.DTOs;

public class ChatRequest
{
    [Required] public string Message { get; set; } = "";
    public string? ConversationId { get; set; }

    /// <summary>
    /// Set when the user is chatting from an itinerary's detail page. The agent is given this
    /// itinerary's current state as context and is steered towards editing it rather than
    /// creating a new one.
    /// </summary>
    public string? ItineraryId { get; set; }
}

public class PendingActionRead
{
    [Required] public Guid Id { get; set; }
    [Required] public string ConversationId { get; set; } = "";
    [Required] public string ToolName { get; set; } = "";
    [Required] public object Payload { get; set; } = new { };
    [Required] public string Summary { get; set; } = "";
    [Required] public string Status { get; set; } = "";
    [Required] public DateTimeOffset CreatedAt { get; set; }
    public object? Result { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "ok";
}

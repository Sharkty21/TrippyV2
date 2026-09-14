using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Trippy.Backend.Interfaces;
using Trippy.Backend.Options;

namespace Trippy.Backend.Services;

/// <summary>
/// Minimal OpenAI function-calling loop (no LangChain).
/// Streams tokens as SSE events; runs tools between model turns.
/// Conversation history is in-memory — fine for a single-instance demo.
/// This wraps the OpenAI SDK (an external service); controllers only see IChatAgent.
/// </summary>
public sealed class ChatAgentService(
    IEnumerable<IAgentTool> tools,
    IItineraryService itineraries,
    IOptions<OpenAiOptions> openAiOptions) : IChatAgent
{
    private const string SystemPrompt = """
        You are Trippy's travel itinerary assistant.
        You help users explore Places (Italy seed data) and plan Itineraries, made of Sections (days)
        and Items (stops referencing a Place). Reply using markdown (headings, **bold**, bullet lists)
        where it improves readability — the chat UI renders it.

        Hard constraints:
        - Every itinerary is EXACTLY 3 days. The user only picks a start date; end_date and the
          3 day sections are created automatically. Never propose more than 3 sections/days for
          one itinerary, and never propose a section date outside start_date..start_date+2.
        - Active itinerary context: if a message is preceded by an "ACTIVE ITINERARY CONTEXT" system
          note, the user is on that itinerary's detail page — its id, current sections, and items are
          given to you there. Default to EDITING that itinerary (entity="item"/"section" with its
          section/item ids and this itinerary_id, or entity="itinerary" op="update" for its own name/
          description/start_date) rather than creating a new one. Only use entity="plan" or
          entity="itinerary" op="create" when the user clearly asks for a new/separate/another trip —
          phrases like "add", "change", "move", "remove", "swap in", or "plan day 2" about the current
          trip mean: mutate the active itinerary in place, not create a whole new itinerary/plan.
        - Item op selection within the active itinerary — this is a common mistake, be careful:
          - "add"/"insert"/"also include" a stop → entity="item" op="create" with the target
            section_id. This ALWAYS creates a brand-new item, even if the section already has an
            item at a similar time or of the same kind (e.g. an existing lunch stop). Do not repurpose
            or overwrite an existing item's id just because it's the closest match in time/category —
            that silently destroys the user's existing stop instead of adding a new one.
          - "change"/"replace"/"swap X for Y"/"update" a stop the user is clearly referring to by
            name or id → entity="item" op="update" on that specific existing item's id.
          - "remove"/"delete" a stop → entity="item" op="delete" on that specific existing item's id.
          - If it's ambiguous whether the user wants a new stop or wants to replace an existing one
            (e.g. there's already an unrelated item at that time), ask a brief clarifying question
            instead of guessing op="update".

        - place_id, ALWAYS: whenever you set a place_id (item create, or any plan item), it MUST be the
          exact `id` value returned by query_db for that specific place. Never invent, guess, or reuse
          a placeholder-looking id such as "place_001" — that is not a real id and will be rejected. If
          you haven't already retrieved a place's real id in this conversation (e.g. from a prior
          query_db result or the ACTIVE ITINERARY CONTEXT), call query_db with table="places" (filter by
          name_contains/city) first and copy its `id` field verbatim before proposing the mutation.

        Tools:
        - query_db: look up existing places/itineraries/sections/items before proposing changes.
          Places include hours, duration_minutes, price_range, rating, tags, seasonal_notes,
          booking_required — always pull these before recommending or scheduling a place.
        - estimate_travel_time: check walking/driving time between two place ids before you
          schedule them back-to-back or judge whether a day's plan is geographically sane.
        - propose_itinerary_mutation: the ONLY way to write data, and it always requires human
          approval — never claim a change is applied before it's approved.
          - Use entity="plan" with op="create" to build a WHOLE itinerary (name, start_date) plus
            all 3 days' items in a single approval when asked to plan a trip — prefer this over many
            small proposals so the user reviews one coherent plan.
          - Item create/update can set start_time ("HH:mm"); set it whenever you know a schedule,
            so the UI can render a timeline.
          - Every item's description must carry real advice (what to order/do/bring, timing), not a
            generic restatement of the place name — see "Item descriptions" below.

        When planning or recommending places, be thoughtful and explicit in your reasoning to the user:
        - Hours & seasonal notes: strongly prefer checking `hours` AND `seasonal_notes` (via query_db)
          against the item's actual scheduled day-of-week, time-of-day, and time-of-year before you
          schedule it — especially when creating a whole plan. Call out anything that might be closed
          or seasonal (e.g. summer-only, closed Mondays) in your reply and in the mutation's summary,
          even when you ultimately find it's open, so the user sees you checked. If you're not fully
          sure, say so and suggest the user double check, rather than blocking the proposal on it.
        - Distance & pacing: use estimate_travel_time between consecutive stops in a day. Don't
          schedule stops so far apart the day becomes unrealistic, and don't schedule two things
          that overlap once you account for start_time + duration_minutes + travel time to the next
          stop — flag or fix any overlap you find.
        - Budget: if the user gives a budget, take note of each place's `price_range` and keep the
          day's/trip's mix within it; mention when something is a splurge.
        - Booking: whenever a place has booking_required=true, explicitly remind the user to book
          ahead, both in your chat reply and in the mutation's summary field.
        - Tags & fit: match `tags` against what the user asked for (e.g. "family friendly",
          "romantic", "food") rather than picking generically.
        - Quality: prefer higher-`rating` places when several fit equally well, unless the user
          asked for a specific place by name.
        - Item descriptions, ALWAYS: every item's `description` must be short, concrete, helpful
          advice for that stop — never just restate the place name or leave it generic. Tailor it
          to `type`/`tags`: for restaurants/cafes/bars, name a dish, drink, or specialty to order
          (use the place's description/tags for hints); for museums/sights, note what not to miss
          or how to skip lines; for activities, note what to bring or book. Fold in anything
          time-sensitive from `hours`/`seasonal_notes` (e.g. "arrive by 6pm, kitchen closes early")
          and a `booking_required` reminder when relevant, so the description alone is useful at a
          glance. Keep it to 1-2 sentences.

        Be concise, practical, and proactive about surfacing tradeoffs (hours, distance, budget,
        booking) rather than silently picking something that conflicts with them.
        """;

    // Demo-only memory. Replace with Redis / DB for multi-instance deployments.
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> Conversations = new();

    private readonly IReadOnlyDictionary<string, IAgentTool> _tools =
        tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string message,
        string? conversationId,
        string? itineraryId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        conversationId ??= Guid.NewGuid().ToString();
        yield return new AgentStreamEvent("conversation", new { conversation_id = conversationId });

        var opts = openAiOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            yield return new AgentStreamEvent("error", new { message = "OPENAI_API_KEY is not set" });
            yield return new AgentStreamEvent("done", new { conversation_id = conversationId });
            yield break;
        }

        var history = Conversations.GetOrAdd(conversationId, _ =>
        [
            new SystemChatMessage(SystemPrompt)
        ]);

        lock (history)
        {
            history.Add(new UserChatMessage(message));
        }

        // Fetched fresh per request (never persisted in `history`) so it always reflects the
        // itinerary's current state and doesn't go stale or bloat the conversation across turns.
        var itineraryContext = await BuildItineraryContextAsync(itineraryId, ct);

        ChatClient? client = null;
        string? clientError = null;
        try
        {
            client = new ChatClient(opts.Model, new ApiKeyCredential(opts.ApiKey));
        }
        catch (Exception ex)
        {
            clientError = ex.Message;
        }

        if (clientError is not null || client is null)
        {
            yield return new AgentStreamEvent("error", new { message = clientError ?? "Failed to create OpenAI client" });
            yield return new AgentStreamEvent("done", new { conversation_id = conversationId });
            yield break;
        }

        var chatOptions = BuildOptions();

        // Cap tool rounds so a bad model loop cannot hang the request forever.
        for (var round = 0; round < 8; round++)
        {
            List<ChatMessage> snapshot;
            lock (history) snapshot = history.ToList();
            if (itineraryContext is not null)
                snapshot.Insert(1, new SystemChatMessage(itineraryContext)); // right after the main system prompt

            var contentBuilder = new StringBuilder();
            var toolCallBuilders = new Dictionary<int, ToolCallBuilder>();
            var finishReason = ChatFinishReason.Stop;
            string? streamError = null;
            var pendingEvents = new List<AgentStreamEvent>();

            try
            {
                await foreach (var update in client.CompleteChatStreamingAsync(snapshot, chatOptions, ct))
                {
                    if (update.FinishReason is not null)
                        finishReason = update.FinishReason.Value;

                    foreach (var part in update.ContentUpdate)
                    {
                        if (part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrEmpty(part.Text))
                        {
                            contentBuilder.Append(part.Text);
                            pendingEvents.Add(new AgentStreamEvent("token", new { content = part.Text }));
                        }
                    }

                    foreach (var toolUpdate in update.ToolCallUpdates)
                    {
                        if (!toolCallBuilders.TryGetValue(toolUpdate.Index, out var builder))
                        {
                            builder = new ToolCallBuilder();
                            toolCallBuilders[toolUpdate.Index] = builder;
                        }

                        if (toolUpdate.ToolCallId is not null)
                            builder.Id = toolUpdate.ToolCallId;
                        if (toolUpdate.FunctionName is not null)
                            builder.Name = toolUpdate.FunctionName;
                        if (toolUpdate.FunctionArgumentsUpdate is not null)
                            builder.Arguments.Append(toolUpdate.FunctionArgumentsUpdate);
                    }
                }
            }
            catch (Exception ex)
            {
                streamError = ex.Message;
            }

            foreach (var evt in pendingEvents)
                yield return evt;

            if (streamError is not null)
            {
                yield return new AgentStreamEvent("error", new { message = streamError });
                yield return new AgentStreamEvent("done", new { conversation_id = conversationId });
                yield break;
            }

            if (toolCallBuilders.Count == 0 || finishReason != ChatFinishReason.ToolCalls)
            {
                var text = contentBuilder.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    lock (history) history.Add(new AssistantChatMessage(text));
                }
                break;
            }

            var assistantToolCalls = toolCallBuilders.OrderBy(kv => kv.Key)
                .Select(kv => ChatToolCall.CreateFunctionToolCall(
                    kv.Value.Id,
                    kv.Value.Name,
                    BinaryData.FromString(kv.Value.Arguments.ToString())))
                .ToList();

            var assistantMessage = new AssistantChatMessage(assistantToolCalls);
            if (contentBuilder.Length > 0)
                assistantMessage.Content.Add(ChatMessageContentPart.CreateTextPart(contentBuilder.ToString()));

            lock (history) history.Add(assistantMessage);

            foreach (var call in assistantToolCalls)
            {
                var args = call.FunctionArguments.ToString();
                string output;
                if (!_tools.TryGetValue(call.FunctionName, out var tool))
                {
                    output = JsonSerializer.Serialize(new { error = $"Unknown tool {call.FunctionName}" });
                }
                else
                {
                    try
                    {
                        output = await tool.ExecuteAsync(args, conversationId, ct);
                    }
                    catch (Exception ex)
                    {
                        output = JsonSerializer.Serialize(new { error = ex.Message });
                    }
                }

                lock (history) history.Add(new ToolChatMessage(call.Id, output));

                if (call.FunctionName == "propose_itinerary_mutation"
                    && TryParsePendingAction(output, out var pending))
                {
                    yield return pending;
                    continue;
                }

                yield return new AgentStreamEvent("tool", new
                {
                    name = call.FunctionName,
                    output = output.Length > 2000 ? output[..2000] : output
                });
            }
        }

        yield return new AgentStreamEvent("done", new { conversation_id = conversationId });
    }

    /// <summary>
    /// Loads the itinerary the user is currently viewing (if any) and renders it as a compact
    /// system note so the model can edit it in place instead of proposing a brand new trip.
    /// Fetched fresh on every call — never cached in conversation history.
    /// </summary>
    private async Task<string?> BuildItineraryContextAsync(string? itineraryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itineraryId) || !Guid.TryParse(itineraryId, out var id))
            return null;

        var itin = await itineraries.GetAsync(id, ct);
        if (itin is null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("ACTIVE ITINERARY CONTEXT — the user is viewing this itinerary right now.");
        sb.AppendLine("Prefer editing it (see the \"Active itinerary context\" hard constraint) over creating a new one.");
        sb.AppendLine($"itinerary_id: {itin.Id}");
        sb.AppendLine($"name: {itin.Name}");
        sb.AppendLine($"description: {itin.Description}");
        sb.AppendLine($"start_date: {itin.StartDate:yyyy-MM-dd}");
        sb.AppendLine($"end_date: {itin.EndDate:yyyy-MM-dd}");

        foreach (var section in itin.Sections.OrderBy(s => s.Sequence))
        {
            sb.AppendLine($"- section_id: {section.Id} | date: {section.Date:yyyy-MM-dd} ({section.Date.DayOfWeek}) | description: {section.Description}");
            foreach (var item in section.Items.OrderBy(i => i.Sequence))
            {
                var startTime = item.StartTime is { } t ? t.ToString("HH:mm") : "unscheduled";
                sb.AppendLine(
                    $"  - item_id: {item.Id} | place_id: {item.PlaceId} | place: {item.Place?.Name} | " +
                    $"start_time: {startTime} | description: {item.Description}");
            }
            if (section.Items.Count == 0)
                sb.AppendLine("  (no items yet)");
        }

        return sb.ToString();
    }

    private static bool TryParsePendingAction(string output, out AgentStreamEvent pending)
    {
        pending = null!;
        try
        {
            using var parsed = JsonDocument.Parse(output);
            if (!parsed.RootElement.TryGetProperty("action_id", out var actionId))
                return false;

            pending = new AgentStreamEvent("pending_action", new
            {
                action_id = actionId.GetString(),
                summary = parsed.RootElement.TryGetProperty("summary", out var s)
                    ? s.GetString()
                    : "",
                command = parsed.RootElement.TryGetProperty("command", out var c)
                    ? JsonSerializer.Deserialize<object>(c.GetRawText())
                    : new { },
                status = "pending"
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private ChatCompletionOptions BuildOptions()
    {
        var options = new ChatCompletionOptions { Temperature = 0.3f };
        foreach (var tool in _tools.Values)
        {
            var schema = BinaryData.FromObjectAsJson(tool.ParameterSchema);
            options.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, schema));
        }
        return options;
    }

    private sealed class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public StringBuilder Arguments { get; } = new();
    }
}

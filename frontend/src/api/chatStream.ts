import { API_BASE_URL } from "./config"

export type ChatSseHandlers = {
  onConversation?: (conversationId: string) => void
  onToken?: (content: string) => void
  onPendingAction?: (action: {
    action_id: string
    summary: string
    command: unknown
    status: string
  }) => void
  onTool?: (name: string, output: string) => void
  onError?: (message: string) => void
  onDone?: () => void
}

/** Stream POST /api/chat as SSE (not via Orval — streaming body). */
export async function streamChat(
  message: string,
  conversationId: string | null,
  handlers: ChatSseHandlers,
  signal?: AbortSignal,
  itineraryId?: string | null,
): Promise<void> {
  // Any failure below (network error, non-2xx response, a broken/interrupted stream, or a
  // malformed SSE payload) must surface via onError rather than throwing — otherwise the caller's
  // assistant chat bubble is left permanently empty with no explanation. The one exception is a
  // deliberate abort (user closed the chat / sent a new message) — that's not a failure.
  let sawDone = false
  try {
    const res = await fetch(`${API_BASE_URL}/api/chat`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
      body: JSON.stringify({
        message,
        conversation_id: conversationId,
        itinerary_id: itineraryId ?? null,
      }),
      signal,
    })

    if (!res.ok || !res.body) {
      handlers.onError?.(`Chat request failed (HTTP ${res.status}). Please try again.`)
      return
    }

    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ""

    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      const parts = buffer.split("\n\n")
      buffer = parts.pop() ?? ""

      for (const part of parts) {
        const lines = part.split("\n")
        let event = "message"
        let data = ""
        for (const line of lines) {
          if (line.startsWith("event:")) event = line.slice(6).trim()
          else if (line.startsWith("data:")) data += line.slice(5).trim()
        }
        if (!data) continue
        try {
          const parsed = JSON.parse(data) as Record<string, unknown>
          if (event === "conversation" && typeof parsed.conversation_id === "string") {
            handlers.onConversation?.(parsed.conversation_id)
          } else if (event === "token" && typeof parsed.content === "string") {
            handlers.onToken?.(parsed.content)
          } else if (event === "pending_action") {
            handlers.onPendingAction?.(parsed as never)
          } else if (event === "tool") {
            handlers.onTool?.(String(parsed.name ?? ""), String(parsed.output ?? ""))
          } else if (event === "error") {
            handlers.onError?.(String(parsed.message ?? "Something went wrong. Please try again."))
          } else if (event === "done") {
            sawDone = true
          }
        } catch {
          // Malformed chunk — skip it, but don't fail the whole stream over one bad event.
        }
      }
    }

    if (!sawDone) {
      // The connection closed before a "done" event arrived (e.g. server crashed mid-stream,
      // proxy/network drop) — the caller has no other signal that something went wrong.
      handlers.onError?.("Connection to the assistant was interrupted. Please try again.")
    }
  } catch (err) {
    if (signal?.aborted) {
      // Deliberate cancellation (chat closed / new message sent) — not a failure to report.
      return
    }
    handlers.onError?.(
      err instanceof Error ? `Chat failed: ${err.message}` : "Chat failed due to a network error.",
    )
  } finally {
    handlers.onDone?.()
  }
}

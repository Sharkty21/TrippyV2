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
    handlers.onError?.(`Chat failed (${res.status})`)
    handlers.onDone?.()
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
          handlers.onError?.(String(parsed.message ?? "Unknown error"))
        } else if (event === "done") {
          handlers.onDone?.()
        }
      } catch {
        // ignore malformed chunks
      }
    }
  }
  handlers.onDone?.()
}

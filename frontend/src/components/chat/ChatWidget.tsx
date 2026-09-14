import { MapPinned, MessageCircle, Send, Sparkles, X } from "lucide-react"
import { useEffect, useRef, useState } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { useMatch } from "react-router-dom"
import { toast } from "sonner"

import { streamChat } from "@/api/chatStream"
import { useApproveAction, useDenyAction, useGetItinerary } from "@/api/generated"
import { useInvalidateItineraries } from "@/api/invalidate"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { cn } from "@/lib/utils"

type PendingCard = {
  action_id: string
  summary: string
  command: unknown
  status: "pending" | "approved" | "denied" | "executed"
}

type ChatMessage = {
  id: string
  role: "user" | "assistant" | "system"
  content: string
  pending?: PendingCard
}

function newId() {
  return crypto.randomUUID()
}

export function ChatWidget() {
  const itineraryMatch = useMatch("/itineraries/:id")
  const itineraryId = itineraryMatch?.params.id ?? null
  const { data: activeItinerary } = useGetItinerary(itineraryId ?? "", {
    query: { enabled: Boolean(itineraryId) },
  })

  const [open, setOpen] = useState(false)
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState("")
  const [streaming, setStreaming] = useState(false)
  const [thinkingMessageId, setThinkingMessageId] = useState<string | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const invalidate = useInvalidateItineraries()
  const approveMut = useApproveAction({
    mutation: { onSuccess: () => invalidate() },
  })
  const denyMut = useDenyAction()

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages, open])

  function openChat() {
    // Fresh conversation every time the window opens
    abortRef.current?.abort()
    setConversationId(crypto.randomUUID())
    setMessages([
      {
        id: newId(),
        role: "system",
        content: activeItinerary
          ? `Chatting within **${activeItinerary.name}** — asks to add/change/remove stops will edit ` +
            "this trip. Database edits need your approval first."
          : "Ask travel questions or request itinerary changes. Database edits need your approval first.",
      },
    ])
    setInput("")
    setOpen(true)
  }

  function closeChat() {
    abortRef.current?.abort()
    setOpen(false)
  }

  async function send() {
    const text = input.trim()
    if (!text || streaming) return
    setInput("")
    const userMsg: ChatMessage = { id: newId(), role: "user", content: text }
    const assistantId = newId()
    setMessages((m) => [
      ...m,
      userMsg,
      { id: assistantId, role: "assistant", content: "" },
    ])
    setStreaming(true)
    setThinkingMessageId(assistantId)

    const ac = new AbortController()
    abortRef.current = ac

    await streamChat(
      text,
      conversationId,
      {
        onConversation: (id) => setConversationId(id),
        onToken: (content) => {
          setThinkingMessageId((id) => (id === assistantId ? null : id))
          setMessages((prev) =>
            prev.map((msg) =>
              msg.id === assistantId
                ? { ...msg, content: msg.content + content }
                : msg,
            ),
          )
        },
        onPendingAction: (action) => {
          setThinkingMessageId((id) => (id === assistantId ? null : id))
          setMessages((prev) => [
            ...prev,
            {
              id: newId(),
              role: "system",
              content: action.summary,
              pending: {
                action_id: action.action_id,
                summary: action.summary,
                command: action.command,
                status: "pending",
              },
            },
          ])
        },
        onError: (message) => {
          setThinkingMessageId((id) => (id === assistantId ? null : id))
          setMessages((prev) =>
            prev.map((msg) =>
              msg.id === assistantId
                ? {
                    ...msg,
                    content: msg.content || `Error: ${message}`,
                  }
                : msg,
            ),
          )
        },
        onDone: () => {
          setThinkingMessageId((id) => (id === assistantId ? null : id))
          setStreaming(false)
        },
      },
      ac.signal,
      itineraryId,
    )
    setThinkingMessageId((id) => (id === assistantId ? null : id))
    setStreaming(false)
  }

  async function handleApprove(actionId: string) {
    try {
      await approveMut.mutateAsync({ actionId })
      setMessages((prev) =>
        prev.map((m) =>
          m.pending?.action_id === actionId
            ? {
                ...m,
                pending: { ...m.pending, status: "executed" },
              }
            : m,
        ),
      )
      toast.success("Action applied")
    } catch {
      toast.error("Approve failed")
    }
  }

  async function handleDeny(actionId: string) {
    try {
      await denyMut.mutateAsync({ actionId })
      setMessages((prev) =>
        prev.map((m) =>
          m.pending?.action_id === actionId
            ? { ...m, pending: { ...m.pending, status: "denied" } }
            : m,
        ),
      )
      toast.message("Action denied")
    } catch {
      toast.error("Deny failed")
    }
  }

  return (
    <>
      {!open && (
        <div className="fixed right-5 bottom-5 z-[2000] flex flex-col items-end gap-2">
          {activeItinerary && (
            <Badge
              variant="secondary"
              className="h-auto max-w-[220px] gap-1 rounded-full py-1 shadow-md"
            >
              <MapPinned className="size-3" />
              <span className="truncate">{activeItinerary.name}</span>
            </Badge>
          )}
          <Button size="lg" className="rounded-full shadow-lg" onClick={openChat}>
            <MessageCircle className="size-5" />
            AI chat
          </Button>
        </div>
      )}

      {open && (
        <div className="fixed right-4 bottom-4 z-[2000] flex h-[min(560px,80vh)] w-[min(520px,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl border bg-white shadow-2xl">
          <div className="flex items-center justify-between gap-2 border-b px-4 py-3">
            <div className="min-w-0">
              <p className="font-semibold">New chat</p>
              <p className="text-xs text-muted-foreground">
                AI answers may be inaccurate
              </p>
            </div>
            <div className="flex items-center gap-2">
              {activeItinerary && (
                <Badge
                  variant="secondary"
                  className="h-auto max-w-[160px] gap-1 rounded-full py-1"
                  title={`Editing ${activeItinerary.name}`}
                >
                  <MapPinned className="size-3" />
                  <span className="truncate">{activeItinerary.name}</span>
                </Badge>
              )}
              <Button size="icon" variant="ghost" onClick={closeChat}>
                <X className="size-4" />
              </Button>
            </div>
          </div>

          <ScrollArea className="min-h-0 flex-1 px-3 py-3">
            <div className="space-y-3">
              {messages.map((msg) => (
                <div
                  key={msg.id}
                  className={cn(
                    "min-w-0 rounded-lg px-3 py-2 text-sm wrap-anywhere",
                    msg.role === "user" && "ml-8 bg-teal-600 text-white",
                    msg.role === "assistant" && "mr-4 bg-stone-100 text-stone-900",
                    msg.role === "system" && "border bg-amber-50 text-stone-800",
                  )}
                >
                  {msg.id === thinkingMessageId && !msg.content && (
                    <div className="flex items-center gap-1.5 py-0.5 text-sm font-medium">
                      <Sparkles className="size-3.5 shrink-0 animate-pulse text-teal-600" />
                      <span className="animate-shimmer-text">Thinking…</span>
                    </div>
                  )}
                  {msg.content && (
                    <div
                      className={cn(
                        "chat-markdown min-w-0 text-sm leading-relaxed wrap-anywhere",
                        "[&_h1]:mt-1 [&_h1]:mb-1 [&_h1]:text-base [&_h1]:font-semibold",
                        "[&_h2]:mt-1 [&_h2]:mb-1 [&_h2]:text-base [&_h2]:font-semibold",
                        "[&_h3]:mt-1 [&_h3]:mb-1 [&_h3]:text-sm [&_h3]:font-semibold",
                        "[&_p]:my-1 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0",
                        "[&_ul]:my-1 [&_ul]:list-disc [&_ul]:pl-5",
                        "[&_ol]:my-1 [&_ol]:list-decimal [&_ol]:pl-5",
                        "[&_li]:my-0.5",
                        "[&_strong]:font-semibold",
                        "[&_code]:rounded [&_code]:bg-black/10 [&_code]:px-1 [&_code]:py-0.5 [&_code]:text-[0.85em] [&_code]:wrap-anywhere",
                        "[&_a]:underline",
                      )}
                    >
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>
                        {msg.content}
                      </ReactMarkdown>
                    </div>
                  )}
                  {msg.pending && (
                    <div className="mt-2 space-y-2">
                      <p className="text-xs font-medium uppercase tracking-wide text-amber-800">
                        Pending action
                      </p>
                      <pre className="max-h-40 overflow-auto rounded bg-stone-900/90 p-2 text-[11px] whitespace-pre-wrap wrap-anywhere text-stone-100">
                        {JSON.stringify(msg.pending.command, null, 2)}
                      </pre>
                      {msg.pending.status === "pending" ? (
                        <div className="flex gap-2">
                          <Button
                            size="sm"
                            onClick={() => void handleApprove(msg.pending!.action_id)}
                            disabled={approveMut.isPending}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => void handleDeny(msg.pending!.action_id)}
                            disabled={denyMut.isPending}
                          >
                            Deny
                          </Button>
                        </div>
                      ) : (
                        <p className="text-xs font-medium capitalize text-muted-foreground">
                          {msg.pending.status}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              ))}
              <div ref={bottomRef} />
            </div>
          </ScrollArea>

          <form
            className="flex gap-2 border-t p-3"
            onSubmit={(e) => {
              e.preventDefault()
              void send()
            }}
          >
            <Input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask travel related questions"
              disabled={streaming}
            />
            <Button type="submit" size="icon" disabled={streaming || !input.trim()}>
              <Send className="size-4" />
            </Button>
          </form>
        </div>
      )}
    </>
  )
}

import { Pencil, Plus } from "lucide-react"
import { useState } from "react"
import { Link } from "react-router-dom"
import { toast } from "sonner"

import {
  useCreateItinerary,
  useDeleteItinerary,
  useListItineraries,
  useUpdateItinerary,
} from "@/api/generated"
import { useInvalidateItineraries } from "@/api/invalidate"
import type { ItinerarySummary } from "@/api/generated/models"
import { ItineraryFormSheet } from "@/components/itinerary/ItineraryFormSheet"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"

function formatRange(start: string, end: string) {
  return `${start} → ${end}`
}

export function LandingPage() {
  const invalidate = useInvalidateItineraries()
  const { data, isLoading, error } = useListItineraries()
  const createMut = useCreateItinerary({
    mutation: { onSuccess: () => invalidate() },
  })
  const deleteMut = useDeleteItinerary({
    mutation: { onSuccess: () => invalidate() },
  })

  const [sheetOpen, setSheetOpen] = useState(false)
  const [editing, setEditing] = useState<ItinerarySummary | null>(null)
  const updateMut = useUpdateItinerary({
    mutation: { onSuccess: () => invalidate(editing?.id) },
  })

  return (
    <div className="min-h-svh bg-gradient-to-b from-sky-50 via-white to-stone-50">
      <header className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-6 py-8">
        <div>
          <p className="text-sm font-medium tracking-wide text-teal-700">
            Trippy
          </p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight text-stone-900">
            Your itineraries
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Plan days, places, and routes — then chat with the travel agent.
          </p>
        </div>
        <Button
          onClick={() => {
            setEditing(null)
            setSheetOpen(true)
          }}
        >
          <Plus className="size-4" />
          New trip
        </Button>
      </header>

      <main className="mx-auto grid max-w-5xl gap-4 px-6 pb-24 sm:grid-cols-2 lg:grid-cols-3">
        {isLoading &&
          Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-36 rounded-xl" />
          ))}
        {!!error && (
          <p className="col-span-full text-sm text-destructive">
            Failed to load itineraries. Is the API running?
          </p>
        )}
        {data?.map((itin) => (
          <div key={itin.id} className="group relative">
            <Link to={`/itineraries/${itin.id}`} className="block">
              <Card className="h-full transition-shadow hover:shadow-md">
                <CardHeader>
                  <CardTitle className="pr-10 text-lg">{itin.name}</CardTitle>
                  <p className="text-xs font-medium text-teal-700">
                    {formatRange(itin.start_date, itin.end_date)}
                  </p>
                  <CardDescription className="line-clamp-3">
                    {itin.description || "No description"}
                  </CardDescription>
                </CardHeader>
              </Card>
            </Link>
            <Button
              type="button"
              size="icon"
              variant="secondary"
              className="absolute top-3 right-3 size-8 opacity-0 transition-opacity group-hover:opacity-100"
              onClick={(e) => {
                e.preventDefault()
                setEditing(itin)
                setSheetOpen(true)
              }}
              aria-label="Edit itinerary"
            >
              <Pencil className="size-3.5" />
            </Button>
          </div>
        ))}
        {!isLoading && data?.length === 0 && (
          <p className="col-span-full text-sm text-muted-foreground">
            No itineraries yet — create one to get started.
          </p>
        )}
      </main>

      <ItineraryFormSheet
        open={sheetOpen}
        onOpenChange={setSheetOpen}
        initial={editing}
        saving={createMut.isPending || updateMut.isPending || deleteMut.isPending}
        onSubmit={async (values) => {
          try {
            if (editing) {
              await updateMut.mutateAsync({
                itineraryId: editing.id,
                data: values,
              })
              toast.success("Itinerary updated")
            } else {
              await createMut.mutateAsync({ data: values })
              toast.success("Itinerary created")
            }
            setSheetOpen(false)
          } catch {
            toast.error("Save failed")
          }
        }}
        onDelete={
          editing
            ? async () => {
                try {
                  await deleteMut.mutateAsync({ itineraryId: editing.id })
                  toast.success("Itinerary deleted")
                  setSheetOpen(false)
                } catch {
                  toast.error("Delete failed")
                }
              }
            : undefined
        }
      />
    </div>
  )
}

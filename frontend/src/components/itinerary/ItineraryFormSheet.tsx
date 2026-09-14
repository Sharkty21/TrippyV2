import { useEffect, useMemo, useState } from "react"

import type { ItinerarySummary } from "@/api/generated/models"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
import { Textarea } from "@/components/ui/textarea"

export type ItineraryFormValues = {
  name: string
  description: string
  start_date: string
}

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  initial: ItinerarySummary | null
  onSubmit: (values: ItineraryFormValues) => Promise<void>
  onDelete?: () => Promise<void>
  saving?: boolean
}

const TRIP_DAYS = 3

function addDays(dateStr: string, days: number): string {
  if (!dateStr) return ""
  const d = new Date(dateStr + "T12:00:00")
  d.setDate(d.getDate() + days)
  return d.toISOString().slice(0, 10)
}

function formatDisplay(dateStr: string): string {
  if (!dateStr) return ""
  return new Date(dateStr + "T12:00:00").toLocaleDateString(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
  })
}

export function ItineraryFormSheet({
  open,
  onOpenChange,
  initial,
  onSubmit,
  onDelete,
  saving,
}: Props) {
  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [startDate, setStartDate] = useState("")

  useEffect(() => {
    if (!open) return
    setName(initial?.name ?? "")
    setDescription(initial?.description ?? "")
    setStartDate(initial?.start_date ?? "")
  }, [open, initial])

  const endDate = useMemo(() => addDays(startDate, TRIP_DAYS - 1), [startDate])

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{initial ? "Edit itinerary" : "New itinerary"}</SheetTitle>
          <SheetDescription>
            Trips are always {TRIP_DAYS} days — pick a start date and we'll set up the days.
          </SheetDescription>
        </SheetHeader>
        <form
          className="flex flex-1 flex-col gap-4 px-4"
          onSubmit={(e) => {
            e.preventDefault()
            void onSubmit({ name, description, start_date: startDate })
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="itin-name">Name</Label>
            <Input
              id="itin-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="itin-desc">Description</Label>
            <Textarea
              id="itin-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={4}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="itin-start">Start date</Label>
            <Input
              id="itin-start"
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              required
            />
            {startDate && (
              <p className="text-xs text-muted-foreground">
                {TRIP_DAYS}-day trip: {formatDisplay(startDate)} → {formatDisplay(endDate)}
              </p>
            )}
          </div>
          <SheetFooter className="mt-auto flex-row gap-2 px-0">
            {onDelete && (
              <Button
                type="button"
                variant="destructive"
                disabled={saving}
                onClick={() => void onDelete()}
              >
                Delete
              </Button>
            )}
            <Button type="submit" disabled={saving} className="ml-auto">
              Save
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}

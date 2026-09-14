import { Check, Search } from "lucide-react"
import { useEffect, useMemo, useRef, useState } from "react"

import { useListPlaces } from "@/api/generated"
import type { ItemRead, PlaceRead } from "@/api/generated/models"
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
import { cn } from "@/lib/utils"

export type ItemFormValues = {
  place_id: string
  description: string
  start_time: string
}

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  initial: ItemRead | null
  onSubmit: (values: ItemFormValues) => Promise<void>
  onDelete?: () => Promise<void>
  saving?: boolean
}

/** Typeahead combobox for picking a Place — single input, dropdown of matches. */
function PlaceCombobox({
  places,
  value,
  onChange,
}: {
  places: PlaceRead[]
  value: string
  onChange: (placeId: string) => void
}) {
  const selected = places.find((p) => p.id === value) ?? null
  const [query, setQuery] = useState(selected ? `${selected.name} — ${selected.city}` : "")
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const current = places.find((p) => p.id === value) ?? null
    setQuery(current ? `${current.name} — ${current.city}` : "")
  }, [value, places])

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener("mousedown", handleClick)
    return () => document.removeEventListener("mousedown", handleClick)
  }, [])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return places.slice(0, 50)
    return places
      .filter((p) =>
        [p.name, p.city, p.type, p.neighborhood ?? ""].some((f) =>
          f.toLowerCase().includes(q),
        ),
      )
      .slice(0, 50)
  }, [places, query])

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          className="pl-8"
          placeholder="Search by name, city, or type…"
          value={query}
          onFocus={() => setOpen(true)}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
            if (!e.target.value) onChange("")
          }}
        />
      </div>
      {open && (
        <div className="absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-md border bg-popover shadow-md">
          {filtered.length === 0 && (
            <p className="px-3 py-2 text-sm text-muted-foreground">No places found</p>
          )}
          {filtered.map((p) => (
            <button
              key={p.id}
              type="button"
              className={cn(
                "flex w-full items-start justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-muted",
                p.id === value && "bg-muted",
              )}
              onClick={() => {
                onChange(p.id)
                setQuery(`${p.name} — ${p.city}`)
                setOpen(false)
              }}
            >
              <span>
                <span className="font-medium">{p.name}</span>
                <span className="text-muted-foreground"> · {p.city}</span>
              </span>
              {p.id === value && <Check className="mt-0.5 size-3.5 shrink-0 text-teal-700" />}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export function ItemFormSheet({
  open,
  onOpenChange,
  initial,
  onSubmit,
  onDelete,
  saving,
}: Props) {
  const { data: places } = useListPlaces(undefined, {
    query: { enabled: open },
  })
  const [placeId, setPlaceId] = useState("")
  const [description, setDescription] = useState("")
  const [startTime, setStartTime] = useState("")

  useEffect(() => {
    if (!open) return
    setPlaceId(initial?.place_id ?? "")
    setDescription(initial?.description ?? "")
    setStartTime(initial?.start_time ?? "")
  }, [open, initial])

  const selectedPlace = places?.find((p) => p.id === placeId)

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{initial ? "Edit stop" : "Add stop"}</SheetTitle>
          <SheetDescription>Search for a place and optionally schedule it.</SheetDescription>
        </SheetHeader>
        <form
          className="flex flex-1 flex-col gap-4 overflow-y-auto px-4"
          onSubmit={(e) => {
            e.preventDefault()
            void onSubmit({ place_id: placeId, description, start_time: startTime })
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="item-search">Find place</Label>
            <PlaceCombobox places={places ?? []} value={placeId} onChange={setPlaceId} />
          </div>

          {selectedPlace && (
            <div className="rounded-md border bg-muted/40 p-2.5 text-xs text-muted-foreground">
              <p className="font-medium text-foreground">{selectedPlace.name}</p>
              <p className="mt-0.5 capitalize">{selectedPlace.type.replaceAll("_", " ")}</p>
              {selectedPlace.hours && <p className="mt-0.5">Hours: {selectedPlace.hours}</p>}
              {selectedPlace.booking_required && (
                <p className="mt-0.5 font-medium text-amber-700">Booking required</p>
              )}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="item-time">Start time (optional)</Label>
            <Input
              id="item-time"
              type="time"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="item-desc">Notes</Label>
            <Textarea
              id="item-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
            />
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
            <Button type="submit" disabled={saving || !placeId} className="ml-auto">
              Save
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}

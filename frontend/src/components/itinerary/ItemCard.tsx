import { useSortable } from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { Clock, GripVertical, Pencil, Star } from "lucide-react"

import type { ItemRead } from "@/api/generated/models"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { sceneryImageUrl } from "@/lib/distance"

type Props = {
  item: ItemRead
  index: number
  onEdit: () => void
}

/** "90" -> "1h 30m"; "45" -> "45m". */
function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes}m`
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return m === 0 ? `${h}h` : `${h}h ${m}m`
}

/** "14:30" -> "2:30 PM" for display. */
function formatTime(time: string): string {
  const [hStr, mStr] = time.split(":")
  const h = Number(hStr)
  const m = Number(mStr)
  if (Number.isNaN(h) || Number.isNaN(m)) return time
  const period = h >= 12 ? "PM" : "AM"
  const h12 = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${m.toString().padStart(2, "0")} ${period}`
}

export function ItemCard({ item, index, onEdit }: Props) {
  const place = item.place
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: item.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.7 : 1,
  }

  const location = [place?.city, place?.region, place?.neighborhood]
    .filter(Boolean)
    .join(" · ")

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="group relative flex gap-3 rounded-xl border bg-white p-3 shadow-sm"
    >
      <button
        type="button"
        className="mt-1 cursor-grab text-muted-foreground active:cursor-grabbing"
        aria-label="Drag to reorder"
        {...attributes}
        {...listeners}
      >
        <GripVertical className="size-4" />
      </button>

      <div className="flex size-7 shrink-0 items-center justify-center rounded-full bg-teal-600 text-xs font-semibold text-white">
        {index + 1}
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-baseline gap-2">
            {item.start_time && (
              <span className="text-xs font-semibold text-teal-700">
                {formatTime(item.start_time)}
              </span>
            )}
            <h4 className="font-semibold text-stone-900">
              {place?.name ?? item.place_id}
            </h4>
          </div>
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="size-7 opacity-0 group-hover:opacity-100"
            onClick={onEdit}
            aria-label="Edit item"
          >
            <Pencil className="size-3.5" />
          </Button>
        </div>

        <div className="mt-1 flex flex-wrap gap-1.5">
          {place?.type && (
            <Badge variant="secondary" className="capitalize">
              {place.type.replaceAll("_", " ")}
            </Badge>
          )}
          {place?.duration_minutes != null && (
            <Badge variant="outline" className="gap-1">
              <Clock className="size-3" />
              {formatDuration(place.duration_minutes)}
            </Badge>
          )}
          {place?.price_range && <Badge variant="outline">{place.price_range}</Badge>}
          {place?.rating != null && (
            <Badge variant="outline" className="gap-1">
              <Star className="size-3 fill-amber-400 text-amber-400" />
              {place.rating.toFixed(1)}
            </Badge>
          )}
          {place?.booking_required && (
            <Badge variant="outline" className="border-amber-300 text-amber-800">
              Booking required
            </Badge>
          )}
        </div>

        {location && (
          <p className="mt-1 text-xs text-muted-foreground">{location}</p>
        )}
        {place?.hours && (
          <p className="mt-1 text-xs text-muted-foreground">Hours: {place.hours}</p>
        )}
        {place?.seasonal_notes && (
          <p className="mt-0.5 text-xs text-amber-700">{place.seasonal_notes}</p>
        )}
        <p className="mt-1 line-clamp-2 text-sm text-stone-600">
          {item.description || place?.description}
        </p>
      </div>

      <img
        src={sceneryImageUrl(item.place_id)}
        alt=""
        className="h-20 w-24 shrink-0 rounded-lg object-cover"
        loading="lazy"
      />
    </div>
  )
}

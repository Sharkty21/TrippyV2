import { Car, Footprints } from "lucide-react"

import type { ItemRead } from "@/api/generated/models"
import { estimateTravel, haversineMiles } from "@/lib/distance"

type Props = {
  fromItem: ItemRead
  toItem: ItemRead
}

function parseTime(time: string): number {
  const [h, m] = time.split(":").map(Number)
  return h * 60 + m
}

export function TravelConnector({ fromItem, toItem }: Props) {
  const from = fromItem.place!
  const to = toItem.place!
  const miles = haversineMiles(
    from.latitude,
    from.longitude,
    to.latitude,
    to.longitude,
  )
  const { mode, minutes, miles: rounded } = estimateTravel(miles)
  const Icon = mode === "drive" ? Car : Footprints

  let overlaps = false
  if (fromItem.start_time && toItem.start_time) {
    const prevEnd = parseTime(fromItem.start_time) + (from.duration_minutes ?? 60)
    const nextStart = parseTime(toItem.start_time)
    overlaps = prevEnd + minutes > nextStart
  }

  return (
    <div className="flex items-center gap-2 py-2 pl-5 text-xs text-muted-foreground">
      <div className="flex h-8 w-8 items-center justify-center rounded-full border bg-white">
        <Icon className="size-3.5 text-teal-700" />
      </div>
      <span className={overlaps ? "font-medium text-destructive" : ""}>
        {minutes} min · {rounded} mi {mode === "drive" ? "drive" : "walk"}
        {overlaps && " — overlaps next start time!"}
      </span>
    </div>
  )
}

/** Haversine distance + rough walk/drive ETAs for itinerary connectors. */

const EARTH_RADIUS_MI = 3958.8
const WALK_MPH = 3
const DRIVE_MPH = 25
const WALK_MAX_MI = 2

export function haversineMiles(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number,
): number {
  const toRad = (d: number) => (d * Math.PI) / 180
  const dLat = toRad(lat2 - lat1)
  const dLon = toRad(lon2 - lon1)
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2
  return 2 * EARTH_RADIUS_MI * Math.asin(Math.sqrt(a))
}

export type TravelMode = "walk" | "drive"

export function estimateTravel(
  miles: number,
): { mode: TravelMode; minutes: number; miles: number } {
  const mode: TravelMode = miles > WALK_MAX_MI ? "drive" : "walk"
  const mph = mode === "walk" ? WALK_MPH : DRIVE_MPH
  const minutes = Math.max(1, Math.round((miles / mph) * 60))
  return { mode, minutes, miles: Math.round(miles * 10) / 10 }
}

export function sceneryImageUrl(seed: string, w = 160, h = 120): string {
  // Deterministic free placeholder images (no API key)
  const n = Math.abs(
    [...seed].reduce((acc, c) => acc + c.charCodeAt(0), 0),
  )
  return `https://picsum.photos/seed/trippy-${n}/${w}/${h}`
}

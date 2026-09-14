import L from "leaflet"
import { useEffect, useMemo } from "react"
import { MapContainer, Marker, Popup, TileLayer, useMap } from "react-leaflet"

import type { ItemRead } from "@/api/generated/models"

import "leaflet/dist/leaflet.css"

type Props = {
  items: ItemRead[]
}

function FitBounds({ items }: { items: ItemRead[] }) {
  const map = useMap()
  const positions = useMemo(
    () =>
      items
        .filter((i) => i.place)
        .map(
          (i) =>
            [i.place!.latitude, i.place!.longitude] as [number, number],
        ),
    [items],
  )

  useEffect(() => {
    if (positions.length === 0) return
    if (positions.length === 1) {
      map.setView(positions[0], 14)
      return
    }
    map.fitBounds(L.latLngBounds(positions), { padding: [40, 40] })
  }, [map, positions])

  return null
}

function numberedIcon(n: number) {
  return L.divIcon({
    className: "",
    html: `<div style="background:#0d9488;color:#fff;width:28px;height:28px;border-radius:999px;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.25)">${n}</div>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
  })
}

export function SectionMap({ items }: Props) {
  const withCoords = items.filter((i) => i.place)
  const center: [number, number] =
    withCoords[0]?.place != null
      ? [withCoords[0].place.latitude, withCoords[0].place.longitude]
      : [41.9, 12.5]

  return (
    <MapContainer
      center={center}
      zoom={13}
      className="h-full w-full rounded-xl"
      scrollWheelZoom
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <FitBounds items={withCoords} />
      {withCoords.map((item, index) => (
        <Marker
          key={item.id}
          position={[item.place!.latitude, item.place!.longitude]}
          icon={numberedIcon(index + 1)}
        >
          <Popup>{item.place?.name}</Popup>
        </Marker>
      ))}
    </MapContainer>
  )
}

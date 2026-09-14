import {
  ChevronLeft,
  ChevronRight,
  Map as MapIcon,
  Pencil,
  Plus,
} from "lucide-react"
import { useMemo, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { toast } from "sonner"

import {
  useCreateItem,
  useCreateSection,
  useDeleteItem,
  useDeleteSection,
  useGetItinerary,
  useReorderItems,
  useUpdateItem,
  useUpdateItinerary,
  useUpdateSection,
} from "@/api/generated"
import { useInvalidateItineraries } from "@/api/invalidate"
import type { ItemRead, SectionRead } from "@/api/generated/models"
import { ItineraryFormSheet } from "@/components/itinerary/ItineraryFormSheet"
import { ItemFormSheet } from "@/components/itinerary/ItemFormSheet"
import { SectionBlock } from "@/components/itinerary/SectionBlock"
import { SectionFormSheet } from "@/components/itinerary/SectionFormSheet"
import { SectionMap } from "@/components/itinerary/SectionMap"
import { Accordion } from "@/components/ui/accordion"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

const MAX_DAYS = 3

type SidePanel = "none" | "map"

export function ItineraryDetailPage() {
  const { id = "" } = useParams()
  const invalidate = useInvalidateItineraries()
  const onTripChange = { mutation: { onSuccess: () => invalidate(id) } }

  const { data, isLoading, error } = useGetItinerary(id, {
    query: { enabled: Boolean(id) },
  })
  const updateItinerary = useUpdateItinerary(onTripChange)
  const createSection = useCreateSection(onTripChange)
  const updateSection = useUpdateSection(onTripChange)
  const deleteSection = useDeleteSection(onTripChange)
  const createItem = useCreateItem(onTripChange)
  const updateItem = useUpdateItem(onTripChange)
  const deleteItem = useDeleteItem(onTripChange)
  const reorderItems = useReorderItems(onTripChange)

  const [heroOpen, setHeroOpen] = useState(false)
  const [sectionOpen, setSectionOpen] = useState(false)
  const [editingSection, setEditingSection] = useState<SectionRead | null>(null)
  const [itemOpen, setItemOpen] = useState(false)
  const [editingItem, setEditingItem] = useState<ItemRead | null>(null)
  const [itemSectionId, setItemSectionId] = useState<string | null>(null)
  const [panel, setPanel] = useState<SidePanel>("map")
  const [activeSectionId, setActiveSectionId] = useState<string | undefined>()

  const sections = useMemo(
    () => [...(data?.sections ?? [])].sort((a, b) => a.sequence - b.sequence),
    [data?.sections],
  )
  const atDayCap = sections.length >= MAX_DAYS

  const activeSection =
    sections.find((s) => s.id === activeSectionId) ?? sections[0]
  const panelItems = activeSection?.items ?? []
  const panelOpen = panel !== "none"

  function togglePanel(next: SidePanel) {
    setPanel((current) => (current === next ? "none" : next))
  }

  if (isLoading) {
    return (
      <div className="p-8">
        <Skeleton className="mb-4 h-24 w-full max-w-xl" />
        <Skeleton className="h-[60vh] w-full" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="p-8">
        <p className="text-destructive">Itinerary not found.</p>
        <Link to="/" className="text-sm text-teal-700 underline">
          Back
        </Link>
      </div>
    )
  }

  return (
    <div className="flex h-svh flex-col overflow-hidden bg-stone-50">
      <div className="border-b bg-white">
        <div className="group relative mx-auto max-w-7xl px-4 py-6 sm:px-6">
          <Link
            to="/"
            className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
          >
            <ChevronLeft className="size-4" />
            All itineraries
          </Link>
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-xs font-medium tracking-wide text-teal-700 uppercase">
                Trippy
              </p>
              <h1 className="mt-1 text-3xl font-semibold tracking-tight text-stone-900">
                {data.name}
              </h1>
              <p className="mt-1 text-sm font-medium text-teal-800">
                {data.start_date} → {data.end_date} · {MAX_DAYS}-day trip
              </p>
              <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
                {data.description}
              </p>
            </div>
            <Button
              type="button"
              size="icon"
              variant="secondary"
              className="opacity-0 transition-opacity group-hover:opacity-100"
              onClick={() => setHeroOpen(true)}
              aria-label="Edit itinerary"
            >
              <Pencil className="size-4" />
            </Button>
          </div>
        </div>
      </div>

      <div className="mx-auto flex min-h-0 w-full max-w-7xl flex-1 flex-col lg:flex-row">
        <section
          className={cn(
            "flex min-h-0 flex-col border-r bg-white",
            panelOpen ? "lg:w-[42%]" : "w-full",
          )}
        >
          <div className="flex items-center justify-between gap-2 border-b px-4 py-3">
            <h2 className="font-semibold">Itinerary</h2>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="outline"
                disabled={atDayCap}
                title={atDayCap ? `Trips are capped at ${MAX_DAYS} days` : undefined}
                onClick={() => {
                  setEditingSection(null)
                  setSectionOpen(true)
                }}
              >
                <Plus className="size-3.5" />
                Day
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="lg:hidden"
                onClick={() => togglePanel("map")}
              >
                <MapIcon className="size-4" />
              </Button>
            </div>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-2">
            <Accordion
              type="single"
              collapsible
              value={activeSectionId ?? sections[0]?.id}
              onValueChange={(v) => setActiveSectionId(v || undefined)}
              className="w-full"
            >
              {sections.map((section) => (
                <SectionBlock
                  key={section.id}
                  section={section}
                  onEditSection={() => {
                    setEditingSection(section)
                    setSectionOpen(true)
                  }}
                  onAddItem={() => {
                    setEditingItem(null)
                    setItemSectionId(section.id)
                    setItemOpen(true)
                  }}
                  onEditItem={(item) => {
                    setEditingItem(item)
                    setItemSectionId(section.id)
                    setItemOpen(true)
                  }}
                  onReorder={(itemIds) => {
                    reorderItems.mutate(
                      { sectionId: section.id, data: { item_ids: itemIds } },
                      { onError: () => toast.error("Reorder failed") },
                    )
                  }}
                />
              ))}
            </Accordion>
            {sections.length === 0 && (
              <p className="py-8 text-sm text-muted-foreground">
                No days yet — add a section to start planning.
              </p>
            )}
          </div>
        </section>

        <aside
          className={cn(
            "isolate relative min-h-[320px] flex-1 bg-stone-100",
            !panelOpen && "hidden lg:block",
          )}
        >
          <div className="absolute top-3 left-3 z-20 flex gap-2">
            {panelOpen ? (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => setPanel("none")}
                className="hidden shadow lg:inline-flex"
              >
                <ChevronRight className="size-3.5" />
                Collapse
              </Button>
            ) : (
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setPanel("map")}
                className="shadow"
              >
                <MapIcon className="size-3.5" />
                Show map
              </Button>
            )}
          </div>
          {panel === "map" && (
            <div className="absolute inset-0 p-3 pt-12">
              {panelItems.length > 0 ? (
                <SectionMap key={activeSection?.id ?? "map"} items={panelItems} />
              ) : (
                <div className="flex h-full items-center justify-center rounded-xl border bg-white text-sm text-muted-foreground">
                  Select a day with stops to see them on the map.
                </div>
              )}
            </div>
          )}
        </aside>
      </div>

      <ItineraryFormSheet
        open={heroOpen}
        onOpenChange={setHeroOpen}
        initial={data}
        saving={updateItinerary.isPending}
        onSubmit={async (values) => {
          try {
            await updateItinerary.mutateAsync({ itineraryId: id, data: values })
            toast.success("Itinerary updated")
            setHeroOpen(false)
          } catch {
            toast.error("Save failed")
          }
        }}
      />

      <SectionFormSheet
        open={sectionOpen}
        onOpenChange={setSectionOpen}
        initial={editingSection}
        saving={createSection.isPending || updateSection.isPending || deleteSection.isPending}
        onSubmit={async (values) => {
          try {
            if (editingSection) {
              await updateSection.mutateAsync({
                sectionId: editingSection.id,
                data: values,
              })
              toast.success("Day updated")
            } else {
              await createSection.mutateAsync({
                data: {
                  itinerary_id: id,
                  ...values,
                },
              })
              toast.success("Day added")
            }
            setSectionOpen(false)
          } catch {
            toast.error("Save failed")
          }
        }}
        onDelete={
          editingSection
            ? async () => {
                try {
                  await deleteSection.mutateAsync({
                    sectionId: editingSection.id,
                  })
                  toast.success("Day deleted")
                  setSectionOpen(false)
                } catch {
                  toast.error("Delete failed")
                }
              }
            : undefined
        }
      />

      <ItemFormSheet
        open={itemOpen}
        onOpenChange={setItemOpen}
        initial={editingItem}
        saving={createItem.isPending || updateItem.isPending || deleteItem.isPending}
        onSubmit={async (values) => {
          try {
            if (editingItem) {
              await updateItem.mutateAsync({
                itemId: editingItem.id,
                data: values,
              })
              toast.success("Stop updated")
            } else if (itemSectionId) {
              await createItem.mutateAsync({
                data: {
                  section_id: itemSectionId,
                  ...values,
                },
              })
              toast.success("Stop added")
            }
            setItemOpen(false)
          } catch {
            toast.error("Save failed")
          }
        }}
        onDelete={
          editingItem
            ? async () => {
                try {
                  await deleteItem.mutateAsync({ itemId: editingItem.id })
                  toast.success("Stop deleted")
                  setItemOpen(false)
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

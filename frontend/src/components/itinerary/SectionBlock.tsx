import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core"
import {
  SortableContext,
  arrayMove,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable"
import { Pencil, Plus } from "lucide-react"
import { useEffect, useState } from "react"

import type { ItemRead, SectionRead } from "@/api/generated/models"
import { ItemCard } from "@/components/itinerary/ItemCard"
import { TravelConnector } from "@/components/itinerary/TravelConnector"
import {
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion"
import { Button } from "@/components/ui/button"

type Props = {
  section: SectionRead
  onEditSection: () => void
  onAddItem: () => void
  onEditItem: (item: ItemRead) => void
  onReorder: (itemIds: string[]) => void
}

export function SectionBlock({
  section,
  onEditSection,
  onAddItem,
  onEditItem,
  onReorder,
}: Props) {
  const [items, setItems] = useState(section.items ?? [])
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
  )

  useEffect(() => {
    setItems(section.items ?? [])
  }, [section.items])

  const dateLabel = new Date(section.date + "T12:00:00").toLocaleDateString(
    undefined,
    { weekday: "long", month: "long", day: "numeric" },
  )

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event
    if (!over || active.id === over.id) return
    const oldIndex = items.findIndex((i) => i.id === active.id)
    const newIndex = items.findIndex((i) => i.id === over.id)
    if (oldIndex < 0 || newIndex < 0) return
    const next = arrayMove(items, oldIndex, newIndex)
    setItems(next)
    onReorder(next.map((i) => i.id))
  }

  return (
    <AccordionItem value={section.id} className="border-none">
      <div className="group mb-1 flex items-start gap-2">
        <AccordionTrigger className="flex-1 py-2 hover:no-underline">
          <div className="text-left">
            <p className="font-semibold text-stone-900">{dateLabel}</p>
            <p className="text-sm text-muted-foreground">
              {section.description || "No subtitle"}
            </p>
          </div>
        </AccordionTrigger>
        <Button
          type="button"
          size="icon"
          variant="ghost"
          className="mt-2 size-8 opacity-0 group-hover:opacity-100"
          onClick={onEditSection}
          aria-label="Edit section"
        >
          <Pencil className="size-3.5" />
        </Button>
      </div>
      <AccordionContent className="pb-4">
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
          <SortableContext
            items={items.map((i) => i.id)}
            strategy={verticalListSortingStrategy}
          >
            <div className="space-y-1">
              {items.map((item, index) => (
                <div key={item.id}>
                  {index > 0 &&
                    items[index - 1]?.place &&
                    item.place && (
                      <TravelConnector
                        fromItem={items[index - 1]}
                        toItem={item}
                      />
                    )}
                  <ItemCard
                    item={item}
                    index={index}
                    onEdit={() => onEditItem(item)}
                  />
                </div>
              ))}
            </div>
          </SortableContext>
        </DndContext>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="mt-3"
          onClick={onAddItem}
        >
          <Plus className="size-3.5" />
          Add stop
        </Button>
      </AccordionContent>
    </AccordionItem>
  )
}

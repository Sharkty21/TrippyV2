import { useEffect, useState } from "react"

import type { SectionRead } from "@/api/generated/models"
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

export type SectionFormValues = {
  date: string
  description: string
}

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  initial: SectionRead | null
  onSubmit: (values: SectionFormValues) => Promise<void>
  onDelete?: () => Promise<void>
  saving?: boolean
}

export function SectionFormSheet({
  open,
  onOpenChange,
  initial,
  onSubmit,
  onDelete,
  saving,
}: Props) {
  const [date, setDate] = useState("")
  const [description, setDescription] = useState("")

  useEffect(() => {
    if (!open) return
    setDate(initial?.date ?? "")
    setDescription(initial?.description ?? "")
  }, [open, initial])

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{initial ? "Edit day" : "Add day"}</SheetTitle>
          <SheetDescription>Date and a short subtitle for the section.</SheetDescription>
        </SheetHeader>
        <form
          className="flex flex-1 flex-col gap-4 px-4"
          onSubmit={(e) => {
            e.preventDefault()
            void onSubmit({ date, description })
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="sec-date">Date</Label>
            <Input
              id="sec-date"
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="sec-desc">Subtitle</Label>
            <Textarea
              id="sec-desc"
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
            <Button type="submit" disabled={saving} className="ml-auto">
              Save
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}

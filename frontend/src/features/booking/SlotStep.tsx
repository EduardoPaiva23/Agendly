import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, Button, Card, EmptyState, Input, Loading } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { addDays, formatDay, formatShortDay, formatTime, salonToday } from '../../lib/datetime'
import { publicApi, publicKeys } from './publicApi'

export interface SelectedSlot {
  date: string
  startAt: string
  professionalId: number
  professionalName: string
}

const MAX_DAYS_AHEAD = 60

export function SlotStep({
  serviceIds,
  professionalId,
  selected,
  conflict,
  onSelect,
  onBack,
  onNext,
}: {
  serviceIds: number[]
  professionalId?: number
  selected: SelectedSlot | null
  conflict: boolean
  onSelect: (slot: SelectedSlot) => void
  onBack: () => void
  onNext: () => void
}) {
  const today = salonToday()
  const [date, setDate] = useState(selected?.date ?? today)
  const quickDays = Array.from({ length: 7 }, (_, i) => addDays(today, i))

  const availability = useQuery({
    queryKey: publicKeys.availability(date, serviceIds, professionalId),
    queryFn: () => publicApi.availability(date, serviceIds, professionalId),
    // Slots change as other customers book: keep them fresh.
    staleTime: 0,
    refetchOnWindowFocus: true,
  })

  const withSlots = availability.data?.professionals.filter((p) => p.slots.length > 0) ?? []

  return (
    <Card>
      <h2 className="mb-4 font-semibold">Escolha a data e o horário</h2>

      {conflict && (
        <div className="mb-4">
          <Alert tone="warning">Esse horário acabou de ser reservado por outra pessoa. Escolha outro, por favor.</Alert>
        </div>
      )}

      <div className="mb-4 flex gap-2 overflow-x-auto pb-1">
        {quickDays.map((d) => (
          <button
            key={d}
            type="button"
            onClick={() => setDate(d)}
            aria-pressed={d === date}
            className={`shrink-0 rounded-lg px-3 py-2 text-xs font-medium capitalize ring-1 ${d === date ? 'bg-rose-600 text-white ring-rose-600' : 'bg-white text-slate-700 ring-slate-200 hover:ring-slate-300'}`}
          >
            {d === today ? 'Hoje' : formatShortDay(d)}
          </button>
        ))}
        <label className="sr-only" htmlFor="booking-date">
          Outra data
        </label>
        <Input
          id="booking-date"
          type="date"
          className="w-40 shrink-0"
          value={date}
          min={today}
          max={addDays(today, MAX_DAYS_AHEAD)}
          onChange={(e) => e.target.value && setDate(e.target.value)}
        />
      </div>

      <p className="mb-3 text-sm font-medium capitalize text-slate-700">{formatDay(date)}</p>

      {availability.isPending ? (
        <Loading label="Buscando horários livres…" />
      ) : availability.isError ? (
        <Alert tone="error">{errorMessage(availability.error)}</Alert>
      ) : withSlots.length === 0 ? (
        <EmptyState title="Não há horários livres nesta data." description="Escolha outro dia acima." />
      ) : (
        <div className="space-y-5">
          {withSlots.map((p) => (
            <div key={p.professionalId}>
              {professionalId === undefined && <p className="mb-2 text-sm font-semibold text-slate-800">{p.professionalName}</p>}
              <div className="grid grid-cols-4 gap-2 sm:grid-cols-6 md:grid-cols-8">
                {p.slots.map((s) => {
                  const isSelected = selected?.startAt === s.startAt && selected.professionalId === p.professionalId
                  return (
                    <button
                      key={s.startAt}
                      type="button"
                      aria-pressed={isSelected}
                      onClick={() => onSelect({ date, startAt: s.startAt, professionalId: p.professionalId, professionalName: p.professionalName })}
                      className={`rounded-md py-2 text-sm font-medium ring-1 ${isSelected ? 'bg-rose-600 text-white ring-rose-600' : 'bg-white text-slate-700 ring-slate-200 hover:ring-rose-400'}`}
                    >
                      {formatTime(s.startAt)}
                    </button>
                  )
                })}
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="mt-5 flex justify-between">
        <Button variant="ghost" onClick={onBack}>
          Voltar
        </Button>
        <Button disabled={!selected || selected.date !== date} onClick={onNext}>
          Continuar
        </Button>
      </div>
    </Card>
  )
}

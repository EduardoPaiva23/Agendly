import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, Button, Card, Input, Loading, PageHeader } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { weekDays } from '../../lib/labels'
import { useAuth } from '../../lib/useAuth'
import type { BusinessHour, DayOfWeek } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'

type Interval = { startTime: string; endTime: string }
type Week = Record<DayOfWeek, Interval[]>

const toWeek = (items: BusinessHour[]): Week => {
  const week = Object.fromEntries(weekDays.map((d) => [d.value, [] as Interval[]])) as Week
  for (const h of items) week[h.dayOfWeek].push({ startTime: h.startTime, endTime: h.endTime })
  return week
}

const toItems = (week: Week): BusinessHour[] =>
  weekDays.flatMap((d) => week[d.value].map((i) => ({ dayOfWeek: d.value, ...i })))

function validate(week: Week): string | null {
  for (const d of weekDays) {
    const sorted = [...week[d.value]].sort((a, b) => a.startTime.localeCompare(b.startTime))
    for (const [i, interval] of sorted.entries()) {
      if (interval.startTime >= interval.endTime) return `${d.label}: o início deve ser antes do fim.`
      if (i > 0 && sorted[i - 1].endTime > interval.startTime) return `${d.label}: os intervalos não podem se sobrepor.`
    }
  }
  return null
}

export function BusinessHoursPage() {
  const hours = useQuery({ queryKey: adminKeys.businessHours, queryFn: adminApi.businessHours })

  return (
    <>
      <PageHeader title="Expediente" description="Horário de funcionamento do salão. Use dois intervalos no mesmo dia para a pausa de almoço." />
      {hours.isPending ? (
        <Loading />
      ) : hours.isError ? (
        <Alert tone="error">{errorMessage(hours.error)}</Alert>
      ) : (
        <WeekEditor key={hours.dataUpdatedAt} initial={toWeek(hours.data)} />
      )}
    </>
  )
}

function WeekEditor({ initial }: { initial: Week }) {
  const { isAdmin } = useAuth()
  const queryClient = useQueryClient()
  const [week, setWeek] = useState<Week>(initial)
  const [localError, setLocalError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const save = useMutation({
    mutationFn: () => adminApi.saveBusinessHours(toItems(week)),
    onSuccess: (data) => {
      queryClient.setQueryData(adminKeys.businessHours, data)
      void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
      setSaved(true)
    },
  })

  const update = (day: DayOfWeek, fn: (intervals: Interval[]) => Interval[]) => {
    setSaved(false)
    setWeek((w) => ({ ...w, [day]: fn(w[day]) }))
  }

  const submit = () => {
    const error = validate(week)
    setLocalError(error)
    if (!error) save.mutate()
  }

  return (
    <Card>
      {!isAdmin && (
        <div className="mb-4">
          <Alert tone="info">Somente administradores podem alterar o expediente.</Alert>
        </div>
      )}
      <ul className="divide-y divide-slate-100">
        {weekDays.map((d) => (
          <li key={d.value} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start">
            <span className="w-36 shrink-0 pt-2 text-sm font-medium text-slate-800">{d.label}</span>
            <div className="flex-1 space-y-2">
              {week[d.value].length === 0 && <p className="pt-2 text-sm text-slate-400">Fechado</p>}
              {week[d.value].map((interval, index) => (
                <div key={index} className="flex items-center gap-2">
                  <label className="sr-only" htmlFor={`${d.value}-${index}-start`}>
                    Início
                  </label>
                  <Input
                    id={`${d.value}-${index}-start`}
                    type="time"
                    step={300}
                    className="w-32"
                    disabled={!isAdmin}
                    value={interval.startTime}
                    onChange={(e) => update(d.value, (list) => list.map((x, i) => (i === index ? { ...x, startTime: e.target.value } : x)))}
                  />
                  <span className="text-slate-400">até</span>
                  <label className="sr-only" htmlFor={`${d.value}-${index}-end`}>
                    Fim
                  </label>
                  <Input
                    id={`${d.value}-${index}-end`}
                    type="time"
                    step={300}
                    className="w-32"
                    disabled={!isAdmin}
                    value={interval.endTime}
                    onChange={(e) => update(d.value, (list) => list.map((x, i) => (i === index ? { ...x, endTime: e.target.value } : x)))}
                  />
                  {isAdmin && (
                    <Button variant="ghost" aria-label="Remover intervalo" onClick={() => update(d.value, (list) => list.filter((_, i) => i !== index))}>
                      ✕
                    </Button>
                  )}
                </div>
              ))}
            </div>
            {isAdmin && (
              <Button
                variant="secondary"
                className="self-start"
                onClick={() =>
                  update(d.value, (list) => [...list, list.length === 0 ? { startTime: '09:00', endTime: '18:00' } : { startTime: '13:00', endTime: '18:00' }])
                }
              >
                Adicionar intervalo
              </Button>
            )}
          </li>
        ))}
      </ul>

      {isAdmin && (
        <div className="mt-4 flex flex-col gap-3 border-t border-slate-200 pt-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex-1">
            {(localError || save.isError) && <Alert tone="error">{localError ?? errorMessage(save.error)}</Alert>}
            {saved && <Alert tone="success">Expediente salvo. Agendamentos existentes não foram alterados.</Alert>}
          </div>
          <Button loading={save.isPending} onClick={submit}>
            Salvar expediente
          </Button>
        </div>
      )}
    </Card>
  )
}

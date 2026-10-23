import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Alert, Button, EmptyState, Field, Input, Loading, Modal } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { formatRange, salonToday, toSalonIso } from '../../lib/datetime'
import type { Professional } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'

export function TimeOffsDialog({ professional, onClose }: { professional: Professional; onClose: () => void }) {
  const queryClient = useQueryClient()
  const today = salonToday()
  const [form, setForm] = useState({ startDate: today, startTime: '12:00', endDate: today, endTime: '13:00', reason: '' })
  const [formError, setFormError] = useState<string | null>(null)

  const timeOffs = useQuery({ queryKey: adminKeys.timeOffs(professional.id), queryFn: () => adminApi.timeOffs(professional.id) })

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.timeOffs(professional.id) })
    void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
  }

  const create = useMutation({
    mutationFn: () =>
      adminApi.createTimeOff(professional.id, {
        startAt: toSalonIso(form.startDate, form.startTime),
        endAt: toSalonIso(form.endDate, form.endTime),
        reason: form.reason.trim() || null,
      }),
    onSuccess: () => {
      invalidate()
      setForm((f) => ({ ...f, reason: '' }))
    },
  })

  const remove = useMutation({
    mutationFn: (timeOffId: number) => adminApi.deleteTimeOff(professional.id, timeOffId),
    onSuccess: invalidate,
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setFormError(null)
    if (`${form.endDate}T${form.endTime}` <= `${form.startDate}T${form.startTime}`) {
      setFormError('O fim deve ser depois do início.')
      return
    }
    create.mutate()
  }

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: e.target.value }))

  return (
    <Modal open wide title={`Folgas e bloqueios · ${professional.name}`} onClose={onClose}>
      <form onSubmit={submit} className="mb-6 space-y-3 rounded-lg bg-slate-50 p-4 ring-1 ring-slate-200">
        <p className="text-sm font-medium text-slate-700">Novo bloqueio</p>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Field label="Início">{(id) => <Input id={id} type="date" value={form.startDate} onChange={set('startDate')} />}</Field>
          <Field label="Hora">{(id) => <Input id={id} type="time" step={300} value={form.startTime} onChange={set('startTime')} />}</Field>
          <Field label="Fim">{(id) => <Input id={id} type="date" value={form.endDate} onChange={set('endDate')} />}</Field>
          <Field label="Hora">{(id) => <Input id={id} type="time" step={300} value={form.endTime} onChange={set('endTime')} />}</Field>
        </div>
        <Field label="Motivo (opcional)">{(id) => <Input id={id} maxLength={200} value={form.reason} onChange={set('reason')} />}</Field>
        {(formError || create.isError) && <Alert tone="error">{formError ?? errorMessage(create.error)}</Alert>}
        <Button type="submit" loading={create.isPending}>
          Adicionar bloqueio
        </Button>
        <p className="text-xs text-slate-500">Não é possível bloquear um período que já tem atendimentos ativos: reagende ou cancele-os antes.</p>
      </form>

      <p className="mb-2 text-sm font-medium text-slate-700">Próximos bloqueios</p>
      {timeOffs.isPending ? (
        <Loading />
      ) : timeOffs.isError ? (
        <Alert tone="error">{errorMessage(timeOffs.error)}</Alert>
      ) : timeOffs.data.length === 0 ? (
        <EmptyState title="Nenhum bloqueio agendado." />
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg ring-1 ring-slate-200">
          {timeOffs.data.map((t) => (
            <li key={t.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
              <span>
                <span className="font-medium text-slate-800">{formatRange(t.startAt, t.endAt)}</span>
                {t.reason && <span className="block text-xs text-slate-500">{t.reason}</span>}
              </span>
              <Button variant="ghost" loading={remove.isPending && remove.variables === t.id} onClick={() => remove.mutate(t.id)}>
                Remover
              </Button>
            </li>
          ))}
        </ul>
      )}
      {remove.isError && (
        <div className="mt-3">
          <Alert tone="error">{errorMessage(remove.error)}</Alert>
        </div>
      )}
    </Modal>
  )
}

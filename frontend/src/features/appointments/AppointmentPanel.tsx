import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, Button, Field, Input, Loading, Modal, Select, StatusBadge, Textarea } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { currency, formatDateTime, formatPhone, formatRange, formatTime, salonParts, salonToday, toSalonIso } from '../../lib/datetime'
import { allowedTransitions, isActiveStatus, statusLabels } from '../../lib/labels'
import type { Appointment, AppointmentStatus } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'
import { publicApi } from '../booking/publicApi'

type Mode = 'view' | 'reschedule' | 'cancel'

/** Details + lifecycle actions. Every rule is re-validated by the API; buttons only mirror what it allows. */
export function AppointmentPanel({ appointmentId, onClose }: { appointmentId: number | null; onClose: () => void }) {
  const [mode, setMode] = useState<Mode>('view')
  const [lastId, setLastId] = useState(appointmentId)
  if (lastId !== appointmentId) {
    setLastId(appointmentId)
    setMode('view')
  }

  const appointment = useQuery({
    queryKey: adminKeys.appointment(appointmentId ?? 0),
    queryFn: () => adminApi.appointment(appointmentId!),
    enabled: appointmentId !== null,
  })

  const close = () => {
    setMode('view')
    onClose()
  }

  const titles: Record<Mode, string> = { view: 'Agendamento', reschedule: 'Reagendar', cancel: 'Cancelar agendamento' }

  return (
    <Modal open={appointmentId !== null} title={titles[mode]} onClose={close} wide={mode === 'reschedule'}>
      {appointment.isPending ? (
        <Loading />
      ) : appointment.isError ? (
        <Alert tone="error">{errorMessage(appointment.error)}</Alert>
      ) : mode === 'reschedule' ? (
        <RescheduleForm appointment={appointment.data} onDone={() => setMode('view')} />
      ) : mode === 'cancel' ? (
        <CancelForm appointment={appointment.data} onDone={() => setMode('view')} />
      ) : (
        <Details appointment={appointment.data} onReschedule={() => setMode('reschedule')} onCancel={() => setMode('cancel')} />
      )}
    </Modal>
  )
}

function useAppointmentMutation<TArgs>(appointmentId: number, fn: (args: TArgs) => Promise<Appointment>, onDone?: () => void) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: (updated) => {
      queryClient.setQueryData(adminKeys.appointment(appointmentId), updated)
      void queryClient.invalidateQueries({ queryKey: ['admin', 'appointments'] })
      void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard })
      void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
      onDone?.()
    },
  })
}

function Details({ appointment: a, onReschedule, onCancel }: { appointment: Appointment; onReschedule: () => void; onCancel: () => void }) {
  const change = useAppointmentMutation(a.id, (status: AppointmentStatus) => adminApi.changeStatus(a.id, status))
  const next = allowedTransitions[a.status].filter((s) => s !== 'Cancelled')
  const active = isActiveStatus(a.status)

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-semibold text-slate-900">{formatRange(a.startAt, a.endAt)}</p>
          <p className="text-sm text-slate-600">com {a.professionalName}</p>
        </div>
        <StatusBadge status={a.status} />
      </div>

      <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
        <Info label="Cliente" value={a.customerName} />
        <Info label="Telefone" value={<a className="text-rose-700 hover:underline" href={`tel:${a.customerPhone}`}>{formatPhone(a.customerPhone)}</a>} />
        {a.customerEmail && <Info label="E-mail" value={a.customerEmail} />}
        <Info label="Criado em" value={formatDateTime(a.createdAt)} />
        {a.updatedAt && <Info label="Atualizado em" value={formatDateTime(a.updatedAt)} />}
        {a.cancelledAt && <Info label="Cancelado em" value={formatDateTime(a.cancelledAt)} />}
        {a.cancellationReason && <Info label="Motivo do cancelamento" value={a.cancellationReason} />}
      </dl>

      <div>
        <p className="mb-1 text-xs font-medium uppercase tracking-wide text-slate-500">Serviços (valores contratados)</p>
        <ul className="divide-y divide-slate-100 rounded-lg text-sm ring-1 ring-slate-200">
          {a.services.map((s) => (
            <li key={s.serviceId} className="flex justify-between px-3 py-2">
              <span>
                {s.name} <span className="text-xs text-slate-500">· {s.durationMinutes} min</span>
              </span>
              <span>{currency.format(s.priceAtBooking)}</span>
            </li>
          ))}
          <li className="flex justify-between px-3 py-2 font-semibold">
            <span>Total</span>
            <span>{currency.format(a.totalPrice)}</span>
          </li>
        </ul>
      </div>

      {change.isError && <Alert tone="error">{errorMessage(change.error)}</Alert>}

      {active && (
        <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4">
          {next.map((status) => (
            <Button
              key={status}
              variant={status === 'Confirmed' ? 'primary' : 'secondary'}
              loading={change.isPending && change.variables === status}
              disabled={change.isPending}
              onClick={() => change.mutate(status)}
            >
              Marcar como {statusLabels[status].toLowerCase()}
            </Button>
          ))}
          <Button variant="secondary" onClick={onReschedule} disabled={change.isPending}>
            Reagendar
          </Button>
          <Button variant="danger" onClick={onCancel} disabled={change.isPending}>
            Cancelar
          </Button>
        </div>
      )}
    </div>
  )
}

function Info({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="font-medium text-slate-800">{value}</dd>
    </div>
  )
}

function CancelForm({ appointment, onDone }: { appointment: Appointment; onDone: () => void }) {
  const [reason, setReason] = useState('')
  const cancel = useAppointmentMutation(appointment.id, () => adminApi.cancel(appointment.id, reason.trim() || null), onDone)

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">
        {appointment.customerName} · {formatRange(appointment.startAt, appointment.endAt)}. O histórico é mantido e o horário fica livre.
      </p>
      <Field label="Motivo (opcional)">
        {(id) => <Textarea id={id} maxLength={300} value={reason} onChange={(e) => setReason(e.target.value)} />}
      </Field>
      {cancel.isError && <Alert tone="error">{errorMessage(cancel.error)}</Alert>}
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onDone} disabled={cancel.isPending}>
          Voltar
        </Button>
        <Button variant="danger" loading={cancel.isPending} onClick={() => cancel.mutate(undefined)}>
          Confirmar cancelamento
        </Button>
      </div>
    </div>
  )
}

function RescheduleForm({ appointment: a, onDone }: { appointment: Appointment; onDone: () => void }) {
  const current = salonParts(a.startAt)
  const [date, setDate] = useState(current.date)
  const [time, setTime] = useState(current.time)
  const [professionalId, setProfessionalId] = useState(a.professionalId)
  const serviceIds = a.services.map((s) => s.serviceId)

  const professionals = useQuery({ queryKey: adminKeys.professionals, queryFn: adminApi.professionals })
  const eligible = (professionals.data ?? []).filter((p) => p.isActive && serviceIds.every((id) => p.serviceIds.includes(id)))

  // Suggestions only: public availability applies the online minimum notice and counts this appointment as busy.
  const suggestions = useQuery({
    queryKey: ['public', 'availability', 'reschedule', a.id, date, professionalId],
    queryFn: () => publicApi.availability(date, serviceIds, professionalId),
    staleTime: 0,
  })
  const slots = suggestions.data?.professionals.find((p) => p.professionalId === professionalId)?.slots ?? []

  const reschedule = useAppointmentMutation(
    a.id,
    () => adminApi.reschedule(a.id, toSalonIso(date, time), professionalId === a.professionalId ? undefined : professionalId),
    onDone,
  )

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">
        Atual: {formatRange(a.startAt, a.endAt)} com {a.professionalName}. Os valores contratados são mantidos.
      </p>

      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Data">
          {(id) => <Input id={id} type="date" min={salonToday()} value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />}
        </Field>
        <Field label="Horário">
          {(id) => <Input id={id} type="time" step={300} value={time} onChange={(e) => e.target.value && setTime(e.target.value)} />}
        </Field>
        <Field label="Profissional">
          {(id) => (
            <Select id={id} value={professionalId} onChange={(e) => setProfessionalId(Number(e.target.value))}>
              {eligible.length === 0 && <option value={a.professionalId}>{a.professionalName}</option>}
              {eligible.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </div>

      <div>
        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-500">Horários livres sugeridos</p>
        {suggestions.isPending ? (
          <Loading />
        ) : slots.length === 0 ? (
          <p className="text-sm text-slate-500">Nenhuma sugestão para esta data. Você pode informar um horário manualmente.</p>
        ) : (
          <div className="grid grid-cols-4 gap-2 sm:grid-cols-8">
            {slots.map((s) => {
              const t = formatTime(s.startAt)
              return (
                <button
                  key={s.startAt}
                  type="button"
                  aria-pressed={t === time}
                  onClick={() => setTime(t)}
                  className={`rounded-md py-1.5 text-sm ring-1 ${t === time ? 'bg-rose-600 text-white ring-rose-600' : 'ring-slate-200 hover:ring-rose-400'}`}
                >
                  {t}
                </button>
              )
            })}
          </div>
        )}
      </div>

      {reschedule.isError && <Alert tone="error">{errorMessage(reschedule.error)}</Alert>}

      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onDone} disabled={reschedule.isPending}>
          Voltar
        </Button>
        <Button loading={reschedule.isPending} onClick={() => reschedule.mutate(undefined)}>
          Confirmar reagendamento
        </Button>
      </div>
    </div>
  )
}

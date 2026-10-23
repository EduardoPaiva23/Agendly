import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { Alert, Button, Card, Field, Input, Loading, StatusBadge, Textarea } from '../../components/ui'
import { ApiError, errorMessage } from '../../lib/api'
import { currency, formatLongDate, formatTime } from '../../lib/datetime'
import { isActiveStatus } from '../../lib/labels'
import { publicApi, publicKeys } from './publicApi'

export function LookupPage() {
  const { reference } = useParams()
  return reference ? <AppointmentView reference={reference} /> : <ReferenceForm />
}

function ReferenceForm() {
  const navigate = useNavigate()
  const [value, setValue] = useState('')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    // Accept either the bare code or the full link the customer received.
    const code = value.trim().split('/').filter(Boolean).pop()
    if (code) navigate(`/agendamento/${code}`)
  }

  return (
    <Card className="mx-auto max-w-md">
      <h1 className="text-xl font-bold">Meu agendamento</h1>
      <p className="mb-4 mt-1 text-sm text-slate-500">Cole o link ou o código recebido ao agendar.</p>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Link ou código">
          {(id) => <Input id={id} value={value} onChange={(e) => setValue(e.target.value)} autoComplete="off" />}
        </Field>
        <Button type="submit" disabled={!value.trim()} className="w-full">
          Consultar
        </Button>
      </form>
    </Card>
  )
}

function AppointmentView({ reference }: { reference: string }) {
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const [reason, setReason] = useState('')

  const appointment = useQuery({ queryKey: publicKeys.appointment(reference), queryFn: () => publicApi.appointment(reference) })

  const cancel = useMutation({
    mutationFn: () => publicApi.cancel(reference, reason.trim() || null),
    onSuccess: (updated) => {
      queryClient.setQueryData(publicKeys.appointment(reference), updated)
      void queryClient.invalidateQueries({ queryKey: ['public', 'availability'] })
      setConfirming(false)
    },
  })

  if (appointment.isPending) return <Loading />
  if (appointment.isError) {
    const notFound = appointment.error instanceof ApiError && appointment.error.status === 404
    return (
      <Card className="mx-auto max-w-md space-y-3 text-center">
        <h1 className="text-lg font-semibold">{notFound ? 'Agendamento não encontrado' : 'Não foi possível carregar'}</h1>
        <p className="text-sm text-slate-500">{notFound ? 'Confira se o link ou código está completo.' : errorMessage(appointment.error)}</p>
      </Card>
    )
  }

  const a = appointment.data
  const active = isActiveStatus(a.status)

  return (
    <Card className="mx-auto max-w-xl space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold first-letter:uppercase">{formatLongDate(a.startAt)}</h1>
          <p className="text-slate-600">
            {formatTime(a.startAt)} – {formatTime(a.endAt)} · {a.professionalName}
          </p>
        </div>
        <StatusBadge status={a.status} />
      </div>

      <ul className="divide-y divide-slate-100 rounded-lg text-sm ring-1 ring-slate-200">
        {a.services.map((s) => (
          <li key={s.serviceId} className="flex justify-between px-4 py-2">
            <span>{s.name}</span>
            <span>{currency.format(s.priceAtBooking)}</span>
          </li>
        ))}
        <li className="flex justify-between px-4 py-2 font-semibold">
          <span>Total</span>
          <span>{currency.format(a.totalPrice)}</span>
        </li>
      </ul>

      {a.status === 'Cancelled' && <Alert tone="info">Este agendamento foi cancelado.</Alert>}

      {active && !a.canCancelOnline && (
        <Alert tone="warning">O prazo para cancelar online já passou. Para alterações, entre em contato com o salão.</Alert>
      )}

      {active && a.canCancelOnline && !confirming && (
        <Button variant="danger" onClick={() => setConfirming(true)}>
          Cancelar agendamento
        </Button>
      )}

      {confirming && (
        <div className="space-y-3 rounded-lg bg-rose-50 p-4 ring-1 ring-rose-200">
          <p className="text-sm font-medium text-rose-900">Tem certeza? O horário será liberado para outras pessoas.</p>
          <Field label="Motivo (opcional)">
            {(id) => <Textarea id={id} maxLength={300} value={reason} onChange={(e) => setReason(e.target.value)} />}
          </Field>
          {cancel.isError && <Alert tone="error">{errorMessage(cancel.error)}</Alert>}
          <div className="flex gap-2">
            <Button variant="danger" loading={cancel.isPending} onClick={() => cancel.mutate()}>
              Confirmar cancelamento
            </Button>
            <Button variant="ghost" onClick={() => setConfirming(false)} disabled={cancel.isPending}>
              Manter agendamento
            </Button>
          </div>
        </div>
      )}
    </Card>
  )
}

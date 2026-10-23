import { useState } from 'react'
import { Link } from 'react-router'
import { Alert, Button, Card } from '../../components/ui'
import { currency, formatLongDate, formatTime } from '../../lib/datetime'
import type { PublicAppointment } from '../../types/api'

export function BookingSuccess({ appointment, onNew }: { appointment: PublicAppointment; onNew: () => void }) {
  const [copied, setCopied] = useState(false)
  const link = `${window.location.origin}/agendamento/${appointment.reference}`

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(link)
      setCopied(true)
    } catch {
      setCopied(false)
    }
  }

  return (
    <Card className="mx-auto max-w-xl space-y-5 text-center">
      <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-emerald-100 text-2xl text-emerald-700" aria-hidden>
        ✓
      </div>
      <div>
        <h1 className="text-xl font-bold">Agendamento realizado!</h1>
        <p className="mt-1 text-slate-600 first-letter:uppercase">
          {formatLongDate(appointment.startAt)}, às {formatTime(appointment.startAt)}
        </p>
        <p className="text-sm text-slate-500">com {appointment.professionalName}</p>
      </div>

      <ul className="divide-y divide-slate-100 rounded-lg text-left text-sm ring-1 ring-slate-200">
        {appointment.services.map((s) => (
          <li key={s.serviceId} className="flex justify-between px-4 py-2">
            <span>{s.name}</span>
            <span>{currency.format(s.priceAtBooking)}</span>
          </li>
        ))}
        <li className="flex justify-between px-4 py-2 font-semibold">
          <span>Total</span>
          <span>{currency.format(appointment.totalPrice)}</span>
        </li>
      </ul>

      <Alert tone="info">
        <p className="font-medium">Guarde o link do seu agendamento</p>
        <p className="mt-1 break-all text-xs">{link}</p>
        <p className="mt-1 text-xs">Com ele você consulta ou cancela o horário. Ele é pessoal: não compartilhe.</p>
      </Alert>

      <div className="flex flex-col justify-center gap-2 sm:flex-row">
        <Button variant="secondary" onClick={copy}>
          {copied ? 'Link copiado!' : 'Copiar link'}
        </Button>
        <Link
          to={`/agendamento/${appointment.reference}`}
          className="inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-semibold text-rose-700 ring-1 ring-inset ring-rose-200 hover:bg-rose-50"
        >
          Ver agendamento
        </Link>
        <Button variant="ghost" onClick={onNew}>
          Novo agendamento
        </Button>
      </div>
    </Card>
  )
}

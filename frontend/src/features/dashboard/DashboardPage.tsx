import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert, Card, EmptyState, Loading, PageHeader, StatusBadge } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { addDays, currency, formatDay, formatTime, salonToday, toSalonIso } from '../../lib/datetime'
import { adminApi, adminKeys } from '../adminApi'
import { AppointmentPanel } from '../appointments/AppointmentPanel'

export function DashboardPage() {
  const [openId, setOpenId] = useState<number | null>(null)
  const today = salonToday()
  const from = toSalonIso(today, '00:00')
  const to = toSalonIso(addDays(today, 1), '00:00')

  const summary = useQuery({ queryKey: adminKeys.dashboard, queryFn: adminApi.dashboard })
  const agenda = useQuery({ queryKey: adminKeys.calendar(from, to), queryFn: () => adminApi.calendar(from, to) })

  return (
    <>
      <PageHeader title="Painel" description={<span className="capitalize">{formatDay(today)}</span>} />

      {summary.isError && <Alert tone="error">{errorMessage(summary.error)}</Alert>}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Atendimentos hoje" value={summary.data?.todayAppointments} />
        <Stat label="Próximos 7 dias" value={summary.data?.nextSevenDaysAppointments} />
        <Stat label="Aguardando confirmação" value={summary.data?.pendingConfirmation} />
        <Stat label="Clientes cadastrados" value={summary.data?.totalCustomers} />
        <Stat label="Receita realizada no mês" value={summary.data && currency.format(summary.data.monthCompletedRevenue)} />
        <Stat
          label="Receita prevista no mês"
          value={summary.data && currency.format(summary.data.monthExpectedRevenue)}
          hint="Agendados, confirmados e concluídos"
        />
        <Stat label="Profissionais ativos" value={summary.data?.activeProfessionals} />
      </div>

      <Card className="mt-6">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="font-semibold">Agenda de hoje</h2>
          <Link to="/admin/agenda" className="text-sm font-medium text-rose-700 hover:underline">
            Abrir calendário
          </Link>
        </div>
        {agenda.isPending ? (
          <Loading />
        ) : agenda.isError ? (
          <Alert tone="error">{errorMessage(agenda.error)}</Alert>
        ) : agenda.data.length === 0 ? (
          <EmptyState title="Nenhum atendimento hoje." />
        ) : (
          <ul className="divide-y divide-slate-100">
            {agenda.data.map((a) => (
              <li key={a.id}>
                <button
                  type="button"
                  onClick={() => setOpenId(a.id)}
                  className="flex w-full flex-wrap items-center gap-x-4 gap-y-1 px-1 py-3 text-left hover:bg-slate-50"
                >
                  <span className="w-28 font-mono text-sm text-slate-700">
                    {formatTime(a.startAt)}–{formatTime(a.endAt)}
                  </span>
                  <span className="min-w-40 flex-1">
                    <span className="block font-medium text-slate-900">{a.customerName}</span>
                    <span className="block text-xs text-slate-500">
                      {a.servicesSummary} · {a.professionalName}
                    </span>
                  </span>
                  <StatusBadge status={a.status} />
                </button>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <AppointmentPanel appointmentId={openId} onClose={() => setOpenId(null)} />
    </>
  )
}

function Stat({ label, value, hint }: { label: string; value: string | number | undefined; hint?: string }) {
  return (
    <Card className="p-4">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-bold text-slate-900">{value ?? '–'}</p>
      {hint && <p className="mt-0.5 text-xs text-slate-400">{hint}</p>}
    </Card>
  )
}

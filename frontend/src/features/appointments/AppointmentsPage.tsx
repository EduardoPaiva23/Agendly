import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, Card, EmptyState, Field, Input, Loading, PageHeader, Pagination, Select, StatusBadge, Table, Td, Th } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { addDays, currency, formatDate, formatTime, salonToday, toSalonIso } from '../../lib/datetime'
import { statusLabels } from '../../lib/labels'
import type { AppointmentStatus } from '../../types/api'
import { adminApi, adminKeys, type AppointmentFilters } from '../adminApi'
import { AppointmentPanel } from './AppointmentPanel'

const statuses = Object.keys(statusLabels) as AppointmentStatus[]

export function AppointmentsPage() {
  const today = salonToday()
  const [fromDate, setFromDate] = useState(today)
  const [toDate, setToDate] = useState(addDays(today, 30))
  const [professionalId, setProfessionalId] = useState<number | undefined>()
  const [status, setStatus] = useState<AppointmentStatus | undefined>()
  const [page, setPage] = useState(1)
  const [openId, setOpenId] = useState<number | null>(null)

  const filters: AppointmentFilters = {
    from: fromDate ? toSalonIso(fromDate, '00:00') : undefined,
    to: toDate ? toSalonIso(addDays(toDate, 1), '00:00') : undefined,
    professionalId,
    status,
    page,
    pageSize: 20,
  }

  const professionals = useQuery({ queryKey: adminKeys.professionals, queryFn: adminApi.professionals })
  const list = useQuery({
    queryKey: adminKeys.appointments(filters),
    queryFn: () => adminApi.appointments(filters),
    placeholderData: keepPreviousData,
  })

  const resetPage = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v)
    setPage(1)
  }

  return (
    <>
      <PageHeader title="Agendamentos" description="Busque por período, profissional e status." />

      <Card className="mb-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Field label="De">{(id) => <Input id={id} type="date" value={fromDate} onChange={(e) => resetPage(setFromDate)(e.target.value)} />}</Field>
        <Field label="Até">{(id) => <Input id={id} type="date" value={toDate} onChange={(e) => resetPage(setToDate)(e.target.value)} />}</Field>
        <Field label="Profissional">
          {(id) => (
            <Select
              id={id}
              value={professionalId ?? ''}
              onChange={(e) => resetPage(setProfessionalId)(e.target.value ? Number(e.target.value) : undefined)}
            >
              <option value="">Todos</option>
              {professionals.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Field label="Status">
          {(id) => (
            <Select id={id} value={status ?? ''} onChange={(e) => resetPage(setStatus)((e.target.value || undefined) as AppointmentStatus | undefined)}>
              <option value="">Todos</option>
              {statuses.map((s) => (
                <option key={s} value={s}>
                  {statusLabels[s]}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </Card>

      <Card>
        {list.isPending ? (
          <Loading />
        ) : list.isError ? (
          <Alert tone="error">{errorMessage(list.error)}</Alert>
        ) : list.data.items.length === 0 ? (
          <EmptyState title="Nenhum agendamento encontrado." description="Ajuste os filtros acima." />
        ) : (
          <>
            <p className="mb-3 text-xs text-slate-500">{list.data.totalCount} agendamento(s)</p>
            <Table
              head={
                <tr>
                  <Th>Data</Th>
                  <Th>Cliente</Th>
                  <Th className="hidden md:table-cell">Serviços</Th>
                  <Th className="hidden sm:table-cell">Profissional</Th>
                  <Th>Status</Th>
                  <Th className="hidden text-right md:table-cell">Total</Th>
                </tr>
              }
            >
              {list.data.items.map((a) => (
                <tr key={a.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpenId(a.id)}>
                  <Td className="whitespace-nowrap">
                    {formatDate(a.startAt)} <span className="text-slate-500">{formatTime(a.startAt)}</span>
                  </Td>
                  <Td>
                    <button type="button" className="text-left font-medium text-slate-900 hover:text-rose-700" onClick={() => setOpenId(a.id)}>
                      {a.customerName}
                    </button>
                  </Td>
                  <Td className="hidden md:table-cell">{a.services.map((s) => s.name).join(' + ')}</Td>
                  <Td className="hidden sm:table-cell">{a.professionalName}</Td>
                  <Td>
                    <StatusBadge status={a.status} />
                  </Td>
                  <Td className="hidden text-right md:table-cell">{currency.format(a.totalPrice)}</Td>
                </tr>
              ))}
            </Table>
            <Pagination page={list.data.page} totalPages={list.data.totalPages} onChange={setPage} />
          </>
        )}
      </Card>

      <AppointmentPanel appointmentId={openId} onClose={() => setOpenId(null)} />
    </>
  )
}

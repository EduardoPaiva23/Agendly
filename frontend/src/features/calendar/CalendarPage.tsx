import type { DatesSetArg, EventClickArg, EventInput } from '@fullcalendar/core'
import ptBrLocale from '@fullcalendar/core/locales/pt-br'
import dayGridPlugin from '@fullcalendar/daygrid'
import interactionPlugin from '@fullcalendar/interaction'
import luxonPlugin from '@fullcalendar/luxon3'
import FullCalendar from '@fullcalendar/react'
import timeGridPlugin from '@fullcalendar/timegrid'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Alert, Card, Checkbox, PageHeader, Select, Spinner } from '../../components/ui'
import { errorMessage } from '../../lib/api'
import { SALON_TZ } from '../../lib/datetime'
import { statusColors, statusLabels } from '../../lib/labels'
import type { AppointmentStatus } from '../../types/api'
import { adminApi, adminKeys } from '../adminApi'
import { AppointmentPanel } from '../appointments/AppointmentPanel'

export function CalendarPage() {
  const [range, setRange] = useState<{ from: string; to: string } | null>(null)
  const [professionalId, setProfessionalId] = useState<number | undefined>()
  const [includeCancelled, setIncludeCancelled] = useState(false)
  const [openId, setOpenId] = useState<number | null>(null)

  const professionals = useQuery({ queryKey: adminKeys.professionals, queryFn: adminApi.professionals })
  const calendar = useQuery({
    queryKey: adminKeys.calendar(range?.from ?? '', range?.to ?? '', professionalId, includeCancelled),
    queryFn: () => adminApi.calendar(range!.from, range!.to, professionalId, includeCancelled),
    enabled: range !== null,
    placeholderData: keepPreviousData,
  })

  const events = useMemo<EventInput[]>(
    () =>
      (calendar.data ?? []).map((a) => ({
        id: String(a.id),
        title: `${a.customerName} · ${a.servicesSummary}`,
        start: a.startAt,
        end: a.endAt,
        backgroundColor: statusColors[a.status].event,
        borderColor: statusColors[a.status].event,
        classNames: a.status === 'Cancelled' ? ['opacity-60', 'line-through'] : [],
        extendedProps: { professionalName: a.professionalName },
      })),
    [calendar.data],
  )

  const onDatesSet = (arg: DatesSetArg) => {
    const next = { from: arg.start.toISOString(), to: arg.end.toISOString() }
    setRange((current) => (current?.from === next.from && current.to === next.to ? current : next))
  }

  const onEventClick = (arg: EventClickArg) => setOpenId(Number(arg.event.id))

  return (
    <>
      <PageHeader
        title="Agenda"
        description="Clique em um atendimento para ver detalhes, confirmar, reagendar ou cancelar."
        actions={calendar.isFetching && <Spinner small />}
      />

      <Card className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <label className="text-sm font-medium text-slate-700" htmlFor="calendar-professional">
            Profissional
          </label>
          <Select
            id="calendar-professional"
            className="sm:w-56"
            value={professionalId ?? ''}
            onChange={(e) => setProfessionalId(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">Todos</option>
            {professionals.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {!p.isActive && ' (inativo)'}
              </option>
            ))}
          </Select>
          <Checkbox label="Mostrar cancelados" checked={includeCancelled} onChange={(e) => setIncludeCancelled(e.target.checked)} />
        </div>
        <ul className="flex flex-wrap gap-3 text-xs text-slate-600">
          {(Object.keys(statusLabels) as AppointmentStatus[]).map((s) => (
            <li key={s} className="flex items-center gap-1.5">
              <span className="size-3 rounded-sm" style={{ backgroundColor: statusColors[s].event }} aria-hidden />
              {statusLabels[s]}
            </li>
          ))}
        </ul>
      </Card>

      {calendar.isError && (
        <div className="mb-4">
          <Alert tone="error">{errorMessage(calendar.error)}</Alert>
        </div>
      )}

      <Card className="p-2 sm:p-4">
        <FullCalendar
          plugins={[timeGridPlugin, dayGridPlugin, interactionPlugin, luxonPlugin]}
          locale={ptBrLocale}
          timeZone={SALON_TZ}
          initialView={window.innerWidth < 640 ? 'timeGridDay' : 'timeGridWeek'}
          headerToolbar={{ left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth' }}
          height="auto"
          allDaySlot={false}
          nowIndicator
          slotMinTime="07:00:00"
          slotMaxTime="21:00:00"
          slotDuration="00:15:00"
          slotLabelInterval="01:00"
          expandRows
          events={events}
          datesSet={onDatesSet}
          eventClick={onEventClick}
          eventTimeFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
        />
      </Card>

      <AppointmentPanel appointmentId={openId} onClose={() => setOpenId(null)} />
    </>
  )
}

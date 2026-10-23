import type { AppointmentStatus, DayOfWeek } from '../types/api'

export const statusLabels: Record<AppointmentStatus, string> = {
  Scheduled: 'Agendado',
  Confirmed: 'Confirmado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
}

export const statusColors: Record<AppointmentStatus, { badge: string; event: string }> = {
  Scheduled: { badge: 'bg-amber-100 text-amber-800 ring-amber-200', event: '#d97706' },
  Confirmed: { badge: 'bg-emerald-100 text-emerald-800 ring-emerald-200', event: '#059669' },
  Completed: { badge: 'bg-slate-100 text-slate-700 ring-slate-200', event: '#64748b' },
  Cancelled: { badge: 'bg-rose-100 text-rose-700 ring-rose-200', event: '#e11d48' },
}

/** Mirrors the backend state machine (Appointment.AllowedTransitions) to decide which buttons to show. */
export const allowedTransitions: Record<AppointmentStatus, AppointmentStatus[]> = {
  Scheduled: ['Confirmed', 'Completed', 'Cancelled'],
  Confirmed: ['Completed', 'Cancelled'],
  Completed: [],
  Cancelled: [],
}

export const isActiveStatus = (status: AppointmentStatus) => status === 'Scheduled' || status === 'Confirmed'

export const weekDays: { value: DayOfWeek; label: string }[] = [
  { value: 'Monday', label: 'Segunda-feira' },
  { value: 'Tuesday', label: 'Terça-feira' },
  { value: 'Wednesday', label: 'Quarta-feira' },
  { value: 'Thursday', label: 'Quinta-feira' },
  { value: 'Friday', label: 'Sexta-feira' },
  { value: 'Saturday', label: 'Sábado' },
  { value: 'Sunday', label: 'Domingo' },
]

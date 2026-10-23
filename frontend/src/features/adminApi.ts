import { api } from '../lib/api'
import type {
  Appointment,
  AppointmentStatus,
  BusinessHour,
  CalendarAppointment,
  Customer,
  CustomerRequest,
  DashboardSummary,
  PagedResult,
  Professional,
  ProfessionalRequest,
  Service,
  ServiceRequest,
  TimeOff,
} from '../types/api'

export interface AppointmentFilters {
  from?: string
  to?: string
  professionalId?: number
  status?: AppointmentStatus
  page?: number
  pageSize?: number
}

export const adminKeys = {
  dashboard: ['admin', 'dashboard'] as const,
  calendar: (from: string, to: string, professionalId?: number, includeCancelled?: boolean) =>
    ['admin', 'appointments', 'calendar', from, to, professionalId ?? 'all', includeCancelled ?? false] as const,
  appointments: (filters: AppointmentFilters) => ['admin', 'appointments', 'list', filters] as const,
  appointment: (id: number) => ['admin', 'appointments', 'detail', id] as const,
  customers: (search: string, page: number) => ['admin', 'customers', search, page] as const,
  services: ['admin', 'services'] as const,
  professionals: ['admin', 'professionals'] as const,
  timeOffs: (professionalId: number) => ['admin', 'time-offs', professionalId] as const,
  businessHours: ['admin', 'business-hours'] as const,
}

export const adminApi = {
  dashboard: () => api.get<DashboardSummary>('/api/dashboard/summary'),
  calendar: (from: string, to: string, professionalId?: number, includeCancelled = false) =>
    api.get<CalendarAppointment[]>('/api/dashboard/appointments', { from, to, professionalId, includeCancelled }),

  appointments: (filters: AppointmentFilters) => api.get<PagedResult<Appointment>>('/api/appointments', { ...filters }),
  appointment: (id: number) => api.get<Appointment>(`/api/appointments/${id}`),
  changeStatus: (id: number, status: AppointmentStatus, reason?: string | null) =>
    api.patch<Appointment>(`/api/appointments/${id}/status`, { status, reason: reason ?? null }),
  reschedule: (id: number, startAt: string, professionalId?: number) =>
    api.post<Appointment>(`/api/appointments/${id}/reschedule`, { startAt, professionalId: professionalId ?? null }),
  cancel: (id: number, reason: string | null) => api.post<Appointment>(`/api/appointments/${id}/cancel`, { reason }),

  customers: (search: string, page: number) => api.get<PagedResult<Customer>>('/api/customers', { search, page, pageSize: 20 }),
  createCustomer: (body: CustomerRequest) => api.post<Customer>('/api/customers', body),
  updateCustomer: (id: number, body: CustomerRequest) => api.put<Customer>(`/api/customers/${id}`, body),

  services: () => api.get<Service[]>('/api/services', { includeInactive: true }),
  createService: (body: ServiceRequest) => api.post<Service>('/api/services', body),
  updateService: (id: number, body: ServiceRequest) => api.put<Service>(`/api/services/${id}`, body),

  professionals: () => api.get<Professional[]>('/api/professionals', { includeInactive: true }),
  createProfessional: (body: ProfessionalRequest) => api.post<Professional>('/api/professionals', body),
  updateProfessional: (id: number, body: ProfessionalRequest) => api.put<Professional>(`/api/professionals/${id}`, body),
  setProfessionalServices: (id: number, serviceIds: number[]) =>
    api.put<Professional>(`/api/professionals/${id}/services`, { serviceIds }),

  timeOffs: (professionalId: number) => api.get<TimeOff[]>(`/api/professionals/${professionalId}/time-offs`),
  createTimeOff: (professionalId: number, body: { startAt: string; endAt: string; reason: string | null }) =>
    api.post<TimeOff>(`/api/professionals/${professionalId}/time-offs`, body),
  deleteTimeOff: (professionalId: number, timeOffId: number) => api.delete(`/api/professionals/${professionalId}/time-offs/${timeOffId}`),

  businessHours: () => api.get<BusinessHour[]>('/api/business-hours'),
  saveBusinessHours: (items: BusinessHour[]) => api.put<BusinessHour[]>('/api/business-hours', { items }),
}

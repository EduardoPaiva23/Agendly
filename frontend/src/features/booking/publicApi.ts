import { api } from '../../lib/api'
import type {
  AvailabilityResponse,
  CreatePublicAppointmentRequest,
  PublicAppointment,
  PublicProfessional,
  PublicService,
} from '../../types/api'

export const publicKeys = {
  services: ['public', 'services'] as const,
  professionals: ['public', 'professionals'] as const,
  availability: (date: string, serviceIds: number[], professionalId?: number) =>
    ['public', 'availability', date, serviceIds.join(','), professionalId ?? 'any'] as const,
  appointment: (reference: string) => ['public', 'appointment', reference] as const,
}

export const publicApi = {
  services: () => api.get<PublicService[]>('/api/public/services'),
  professionals: () => api.get<PublicProfessional[]>('/api/public/professionals'),
  availability: (date: string, serviceIds: number[], professionalId?: number) =>
    api.get<AvailabilityResponse>('/api/public/availability', { date, serviceIds, professionalId }),
  book: (request: CreatePublicAppointmentRequest) => api.post<PublicAppointment>('/api/public/appointments', request),
  appointment: (reference: string) => api.get<PublicAppointment>(`/api/public/appointments/${encodeURIComponent(reference)}`),
  cancel: (reference: string, reason: string | null) =>
    api.post<PublicAppointment>(`/api/public/appointments/${encodeURIComponent(reference)}/cancel`, { reason }),
}

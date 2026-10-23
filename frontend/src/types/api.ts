export type AppointmentStatus = 'Scheduled' | 'Confirmed' | 'Completed' | 'Cancelled'

export type DayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

// Auth
export interface CurrentUser {
  id: string
  email: string
  fullName: string
  roles: string[]
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

// Public
export interface PublicService {
  id: number
  name: string
  description: string | null
  durationMinutes: number
  price: number
}

export interface PublicProfessional {
  id: number
  name: string
  serviceIds: number[]
}

export interface Slot {
  startAt: string
  endAt: string
}

export interface ProfessionalAvailability {
  professionalId: number
  professionalName: string
  slots: Slot[]
}

export interface AvailabilityResponse {
  date: string
  durationMinutes: number
  totalPrice: number
  professionals: ProfessionalAvailability[]
}

export interface AppointmentServiceItem {
  serviceId: number
  name: string
  priceAtBooking: number
  durationMinutes: number
}

export interface PublicAppointment {
  reference: string
  status: AppointmentStatus
  startAt: string
  endAt: string
  professionalName: string
  services: AppointmentServiceItem[]
  totalPrice: number
  canCancelOnline: boolean
}

export interface CreatePublicAppointmentRequest {
  professionalId: number
  serviceIds: number[]
  startAt: string
  customerName: string
  customerPhone: string
  customerEmail: string | null
}

// Admin
export interface Customer {
  id: number
  name: string
  phone: string
  email: string | null
  createdAt: string
}

export interface CustomerRequest {
  name: string
  phone: string
  email: string | null
}

export interface Service {
  id: number
  name: string
  description: string | null
  durationMinutes: number
  price: number
  isActive: boolean
}

export interface ServiceRequest {
  name: string
  description: string | null
  durationMinutes: number
  price: number
  isActive: boolean
}

export interface Professional {
  id: number
  name: string
  phone: string | null
  isActive: boolean
  serviceIds: number[]
}

export interface ProfessionalRequest {
  name: string
  phone: string | null
  isActive: boolean
}

export interface TimeOff {
  id: number
  professionalId: number
  startAt: string
  endAt: string
  reason: string | null
}

export interface BusinessHour {
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

export interface Appointment {
  id: number
  reference: string
  status: AppointmentStatus
  startAt: string
  endAt: string
  customerId: number
  customerName: string
  customerPhone: string
  customerEmail: string | null
  professionalId: number
  professionalName: string
  services: AppointmentServiceItem[]
  totalPrice: number
  createdAt: string
  updatedAt: string | null
  cancelledAt: string | null
  cancellationReason: string | null
}

export interface CalendarAppointment {
  id: number
  status: AppointmentStatus
  startAt: string
  endAt: string
  professionalId: number
  professionalName: string
  customerName: string
  servicesSummary: string
  totalPrice: number
}

export interface DashboardSummary {
  today: string
  todayAppointments: number
  nextSevenDaysAppointments: number
  pendingConfirmation: number
  monthCompletedRevenue: number
  monthExpectedRevenue: number
  activeProfessionals: number
  totalCustomers: number
}

import { createBrowserRouter } from 'react-router'
import { Loading } from '../components/ui'
import { BookingPage } from '../features/booking/BookingPage'
import { LookupPage } from '../features/booking/LookupPage'
import { AdminLayout } from '../layouts/AdminLayout'
import { PublicLayout } from '../layouts/PublicLayout'
import { NotFoundPage } from '../pages/NotFoundPage'
import { RequireAuth } from './RequireAuth'

// Admin screens (and FullCalendar) are split out so public visitors download only the booking flow.
export const router = createBrowserRouter([
  {
    element: <PublicLayout />,
    children: [
      { path: '/', element: <BookingPage /> },
      { path: '/agendamento', element: <LookupPage /> },
      { path: '/agendamento/:reference', element: <LookupPage /> },
    ],
  },
  {
    path: '/admin/login',
    lazy: async () => ({ Component: (await import('../features/auth/LoginPage')).LoginPage }),
  },
  {
    path: '/admin',
    element: (
      <RequireAuth>
        <AdminLayout />
      </RequireAuth>
    ),
    hydrateFallbackElement: <Loading />,
    children: [
      { index: true, lazy: async () => ({ Component: (await import('../features/dashboard/DashboardPage')).DashboardPage }) },
      { path: 'agenda', lazy: async () => ({ Component: (await import('../features/calendar/CalendarPage')).CalendarPage }) },
      {
        path: 'agendamentos',
        lazy: async () => ({ Component: (await import('../features/appointments/AppointmentsPage')).AppointmentsPage }),
      },
      { path: 'clientes', lazy: async () => ({ Component: (await import('../features/customers/CustomersPage')).CustomersPage }) },
      {
        path: 'profissionais',
        lazy: async () => ({ Component: (await import('../features/professionals/ProfessionalsPage')).ProfessionalsPage }),
      },
      { path: 'servicos', lazy: async () => ({ Component: (await import('../features/services/ServicesPage')).ServicesPage }) },
      {
        path: 'expediente',
        lazy: async () => ({ Component: (await import('../features/business-hours/BusinessHoursPage')).BusinessHoursPage }),
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
])

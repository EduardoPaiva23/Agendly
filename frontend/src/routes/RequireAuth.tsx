import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { Loading } from '../components/ui'
import { useAuth } from '../lib/useAuth'

export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <Loading label="Verificando sessão…" />
  if (!isAuthenticated) return <Navigate to="/admin/login" replace state={{ from: location.pathname + location.search }} />
  return children
}

/** Hides admin-only screens from Staff. The API enforces the same rule (403). */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuth()
  if (!isAdmin)
    return (
      <div className="rounded-xl bg-amber-50 p-6 text-sm text-amber-900 ring-1 ring-amber-200">
        Esta área é restrita a administradores.
      </div>
    )
  return children
}

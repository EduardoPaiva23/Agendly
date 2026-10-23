import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { CurrentUser, LoginResponse } from '../types/api'
import { api, getToken, onUnauthorized, setToken } from './api'
import { AuthContext, type AuthState } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [hasToken, setHasToken] = useState(() => getToken() !== null)

  const me = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => api.get<CurrentUser>('/api/auth/me'),
    enabled: hasToken,
    staleTime: 5 * 60_000,
    retry: false,
  })

  const logout = useCallback(() => {
    setToken(null)
    setHasToken(false)
    queryClient.clear()
  }, [queryClient])

  useEffect(() => {
    onUnauthorized(logout)
    return () => onUnauthorized(null)
  }, [logout])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await api.post<LoginResponse>('/api/auth/login', { email, password })
      setToken(response.accessToken)
      queryClient.setQueryData(['auth', 'me'], response.user)
      setHasToken(true)
      return response.user
    },
    [queryClient],
  )

  const value = useMemo<AuthState>(() => {
    const user = hasToken ? (me.data ?? null) : null
    return {
      user,
      isLoading: hasToken && me.isPending,
      isAuthenticated: user !== null,
      isAdmin: user?.roles.includes('Admin') ?? false,
      login,
      logout,
    }
  }, [hasToken, me.data, me.isPending, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

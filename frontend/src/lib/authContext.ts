import { createContext } from 'react'
import type { CurrentUser } from '../types/api'

export interface AuthState {
  user: CurrentUser | null
  isLoading: boolean
  isAuthenticated: boolean
  isAdmin: boolean
  login: (email: string, password: string) => Promise<CurrentUser>
  logout: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

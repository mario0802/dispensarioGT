import { createContext, useContext } from 'react'

import type {
  AuthStatus,
  CurrentUserDto,
  LoginRequest,
} from '@/features/auth/auth-types'

export interface AuthContextValue {
  status: AuthStatus
  // Solo con status 'authenticated'
  user: CurrentUserDto | null
  // Rechaza con ApiError; pensado para usarse con useApi
  login: (request: LoginRequest) => Promise<CurrentUserDto>
  logout: () => void
  // Vuelve a pedir /me tras un error de arranque
  retry: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

// Punto de entrada de la sesión para todas las features
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider.')
  }
  return context
}

import type { ReactNode } from 'react'
import { vi } from 'vitest'

import type { CurrentUserDto } from '@/features/auth/auth-types'
import { AuthContext, type AuthContextValue } from '@/features/auth/use-auth'

// Sesión simulada para los tests que solo necesitan el usuario de useAuth
export function TestAuthProvider({
  user,
  children,
}: {
  user: CurrentUserDto
  children: ReactNode
}) {
  const value: AuthContextValue = {
    status: 'authenticated',
    user,
    login: vi.fn(),
    logout: vi.fn(),
    retry: vi.fn(),
    refreshUser: vi.fn(),
  }
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

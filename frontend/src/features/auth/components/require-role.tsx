import type { ReactNode } from 'react'
import { Navigate } from 'react-router'

import type { UserRole } from '@/features/auth/auth-types'
import { useAuth } from '@/features/auth/use-auth'

interface RequireRoleProps {
  allowedRoles: UserRole[]
  children: ReactNode
}

// Guard por rol: va dentro de RequireAuth; un rol no permitido vuelve al inicio
export function RequireRole({ allowedRoles, children }: RequireRoleProps) {
  const { user } = useAuth()

  // Dentro de RequireAuth siempre hay usuario
  if (!user) {
    return null
  }
  if (!allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />
  }
  return children
}

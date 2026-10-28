import type { UserRole } from '@/features/auth/auth-types'

// Nombres de los roles para la interfaz; la API los envía en inglés
export const roleLabels: Record<UserRole, string> = {
  Admin: 'Administrador',
  Doctor: 'Doctor',
  Receptionist: 'Recepcionista',
}

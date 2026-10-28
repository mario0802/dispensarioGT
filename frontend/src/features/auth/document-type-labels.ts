import type { DocumentType } from '@/features/auth/auth-types'

// Nombres de los tipos de documento para la interfaz; la API los envía en inglés
export const documentTypeLabels: Record<DocumentType, string> = {
  NationalId: 'Cédula',
  Passport: 'Pasaporte',
}

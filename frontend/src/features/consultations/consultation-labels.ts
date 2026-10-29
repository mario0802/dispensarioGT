import {
  formatLocalDate,
  formatLocalTime,
  statusBadgeClasses,
} from '@/features/appointments/appointment-labels'
import type {
  ConsultationDto,
  ConsultationStatus,
} from '@/features/consultations/consultation-types'

// Nombres de los estados para la interfaz; la API los envía en inglés
export const consultationStatusLabels: Record<ConsultationStatus, string> = {
  Open: 'Abierta',
  Closed: 'Cerrada',
}

// Mismas clases que los estados de cita equivalentes (Open ≈ Confirmed, Closed ≈ Completed)
export const consultationStatusBadgeClasses: Record<
  ConsultationStatus,
  string
> = {
  Open: statusBadgeClasses.Confirmed,
  Closed: statusBadgeClasses.Completed,
}

// Editable: abierta y del doctor del usuario
export function isConsultationEditable(
  consultation: ConsultationDto,
  doctorId: string | null,
): boolean {
  return (
    consultation.status === 'Open' &&
    doctorId !== null &&
    consultation.doctorId === doctorId
  )
}

// "2026-11-03T09:32:10" → "03/11/2026 09:32", sin pasar por new Date() para no desplazar la hora local
export function formatLocalDateTime(local: string): string {
  return `${formatLocalDate(local)} ${formatLocalTime(local)}`
}

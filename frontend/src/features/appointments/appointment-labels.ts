import type { AppointmentStatus } from '@/features/appointments/appointment-types'
import { toLocalIsoDate } from '@/features/patients/patient-labels'

// Nombres de los estados para la interfaz; la API los envía en inglés
export const statusLabels: Record<AppointmentStatus, string> = {
  Scheduled: 'Agendada',
  Confirmed: 'Confirmada',
  Completed: 'Completada',
  Cancelled: 'Cancelada',
  NoShow: 'No asistió',
}

export const appointmentStatuses = Object.keys(
  statusLabels,
) as AppointmentStatus[]

// Clases del Badge (variante outline) por estado, con su par para el tema oscuro
export const statusBadgeClasses: Record<AppointmentStatus, string> = {
  Scheduled: '',
  Confirmed:
    'border-transparent bg-blue-100 text-blue-800 dark:bg-blue-900/60 dark:text-blue-100',
  Completed:
    'border-transparent bg-green-100 text-green-800 dark:bg-green-900/60 dark:text-green-100',
  Cancelled:
    'border-transparent bg-destructive text-white dark:bg-destructive/60',
  NoShow: 'border-transparent bg-secondary text-secondary-foreground',
}

// Las horas locales llegan como "yyyy-MM-ddTHH:mm[:ss]": se parte la cadena, sin new Date(),
// para no desplazarlas a la zona del navegador

// "2026-11-03T09:30:00" → "03/11/2026"
export function formatLocalDate(local: string): string {
  const [year, month, day] = local.slice(0, 10).split('-')
  return `${day}/${month}/${year}`
}

// "2026-11-03T09:30:00" → "09:30"
export function formatLocalTime(local: string): string {
  return local.slice(11, 16)
}

// "09:30 – 10:00"
export function formatLocalTimeRange(
  startLocal: string,
  endLocal: string,
): string {
  return `${formatLocalTime(startLocal)} – ${formatLocalTime(endLocal)}`
}

// "2026-11-03T09:30:00" → "2026-11-03T09:30", la forma que documenta la API para el alta
export function toStartLocalRequest(local: string): string {
  return local.slice(0, 16)
}

// Fecha local del navegador como "yyyy-MM-dd" ("hoy" y el min del input date)
export function toLocalDateString(date: Date): string {
  return toLocalIsoDate(date)
}

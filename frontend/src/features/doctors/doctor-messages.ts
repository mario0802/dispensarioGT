import type { DoctorDto } from '@/features/doctors/doctor-types'
import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

export const doctorNotFoundMessage = 'El doctor ya no existe.'
export const duplicateLicenseMessage = 'Ya existe un doctor con esa matrícula.'

// Errores comunes a todas las acciones (403, 404, red y el resto)
export function getDoctorActionErrorMessage(error: ApiError): string {
  if (error.status === 403) {
    return 'No tienes permiso para realizar esta acción.'
  }
  if (error.status === 404) {
    return doctorNotFoundMessage
  }
  if (error.kind === 'network') {
    return 'No se pudo conectar con el servidor.'
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

// Errores del formulario; el 409 se muestra en el campo matrícula
export function getDoctorFormErrorMessage(
  error: ApiError,
  mode: 'create' | 'edit',
): string {
  if (error.status === 400) {
    // La duración frente a los bloques del horario solo la puede comprobar el backend
    return mode === 'edit'
      ? 'Revisa los datos. La duración de la cita no puede ser mayor que un bloque del horario del doctor.'
      : 'Revisa los datos e inténtalo de nuevo.'
  }
  return getDoctorActionErrorMessage(error)
}

// Detalle del aviso cuando falla la carga del listado
export function getDoctorsLoadErrorMessage(error: ApiError): string {
  return error.kind === 'network'
    ? 'No se pudo conectar con el servidor.'
    : 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

export function getDoctorFullName(
  doctor: Pick<DoctorDto, 'firstName' | 'lastName'>,
): string {
  return `${doctor.firstName} ${doctor.lastName}`
}

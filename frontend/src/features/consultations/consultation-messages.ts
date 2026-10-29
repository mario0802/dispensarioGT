import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

// El 409 (la cita ya tiene consulta) no pasa por aquí: dispara la búsqueda de la existente
export function getStartConsultationErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return 'No se puede iniciar la consulta: la cita ya no está pendiente, no es de hoy o el paciente está inactivo.'
  }
  if (error.status === 403) {
    return 'No tienes permiso para iniciar esta consulta.'
  }
  if (error.status === 404) {
    return 'La cita o el paciente ya no existe.'
  }
  if (error.kind === 'network') {
    return 'No se pudo conectar con el servidor.'
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

export const existingConsultationNotFoundMessage =
  'La cita ya tiene una consulta, pero no se pudo abrir.'

import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

export const patientNotFoundMessage = 'El paciente ya no existe.'
export const duplicateDocumentMessage =
  'Ya existe un paciente con ese documento.'

// Errores comunes a todas las acciones (403, 404, red y el resto)
export function getPatientActionErrorMessage(error: ApiError): string {
  if (error.status === 403) {
    return 'No tienes permiso para realizar esta acción.'
  }
  if (error.status === 404) {
    return patientNotFoundMessage
  }
  if (error.kind === 'network') {
    return 'No se pudo conectar con el servidor.'
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

// Errores del formulario; el 409 se muestra en el campo número de documento
export function getPatientFormErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return 'Revisa los datos e inténtalo de nuevo.'
  }
  return getPatientActionErrorMessage(error)
}

// Detalle del aviso cuando falla la carga del listado
export function getPatientsLoadErrorMessage(error: ApiError): string {
  return error.kind === 'network'
    ? 'No se pudo conectar con el servidor.'
    : 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

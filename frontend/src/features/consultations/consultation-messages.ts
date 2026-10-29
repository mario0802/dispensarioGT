import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

const networkMessage = 'No se pudo conectar con el servidor.'
const unexpectedMessage = 'Ocurrió un error inesperado. Inténtalo de nuevo.'
const invalidFieldsMessage =
  'Revisa los campos: hay textos vacíos o demasiado largos.'
const consultationGoneMessage = 'La consulta ya no existe.'

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
    return networkMessage
  }
  return unexpectedMessage
}

export const existingConsultationNotFoundMessage =
  'La cita ya tiene una consulta, pero no se pudo abrir.'

// Errores al cargar la consulta (GET /api/consultations/{id})
export function getLoadConsultationErrorMessage(error: ApiError): string {
  if (error.status === 404) {
    return 'La consulta no existe.'
  }
  if (error.status === 403) {
    return 'No se pudo ver la consulta. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar.'
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// Tras un 404 o un 403 de carga no se ofrece Reintentar
export function canRetryLoadConsultation(error: ApiError): boolean {
  return error.status !== 404 && error.status !== 403
}

// Errores al guardar (PUT /api/consultations/{id})
export function getSaveConsultationErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return error.validationErrors.length > 0
      ? invalidFieldsMessage
      : 'La consulta ya está cerrada y no se puede editar.'
  }
  if (error.status === 403) {
    return 'No tienes permiso para editar esta consulta.'
  }
  if (error.status === 404) {
    return consultationGoneMessage
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// Errores al finalizar (el PUT previo o POST /api/consultations/{id}/close)
export function getCloseConsultationErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return error.validationErrors.length > 0
      ? invalidFieldsMessage
      : 'La consulta ya está cerrada.'
  }
  if (error.status === 403) {
    return 'No tienes permiso para finalizar esta consulta.'
  }
  if (error.status === 404) {
    return consultationGoneMessage
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// Tras un 400 de consulta cerrada, un 403 o un 404 al escribir se vuelve a pedir la consulta
export function shouldReloadConsultation(error: ApiError): boolean {
  return (
    (error.status === 400 && error.validationErrors.length === 0) ||
    error.status === 403 ||
    error.status === 404
  )
}

export const loadPatientErrorMessage =
  'No se pudieron cargar los datos del paciente.'

export const loadHistoryErrorMessage = 'No se pudo cargar la historia clínica.'

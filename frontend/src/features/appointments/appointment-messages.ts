import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

const forbiddenMessage = 'No tienes permiso para realizar esta acción.'
const networkMessage = 'No se pudo conectar con el servidor.'
const unexpectedMessage = 'Ocurrió un error inesperado. Inténtalo de nuevo.'

// Errores del alta de la cita (POST /api/appointments)
export function getCreateAppointmentErrorMessage(error: ApiError): string {
  if (error.status === 409) {
    return 'Ese horario ya no está disponible: el doctor o el paciente ya tienen una cita que se cruza. Elige otro.'
  }
  if (error.status === 400) {
    return 'No se pudo agendar: revisa que el paciente y el doctor sigan activos y que el horario siga libre.'
  }
  if (error.status === 404) {
    return 'El paciente o el doctor ya no existe.'
  }
  if (error.status === 403) {
    return forbiddenMessage
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// Tras estos errores del alta se vuelven a pedir los slots: el elegido pudo ocuparse o pasar
export function shouldReloadSlots(error: ApiError): boolean {
  return error.status === 409 || error.status === 400
}

// Errores de GET /api/doctors/{id}/available-slots
export function getAvailableSlotsErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return 'No se pueden consultar los horarios: el doctor está inactivo o la fecha ya pasó.'
  }
  if (error.status === 404) {
    return 'El doctor ya no existe.'
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// Detalle del aviso cuando falla la carga del listado. Al doctor, un 403 suele venir de un
// token emitido antes de vincularlo a su ficha (sin el claim doctor_id)
export function getAppointmentsLoadErrorMessage(
  error: ApiError,
  isDoctor = false,
): string {
  if (error.kind === 'network') {
    return networkMessage
  }
  if (error.status === 403) {
    return isDoctor
      ? 'No se pudieron ver tus citas. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar.'
      : forbiddenMessage
  }
  return unexpectedMessage
}

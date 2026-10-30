import type { ApiError } from '@/lib/api/api-error'

// Mensajes propios: los title/detail de la API vienen en inglés

const networkMessage = 'No se pudo conectar con el servidor.'
const unexpectedMessage = 'Ocurrió un error inesperado. Inténtalo de nuevo.'
const userGoneMessage = 'Tu usuario ya no existe.'

export const incorrectCurrentPasswordMessage =
  'La contraseña actual no es correcta.'

// Errores de PUT /api/users/me (el 401 lo resuelve el manejador global)
export function getUpdateProfileErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return 'Revisa los datos e inténtalo de nuevo.'
  }
  if (error.status === 404) {
    return userGoneMessage
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

// La API responde 400 sin ValidationErrors solo con la contraseña actual incorrecta
export function isIncorrectCurrentPasswordError(error: ApiError): boolean {
  return error.status === 400 && error.validationErrors.length === 0
}

// Errores de PUT /api/users/me/password, salvo la contraseña actual incorrecta (va a su campo)
export function getChangePasswordErrorMessage(error: ApiError): string {
  if (error.status === 400) {
    return 'La nueva contraseña no cumple las reglas o es igual a la actual.'
  }
  if (error.status === 404) {
    return userGoneMessage
  }
  if (error.kind === 'network') {
    return networkMessage
  }
  return unexpectedMessage
}

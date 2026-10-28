// Manejador global del 401: lo registra la sesión para cerrarse cuando la API rechaza el token.
// Solo se avisa si la petición llevaba token; el 401 del login (sin token) no cuenta
export type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

export function setUnauthorizedHandler(
  handler: UnauthorizedHandler | null,
): void {
  unauthorizedHandler = handler
}

// No hace nada si no hay manejador registrado
export function notifyUnauthorized(): void {
  unauthorizedHandler?.()
}

// Hueco para el token de acceso: lo registra el login y lo quita el logout.
// El cliente HTTP no sabe dónde se guarda el token, solo lo pide en cada petición
export type AccessTokenProvider = () => string | null

let accessTokenProvider: AccessTokenProvider | null = null

export function setAccessTokenProvider(
  provider: AccessTokenProvider | null,
): void {
  accessTokenProvider = provider
}

// null si no hay proveedor o si devuelve un token vacío
export function getAccessToken(): string | null {
  return accessTokenProvider?.() || null
}

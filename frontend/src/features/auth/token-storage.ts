// Token de la sesión en localStorage, para que sobreviva a recargas y pestañas nuevas.
// Todo acceso va en try/catch: si el almacenamiento falla, la sesión sigue en memoria
export const TOKEN_STORAGE_KEY = 'dispensario-auth'

export interface StoredToken {
  accessToken: string
  expiresAtUtc: string
}

// null si no hay token o si no es válido o ya caducó; en esos dos casos también borra la clave
export function readStoredToken(now: Date = new Date()): StoredToken | null {
  let raw: string | null
  try {
    raw = localStorage.getItem(TOKEN_STORAGE_KEY)
  } catch {
    return null
  }
  if (raw === null) {
    return null
  }

  const token = parseToken(raw)
  if (token === null || Date.parse(token.expiresAtUtc) <= now.getTime()) {
    clearStoredToken()
    return null
  }
  return token
}

export function saveStoredToken(token: StoredToken): void {
  try {
    localStorage.setItem(
      TOKEN_STORAGE_KEY,
      JSON.stringify({
        accessToken: token.accessToken,
        expiresAtUtc: token.expiresAtUtc,
      }),
    )
  } catch {
    // Sin almacenamiento, la sesión solo dura hasta recargar
  }
}

export function clearStoredToken(): void {
  try {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
  } catch {
    // Nada que borrar si el almacenamiento no está disponible
  }
}

function parseToken(raw: string): StoredToken | null {
  let value: unknown
  try {
    value = JSON.parse(raw)
  } catch {
    return null
  }
  if (typeof value !== 'object' || value === null) {
    return null
  }

  const { accessToken, expiresAtUtc } = value as Record<string, unknown>
  if (
    typeof accessToken !== 'string' ||
    accessToken === '' ||
    typeof expiresAtUtc !== 'string' ||
    Number.isNaN(Date.parse(expiresAtUtc))
  ) {
    return null
  }
  return { accessToken, expiresAtUtc }
}

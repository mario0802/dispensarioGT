import { getAccessToken } from '@/lib/api/access-token'
import { ApiError, createHttpError } from '@/lib/api/api-error'
import { config } from '@/lib/config'

export type QueryValue = string | number | boolean | null | undefined

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  // Se serializa con JSON.stringify
  body?: unknown
  query?: Record<string, QueryValue>
}

// Llama a la API y resuelve el cuerpo de la respuesta; cualquier fallo se lanza como ApiError.
// El tipo T no se valida en tiempo de ejecución: lo garantiza el contrato del README.md del backend
export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<T> {
  const { method = 'GET', body, query } = options

  const headers = new Headers({ Accept: 'application/json' })
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  let response: Response
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    // fetch solo rechaza sin respuesta: sin conexión, CORS o DNS
    throw new ApiError({ kind: 'network', status: 0, title: 'Network error.' })
  }

  if (!response.ok) {
    throw await createHttpError(response)
  }

  return (await readBody(response)) as T
}

export const api = {
  get: <T>(path: string, query?: Record<string, QueryValue>) =>
    apiRequest<T>(path, { query }),
  post: <T = void>(path: string, body?: unknown) =>
    apiRequest<T>(path, { method: 'POST', body }),
  put: <T = void>(path: string, body?: unknown) =>
    apiRequest<T>(path, { method: 'PUT', body }),
  delete: <T = void>(path: string) => apiRequest<T>(path, { method: 'DELETE' }),
}

function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== null && value !== undefined) {
      params.append(key, String(value))
    }
  }
  const search = params.toString()
  return `${config.apiBaseUrl}${path}${search ? `?${search}` : ''}`
}

async function readBody(response: Response): Promise<unknown> {
  const text = response.status === 204 ? '' : await response.text()
  if (text === '') {
    return undefined
  }
  if (!response.headers.get('Content-Type')?.includes('json')) {
    return text
  }
  try {
    return JSON.parse(text)
  } catch {
    throw new ApiError({
      kind: 'unknown',
      status: response.status,
      title: 'Invalid JSON response.',
    })
  }
}

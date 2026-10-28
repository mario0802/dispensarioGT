import type { CurrentUserDto, LoginResponse } from '@/features/auth/auth-types'
import {
  saveStoredToken,
  type StoredToken,
} from '@/features/auth/token-storage'

// Respuestas simuladas de la API para los tests que sustituyen fetch

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

export function problemResponse(status: number, title = 'Error'): Response {
  return new Response(JSON.stringify({ title, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

// Un día después de ahora: el token sigue vigente durante el test
export function futureUtc(): string {
  return new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString()
}

export const testUser: CurrentUserDto = {
  id: '0b6f0000-0000-0000-0000-000000000001',
  email: 'ana.torres@dispensario.local',
  firstName: 'Ana',
  lastName: 'Torres',
  identificationType: 'NationalId',
  identificationNumber: '1712345678',
  phone: null,
  role: 'Receptionist',
  doctorId: null,
  isActive: true,
}

export function loginResponse(accessToken = 'new-token'): LoginResponse {
  return { accessToken, tokenType: 'Bearer', expiresAtUtc: futureUtc() }
}

export function storeValidToken(accessToken = 'stored-token'): StoredToken {
  const token = { accessToken, expiresAtUtc: futureUtc() }
  saveStoredToken(token)
  return token
}

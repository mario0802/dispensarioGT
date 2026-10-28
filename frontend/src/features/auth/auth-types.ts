export type UserRole = 'Admin' | 'Doctor' | 'Receptionist'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: 'Bearer'
  // ISO 8601 en UTC
  expiresAtUtc: string
}

// GET /api/auth/me
export interface CurrentUserDto {
  id: string
  email: string
  firstName: string
  lastName: string
  identificationType: string
  identificationNumber: string
  phone: string | null
  role: UserRole
  doctorId: string | null
  isActive: boolean
}

// loading y error solo ocurren al arrancar con un token guardado (mientras responde /me)
export type AuthStatus = 'loading' | 'error' | 'authenticated' | 'anonymous'

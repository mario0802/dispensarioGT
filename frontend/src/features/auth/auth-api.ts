import type {
  CurrentUserDto,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
} from '@/features/auth/auth-types'
import { api } from '@/lib/api/api-client'

export const login = (request: LoginRequest) =>
  api.post<LoginResponse>('/api/auth/login', request)

export const getCurrentUser = () => api.get<CurrentUserDto>('/api/auth/me')

// No se llama register para no chocar con el register de React Hook Form
export const registerAccount = (request: RegisterRequest) =>
  api.post<RegisterResponse>('/api/auth/register', request)

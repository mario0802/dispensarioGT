import type {
  CurrentUserDto,
  LoginRequest,
  LoginResponse,
} from '@/features/auth/auth-types'
import { api } from '@/lib/api/api-client'

export const login = (request: LoginRequest) =>
  api.post<LoginResponse>('/api/auth/login', request)

export const getCurrentUser = () => api.get<CurrentUserDto>('/api/auth/me')

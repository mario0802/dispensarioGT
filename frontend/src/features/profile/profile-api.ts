import type {
  ChangePasswordRequest,
  UpdateProfileRequest,
} from '@/features/profile/profile-types'
import { api } from '@/lib/api/api-client'

export const updateMyProfile = (request: UpdateProfileRequest) =>
  api.put<void>('/api/users/me', request)

export const changeMyPassword = (request: ChangePasswordRequest) =>
  api.put<void>('/api/users/me/password', request)

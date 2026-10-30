// Cuerpo de PUT /api/users/me: phone null borra el teléfono
export interface UpdateProfileRequest {
  firstName: string
  lastName: string
  phone: string | null
}

// Cuerpo de PUT /api/users/me/password
export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

// Respuesta de GET /api/doctors/{id} y cada elemento del listado
export interface DoctorDto {
  id: string
  firstName: string
  lastName: string
  licenseNumber: string
  specialty: string
  email: string
  phone: string | null
  appointmentDurationMinutes: number
  isActive: boolean
  userId: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/doctors y PUT /api/doctors/{id}
export interface DoctorRequest {
  firstName: string
  lastName: string
  licenseNumber: string
  specialty: string
  email: string
  phone: string | null
  appointmentDurationMinutes: number
}

export interface CreateDoctorResponse {
  id: string
}

// Query de GET /api/doctors (los null/undefined los omite el cliente)
export interface DoctorListQuery {
  pageIndex: number
  pageSize: number
  search?: string
  isActive?: boolean
}

// Vive aquí hasta que otra feature lo use; entonces pasa a src/lib/api/
export interface PaginatedResult<T> {
  pageIndex: number
  pageSize: number
  count: number
  data: T[]
}

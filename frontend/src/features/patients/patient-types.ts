import type { DocumentType } from '@/features/auth/auth-types'

export type Gender = 'Male' | 'Female' | 'Other'

// Notación clínica, tal como la envía y la espera la API
export type BloodType = 'A+' | 'A-' | 'B+' | 'B-' | 'AB+' | 'AB-' | 'O+' | 'O-'

export interface AddressDto {
  street: string
  city: string
  state: string | null
  reference: string | null
}

// Respuesta de GET /api/patients/{id} y cada elemento del listado
export interface PatientDto {
  id: string
  documentType: DocumentType
  documentNumber: string
  firstName: string
  lastName: string
  // "yyyy-MM-dd"
  birthDate: string
  gender: Gender
  email: string | null
  phone: string | null
  address: AddressDto | null
  bloodType: BloodType | null
  allergies: string | null
  isActive: boolean
  createdAtUtc: string
}

// Cuerpo de POST /api/patients y PUT /api/patients/{id} (el PUT reemplaza todo: null borra)
export interface PatientRequest {
  documentType: DocumentType
  documentNumber: string
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
  email: string | null
  phone: string | null
  address: AddressDto | null
  bloodType: BloodType | null
  allergies: string | null
}

export interface CreatePatientResponse {
  id: string
}

// Query de GET /api/patients (los null/undefined los omite el cliente)
export interface PatientListQuery {
  pageIndex: number
  pageSize: number
  search?: string
  isActive?: boolean
}

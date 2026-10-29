import type {
  CreatePatientResponse,
  PatientDto,
  PatientListQuery,
  PatientRequest,
} from '@/features/patients/patient-types'
import { api } from '@/lib/api/api-client'
import type { PaginatedResult } from '@/lib/api/paginated-result'

// Se copia la query a un objeto literal: una interfaz no encaja en Record<string, QueryValue>
export const getPatients = (query: PatientListQuery) =>
  api.get<PaginatedResult<PatientDto>>('/api/patients', { ...query })

export const getPatient = (id: string) =>
  api.get<PatientDto>(`/api/patients/${id}`)

export const createPatient = (request: PatientRequest) =>
  api.post<CreatePatientResponse>('/api/patients', request)

export const updatePatient = (id: string, request: PatientRequest) =>
  api.put(`/api/patients/${id}`, request)

export const activatePatient = (id: string) =>
  api.post(`/api/patients/${id}/activate`)

export const deactivatePatient = (id: string) =>
  api.post(`/api/patients/${id}/deactivate`)

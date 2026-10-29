import type {
  CloseConsultationRequest,
  ConsultationDto,
  PatientConsultationsQuery,
  StartConsultationRequest,
  StartConsultationResponse,
  UpdateConsultationRequest,
} from '@/features/consultations/consultation-types'
import { api } from '@/lib/api/api-client'
import type { PaginatedResult } from '@/lib/api/paginated-result'

export const startConsultation = (request: StartConsultationRequest) =>
  api.post<StartConsultationResponse>('/api/consultations', request)

export const getConsultation = (id: string) =>
  api.get<ConsultationDto>(`/api/consultations/${id}`)

export const updateConsultation = (
  id: string,
  request: UpdateConsultationRequest,
) => api.put<void>(`/api/consultations/${id}`, request)

export const closeConsultation = (
  id: string,
  request: CloseConsultationRequest,
) => api.post<void>(`/api/consultations/${id}/close`, request)

// La ruta cuelga de /api/patients pero pertenece al módulo de consultas
export const getPatientConsultations = (
  patientId: string,
  query: PatientConsultationsQuery,
) =>
  api.get<PaginatedResult<ConsultationDto>>(
    `/api/patients/${patientId}/consultations`,
    { ...query },
  )

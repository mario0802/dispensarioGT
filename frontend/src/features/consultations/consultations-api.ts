import type {
  ConsultationDto,
  PatientConsultationsQuery,
  StartConsultationRequest,
  StartConsultationResponse,
} from '@/features/consultations/consultation-types'
import { api } from '@/lib/api/api-client'
import type { PaginatedResult } from '@/lib/api/paginated-result'

export const startConsultation = (request: StartConsultationRequest) =>
  api.post<StartConsultationResponse>('/api/consultations', request)

// La ruta cuelga de /api/patients pero pertenece al módulo de consultas
export const getPatientConsultations = (
  patientId: string,
  query: PatientConsultationsQuery,
) =>
  api.get<PaginatedResult<ConsultationDto>>(
    `/api/patients/${patientId}/consultations`,
    { ...query },
  )

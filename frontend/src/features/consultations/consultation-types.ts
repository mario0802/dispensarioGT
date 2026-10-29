export type ConsultationStatus = 'Open' | 'Closed'

// Respuesta de GET /api/consultations/{id} y cada elemento de la historia del paciente
export interface ConsultationDto {
  id: string
  patientId: string
  patientFullName: string
  doctorId: string
  doctorFullName: string
  appointmentId: string | null
  reason: string
  diagnosis: string | null
  notes: string | null
  status: ConsultationStatus
  startedAtUtc: string
  closedAtUtc: string | null
  // "yyyy-MM-ddTHH:mm:ss", hora local del dispensario sin offset
  startedAtLocal: string
  closedAtLocal: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/consultations (el doctor sale del token)
export interface StartConsultationRequest {
  patientId: string
  appointmentId: string
  reason: string
}

export interface StartConsultationResponse {
  id: string
}

// Query de GET /api/patients/{id}/consultations
export interface PatientConsultationsQuery {
  pageIndex: number
  pageSize: number
}

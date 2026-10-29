export type AppointmentStatus =
  'Scheduled' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'

// Respuesta de GET /api/appointments/{id} y cada elemento del listado
export interface AppointmentDto {
  id: string
  patientId: string
  patientFullName: string
  doctorId: string
  doctorFullName: string
  startUtc: string
  endUtc: string
  // "yyyy-MM-ddTHH:mm:ss", hora local del dispensario sin offset
  startLocal: string
  endLocal: string
  reason: string
  status: AppointmentStatus
  cancellationReason: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/appointments
export interface CreateAppointmentRequest {
  patientId: string
  doctorId: string
  // "yyyy-MM-ddTHH:mm", sin offset
  startLocal: string
  reason: string
}

export interface CreateAppointmentResponse {
  id: string
}

// Query de GET /api/appointments (los null/undefined los omite el cliente)
export interface AppointmentListQuery {
  pageIndex: number
  pageSize: number
  doctorId?: string
  patientId?: string
  status?: AppointmentStatus
  // "yyyy-MM-dd", fechas locales del dispensario, ambas inclusive
  from?: string
  to?: string
}

export interface AvailableSlotDto {
  // "yyyy-MM-ddTHH:mm:ss"
  startLocal: string
  endLocal: string
}

// Respuesta de GET /api/doctors/{id}/available-slots?date=
export interface AvailableSlotsDto {
  doctorId: string
  // "yyyy-MM-dd"
  date: string
  durationMinutes: number
  slots: AvailableSlotDto[]
}

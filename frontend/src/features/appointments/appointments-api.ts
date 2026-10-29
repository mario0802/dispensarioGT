import type {
  AppointmentDto,
  AppointmentListQuery,
  AvailableSlotsDto,
  CreateAppointmentRequest,
  CreateAppointmentResponse,
} from '@/features/appointments/appointment-types'
import { api } from '@/lib/api/api-client'
import type { PaginatedResult } from '@/lib/api/paginated-result'

// Se copia la query a un objeto literal: una interfaz no encaja en Record<string, QueryValue>
export const getAppointments = (query: AppointmentListQuery) =>
  api.get<PaginatedResult<AppointmentDto>>('/api/appointments', { ...query })

export const createAppointment = (request: CreateAppointmentRequest) =>
  api.post<CreateAppointmentResponse>('/api/appointments', request)

// La ruta cuelga de /api/doctors pero pertenece al módulo de citas
export const getAvailableSlots = (doctorId: string, date: string) =>
  api.get<AvailableSlotsDto>(`/api/doctors/${doctorId}/available-slots`, {
    date,
  })

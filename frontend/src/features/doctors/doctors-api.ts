import type {
  DoctorScheduleDto,
  SetDoctorScheduleRequest,
} from '@/features/doctors/doctor-schedule-types'
import type {
  CreateDoctorResponse,
  DoctorDto,
  DoctorListQuery,
  DoctorRequest,
  PaginatedResult,
} from '@/features/doctors/doctor-types'
import { api } from '@/lib/api/api-client'

// Se copia la query a un objeto literal: una interfaz no encaja en Record<string, QueryValue>
export const getDoctors = (query: DoctorListQuery) =>
  api.get<PaginatedResult<DoctorDto>>('/api/doctors', { ...query })

export const getDoctor = (id: string) =>
  api.get<DoctorDto>(`/api/doctors/${id}`)

export const createDoctor = (request: DoctorRequest) =>
  api.post<CreateDoctorResponse>('/api/doctors', request)

export const updateDoctor = (id: string, request: DoctorRequest) =>
  api.put(`/api/doctors/${id}`, request)

export const activateDoctor = (id: string) =>
  api.post(`/api/doctors/${id}/activate`)

export const deactivateDoctor = (id: string) =>
  api.post(`/api/doctors/${id}/deactivate`)

export const getDoctorSchedule = (id: string) =>
  api.get<DoctorScheduleDto>(`/api/doctors/${id}/schedule`)

export const setDoctorSchedule = (
  id: string,
  request: SetDoctorScheduleRequest,
) => api.put(`/api/doctors/${id}/schedule`, request)

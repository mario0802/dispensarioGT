// Como el enum DayOfWeek de .NET, en texto
export type DayOfWeek =
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'
  | 'Sunday'

// Horas "HH:mm" en hora local del dispensario
export interface ScheduleBlockDto {
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

// Respuesta de GET /api/doctors/{id}/schedule
export interface DoctorScheduleDto {
  doctorId: string
  blocks: ScheduleBlockDto[]
}

// Cuerpo de PUT /api/doctors/{id}/schedule ([] borra el horario)
export interface SetDoctorScheduleRequest {
  blocks: ScheduleBlockDto[]
}

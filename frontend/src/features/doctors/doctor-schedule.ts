import { z } from 'zod'

import type {
  DayOfWeek,
  ScheduleBlockDto,
  SetDoctorScheduleRequest,
} from '@/features/doctors/doctor-schedule-types'

// Lunes a domingo, el orden en que la API devuelve los bloques
export const weekDays: { value: DayOfWeek; label: string }[] = [
  { value: 'Monday', label: 'Lunes' },
  { value: 'Tuesday', label: 'Martes' },
  { value: 'Wednesday', label: 'Miércoles' },
  { value: 'Thursday', label: 'Jueves' },
  { value: 'Friday', label: 'Viernes' },
  { value: 'Saturday', label: 'Sábado' },
  { value: 'Sunday', label: 'Domingo' },
]

const lastHour = 23

// "00:00" … "23:00": un bloque no cruza la medianoche y "24:00" no es una hora válida
export const hourOptions: string[] = Array.from(
  { length: lastHour + 1 },
  (_, hour) => formatTime(hour * 60),
)

// Bloque que crea el switch al encender un día
export const defaultBlock = { startTime: '08:00', endTime: '12:00' }

const firstBlockStartMinutes = 8 * 60

// "09:30" → 570
export function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

// 570 → "09:30"
function formatTime(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

export function isWholeHour(time: string): boolean {
  return toMinutes(time) % 60 === 0
}

export interface ScheduleBlockValues {
  startTime: string
  endTime: string
}

// Valores del formulario: siempre 7 días, en el orden de weekDays
export interface ScheduleDayValues {
  dayOfWeek: DayOfWeek
  enabled: boolean
  blocks: ScheduleBlockValues[]
}

export interface ScheduleFormValues {
  days: ScheduleDayValues[]
}

function byStartTime(a: ScheduleBlockValues, b: ScheduleBlockValues): number {
  return toMinutes(a.startTime) - toMinutes(b.startTime)
}

// Agrupa los bloques por día; se conservan las horas tal cual (también las que no son completas)
export function toScheduleFormValues(
  blocks: ScheduleBlockDto[],
): ScheduleFormValues {
  return {
    days: weekDays.map(({ value }) => {
      const dayBlocks = blocks
        .filter((block) => block.dayOfWeek === value)
        .map(({ startTime, endTime }) => ({ startTime, endTime }))
        .sort(byStartTime)
      return {
        dayOfWeek: value,
        enabled: dayBlocks.length > 0,
        blocks: dayBlocks,
      }
    }),
  }
}

// Solo los días encendidos, de lunes a domingo y por hora de inicio; sin días, [] borra el horario
export function toScheduleRequest(
  values: ScheduleFormValues,
): SetDoctorScheduleRequest {
  const order = weekDays.map(({ value }) => value)
  return {
    blocks: [...values.days]
      .filter((day) => day.enabled)
      .sort((a, b) => order.indexOf(a.dayOfWeek) - order.indexOf(b.dayOfWeek))
      .flatMap((day) =>
        [...day.blocks].sort(byStartTime).map((block) => ({
          dayOfWeek: day.dayOfWeek,
          startTime: block.startTime,
          endTime: block.endTime,
        })),
      ),
  }
}

// Primer error de un bloque, en el orden de las reglas, o null
function getBlockError(
  blocks: ScheduleBlockValues[],
  index: number,
  appointmentDurationMinutes: number,
): string | null {
  const block = blocks[index]
  if (!isWholeHour(block.startTime) || !isWholeHour(block.endTime)) {
    return 'Usa horas completas.'
  }
  const start = toMinutes(block.startTime)
  const end = toMinutes(block.endTime)
  if (end <= start) {
    return 'La hora de fin debe ser posterior a la de inicio.'
  }
  if (end - start < appointmentDurationMinutes) {
    return `El bloque debe durar al menos ${appointmentDurationMinutes} min (la duración de cada cita).`
  }
  // Dos bloques contiguos (uno termina cuando empieza el otro) no se solapan
  const overlaps = blocks.some(
    (other, otherIndex) =>
      otherIndex !== index &&
      start < toMinutes(other.endTime) &&
      toMinutes(other.startTime) < end,
  )
  return overlaps ? 'Se solapa con otro bloque del día.' : null
}

const scheduleBlockSchema = z.object({
  startTime: z.string(),
  endTime: z.string(),
})

const scheduleDaySchema = z.object({
  dayOfWeek: z.enum(
    weekDays.map(({ value }) => value) as [DayOfWeek, ...DayOfWeek[]],
  ),
  enabled: z.boolean(),
  blocks: z.array(scheduleBlockSchema),
})

// Mismas reglas que ScheduleBlock y Doctor.SetWeeklySchedule del backend, más las horas completas.
// El error va en el bloque (days.N.blocks.M) y solo se valida en los días encendidos
export function createScheduleSchema(appointmentDurationMinutes: number) {
  return z
    .object({ days: z.array(scheduleDaySchema) })
    .superRefine((values, ctx) => {
      values.days.forEach((day, dayIndex) => {
        if (!day.enabled) {
          return
        }
        day.blocks.forEach((_, blockIndex) => {
          const message = getBlockError(
            day.blocks,
            blockIndex,
            appointmentDurationMinutes,
          )
          if (message) {
            ctx.addIssue({
              code: 'custom',
              message,
              path: ['days', dayIndex, 'blocks', blockIndex],
            })
          }
        })
      })
    })
}

// Bloque que propone "+ Añadir bloque": empieza donde termina el último del día (en la hora
// completa siguiente) o a las 08:00, y dura las horas completas que pide la cita.
// null si el fin pasa de las 23:00
export function getNextBlock(
  blocks: ScheduleBlockValues[],
  appointmentDurationMinutes: number,
): ScheduleBlockValues | null {
  const lastEnd =
    blocks.length > 0
      ? Math.max(...blocks.map((block) => toMinutes(block.endTime)))
      : firstBlockStartMinutes
  const start = Math.ceil(lastEnd / 60) * 60
  const hours = Math.max(1, Math.ceil(appointmentDurationMinutes / 60))
  const end = start + hours * 60
  if (end > lastHour * 60) {
    return null
  }
  return { startTime: formatTime(start), endTime: formatTime(end) }
}

import { describe, expect, it } from 'vitest'

import {
  createScheduleSchema,
  getNextBlock,
  hourOptions,
  isWholeHour,
  toMinutes,
  toScheduleFormValues,
  toScheduleRequest,
  type ScheduleFormValues,
} from '@/features/doctors/doctor-schedule'
import type { DayOfWeek } from '@/features/doctors/doctor-schedule-types'

// Lunes encendido con los bloques dados; el resto apagados
function mondayWith(
  blocks: { startTime: string; endTime: string }[],
): ScheduleFormValues {
  const values = toScheduleFormValues([])
  values.days[0] = { dayOfWeek: 'Monday', enabled: true, blocks }
  return values
}

function issuesOf(values: ScheduleFormValues, durationMinutes = 30) {
  const result = createScheduleSchema(durationMinutes).safeParse(values)
  return result.success
    ? []
    : result.error.issues.map((issue) => ({
        path: issue.path.join('.'),
        message: issue.message,
      }))
}

describe('hourOptions', () => {
  it('va de 00:00 a 23:00 en horas completas', () => {
    expect(hourOptions).toHaveLength(24)
    expect(hourOptions[0]).toBe('00:00')
    expect(hourOptions[9]).toBe('09:00')
    expect(hourOptions.at(-1)).toBe('23:00')
  })
})

describe('toMinutes e isWholeHour', () => {
  it('convierte HH:mm a minutos', () => {
    expect(toMinutes('09:30')).toBe(570)
    expect(toMinutes('00:00')).toBe(0)
  })

  it('distingue las horas completas', () => {
    expect(isWholeHour('09:00')).toBe(true)
    expect(isWholeHour('09:30')).toBe(false)
  })
})

describe('toScheduleFormValues', () => {
  it('agrupa por día, ordena por inicio y apaga los días sin bloques', () => {
    const values = toScheduleFormValues([
      { dayOfWeek: 'Monday', startTime: '14:00', endTime: '18:00' },
      { dayOfWeek: 'Wednesday', startTime: '09:30', endTime: '12:00' },
      { dayOfWeek: 'Monday', startTime: '08:00', endTime: '12:00' },
    ])

    expect(values.days.map((day) => day.dayOfWeek)).toEqual<DayOfWeek[]>([
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
      'Sunday',
    ])
    expect(values.days[0]).toEqual({
      dayOfWeek: 'Monday',
      enabled: true,
      blocks: [
        { startTime: '08:00', endTime: '12:00' },
        { startTime: '14:00', endTime: '18:00' },
      ],
    })
    expect(values.days[1]).toEqual({
      dayOfWeek: 'Tuesday',
      enabled: false,
      blocks: [],
    })
    // Las horas no completas se conservan
    expect(values.days[2].blocks).toEqual([
      { startTime: '09:30', endTime: '12:00' },
    ])
  })
})

describe('toScheduleRequest', () => {
  it('ordena de lunes a domingo y por inicio, y omite los días apagados', () => {
    const values = toScheduleFormValues([])
    values.days[4] = {
      dayOfWeek: 'Friday',
      enabled: true,
      blocks: [{ startTime: '10:00', endTime: '12:00' }],
    }
    values.days[0] = {
      dayOfWeek: 'Monday',
      enabled: true,
      blocks: [
        { startTime: '14:00', endTime: '18:00' },
        { startTime: '08:00', endTime: '12:00' },
      ],
    }
    values.days[2] = {
      dayOfWeek: 'Wednesday',
      enabled: false,
      blocks: [{ startTime: '08:00', endTime: '12:00' }],
    }

    expect(toScheduleRequest(values)).toEqual({
      blocks: [
        { dayOfWeek: 'Monday', startTime: '08:00', endTime: '12:00' },
        { dayOfWeek: 'Monday', startTime: '14:00', endTime: '18:00' },
        { dayOfWeek: 'Friday', startTime: '10:00', endTime: '12:00' },
      ],
    })
  })

  it('sin días encendidos devuelve una lista vacía', () => {
    expect(toScheduleRequest(toScheduleFormValues([]))).toEqual({ blocks: [] })
  })
})

describe('createScheduleSchema', () => {
  it('acepta bloques válidos y contiguos', () => {
    expect(
      issuesOf(
        mondayWith([
          { startTime: '09:00', endTime: '12:00' },
          { startTime: '12:00', endTime: '14:00' },
        ]),
      ),
    ).toEqual([])
  })

  it('rechaza las horas no completas', () => {
    expect(
      issuesOf(mondayWith([{ startTime: '09:30', endTime: '12:00' }])),
    ).toEqual([{ path: 'days.0.blocks.0', message: 'Usa horas completas.' }])
  })

  it('rechaza un fin igual o anterior al inicio', () => {
    expect(
      issuesOf(mondayWith([{ startTime: '12:00', endTime: '12:00' }])),
    ).toEqual([
      {
        path: 'days.0.blocks.0',
        message: 'La hora de fin debe ser posterior a la de inicio.',
      },
    ])
  })

  it('rechaza un bloque más corto que la duración de la cita', () => {
    expect(
      issuesOf(mondayWith([{ startTime: '09:00', endTime: '10:00' }]), 90),
    ).toEqual([
      {
        path: 'days.0.blocks.0',
        message:
          'El bloque debe durar al menos 90 min (la duración de cada cita).',
      },
    ])
  })

  it('rechaza los bloques solapados del mismo día', () => {
    expect(
      issuesOf(
        mondayWith([
          { startTime: '08:00', endTime: '12:00' },
          { startTime: '11:00', endTime: '14:00' },
        ]),
      ),
    ).toEqual([
      {
        path: 'days.0.blocks.0',
        message: 'Se solapa con otro bloque del día.',
      },
      {
        path: 'days.0.blocks.1',
        message: 'Se solapa con otro bloque del día.',
      },
    ])
  })

  it('no valida los días apagados', () => {
    const values = toScheduleFormValues([])
    values.days[1] = {
      dayOfWeek: 'Tuesday',
      enabled: false,
      blocks: [{ startTime: '12:00', endTime: '09:00' }],
    }
    expect(issuesOf(values)).toEqual([])
  })
})

describe('getNextBlock', () => {
  it('sin bloques empieza a las 08:00', () => {
    expect(getNextBlock([], 30)).toEqual({
      startTime: '08:00',
      endTime: '09:00',
    })
  })

  it('empieza donde termina el último bloque y dura las horas que pide la cita', () => {
    expect(
      getNextBlock(
        [
          { startTime: '08:00', endTime: '12:00' },
          { startTime: '14:00', endTime: '18:00' },
        ],
        90,
      ),
    ).toEqual({ startTime: '18:00', endTime: '20:00' })
  })

  it('devuelve null si no cabe antes de las 23:00', () => {
    expect(getNextBlock([{ startTime: '14:00', endTime: '22:00' }], 120)).toBe(
      null,
    )
  })
})

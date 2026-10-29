import { describe, expect, it } from 'vitest'

import {
  appointmentStatuses,
  canStartConsultation,
  formatLocalDate,
  formatLocalTime,
  formatLocalTimeRange,
  statusLabels,
  toLocalDateString,
  toStartLocalRequest,
} from '@/features/appointments/appointment-labels'
import type { AppointmentDto } from '@/features/appointments/appointment-types'

describe('statusLabels', () => {
  it('translates the five statuses in order', () => {
    expect(appointmentStatuses.map((status) => statusLabels[status])).toEqual([
      'Agendada',
      'Confirmada',
      'Completada',
      'Cancelada',
      'No asistió',
    ])
  })
})

describe('local date and time formatting', () => {
  it('formats the local date as dd/MM/yyyy', () => {
    expect(formatLocalDate('2026-11-03T09:30:00')).toBe('03/11/2026')
  })

  it('formats the local time as HH:mm', () => {
    expect(formatLocalTime('2026-11-03T09:30:00')).toBe('09:30')
  })

  it('formats the time range', () => {
    expect(
      formatLocalTimeRange('2026-11-03T09:30:00', '2026-11-03T10:00:00'),
    ).toBe('09:30 – 10:00')
  })

  it('does not shift a time near midnight', () => {
    expect(formatLocalDate('2026-11-03T23:45:00')).toBe('03/11/2026')
    expect(formatLocalTime('2026-11-03T23:45:00')).toBe('23:45')
    expect(formatLocalDate('2026-11-04T00:15:00')).toBe('04/11/2026')
    expect(formatLocalTime('2026-11-04T00:15:00')).toBe('00:15')
  })

  it('trims the seconds for the request', () => {
    expect(toStartLocalRequest('2026-11-03T09:30:00')).toBe('2026-11-03T09:30')
  })
})

describe('toLocalDateString', () => {
  it('uses the local date with zero padding', () => {
    expect(toLocalDateString(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05')
    expect(toLocalDateString(new Date(2026, 10, 3, 0, 5))).toBe('2026-11-03')
  })
})

describe('canStartConsultation', () => {
  const today = '2026-11-03'
  const appointment: AppointmentDto = {
    id: '0193f1d0-0000-0000-0000-000000000001',
    patientId: '7b2e0000-0000-0000-0000-000000000001',
    patientFullName: 'Ana Pérez',
    doctorId: '3c1a0000-0000-0000-0000-000000000001',
    doctorFullName: 'Luis Andrade',
    startUtc: '2026-11-03T14:30:00Z',
    endUtc: '2026-11-03T15:00:00Z',
    startLocal: '2026-11-03T09:30:00',
    endLocal: '2026-11-03T10:00:00',
    reason: 'Control',
    status: 'Scheduled',
    cancellationReason: null,
    createdAtUtc: '2026-10-27T15:02:11Z',
  }

  it.each(['Scheduled', 'Confirmed'] as const)(
    'allows a %s appointment of today',
    (status) => {
      expect(canStartConsultation({ ...appointment, status }, today)).toBe(true)
    },
  )

  it.each(['Completed', 'Cancelled', 'NoShow'] as const)(
    'does not allow a %s appointment of today',
    (status) => {
      expect(canStartConsultation({ ...appointment, status }, today)).toBe(
        false,
      )
    },
  )

  it('does not allow an appointment of another day', () => {
    expect(canStartConsultation(appointment, '2026-11-04')).toBe(false)
  })
})

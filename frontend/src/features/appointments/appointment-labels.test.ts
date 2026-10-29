import { describe, expect, it } from 'vitest'

import {
  appointmentStatuses,
  formatLocalDate,
  formatLocalTime,
  formatLocalTimeRange,
  statusLabels,
  toLocalDateString,
  toStartLocalRequest,
} from '@/features/appointments/appointment-labels'

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

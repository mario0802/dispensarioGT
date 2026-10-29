import { describe, expect, it } from 'vitest'

import {
  formatAge,
  formatBirthDate,
  formatDocument,
  getAge,
  toLocalIsoDate,
} from '@/features/patients/patient-labels'

// Fechas locales: el mes de Date empieza en 0
const today = new Date(2026, 9, 28)

describe('getAge', () => {
  it('does not count the birthday before it arrives', () => {
    expect(getAge('1990-11-15', today)).toBe(35)
  })

  it('counts the birthday after it passed', () => {
    expect(getAge('1990-05-14', today)).toBe(36)
  })

  it('counts the birthday on the same day', () => {
    expect(getAge('1990-10-28', today)).toBe(36)
  })

  it('returns 0 for a baby under one year', () => {
    expect(getAge('2026-03-01', today)).toBe(0)
    expect(getAge('2025-12-01', today)).toBe(0)
  })
})

describe('formatAge', () => {
  it('uses the singular for one year', () => {
    expect(formatAge(1)).toBe('1 año')
    expect(formatAge(0)).toBe('0 años')
    expect(formatAge(35)).toBe('35 años')
  })
})

describe('formatDocument', () => {
  it('shows the label of the document type and the number', () => {
    expect(
      formatDocument({ documentType: 'NationalId', documentNumber: '0102' }),
    ).toBe('Cédula 0102')
    expect(
      formatDocument({ documentType: 'Passport', documentNumber: 'AB123' }),
    ).toBe('Pasaporte AB123')
  })
})

describe('formatBirthDate', () => {
  it('formats yyyy-MM-dd as dd/MM/yyyy without shifting the day', () => {
    expect(formatBirthDate('1990-05-01')).toBe('01/05/1990')
  })
})

describe('toLocalIsoDate', () => {
  it('formats the local date with zero padding', () => {
    expect(toLocalIsoDate(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05')
  })
})

import { describe, expect, it } from 'vitest'

import {
  finishConsultationSchema,
  saveConsultationSchema,
  toNullableText,
} from '@/features/consultations/consultation-form-schema'

const valid = { reason: 'Dolor de cabeza', diagnosis: '', notes: '' }

function messages(result: {
  success: boolean
  error?: { issues: { message: string }[] }
}) {
  return result.error?.issues.map((issue) => issue.message) ?? []
}

describe('saveConsultationSchema', () => {
  it('accepts an empty diagnosis and notes', () => {
    expect(saveConsultationSchema.safeParse(valid).success).toBe(true)
  })

  it('rejects an empty or blank reason', () => {
    const result = saveConsultationSchema.safeParse({ ...valid, reason: '   ' })
    expect(messages(result)).toEqual(['El motivo es obligatorio.'])
  })

  it('rejects a reason longer than 500 characters', () => {
    const result = saveConsultationSchema.safeParse({
      ...valid,
      reason: 'a'.repeat(501),
    })
    expect(messages(result)).toEqual([
      'El motivo admite como máximo 500 caracteres.',
    ])
  })
})

describe('finishConsultationSchema', () => {
  it('requires the diagnosis', () => {
    const result = finishConsultationSchema.safeParse(valid)
    expect(messages(result)).toEqual([
      'El diagnóstico es obligatorio para finalizar.',
    ])
  })

  it('rejects a diagnosis of 1001 and notes of 4001 characters', () => {
    const result = finishConsultationSchema.safeParse({
      ...valid,
      diagnosis: 'a'.repeat(1001),
      notes: 'a'.repeat(4001),
    })
    expect(messages(result)).toEqual([
      'El diagnóstico admite como máximo 1000 caracteres.',
      'Las observaciones admiten como máximo 4000 caracteres.',
    ])
  })

  it('accepts a diagnosis', () => {
    expect(
      finishConsultationSchema.safeParse({ ...valid, diagnosis: 'Cefalea' })
        .success,
    ).toBe(true)
  })
})

describe('toNullableText', () => {
  it('turns a blank text into null and trims the rest', () => {
    expect(toNullableText('  ')).toBeNull()
    expect(toNullableText(' Cefalea ')).toBe('Cefalea')
  })
})

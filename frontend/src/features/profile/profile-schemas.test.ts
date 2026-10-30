import { describe, expect, it } from 'vitest'

import {
  changePasswordSchema,
  profileSchema,
} from '@/features/profile/profile-schemas'

const validProfile = { firstName: 'Ana', lastName: 'Torres', phone: '' }

const validPasswords = {
  currentPassword: 'Passw0rd',
  newPassword: 'Changed-Passw0rd',
  confirmNewPassword: 'Changed-Passw0rd',
}

function issuesOf(value: unknown): { path: string; message: string }[] {
  const result = changePasswordSchema.safeParse(value)
  return (result.error?.issues ?? []).map((issue) => ({
    path: issue.path.join('.'),
    message: issue.message,
  }))
}

describe('profileSchema', () => {
  it.each([
    ['firstName', '', 'Ingresa tu nombre.'],
    [
      'firstName',
      'a'.repeat(101),
      'El nombre no puede superar 100 caracteres.',
    ],
    ['lastName', '   ', 'Ingresa tu apellido.'],
  ])('rejects %s = "%s"', (field, value, message) => {
    const result = profileSchema.safeParse({ ...validProfile, [field]: value })

    expect(result.success).toBe(false)
    expect(result.error?.issues[0].message).toBe(message)
  })

  it('turns an empty phone into null and trims the names', () => {
    const result = profileSchema.parse({
      firstName: ' Ana ',
      lastName: 'Torres ',
      phone: '  ',
    })

    expect(result).toEqual({
      firstName: 'Ana',
      lastName: 'Torres',
      phone: null,
    })
  })

  it('rejects an invalid phone', () => {
    const result = profileSchema.safeParse({ ...validProfile, phone: 'abc' })

    expect(result.success).toBe(false)
    expect(result.error?.issues[0].message).toBe(
      'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
    )
  })

  it('accepts a valid phone', () => {
    const result = profileSchema.parse({
      ...validProfile,
      phone: '0991234567',
    })

    expect(result.phone).toBe('0991234567')
  })
})

describe('changePasswordSchema', () => {
  it('accepts valid passwords', () => {
    expect(issuesOf(validPasswords)).toEqual([])
  })

  it('requires the current password', () => {
    expect(issuesOf({ ...validPasswords, currentPassword: '' })).toEqual([
      { path: 'currentPassword', message: 'Ingresa tu contraseña actual.' },
    ])
  })

  it('applies the password rules to the new one', () => {
    expect(
      issuesOf({
        ...validPasswords,
        newPassword: 'short1',
        confirmNewPassword: 'short1',
      }),
    ).toEqual([
      {
        path: 'newPassword',
        message: 'La contraseña debe tener al menos 8 caracteres.',
      },
    ])
  })

  it('requires the confirmation to match', () => {
    expect(
      issuesOf({ ...validPasswords, confirmNewPassword: 'Other-Passw0rd' }),
    ).toEqual([
      { path: 'confirmNewPassword', message: 'Las contraseñas no coinciden.' },
    ])
  })

  it('requires the new password to be different from the current one', () => {
    expect(
      issuesOf({
        currentPassword: 'Passw0rd',
        newPassword: 'Passw0rd',
        confirmNewPassword: 'Passw0rd',
      }),
    ).toEqual([
      {
        path: 'newPassword',
        message: 'La nueva contraseña debe ser distinta de la actual.',
      },
    ])
  })
})

import { describe, expect, it } from 'vitest'

import { getInitials } from '@/features/layout/initials'

describe('getInitials', () => {
  it.each([
    ['Ana', 'Torres', 'AT'],
    ['María José', 'Pérez Gómez', 'MP'],
    ['  ángel ', 'ñuñez', 'ÁÑ'],
    ['Ana', '', 'A'],
    ['', 'Torres', 'T'],
    ['', '  ', '?'],
  ])('returns the initials of "%s" "%s"', (firstName, lastName, expected) => {
    expect(getInitials(firstName, lastName)).toBe(expected)
  })
})

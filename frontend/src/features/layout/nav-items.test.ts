import { House } from 'lucide-react'
import { describe, expect, it } from 'vitest'

import type { UserRole } from '@/features/auth/auth-types'
import { getNavItems, isNavItemActive } from '@/features/layout/nav-items'

const roles: UserRole[] = ['Admin', 'Doctor', 'Receptionist']

describe('getNavItems', () => {
  it.each(roles)('starts with Inicio for %s', (role) => {
    const [first] = getNavItems(role)

    expect(first.title).toBe('Inicio')
    expect(first.url).toBe('/')
  })

  it.each(roles)('has no repeated urls for %s', (role) => {
    const urls = getNavItems(role).map((item) => item.url)

    expect(new Set(urls).size).toBe(urls.length)
  })
})

describe('isNavItemActive', () => {
  const home = { title: 'Inicio', url: '/', icon: House }
  const patients = { title: 'Pacientes', url: '/patients', icon: House }

  it('matches / only exactly', () => {
    expect(isNavItemActive(home, '/')).toBe(true)
    expect(isNavItemActive(home, '/patients')).toBe(false)
  })

  it('matches the other items with their route and its children', () => {
    expect(isNavItemActive(patients, '/patients')).toBe(true)
    expect(isNavItemActive(patients, '/patients/42')).toBe(true)
    expect(isNavItemActive(patients, '/patients-archive')).toBe(false)
  })
})

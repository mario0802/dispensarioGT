import { render, screen } from '@testing-library/react'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import type { CurrentUserDto } from '@/features/auth/auth-types'
import { ProfilePage } from '@/features/profile/profile-page'
import { testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProfilePage', () => {
  it.each([
    ['Admin', 'Administrador'],
    ['Receptionist', 'Recepcionista'],
    ['Doctor', 'Doctor'],
  ] as const)(
    'shows the header and both cards for %s without calling the API',
    (role, label) => {
      const user: CurrentUserDto = { ...testUser, role }
      render(
        <TestAuthProvider user={user}>
          <ProfilePage />
        </TestAuthProvider>,
      )

      expect(screen.getByText('AT')).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 2, name: 'Ana Torres' }),
      ).toBeInTheDocument()
      // El rol sale en el encabezado y en la tarjeta; el email, también
      expect(screen.getAllByText(label)).toHaveLength(2)
      expect(screen.getAllByText(user.email)).toHaveLength(2)
      expect(screen.getByText('Datos personales')).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Cambiar contraseña' }),
      ).toBeInTheDocument()
      expect(fetchMock).not.toHaveBeenCalled()
    },
  )
})

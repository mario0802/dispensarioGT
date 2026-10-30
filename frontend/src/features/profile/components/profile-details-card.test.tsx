import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/ui/sonner'
import type { CurrentUserDto } from '@/features/auth/auth-types'
import { AuthContext, type AuthContextValue } from '@/features/auth/use-auth'
import { ProfileDetailsCard } from '@/features/profile/components/profile-details-card'
import { problemResponse, testUser } from '@/test/api-responses'
import { mockPrefersDark } from '@/test/match-media'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  mockPrefersDark(false)
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  document.documentElement.classList.remove('light', 'dark')
})

const user: CurrentUserDto = { ...testUser, phone: '0991234567' }

function renderCard() {
  const refreshUser = vi.fn(async () => undefined)
  const auth: AuthContextValue = {
    status: 'authenticated',
    user,
    login: vi.fn(),
    logout: vi.fn(),
    retry: vi.fn(),
    refreshUser,
  }
  const userEvents = userEvent.setup()
  render(
    <ThemeProvider>
      <AuthContext value={auth}>
        <ProfileDetailsCard user={user} />
        <Toaster />
      </AuthContext>
    </ThemeProvider>,
  )
  return { userEvents, refreshUser }
}

const saveButton = () => screen.getByRole('button', { name: 'Guardar' })

describe('ProfileDetailsCard', () => {
  it('shows the read-only data and the initial values', () => {
    renderCard()

    expect(screen.getByText(user.email)).toBeInTheDocument()
    expect(screen.getByText('Cédula 1712345678')).toBeInTheDocument()
    expect(screen.getByText('Recepcionista')).toBeInTheDocument()
    expect(
      screen.getByText(
        'El email, el documento y el rol no se pueden modificar.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveValue('Ana')
    expect(screen.getByLabelText('Apellido')).toHaveValue('Torres')
    expect(screen.getByLabelText('Teléfono')).toHaveValue('0991234567')
  })

  it('disables Guardar until something changes', async () => {
    const { userEvents } = renderCard()
    expect(saveButton()).toBeDisabled()

    await userEvents.type(screen.getByLabelText('Apellido'), ' Ruiz')

    expect(saveButton()).toBeEnabled()
  })

  it('saves with PUT /api/users/me, the empty phone as null, and refreshes the user', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    const { userEvents, refreshUser } = renderCard()

    const lastName = screen.getByLabelText('Apellido')
    await userEvents.clear(lastName)
    await userEvents.type(lastName, 'Ruiz ')
    await userEvents.clear(screen.getByLabelText('Teléfono'))
    await userEvents.click(saveButton())

    expect(await screen.findByText('Datos actualizados.')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/users\/me$/)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual({
      firstName: 'Ana',
      lastName: 'Ruiz',
      phone: null,
    })
    expect(refreshUser).toHaveBeenCalledOnce()
    // Lo guardado es el nuevo estado limpio
    await waitFor(() => expect(saveButton()).toBeDisabled())
    expect(lastName).toHaveValue('Ruiz')
  })

  it('shows the error toast and keeps what was typed on a 400', async () => {
    fetchMock.mockResolvedValue(problemResponse(400))
    const { userEvents, refreshUser } = renderCard()

    await userEvents.type(screen.getByLabelText('Nombre'), ' María')
    await userEvents.click(saveButton())

    expect(
      await screen.findByText('Revisa los datos e inténtalo de nuevo.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveValue('Ana María')
    expect(refreshUser).not.toHaveBeenCalled()
  })
})

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
import { ChangePasswordCard } from '@/features/profile/components/change-password-card'
import { jsonResponse, problemResponse } from '@/test/api-responses'
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

function renderCard() {
  const userEvents = userEvent.setup()
  render(
    <ThemeProvider>
      <ChangePasswordCard />
      <Toaster />
    </ThemeProvider>,
  )
  return { userEvents }
}

const currentPassword = () => screen.getByLabelText('Contraseña actual')
const newPassword = () => screen.getByLabelText('Nueva contraseña')
const confirmNewPassword = () =>
  screen.getByLabelText('Confirmar nueva contraseña')

async function fillAndSubmit(userEvents: ReturnType<typeof userEvent.setup>) {
  await userEvents.type(currentPassword(), 'Passw0rd')
  await userEvents.type(newPassword(), 'Changed-Passw0rd')
  await userEvents.type(confirmNewPassword(), 'Changed-Passw0rd')
  await userEvents.click(
    screen.getByRole('button', { name: 'Cambiar contraseña' }),
  )
}

describe('ChangePasswordCard', () => {
  it('has the eye button only on the new password and its confirmation', () => {
    renderCard()

    expect(currentPassword()).toHaveAttribute('type', 'password')
    expect(
      screen.getByRole('button', { name: 'Mostrar contraseña' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Mostrar confirmación' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /^Mostrar/ })).toHaveLength(2)
  })

  it('changes the password with PUT /api/users/me/password and clears the fields', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    const { userEvents } = renderCard()

    await fillAndSubmit(userEvents)

    expect(await screen.findByText('Contraseña cambiada.')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/users\/me\/password$/)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual({
      currentPassword: 'Passw0rd',
      newPassword: 'Changed-Passw0rd',
    })
    await waitFor(() => expect(currentPassword()).toHaveValue(''))
    expect(newPassword()).toHaveValue('')
    expect(confirmNewPassword()).toHaveValue('')
  })

  it('marks the current password when the API says it is incorrect', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(400, 'Current password is incorrect.'),
    )
    const { userEvents } = renderCard()

    await fillAndSubmit(userEvents)

    expect(
      await screen.findByText('La contraseña actual no es correcta.'),
    ).toBeInTheDocument()
    expect(currentPassword()).toHaveAttribute('aria-invalid', 'true')
    expect(
      document.querySelector('[data-sonner-toast][data-type="error"]'),
    ).toBeNull()
    expect(newPassword()).toHaveValue('Changed-Passw0rd')
  })

  it('shows a toast for a 400 with validation errors', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'Validation',
          status: 400,
          ValidationErrors: [
            { propertyName: 'NewPassword', errorMessage: 'Weak.' },
          ],
        },
        400,
      ),
    )
    const { userEvents } = renderCard()

    await fillAndSubmit(userEvents)

    expect(
      await screen.findByText(
        'La nueva contraseña no cumple las reglas o es igual a la actual.',
      ),
    ).toBeInTheDocument()
  })

  it('shows a toast on a network error and keeps the fields', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    const { userEvents } = renderCard()

    await fillAndSubmit(userEvents)

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
    expect(currentPassword()).toHaveValue('Passw0rd')
  })
})

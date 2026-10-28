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

import { AuthProvider } from '@/features/auth/auth-provider'
import { LoginForm } from '@/features/auth/components/login-form'
import { TOKEN_STORAGE_KEY } from '@/features/auth/token-storage'
import { setAccessTokenProvider } from '@/lib/api/access-token'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'
import {
  jsonResponse,
  loginResponse,
  problemResponse,
  testUser,
} from '@/test/api-responses'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAccessTokenProvider(null)
  setUnauthorizedHandler(null)
})

function renderForm() {
  const user = userEvent.setup()
  render(
    <AuthProvider>
      <LoginForm />
    </AuthProvider>,
  )
  return {
    user,
    emailInput: screen.getByLabelText('Email'),
    passwordInput: screen.getByLabelText('Contraseña'),
    submit: () =>
      user.click(screen.getByRole('button', { name: 'Iniciar sesión' })),
  }
}

async function fillAndSubmit(email = testUser.email, password = 'Passw0rd') {
  const form = renderForm()
  await form.user.type(form.emailInput, email)
  await form.user.type(form.passwordInput, password)
  await form.submit()
  return form
}

describe('LoginForm', () => {
  it('shows the required messages and calls no API when empty', async () => {
    const { submit } = renderForm()

    await submit()

    expect(await screen.findByText('Ingresa tu email.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa tu contraseña.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows a message for an invalid email', async () => {
    const { user, emailInput, passwordInput, submit } = renderForm()

    await user.type(emailInput, 'ana@')
    await user.type(passwordInput, 'Passw0rd')
    await submit()

    expect(
      await screen.findByText('Ingresa un email válido.'),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('posts the trimmed email and the password', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(loginResponse()))
      .mockResolvedValueOnce(jsonResponse(testUser))

    await fillAndSubmit(`  ${testUser.email} `, ' Passw0rd ')

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/auth\/login$/)
    expect(JSON.parse(init!.body as string)).toEqual({
      email: testUser.email,
      password: ' Passw0rd ',
    })
  })

  it('disables the button while the login is in progress', async () => {
    fetchMock.mockReturnValue(new Promise<Response>(() => {}))

    await fillAndSubmit()

    const button = await screen.findByRole('button', {
      name: 'Iniciando sesión…',
    })
    expect(button).toBeDisabled()
  })

  it('shows invalid credentials, clears the password and focuses it on a 401', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(401, 'Unauthorized'))

    const { emailInput, passwordInput } = await fillAndSubmit()

    expect(
      await screen.findByText('Email o contraseña incorrectos.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(passwordInput).toHaveValue(''))
    expect(passwordInput).toHaveFocus()
    expect(emailInput).toHaveValue(testUser.email)
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('shows a connection message on a network error', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))

    await fillAndSubmit()

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
  })

  it('shows a generic message on any other error', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(500))

    await fillAndSubmit()

    expect(
      await screen.findByText(
        'Ocurrió un error inesperado. Inténtalo de nuevo.',
      ),
    ).toBeInTheDocument()
  })

  it('toggles the password visibility', async () => {
    const { user, passwordInput } = renderForm()
    expect(passwordInput).toHaveAttribute('type', 'password')

    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(passwordInput).toHaveAttribute('type', 'text')

    await user.click(screen.getByRole('button', { name: 'Ocultar contraseña' }))
    expect(passwordInput).toHaveAttribute('type', 'password')
  })
})

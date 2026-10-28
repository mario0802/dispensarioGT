import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { AppRoutes } from '@/app-routes'
import { ThemeProvider } from '@/components/theme-provider'
import { AuthProvider } from '@/features/auth/auth-provider'
import { TOKEN_STORAGE_KEY } from '@/features/auth/token-storage'
import { setAccessTokenProvider } from '@/lib/api/access-token'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'
import {
  jsonResponse,
  loginResponse,
  storeValidToken,
  testUser,
} from '@/test/api-responses'
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
  setAccessTokenProvider(null)
  setUnauthorizedHandler(null)
})

// Muestra la ruta actual para comprobar las redirecciones
function LocationDisplay() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname + location.search}</div>
}

function renderRoutes(initialEntry: string) {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <AuthProvider>
          <AppRoutes />
          <LocationDisplay />
        </AuthProvider>
      </MemoryRouter>
    </ThemeProvider>,
  )
  return user
}

const currentLocation = () => screen.getByTestId('location').textContent

const loginButton = () =>
  screen.queryByRole('button', { name: 'Iniciar sesión' })

async function logIn(user: ReturnType<typeof userEvent.setup>) {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(loginResponse()))
    .mockResolvedValueOnce(jsonResponse(testUser))
  await user.type(screen.getByLabelText('Email'), testUser.email)
  await user.type(screen.getByLabelText('Contraseña'), 'Passw0rd')
  await user.click(loginButton()!)
}

describe('AppRoutes', () => {
  it('redirects / to the login without a session', () => {
    renderRoutes('/')

    expect(currentLocation()).toBe('/login')
    expect(loginButton()).toBeInTheDocument()
  })

  it('redirects an unknown route to the login without a session', () => {
    renderRoutes('/unknown')

    expect(currentLocation()).toBe('/login')
  })

  it('shows neither the login nor the home while /me is loading', () => {
    storeValidToken()
    fetchMock.mockReturnValue(new Promise<Response>(() => {}))

    renderRoutes('/')

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(loginButton()).not.toBeInTheDocument()
    expect(screen.queryByText(/Hola,/)).not.toBeInTheDocument()
  })

  it('shows the error screen when /me fails and recovers with Reintentar', async () => {
    storeValidToken()
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    const user = renderRoutes('/')

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()

    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
  })

  it('shows the home with the name and the role after logging in', async () => {
    const user = renderRoutes('/login')

    await logIn(user)

    expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
    expect(screen.getByText('Recepcionista')).toBeInTheDocument()
    expect(currentLocation()).toBe('/')
  })

  it('returns to the requested route after logging in', async () => {
    const user = renderRoutes('/?x=1')
    expect(currentLocation()).toBe('/login')

    await logIn(user)

    await waitFor(() => expect(currentLocation()).toBe('/?x=1'))
  })

  it('redirects /login to the home with a session', async () => {
    storeValidToken()
    fetchMock.mockResolvedValue(jsonResponse(testUser))

    renderRoutes('/login')

    expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
    expect(currentLocation()).toBe('/')
  })

  it('logs out, removing the token, and goes to the login', async () => {
    storeValidToken()
    fetchMock.mockResolvedValue(jsonResponse(testUser))
    const user = renderRoutes('/')
    await screen.findByText('Hola, Ana Torres')

    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    expect(currentLocation()).toBe('/login')
    expect(loginButton()).toBeInTheDocument()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  describe('register', () => {
    const registeredNotice = 'Cuenta creada. Inicia sesión para continuar.'
    const registerButton = () =>
      screen.queryByRole('button', { name: 'Crear cuenta' })

    it('shows the register page without a session', () => {
      renderRoutes('/register')

      expect(currentLocation()).toBe('/register')
      expect(
        screen.getByText('Crea una cuenta de recepcionista.'),
      ).toBeInTheDocument()
      expect(registerButton()).toBeInTheDocument()
    })

    it('links the login and the register pages', async () => {
      const user = renderRoutes('/login')

      await user.click(screen.getByRole('link', { name: 'Regístrate' }))
      expect(currentLocation()).toBe('/register')
      expect(registerButton()).toBeInTheDocument()

      await user.click(screen.getByRole('link', { name: 'Inicia sesión' }))
      expect(currentLocation()).toBe('/login')
      expect(loginButton()).toBeInTheDocument()
    })

    it('redirects /register to the home with a session', async () => {
      storeValidToken()
      fetchMock.mockResolvedValue(jsonResponse(testUser))

      renderRoutes('/register')

      expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
      expect(currentLocation()).toBe('/')
    })

    it('goes to the login with the notice and the email after registering', async () => {
      const user = renderRoutes('/register')
      fetchMock.mockResolvedValueOnce(jsonResponse({ id: testUser.id }, 201))
      await user.type(screen.getByLabelText('Nombre'), testUser.firstName)
      await user.type(screen.getByLabelText('Apellido'), testUser.lastName)
      await user.type(
        screen.getByLabelText('Número de documento'),
        testUser.identificationNumber,
      )
      await user.type(screen.getByLabelText('Email'), testUser.email)
      await user.type(screen.getByLabelText('Contraseña'), 'Passw0rd')
      await user.type(screen.getByLabelText('Confirmar contraseña'), 'Passw0rd')
      await user.click(registerButton()!)

      expect(await screen.findByText(registeredNotice)).toBeInTheDocument()
      expect(currentLocation()).toBe('/login')
      expect(screen.getByLabelText('Email')).toHaveValue(testUser.email)

      fetchMock
        .mockResolvedValueOnce(jsonResponse(loginResponse()))
        .mockResolvedValueOnce(jsonResponse(testUser))
      await user.type(screen.getByLabelText('Contraseña'), 'Passw0rd')
      await user.click(loginButton()!)

      expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
      expect(currentLocation()).toBe('/')
    })

    it('does not show the notice on the login without state', () => {
      renderRoutes('/login')

      expect(screen.queryByText(registeredNotice)).not.toBeInTheDocument()
      expect(screen.getByLabelText('Email')).toHaveValue('')
    })
  })
})

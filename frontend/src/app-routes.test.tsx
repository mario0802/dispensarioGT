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
})

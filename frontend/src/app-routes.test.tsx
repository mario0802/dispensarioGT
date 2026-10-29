import { render, screen, waitFor, within } from '@testing-library/react'
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
import type { UserRole } from '@/features/auth/auth-types'
import { TOKEN_STORAGE_KEY } from '@/features/auth/token-storage'
import { SIDEBAR_COOKIE_NAME } from '@/features/layout/sidebar-cookie'
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
  document.cookie = `${SIDEBAR_COOKIE_NAME}=; path=/; max-age=0`
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
    // El rol sale también en el sidebar: se comprueba el del área principal
    expect(
      within(screen.getByRole('main')).getByText('Recepcionista'),
    ).toBeInTheDocument()
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

    await user.click(screen.getByRole('button', { name: 'Menú de usuario' }))
    await user.click(
      await screen.findByRole('menuitem', { name: 'Cerrar sesión' }),
    )

    expect(currentLocation()).toBe('/login')
    expect(loginButton()).toBeInTheDocument()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  describe('layout', () => {
    const sidebar = () => document.querySelector('[data-slot="sidebar"]')
    const toggleButton = () =>
      within(screen.getByRole('main')).getByRole('button', {
        name: 'Mostrar u ocultar menú',
      })

    async function renderWithSession() {
      storeValidToken()
      fetchMock.mockResolvedValue(jsonResponse(testUser))
      const user = renderRoutes('/')
      await screen.findByText('Hola, Ana Torres')
      return user
    }

    it('shows the sidebar, the Inicio title and the welcome on /', async () => {
      await renderWithSession()

      expect(screen.getByText('Dispensario Médico')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Inicio' })).toHaveAttribute(
        'aria-current',
        'page',
      )
      expect(
        screen.getByRole('button', { name: 'Menú de usuario' }),
      ).toBeInTheDocument()
      expect(
        within(screen.getByRole('main')).getByRole('heading', {
          level: 1,
          name: 'Inicio',
        }),
      ).toBeInTheDocument()
    })

    it('collapses the sidebar and remembers it in the cookie', async () => {
      const user = await renderWithSession()
      expect(sidebar()).toHaveAttribute('data-state', 'expanded')

      await user.click(toggleButton())

      expect(sidebar()).toHaveAttribute('data-state', 'collapsed')
      expect(document.cookie).toContain(`${SIDEBAR_COOKIE_NAME}=false`)
    })

    it('starts collapsed with the cookie set to false', async () => {
      document.cookie = `${SIDEBAR_COOKIE_NAME}=false; path=/`

      await renderWithSession()

      expect(sidebar()).toHaveAttribute('data-state', 'collapsed')
    })

    it.each(['/login', '/register'])(
      'does not show the sidebar on %s',
      (route) => {
        renderRoutes(route)

        expect(currentLocation()).toBe(route)
        expect(sidebar()).not.toBeInTheDocument()
      },
    )
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

  describe('doctors', () => {
    // /me devuelve el usuario con el rol pedido y /api/doctors, una página vacía
    function respondAs(role: UserRole) {
      storeValidToken()
      fetchMock.mockImplementation(async (input) =>
        new URL(String(input)).pathname === '/api/auth/me'
          ? jsonResponse({ ...testUser, role })
          : jsonResponse({ pageIndex: 0, pageSize: 10, count: 0, data: [] }),
      )
    }

    it.each<UserRole>(['Admin', 'Receptionist'])(
      'shows Doctores to %s and opens its page',
      async (role) => {
        respondAs(role)
        const user = renderRoutes('/')
        await screen.findByText('Hola, Ana Torres')

        await user.click(screen.getByRole('link', { name: 'Doctores' }))

        expect(currentLocation()).toBe('/doctors')
        expect(
          within(screen.getByRole('main')).getByRole('heading', {
            level: 1,
            name: 'Doctores',
          }),
        ).toBeInTheDocument()
        expect(
          await screen.findByText('Todavía no hay doctores registrados.'),
        ).toBeInTheDocument()
      },
    )

    it('hides Doctores from a Doctor and redirects /doctors to /', async () => {
      respondAs('Doctor')
      renderRoutes('/doctors')

      expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
      expect(currentLocation()).toBe('/')
      expect(
        screen.queryByRole('link', { name: 'Doctores' }),
      ).not.toBeInTheDocument()
    })
  })

  describe('patients', () => {
    // /me devuelve el usuario con el rol pedido y /api/patients, una página vacía
    function respondAs(role: UserRole) {
      storeValidToken()
      fetchMock.mockImplementation(async (input) =>
        new URL(String(input)).pathname === '/api/auth/me'
          ? jsonResponse({ ...testUser, role })
          : jsonResponse({ pageIndex: 0, pageSize: 10, count: 0, data: [] }),
      )
    }

    it.each<UserRole>(['Admin', 'Receptionist'])(
      'shows Pacientes to %s and opens its page',
      async (role) => {
        respondAs(role)
        const user = renderRoutes('/')
        await screen.findByText('Hola, Ana Torres')

        await user.click(screen.getByRole('link', { name: 'Pacientes' }))

        expect(currentLocation()).toBe('/patients')
        expect(
          within(screen.getByRole('main')).getByRole('heading', {
            level: 1,
            name: 'Pacientes',
          }),
        ).toBeInTheDocument()
        expect(
          await screen.findByText('Todavía no hay pacientes registrados.'),
        ).toBeInTheDocument()
      },
    )

    it('hides Pacientes from a Doctor and redirects /patients to /', async () => {
      respondAs('Doctor')
      renderRoutes('/patients')

      expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
      expect(currentLocation()).toBe('/')
      expect(
        screen.queryByRole('link', { name: 'Pacientes' }),
      ).not.toBeInTheDocument()
    })
  })

  describe('appointments', () => {
    // /me devuelve el usuario con el rol pedido y /api/appointments, una página vacía
    function respondAs(role: UserRole) {
      storeValidToken()
      fetchMock.mockImplementation(async (input) =>
        new URL(String(input)).pathname === '/api/auth/me'
          ? jsonResponse({ ...testUser, role })
          : jsonResponse({ pageIndex: 0, pageSize: 10, count: 0, data: [] }),
      )
    }

    it.each<UserRole>(['Admin', 'Receptionist'])(
      'shows Citas to %s and opens its page',
      async (role) => {
        respondAs(role)
        const user = renderRoutes('/')
        await screen.findByText('Hola, Ana Torres')

        await user.click(screen.getByRole('link', { name: 'Citas' }))

        expect(currentLocation()).toBe('/appointments')
        expect(
          within(screen.getByRole('main')).getByRole('heading', {
            level: 1,
            name: 'Citas',
          }),
        ).toBeInTheDocument()
        expect(
          await screen.findByText('No hay citas próximas.'),
        ).toBeInTheDocument()
      },
    )

    it('shows Citas to a Doctor and opens its page', async () => {
      respondAs('Doctor')
      const user = renderRoutes('/')
      await screen.findByText('Hola, Ana Torres')

      await user.click(screen.getByRole('link', { name: 'Citas' }))

      expect(currentLocation()).toBe('/appointments')
      expect(
        within(screen.getByRole('main')).getByRole('heading', {
          level: 1,
          name: 'Citas',
        }),
      ).toBeInTheDocument()
    })
  })

  describe('consultation', () => {
    function respondAs(role: UserRole) {
      storeValidToken()
      fetchMock.mockImplementation(async () =>
        jsonResponse({ ...testUser, role }),
      )
    }

    it('opens the consultation page for a Doctor', async () => {
      respondAs('Doctor')
      renderRoutes('/consultations/0193f2e0')

      expect(
        await screen.findByRole('heading', { name: 'Consulta' }),
      ).toBeInTheDocument()
      expect(currentLocation()).toBe('/consultations/0193f2e0')
    })

    it.each<UserRole>(['Admin', 'Receptionist'])(
      'redirects %s from the consultation page to /',
      async (role) => {
        respondAs(role)
        renderRoutes('/consultations/0193f2e0')

        expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
        expect(currentLocation()).toBe('/')
      },
    )
  })

  describe('my schedule', () => {
    // /me devuelve el usuario con el rol pedido, sin ficha de doctor vinculada
    function respondAs(role: UserRole) {
      storeValidToken()
      fetchMock.mockImplementation(async () =>
        jsonResponse({ ...testUser, role, doctorId: null }),
      )
    }

    it('shows Mi horario to a Doctor and opens its page', async () => {
      respondAs('Doctor')
      const user = renderRoutes('/')
      await screen.findByText('Hola, Ana Torres')

      await user.click(screen.getByRole('link', { name: 'Mi horario' }))

      expect(currentLocation()).toBe('/my-schedule')
      expect(
        within(screen.getByRole('main')).getByRole('heading', {
          level: 1,
          name: 'Mi horario',
        }),
      ).toBeInTheDocument()
      expect(
        screen.getByText(/Tu usuario todavía no está vinculado/),
      ).toBeInTheDocument()
    })

    it.each<UserRole>(['Admin', 'Receptionist'])(
      'hides Mi horario from %s and redirects /my-schedule to /',
      async (role) => {
        respondAs(role)
        renderRoutes('/my-schedule')

        expect(await screen.findByText('Hola, Ana Torres')).toBeInTheDocument()
        expect(currentLocation()).toBe('/')
        expect(
          screen.queryByRole('link', { name: 'Mi horario' }),
        ).not.toBeInTheDocument()
      },
    )
  })
})

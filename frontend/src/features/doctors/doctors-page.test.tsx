import { render, screen, waitFor, within } from '@testing-library/react'
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
import { TooltipProvider } from '@/components/ui/tooltip'
import type { UserRole } from '@/features/auth/auth-types'
import type {
  DoctorDto,
  PaginatedResult,
} from '@/features/doctors/doctor-types'
import { DoctorsPage } from '@/features/doctors/doctors-page'
import { jsonResponse, problemResponse, testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'
import { mockPrefersDark } from '@/test/match-media'

function makeDoctor(
  index: number,
  overrides: Partial<DoctorDto> = {},
): DoctorDto {
  return {
    id: `3c1a0000-0000-0000-0000-${String(index).padStart(12, '0')}`,
    firstName: 'Luis',
    lastName: `Mora ${index}`,
    licenseNumber: `MP-${index}`,
    specialty: 'Pediatría',
    email: `luis.mora${index}@dispensario.local`,
    phone: '0991234567',
    appointmentDurationMinutes: 30,
    isActive: true,
    userId: null,
    createdAtUtc: '2026-10-26T15:00:00Z',
    ...overrides,
  }
}

const luis = makeDoctor(1, { lastName: 'Mora', phone: null })
const ana = makeDoctor(2, {
  firstName: 'Ana',
  lastName: 'Ruiz',
  specialty: 'Cardiología',
  isActive: false,
  appointmentDurationMinutes: 45,
})

function pageOf(
  data: DoctorDto[],
  count = data.length,
  pageIndex = 0,
): PaginatedResult<DoctorDto> {
  return { pageIndex, pageSize: 10, count, data }
}

interface Request {
  url: URL
  method: string
  body: unknown
}

let fetchMock: Mock<typeof fetch>
let requests: Request[]

// Responde según la ruta: por defecto, una página con Luis y Ana
let listResponse: (url: URL) => Response
let otherResponse: (request: Request) => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  listResponse = () => jsonResponse(pageOf([luis, ana]))
  otherResponse = () => new Response(null, { status: 204 })
  fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    if (request.method === 'GET' && request.url.pathname === '/api/doctors') {
      return listResponse(request.url)
    }
    return otherResponse(request)
  })
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

function renderPage(role: UserRole = 'Admin') {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <TestAuthProvider user={{ ...testUser, role }}>
        <TooltipProvider>
          <DoctorsPage />
        </TooltipProvider>
        <Toaster />
      </TestAuthProvider>
    </ThemeProvider>,
  )
  return user
}

const listRequests = () =>
  requests.filter(
    (request) =>
      request.method === 'GET' && request.url.pathname === '/api/doctors',
  )
const lastListParams = () => listRequests().at(-1)!.url.searchParams

const rowOf = (name: string) => screen.getByText(name).closest('tr')!

describe('DoctorsPage', () => {
  it('loads the first page and shows the doctors', async () => {
    renderPage()

    expect(screen.getAllByTestId('doctor-row-skeleton')).toHaveLength(5)
    const row = within(
      await screen.findByText('Luis Mora').then((cell) => cell.closest('tr')!),
    )

    expect(lastListParams().toString()).toBe('pageIndex=0&pageSize=10')
    expect(row.getByText(luis.email)).toBeInTheDocument()
    expect(row.getByText('MP-1')).toBeInTheDocument()
    expect(row.getByText('Pediatría')).toBeInTheDocument()
    expect(row.getByText('—')).toBeInTheDocument()
    expect(row.getByText('30 min')).toBeInTheDocument()
    expect(row.getByText('Activo')).toBeInTheDocument()

    const anaRow = within(rowOf('Ana Ruiz'))
    expect(anaRow.getByText('0991234567')).toBeInTheDocument()
    expect(anaRow.getByText('45 min')).toBeInTheDocument()
    expect(anaRow.getByText('Inactivo')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1 · 2 doctores')).toBeInTheDocument()
  })

  it('searches once after the debounce and goes back to the first page', async () => {
    listResponse = (url) =>
      jsonResponse(
        pageOf([luis], 15, Number(url.searchParams.get('pageIndex'))),
      )
    const user = renderPage()
    await screen.findByText('Luis Mora')
    await user.click(screen.getByRole('button', { name: /Siguiente/ }))
    await waitFor(() => expect(lastListParams().get('pageIndex')).toBe('1'))

    await user.type(screen.getByLabelText('Buscar doctores'), ' mora')

    await waitFor(() => expect(lastListParams().get('search')).toBe('mora'))
    const searches = listRequests().filter((request) =>
      request.url.searchParams.has('search'),
    )
    expect(searches).toHaveLength(1)
    expect(lastListParams().get('pageIndex')).toBe('0')
  })

  it('filters by status', async () => {
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(screen.getByLabelText('Filtrar por estado'))
    await user.click(await screen.findByRole('option', { name: 'Inactivos' }))
    await waitFor(() => expect(lastListParams().get('isActive')).toBe('false'))

    await user.click(screen.getByLabelText('Filtrar por estado'))
    await user.click(await screen.findByRole('option', { name: 'Activos' }))
    await waitFor(() => expect(lastListParams().get('isActive')).toBe('true'))

    await user.click(screen.getByLabelText('Filtrar por estado'))
    await user.click(await screen.findByRole('option', { name: 'Todos' }))
    await waitFor(() => expect(lastListParams().has('isActive')).toBe(false))
  })

  it('moves between pages and disables the buttons at the ends', async () => {
    listResponse = (url) => {
      const pageIndex = Number(url.searchParams.get('pageIndex'))
      return jsonResponse(pageOf([makeDoctor(pageIndex + 10)], 12, pageIndex))
    }
    const user = renderPage()

    expect(
      await screen.findByText('Página 1 de 2 · 12 doctores'),
    ).toBeInTheDocument()
    const previous = screen.getByRole('button', { name: /Anterior/ })
    const next = screen.getByRole('button', { name: /Siguiente/ })
    expect(previous).toBeDisabled()
    expect(next).toBeEnabled()

    await user.click(next)

    expect(
      await screen.findByText('Página 2 de 2 · 12 doctores'),
    ).toBeInTheDocument()
    expect(lastListParams().get('pageIndex')).toBe('1')
    expect(screen.getByRole('button', { name: /Siguiente/ })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /Anterior/ }))

    expect(
      await screen.findByText('Página 1 de 2 · 12 doctores'),
    ).toBeInTheDocument()
    expect(lastListParams().get('pageIndex')).toBe('0')
  })

  it('shows the empty state without filters', async () => {
    listResponse = () => jsonResponse(pageOf([]))
    renderPage()

    expect(
      await screen.findByText('Todavía no hay doctores registrados.'),
    ).toBeInTheDocument()
  })

  it('shows the no results state with filters', async () => {
    listResponse = (url) =>
      jsonResponse(pageOf(url.searchParams.has('search') ? [] : [luis]))
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.type(screen.getByLabelText('Buscar doctores'), 'zzz')

    expect(
      await screen.findByText('No se encontraron doctores con esos filtros.'),
    ).toBeInTheDocument()
  })

  it('shows the load error and retries the query', async () => {
    listResponse = () => problemResponse(500)
    const user = renderPage()

    expect(
      await screen.findByText('No se pudieron cargar los doctores.'),
    ).toBeInTheDocument()

    listResponse = () => jsonResponse(pageOf([luis]))
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('Luis Mora')).toBeInTheDocument()
    expect(listRequests()).toHaveLength(2)
  })

  it('shows the write actions to the Admin', async () => {
    renderPage('Admin')
    await screen.findByText('Luis Mora')

    expect(
      screen.getByRole('button', { name: 'Nuevo doctor' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Más acciones de Luis Mora' }),
    ).toBeInTheDocument()
  })

  it('shows only the schedule to the receptionist', async () => {
    renderPage('Receptionist')
    await screen.findByText('Luis Mora')

    expect(
      screen.queryByRole('button', { name: 'Nuevo doctor' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Más acciones/ }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Ver cronograma de Luis Mora' }),
    ).toBeInTheDocument()
  })

  it('creates a doctor and refreshes the list', async () => {
    otherResponse = () => jsonResponse({ id: 'new-id' }, 201)
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(screen.getByRole('button', { name: 'Nuevo doctor' }))
    const dialog = within(await screen.findByRole('dialog'))
    await user.type(dialog.getByLabelText('Nombre'), 'Eva')
    await user.type(dialog.getByLabelText('Apellido'), 'Paz')
    await user.type(dialog.getByLabelText('Matrícula'), 'mg-9')
    await user.type(dialog.getByLabelText('Especialidad'), 'Medicina General')
    await user.type(dialog.getByLabelText('Email'), 'eva.paz@dispensario.local')
    await user.click(dialog.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Doctor creado.')).toBeInTheDocument()
    const creation = requests.find((request) => request.method === 'POST')!
    expect(creation.url.pathname).toBe('/api/doctors')
    expect(creation.body).toEqual({
      firstName: 'Eva',
      lastName: 'Paz',
      licenseNumber: 'mg-9',
      specialty: 'Medicina General',
      email: 'eva.paz@dispensario.local',
      phone: null,
      appointmentDurationMinutes: 30,
    })
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('edits a doctor loading its details first', async () => {
    otherResponse = (request) =>
      request.method === 'GET'
        ? jsonResponse({ ...luis, specialty: 'Pediatría Clínica' })
        : new Response(null, { status: 204 })
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Luis Mora' }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Editar' }))

    const dialog = within(await screen.findByRole('dialog'))
    expect(
      dialog.getByText('Modifica los datos de Luis Mora.'),
    ).toBeInTheDocument()
    expect(await dialog.findByLabelText('Especialidad')).toHaveValue(
      'Pediatría Clínica',
    )
    const details = requests.find(
      (request) => request.url.pathname === `/api/doctors/${luis.id}`,
    )!
    expect(details.method).toBe('GET')

    await user.clear(dialog.getByLabelText('Apellido'))
    await user.type(dialog.getByLabelText('Apellido'), 'Mora Vega')
    await user.click(dialog.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Doctor actualizado.')).toBeInTheDocument()
    const update = requests.find((request) => request.method === 'PUT')!
    expect(update.url.pathname).toBe(`/api/doctors/${luis.id}`)
    expect(update.body).toMatchObject({
      lastName: 'Mora Vega',
      specialty: 'Pediatría Clínica',
      phone: null,
      appointmentDurationMinutes: 30,
    })
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('closes the edition and refreshes when the doctor no longer exists', async () => {
    otherResponse = () => problemResponse(404)
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Luis Mora' }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Editar' }))

    expect(
      await screen.findByText('El doctor ya no existe.'),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('deactivates a doctor after confirming and refreshes the list', async () => {
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Luis Mora' }),
    )
    await user.click(
      await screen.findByRole('menuitem', { name: 'Desactivar' }),
    )
    listResponse = () => jsonResponse(pageOf([{ ...luis, isActive: false }]))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Desactivar',
      }),
    )

    expect(await screen.findByText('Doctor desactivado.')).toBeInTheDocument()
    expect(
      requests.some((request) =>
        request.url.pathname.endsWith(`${luis.id}/deactivate`),
      ),
    ).toBe(true)
    await waitFor(() =>
      expect(
        within(rowOf('Luis Mora')).getByText('Inactivo'),
      ).toBeInTheDocument(),
    )
  })

  it('shows the network error of an action', async () => {
    otherResponse = () => {
      throw new TypeError('Failed to fetch')
    }
    const user = renderPage()
    await screen.findByText('Ana Ruiz')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Ana Ruiz' }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Activar' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Activar',
      }),
    )

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
  })

  it('opens the schedule dialog and loads the schedule', async () => {
    otherResponse = (request) =>
      request.url.pathname === `/api/doctors/${luis.id}/schedule`
        ? jsonResponse({
            doctorId: luis.id,
            blocks: [
              { dayOfWeek: 'Monday', startTime: '08:00', endTime: '12:00' },
            ],
          })
        : new Response(null, { status: 204 })
    const user = renderPage('Receptionist')
    await screen.findByText('Luis Mora')

    await user.click(
      screen.getByRole('button', { name: 'Ver cronograma de Luis Mora' }),
    )

    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Cronograma de Luis Mora')).toBeInTheDocument()
    expect(dialog.getByText('Pediatría')).toBeInTheDocument()
    expect(await dialog.findByText('08:00 – 12:00')).toBeInTheDocument()
    expect(
      requests.some(
        (request) =>
          request.method === 'GET' &&
          request.url.pathname === `/api/doctors/${luis.id}/schedule`,
      ),
    ).toBe(true)

    // El primero es el botón del pie; el segundo, la X del modal
    await user.click(dialog.getAllByRole('button', { name: 'Cerrar' })[0])
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
  })
})

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
import { getAge } from '@/features/patients/patient-labels'
import type { PatientDto } from '@/features/patients/patient-types'
import { PatientsPage } from '@/features/patients/patients-page'
import type { PaginatedResult } from '@/lib/api/paginated-result'
import { jsonResponse, problemResponse, testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'
import { mockPrefersDark } from '@/test/match-media'

function makePatient(
  index: number,
  overrides: Partial<PatientDto> = {},
): PatientDto {
  return {
    id: `7b2e0000-0000-0000-0000-${String(index).padStart(12, '0')}`,
    documentType: 'NationalId',
    documentNumber: `01020304${String(index).padStart(2, '0')}`,
    firstName: 'Ana',
    lastName: `Pérez ${index}`,
    birthDate: '1990-05-14',
    gender: 'Female',
    email: `ana${index}@example.com`,
    phone: '0991234567',
    address: null,
    bloodType: null,
    allergies: null,
    isActive: true,
    createdAtUtc: '2026-10-27T15:00:00Z',
    ...overrides,
  }
}

const ana = makePatient(1, { lastName: 'Pérez', phone: null })
const luis = makePatient(2, {
  firstName: 'Luis',
  lastName: 'Mora',
  documentType: 'Passport',
  documentNumber: 'AB123',
  email: null,
  isActive: false,
})

function pageOf(
  data: PatientDto[],
  count = data.length,
  pageIndex = 0,
): PaginatedResult<PatientDto> {
  return { pageIndex, pageSize: 10, count, data }
}

interface Request {
  url: URL
  method: string
  body: unknown
}

let fetchMock: Mock<typeof fetch>
let requests: Request[]

// Responde según la ruta: por defecto, una página con Ana y Luis
let listResponse: (url: URL) => Response
let otherResponse: (request: Request) => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  listResponse = () => jsonResponse(pageOf([ana, luis]))
  otherResponse = () => new Response(null, { status: 204 })
  fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    if (request.method === 'GET' && request.url.pathname === '/api/patients') {
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

function renderPage(role: UserRole = 'Receptionist') {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <TestAuthProvider user={{ ...testUser, role }}>
        <TooltipProvider>
          <PatientsPage />
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
      request.method === 'GET' && request.url.pathname === '/api/patients',
  )
const lastListParams = () => listRequests().at(-1)!.url.searchParams

const rowOf = (name: string) => screen.getByText(name).closest('tr')!

describe('PatientsPage', () => {
  it('loads the first page and shows the patients', async () => {
    renderPage()

    expect(screen.getAllByTestId('patient-row-skeleton')).toHaveLength(5)
    const row = within(
      await screen.findByText('Ana Pérez').then((cell) => cell.closest('tr')!),
    )

    expect(lastListParams().toString()).toBe('pageIndex=0&pageSize=10')
    expect(row.getByText(ana.email!)).toBeInTheDocument()
    expect(row.getByText('Cédula 0102030401')).toBeInTheDocument()
    const age = getAge(ana.birthDate, new Date())
    expect(row.getByText(`${age} años`)).toBeInTheDocument()
    expect(row.getByText('14/05/1990')).toBeInTheDocument()
    expect(row.getByText('—')).toBeInTheDocument()
    expect(row.getByText('Activo')).toBeInTheDocument()

    const luisRow = within(rowOf('Luis Mora'))
    expect(luisRow.getByText('Pasaporte AB123')).toBeInTheDocument()
    expect(luisRow.getByText('0991234567')).toBeInTheDocument()
    expect(luisRow.getByText('Inactivo')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1 · 2 pacientes')).toBeInTheDocument()
  })

  it('searches once after the debounce and goes back to the first page', async () => {
    listResponse = (url) =>
      jsonResponse(pageOf([ana], 15, Number(url.searchParams.get('pageIndex'))))
    const user = renderPage()
    await screen.findByText('Ana Pérez')
    await user.click(screen.getByRole('button', { name: /Siguiente/ }))
    await waitFor(() => expect(lastListParams().get('pageIndex')).toBe('1'))

    await user.type(screen.getByLabelText('Buscar pacientes'), ' 0102')

    await waitFor(() => expect(lastListParams().get('search')).toBe('0102'))
    const searches = listRequests().filter((request) =>
      request.url.searchParams.has('search'),
    )
    expect(searches).toHaveLength(1)
    expect(lastListParams().get('pageIndex')).toBe('0')
  })

  it('filters by status', async () => {
    const user = renderPage()
    await screen.findByText('Ana Pérez')

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
      return jsonResponse(pageOf([makePatient(pageIndex + 10)], 12, pageIndex))
    }
    const user = renderPage()

    expect(
      await screen.findByText('Página 1 de 2 · 12 pacientes'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Anterior/ })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /Siguiente/ }))

    expect(
      await screen.findByText('Página 2 de 2 · 12 pacientes'),
    ).toBeInTheDocument()
    expect(lastListParams().get('pageIndex')).toBe('1')
    expect(screen.getByRole('button', { name: /Siguiente/ })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /Anterior/ }))

    expect(
      await screen.findByText('Página 1 de 2 · 12 pacientes'),
    ).toBeInTheDocument()
  })

  it('shows the empty state without filters', async () => {
    listResponse = () => jsonResponse(pageOf([]))
    renderPage()

    expect(
      await screen.findByText('Todavía no hay pacientes registrados.'),
    ).toBeInTheDocument()
  })

  it('shows the no results state with filters', async () => {
    listResponse = (url) =>
      jsonResponse(pageOf(url.searchParams.has('search') ? [] : [ana]))
    const user = renderPage()
    await screen.findByText('Ana Pérez')

    await user.type(screen.getByLabelText('Buscar pacientes'), 'zzz')

    expect(
      await screen.findByText('No se encontraron pacientes con esos filtros.'),
    ).toBeInTheDocument()
  })

  it('shows the load error and retries the query', async () => {
    listResponse = () => problemResponse(500)
    const user = renderPage()

    expect(
      await screen.findByText('No se pudieron cargar los pacientes.'),
    ).toBeInTheDocument()

    listResponse = () => jsonResponse(pageOf([ana]))
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('Ana Pérez')).toBeInTheDocument()
    expect(listRequests()).toHaveLength(2)
  })

  it.each<UserRole>(['Admin', 'Receptionist'])(
    'shows the write actions to %s',
    async (role) => {
      renderPage(role)
      await screen.findByText('Ana Pérez')

      expect(
        screen.getByRole('button', { name: 'Nuevo paciente' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Editar a Ana Pérez' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Más acciones de Ana Pérez' }),
      ).toBeInTheDocument()
    },
  )

  it('creates a patient and refreshes the list', async () => {
    otherResponse = () => jsonResponse({ id: 'new-id' }, 201)
    const user = renderPage()
    await screen.findByText('Ana Pérez')

    await user.click(screen.getByRole('button', { name: 'Nuevo paciente' }))
    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Registra un paciente activo.')).toBeInTheDocument()
    await user.type(dialog.getByLabelText('Número de documento'), '1712345678')
    await user.type(dialog.getByLabelText('Nombre'), 'Eva')
    await user.type(dialog.getByLabelText('Apellido'), 'Paz')
    await user.type(dialog.getByLabelText('Fecha de nacimiento'), '2001-02-03')
    await user.click(dialog.getByLabelText('Género'))
    await user.click(await screen.findByRole('option', { name: 'Otro' }))
    await user.click(dialog.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Paciente creado.')).toBeInTheDocument()
    const creation = requests.find((request) => request.method === 'POST')!
    expect(creation.url.pathname).toBe('/api/patients')
    expect(creation.body).toEqual({
      documentType: 'NationalId',
      documentNumber: '1712345678',
      firstName: 'Eva',
      lastName: 'Paz',
      birthDate: '2001-02-03',
      gender: 'Other',
      email: null,
      phone: null,
      address: null,
      bloodType: null,
      allergies: null,
    })
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('edits a patient loading its details first', async () => {
    otherResponse = (request) =>
      request.method === 'GET'
        ? jsonResponse({ ...ana, bloodType: 'O+', allergies: 'Polen' })
        : new Response(null, { status: 204 })
    const user = renderPage()
    await screen.findByText('Ana Pérez')

    await user.click(screen.getByRole('button', { name: 'Editar a Ana Pérez' }))

    const dialog = within(await screen.findByRole('dialog'))
    expect(
      dialog.getByText('Modifica los datos de Ana Pérez.'),
    ).toBeInTheDocument()
    expect(await dialog.findByLabelText('Alergias (opcional)')).toHaveValue(
      'Polen',
    )
    const details = requests.find(
      (request) => request.url.pathname === `/api/patients/${ana.id}`,
    )!
    expect(details.method).toBe('GET')

    await user.clear(dialog.getByLabelText('Alergias (opcional)'))
    await user.clear(dialog.getByLabelText('Email (opcional)'))
    await user.click(dialog.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Paciente actualizado.')).toBeInTheDocument()
    const update = requests.find((request) => request.method === 'PUT')!
    expect(update.url.pathname).toBe(`/api/patients/${ana.id}`)
    expect(update.body).toMatchObject({
      documentNumber: ana.documentNumber,
      bloodType: 'O+',
      allergies: null,
      email: null,
    })
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('closes the edition and refreshes when the patient no longer exists', async () => {
    otherResponse = () => problemResponse(404)
    const user = renderPage()
    await screen.findByText('Ana Pérez')

    await user.click(screen.getByRole('button', { name: 'Editar a Ana Pérez' }))

    expect(
      await screen.findByText('El paciente ya no existe.'),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    await waitFor(() => expect(listRequests()).toHaveLength(2))
  })

  it('deactivates a patient after confirming and refreshes the list', async () => {
    const user = renderPage()
    await screen.findByText('Ana Pérez')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Ana Pérez' }),
    )
    await user.click(
      await screen.findByRole('menuitem', { name: 'Desactivar' }),
    )
    listResponse = () => jsonResponse(pageOf([{ ...ana, isActive: false }]))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Desactivar',
      }),
    )

    expect(await screen.findByText('Paciente desactivado.')).toBeInTheDocument()
    expect(
      requests.some((request) =>
        request.url.pathname.endsWith(`${ana.id}/deactivate`),
      ),
    ).toBe(true)
    await waitFor(() =>
      expect(
        within(rowOf('Ana Pérez')).getByText('Inactivo'),
      ).toBeInTheDocument(),
    )
  })

  it('shows the network error of an action', async () => {
    otherResponse = () => {
      throw new TypeError('Failed to fetch')
    }
    const user = renderPage()
    await screen.findByText('Luis Mora')

    await user.click(
      screen.getByRole('button', { name: 'Más acciones de Luis Mora' }),
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
})

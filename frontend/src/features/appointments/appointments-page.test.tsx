import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
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
import { toLocalDateString } from '@/features/appointments/appointment-labels'
import type { AppointmentDto } from '@/features/appointments/appointment-types'
import { AppointmentsPage } from '@/features/appointments/appointments-page'
import type { CurrentUserDto } from '@/features/auth/auth-types'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import type { PatientDto } from '@/features/patients/patient-types'
import type { PaginatedResult } from '@/lib/api/paginated-result'
import { jsonResponse, problemResponse, testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'
import { mockPrefersDark } from '@/test/match-media'

const today = toLocalDateString(new Date())

const patient: PatientDto = {
  id: '7b2e0000-0000-0000-0000-000000000001',
  documentType: 'NationalId',
  documentNumber: '0102030401',
  firstName: 'Ana',
  lastName: 'Pérez',
  birthDate: '1990-05-14',
  gender: 'Female',
  email: null,
  phone: null,
  address: null,
  bloodType: null,
  allergies: null,
  isActive: true,
  createdAtUtc: '2026-10-27T15:00:00Z',
}

const doctor: DoctorDto = {
  id: '3c1a0000-0000-0000-0000-000000000001',
  firstName: 'Luis',
  lastName: 'Andrade',
  licenseNumber: 'MG-0001',
  specialty: 'Medicina general',
  email: 'luis@example.com',
  phone: null,
  appointmentDurationMinutes: 30,
  isActive: true,
  userId: null,
  createdAtUtc: '2026-10-27T15:00:00Z',
}

function makeAppointment(
  index: number,
  overrides: Partial<AppointmentDto> = {},
): AppointmentDto {
  return {
    id: `0193f1d0-0000-0000-0000-${String(index).padStart(12, '0')}`,
    patientId: patient.id,
    patientFullName: 'Ana Pérez',
    doctorId: doctor.id,
    doctorFullName: 'Luis Andrade',
    startUtc: '2026-11-03T14:30:00Z',
    endUtc: '2026-11-03T15:00:00Z',
    startLocal: '2026-11-03T09:30:00',
    endLocal: '2026-11-03T10:00:00',
    reason: `Control ${index}`,
    status: 'Scheduled',
    cancellationReason: null,
    createdAtUtc: '2026-10-27T15:02:11Z',
    ...overrides,
  }
}

const first = makeAppointment(1, { reason: 'Control de presión arterial' })
const second = makeAppointment(2, {
  patientFullName: 'Pedro Mora',
  doctorFullName: 'Marta Vera',
  startLocal: '2026-11-04T11:00:00',
  endLocal: '2026-11-04T11:20:00',
  status: 'Confirmed',
})

function pageOf(
  data: AppointmentDto[],
  count = data.length,
  pageIndex = 0,
): PaginatedResult<AppointmentDto> {
  return { pageIndex, pageSize: 10, count, data }
}

interface Request {
  url: URL
  method: string
  body: unknown
}

let requests: Request[]
let listResponse: (url: URL) => Response
let createResponse: () => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  listResponse = () => jsonResponse(pageOf([first, second]))
  createResponse = () => jsonResponse({ id: 'new' }, 201)
  const fetchMock: Mock<typeof fetch> = vi.fn(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    const { pathname } = request.url
    if (pathname === '/api/appointments') {
      return request.method === 'POST'
        ? createResponse()
        : listResponse(request.url)
    }
    if (pathname.endsWith('/available-slots')) {
      const date = request.url.searchParams.get('date')!
      return jsonResponse({
        doctorId: doctor.id,
        date,
        durationMinutes: 30,
        slots: [
          { startLocal: `${date}T09:00:00`, endLocal: `${date}T09:30:00` },
        ],
      })
    }
    if (pathname === '/api/doctors') {
      return jsonResponse({
        pageIndex: 0,
        pageSize: 10,
        count: 1,
        data: [doctor],
      })
    }
    if (pathname === '/api/patients') {
      return jsonResponse({
        pageIndex: 0,
        pageSize: 10,
        count: 1,
        data: [patient],
      })
    }
    return problemResponse(404)
  })
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

function renderPage(
  currentUser: CurrentUserDto = { ...testUser, role: 'Receptionist' },
) {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <MemoryRouter initialEntries={['/appointments']}>
        <TestAuthProvider user={currentUser}>
          <TooltipProvider>
            <AppointmentsPage />
          </TooltipProvider>
          <Toaster />
        </TestAuthProvider>
      </MemoryRouter>
    </ThemeProvider>,
  )
  return user
}

const listRequests = () =>
  requests.filter(
    (request) =>
      request.method === 'GET' && request.url.pathname === '/api/appointments',
  )
const lastListParams = () => listRequests().at(-1)!.url.searchParams

async function choose(user: UserEvent, combobox: string, option: RegExp) {
  await user.click(screen.getByRole('combobox', { name: combobox }))
  await user.click(await screen.findByRole('option', { name: option }))
}

describe('AppointmentsPage', () => {
  it('loads the appointments from today and shows them', async () => {
    renderPage()

    expect(screen.getAllByTestId('appointment-row-skeleton')).toHaveLength(5)
    const row = within((await screen.findByText('03/11/2026')).closest('tr')!)

    expect(lastListParams().toString()).toBe(
      `pageIndex=0&pageSize=10&from=${today}`,
    )
    expect(row.getByText('09:30 – 10:00')).toBeInTheDocument()
    expect(row.getByText('Ana Pérez')).toBeInTheDocument()
    expect(row.getByText('Luis Andrade')).toBeInTheDocument()
    expect(row.getByText('Control de presión arterial')).toHaveAttribute(
      'title',
      'Control de presión arterial',
    )
    expect(row.getByText('Agendada')).toBeInTheDocument()

    const secondRow = within(screen.getByText('04/11/2026').closest('tr')!)
    expect(secondRow.getByText('11:00 – 11:20')).toBeInTheDocument()
    expect(secondRow.getByText('Confirmada')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1 · 2 citas')).toBeInTheDocument()
    expect(screen.getByLabelText('Desde')).toHaveValue(today)
  })

  it('omits from when Desde is cleared', async () => {
    renderPage()
    await screen.findByText('03/11/2026')

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '' } })

    await waitFor(() => expect(lastListParams().has('from')).toBe(false))
  })

  it('does not query when Hasta is before Desde', async () => {
    renderPage()
    await screen.findByText('03/11/2026')
    const before = listRequests().length

    fireEvent.change(screen.getByLabelText('Desde'), {
      target: { value: '2026-11-10' },
    })
    await waitFor(() => expect(listRequests()).toHaveLength(before + 1))
    fireEvent.change(screen.getByLabelText('Hasta'), {
      target: { value: '2026-11-01' },
    })

    expect(
      await screen.findByText(
        'La fecha final no puede ser anterior a la inicial.',
      ),
    ).toBeInTheDocument()
    expect(listRequests()).toHaveLength(before + 1)
    expect(screen.getByText('03/11/2026')).toBeInTheDocument()
  })

  it('filters by status', async () => {
    const user = renderPage()
    await screen.findByText('03/11/2026')

    await user.click(screen.getByLabelText('Filtrar por estado'))
    await user.click(await screen.findByRole('option', { name: 'Confirmada' }))

    await waitFor(() =>
      expect(lastListParams().get('status')).toBe('Confirmed'),
    )
  })

  it('filters by doctor and patient going back to the first page', async () => {
    listResponse = (url) =>
      jsonResponse(
        pageOf([first], 15, Number(url.searchParams.get('pageIndex'))),
      )
    const user = renderPage()
    await screen.findByText('03/11/2026')
    await user.click(screen.getByRole('button', { name: /Siguiente/ }))
    await waitFor(() => expect(lastListParams().get('pageIndex')).toBe('1'))

    await choose(user, 'Filtrar por doctor', /Luis Andrade/)

    await waitFor(() =>
      expect(lastListParams().get('doctorId')).toBe(doctor.id),
    )
    expect(lastListParams().get('pageIndex')).toBe('0')
    const doctorSearch = requests.find(
      (request) => request.url.pathname === '/api/doctors',
    )!
    expect(doctorSearch.url.searchParams.get('isActive')).toBe('true')

    await choose(user, 'Filtrar por paciente', /Ana Pérez/)

    await waitFor(() =>
      expect(lastListParams().get('patientId')).toBe(patient.id),
    )
  })

  it('clears the filters back to the defaults', async () => {
    const user = renderPage()
    await screen.findByText('03/11/2026')
    const clear = screen.getByRole('button', { name: 'Limpiar filtros' })
    expect(clear).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Hasta'), {
      target: { value: '2026-12-31' },
    })
    await choose(user, 'Filtrar por doctor', /Luis Andrade/)
    await waitFor(() => expect(lastListParams().has('doctorId')).toBe(true))

    await user.click(clear)

    await waitFor(() =>
      expect(lastListParams().toString()).toBe(
        `pageIndex=0&pageSize=10&from=${today}`,
      ),
    )
    expect(screen.getByLabelText('Hasta')).toHaveValue('')
    expect(clear).toBeDisabled()
  })

  it('moves between pages and disables the buttons at the ends', async () => {
    listResponse = (url) => {
      const pageIndex = Number(url.searchParams.get('pageIndex'))
      return jsonResponse(
        pageOf([makeAppointment(pageIndex + 10)], 12, pageIndex),
      )
    }
    const user = renderPage()

    expect(
      await screen.findByText('Página 1 de 2 · 12 citas'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Anterior/ })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /Siguiente/ }))

    expect(
      await screen.findByText('Página 2 de 2 · 12 citas'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Siguiente/ })).toBeDisabled()
  })

  it('shows the empty states', async () => {
    listResponse = () => jsonResponse(pageOf([]))
    const user = renderPage()

    expect(
      await screen.findByText('No hay citas próximas.'),
    ).toBeInTheDocument()

    await user.click(screen.getByLabelText('Filtrar por estado'))
    await user.click(await screen.findByRole('option', { name: 'Cancelada' }))

    expect(
      await screen.findByText('No se encontraron citas con esos filtros.'),
    ).toBeInTheDocument()
  })

  it('shows the load error and retries the query', async () => {
    listResponse = () => problemResponse(500)
    const user = renderPage()

    expect(
      await screen.findByText('No se pudieron cargar las citas.'),
    ).toBeInTheDocument()

    listResponse = () => jsonResponse(pageOf([first]))
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('03/11/2026')).toBeInTheDocument()
  })

  it('schedules an appointment and refreshes the list', async () => {
    const user = renderPage()
    await screen.findByText('03/11/2026')

    await user.click(screen.getByRole('button', { name: 'Nueva cita' }))
    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Nueva cita')).toBeInTheDocument()
    await choose(user, 'Paciente', /Ana Pérez/)
    await choose(user, 'Doctor', /Luis Andrade/)
    await user.click(
      await dialog.findByRole('button', { name: '09:00 a 09:30' }),
    )
    await user.type(dialog.getByLabelText('Motivo'), 'Chequeo anual')
    const before = listRequests().length

    await user.click(dialog.getByRole('button', { name: 'Agendar cita' }))

    expect(await screen.findByText('Cita agendada.')).toBeInTheDocument()
    const post = requests.find((request) => request.method === 'POST')!
    expect(post.body).toEqual({
      patientId: patient.id,
      doctorId: doctor.id,
      startLocal: `${today}T09:00`,
      reason: 'Chequeo anual',
    })
    await waitFor(() => expect(listRequests()).toHaveLength(before + 1))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('does not show the start action to Admin', async () => {
    listResponse = () =>
      jsonResponse(
        pageOf([
          makeAppointment(3, {
            startLocal: `${today}T09:30:00`,
            endLocal: `${today}T10:00:00`,
          }),
        ]),
      )
    renderPage({ ...testUser, role: 'Admin' })

    await screen.findByText('09:30 – 10:00')

    expect(
      screen.queryByRole('button', { name: /Iniciar consulta/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Doctor' })).toBeVisible()
  })
})

describe('AppointmentsPage for a Doctor', () => {
  const linkedDoctor: CurrentUserDto = {
    ...testUser,
    role: 'Doctor',
    doctorId: doctor.id,
  }

  const startable = makeAppointment(10, {
    patientFullName: 'Ana Pérez',
    startLocal: `${today}T09:30:00`,
    endLocal: `${today}T10:00:00`,
    reason: 'Control de presión arterial',
  })
  const completed = makeAppointment(11, {
    patientFullName: 'Pedro Mora',
    startLocal: `${today}T10:00:00`,
    endLocal: `${today}T10:30:00`,
    status: 'Completed',
  })
  const otherDay = makeAppointment(12, {
    patientFullName: 'Rosa Vela',
    startLocal: '2099-01-05T09:00:00',
    endLocal: '2099-01-05T09:30:00',
  })

  const rowOf = (patientName: string) =>
    screen.getByText(patientName).closest('tr')!

  it('shows the notice without requests when it is not linked', () => {
    renderPage({ ...testUser, role: 'Doctor', doctorId: null })

    expect(
      screen.getByText(/Tu usuario todavía no está vinculado/),
    ).toBeInTheDocument()
    expect(requests).toHaveLength(0)
  })

  it('loads only today without doctorId nor the reception actions', async () => {
    listResponse = () => jsonResponse(pageOf([]))
    renderPage(linkedDoctor)

    expect(await screen.findByText('No tienes citas hoy.')).toBeInTheDocument()
    expect(lastListParams().toString()).toBe(
      `pageIndex=0&pageSize=10&from=${today}&to=${today}`,
    )
    expect(
      screen.queryByRole('button', { name: 'Nueva cita' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('combobox', { name: 'Filtrar por doctor' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('columnheader', { name: 'Doctor' }),
    ).not.toBeInTheDocument()
    expect(screen.getByLabelText('Hasta')).toHaveValue(today)
    expect(
      screen.getByRole('combobox', { name: 'Filtrar por paciente' }),
    ).toBeInTheDocument()
  })

  it('offers Iniciar only on the startable appointments', async () => {
    listResponse = () => jsonResponse(pageOf([startable, completed, otherDay]))
    renderPage(linkedDoctor)
    await screen.findByText('Ana Pérez')

    expect(
      within(rowOf('Ana Pérez')).getByRole('button', {
        name: 'Iniciar consulta de Ana Pérez',
      }),
    ).toBeInTheDocument()
    expect(
      within(rowOf('Pedro Mora')).queryByRole('button'),
    ).not.toBeInTheDocument()
    expect(
      within(rowOf('Rosa Vela')).queryByRole('button'),
    ).not.toBeInTheDocument()
  })

  it('opens the confirmation from the row and from the button', async () => {
    listResponse = () => jsonResponse(pageOf([startable, completed]))
    const user = renderPage(linkedDoctor)
    await screen.findByText('Ana Pérez')

    await user.click(rowOf('Pedro Mora'))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()

    await user.click(rowOf('Ana Pérez'))
    const dialog = within(await screen.findByRole('alertdialog'))
    expect(dialog.getByText(/Se iniciará la consulta de/)).toHaveTextContent(
      'Control de presión arterial',
    )
    await user.click(dialog.getByRole('button', { name: 'Cancelar' }))
    await waitFor(() =>
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
    )

    await user.click(
      screen.getByRole('button', { name: 'Iniciar consulta de Ana Pérez' }),
    )
    expect(await screen.findAllByRole('alertdialog')).toHaveLength(1)
  })

  it('clears the filters back to today', async () => {
    renderPage(linkedDoctor)
    await screen.findByText('03/11/2026')
    const clear = screen.getByRole('button', { name: 'Limpiar filtros' })
    expect(clear).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '' } })
    await waitFor(() => expect(lastListParams().has('to')).toBe(false))

    await userEvent.setup().click(clear)

    await waitFor(() =>
      expect(lastListParams().toString()).toBe(
        `pageIndex=0&pageSize=10&from=${today}&to=${today}`,
      ),
    )
    expect(clear).toBeDisabled()
  })

  it('shows its own message on a 403', async () => {
    listResponse = () => problemResponse(403)
    renderPage(linkedDoctor)

    expect(
      await screen.findByText(
        'No se pudieron ver tus citas. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar.',
      ),
    ).toBeInTheDocument()
  })
})

import { render, screen, within } from '@testing-library/react'
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
import type { DoctorDto } from '@/features/doctors/doctor-types'
import { MySchedulePage } from '@/features/doctors/my-schedule-page'
import { jsonResponse, problemResponse, testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'
import { mockPrefersDark } from '@/test/match-media'

const doctor: DoctorDto = {
  id: '3c1a0000-0000-0000-0000-000000000001',
  firstName: 'Luis',
  lastName: 'Mora',
  licenseNumber: 'MG-0001',
  specialty: 'Medicina general',
  email: 'luis.mora@dispensario.local',
  phone: null,
  appointmentDurationMinutes: 30,
  isActive: true,
  userId: '0b6f0000-0000-0000-0000-000000000001',
  createdAtUtc: '2026-10-26T15:00:00Z',
}

const monday = { dayOfWeek: 'Monday', startTime: '08:00', endTime: '12:00' }

interface Request {
  url: URL
  method: string
  body: unknown
}

let fetchMock: Mock<typeof fetch>
let requests: Request[]
let doctorResponse: () => Response
let putResponse: () => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  doctorResponse = () => jsonResponse(doctor)
  putResponse = () => new Response(null, { status: 204 })
  fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    if (request.method === 'PUT') {
      return putResponse()
    }
    if (request.url.pathname.endsWith('/schedule')) {
      return jsonResponse({ doctorId: doctor.id, blocks: [monday] })
    }
    return doctorResponse()
  })
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

function renderPage(doctorId: string | null = doctor.id) {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <TestAuthProvider user={{ ...testUser, role: 'Doctor', doctorId }}>
        <MySchedulePage />
        <Toaster />
      </TestAuthProvider>
    </ThemeProvider>,
  )
  return user
}

describe('MySchedulePage', () => {
  it('tells an unlinked doctor without calling the API', () => {
    renderPage(null)

    expect(
      screen.getByText(
        'Tu usuario todavía no está vinculado a una ficha de doctor. Pide al administrador que lo vincule.',
      ),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('loads the doctor and shows the header and the schedule', async () => {
    renderPage()

    expect(screen.getByTestId('my-doctor-skeleton')).toBeInTheDocument()
    expect(await screen.findByText('08:00 – 12:00')).toBeInTheDocument()
    expect(screen.getByText('Luis Mora')).toBeInTheDocument()
    expect(
      screen.getByText(/Medicina general · Citas de 30 min/),
    ).toBeInTheDocument()
    expect(requests[0].url.pathname).toBe(`/api/doctors/${doctor.id}`)
    expect(requests[1].url.pathname).toBe(`/api/doctors/${doctor.id}/schedule`)
  })

  it('edits and saves the own schedule', async () => {
    const user = renderPage()
    await screen.findByText('08:00 – 12:00')

    await user.click(screen.getByRole('button', { name: 'Editar horario' }))
    await user.click(screen.getByRole('switch', { name: 'Atiende el martes' }))
    await user.click(screen.getByRole('button', { name: 'Guardar horario' }))

    expect(await screen.findByText('Horario actualizado.')).toBeInTheDocument()
    const put = requests.find((request) => request.method === 'PUT')!
    expect(put.url.pathname).toBe(`/api/doctors/${doctor.id}/schedule`)
    expect(put.body).toEqual({
      blocks: [
        monday,
        { dayOfWeek: 'Tuesday', startTime: '08:00', endTime: '12:00' },
      ],
    })
  })

  it('asks to log in again on a 403 when saving', async () => {
    putResponse = () => problemResponse(403)
    const user = renderPage()
    await screen.findByText('08:00 – 12:00')

    await user.click(screen.getByRole('button', { name: 'Editar horario' }))
    await user.click(screen.getByRole('button', { name: 'Guardar horario' }))

    expect(
      await screen.findByText(
        'No puedes editar este horario. Si tu usuario se vinculó hace poco, cierra sesión y vuelve a entrar.',
      ),
    ).toBeInTheDocument()
  })

  it('tells an inactive doctor that the schedule cannot be edited', async () => {
    doctorResponse = () => jsonResponse({ ...doctor, isActive: false })
    renderPage()
    await screen.findByText('08:00 – 12:00')

    expect(
      screen.getByText(
        'Tu ficha de doctor está inactiva: pide al administrador que la active para editar tu horario.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Editar horario' }),
    ).not.toBeInTheDocument()
  })

  it('tells when the doctor record does not exist', async () => {
    doctorResponse = () => problemResponse(404)
    renderPage()

    expect(
      await screen.findByText(
        'No se encontró tu ficha de doctor. Contacta con el administrador.',
      ),
    ).toBeInTheDocument()
  })

  it('retries after a load error', async () => {
    let failures = 1
    doctorResponse = () =>
      failures-- > 0 ? problemResponse(500) : jsonResponse(doctor)
    const user = renderPage()

    const alert = await screen.findByRole('alert')
    expect(
      within(alert).getByText(/No se pudo cargar tu ficha de doctor\./),
    ).toBeInTheDocument()
    await user.click(within(alert).getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('08:00 – 12:00')).toBeInTheDocument()
  })
})

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

import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/ui/sonner'
import type { AppointmentDto } from '@/features/appointments/appointment-types'
import { StartConsultationDialog } from '@/features/appointments/components/start-consultation-dialog'
import type { ConsultationDto } from '@/features/consultations/consultation-types'
import { jsonResponse, problemResponse } from '@/test/api-responses'
import { mockPrefersDark } from '@/test/match-media'

const appointment: AppointmentDto = {
  id: '0193f1d0-0000-0000-0000-000000000001',
  patientId: '7b2e0000-0000-0000-0000-000000000001',
  patientFullName: 'Ana Pérez',
  doctorId: '3c1a0000-0000-0000-0000-000000000001',
  doctorFullName: 'Luis Andrade',
  startUtc: '2026-11-03T14:30:00Z',
  endUtc: '2026-11-03T15:00:00Z',
  startLocal: '2026-11-03T09:30:00',
  endLocal: '2026-11-03T10:00:00',
  reason: 'Control de presión arterial',
  status: 'Scheduled',
  cancellationReason: null,
  createdAtUtc: '2026-10-27T15:02:11Z',
}

function makeConsultation(
  id: string,
  appointmentId: string | null,
): ConsultationDto {
  return {
    id,
    patientId: appointment.patientId,
    patientFullName: 'Ana Pérez',
    doctorId: appointment.doctorId,
    doctorFullName: 'Luis Andrade',
    appointmentId,
    reason: 'Control',
    diagnosis: null,
    notes: null,
    status: 'Open',
    startedAtUtc: '2026-11-03T14:32:10Z',
    closedAtUtc: null,
    startedAtLocal: '2026-11-03T09:32:10',
    closedAtLocal: null,
    createdAtUtc: '2026-11-03T14:32:10Z',
  }
}

interface Request {
  url: URL
  method: string
  body: unknown
}

let requests: Request[]
let startResponse: () => Response | Promise<Response>
let historyResponse: () => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  startResponse = () => jsonResponse({ id: 'new-consultation' }, 201)
  historyResponse = () =>
    jsonResponse({ pageIndex: 0, pageSize: 100, count: 0, data: [] })
  const fetchMock: Mock<typeof fetch> = vi.fn(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    if (request.url.pathname === '/api/consultations') {
      return startResponse()
    }
    if (request.url.pathname.endsWith('/consultations')) {
      return historyResponse()
    }
    return problemResponse(404)
  })
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

function LocationDisplay() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname}</div>
}

function renderDialog() {
  const onClose = vi.fn()
  const onFailed = vi.fn()
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <MemoryRouter initialEntries={['/appointments']}>
        <StartConsultationDialog
          appointment={appointment}
          onClose={onClose}
          onFailed={onFailed}
        />
        <LocationDisplay />
      </MemoryRouter>
      <Toaster />
    </ThemeProvider>,
  )
  return { user, onClose, onFailed }
}

const currentLocation = () => screen.getByTestId('location').textContent
const confirmButton = () =>
  screen.getByRole('button', { name: 'Iniciar consulta' })

describe('StartConsultationDialog', () => {
  it('shows the patient, the time range and the reason', () => {
    renderDialog()

    expect(
      screen.getByRole('heading', { name: 'Iniciar consulta' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/Se iniciará la consulta de/)).toHaveTextContent(
      'Se iniciará la consulta de Ana Pérez de las 09:30 – 10:00 con el motivo de la cita: “Control de presión arterial”.',
    )
  })

  it('starts the consultation and goes to its page', async () => {
    let resolve: (response: Response) => void = () => {}
    startResponse = () =>
      new Promise<Response>((done) => {
        resolve = done
      })
    const { user } = renderDialog()

    await user.click(confirmButton())

    expect(
      await screen.findByRole('button', { name: 'Iniciando…' }),
    ).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
    resolve(jsonResponse({ id: 'new-consultation' }, 201))

    await waitFor(() =>
      expect(currentLocation()).toBe('/consultations/new-consultation'),
    )
    expect(requests[0].method).toBe('POST')
    expect(requests[0].body).toEqual({
      patientId: appointment.patientId,
      appointmentId: appointment.id,
      reason: 'Control de presión arterial',
    })
  })

  it('opens the existing consultation on a 409', async () => {
    startResponse = () => problemResponse(409)
    historyResponse = () =>
      jsonResponse({
        pageIndex: 0,
        pageSize: 100,
        count: 2,
        data: [
          makeConsultation('other', null),
          makeConsultation('existing', appointment.id),
        ],
      })
    const { user, onFailed } = renderDialog()

    await user.click(confirmButton())

    await waitFor(() =>
      expect(currentLocation()).toBe('/consultations/existing'),
    )
    const historyRequest = requests[1]
    expect(historyRequest.url.pathname).toBe(
      `/api/patients/${appointment.patientId}/consultations`,
    )
    expect(historyRequest.url.searchParams.toString()).toBe(
      'pageIndex=0&pageSize=100',
    )
    expect(onFailed).not.toHaveBeenCalled()
  })

  it('warns when the existing consultation is not found', async () => {
    startResponse = () => problemResponse(409)
    const { user, onClose, onFailed } = renderDialog()

    await user.click(confirmButton())

    expect(
      await screen.findByText(
        'La cita ya tiene una consulta, pero no se pudo abrir.',
      ),
    ).toBeInTheDocument()
    expect(onClose).toHaveBeenCalled()
    expect(onFailed).toHaveBeenCalled()
    expect(currentLocation()).toBe('/appointments')
  })

  it('shows the message of a 400', async () => {
    startResponse = () => problemResponse(400)
    const { user, onFailed } = renderDialog()

    await user.click(confirmButton())

    expect(
      await screen.findByText(
        'No se puede iniciar la consulta: la cita ya no está pendiente, no es de hoy o el paciente está inactivo.',
      ),
    ).toBeInTheDocument()
    expect(onFailed).toHaveBeenCalled()
  })

  it('shows the network message', async () => {
    startResponse = () => Promise.reject(new TypeError('Failed to fetch'))
    const { user, onFailed } = renderDialog()

    await user.click(confirmButton())

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
    expect(onFailed).toHaveBeenCalled()
  })

  it('closes with Cancelar without requests', async () => {
    const { user, onClose } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onClose).toHaveBeenCalled()
    expect(requests).toHaveLength(0)
  })
})

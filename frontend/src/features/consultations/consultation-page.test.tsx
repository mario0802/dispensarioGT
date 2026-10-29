import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
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
import type { CurrentUserDto } from '@/features/auth/auth-types'
import { ConsultationPage } from '@/features/consultations/consultation-page'
import type { ConsultationDto } from '@/features/consultations/consultation-types'
import type { PatientDto } from '@/features/patients/patient-types'
import { jsonResponse, problemResponse, testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'
import { mockPrefersDark } from '@/test/match-media'

const doctorId = '0193f1b8-0000-0000-0000-000000000001'
const consultationId = '0193f2e0-0000-0000-0000-000000000001'

const doctorUser: CurrentUserDto = { ...testUser, role: 'Doctor', doctorId }

const patient: PatientDto = {
  id: '0193f1c2-0000-0000-0000-000000000001',
  documentType: 'NationalId',
  documentNumber: '0102030405',
  firstName: 'Ana',
  lastName: 'Pérez',
  birthDate: '1990-05-14',
  gender: 'Female',
  email: null,
  phone: null,
  address: null,
  bloodType: 'O+',
  allergies: 'Penicilina',
  isActive: true,
  createdAtUtc: '2026-10-27T15:00:00Z',
}

const openConsultation: ConsultationDto = {
  id: consultationId,
  patientId: patient.id,
  patientFullName: 'Ana Pérez',
  doctorId,
  doctorFullName: 'Luis Andrade',
  appointmentId: '0193f1d0-0000-0000-0000-000000000001',
  reason: 'Dolor de cabeza',
  diagnosis: null,
  notes: null,
  status: 'Open',
  startedAtUtc: '2026-11-03T14:32:10Z',
  closedAtUtc: null,
  startedAtLocal: '2026-11-03T09:32:10',
  closedAtLocal: null,
  createdAtUtc: '2026-11-03T14:32:10Z',
}

interface Request {
  url: URL
  method: string
  body: unknown
}

let requests: Request[]
let consultationResponse: () => Response
let updateResponse: () => Response
let closeResponse: () => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  consultationResponse = () => jsonResponse(openConsultation)
  updateResponse = () => new Response(null, { status: 204 })
  closeResponse = () => new Response(null, { status: 204 })
  const fetchMock: Mock<typeof fetch> = vi.fn(async (input, init) => {
    const request: Request = {
      url: new URL(String(input)),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    }
    requests.push(request)
    const { pathname } = request.url
    if (pathname === `/api/consultations/${consultationId}`) {
      return request.method === 'PUT'
        ? updateResponse()
        : consultationResponse()
    }
    if (pathname === `/api/consultations/${consultationId}/close`) {
      return closeResponse()
    }
    if (pathname === `/api/patients/${patient.id}`) {
      return jsonResponse(patient)
    }
    if (pathname === `/api/patients/${patient.id}/consultations`) {
      return jsonResponse({
        pageIndex: 0,
        pageSize: 5,
        count: 1,
        data: [openConsultation],
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

function renderPage(currentUser: CurrentUserDto = doctorUser) {
  const user = userEvent.setup()
  render(
    <ThemeProvider>
      <MemoryRouter initialEntries={[`/consultations/${consultationId}`]}>
        <TestAuthProvider user={currentUser}>
          <Routes>
            <Route
              path="/consultations/:consultationId"
              element={<ConsultationPage />}
            />
            <Route path="/appointments" element={<p>Página de citas</p>} />
          </Routes>
          <Toaster />
        </TestAuthProvider>
      </MemoryRouter>
    </ThemeProvider>,
  )
  return user
}

const writes = () => requests.filter((request) => request.method !== 'GET')
const consultationLoads = () =>
  requests.filter(
    (request) =>
      request.method === 'GET' &&
      request.url.pathname === `/api/consultations/${consultationId}`,
  )

describe('ConsultationPage', () => {
  it('loads the consultation and the patient and shows both cards and the history', async () => {
    renderPage()

    expect(
      await screen.findByRole('heading', { name: 'Consulta de Ana Pérez' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Cédula 0102030405')).toBeInTheDocument()
    expect(screen.getByText(/· 14\/05\/1990/)).toBeInTheDocument()
    expect(screen.getByText('Femenino')).toBeInTheDocument()
    expect(screen.getByText('O+')).toBeInTheDocument()
    // Teléfono nulo
    expect(screen.getByText('—')).toBeInTheDocument()
    const allergies = screen
      .getAllByRole('alert')
      .find((alert) => alert.textContent?.includes('Penicilina'))
    expect(allergies).toHaveTextContent('Alergias')

    expect(screen.getByText('03/11/2026 09:32')).toBeInTheDocument()
    expect(screen.getByText('Abierta')).toBeInTheDocument()
    expect(screen.getByText('Luis Andrade')).toBeInTheDocument()
    expect(screen.getByLabelText('Motivo')).toHaveValue('Dolor de cabeza')
    expect(
      await screen.findByText('El paciente no tiene consultas previas.'),
    ).toBeInTheDocument()
  })

  it('saves the changes', async () => {
    const user = renderPage()

    await user.type(await screen.findByLabelText('Diagnóstico'), 'Cefalea')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Cambios guardados.')).toBeInTheDocument()
    expect(writes()).toEqual([
      expect.objectContaining({
        method: 'PUT',
        body: { reason: 'Dolor de cabeza', diagnosis: 'Cefalea', notes: null },
      }),
    ])
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled()
  })

  it('finishes an already saved consultation with only the close request', async () => {
    consultationResponse = () =>
      jsonResponse({ ...openConsultation, diagnosis: 'Cefalea' })
    const user = renderPage()

    await user.click(
      await screen.findByRole('button', { name: 'Finalizar consulta' }),
    )
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Finalizar',
      }),
    )

    expect(await screen.findByText('Página de citas')).toBeInTheDocument()
    expect(await screen.findByText('Consulta finalizada.')).toBeInTheDocument()
    expect(writes()).toEqual([
      expect.objectContaining({
        method: 'POST',
        url: expect.objectContaining({
          pathname: `/api/consultations/${consultationId}/close`,
        }),
        body: { diagnosis: 'Cefalea', notes: null },
      }),
    ])
  })

  it('saves the pending changes before closing', async () => {
    const user = renderPage()

    await user.type(await screen.findByLabelText('Diagnóstico'), 'Cefalea')
    await user.type(screen.getByLabelText('Observaciones'), 'Control')
    await user.click(screen.getByRole('button', { name: 'Finalizar consulta' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Finalizar',
      }),
    )

    expect(await screen.findByText('Página de citas')).toBeInTheDocument()
    expect(writes().map((request) => request.method)).toEqual(['PUT', 'POST'])
    expect(writes()[0].body).toEqual({
      reason: 'Dolor de cabeza',
      diagnosis: 'Cefalea',
      notes: 'Control',
    })
    expect(writes()[1].body).toEqual({ diagnosis: 'Cefalea', notes: 'Control' })
  })

  it('reloads the consultation when closing finds it already closed', async () => {
    consultationResponse = () =>
      jsonResponse({ ...openConsultation, diagnosis: 'Cefalea' })
    closeResponse = () => problemResponse(400)
    const user = renderPage()

    await user.click(
      await screen.findByRole('button', { name: 'Finalizar consulta' }),
    )
    consultationResponse = () =>
      jsonResponse({
        ...openConsultation,
        diagnosis: 'Cefalea',
        status: 'Closed',
        closedAtLocal: '2026-11-03T09:50:00',
      })
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Finalizar',
      }),
    )

    expect(
      await screen.findByText('La consulta ya está cerrada.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(consultationLoads()).toHaveLength(2))
    expect(
      await screen.findByText(
        'Esta consulta está cerrada y no se puede editar.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText('Página de citas')).not.toBeInTheDocument()
  })

  it('shows a closed consultation as read-only', async () => {
    consultationResponse = () =>
      jsonResponse({
        ...openConsultation,
        status: 'Closed',
        diagnosis: 'Cefalea tensional',
        closedAtLocal: '2026-11-03T09:55:40',
      })
    renderPage()

    expect(
      await screen.findByText(
        'Esta consulta está cerrada y no se puede editar.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Cefalea tensional')).toBeInTheDocument()
    expect(screen.getByText('03/11/2026 09:55')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Guardar' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Finalizar consulta' }),
    ).not.toBeInTheDocument()
  })

  it("shows another doctor's consultation as read-only", async () => {
    consultationResponse = () =>
      jsonResponse({
        ...openConsultation,
        doctorId: 'another',
        doctorFullName: 'Marta Vera',
      })
    renderPage()

    expect(
      await screen.findByText(
        'Esta consulta es de Marta Vera: solo puedes verla.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Guardar' }),
    ).not.toBeInTheDocument()
  })

  it.each([
    [404, 'La consulta no existe.'],
    [
      403,
      'No se pudo ver la consulta. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar.',
    ],
  ])('shows the message of a %i without retry', async (status, message) => {
    consultationResponse = () => problemResponse(status)
    renderPage()

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Reintentar' }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Volver a citas' }),
    ).toBeInTheDocument()
  })

  it('retries after a network error', async () => {
    consultationResponse = () => {
      throw new TypeError('Failed to fetch')
    }
    const user = renderPage()

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
    consultationResponse = () => jsonResponse(openConsultation)
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(
      await screen.findByRole('heading', { name: 'Consulta de Ana Pérez' }),
    ).toBeInTheDocument()
  })

  it('warns an unlinked doctor without calling the API', () => {
    renderPage({ ...doctorUser, doctorId: null })

    expect(
      screen.getByText(/no está vinculado a una ficha de doctor/),
    ).toBeInTheDocument()
    expect(requests).toHaveLength(0)
  })

  it('goes back directly without changes', async () => {
    const user = renderPage()

    await screen.findByLabelText('Motivo')
    await user.click(screen.getByRole('button', { name: 'Volver a citas' }))

    expect(await screen.findByText('Página de citas')).toBeInTheDocument()
  })

  it('asks before discarding the changes when going back', async () => {
    const user = renderPage()

    await user.type(await screen.findByLabelText('Diagnóstico'), 'Cefalea')
    await user.click(screen.getByRole('button', { name: 'Volver a citas' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent('¿Descartar los cambios?')
    expect(dialog).toHaveTextContent(
      'Los cambios de la consulta no se guardarán.',
    )
    await user.click(within(dialog).getByRole('button', { name: 'Descartar' }))

    expect(await screen.findByText('Página de citas')).toBeInTheDocument()
  })

  it('makes the browser warn on unload only with unsaved changes', async () => {
    const user = renderPage()
    const unload = () => {
      const event = new Event('beforeunload', { cancelable: true })
      window.dispatchEvent(event)
      return event.defaultPrevented
    }

    await screen.findByLabelText('Motivo')
    expect(unload()).toBe(false)
    await user.type(screen.getByLabelText('Diagnóstico'), 'Cefalea')
    expect(unload()).toBe(true)
  })
})

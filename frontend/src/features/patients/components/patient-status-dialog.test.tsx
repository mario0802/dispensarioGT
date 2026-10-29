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

import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/ui/sonner'
import { PatientStatusDialog } from '@/features/patients/components/patient-status-dialog'
import type { PatientDto } from '@/features/patients/patient-types'
import { problemResponse } from '@/test/api-responses'
import { mockPrefersDark } from '@/test/match-media'

const patient: PatientDto = {
  id: '7b2e0000-0000-0000-0000-000000000001',
  documentType: 'NationalId',
  documentNumber: '0102030405',
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

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  mockPrefersDark(false)
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

function renderDialog(target: PatientDto = patient) {
  const user = userEvent.setup()
  const onClose = vi.fn()
  const onDone = vi.fn()
  render(
    <ThemeProvider>
      <PatientStatusDialog patient={target} onClose={onClose} onDone={onDone} />
      <Toaster />
    </ThemeProvider>,
  )
  return { user, onClose, onDone }
}

const requestedUrl = () => String(fetchMock.mock.calls[0][0])
const requestedMethod = () => fetchMock.mock.calls[0][1]?.method

describe('PatientStatusDialog', () => {
  it('asks to confirm the deactivation', () => {
    renderDialog()

    const dialog = screen.getByRole('alertdialog')
    expect(dialog).toHaveTextContent('¿Desactivar a Ana Pérez?')
    expect(dialog).toHaveTextContent(
      'No se le podrán agendar citas nuevas. Podrás activarlo de nuevo.',
    )
  })

  it('does not call the API on Cancelar', async () => {
    const { user, onClose } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onClose).toHaveBeenCalled()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('deactivates, shows the toast and calls onDone', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    const { user, onClose, onDone } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(await screen.findByText('Paciente desactivado.')).toBeInTheDocument()
    expect(requestedUrl()).toMatch(/\/api\/patients\/7b2e[^/]*\/deactivate$/)
    expect(requestedMethod()).toBe('POST')
    expect(onClose).toHaveBeenCalled()
    expect(onDone).toHaveBeenCalled()
  })

  it('reports a patient that was already inactive', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(400))
    const { user, onDone } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(
      await screen.findByText('El paciente ya estaba inactivo.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('reports a patient that no longer exists', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(404))
    const { user } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(
      await screen.findByText('El paciente ya no existe.'),
    ).toBeInTheDocument()
  })

  it('activates an inactive patient', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    const { user } = renderDialog({ ...patient, isActive: false })

    expect(screen.getByRole('alertdialog')).toHaveTextContent(
      '¿Activar a Ana Pérez?',
    )
    await user.click(screen.getByRole('button', { name: 'Activar' }))

    expect(await screen.findByText('Paciente activado.')).toBeInTheDocument()
    expect(requestedUrl()).toMatch(/\/activate$/)
  })
})

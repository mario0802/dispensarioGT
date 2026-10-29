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
import { DoctorStatusDialog } from '@/features/doctors/components/doctor-status-dialog'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import { problemResponse } from '@/test/api-responses'
import { mockPrefersDark } from '@/test/match-media'

const doctor: DoctorDto = {
  id: '3c1a0000-0000-0000-0000-000000000001',
  firstName: 'Luis',
  lastName: 'Mora',
  licenseNumber: 'MP-12345',
  specialty: 'Pediatría',
  email: 'luis.mora@dispensario.local',
  phone: null,
  appointmentDurationMinutes: 30,
  isActive: true,
  userId: null,
  createdAtUtc: '2026-10-26T15:00:00Z',
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

function renderDialog(target: DoctorDto = doctor) {
  const user = userEvent.setup()
  const onClose = vi.fn()
  const onDone = vi.fn()
  render(
    <ThemeProvider>
      <DoctorStatusDialog doctor={target} onClose={onClose} onDone={onDone} />
      <Toaster />
    </ThemeProvider>,
  )
  return { user, onClose, onDone }
}

const requestedUrl = () => String(fetchMock.mock.calls[0][0])
const requestedMethod = () => fetchMock.mock.calls[0][1]?.method

describe('DoctorStatusDialog', () => {
  it('asks to confirm the deactivation', () => {
    renderDialog()

    const dialog = screen.getByRole('alertdialog')
    expect(dialog).toHaveTextContent('¿Desactivar a Luis Mora?')
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

    expect(await screen.findByText('Doctor desactivado.')).toBeInTheDocument()
    expect(requestedUrl()).toMatch(/\/api\/doctors\/3c1a[^/]*\/deactivate$/)
    expect(requestedMethod()).toBe('POST')
    expect(onClose).toHaveBeenCalled()
    expect(onDone).toHaveBeenCalled()
  })

  it('reports a doctor that was already inactive', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(400))
    const { user, onDone } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(
      await screen.findByText('El doctor ya estaba inactivo.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('reports a doctor that no longer exists', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(404))
    const { user } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(
      await screen.findByText('El doctor ya no existe.'),
    ).toBeInTheDocument()
  })

  it('activates an inactive doctor', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    const { user } = renderDialog({ ...doctor, isActive: false })

    expect(screen.getByRole('alertdialog')).toHaveTextContent(
      '¿Activar a Luis Mora?',
    )
    await user.click(screen.getByRole('button', { name: 'Activar' }))

    expect(await screen.findByText('Doctor activado.')).toBeInTheDocument()
    expect(requestedUrl()).toMatch(/\/activate$/)
  })
})

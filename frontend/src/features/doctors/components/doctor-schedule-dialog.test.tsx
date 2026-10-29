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
import type { UserRole } from '@/features/auth/auth-types'
import { DoctorScheduleDialog } from '@/features/doctors/components/doctor-schedule-dialog'
import type { ScheduleBlockDto } from '@/features/doctors/doctor-schedule-types'
import type { DoctorDto } from '@/features/doctors/doctor-types'
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
  userId: null,
  createdAtUtc: '2026-10-26T15:00:00Z',
}

const schedulePath = `/api/doctors/${doctor.id}/schedule`

const mondayBlocks: ScheduleBlockDto[] = [
  { dayOfWeek: 'Monday', startTime: '08:00', endTime: '12:00' },
  { dayOfWeek: 'Monday', startTime: '14:00', endTime: '18:00' },
]

interface Request {
  url: URL
  method: string
  body: unknown
}

let fetchMock: Mock<typeof fetch>
let requests: Request[]
let scheduleResponses: (() => Response)[]
let putResponse: () => Response

beforeEach(() => {
  mockPrefersDark(false)
  requests = []
  scheduleResponses = []
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
    // Cada GET usa la siguiente respuesta; la última se repite
    const next =
      scheduleResponses.length > 1
        ? scheduleResponses.shift()!
        : scheduleResponses[0]
    return next()
  })
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

function respondWith(...blocksList: ScheduleBlockDto[][]) {
  scheduleResponses = blocksList.map(
    (blocks) => () => jsonResponse({ doctorId: doctor.id, blocks }),
  )
}

function renderDialog({
  role = 'Admin',
  target = doctor,
}: { role?: UserRole; target?: DoctorDto } = {}) {
  const user = userEvent.setup()
  const onClose = vi.fn()
  const onNotFound = vi.fn()
  render(
    <ThemeProvider>
      <TestAuthProvider user={{ ...testUser, role }}>
        <DoctorScheduleDialog
          doctor={target}
          onClose={onClose}
          onNotFound={onNotFound}
        />
        <Toaster />
      </TestAuthProvider>
    </ThemeProvider>,
  )
  return { user, onClose, onNotFound }
}

const dialog = () => within(screen.getByRole('dialog'))

const scheduleRows = async () =>
  within(
    await screen.findByRole('list', { name: 'Horario semanal' }),
  ).getAllByRole('listitem')

describe('DoctorScheduleDialog', () => {
  it('loads the schedule and shows the seven days', async () => {
    respondWith(mondayBlocks)
    renderDialog({ role: 'Receptionist' })

    expect(screen.getByTestId('schedule-skeleton')).toBeInTheDocument()
    const rows = await scheduleRows()

    expect(rows).toHaveLength(7)
    expect(rows[0]).toHaveTextContent('Lunes')
    expect(rows[0]).toHaveTextContent('08:00 – 12:00')
    expect(rows[0]).toHaveTextContent('14:00 – 18:00')
    expect(rows[1]).toHaveTextContent('MartesNo atiende')
    expect(rows[6]).toHaveTextContent('DomingoNo atiende')
    expect(requests[0].url.pathname).toBe(schedulePath)
    // Recepción solo ve
    expect(
      dialog().queryByRole('button', { name: 'Editar horario' }),
    ).not.toBeInTheDocument()
  })

  it('tells when the doctor has no schedule', async () => {
    respondWith([])
    renderDialog()

    expect(
      await screen.findByText('Este doctor todavía no tiene horario.'),
    ).toBeInTheDocument()
  })

  it('lets the Admin edit and save, and goes back to read mode', async () => {
    respondWith(mondayBlocks, [
      ...mondayBlocks,
      { dayOfWeek: 'Friday', startTime: '08:00', endTime: '12:00' },
    ])
    const { user } = renderDialog()
    await scheduleRows()

    await user.click(dialog().getByRole('button', { name: 'Editar horario' }))
    await user.click(
      dialog().getByRole('switch', { name: 'Atiende el viernes' }),
    )
    await user.click(dialog().getByRole('button', { name: 'Guardar horario' }))

    expect(await screen.findByText('Horario actualizado.')).toBeInTheDocument()
    const rows = await scheduleRows()
    expect(rows[4]).toHaveTextContent('Viernes08:00 – 12:00')

    const put = requests.find((request) => request.method === 'PUT')!
    expect(put.url.pathname).toBe(schedulePath)
    expect(put.body).toEqual({
      blocks: [
        ...mondayBlocks,
        { dayOfWeek: 'Friday', startTime: '08:00', endTime: '12:00' },
      ],
    })
    expect(requests.filter((request) => request.method === 'GET')).toHaveLength(
      2,
    )
  })

  it('does not let editing an inactive doctor', async () => {
    respondWith(mondayBlocks)
    renderDialog({ target: { ...doctor, isActive: false } })
    await scheduleRows()

    expect(
      screen.getByText(
        'El doctor está inactivo: actívalo para editar su horario.',
      ),
    ).toBeInTheDocument()
    expect(
      dialog().queryByRole('button', { name: 'Editar horario' }),
    ).not.toBeInTheDocument()
  })

  it('retries after a load error', async () => {
    scheduleResponses = [
      () => problemResponse(500),
      () => jsonResponse({ doctorId: doctor.id, blocks: mondayBlocks }),
    ]
    const { user } = renderDialog()

    expect(
      await screen.findByText(/No se pudo cargar el horario\./),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await scheduleRows()).toHaveLength(7)
  })

  it('closes and refreshes when the doctor no longer exists', async () => {
    scheduleResponses = [() => problemResponse(404)]
    const { onClose, onNotFound } = renderDialog()

    await waitFor(() => expect(onNotFound).toHaveBeenCalled())
    expect(onClose).toHaveBeenCalled()
    expect(
      await screen.findByText('El doctor ya no existe.'),
    ).toBeInTheDocument()
  })

  it('asks before closing with unsaved changes', async () => {
    respondWith(mondayBlocks)
    const { user, onClose } = renderDialog()
    await scheduleRows()

    await user.click(dialog().getByRole('button', { name: 'Editar horario' }))
    await user.click(
      dialog().getByRole('switch', { name: 'Atiende el sábado' }),
    )
    await user.keyboard('{Escape}')

    const confirm = within(await screen.findByRole('alertdialog'))
    expect(confirm.getByText('¿Descartar los cambios?')).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()

    await user.click(confirm.getByRole('button', { name: 'Descartar' }))
    expect(onClose).toHaveBeenCalled()
  })

  it('closes without asking when nothing changed', async () => {
    respondWith(mondayBlocks)
    const { user, onClose } = renderDialog()
    await scheduleRows()

    // El primero es el botón del pie; el segundo, la X del modal
    await user.click(dialog().getAllByRole('button', { name: 'Cerrar' })[0])

    expect(onClose).toHaveBeenCalled()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })
})

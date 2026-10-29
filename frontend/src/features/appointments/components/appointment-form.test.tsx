import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { toLocalDateString } from '@/features/appointments/appointment-labels'
import type {
  AvailableSlotsDto,
  CreateAppointmentRequest,
} from '@/features/appointments/appointment-types'
import { AppointmentForm } from '@/features/appointments/components/appointment-form'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import type { PatientDto } from '@/features/patients/patient-types'
import { ApiError } from '@/lib/api/api-error'
import { jsonResponse, problemResponse } from '@/test/api-responses'

const today = toLocalDateString(new Date())
const tomorrow = toLocalDateString(new Date(Date.now() + 24 * 60 * 60 * 1000))

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

function makeDoctor(overrides: Partial<DoctorDto>): DoctorDto {
  return {
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
    ...overrides,
  }
}

const luis = makeDoctor({})
const marta = makeDoctor({
  id: '3c1a0000-0000-0000-0000-000000000002',
  firstName: 'Marta',
  lastName: 'Vera',
  licenseNumber: 'PED-0002',
  specialty: 'Pediatría',
  appointmentDurationMinutes: 20,
})

function slotsOf(doctorId: string, date: string, times: string[][]) {
  const body: AvailableSlotsDto = {
    doctorId,
    date,
    durationMinutes: 30,
    slots: times.map(([start, end]) => ({
      startLocal: `${date}T${start}:00`,
      endLocal: `${date}T${end}:00`,
    })),
  }
  return jsonResponse(body)
}

let requests: URL[]
let slotsResponse: (doctorId: string, date: string) => Response

beforeEach(() => {
  requests = []
  slotsResponse = (doctorId, date) =>
    slotsOf(doctorId, date, [
      ['09:00', '09:30'],
      ['09:30', '10:00'],
    ])
  const fetchMock: Mock<typeof fetch> = vi.fn(async (input) => {
    const url = new URL(String(input))
    requests.push(url)
    const slots = url.pathname.match(
      /^\/api\/doctors\/([^/]+)\/available-slots$/,
    )
    if (slots) {
      return slotsResponse(slots[1], url.searchParams.get('date')!)
    }
    if (url.pathname === '/api/doctors') {
      return jsonResponse({
        pageIndex: 0,
        pageSize: 10,
        count: 2,
        data: [luis, marta],
      })
    }
    if (url.pathname === '/api/patients') {
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
})

function renderForm(
  onSubmit: Mock<
    (request: CreateAppointmentRequest) => Promise<ApiError | null>
  > = vi.fn(async () => null),
) {
  const user = userEvent.setup()
  render(<AppointmentForm onSubmit={onSubmit} onCancel={vi.fn()} />)
  return { user, onSubmit }
}

async function choose(user: UserEvent, combobox: string, option: RegExp) {
  await user.click(screen.getByRole('combobox', { name: combobox }))
  await user.click(await screen.findByRole('option', { name: option }))
}

const slotRequests = () =>
  requests.filter((url) => url.pathname.endsWith('/available-slots'))

const slotButton = (name: string) => screen.findByRole('button', { name })

async function fillAll(user: UserEvent) {
  await choose(user, 'Paciente', /Ana Pérez/)
  await choose(user, 'Doctor', /Luis Andrade/)
  await user.click(await slotButton('09:30 a 10:00'))
  await user.type(screen.getByLabelText('Motivo'), '  Control de presión  ')
}

describe('AppointmentForm', () => {
  it('shows the messages of the empty form without submitting', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    expect(
      await screen.findByText('Selecciona un paciente.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Selecciona un doctor.')).toBeInTheDocument()
    expect(screen.getByText('Selecciona un horario.')).toBeInTheDocument()
    expect(
      screen.getByText('Ingresa el motivo de la cita.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Selecciona un doctor y una fecha para ver los horarios libres.',
      ),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows the doctor details and loads the free slots of the date', async () => {
    const { user } = renderForm()
    expect(screen.getByLabelText('Fecha')).toHaveValue(today)

    await choose(user, 'Doctor', /Marta Vera/)

    expect(screen.getByText('Pediatría · Citas de 20 min')).toBeInTheDocument()
    expect(await slotButton('09:00 a 09:30')).toHaveTextContent('09:00')
    expect(
      screen.getByRole('button', { name: '09:30 a 10:00' }),
    ).toBeInTheDocument()
    const url = slotRequests().at(-1)!
    expect(url.pathname).toBe(`/api/doctors/${marta.id}/available-slots`)
    expect(url.searchParams.get('date')).toBe(today)
  })

  it('tells when the doctor has no free slots that day', async () => {
    slotsResponse = (doctorId, date) => slotsOf(doctorId, date, [])
    const { user } = renderForm()

    await choose(user, 'Doctor', /Luis Andrade/)

    expect(
      await screen.findByText('El doctor no tiene horarios libres ese día.'),
    ).toBeInTheDocument()
  })

  it('marks the chosen slot and clears it when the date or the doctor change', async () => {
    const { user } = renderForm()
    await choose(user, 'Doctor', /Luis Andrade/)

    const slot = await slotButton('09:30 a 10:00')
    await user.click(slot)
    expect(slot).toHaveAttribute('aria-pressed', 'true')

    fireEvent.change(screen.getByLabelText('Fecha'), {
      target: { value: tomorrow },
    })
    await waitFor(() =>
      expect(slotRequests().at(-1)!.searchParams.get('date')).toBe(tomorrow),
    )
    expect(await slotButton('09:30 a 10:00')).toHaveAttribute(
      'aria-pressed',
      'false',
    )

    await user.click(await slotButton('09:00 a 09:30'))
    await choose(user, 'Doctor', /Marta Vera/)
    await waitFor(() =>
      expect(slotRequests().at(-1)!.pathname).toContain(marta.id),
    )
    expect(await slotButton('09:00 a 09:30')).toHaveAttribute(
      'aria-pressed',
      'false',
    )
  })

  it('shows the slots error and retries', async () => {
    slotsResponse = () => problemResponse(400)
    const { user } = renderForm()
    await choose(user, 'Doctor', /Luis Andrade/)

    expect(
      await screen.findByText(
        'No se pueden consultar los horarios: el doctor está inactivo o la fecha ya pasó.',
      ),
    ).toBeInTheDocument()

    slotsResponse = (doctorId, date) =>
      slotsOf(doctorId, date, [['10:00', '10:30']])
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    expect(await slotButton('10:00 a 10:30')).toBeInTheDocument()
  })

  it('submits the request with the slot without seconds and the trimmed reason', async () => {
    const { user, onSubmit } = renderForm()
    await fillAll(user)

    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        patientId: patient.id,
        doctorId: luis.id,
        startLocal: `${today}T09:30`,
        reason: 'Control de presión',
      }),
    )
  })

  it('rejects a reason longer than 500 characters', async () => {
    const { user, onSubmit } = renderForm()

    fireEvent.change(screen.getByLabelText('Motivo'), {
      target: { value: 'a'.repeat(501) },
    })
    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    expect(
      await screen.findByText('El motivo no puede superar 500 caracteres.'),
    ).toBeInTheDocument()
    expect(screen.getByText('501/500')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows the conflict, clears the slot and reloads the slots after a 409', async () => {
    const onSubmit = vi.fn(
      async () =>
        new ApiError({ kind: 'http', status: 409, title: 'Conflict' }),
    )
    const { user } = renderForm(onSubmit)
    await fillAll(user)
    const before = slotRequests().length

    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    expect(
      await screen.findByText(
        'Ese horario ya no está disponible: el doctor o el paciente ya tienen una cita que se cruza. Elige otro.',
      ),
    ).toBeInTheDocument()
    await waitFor(() => expect(slotRequests()).toHaveLength(before + 1))
    expect(await slotButton('09:30 a 10:00')).toHaveAttribute(
      'aria-pressed',
      'false',
    )
    expect(
      screen.getByRole('combobox', { name: 'Paciente' }),
    ).toHaveTextContent('Ana Pérez')
    expect(screen.getByLabelText('Motivo')).toHaveValue(
      '  Control de presión  ',
    )
  })

  it('shows the network error', async () => {
    const onSubmit = vi.fn(
      async () =>
        new ApiError({ kind: 'network', status: 0, title: 'Network' }),
    )
    const { user } = renderForm(onSubmit)
    await fillAll(user)

    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
  })

  it('disables the buttons while scheduling', async () => {
    let resolve!: (error: ApiError | null) => void
    const onSubmit = vi.fn(
      () => new Promise<ApiError | null>((done) => (resolve = done)),
    )
    const { user } = renderForm(onSubmit)
    await fillAll(user)

    await user.click(screen.getByRole('button', { name: 'Agendar cita' }))

    const submit = await screen.findByRole('button', { name: /Agendando…/ })
    expect(submit).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
    resolve(null)
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Agendar cita' }),
      ).toBeEnabled(),
    )
  })
})

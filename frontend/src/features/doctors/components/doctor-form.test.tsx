import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { DoctorForm } from '@/features/doctors/components/doctor-form'
import type { DoctorDto, DoctorRequest } from '@/features/doctors/doctor-types'
import { ApiError } from '@/lib/api/api-error'

const doctor: DoctorDto = {
  id: '3c1a0000-0000-0000-0000-000000000001',
  firstName: 'Luis',
  lastName: 'Mora',
  licenseNumber: 'MP-12345',
  specialty: 'Pediatría',
  email: 'luis.mora@dispensario.local',
  phone: null,
  appointmentDurationMinutes: 45,
  isActive: true,
  userId: null,
  createdAtUtc: '2026-10-26T15:00:00Z',
}

function renderForm({
  mode = 'create',
  initial,
  result = null,
}: {
  mode?: 'create' | 'edit'
  initial?: DoctorDto
  result?: ApiError | null | Promise<ApiError | null>
} = {}) {
  const user = userEvent.setup()
  const onSubmit = vi.fn<(request: DoctorRequest) => Promise<ApiError | null>>(
    () => Promise.resolve(result),
  )
  render(
    <DoctorForm
      mode={mode}
      doctor={initial}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
    />,
  )
  return { user, onSubmit }
}

const saveButton = () => screen.getByRole('button', { name: /Guardar/ })

async function fillValid(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Nombre'), '  Luis ')
  await user.type(screen.getByLabelText('Apellido'), 'Mora')
  await user.type(screen.getByLabelText('Matrícula'), ' mp-12345 ')
  await user.type(screen.getByLabelText('Especialidad'), 'Pediatría')
  await user.type(screen.getByLabelText('Email'), 'luis.mora@dispensario.local')
}

describe('DoctorForm', () => {
  it('shows the required messages and does not submit an empty form', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(saveButton())

    expect(await screen.findByText('Ingresa el nombre.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa el apellido.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa la matrícula.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa la especialidad.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa el email.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects an invalid email', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'not-an-email')

    await user.click(saveButton())

    expect(
      await screen.findByText('Ingresa un email válido.'),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects an invalid phone', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await user.type(screen.getByLabelText('Teléfono (opcional)'), 'abc')

    await user.click(saveButton())

    expect(
      await screen.findByText(
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      ),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits the trimmed values, a null phone and 30 minutes by default', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)

    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        firstName: 'Luis',
        lastName: 'Mora',
        licenseNumber: 'mp-12345',
        specialty: 'Pediatría',
        email: 'luis.mora@dispensario.local',
        phone: null,
        appointmentDurationMinutes: 30,
      }),
    )
  })

  it('submits the duration chosen in the select', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await user.type(screen.getByLabelText('Teléfono (opcional)'), '0991234567')

    await user.click(screen.getByLabelText('Duración de la cita'))
    await user.click(await screen.findByRole('option', { name: '45 min' }))
    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          phone: '0991234567',
          appointmentDurationMinutes: 45,
        }),
      ),
    )
  })

  it('fills the values of the doctor being edited', () => {
    renderForm({ mode: 'edit', initial: doctor })

    expect(screen.getByLabelText('Nombre')).toHaveValue('Luis')
    expect(screen.getByLabelText('Apellido')).toHaveValue('Mora')
    expect(screen.getByLabelText('Matrícula')).toHaveValue('MP-12345')
    expect(screen.getByLabelText('Especialidad')).toHaveValue('Pediatría')
    expect(screen.getByLabelText('Email')).toHaveValue(doctor.email)
    expect(screen.getByLabelText('Teléfono (opcional)')).toHaveValue('')
    expect(screen.getByLabelText('Duración de la cita')).toHaveTextContent(
      '45 min',
    )
  })

  it('shows a 409 under the license number and keeps the values', async () => {
    const { user } = renderForm({
      result: new ApiError({ kind: 'http', status: 409, title: 'Conflict' }),
    })
    await fillValid(user)

    await user.click(saveButton())

    expect(
      await screen.findByText('Ya existe un doctor con esa matrícula.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Matrícula')).toHaveValue(' mp-12345 ')
  })

  it('shows a 400 of the creation in an alert', async () => {
    const { user } = renderForm({
      result: new ApiError({ kind: 'http', status: 400, title: 'Bad' }),
    })
    await fillValid(user)

    await user.click(saveButton())

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Revisa los datos e inténtalo de nuevo.',
    )
  })

  it('shows a 400 of the edition with the duration notice', async () => {
    const { user } = renderForm({
      mode: 'edit',
      initial: doctor,
      result: new ApiError({ kind: 'http', status: 400, title: 'Bad' }),
    })

    await user.click(saveButton())

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'La duración de la cita no puede ser mayor que un bloque del horario del doctor.',
    )
  })

  it('shows a network error in an alert', async () => {
    const { user } = renderForm({
      result: new ApiError({ kind: 'network', status: 0, title: 'Network' }),
    })
    await fillValid(user)

    await user.click(saveButton())

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se pudo conectar con el servidor.',
    )
  })

  it('disables the buttons while saving', async () => {
    const { user } = renderForm({ result: new Promise(() => {}) })
    await fillValid(user)

    await user.click(saveButton())

    const button = await screen.findByRole('button', { name: /Guardando…/ })
    expect(button).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
    expect(within(button).queryByText('Guardar')).not.toBeInTheDocument()
  })
})

import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { PatientForm } from '@/features/patients/components/patient-form'
import type {
  PatientDto,
  PatientRequest,
} from '@/features/patients/patient-types'
import { ApiError } from '@/lib/api/api-error'

const patient: PatientDto = {
  id: '7b2e0000-0000-0000-0000-000000000001',
  documentType: 'Passport',
  documentNumber: 'AB123',
  firstName: 'Ana',
  lastName: 'Pérez',
  birthDate: '1990-05-14',
  gender: 'Female',
  email: null,
  phone: '+593 99 123 4567',
  address: {
    street: 'Av. Siempre Viva 742',
    city: 'Quito',
    state: null,
    reference: 'Casa azul',
  },
  bloodType: 'O+',
  allergies: 'Penicilina',
  isActive: true,
  createdAtUtc: '2026-10-27T15:00:00Z',
}

function renderForm({
  initial,
  result = null,
}: {
  initial?: PatientDto
  result?: ApiError | null | Promise<ApiError | null>
} = {}) {
  const user = userEvent.setup()
  const onSubmit = vi.fn<(request: PatientRequest) => Promise<ApiError | null>>(
    () => Promise.resolve(result),
  )
  render(
    <PatientForm patient={initial} onSubmit={onSubmit} onCancel={vi.fn()} />,
  )
  return { user, onSubmit }
}

type User = ReturnType<typeof userEvent.setup>

const saveButton = () => screen.getByRole('button', { name: /Guardar/ })

// El input date de jsdom no se escribe con user.type: se cambia su valor directamente
const setBirthDate = (value: string) =>
  fireEvent.change(screen.getByLabelText('Fecha de nacimiento'), {
    target: { value },
  })

async function choose(user: User, label: string, option: string) {
  await user.click(screen.getByLabelText(label))
  await user.click(await screen.findByRole('option', { name: option }))
}

async function fillValid(user: User) {
  await user.type(screen.getByLabelText('Número de documento'), ' 0102030405 ')
  await user.type(screen.getByLabelText('Nombre'), '  Ana ')
  await user.type(screen.getByLabelText('Apellido'), 'Pérez')
  setBirthDate('1990-05-14')
  await choose(user, 'Género', 'Femenino')
}

describe('PatientForm', () => {
  it('shows the required messages and does not submit an empty form', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(saveButton())

    expect(
      await screen.findByText('Ingresa el número de documento.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Ingresa el nombre.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa el apellido.')).toBeInTheDocument()
    expect(
      screen.getByText('Ingresa la fecha de nacimiento.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Selecciona el género.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects a future birth date', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    setBirthDate('2999-01-01')

    await user.click(saveButton())

    expect(
      await screen.findByText('La fecha de nacimiento no puede ser futura.'),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects an invalid email and phone', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await user.type(screen.getByLabelText('Email (opcional)'), 'not-an-email')
    await user.type(screen.getByLabelText('Teléfono (opcional)'), 'abc')

    await user.click(saveButton())

    expect(
      await screen.findByText('Ingresa un email válido.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      ),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('requires the street when another address field is filled', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await user.type(screen.getByLabelText('Ciudad'), 'Quito')

    await user.click(saveButton())

    expect(await screen.findByText('Ingresa la calle.')).toBeInTheDocument()
    expect(screen.queryByText('Ingresa la ciudad.')).not.toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects allergies longer than 1000 characters', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    fireEvent.change(screen.getByLabelText('Alergias (opcional)'), {
      target: { value: 'a'.repeat(1001) },
    })

    expect(screen.getByText('1001/1000')).toBeInTheDocument()
    await user.click(saveButton())

    expect(
      await screen.findByText(
        'Las alergias no pueden superar 1000 caracteres.',
      ),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits the trimmed values and the empty optionals as null', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)

    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
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
      }),
    )
  })

  it('submits the address, the blood type and the other optionals', async () => {
    const { user, onSubmit } = renderForm()
    await fillValid(user)
    await choose(user, 'Tipo de documento', 'Pasaporte')
    await user.type(
      screen.getByLabelText('Email (opcional)'),
      'ana@example.com',
    )
    await user.type(screen.getByLabelText('Teléfono (opcional)'), '0991234567')
    await user.type(screen.getByLabelText('Calle'), ' Av. Siempre Viva 742 ')
    await user.type(screen.getByLabelText('Ciudad'), 'Quito')
    await choose(user, 'Tipo de sangre', 'AB-')
    await user.type(screen.getByLabelText('Alergias (opcional)'), 'Penicilina')

    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          documentType: 'Passport',
          email: 'ana@example.com',
          phone: '0991234567',
          address: {
            street: 'Av. Siempre Viva 742',
            city: 'Quito',
            state: null,
            reference: null,
          },
          bloodType: 'AB-',
          allergies: 'Penicilina',
        }),
      ),
    )
  })

  it('fills the values of the patient being edited', () => {
    renderForm({ initial: patient })

    expect(screen.getByLabelText('Tipo de documento')).toHaveTextContent(
      'Pasaporte',
    )
    expect(screen.getByLabelText('Número de documento')).toHaveValue('AB123')
    expect(screen.getByLabelText('Nombre')).toHaveValue('Ana')
    expect(screen.getByLabelText('Fecha de nacimiento')).toHaveValue(
      '1990-05-14',
    )
    expect(screen.getByLabelText('Género')).toHaveTextContent('Femenino')
    expect(screen.getByLabelText('Email (opcional)')).toHaveValue('')
    expect(screen.getByLabelText('Calle')).toHaveValue('Av. Siempre Viva 742')
    expect(screen.getByLabelText('Provincia')).toHaveValue('')
    expect(screen.getByLabelText('Referencia')).toHaveValue('Casa azul')
    expect(screen.getByLabelText('Tipo de sangre')).toHaveTextContent('O+')
    expect(screen.getByLabelText('Alergias (opcional)')).toHaveValue(
      'Penicilina',
    )
  })

  it('sends the cleared optionals of the edition as null', async () => {
    const { user, onSubmit } = renderForm({ initial: patient })
    await user.clear(screen.getByLabelText('Calle'))
    await user.clear(screen.getByLabelText('Ciudad'))
    await user.clear(screen.getByLabelText('Referencia'))
    await choose(user, 'Tipo de sangre', 'Sin especificar')

    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ address: null, bloodType: null }),
      ),
    )
  })

  it('shows a 409 under the document number and keeps the values', async () => {
    const { user } = renderForm({
      result: new ApiError({ kind: 'http', status: 409, title: 'Conflict' }),
    })
    await fillValid(user)

    await user.click(saveButton())

    expect(
      await screen.findByText('Ya existe un paciente con ese documento.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Número de documento')).toHaveValue(
      ' 0102030405 ',
    )
  })

  it('shows a 400 in an alert', async () => {
    const { user } = renderForm({
      result: new ApiError({ kind: 'http', status: 400, title: 'Bad' }),
    })
    await fillValid(user)

    await user.click(saveButton())

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Revisa los datos e inténtalo de nuevo.',
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

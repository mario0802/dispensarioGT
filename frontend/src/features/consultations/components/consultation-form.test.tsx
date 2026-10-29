import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { ConsultationForm } from '@/features/consultations/components/consultation-form'
import type {
  ConsultationDto,
  UpdateConsultationRequest,
} from '@/features/consultations/consultation-types'

const consultation: ConsultationDto = {
  id: '0193f2e0-0000-0000-0000-000000000001',
  patientId: '0193f1c2-0000-0000-0000-000000000001',
  patientFullName: 'Ana Pérez',
  doctorId: '0193f1b8-0000-0000-0000-000000000001',
  doctorFullName: 'Luis Andrade',
  appointmentId: '0193f1d0-0000-0000-0000-000000000001',
  reason: 'Dolor de cabeza',
  diagnosis: null,
  notes: 'Sin fiebre',
  status: 'Open',
  startedAtUtc: '2026-11-03T14:32:10Z',
  closedAtUtc: null,
  startedAtLocal: '2026-11-03T09:32:10',
  closedAtLocal: null,
  createdAtUtc: '2026-11-03T14:32:10Z',
}

function renderForm() {
  const onSave = vi.fn<
    (request: UpdateConsultationRequest) => Promise<boolean>
  >(async () => true)
  const onFinish = vi.fn<
    (
      request: UpdateConsultationRequest,
      hasChanges: boolean,
    ) => Promise<boolean>
  >(async () => true)
  const user = userEvent.setup()
  render(
    <ConsultationForm
      consultation={consultation}
      onSave={onSave}
      onFinish={onFinish}
    />,
  )
  return { user, onSave, onFinish }
}

describe('ConsultationForm', () => {
  it('shows the initial values and their counters', () => {
    renderForm()

    expect(screen.getByLabelText('Motivo')).toHaveValue('Dolor de cabeza')
    expect(screen.getByLabelText('Diagnóstico')).toHaveValue('')
    expect(screen.getByLabelText('Observaciones')).toHaveValue('Sin fiebre')
    expect(screen.getByText('15/500')).toBeInTheDocument()
    expect(screen.getByText('0/1000')).toBeInTheDocument()
    expect(screen.getByText('10/4000')).toBeInTheDocument()
  })

  it('disables Guardar until something changes', async () => {
    const { user } = renderForm()
    const save = screen.getByRole('button', { name: 'Guardar' })
    expect(save).toBeDisabled()

    await user.type(screen.getByLabelText('Diagnóstico'), 'Cefalea')

    expect(save).toBeEnabled()
  })

  it('saves the three texts with the empty ones as null', async () => {
    const { user, onSave } = renderForm()

    await user.clear(screen.getByLabelText('Observaciones'))
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({
        reason: 'Dolor de cabeza',
        diagnosis: null,
        notes: null,
      }),
    )
    // Lo guardado pasa a ser el valor inicial
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled(),
    )
  })

  it('requires the reason to save', async () => {
    const { user, onSave } = renderForm()

    await user.clear(screen.getByLabelText('Motivo'))
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('El motivo es obligatorio.'),
    ).toBeInTheDocument()
    expect(onSave).not.toHaveBeenCalled()
  })

  it('marks the diagnosis instead of confirming when finishing without it', async () => {
    const { user, onFinish } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Finalizar consulta' }))

    expect(
      await screen.findByText('El diagnóstico es obligatorio para finalizar.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(onFinish).not.toHaveBeenCalled()
  })

  it('confirms and finishes with the current texts', async () => {
    const { user, onFinish } = renderForm()

    await user.type(screen.getByLabelText('Diagnóstico'), 'Cefalea tensional')
    await user.click(screen.getByRole('button', { name: 'Finalizar consulta' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent(
      'La consulta de Ana Pérez se cerrará y su cita pasará a Completada.',
    )
    await user.click(screen.getByRole('button', { name: 'Finalizar' }))

    await waitFor(() =>
      expect(onFinish).toHaveBeenCalledWith(
        {
          reason: 'Dolor de cabeza',
          diagnosis: 'Cefalea tensional',
          notes: 'Sin fiebre',
        },
        true,
      ),
    )
  })
})

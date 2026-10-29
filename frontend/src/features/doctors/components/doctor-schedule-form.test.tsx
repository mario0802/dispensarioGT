import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { DoctorScheduleForm } from '@/features/doctors/components/doctor-schedule-form'
import { toScheduleFormValues } from '@/features/doctors/doctor-schedule'
import type {
  ScheduleBlockDto,
  SetDoctorScheduleRequest,
} from '@/features/doctors/doctor-schedule-types'
import { ApiError } from '@/lib/api/api-error'

type SubmitResult = ApiError | null

function renderForm({
  blocks = [],
  durationMinutes = 30,
  isOwnSchedule = false,
  onSubmit = vi.fn(async (): Promise<SubmitResult> => null),
  onCancel = vi.fn(),
}: {
  blocks?: ScheduleBlockDto[]
  durationMinutes?: number
  isOwnSchedule?: boolean
  onSubmit?: (request: SetDoctorScheduleRequest) => Promise<SubmitResult>
  onCancel?: () => void
} = {}) {
  const user = userEvent.setup()
  render(
    <DoctorScheduleForm
      defaultValues={toScheduleFormValues(blocks)}
      appointmentDurationMinutes={durationMinutes}
      isOwnSchedule={isOwnSchedule}
      onSubmit={onSubmit}
      onCancel={onCancel}
    />,
  )
  return { user, onSubmit, onCancel }
}

const saveButton = () => screen.getByRole('button', { name: 'Guardar horario' })

async function chooseTime(
  user: ReturnType<typeof userEvent.setup>,
  label: string,
  time: string,
) {
  await user.click(screen.getByRole('combobox', { name: label }))
  await user.click(await screen.findByRole('option', { name: time }))
}

const mondayMorning: ScheduleBlockDto = {
  dayOfWeek: 'Monday',
  startTime: '08:00',
  endTime: '12:00',
}

function httpError(status: number) {
  return new ApiError({ kind: 'http', status, title: 'Error' })
}

describe('DoctorScheduleForm', () => {
  it('shows the seven days, off when they have no blocks', () => {
    renderForm({ blocks: [mondayMorning] })

    expect(
      screen.getByRole('switch', { name: 'Atiende el lunes' }),
    ).toBeChecked()
    expect(
      screen.getByRole('switch', { name: 'Atiende el domingo' }),
    ).not.toBeChecked()
    expect(screen.getAllByRole('switch')).toHaveLength(7)
    expect(screen.getAllByText('No atiende')).toHaveLength(6)
  })

  it('turning a day on creates 08:00–12:00 and saves it', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(screen.getByRole('switch', { name: 'Atiende el martes' }))
    expect(
      screen.getByRole('combobox', { name: 'Inicio del bloque 1 del martes' }),
    ).toHaveTextContent('08:00')
    expect(
      screen.getByRole('combobox', { name: 'Fin del bloque 1 del martes' }),
    ).toHaveTextContent('12:00')

    await user.click(saveButton())

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        blocks: [
          { dayOfWeek: 'Tuesday', startTime: '08:00', endTime: '12:00' },
        ],
      }),
    )
  })

  it('turning a day off removes its blocks', async () => {
    const { user, onSubmit } = renderForm({ blocks: [mondayMorning] })

    await user.click(screen.getByRole('switch', { name: 'Atiende el lunes' }))
    expect(
      screen.queryByRole('combobox', { name: 'Inicio del bloque 1 del lunes' }),
    ).not.toBeInTheDocument()

    await user.click(saveButton())
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ blocks: [] }))
  })

  it('adds the next block and removing the last one turns the day off', async () => {
    const { user, onSubmit } = renderForm({ blocks: [mondayMorning] })

    await user.click(screen.getByRole('button', { name: 'Añadir bloque' }))
    expect(
      screen.getByRole('combobox', { name: 'Inicio del bloque 2 del lunes' }),
    ).toHaveTextContent('12:00')
    expect(
      screen.getByRole('combobox', { name: 'Fin del bloque 2 del lunes' }),
    ).toHaveTextContent('13:00')

    await user.click(saveButton())
    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        blocks: [
          mondayMorning,
          { dayOfWeek: 'Monday', startTime: '12:00', endTime: '13:00' },
        ],
      }),
    )

    await user.click(
      screen.getByRole('button', { name: 'Quitar el bloque 2 del lunes' }),
    )
    await user.click(
      screen.getByRole('button', { name: 'Quitar el bloque 1 del lunes' }),
    )
    expect(
      screen.getByRole('switch', { name: 'Atiende el lunes' }),
    ).not.toBeChecked()
  })

  it('shows the overlap message under the block without submitting', async () => {
    const { user, onSubmit } = renderForm({ blocks: [mondayMorning] })

    await user.click(screen.getByRole('button', { name: 'Añadir bloque' }))
    await chooseTime(user, 'Inicio del bloque 2 del lunes', '11:00')
    await user.click(saveButton())

    expect(
      await screen.findAllByText('Se solapa con otro bloque del día.'),
    ).toHaveLength(2)
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows the minimum duration message for a short block', async () => {
    const { user, onSubmit } = renderForm({
      blocks: [mondayMorning],
      durationMinutes: 90,
    })

    await chooseTime(user, 'Fin del bloque 1 del lunes', '09:00')
    await user.click(saveButton())

    expect(
      await screen.findByText(
        'El bloque debe durar al menos 90 min (la duración de cada cita).',
      ),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows a loaded half hour and asks for whole hours', async () => {
    const { user, onSubmit } = renderForm({
      blocks: [{ dayOfWeek: 'Monday', startTime: '09:30', endTime: '12:00' }],
    })

    expect(
      screen.getByRole('combobox', { name: 'Inicio del bloque 1 del lunes' }),
    ).toHaveTextContent('09:30')
    await user.click(saveButton())

    expect(await screen.findByText('Usa horas completas.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()

    await chooseTime(user, 'Inicio del bloque 1 del lunes', '09:00')
    await user.click(saveButton())
    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        blocks: [{ dayOfWeek: 'Monday', startTime: '09:00', endTime: '12:00' }],
      }),
    )
  })

  it('shows the API errors and keeps the values', async () => {
    const onSubmit = vi
      .fn<(request: SetDoctorScheduleRequest) => Promise<SubmitResult>>()
      .mockResolvedValueOnce(httpError(400))
      .mockResolvedValueOnce(
        new ApiError({ kind: 'network', status: 0, title: 'Network error' }),
      )
    const { user } = renderForm({ blocks: [mondayMorning], onSubmit })

    await user.click(saveButton())
    expect(
      await screen.findByText(
        'Revisa el horario: algún bloque no cumple las reglas del dispensario.',
      ),
    ).toBeInTheDocument()

    await user.click(saveButton())
    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('combobox', { name: 'Inicio del bloque 1 del lunes' }),
    ).toHaveTextContent('08:00')
  })

  it('shows the relogin message for a 403 in my schedule', async () => {
    const { user } = renderForm({
      blocks: [mondayMorning],
      isOwnSchedule: true,
      onSubmit: async () => httpError(403),
    })

    await user.click(saveButton())
    expect(
      await screen.findByText(
        'No puedes editar este horario. Si tu usuario se vinculó hace poco, cierra sesión y vuelve a entrar.',
      ),
    ).toBeInTheDocument()
  })

  it('disables the buttons while saving', async () => {
    let resolve: (value: SubmitResult) => void = () => {}
    const { user } = renderForm({
      blocks: [mondayMorning],
      onSubmit: () => new Promise((done) => (resolve = done)),
    })

    await user.click(saveButton())

    const saving = await screen.findByRole('button', { name: 'Guardando…' })
    expect(saving).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
    resolve(null)
    await waitFor(() => expect(saveButton()).toBeEnabled())
  })

  it('cancels without asking when nothing changed', async () => {
    const { user, onCancel } = renderForm({ blocks: [mondayMorning] })

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onCancel).toHaveBeenCalled()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('asks before discarding the changes', async () => {
    const { user, onCancel } = renderForm({ blocks: [mondayMorning] })

    await user.click(screen.getByRole('switch', { name: 'Atiende el viernes' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    let confirm = within(await screen.findByRole('alertdialog'))
    expect(confirm.getByText('¿Descartar los cambios?')).toBeInTheDocument()
    await user.click(confirm.getByRole('button', { name: 'Seguir editando' }))
    await waitFor(() =>
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
    )
    expect(onCancel).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    confirm = within(await screen.findByRole('alertdialog'))
    await user.click(confirm.getByRole('button', { name: 'Descartar' }))
    expect(onCancel).toHaveBeenCalled()
  })
})

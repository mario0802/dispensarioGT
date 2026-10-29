import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldTitle,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import {
  toLocalDateString,
  toStartLocalRequest,
} from '@/features/appointments/appointment-labels'
import {
  getCreateAppointmentErrorMessage,
  shouldReloadSlots,
} from '@/features/appointments/appointment-messages'
import type { CreateAppointmentRequest } from '@/features/appointments/appointment-types'
import { AvailableSlotsPicker } from '@/features/appointments/components/available-slots-picker'
import { DoctorCombobox } from '@/features/appointments/components/doctor-combobox'
import { PatientCombobox } from '@/features/appointments/components/patient-combobox'
import type { SearchComboboxOption } from '@/features/appointments/components/search-combobox'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import type { ApiError } from '@/lib/api/api-error'

const reasonMaxLength = 500

const appointmentSchema = z.object({
  patientId: z.string().min(1, 'Selecciona un paciente.'),
  doctorId: z.string().min(1, 'Selecciona un doctor.'),
  date: z
    .string()
    .min(1, { error: 'Selecciona la fecha.', abort: true })
    .refine(
      (value) => value >= toLocalDateString(new Date()),
      'La fecha no puede ser anterior a hoy.',
    ),
  // startLocal del slot elegido, tal como lo devuelve la API
  startLocal: z.string().min(1, 'Selecciona un horario.'),
  reason: z
    .string()
    .trim()
    .min(1, 'Ingresa el motivo de la cita.')
    .max(
      reasonMaxLength,
      `El motivo no puede superar ${reasonMaxLength} caracteres.`,
    ),
})

type AppointmentFormInput = z.input<typeof appointmentSchema>
type AppointmentFormValues = z.output<typeof appointmentSchema>

interface AppointmentFormProps {
  // Devuelve el error que debe mostrar el formulario, o null
  onSubmit: (request: CreateAppointmentRequest) => Promise<ApiError | null>
  onCancel: () => void
}

// Formulario de agendamiento: paciente, doctor, fecha, slot libre y motivo
export function AppointmentForm({ onSubmit, onCancel }: AppointmentFormProps) {
  const [today] = useState(() => toLocalDateString(new Date()))
  const [apiError, setApiError] = useState<ApiError | null>(null)
  const [patient, setPatient] = useState<SearchComboboxOption | null>(null)
  const [doctorOption, setDoctorOption] = useState<SearchComboboxOption | null>(
    null,
  )
  const [doctor, setDoctor] = useState<DoctorDto | null>(null)
  const [slotsReloadKey, setSlotsReloadKey] = useState(0)

  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting, isSubmitted },
  } = useForm<AppointmentFormInput, unknown, AppointmentFormValues>({
    resolver: zodResolver(appointmentSchema),
    defaultValues: {
      patientId: '',
      doctorId: '',
      date: today,
      startLocal: '',
      reason: '',
    },
  })

  const doctorId = useWatch({ control, name: 'doctorId' })
  const date = useWatch({ control, name: 'date' })
  const reasonLength = (useWatch({ control, name: 'reason' }) ?? '').length

  // Cambiar el doctor o la fecha deja el horario sin elegir (los slots se vuelven a pedir)
  const clearSlot = () =>
    setValue('startLocal', '', { shouldValidate: isSubmitted })

  const submit = handleSubmit(async (values) => {
    setApiError(null)
    const error = await onSubmit({
      patientId: values.patientId,
      doctorId: values.doctorId,
      startLocal: toStartLocalRequest(values.startLocal),
      reason: values.reason,
    })
    if (!error) {
      return
    }
    setApiError(error)
    // El slot pudo ocuparse o pasar: se quita y se vuelven a pedir los libres
    if (shouldReloadSlots(error)) {
      setValue('startLocal', '')
      setSlotsReloadKey((key) => key + 1)
    }
  })

  return (
    <form
      onSubmit={submit}
      noValidate
      className="flex min-h-0 flex-1 flex-col gap-4"
    >
      <div className="-mx-6 min-h-0 flex-1 overflow-y-auto px-6 py-1">
        <FieldGroup className="gap-5">
          {apiError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>
                {getCreateAppointmentErrorMessage(apiError)}
              </AlertDescription>
            </Alert>
          )}

          <Controller
            name="patientId"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="appointment-patient">Paciente</FieldLabel>
                <PatientCombobox
                  id="appointment-patient"
                  value={patient}
                  onChange={(option) => {
                    setPatient(option)
                    field.onChange(option?.id ?? '')
                  }}
                  ariaLabel="Paciente"
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="doctorId"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="appointment-doctor">Doctor</FieldLabel>
                <DoctorCombobox
                  id="appointment-doctor"
                  value={doctorOption}
                  onChange={(option) => {
                    setDoctorOption(option)
                    if ((option?.id ?? '') !== field.value) {
                      field.onChange(option?.id ?? '')
                      clearSlot()
                    }
                  }}
                  onDoctorChange={setDoctor}
                  ariaLabel="Doctor"
                  invalid={fieldState.invalid}
                />
                {doctor && (
                  <FieldDescription>
                    {doctor.specialty} · Citas de{' '}
                    {doctor.appointmentDurationMinutes} min
                  </FieldDescription>
                )}
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Field data-invalid={!!errors.date}>
            <FieldLabel htmlFor="appointment-date">Fecha</FieldLabel>
            <Input
              id="appointment-date"
              type="date"
              min={today}
              aria-invalid={!!errors.date}
              {...register('date', { onChange: clearSlot })}
            />
            <FieldError errors={[errors.date]} />
          </Field>

          <Controller
            name="startLocal"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldTitle>Horario</FieldTitle>
                <AvailableSlotsPicker
                  doctorId={doctorId}
                  date={date}
                  today={today}
                  value={field.value}
                  onChange={field.onChange}
                  reloadKey={slotsReloadKey}
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Field data-invalid={!!errors.reason}>
            <FieldLabel htmlFor="appointment-reason">Motivo</FieldLabel>
            <Textarea
              id="appointment-reason"
              rows={3}
              aria-invalid={!!errors.reason}
              {...register('reason')}
            />
            <FieldDescription className="text-right">
              {reasonLength}/{reasonMaxLength}
            </FieldDescription>
            <FieldError errors={[errors.reason]} />
          </Field>
        </FieldGroup>
      </div>

      <div className="flex shrink-0 flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button
          type="button"
          variant="outline"
          disabled={isSubmitting}
          onClick={onCancel}
        >
          Cancelar
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? (
            <>
              <Loader2 className="animate-spin" />
              Agendando…
            </>
          ) : (
            'Agendar cita'
          )}
        </Button>
      </div>
    </form>
  )
}

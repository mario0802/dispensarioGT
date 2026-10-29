import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  duplicateLicenseMessage,
  getDoctorFormErrorMessage,
} from '@/features/doctors/doctor-messages'
import type { DoctorDto, DoctorRequest } from '@/features/doctors/doctor-types'
import type { ApiError } from '@/lib/api/api-error'

// De 10 a 120 minutos en pasos de 5, como Doctor en el backend
const durationOptions = Array.from({ length: 23 }, (_, index) => 10 + index * 5)

const defaultDurationMinutes = 30

// Mismas reglas que CreateDoctorCommandValidator y los value objects del backend
// (FullName, Email, PhoneNumber y Doctor). abort corta en el primer fallo
const doctorSchema = z.object({
  firstName: z
    .string()
    .trim()
    .min(1, { error: 'Ingresa el nombre.', abort: true })
    .max(100, 'El nombre no puede superar 100 caracteres.'),
  lastName: z
    .string()
    .trim()
    .min(1, { error: 'Ingresa el apellido.', abort: true })
    .max(100, 'El apellido no puede superar 100 caracteres.'),
  licenseNumber: z
    .string()
    .trim()
    .min(1, { error: 'Ingresa la matrícula.', abort: true })
    .max(30, 'La matrícula no puede superar 30 caracteres.'),
  specialty: z
    .string()
    .trim()
    .min(1, { error: 'Ingresa la especialidad.', abort: true })
    .max(100, 'La especialidad no puede superar 100 caracteres.'),
  email: z
    .string()
    .trim()
    .min(1, 'Ingresa el email.')
    .max(254, 'El email no puede superar 254 caracteres.')
    .pipe(z.email('Ingresa un email válido.')),
  // Opcional: vacío se envía como null (en la edición, null borra el teléfono)
  phone: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
      'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
    )
    .transform((value) => (value === '' ? null : value)),
  // Select de 10 a 120 en pasos de 5: no admite valores inválidos
  appointmentDurationMinutes: z.number().int().min(10).max(120).multipleOf(5),
})

type DoctorFormInput = z.input<typeof doctorSchema>
type DoctorFormValues = z.output<typeof doctorSchema>

function toFormInput(doctor?: DoctorDto): DoctorFormInput {
  return {
    firstName: doctor?.firstName ?? '',
    lastName: doctor?.lastName ?? '',
    licenseNumber: doctor?.licenseNumber ?? '',
    specialty: doctor?.specialty ?? '',
    email: doctor?.email ?? '',
    phone: doctor?.phone ?? '',
    appointmentDurationMinutes:
      doctor?.appointmentDurationMinutes ?? defaultDurationMinutes,
  }
}

interface DoctorFormProps {
  mode: 'create' | 'edit'
  // En la edición, el doctor recién leído de la API
  doctor?: DoctorDto
  // Devuelve el error que debe mostrar el formulario, o null
  onSubmit: (request: DoctorRequest) => Promise<ApiError | null>
  onCancel: () => void
}

// Formulario de alta y edición de un doctor
export function DoctorForm({
  mode,
  doctor,
  onSubmit,
  onCancel,
}: DoctorFormProps) {
  const [apiError, setApiError] = useState<ApiError | null>(null)

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<DoctorFormInput, unknown, DoctorFormValues>({
    resolver: zodResolver(doctorSchema),
    defaultValues: toFormInput(doctor),
  })

  const submit = handleSubmit(async (values) => {
    setApiError(null)
    const error = await onSubmit(values)
    if (!error) {
      return
    }
    if (error.status === 409) {
      setError('licenseNumber', { message: duplicateLicenseMessage })
    } else {
      setApiError(error)
    }
  })

  return (
    <form onSubmit={submit} noValidate>
      <FieldGroup className="gap-5">
        {apiError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>
              {getDoctorFormErrorMessage(apiError, mode)}
            </AlertDescription>
          </Alert>
        )}

        <div className="grid gap-5 sm:grid-cols-2">
          <Field data-invalid={!!errors.firstName}>
            <FieldLabel htmlFor="firstName">Nombre</FieldLabel>
            <Input
              id="firstName"
              autoComplete="off"
              aria-invalid={!!errors.firstName}
              {...register('firstName')}
            />
            <FieldError errors={[errors.firstName]} />
          </Field>

          <Field data-invalid={!!errors.lastName}>
            <FieldLabel htmlFor="lastName">Apellido</FieldLabel>
            <Input
              id="lastName"
              autoComplete="off"
              aria-invalid={!!errors.lastName}
              {...register('lastName')}
            />
            <FieldError errors={[errors.lastName]} />
          </Field>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field data-invalid={!!errors.licenseNumber}>
            <FieldLabel htmlFor="licenseNumber">Matrícula</FieldLabel>
            <Input
              id="licenseNumber"
              autoComplete="off"
              aria-invalid={!!errors.licenseNumber}
              {...register('licenseNumber')}
            />
            <FieldError errors={[errors.licenseNumber]} />
          </Field>

          <Field data-invalid={!!errors.specialty}>
            <FieldLabel htmlFor="specialty">Especialidad</FieldLabel>
            <Input
              id="specialty"
              autoComplete="off"
              aria-invalid={!!errors.specialty}
              {...register('specialty')}
            />
            <FieldError errors={[errors.specialty]} />
          </Field>
        </div>

        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="email">Email</FieldLabel>
          <Input
            id="email"
            type="email"
            autoComplete="off"
            aria-invalid={!!errors.email}
            {...register('email')}
          />
          <FieldError errors={[errors.email]} />
        </Field>

        <Field data-invalid={!!errors.phone}>
          <FieldLabel htmlFor="phone">Teléfono (opcional)</FieldLabel>
          <Input
            id="phone"
            type="tel"
            autoComplete="off"
            aria-invalid={!!errors.phone}
            {...register('phone')}
          />
          <FieldError errors={[errors.phone]} />
        </Field>

        <Controller
          name="appointmentDurationMinutes"
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="appointmentDurationMinutes">
                Duración de la cita
              </FieldLabel>
              <Select
                name={field.name}
                value={String(field.value)}
                onValueChange={(value) => field.onChange(Number(value))}
              >
                <SelectTrigger
                  id="appointmentDurationMinutes"
                  className="w-full"
                  aria-invalid={fieldState.invalid}
                  onBlur={field.onBlur}
                  ref={field.ref}
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {durationOptions.map((minutes) => (
                    <SelectItem key={minutes} value={String(minutes)}>
                      {minutes} min
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FieldDescription>
                Duración de cada cita con este doctor.
              </FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
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
                Guardando…
              </>
            ) : (
              'Guardar'
            )}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

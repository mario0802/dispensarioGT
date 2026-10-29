import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import {
  Controller,
  useForm,
  useWatch,
  type Control,
  type DefaultValues,
} from 'react-hook-form'
import { z } from 'zod'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import type { DocumentType } from '@/features/auth/auth-types'
import { documentTypeLabels } from '@/features/auth/document-type-labels'
import {
  bloodTypes,
  genderLabels,
  genders,
  toLocalIsoDate,
} from '@/features/patients/patient-labels'
import {
  duplicateDocumentMessage,
  getPatientFormErrorMessage,
} from '@/features/patients/patient-messages'
import type {
  BloodType,
  PatientDto,
  PatientRequest,
} from '@/features/patients/patient-types'
import type { ApiError } from '@/lib/api/api-error'

const documentTypes = Object.keys(documentTypeLabels) as DocumentType[]

const allergiesMaxLength = 1000

// Centinela del Select de tipo de sangre: Radix no admite value=""; se envía como null
const noBloodType = 'none'

const optionalText = (max: number, message: string) =>
  z.string().trim().max(max, message)

// Mismas reglas que el validador y los value objects del backend (Document, FullName,
// Email, PhoneNumber, Address y BloodTypeNotation). abort corta en el primer fallo
const patientSchema = z
  .object({
    documentType: z.enum(['NationalId', 'Passport']),
    documentNumber: z
      .string()
      .trim()
      .min(1, { error: 'Ingresa el número de documento.', abort: true })
      .max(20, 'El documento no puede superar 20 caracteres.'),
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
    // "yyyy-MM-dd" del input date; se compara como texto con la fecha local de hoy
    birthDate: z
      .string()
      .min(1, { error: 'Ingresa la fecha de nacimiento.', abort: true })
      .refine(
        (value) => value <= toLocalIsoDate(new Date()),
        'La fecha de nacimiento no puede ser futura.',
      ),
    gender: z.enum(['Male', 'Female', 'Other'], 'Selecciona el género.'),
    email: z
      .string()
      .trim()
      .max(254, {
        error: 'El email no puede superar 254 caracteres.',
        abort: true,
      })
      .refine(
        (value) => value === '' || z.email().safeParse(value).success,
        'Ingresa un email válido.',
      ),
    phone: z
      .string()
      .trim()
      .refine(
        (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      ),
    street: optionalText(200, 'La calle no puede superar 200 caracteres.'),
    city: optionalText(100, 'La ciudad no puede superar 100 caracteres.'),
    state: optionalText(100, 'La provincia no puede superar 100 caracteres.'),
    reference: optionalText(
      250,
      'La referencia no puede superar 250 caracteres.',
    ),
    bloodType: z.enum([
      noBloodType,
      'A+',
      'A-',
      'B+',
      'B-',
      'AB+',
      'AB-',
      'O+',
      'O-',
    ]),
    allergies: optionalText(
      allergiesMaxLength,
      'Las alergias no pueden superar 1000 caracteres.',
    ),
  })
  // Dirección opcional: si se llena algún campo, la calle y la ciudad son obligatorias
  .superRefine((values, context) => {
    const hasAddress = [
      values.street,
      values.city,
      values.state,
      values.reference,
    ].some((value) => value !== '')
    if (!hasAddress) {
      return
    }
    if (values.street === '') {
      context.addIssue({
        code: 'custom',
        path: ['street'],
        message: 'Ingresa la calle.',
      })
    }
    if (values.city === '') {
      context.addIssue({
        code: 'custom',
        path: ['city'],
        message: 'Ingresa la ciudad.',
      })
    }
  })

type PatientFormInput = z.input<typeof patientSchema>
type PatientFormValues = z.output<typeof patientSchema>

const nullIfEmpty = (value: string) => (value === '' ? null : value)

// Valores ya recortados por Zod → cuerpo de la API (opcionales vacíos como null)
function toRequest(values: PatientFormValues): PatientRequest {
  const hasAddress = values.street !== '' || values.city !== ''
  return {
    documentType: values.documentType,
    documentNumber: values.documentNumber,
    firstName: values.firstName,
    lastName: values.lastName,
    birthDate: values.birthDate,
    gender: values.gender,
    email: nullIfEmpty(values.email),
    phone: nullIfEmpty(values.phone),
    address: hasAddress
      ? {
          street: values.street,
          city: values.city,
          state: nullIfEmpty(values.state),
          reference: nullIfEmpty(values.reference),
        }
      : null,
    bloodType:
      values.bloodType === noBloodType ? null : (values.bloodType as BloodType),
    allergies: nullIfEmpty(values.allergies),
  }
}

// En el alta, gender queda sin valor para que el Select muestre su placeholder
function toFormInput(patient?: PatientDto): DefaultValues<PatientFormInput> {
  return {
    documentType: patient?.documentType ?? 'NationalId',
    documentNumber: patient?.documentNumber ?? '',
    firstName: patient?.firstName ?? '',
    lastName: patient?.lastName ?? '',
    birthDate: patient?.birthDate ?? '',
    gender: patient?.gender,
    email: patient?.email ?? '',
    phone: patient?.phone ?? '',
    street: patient?.address?.street ?? '',
    city: patient?.address?.city ?? '',
    state: patient?.address?.state ?? '',
    reference: patient?.address?.reference ?? '',
    bloodType: patient?.bloodType ?? noBloodType,
    allergies: patient?.allergies ?? '',
  }
}

interface SelectFieldProps {
  control: Control<PatientFormInput, unknown, PatientFormValues>
  name: 'documentType' | 'gender' | 'bloodType'
  label: string
  placeholder?: string
  options: { value: string; label: string }[]
}

// Select de Radix conectado con Controller
function SelectField({
  control,
  name,
  label,
  placeholder,
  options,
}: SelectFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={name}>{label}</FieldLabel>
          <Select
            name={field.name}
            value={field.value ?? ''}
            onValueChange={field.onChange}
          >
            <SelectTrigger
              id={name}
              className="w-full"
              aria-invalid={fieldState.invalid}
              onBlur={field.onBlur}
              ref={field.ref}
            >
              <SelectValue placeholder={placeholder} />
            </SelectTrigger>
            <SelectContent>
              {options.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <FieldError errors={[fieldState.error]} />
        </Field>
      )}
    />
  )
}

interface PatientFormProps {
  // En la edición, el paciente recién leído de la API
  patient?: PatientDto
  // Devuelve el error que debe mostrar el formulario, o null
  onSubmit: (request: PatientRequest) => Promise<ApiError | null>
  onCancel: () => void
}

// Formulario de alta y edición de un paciente: el cuerpo tiene scroll y los botones quedan al pie
export function PatientForm({ patient, onSubmit, onCancel }: PatientFormProps) {
  const [apiError, setApiError] = useState<ApiError | null>(null)

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PatientFormInput, unknown, PatientFormValues>({
    resolver: zodResolver(patientSchema),
    defaultValues: toFormInput(patient),
  })

  const submit = handleSubmit(async (values) => {
    setApiError(null)
    const error = await onSubmit(toRequest(values))
    if (!error) {
      return
    }
    if (error.status === 409) {
      setError('documentNumber', { message: duplicateDocumentMessage })
    } else {
      setApiError(error)
    }
  })

  const allergiesLength = (useWatch({ control, name: 'allergies' }) ?? '')
    .length

  const textField = (
    name:
      | 'documentNumber'
      | 'firstName'
      | 'lastName'
      | 'email'
      | 'phone'
      | 'street'
      | 'city'
      | 'state'
      | 'reference',
    label: string,
    type = 'text',
  ) => (
    <Field data-invalid={!!errors[name]}>
      <FieldLabel htmlFor={name}>{label}</FieldLabel>
      <Input
        id={name}
        type={type}
        autoComplete="off"
        aria-invalid={!!errors[name]}
        {...register(name)}
      />
      <FieldError errors={[errors[name]]} />
    </Field>
  )

  return (
    <form
      onSubmit={submit}
      noValidate
      className="flex min-h-0 flex-1 flex-col gap-4"
    >
      <div className="-mx-6 min-h-0 flex-1 overflow-y-auto px-6 py-1">
        <FieldGroup className="gap-6">
          {apiError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>
                {getPatientFormErrorMessage(apiError)}
              </AlertDescription>
            </Alert>
          )}

          <FieldSet>
            <FieldLegend variant="label">Identificación</FieldLegend>
            <FieldGroup className="gap-5">
              <div className="grid gap-5 sm:grid-cols-2">
                <SelectField
                  control={control}
                  name="documentType"
                  label="Tipo de documento"
                  options={documentTypes.map((value) => ({
                    value,
                    label: documentTypeLabels[value],
                  }))}
                />
                {textField('documentNumber', 'Número de documento')}
              </div>
              <div className="grid gap-5 sm:grid-cols-2">
                {textField('firstName', 'Nombre')}
                {textField('lastName', 'Apellido')}
              </div>
              <div className="grid gap-5 sm:grid-cols-2">
                <Field data-invalid={!!errors.birthDate}>
                  <FieldLabel htmlFor="birthDate">
                    Fecha de nacimiento
                  </FieldLabel>
                  <Input
                    id="birthDate"
                    type="date"
                    max={toLocalIsoDate(new Date())}
                    aria-invalid={!!errors.birthDate}
                    {...register('birthDate')}
                  />
                  <FieldError errors={[errors.birthDate]} />
                </Field>
                <SelectField
                  control={control}
                  name="gender"
                  label="Género"
                  placeholder="Selecciona"
                  options={genders.map((value) => ({
                    value,
                    label: genderLabels[value],
                  }))}
                />
              </div>
            </FieldGroup>
          </FieldSet>

          <FieldSet>
            <FieldLegend variant="label">Contacto</FieldLegend>
            <div className="grid gap-5 sm:grid-cols-2">
              {textField('email', 'Email (opcional)', 'email')}
              {textField('phone', 'Teléfono (opcional)', 'tel')}
            </div>
          </FieldSet>

          <FieldSet>
            <FieldLegend variant="label">Dirección (opcional)</FieldLegend>
            <FieldDescription>
              Si la indicas, la calle y la ciudad son obligatorias.
            </FieldDescription>
            <FieldGroup className="gap-5">
              {textField('street', 'Calle')}
              <div className="grid gap-5 sm:grid-cols-2">
                {textField('city', 'Ciudad')}
                {textField('state', 'Provincia')}
              </div>
              {textField('reference', 'Referencia')}
            </FieldGroup>
          </FieldSet>

          <FieldSet>
            <FieldLegend variant="label">Datos clínicos</FieldLegend>
            <FieldGroup className="gap-5">
              <SelectField
                control={control}
                name="bloodType"
                label="Tipo de sangre"
                options={[
                  { value: noBloodType, label: 'Sin especificar' },
                  ...bloodTypes.map((value) => ({ value, label: value })),
                ]}
              />
              <Field data-invalid={!!errors.allergies}>
                <FieldLabel htmlFor="allergies">Alergias (opcional)</FieldLabel>
                <Textarea
                  id="allergies"
                  rows={3}
                  aria-invalid={!!errors.allergies}
                  {...register('allergies')}
                />
                <FieldDescription className="text-right">
                  {allergiesLength}/{allergiesMaxLength}
                </FieldDescription>
                <FieldError errors={[errors.allergies]} />
              </Field>
            </FieldGroup>
          </FieldSet>
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
              Guardando…
            </>
          ) : (
            'Guardar'
          )}
        </Button>
      </div>
    </form>
  )
}

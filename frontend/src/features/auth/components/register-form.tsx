import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Eye, EyeOff, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router'
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
import { registerAccount } from '@/features/auth/auth-api'
import type { DocumentType, RegisterRequest } from '@/features/auth/auth-types'
import { documentTypeLabels } from '@/features/auth/document-type-labels'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

const documentTypes = Object.keys(documentTypeLabels) as DocumentType[]

// Mismas reglas que ValidPassword del backend. Sin trim, como en el login.
// abort corta en el primer fallo para mostrar un único mensaje por campo
const passwordSchema = z
  .string()
  .min(1, { error: 'Ingresa una contraseña.', abort: true })
  .min(8, {
    error: 'La contraseña debe tener al menos 8 caracteres.',
    abort: true,
  })
  .max(72, {
    error: 'La contraseña no puede superar 72 caracteres.',
    abort: true,
  })
  .regex(/[A-Za-z]/, {
    error: 'La contraseña debe tener al menos una letra.',
    abort: true,
  })
  .regex(/[0-9]/, { error: 'La contraseña debe tener al menos un dígito.' })

const confirmPasswordSchema = z.string().min(1, 'Confirma la contraseña.')

// Para comparar las contraseñas aunque otros campos tengan errores
const passwordFieldsSchema = z.object({
  password: passwordSchema,
  confirmPassword: confirmPasswordSchema,
})

// Mismas reglas que RegisterUserCommandValidator y los value objects del backend
// (Email, FullName, Document y PhoneNumber)
const registerSchema = z
  .object({
    firstName: z
      .string()
      .trim()
      .min(1, { error: 'Ingresa tu nombre.', abort: true })
      .max(100, 'El nombre no puede superar 100 caracteres.'),
    lastName: z
      .string()
      .trim()
      .min(1, { error: 'Ingresa tu apellido.', abort: true })
      .max(100, 'El apellido no puede superar 100 caracteres.'),
    documentType: z.enum(['NationalId', 'Passport']),
    documentNumber: z
      .string()
      .trim()
      .min(1, { error: 'Ingresa el número de documento.', abort: true })
      .max(20, 'El número de documento no puede superar 20 caracteres.'),
    email: z
      .string()
      .trim()
      .min(1, 'Ingresa tu email.')
      .max(254, 'El email no puede superar 254 caracteres.')
      .pipe(z.email('Ingresa un email válido.')),
    // Opcional: vacío se envía como null (la API rechaza un teléfono vacío)
    phone: z
      .string()
      .trim()
      .refine(
        (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      )
      .transform((value) => (value === '' ? null : value)),
    password: passwordSchema,
    confirmPassword: confirmPasswordSchema,
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    error: 'Las contraseñas no coinciden.',
    when: (payload) => passwordFieldsSchema.safeParse(payload.value).success,
  })

type RegisterFormInput = z.input<typeof registerSchema>
type RegisterFormValues = z.output<typeof registerSchema>

function getRegisterErrorMessage(error: ApiError): string {
  if (error.status === 409) {
    return 'Ya existe un usuario con ese email o esa identificación.'
  }
  if (error.status === 400) {
    return 'Revisa los datos e inténtalo de nuevo.'
  }
  if (error.kind === 'network') {
    return 'No se pudo conectar con el servidor.'
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

interface PasswordInputProps extends React.ComponentProps<typeof Input> {
  showLabel: string
  hideLabel: string
}

// Campo de contraseña con el botón del ojo para mostrarla u ocultarla
function PasswordInput({ showLabel, hideLabel, ...props }: PasswordInputProps) {
  const [visible, setVisible] = useState(false)

  return (
    <div className="relative">
      <Input
        {...props}
        type={visible ? 'text' : 'password'}
        className="pr-10"
      />
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="absolute top-0 right-0 text-muted-foreground"
        aria-label={visible ? hideLabel : showLabel}
        onClick={() => setVisible((value) => !value)}
      >
        {visible ? <EyeOff /> : <Eye />}
      </Button>
    </div>
  )
}

// Tras crear la cuenta lleva al login con el email; la API no devuelve token
export function RegisterForm() {
  const navigate = useNavigate()
  const { error, isLoading, execute } = useApi(registerAccount)

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormInput, unknown, RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      documentType: 'NationalId',
      documentNumber: '',
      email: '',
      phone: '',
      password: '',
      confirmPassword: '',
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    // confirmPassword solo se valida en el cliente: no se envía
    const request: RegisterRequest = {
      email: values.email,
      password: values.password,
      firstName: values.firstName,
      lastName: values.lastName,
      documentType: values.documentType,
      documentNumber: values.documentNumber,
      phone: values.phone,
    }
    const result = await execute(request)
    if (!result.error) {
      navigate('/login', {
        replace: true,
        state: { registeredEmail: values.email },
      })
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup className="gap-5">
        {error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>
              {getRegisterErrorMessage(error)}
            </AlertDescription>
          </Alert>
        )}

        <div className="grid gap-5 sm:grid-cols-2">
          <Field data-invalid={!!errors.firstName}>
            <FieldLabel htmlFor="firstName">Nombre</FieldLabel>
            <Input
              id="firstName"
              autoComplete="given-name"
              aria-invalid={!!errors.firstName}
              {...register('firstName')}
            />
            <FieldError errors={[errors.firstName]} />
          </Field>

          <Field data-invalid={!!errors.lastName}>
            <FieldLabel htmlFor="lastName">Apellido</FieldLabel>
            <Input
              id="lastName"
              autoComplete="family-name"
              aria-invalid={!!errors.lastName}
              {...register('lastName')}
            />
            <FieldError errors={[errors.lastName]} />
          </Field>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <Controller
            name="documentType"
            control={control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="documentType">
                  Tipo de documento
                </FieldLabel>
                <Select
                  name={field.name}
                  value={field.value}
                  onValueChange={field.onChange}
                >
                  <SelectTrigger
                    id="documentType"
                    className="w-full"
                    aria-invalid={fieldState.invalid}
                    onBlur={field.onBlur}
                    ref={field.ref}
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {documentTypes.map((type) => (
                      <SelectItem key={type} value={type}>
                        {documentTypeLabels[type]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Field data-invalid={!!errors.documentNumber}>
            <FieldLabel htmlFor="documentNumber">
              Número de documento
            </FieldLabel>
            <Input
              id="documentNumber"
              autoComplete="off"
              aria-invalid={!!errors.documentNumber}
              {...register('documentNumber')}
            />
            <FieldError errors={[errors.documentNumber]} />
          </Field>
        </div>

        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="email">Email</FieldLabel>
          <Input
            id="email"
            type="email"
            autoComplete="email"
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
            autoComplete="tel"
            aria-invalid={!!errors.phone}
            {...register('phone')}
          />
          <FieldError errors={[errors.phone]} />
        </Field>

        <Field data-invalid={!!errors.password}>
          <FieldLabel htmlFor="password">Contraseña</FieldLabel>
          <PasswordInput
            id="password"
            autoComplete="new-password"
            aria-invalid={!!errors.password}
            showLabel="Mostrar contraseña"
            hideLabel="Ocultar contraseña"
            {...register('password')}
          />
          <FieldDescription>
            De 8 a 72 caracteres, con al menos una letra y un dígito.
          </FieldDescription>
          <FieldError errors={[errors.password]} />
        </Field>

        <Field data-invalid={!!errors.confirmPassword}>
          <FieldLabel htmlFor="confirmPassword">
            Confirmar contraseña
          </FieldLabel>
          <PasswordInput
            id="confirmPassword"
            autoComplete="new-password"
            aria-invalid={!!errors.confirmPassword}
            showLabel="Mostrar confirmación"
            hideLabel="Ocultar confirmación"
            {...register('confirmPassword')}
          />
          <FieldError errors={[errors.confirmPassword]} />
        </Field>

        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? (
            <>
              <Loader2 className="animate-spin" />
              Creando cuenta…
            </>
          ) : (
            'Crear cuenta'
          )}
        </Button>
      </FieldGroup>
    </form>
  )
}

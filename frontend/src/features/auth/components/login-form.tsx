import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Eye, EyeOff, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { useAuth } from '@/features/auth/use-auth'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

// Mismas reglas que LoginCommandValidator del backend: NotEmpty, MaximumLength(254) y EmailAddress.
// El email se valida tras el trim y solo si pasó los anteriores, para mostrar un único mensaje
const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Ingresa tu email.')
    .max(254, 'El email no puede superar 254 caracteres.')
    .pipe(z.email('Ingresa un email válido.')),
  password: z.string().min(1, 'Ingresa tu contraseña.'),
})

type LoginFormInput = z.input<typeof loginSchema>
type LoginFormValues = z.output<typeof loginSchema>

function getLoginErrorMessage(error: ApiError): string {
  if (error.status === 401) {
    return 'Email o contraseña incorrectos.'
  }
  if (error.kind === 'network') {
    return 'No se pudo conectar con el servidor.'
  }
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
}

// No navega: al quedar la sesión autenticada, la ruta /login redirige sola
export function LoginForm() {
  const { login } = useAuth()
  const { error, isLoading, execute } = useApi(login)
  const [showPassword, setShowPassword] = useState(false)

  const {
    register,
    handleSubmit,
    resetField,
    setFocus,
    formState: { errors },
  } = useForm<LoginFormInput, unknown, LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    const result = await execute(values)
    if (result.error?.status === 401) {
      resetField('password')
      setFocus('password')
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup className="gap-5">
        {error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{getLoginErrorMessage(error)}</AlertDescription>
          </Alert>
        )}

        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="email">Email</FieldLabel>
          <Input
            id="email"
            type="email"
            autoComplete="username"
            aria-invalid={!!errors.email}
            {...register('email')}
          />
          <FieldError errors={[errors.email]} />
        </Field>

        <Field data-invalid={!!errors.password}>
          <FieldLabel htmlFor="password">Contraseña</FieldLabel>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              className="pr-10"
              aria-invalid={!!errors.password}
              {...register('password')}
            />
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="absolute top-0 right-0 text-muted-foreground"
              aria-label={
                showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'
              }
              onClick={() => setShowPassword((value) => !value)}
            >
              {showPassword ? <EyeOff /> : <Eye />}
            </Button>
          </div>
          <FieldError errors={[errors.password]} />
        </Field>

        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? (
            <>
              <Loader2 className="animate-spin" />
              Iniciando sesión…
            </>
          ) : (
            'Iniciar sesión'
          )}
        </Button>
      </FieldGroup>
    </form>
  )
}

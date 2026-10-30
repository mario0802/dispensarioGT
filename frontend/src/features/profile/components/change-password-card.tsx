import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'

import { PasswordInput } from '@/components/password-input'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { changeMyPassword } from '@/features/profile/profile-api'
import {
  getChangePasswordErrorMessage,
  incorrectCurrentPasswordMessage,
  isIncorrectCurrentPasswordError,
} from '@/features/profile/profile-messages'
import {
  type ChangePasswordFormValues,
  changePasswordSchema,
} from '@/features/profile/profile-schemas'
import { useApi } from '@/hooks/use-api'

const emptyValues: ChangePasswordFormValues = {
  currentPassword: '',
  newPassword: '',
  confirmNewPassword: '',
}

// Tarjeta Cambiar contraseña (PUT /api/users/me/password). La sesión sigue abierta tras el cambio
export function ChangePasswordCard() {
  const { isLoading, execute } = useApi(changeMyPassword)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: emptyValues,
  })

  const onSubmit = handleSubmit(async (values) => {
    // confirmNewPassword solo se valida en el cliente: no se envía
    const result = await execute({
      currentPassword: values.currentPassword,
      newPassword: values.newPassword,
    })
    if (result.error) {
      if (isIncorrectCurrentPasswordError(result.error)) {
        setError(
          'currentPassword',
          { message: incorrectCurrentPasswordMessage },
          { shouldFocus: true },
        )
      } else {
        toast.error(getChangePasswordErrorMessage(result.error))
      }
      return
    }
    toast.success('Contraseña cambiada.')
    reset(emptyValues)
  })

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Cambiar contraseña</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} noValidate>
          <FieldGroup className="gap-5">
            {/* La contraseña actual nunca se muestra: sin botón del ojo */}
            <Field data-invalid={!!errors.currentPassword}>
              <FieldLabel htmlFor="currentPassword">
                Contraseña actual
              </FieldLabel>
              <Input
                id="currentPassword"
                type="password"
                autoComplete="current-password"
                aria-invalid={!!errors.currentPassword}
                {...register('currentPassword')}
              />
              <FieldError errors={[errors.currentPassword]} />
            </Field>

            <Field data-invalid={!!errors.newPassword}>
              <FieldLabel htmlFor="newPassword">Nueva contraseña</FieldLabel>
              <PasswordInput
                id="newPassword"
                autoComplete="new-password"
                aria-invalid={!!errors.newPassword}
                showLabel="Mostrar contraseña"
                hideLabel="Ocultar contraseña"
                {...register('newPassword')}
              />
              <FieldDescription>
                De 8 a 72 caracteres, con al menos una letra y un dígito.
              </FieldDescription>
              <FieldError errors={[errors.newPassword]} />
            </Field>

            <Field data-invalid={!!errors.confirmNewPassword}>
              <FieldLabel htmlFor="confirmNewPassword">
                Confirmar nueva contraseña
              </FieldLabel>
              <PasswordInput
                id="confirmNewPassword"
                autoComplete="new-password"
                aria-invalid={!!errors.confirmNewPassword}
                showLabel="Mostrar confirmación"
                hideLabel="Ocultar confirmación"
                {...register('confirmNewPassword')}
              />
              <FieldError errors={[errors.confirmNewPassword]} />
            </Field>

            <div className="flex justify-end">
              <Button type="submit" disabled={isLoading}>
                {isLoading ? (
                  <>
                    <Loader2 className="animate-spin" />
                    Cambiando…
                  </>
                ) : (
                  'Cambiar contraseña'
                )}
              </Button>
            </div>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}

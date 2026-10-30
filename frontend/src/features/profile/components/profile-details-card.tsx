import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'

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
import type { CurrentUserDto, DocumentType } from '@/features/auth/auth-types'
import { documentTypeLabels } from '@/features/auth/document-type-labels'
import { roleLabels } from '@/features/auth/role-labels'
import { useAuth } from '@/features/auth/use-auth'
import { updateMyProfile } from '@/features/profile/profile-api'
import { getUpdateProfileErrorMessage } from '@/features/profile/profile-messages'
import {
  type ProfileFormInput,
  type ProfileFormValues,
  profileSchema,
} from '@/features/profile/profile-schemas'
import { useApi } from '@/hooks/use-api'

function DetailItem({
  label,
  children,
}: {
  label: string
  children: ReactNode
}) {
  return (
    <div className="min-w-0">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm break-words">{children}</dd>
    </div>
  )
}

function formatUserDocument(user: CurrentUserDto): string {
  // La API envía el tipo como texto; uno desconocido se muestra tal cual
  const label =
    documentTypeLabels[user.identificationType as DocumentType] ??
    user.identificationType
  return `${label} ${user.identificationNumber}`
}

// Tarjeta Datos personales: email, documento y rol en solo lectura y el formulario de PUT /api/users/me
export function ProfileDetailsCard({ user }: { user: CurrentUserDto }) {
  const { refreshUser } = useAuth()
  const { isLoading, execute } = useApi(updateMyProfile)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ProfileFormInput, unknown, ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      firstName: user.firstName,
      lastName: user.lastName,
      phone: user.phone ?? '',
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    const result = await execute(values)
    if (result.error) {
      toast.error(getUpdateProfileErrorMessage(result.error))
      return
    }
    toast.success('Datos actualizados.')
    // Lo guardado pasa a ser el estado limpio del formulario
    reset({
      firstName: values.firstName,
      lastName: values.lastName,
      phone: values.phone ?? '',
    })
    void refreshUser()
  })

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Datos personales</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <dl className="grid gap-4 sm:grid-cols-2">
            <DetailItem label="Email">{user.email}</DetailItem>
            <DetailItem label="Documento">
              {formatUserDocument(user)}
            </DetailItem>
            <DetailItem label="Rol">{roleLabels[user.role]}</DetailItem>
          </dl>
          <p className="text-sm text-muted-foreground">
            El email, el documento y el rol no se pueden modificar.
          </p>
        </div>

        <form onSubmit={onSubmit} noValidate>
          <FieldGroup className="gap-5">
            <div className="grid gap-5 sm:grid-cols-2">
              <Field data-invalid={!!errors.firstName}>
                <FieldLabel htmlFor="profile-firstName">Nombre</FieldLabel>
                <Input
                  id="profile-firstName"
                  autoComplete="given-name"
                  aria-invalid={!!errors.firstName}
                  {...register('firstName')}
                />
                <FieldError errors={[errors.firstName]} />
              </Field>

              <Field data-invalid={!!errors.lastName}>
                <FieldLabel htmlFor="profile-lastName">Apellido</FieldLabel>
                <Input
                  id="profile-lastName"
                  autoComplete="family-name"
                  aria-invalid={!!errors.lastName}
                  {...register('lastName')}
                />
                <FieldError errors={[errors.lastName]} />
              </Field>
            </div>

            <Field data-invalid={!!errors.phone}>
              <FieldLabel htmlFor="profile-phone">Teléfono</FieldLabel>
              <Input
                id="profile-phone"
                type="tel"
                autoComplete="tel"
                aria-invalid={!!errors.phone}
                {...register('phone')}
              />
              <FieldDescription>Opcional.</FieldDescription>
              <FieldError errors={[errors.phone]} />
            </Field>

            <div className="flex justify-end">
              <Button type="submit" disabled={!isDirty || isLoading}>
                {isLoading ? (
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
      </CardContent>
    </Card>
  )
}

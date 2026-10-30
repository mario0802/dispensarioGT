import { z } from 'zod'

import { passwordSchema } from '@/features/auth/password-schema'

// Mismas reglas y mensajes que el registro (FullName y PhoneNumber del backend)
export const profileSchema = z.object({
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
  // Opcional: vacío se envía como null, que borra el teléfono (la API rechaza uno vacío)
  phone: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
      'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
    )
    .transform((value) => (value === '' ? null : value)),
})

export type ProfileFormInput = z.input<typeof profileSchema>
export type ProfileFormValues = z.output<typeof profileSchema>

const currentPasswordSchema = z.string().min(1, 'Ingresa tu contraseña actual.')
const confirmNewPasswordSchema = z
  .string()
  .min(1, 'Confirma la nueva contraseña.')

// Para comparar solo cuando los campos implicados no tienen errores propios
const confirmFieldsSchema = z.object({
  newPassword: passwordSchema,
  confirmNewPassword: confirmNewPasswordSchema,
})
const differentFieldsSchema = z.object({
  currentPassword: currentPasswordSchema,
  newPassword: passwordSchema,
})

// Contraseñas sin trim, como en el login y el registro
export const changePasswordSchema = z
  .object({
    currentPassword: currentPasswordSchema,
    newPassword: passwordSchema,
    confirmNewPassword: confirmNewPasswordSchema,
  })
  .refine((values) => values.newPassword === values.confirmNewPassword, {
    path: ['confirmNewPassword'],
    error: 'Las contraseñas no coinciden.',
    when: (payload) => confirmFieldsSchema.safeParse(payload.value).success,
  })
  // Como el NotEqual del backend
  .refine((values) => values.newPassword !== values.currentPassword, {
    path: ['newPassword'],
    error: 'La nueva contraseña debe ser distinta de la actual.',
    when: (payload) => differentFieldsSchema.safeParse(payload.value).success,
  })

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>

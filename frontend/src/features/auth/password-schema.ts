import { z } from 'zod'

// Mismas reglas que ValidPassword del backend. Sin trim, como en el login.
// abort corta en el primer fallo para mostrar un único mensaje por campo
export const passwordSchema = z
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

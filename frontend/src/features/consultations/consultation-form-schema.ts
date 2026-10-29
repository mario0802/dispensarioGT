import { z } from 'zod'

export const reasonMaxLength = 500
export const diagnosisMaxLength = 1000
export const notesMaxLength = 4000

const diagnosisTooLongMessage = `El diagnóstico admite como máximo ${diagnosisMaxLength} caracteres.`

// Guardar: motivo obligatorio; diagnóstico y observaciones opcionales
export const saveConsultationSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, 'El motivo es obligatorio.')
    .max(
      reasonMaxLength,
      `El motivo admite como máximo ${reasonMaxLength} caracteres.`,
    ),
  diagnosis: z.string().trim().max(diagnosisMaxLength, diagnosisTooLongMessage),
  notes: z
    .string()
    .trim()
    .max(
      notesMaxLength,
      `Las observaciones admiten como máximo ${notesMaxLength} caracteres.`,
    ),
})

// Finalizar: además, diagnóstico obligatorio
export const finishConsultationSchema = saveConsultationSchema.extend({
  diagnosis: z
    .string()
    .trim()
    .min(1, 'El diagnóstico es obligatorio para finalizar.')
    .max(diagnosisMaxLength, diagnosisTooLongMessage),
})

export type ConsultationFormValues = z.infer<typeof saveConsultationSchema>

// Un texto vacío tras trim viaja como null
export function toNullableText(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

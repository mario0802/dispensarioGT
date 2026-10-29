import { zodResolver } from '@hookform/resolvers/zod'
import { CircleCheck, Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { FinishConsultationDialog } from '@/features/consultations/components/finish-consultation-dialog'
import {
  diagnosisMaxLength,
  finishConsultationSchema,
  notesMaxLength,
  reasonMaxLength,
  saveConsultationSchema,
  toNullableText,
  type ConsultationFormValues,
} from '@/features/consultations/consultation-form-schema'
import type {
  ConsultationDto,
  UpdateConsultationRequest,
} from '@/features/consultations/consultation-types'

interface ConsultationFormProps {
  consultation: ConsultationDto
  // PUT; resuelve true si se guardó
  onSave: (request: UpdateConsultationRequest) => Promise<boolean>
  // PUT si hasChanges y luego el cierre; resuelve true si los textos quedaron guardados
  onFinish: (
    request: UpdateConsultationRequest,
    hasChanges: boolean,
  ) => Promise<boolean>
  // La página avisa al salir con cambios sin guardar
  onDirtyChange?: (dirty: boolean) => void
}

function toFormValues(
  texts: Pick<ConsultationDto, 'reason' | 'diagnosis' | 'notes'>,
): ConsultationFormValues {
  return {
    reason: texts.reason,
    diagnosis: texts.diagnosis ?? '',
    notes: texts.notes ?? '',
  }
}

function toRequest(values: ConsultationFormValues): UpdateConsultationRequest {
  return {
    reason: values.reason.trim(),
    diagnosis: toNullableText(values.diagnosis),
    notes: toNullableText(values.notes),
  }
}

// Motivo, diagnóstico y observaciones de una consulta abierta, con Guardar y Finalizar consulta
export function ConsultationForm({
  consultation,
  onSave,
  onFinish,
  onDirtyChange,
}: ConsultationFormProps) {
  const {
    register,
    control,
    handleSubmit,
    getValues,
    setError,
    clearErrors,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<ConsultationFormValues>({
    resolver: zodResolver(saveConsultationSchema),
    defaultValues: toFormValues(consultation),
  })
  const [confirmingFinish, setConfirmingFinish] = useState(false)
  const [isFinishing, setIsFinishing] = useState(false)
  const isBusy = isSubmitting || isFinishing

  const [reason = '', diagnosis = '', notes = ''] = useWatch({
    control,
    name: ['reason', 'diagnosis', 'notes'],
  })

  useEffect(() => {
    onDirtyChange?.(isDirty)
  }, [isDirty, onDirtyChange])

  // Tras guardar, el formulario parte de lo guardado y deja de estar sucio
  const markSaved = (request: UpdateConsultationRequest) =>
    reset(toFormValues(request))

  const save = handleSubmit(async (values) => {
    const request = toRequest(values)
    if (await onSave(request)) {
      markSaved(request)
    }
  })

  // Finalizar exige además el diagnóstico: se valida antes de abrir la confirmación
  const requestFinish = () => {
    clearErrors()
    const result = finishConsultationSchema.safeParse(getValues())
    if (result.success) {
      setConfirmingFinish(true)
      return
    }
    for (const issue of result.error.issues) {
      const name = issue.path[0] as keyof ConsultationFormValues
      setError(name, { message: issue.message }, { shouldFocus: true })
    }
  }

  const finish = async () => {
    const hasChanges = isDirty
    const request = toRequest(getValues())
    setIsFinishing(true)
    const saved = await onFinish(request, hasChanges)
    // Si el cierre fue bien la página navega; si no, se vuelve al formulario
    setIsFinishing(false)
    setConfirmingFinish(false)
    if (saved && hasChanges) {
      markSaved(request)
    }
  }

  return (
    <form onSubmit={save} noValidate className="flex flex-col gap-6">
      <FieldGroup className="gap-5">
        <Field data-invalid={!!errors.reason}>
          <FieldLabel htmlFor="consultation-reason">Motivo</FieldLabel>
          <Textarea
            id="consultation-reason"
            rows={2}
            aria-invalid={!!errors.reason}
            {...register('reason')}
          />
          <FieldDescription className="text-right">
            {reason.length}/{reasonMaxLength}
          </FieldDescription>
          <FieldError errors={[errors.reason]} />
        </Field>

        <Field data-invalid={!!errors.diagnosis}>
          <FieldLabel htmlFor="consultation-diagnosis">Diagnóstico</FieldLabel>
          <Textarea
            id="consultation-diagnosis"
            rows={3}
            aria-invalid={!!errors.diagnosis}
            {...register('diagnosis')}
          />
          <FieldDescription className="text-right">
            {diagnosis.length}/{diagnosisMaxLength}
          </FieldDescription>
          <FieldError errors={[errors.diagnosis]} />
        </Field>

        <Field data-invalid={!!errors.notes}>
          <FieldLabel htmlFor="consultation-notes">Observaciones</FieldLabel>
          <Textarea
            id="consultation-notes"
            rows={6}
            aria-invalid={!!errors.notes}
            {...register('notes')}
          />
          <FieldDescription className="text-right">
            {notes.length}/{notesMaxLength}
          </FieldDescription>
          <FieldError errors={[errors.notes]} />
        </Field>
      </FieldGroup>

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="submit" variant="outline" disabled={!isDirty || isBusy}>
          {isSubmitting && <Loader2 className="animate-spin" />}
          {isSubmitting ? 'Guardando…' : 'Guardar'}
        </Button>
        <Button type="button" disabled={isBusy} onClick={requestFinish}>
          <CircleCheck />
          Finalizar consulta
        </Button>
      </div>

      <FinishConsultationDialog
        open={confirmingFinish}
        patientFullName={consultation.patientFullName}
        isFinishing={isFinishing}
        onConfirm={() => void finish()}
        onCancel={() => setConfirmingFinish(false)}
      />
    </form>
  )
}

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { DiscardChangesDialog } from '@/features/doctors/components/discard-changes-dialog'
import { DoctorScheduleDayRow } from '@/features/doctors/components/doctor-schedule-day-row'
import { getScheduleSaveErrorMessage } from '@/features/doctors/doctor-messages'
import {
  createScheduleSchema,
  toScheduleRequest,
  weekDays,
  type ScheduleFormValues,
} from '@/features/doctors/doctor-schedule'
import type { SetDoctorScheduleRequest } from '@/features/doctors/doctor-schedule-types'
import type { ApiError } from '@/lib/api/api-error'

interface DoctorScheduleFormProps {
  defaultValues: ScheduleFormValues
  appointmentDurationMinutes: number
  // En Mi horario el 403 tiene su propio mensaje
  isOwnSchedule: boolean
  // Devuelve el error que debe mostrar el formulario, o null
  onSubmit: (request: SetDoctorScheduleRequest) => Promise<ApiError | null>
  onCancel: () => void
  onDirtyChange?: (dirty: boolean) => void
}

// Edición del horario semanal: un switch por día y sus bloques en horas completas
export function DoctorScheduleForm({
  defaultValues,
  appointmentDurationMinutes,
  isOwnSchedule,
  onSubmit,
  onCancel,
  onDirtyChange,
}: DoctorScheduleFormProps) {
  const [apiError, setApiError] = useState<ApiError | null>(null)
  const [confirmingDiscard, setConfirmingDiscard] = useState(false)

  const form = useForm<ScheduleFormValues>({
    resolver: zodResolver(createScheduleSchema(appointmentDurationMinutes)),
    defaultValues,
  })
  const {
    handleSubmit,
    formState: { isDirty, isSubmitting },
  } = form

  useEffect(() => {
    onDirtyChange?.(isDirty)
  }, [isDirty, onDirtyChange])

  const submit = handleSubmit(async (values) => {
    setApiError(null)
    const error = await onSubmit(toScheduleRequest(values))
    if (error) {
      setApiError(error)
    }
  })

  const cancel = () => {
    if (isDirty) {
      setConfirmingDiscard(true)
    } else {
      onCancel()
    }
  }

  return (
    <FormProvider {...form}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          Horas completas, en hora local del dispensario. Cada bloque dura al
          menos {appointmentDurationMinutes} min.
        </p>

        {apiError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>
              {getScheduleSaveErrorMessage(apiError, isOwnSchedule)}
            </AlertDescription>
          </Alert>
        )}

        <ul
          aria-label="Editar horario semanal"
          className="divide-y rounded-md border"
        >
          {weekDays.map(({ label }, dayIndex) => (
            <DoctorScheduleDayRow
              key={label}
              dayIndex={dayIndex}
              dayLabel={label}
              appointmentDurationMinutes={appointmentDurationMinutes}
              disabled={isSubmitting}
            />
          ))}
        </ul>

        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button
            type="button"
            variant="outline"
            disabled={isSubmitting}
            onClick={cancel}
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
              'Guardar horario'
            )}
          </Button>
        </div>
      </form>

      <DiscardChangesDialog
        open={confirmingDiscard}
        onKeepEditing={() => setConfirmingDiscard(false)}
        onDiscard={() => {
          setConfirmingDiscard(false)
          onCancel()
        }}
      />
    </FormProvider>
  )
}

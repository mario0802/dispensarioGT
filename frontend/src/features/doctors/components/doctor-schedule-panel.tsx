import { AlertCircle, Info, Pencil } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { toast } from 'sonner'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { DoctorScheduleForm } from '@/features/doctors/components/doctor-schedule-form'
import { DoctorScheduleView } from '@/features/doctors/components/doctor-schedule-view'
import { getDoctorsLoadErrorMessage } from '@/features/doctors/doctor-messages'
import { toScheduleFormValues } from '@/features/doctors/doctor-schedule'
import type { SetDoctorScheduleRequest } from '@/features/doctors/doctor-schedule-types'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import {
  getDoctorSchedule,
  setDoctorSchedule,
} from '@/features/doctors/doctors-api'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

interface DoctorSchedulePanelProps {
  // Nombre, duración de la cita y estado
  doctor: DoctorDto
  // Admin en el modal; el Doctor en Mi horario
  canEdit: boolean
  // Mi horario: textos en segunda persona y su propio mensaje para el 403
  isOwnSchedule?: boolean
  // 404 al cargar o al guardar
  onNotFound: () => void
  // El modal lo usa para confirmar al cerrar y para no cerrarse mientras guarda
  onDirtyChange?: (dirty: boolean) => void
  onSavingChange?: (saving: boolean) => void
  // Acciones extra junto a Editar horario, solo en lectura (el botón Cerrar del modal)
  viewActions?: ReactNode
}

// Horario semanal de un doctor: lo carga, lo muestra y, si se puede, lo edita
export function DoctorSchedulePanel({
  doctor,
  canEdit,
  isOwnSchedule = false,
  onNotFound,
  onDirtyChange,
  onSavingChange,
  viewActions,
}: DoctorSchedulePanelProps) {
  const schedule = useApi(getDoctorSchedule)
  const saving = useApi(setDoctorSchedule)
  const { execute: loadSchedule } = schedule
  const [isEditing, setIsEditing] = useState(false)

  const load = async () => {
    const { error } = await loadSchedule(doctor.id)
    if (error?.status === 404) {
      onNotFound()
    }
  }

  useEffect(() => {
    void load()
    // Solo se vuelve a cargar si cambia el doctor
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [doctor.id])

  const leaveEditing = () => {
    setIsEditing(false)
    onDirtyChange?.(false)
  }

  const save = async (
    request: SetDoctorScheduleRequest,
  ): Promise<ApiError | null> => {
    onSavingChange?.(true)
    const { error } = await saving.execute(doctor.id, request)
    if (error) {
      onSavingChange?.(false)
      if (error.status === 404) {
        onNotFound()
        return null
      }
      return error
    }
    toast.success('Horario actualizado.')
    // Se vuelve a lectura con el horario guardado
    await loadSchedule(doctor.id)
    onSavingChange?.(false)
    leaveEditing()
    return null
  }

  if (schedule.error && !isEditing) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription className="gap-3">
          <p>
            No se pudo cargar el horario.{' '}
            {getDoctorsLoadErrorMessage(schedule.error)}
          </p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void load()}
          >
            Reintentar
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  if (!schedule.data) {
    return (
      <div className="flex flex-col gap-2" data-testid="schedule-skeleton">
        {Array.from({ length: 7 }, (_, index) => (
          <Skeleton key={index} className="h-9 w-full" />
        ))}
      </div>
    )
  }

  if (isEditing) {
    return (
      <DoctorScheduleForm
        defaultValues={toScheduleFormValues(schedule.data.blocks)}
        appointmentDurationMinutes={doctor.appointmentDurationMinutes}
        isOwnSchedule={isOwnSchedule}
        onSubmit={save}
        onCancel={leaveEditing}
        onDirtyChange={onDirtyChange}
      />
    )
  }

  const canEditNow = canEdit && doctor.isActive

  return (
    <div className="flex flex-col gap-4">
      <DoctorScheduleView
        blocks={schedule.data.blocks}
        emptyMessage={
          isOwnSchedule
            ? 'Todavía no tienes horario.'
            : 'Este doctor todavía no tiene horario.'
        }
      />
      <p className="text-xs text-muted-foreground">
        Horas locales del dispensario.
      </p>

      {canEdit && !doctor.isActive && (
        <Alert>
          <Info />
          <AlertDescription>
            {isOwnSchedule
              ? 'Tu ficha de doctor está inactiva: pide al administrador que la active para editar tu horario.'
              : 'El doctor está inactivo: actívalo para editar su horario.'}
          </AlertDescription>
        </Alert>
      )}

      {(canEditNow || viewActions) && (
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          {viewActions}
          {canEditNow && (
            <Button type="button" onClick={() => setIsEditing(true)}>
              <Pencil />
              Editar horario
            </Button>
          )}
        </div>
      )}
    </div>
  )
}

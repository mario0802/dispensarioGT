import { AlertCircle } from 'lucide-react'
import { useEffect } from 'react'
import { toast } from 'sonner'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { DoctorForm } from '@/features/doctors/components/doctor-form'
import {
  doctorNotFoundMessage,
  getDoctorActionErrorMessage,
  getDoctorFullName,
} from '@/features/doctors/doctor-messages'
import type { DoctorDto, DoctorRequest } from '@/features/doctors/doctor-types'
import {
  createDoctor,
  getDoctor,
  updateDoctor,
} from '@/features/doctors/doctors-api'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

export type DoctorFormTarget =
  { mode: 'create' } | { mode: 'edit'; doctor: DoctorDto }

interface DoctorFormDialogProps {
  target: DoctorFormTarget
  onClose: () => void
  // Tras guardar o al descubrir que el doctor ya no existe: refresca el listado
  onSaved: () => void
}

// Modal de alta y edición; la edición pide antes el detalle a la API
export function DoctorFormDialog({
  target,
  onClose,
  onSaved,
}: DoctorFormDialogProps) {
  const details = useApi(getDoctor)
  const creation = useApi(createDoctor)
  const update = useApi(updateDoctor)
  const { execute: loadDoctor } = details
  const isSaving = creation.isLoading || update.isLoading
  const editId = target.mode === 'edit' ? target.doctor.id : null

  // El doctor ya no existe: se cierra el modal y se refresca el listado
  const closeAsNotFound = () => {
    onClose()
    toast.error(doctorNotFoundMessage)
    onSaved()
  }

  const loadDetails = async (id: string) => {
    const { error } = await loadDoctor(id)
    if (error?.status === 404) {
      closeAsNotFound()
    }
  }

  useEffect(() => {
    if (editId) {
      void loadDetails(editId)
    }
    // Solo se vuelve a cargar si cambia el doctor
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editId])

  const save = async (request: DoctorRequest): Promise<ApiError | null> => {
    const { error } =
      target.mode === 'create'
        ? await creation.execute(request)
        : await update.execute(target.doctor.id, request)

    if (error?.status === 404) {
      closeAsNotFound()
      return null
    }
    if (error) {
      return error
    }
    onClose()
    toast.success(
      target.mode === 'create' ? 'Doctor creado.' : 'Doctor actualizado.',
    )
    onSaved()
    return null
  }

  const isEdit = target.mode === 'edit'

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        // Mientras se guarda no se cierra con Esc, clic fuera ni la X
        if (!open && !isSaving) {
          onClose()
        }
      }}
    >
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Editar doctor' : 'Nuevo doctor'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? `Modifica los datos de ${getDoctorFullName(target.doctor)}.`
              : 'Registra un doctor activo.'}
          </DialogDescription>
        </DialogHeader>

        {!isEdit && (
          <DoctorForm mode="create" onSubmit={save} onCancel={onClose} />
        )}

        {isEdit && details.data && (
          <DoctorForm
            mode="edit"
            doctor={details.data}
            onSubmit={save}
            onCancel={onClose}
          />
        )}

        {isEdit && !details.data && details.error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription className="gap-3">
              <p>{getDoctorActionErrorMessage(details.error)}</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void loadDetails(target.doctor.id)}
              >
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {isEdit && !details.data && !details.error && (
          <div className="grid gap-5" data-testid="doctor-form-skeleton">
            {Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-9 w-full" />
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

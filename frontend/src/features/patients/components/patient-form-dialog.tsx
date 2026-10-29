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
import { PatientForm } from '@/features/patients/components/patient-form'
import { getPatientFullName } from '@/features/patients/patient-labels'
import {
  getPatientActionErrorMessage,
  patientNotFoundMessage,
} from '@/features/patients/patient-messages'
import type {
  PatientDto,
  PatientRequest,
} from '@/features/patients/patient-types'
import {
  createPatient,
  getPatient,
  updatePatient,
} from '@/features/patients/patients-api'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

export type PatientFormTarget =
  { mode: 'create' } | { mode: 'edit'; patient: PatientDto }

interface PatientFormDialogProps {
  target: PatientFormTarget
  onClose: () => void
  // Tras guardar o al descubrir que el paciente ya no existe: refresca el listado
  onSaved: () => void
}

// Modal de alta y edición; la edición pide antes el detalle a la API
export function PatientFormDialog({
  target,
  onClose,
  onSaved,
}: PatientFormDialogProps) {
  const details = useApi(getPatient)
  const creation = useApi(createPatient)
  const update = useApi(updatePatient)
  const { execute: loadPatient } = details
  const isSaving = creation.isLoading || update.isLoading
  const editId = target.mode === 'edit' ? target.patient.id : null

  // El paciente ya no existe: se cierra el modal y se refresca el listado
  const closeAsNotFound = () => {
    onClose()
    toast.error(patientNotFoundMessage)
    onSaved()
  }

  const loadDetails = async (id: string) => {
    const { error } = await loadPatient(id)
    if (error?.status === 404) {
      closeAsNotFound()
    }
  }

  useEffect(() => {
    if (editId) {
      void loadDetails(editId)
    }
    // Solo se vuelve a cargar si cambia el paciente
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editId])

  const save = async (request: PatientRequest): Promise<ApiError | null> => {
    const { error } =
      target.mode === 'create'
        ? await creation.execute(request)
        : await update.execute(target.patient.id, request)

    if (error?.status === 404) {
      closeAsNotFound()
      return null
    }
    if (error) {
      return error
    }
    onClose()
    toast.success(
      target.mode === 'create' ? 'Paciente creado.' : 'Paciente actualizado.',
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
      {/* Columna flexible: el cuerpo del formulario hace scroll y título y botones quedan fijos */}
      <DialogContent className="flex max-h-[calc(100dvh-2rem)] flex-col sm:max-w-2xl">
        <DialogHeader className="shrink-0">
          <DialogTitle>
            {isEdit ? 'Editar paciente' : 'Nuevo paciente'}
          </DialogTitle>
          <DialogDescription>
            {isEdit
              ? `Modifica los datos de ${getPatientFullName(target.patient)}.`
              : 'Registra un paciente activo.'}
          </DialogDescription>
        </DialogHeader>

        {!isEdit && <PatientForm onSubmit={save} onCancel={onClose} />}

        {isEdit && details.data && (
          <PatientForm
            patient={details.data}
            onSubmit={save}
            onCancel={onClose}
          />
        )}

        {isEdit && !details.data && details.error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription className="gap-3">
              <p>{getPatientActionErrorMessage(details.error)}</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void loadDetails(target.patient.id)}
              >
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {isEdit && !details.data && !details.error && (
          <div className="grid gap-5" data-testid="patient-form-skeleton">
            {Array.from({ length: 6 }, (_, index) => (
              <Skeleton key={index} className="h-9 w-full" />
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

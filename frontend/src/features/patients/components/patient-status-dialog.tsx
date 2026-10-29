import { Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { getPatientFullName } from '@/features/patients/patient-labels'
import { getPatientActionErrorMessage } from '@/features/patients/patient-messages'
import type { PatientDto } from '@/features/patients/patient-types'
import {
  activatePatient,
  deactivatePatient,
} from '@/features/patients/patients-api'
import { useApi } from '@/hooks/use-api'

// Activa o desactiva según el estado actual del paciente
const togglePatientStatus = (patient: PatientDto) =>
  patient.isActive ? deactivatePatient(patient.id) : activatePatient(patient.id)

interface PatientStatusDialogProps {
  patient: PatientDto
  onClose: () => void
  // Al terminar, con éxito o con error: refresca el listado
  onDone: () => void
}

// Confirmación de activar o desactivar un paciente
export function PatientStatusDialog({
  patient,
  onClose,
  onDone,
}: PatientStatusDialogProps) {
  const { isLoading, execute } = useApi(togglePatientStatus)
  const deactivating = patient.isActive
  const fullName = getPatientFullName(patient)

  // Botón propio, no AlertDialogAction: este cerraría el diálogo antes de que responda la API
  const confirm = async () => {
    const { error } = await execute(patient)
    onClose()
    if (!error) {
      toast.success(
        deactivating ? 'Paciente desactivado.' : 'Paciente activado.',
      )
    } else if (error.status === 400) {
      toast.error(
        deactivating
          ? 'El paciente ya estaba inactivo.'
          : 'El paciente ya estaba activo.',
      )
    } else {
      toast.error(getPatientActionErrorMessage(error))
    }
    onDone()
  }

  return (
    <AlertDialog
      open
      onOpenChange={(open) => {
        if (!open && !isLoading) {
          onClose()
        }
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {deactivating
              ? `¿Desactivar a ${fullName}?`
              : `¿Activar a ${fullName}?`}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {deactivating
              ? 'No se le podrán agendar citas nuevas. Podrás activarlo de nuevo.'
              : 'Se le podrán volver a agendar citas.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isLoading}>Cancelar</AlertDialogCancel>
          <Button
            type="button"
            variant={deactivating ? 'destructive' : 'default'}
            disabled={isLoading}
            onClick={() => void confirm()}
          >
            {isLoading && <Loader2 className="animate-spin" />}
            {deactivating ? 'Desactivar' : 'Activar'}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

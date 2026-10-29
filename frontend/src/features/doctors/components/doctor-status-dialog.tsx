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
import {
  getDoctorActionErrorMessage,
  getDoctorFullName,
} from '@/features/doctors/doctor-messages'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import {
  activateDoctor,
  deactivateDoctor,
} from '@/features/doctors/doctors-api'
import { useApi } from '@/hooks/use-api'

// Activa o desactiva según el estado actual del doctor
const toggleDoctorStatus = (doctor: DoctorDto) =>
  doctor.isActive ? deactivateDoctor(doctor.id) : activateDoctor(doctor.id)

interface DoctorStatusDialogProps {
  doctor: DoctorDto
  onClose: () => void
  // Al terminar, con éxito o con error: refresca el listado
  onDone: () => void
}

// Confirmación de activar o desactivar un doctor
export function DoctorStatusDialog({
  doctor,
  onClose,
  onDone,
}: DoctorStatusDialogProps) {
  const { isLoading, execute } = useApi(toggleDoctorStatus)
  const deactivating = doctor.isActive
  const fullName = getDoctorFullName(doctor)

  // Botón propio, no AlertDialogAction: este cerraría el diálogo antes de que responda la API
  const confirm = async () => {
    const { error } = await execute(doctor)
    onClose()
    if (!error) {
      toast.success(deactivating ? 'Doctor desactivado.' : 'Doctor activado.')
    } else if (error.status === 400) {
      toast.error(
        deactivating
          ? 'El doctor ya estaba inactivo.'
          : 'El doctor ya estaba activo.',
      )
    } else {
      toast.error(getDoctorActionErrorMessage(error))
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

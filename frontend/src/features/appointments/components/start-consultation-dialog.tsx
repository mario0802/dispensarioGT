import { Loader2 } from 'lucide-react'
import { useNavigate } from 'react-router'
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
import { formatLocalTimeRange } from '@/features/appointments/appointment-labels'
import type { AppointmentDto } from '@/features/appointments/appointment-types'
import {
  existingConsultationNotFoundMessage,
  getStartConsultationErrorMessage,
} from '@/features/consultations/consultation-messages'
import {
  getPatientConsultations,
  startConsultation,
} from '@/features/consultations/consultations-api'
import { useApi } from '@/hooks/use-api'

// Página de la historia en la que se busca la consulta ya iniciada (de la más reciente a la más antigua)
const historyPageSize = 100

interface StartConsultationDialogProps {
  appointment: AppointmentDto
  onClose: () => void
  // Si no se pudo iniciar ni abrir la consulta: refresca el listado
  onFailed: () => void
}

// Confirmación de iniciar la consulta de una cita de hoy; al terminar, lleva a su página
export function StartConsultationDialog({
  appointment,
  onClose,
  onFailed,
}: StartConsultationDialogProps) {
  const navigate = useNavigate()
  const start = useApi(startConsultation)
  const history = useApi(getPatientConsultations)
  const isLoading = start.isLoading || history.isLoading

  const fail = (message: string) => {
    onClose()
    toast.error(message)
    onFailed()
  }

  // Botón propio, no AlertDialogAction: este cerraría el diálogo antes de que responda la API
  const confirm = async () => {
    const { data, error } = await start.execute({
      patientId: appointment.patientId,
      appointmentId: appointment.id,
      reason: appointment.reason,
    })
    if (data) {
      navigate(`/consultations/${data.id}`)
      return
    }
    if (error.status !== 409) {
      fail(getStartConsultationErrorMessage(error))
      return
    }

    // La cita ya tiene consulta (p. ej. se volvió atrás tras iniciarla): se busca y se abre
    const { data: page } = await history.execute(appointment.patientId, {
      pageIndex: 0,
      pageSize: historyPageSize,
    })
    const existing = page?.data.find(
      (consultation) => consultation.appointmentId === appointment.id,
    )
    if (existing) {
      navigate(`/consultations/${existing.id}`)
    } else {
      fail(existingConsultationNotFoundMessage)
    }
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
          <AlertDialogTitle>Iniciar consulta</AlertDialogTitle>
          <AlertDialogDescription>
            Se iniciará la consulta de{' '}
            <span className="font-medium text-foreground">
              {appointment.patientFullName}
            </span>{' '}
            de las{' '}
            {formatLocalTimeRange(appointment.startLocal, appointment.endLocal)}{' '}
            con el motivo de la cita: “{appointment.reason}”.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isLoading}>Cancelar</AlertDialogCancel>
          <Button
            type="button"
            disabled={isLoading}
            onClick={() => void confirm()}
          >
            {isLoading && <Loader2 className="animate-spin" />}
            {isLoading ? 'Iniciando…' : 'Iniciar consulta'}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

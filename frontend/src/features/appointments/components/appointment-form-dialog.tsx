import { toast } from 'sonner'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import type { CreateAppointmentRequest } from '@/features/appointments/appointment-types'
import { createAppointment } from '@/features/appointments/appointments-api'
import { AppointmentForm } from '@/features/appointments/components/appointment-form'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

interface AppointmentFormDialogProps {
  onClose: () => void
  // Tras agendar: refresca el listado
  onSaved: () => void
}

// Modal Nueva cita; cada apertura monta el formulario vacío
export function AppointmentFormDialog({
  onClose,
  onSaved,
}: AppointmentFormDialogProps) {
  const creation = useApi(createAppointment)

  const save = async (
    request: CreateAppointmentRequest,
  ): Promise<ApiError | null> => {
    const { error } = await creation.execute(request)
    if (error) {
      return error
    }
    onClose()
    toast.success('Cita agendada.')
    onSaved()
    return null
  }

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        // Mientras se guarda no se cierra con Esc, clic fuera ni la X
        if (!open && !creation.isLoading) {
          onClose()
        }
      }}
    >
      {/* Columna flexible: el cuerpo del formulario hace scroll y título y botones quedan fijos */}
      <DialogContent className="flex max-h-[calc(100dvh-2rem)] flex-col sm:max-w-2xl">
        <DialogHeader className="shrink-0">
          <DialogTitle>Nueva cita</DialogTitle>
          <DialogDescription>
            Elige el paciente, el doctor y uno de sus horarios libres.
          </DialogDescription>
        </DialogHeader>

        <AppointmentForm onSubmit={save} onCancel={onClose} />
      </DialogContent>
    </Dialog>
  )
}

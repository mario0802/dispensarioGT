import { useState } from 'react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useAuth } from '@/features/auth/use-auth'
import { DiscardChangesDialog } from '@/features/doctors/components/discard-changes-dialog'
import { DoctorSchedulePanel } from '@/features/doctors/components/doctor-schedule-panel'
import {
  doctorNotFoundMessage,
  getDoctorFullName,
} from '@/features/doctors/doctor-messages'
import type { DoctorDto } from '@/features/doctors/doctor-types'

interface DoctorScheduleDialogProps {
  // El doctor de la fila: trae la duración de la cita y el estado
  doctor: DoctorDto
  onClose: () => void
  // El doctor ya no existe: refresca el listado
  onNotFound: () => void
}

// Modal del cronograma: el horario semanal; solo el Admin lo edita
export function DoctorScheduleDialog({
  doctor,
  onClose,
  onNotFound,
}: DoctorScheduleDialogProps) {
  const { user } = useAuth()
  const [isDirty, setIsDirty] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [confirmingClose, setConfirmingClose] = useState(false)

  const requestClose = () => {
    if (isSaving) {
      return
    }
    if (isDirty) {
      setConfirmingClose(true)
    } else {
      onClose()
    }
  }

  const closeAsNotFound = () => {
    onClose()
    toast.error(doctorNotFoundMessage)
    onNotFound()
  }

  return (
    <>
      <Dialog open onOpenChange={(open) => !open && requestClose()}>
        <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Cronograma de {getDoctorFullName(doctor)}</DialogTitle>
            <DialogDescription>{doctor.specialty}</DialogDescription>
          </DialogHeader>
          <DoctorSchedulePanel
            doctor={doctor}
            canEdit={user?.role === 'Admin'}
            onNotFound={closeAsNotFound}
            onDirtyChange={setIsDirty}
            onSavingChange={setIsSaving}
            viewActions={
              <Button type="button" variant="outline" onClick={requestClose}>
                Cerrar
              </Button>
            }
          />
        </DialogContent>
      </Dialog>

      <DiscardChangesDialog
        open={confirmingClose}
        onKeepEditing={() => setConfirmingClose(false)}
        onDiscard={() => {
          setConfirmingClose(false)
          onClose()
        }}
      />
    </>
  )
}

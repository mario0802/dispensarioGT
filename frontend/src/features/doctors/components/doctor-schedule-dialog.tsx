import { CalendarClock } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { getDoctorFullName } from '@/features/doctors/doctor-messages'
import type { DoctorDto } from '@/features/doctors/doctor-types'

interface DoctorScheduleDialogProps {
  doctor: DoctorDto
  onClose: () => void
}

// Modal del cronograma de disponibilidad: de momento solo el aviso, la funcionalidad llega en otro spec
export function DoctorScheduleDialog({
  doctor,
  onClose,
}: DoctorScheduleDialogProps) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      {/* Sin la X: ya tiene el botón Cerrar */}
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Cronograma de {getDoctorFullName(doctor)}</DialogTitle>
          <DialogDescription>{doctor.specialty}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col items-center gap-3 py-6 text-center text-sm text-muted-foreground">
          <CalendarClock className="size-10" aria-hidden />
          <p>El cronograma de disponibilidad estará disponible próximamente.</p>
        </div>
        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" variant="outline">
              Cerrar
            </Button>
          </DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

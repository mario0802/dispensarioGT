import { Badge } from '@/components/ui/badge'
import {
  statusBadgeClasses,
  statusLabels,
} from '@/features/appointments/appointment-labels'
import type { AppointmentStatus } from '@/features/appointments/appointment-types'

// Estado de la cita con su etiqueta en español y su color
export function AppointmentStatusBadge({
  status,
}: {
  status: AppointmentStatus
}) {
  return (
    <Badge variant="outline" className={statusBadgeClasses[status]}>
      {statusLabels[status]}
    </Badge>
  )
}

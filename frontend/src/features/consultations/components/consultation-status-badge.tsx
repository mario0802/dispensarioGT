import { Badge } from '@/components/ui/badge'
import {
  consultationStatusBadgeClasses,
  consultationStatusLabels,
} from '@/features/consultations/consultation-labels'
import type { ConsultationStatus } from '@/features/consultations/consultation-types'

// Estado de la consulta con su etiqueta en español y su color
export function ConsultationStatusBadge({
  status,
}: {
  status: ConsultationStatus
}) {
  return (
    <Badge variant="outline" className={consultationStatusBadgeClasses[status]}>
      {consultationStatusLabels[status]}
    </Badge>
  )
}

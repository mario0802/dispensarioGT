import type { ReactNode } from 'react'

import { ConsultationStatusBadge } from '@/features/consultations/components/consultation-status-badge'
import { formatLocalDateTime } from '@/features/consultations/consultation-labels'
import type { ConsultationDto } from '@/features/consultations/consultation-types'

// Inicio, estado, doctor y, si está cerrada, cierre de la consulta
export function ConsultationDetails({
  consultation,
}: {
  consultation: ConsultationDto
}) {
  return (
    <dl className="grid gap-4 sm:grid-cols-2">
      <DetailItem label="Inicio">
        {formatLocalDateTime(consultation.startedAtLocal)}
      </DetailItem>
      <DetailItem label="Estado">
        <ConsultationStatusBadge status={consultation.status} />
      </DetailItem>
      <DetailItem label="Doctor">{consultation.doctorFullName}</DetailItem>
      {consultation.closedAtLocal && (
        <DetailItem label="Cierre">
          {formatLocalDateTime(consultation.closedAtLocal)}
        </DetailItem>
      )}
    </dl>
  )
}

export function DetailItem({
  label,
  children,
  className,
}: {
  label: string
  children: ReactNode
  className?: string
}) {
  return (
    <div className={className}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{children}</dd>
    </div>
  )
}

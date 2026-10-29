import type { ConsultationDto } from '@/features/consultations/consultation-types'

type ConsultationTexts = Pick<ConsultationDto, 'reason' | 'diagnosis' | 'notes'>

// Motivo, diagnóstico y observaciones como texto (consulta cerrada, de otro doctor o en la historia)
export function ConsultationReadOnly({
  consultation,
}: {
  consultation: ConsultationTexts
}) {
  return (
    <dl className="flex flex-col gap-4">
      <ReadOnlyText label="Motivo" value={consultation.reason} />
      <ReadOnlyText label="Diagnóstico" value={consultation.diagnosis} />
      <ReadOnlyText label="Observaciones" value={consultation.notes} />
    </dl>
  )
}

function ReadOnlyText({
  label,
  value,
}: {
  label: string
  value: string | null
}) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm break-words whitespace-pre-wrap">
        {value ?? '—'}
      </dd>
    </div>
  )
}

import { AlertCircle, ChevronLeft, ChevronRight } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'

import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ConsultationReadOnly } from '@/features/consultations/components/consultation-read-only'
import { ConsultationStatusBadge } from '@/features/consultations/components/consultation-status-badge'
import { formatLocalDateTime } from '@/features/consultations/consultation-labels'
import { loadHistoryErrorMessage } from '@/features/consultations/consultation-messages'
import { getPatientConsultations } from '@/features/consultations/consultations-api'
import { useApi } from '@/hooks/use-api'

const pageSize = 5

interface ConsultationHistoryProps {
  patientId: string
  currentConsultationId: string
}

// "3 consultas previas" / "1 consulta previa" / "Sin consultas previas"
function formatPreviousCount(count: number): string {
  if (count === 0) {
    return 'Sin consultas previas'
  }
  return count === 1 ? '1 consulta previa' : `${count} consultas previas`
}

// Historia clínica del paciente: consultas previas (sin la actual) en acordeón, de 5 en 5
export function ConsultationHistory({
  patientId,
  currentConsultationId,
}: ConsultationHistoryProps) {
  const { data: page, error, execute } = useApi(getPatientConsultations)
  const [pageIndex, setPageIndex] = useState(0)

  const load = useCallback(
    () => execute(patientId, { pageIndex, pageSize }),
    [execute, patientId, pageIndex],
  )

  useEffect(() => {
    void load()
  }, [load])

  // La API incluye la consulta actual: se descuenta y se quita de la página (puede quedar con 4)
  const previousCount = page ? Math.max(0, page.count - 1) : 0
  const previous =
    page?.data.filter(
      (consultation) => consultation.id !== currentConsultationId,
    ) ?? []
  const totalPages = page ? Math.max(1, Math.ceil(page.count / pageSize)) : 1

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Historia clínica</CardTitle>
        {page && (
          <CardDescription>
            {formatPreviousCount(previousCount)}
          </CardDescription>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {error ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription className="gap-3">
              <p>{loadHistoryErrorMessage}</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void load()}
              >
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>
        ) : !page ? (
          <div className="flex flex-col gap-2" data-testid="history-skeleton">
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : previousCount === 0 ? (
          <p className="text-sm text-muted-foreground">
            El paciente no tiene consultas previas.
          </p>
        ) : (
          // La key reinicia el acordeón al cambiar de página: todo cerrado
          <Accordion key={page.pageIndex} type="multiple">
            {previous.map((consultation) => (
              <AccordionItem key={consultation.id} value={consultation.id}>
                <AccordionTrigger className="items-center hover:no-underline">
                  <div className="flex min-w-0 flex-1 flex-col gap-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span>
                        {formatLocalDateTime(consultation.startedAtLocal)}
                      </span>
                      <span className="text-muted-foreground">
                        · {consultation.doctorFullName}
                      </span>
                      <ConsultationStatusBadge status={consultation.status} />
                    </div>
                    <span className="truncate font-normal text-muted-foreground">
                      {consultation.reason}
                    </span>
                  </div>
                </AccordionTrigger>
                <AccordionContent className="flex flex-col gap-4">
                  <ConsultationReadOnly consultation={consultation} />
                  {consultation.closedAtLocal && (
                    <p className="text-sm text-muted-foreground">
                      Cerrada el{' '}
                      {formatLocalDateTime(consultation.closedAtLocal)}
                    </p>
                  )}
                </AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        )}

        {page && totalPages > 1 && (
          <div className="flex flex-col items-center justify-between gap-2 text-sm text-muted-foreground sm:flex-row">
            <p>
              Página {page.pageIndex + 1} de {totalPages}
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={page.pageIndex === 0}
                onClick={() => setPageIndex(page.pageIndex - 1)}
              >
                <ChevronLeft />
                Anterior
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={page.pageIndex + 1 >= totalPages}
                onClick={() => setPageIndex(page.pageIndex + 1)}
              >
                Siguiente
                <ChevronRight />
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

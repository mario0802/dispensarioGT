import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Stethoscope,
} from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  canStartConsultation,
  formatLocalDate,
  formatLocalTimeRange,
} from '@/features/appointments/appointment-labels'
import { getAppointmentsLoadErrorMessage } from '@/features/appointments/appointment-messages'
import type { AppointmentDto } from '@/features/appointments/appointment-types'
import { AppointmentStatusBadge } from '@/features/appointments/components/appointment-status-badge'
import type { ApiError } from '@/lib/api/api-error'
import type { PaginatedResult } from '@/lib/api/paginated-result'

interface AppointmentsTableProps {
  page: PaginatedResult<AppointmentDto> | null
  error: ApiError | null
  hasCustomFilters: boolean
  onRetry: () => void
  onPageChange: (pageIndex: number) => void
  // Texto sin citas con los filtros por defecto
  emptyText?: string
  // El rol Doctor no ve la columna: siempre es él
  showDoctorColumn?: boolean
  // Con ella hay columna Acciones y las filas iniciables (hoy, Scheduled/Confirmed) se pueden pulsar
  onStart?: (appointment: AppointmentDto) => void
  // Fecha local "yyyy-MM-dd" con la que se decide qué citas son de hoy
  today?: string
  // Para el doctor, el 403 tiene un mensaje propio
  isDoctor?: boolean
}

// Tabla de citas con sus estados (carga, error, vacío) y la paginación
export function AppointmentsTable({
  page,
  error,
  hasCustomFilters,
  onRetry,
  onPageChange,
  emptyText = 'No hay citas próximas.',
  showDoctorColumn = true,
  onStart,
  today = '',
  isDoctor = false,
}: AppointmentsTableProps) {
  const columnCount = 4 + (showDoctorColumn ? 1 : 0) + (onStart ? 1 : 0)

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertTitle>No se pudieron cargar las citas.</AlertTitle>
        <AlertDescription className="gap-3">
          <p>{getAppointmentsLoadErrorMessage(error, isDoctor)}</p>
          <Button type="button" variant="outline" size="sm" onClick={onRetry}>
            Reintentar
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  const totalPages = page
    ? Math.max(1, Math.ceil(page.count / page.pageSize))
    : 1

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Fecha y hora</TableHead>
              <TableHead>Paciente</TableHead>
              {showDoctorColumn && <TableHead>Doctor</TableHead>}
              <TableHead>Motivo</TableHead>
              <TableHead>Estado</TableHead>
              {onStart && (
                <TableHead>
                  <span className="sr-only">Acciones</span>
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {/* Solo la primera carga: al recargar se conservan las filas anteriores */}
            {!page &&
              Array.from({ length: 5 }, (_, index) => (
                <TableRow key={index} data-testid="appointment-row-skeleton">
                  <TableCell colSpan={columnCount}>
                    <Skeleton className="h-8 w-full" />
                  </TableCell>
                </TableRow>
              ))}

            {page?.data.length === 0 && (
              <TableRow>
                <TableCell
                  colSpan={columnCount}
                  className="h-24 text-center text-muted-foreground"
                >
                  {hasCustomFilters
                    ? 'No se encontraron citas con esos filtros.'
                    : emptyText}
                </TableCell>
              </TableRow>
            )}

            {page?.data.map((appointment) => {
              const startable =
                onStart !== undefined &&
                canStartConsultation(appointment, today)
              return (
                <TableRow
                  key={appointment.id}
                  className={startable ? 'cursor-pointer' : undefined}
                  onClick={startable ? () => onStart(appointment) : undefined}
                >
                  <TableCell>
                    <div className="font-medium">
                      {formatLocalDate(appointment.startLocal)}
                    </div>
                    <div className="text-muted-foreground">
                      {formatLocalTimeRange(
                        appointment.startLocal,
                        appointment.endLocal,
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{appointment.patientFullName}</TableCell>
                  {showDoctorColumn && (
                    <TableCell>{appointment.doctorFullName}</TableCell>
                  )}
                  <TableCell>
                    <div
                      className="max-w-64 truncate"
                      title={appointment.reason}
                    >
                      {appointment.reason}
                    </div>
                  </TableCell>
                  <TableCell>
                    <AppointmentStatusBadge status={appointment.status} />
                  </TableCell>
                  {onStart && (
                    <TableCell className="text-right">
                      {startable && (
                        <Button
                          type="button"
                          size="sm"
                          aria-label={`Iniciar consulta de ${appointment.patientFullName}`}
                          onClick={(event) => {
                            // La fila también abre la confirmación: se evita abrirla dos veces
                            event.stopPropagation()
                            onStart(appointment)
                          }}
                        >
                          <Stethoscope />
                          Iniciar
                        </Button>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>

      {page && page.count > 0 && (
        <div className="flex flex-col items-center justify-between gap-2 text-sm text-muted-foreground sm:flex-row">
          <p>
            Página {page.pageIndex + 1} de {totalPages} · {page.count}{' '}
            {page.count === 1 ? 'cita' : 'citas'}
          </p>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page.pageIndex === 0}
              onClick={() => onPageChange(page.pageIndex - 1)}
            >
              <ChevronLeft />
              Anterior
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page.pageIndex + 1 >= totalPages}
              onClick={() => onPageChange(page.pageIndex + 1)}
            >
              Siguiente
              <ChevronRight />
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

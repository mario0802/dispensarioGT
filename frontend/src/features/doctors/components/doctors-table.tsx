import { AlertCircle, ChevronLeft, ChevronRight } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
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
import { DoctorRowActions } from '@/features/doctors/components/doctor-row-actions'
import {
  getDoctorFullName,
  getDoctorsLoadErrorMessage,
} from '@/features/doctors/doctor-messages'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import type { ApiError } from '@/lib/api/api-error'
import type { PaginatedResult } from '@/lib/api/paginated-result'

const columnCount = 7

interface DoctorsTableProps {
  page: PaginatedResult<DoctorDto> | null
  error: ApiError | null
  hasFilters: boolean
  onRetry: () => void
  onPageChange: (pageIndex: number) => void
  canManage: boolean
  onSchedule: (doctor: DoctorDto) => void
  onEdit: (doctor: DoctorDto) => void
  onToggleStatus: (doctor: DoctorDto) => void
}

// Tabla de doctores con sus estados (carga, error, vacío) y la paginación
export function DoctorsTable({
  page,
  error,
  hasFilters,
  onRetry,
  onPageChange,
  canManage,
  onSchedule,
  onEdit,
  onToggleStatus,
}: DoctorsTableProps) {
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertTitle>No se pudieron cargar los doctores.</AlertTitle>
        <AlertDescription className="gap-3">
          <p>{getDoctorsLoadErrorMessage(error)}</p>
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
              <TableHead>Doctor</TableHead>
              <TableHead>Matrícula</TableHead>
              <TableHead>Especialidad</TableHead>
              <TableHead>Teléfono</TableHead>
              <TableHead>Duración</TableHead>
              <TableHead>Estado</TableHead>
              <TableHead>
                <span className="sr-only">Acciones</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {/* Solo la primera carga: al recargar se conservan las filas anteriores */}
            {!page &&
              Array.from({ length: 5 }, (_, index) => (
                <TableRow key={index} data-testid="doctor-row-skeleton">
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
                  {hasFilters
                    ? 'No se encontraron doctores con esos filtros.'
                    : 'Todavía no hay doctores registrados.'}
                </TableCell>
              </TableRow>
            )}

            {page?.data.map((doctor) => (
              <TableRow key={doctor.id}>
                <TableCell>
                  <div className="font-medium">{getDoctorFullName(doctor)}</div>
                  <div className="text-muted-foreground">{doctor.email}</div>
                </TableCell>
                <TableCell>{doctor.licenseNumber}</TableCell>
                <TableCell>{doctor.specialty}</TableCell>
                <TableCell>{doctor.phone ?? '—'}</TableCell>
                <TableCell>{doctor.appointmentDurationMinutes} min</TableCell>
                <TableCell>
                  {doctor.isActive ? (
                    <Badge>Activo</Badge>
                  ) : (
                    <Badge variant="secondary">Inactivo</Badge>
                  )}
                </TableCell>
                <TableCell>
                  <DoctorRowActions
                    doctor={doctor}
                    canManage={canManage}
                    onSchedule={onSchedule}
                    onEdit={onEdit}
                    onToggleStatus={onToggleStatus}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {page && page.count > 0 && (
        <div className="flex flex-col items-center justify-between gap-2 text-sm text-muted-foreground sm:flex-row">
          <p>
            Página {page.pageIndex + 1} de {totalPages} · {page.count}{' '}
            {page.count === 1 ? 'doctor' : 'doctores'}
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

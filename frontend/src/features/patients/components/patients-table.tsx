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
import { PatientRowActions } from '@/features/patients/components/patient-row-actions'
import {
  formatAge,
  formatBirthDate,
  formatDocument,
  getAge,
  getPatientFullName,
} from '@/features/patients/patient-labels'
import { getPatientsLoadErrorMessage } from '@/features/patients/patient-messages'
import type { PatientDto } from '@/features/patients/patient-types'
import type { ApiError } from '@/lib/api/api-error'
import type { PaginatedResult } from '@/lib/api/paginated-result'

const columnCount = 6

interface PatientsTableProps {
  page: PaginatedResult<PatientDto> | null
  error: ApiError | null
  hasFilters: boolean
  onRetry: () => void
  onPageChange: (pageIndex: number) => void
  onEdit: (patient: PatientDto) => void
  onToggleStatus: (patient: PatientDto) => void
}

// Tabla de pacientes con sus estados (carga, error, vacío) y la paginación
export function PatientsTable({
  page,
  error,
  hasFilters,
  onRetry,
  onPageChange,
  onEdit,
  onToggleStatus,
}: PatientsTableProps) {
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertTitle>No se pudieron cargar los pacientes.</AlertTitle>
        <AlertDescription className="gap-3">
          <p>{getPatientsLoadErrorMessage(error)}</p>
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
  const today = new Date()

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Paciente</TableHead>
              <TableHead>Documento</TableHead>
              <TableHead>Edad</TableHead>
              <TableHead>Teléfono</TableHead>
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
                <TableRow key={index} data-testid="patient-row-skeleton">
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
                    ? 'No se encontraron pacientes con esos filtros.'
                    : 'Todavía no hay pacientes registrados.'}
                </TableCell>
              </TableRow>
            )}

            {page?.data.map((patient) => (
              <TableRow key={patient.id}>
                <TableCell>
                  <div className="font-medium">
                    {getPatientFullName(patient)}
                  </div>
                  {patient.email && (
                    <div className="text-muted-foreground">{patient.email}</div>
                  )}
                </TableCell>
                <TableCell>{formatDocument(patient)}</TableCell>
                <TableCell>
                  <div>{formatAge(getAge(patient.birthDate, today))}</div>
                  <div className="text-muted-foreground">
                    {formatBirthDate(patient.birthDate)}
                  </div>
                </TableCell>
                <TableCell>{patient.phone ?? '—'}</TableCell>
                <TableCell>
                  {patient.isActive ? (
                    <Badge>Activo</Badge>
                  ) : (
                    <Badge variant="secondary">Inactivo</Badge>
                  )}
                </TableCell>
                <TableCell>
                  <PatientRowActions
                    patient={patient}
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
            {page.count === 1 ? 'paciente' : 'pacientes'}
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

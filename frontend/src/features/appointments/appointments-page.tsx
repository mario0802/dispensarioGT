import { Info } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { toLocalDateString } from '@/features/appointments/appointment-labels'
import type { AppointmentDto } from '@/features/appointments/appointment-types'
import { getAppointments } from '@/features/appointments/appointments-api'
import {
  AppointmentsFilters,
  type AppointmentStatusFilter,
} from '@/features/appointments/components/appointments-filters'
import { AppointmentFormDialog } from '@/features/appointments/components/appointment-form-dialog'
import { AppointmentsTable } from '@/features/appointments/components/appointments-table'
import type { SearchComboboxOption } from '@/features/appointments/components/search-combobox'
import { StartConsultationDialog } from '@/features/appointments/components/start-consultation-dialog'
import { useAuth } from '@/features/auth/use-auth'
import { useApi } from '@/hooks/use-api'

const pageSize = 10

// Página /appointments: Admin y Recepción listan y agendan citas; el Doctor ve las suyas
// e inicia la consulta de las de hoy
export function AppointmentsPage() {
  const { user } = useAuth()
  const isDoctor = user?.role === 'Doctor'

  // Sin ficha vinculada la API respondería 403: no se consulta
  if (isDoctor && !user.doctorId) {
    return (
      <div className="flex max-w-3xl flex-col gap-4 p-6">
        <Alert>
          <Info />
          <AlertDescription>
            Tu usuario todavía no está vinculado a una ficha de doctor. Pide al
            administrador que lo vincule.
          </AlertDescription>
        </Alert>
      </div>
    )
  }

  return <AppointmentsList isDoctor={isDoctor} />
}

function AppointmentsList({ isDoctor }: { isDoctor: boolean }) {
  const { data, error, execute } = useApi(getAppointments)

  // Por defecto, las citas desde hoy (la API ordena por inicio ascendente); al doctor, solo las de hoy
  const [today] = useState(() => toLocalDateString(new Date()))
  const defaultTo = isDoctor ? today : ''
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(defaultTo)
  const [status, setStatus] = useState<AppointmentStatusFilter>('all')
  const [doctor, setDoctor] = useState<SearchComboboxOption | null>(null)
  const [patient, setPatient] = useState<SearchComboboxOption | null>(null)
  const [isCreating, setIsCreating] = useState(false)
  const [starting, setStarting] = useState<AppointmentDto | null>(null)

  // Las cadenas "yyyy-MM-dd" se comparan bien como texto
  const dateRangeInvalid = from !== '' && to !== '' && from > to

  // La página va ligada a los filtros: al cambiar estos se vuelve a la primera (sin efecto extra)
  const filterKey = [from, to, status, doctor?.id, patient?.id].join('|')
  const [page, setPage] = useState({ filterKey, index: 0 })
  const pageIndex = page.filterKey === filterKey ? page.index : 0
  const setPageIndex = (index: number) => setPage({ filterKey, index })

  const hasCustomFilters =
    from !== today ||
    to !== defaultTo ||
    status !== 'all' ||
    doctor !== null ||
    patient !== null

  // Al doctor no se le envía doctorId: la API devuelve solo las del doctor del token
  const load = useCallback(
    () =>
      execute({
        pageIndex,
        pageSize,
        from: from || undefined,
        to: to || undefined,
        status: status === 'all' ? undefined : status,
        doctorId: doctor?.id,
        patientId: patient?.id,
      }),
    [execute, pageIndex, from, to, status, doctor?.id, patient?.id],
  )

  // Con el rango invertido no se consulta: la tabla conserva las filas anteriores
  useEffect(() => {
    if (!dateRangeInvalid) {
      void load()
    }
  }, [load, dateRangeInvalid])

  const clearFilters = () => {
    setFrom(today)
    setTo(defaultTo)
    setStatus('all')
    setDoctor(null)
    setPatient(null)
  }

  return (
    <div className="flex flex-col gap-4 p-6">
      <AppointmentsFilters
        from={from}
        onFromChange={setFrom}
        to={to}
        onToChange={setTo}
        dateRangeInvalid={dateRangeInvalid}
        status={status}
        onStatusChange={setStatus}
        doctor={doctor}
        onDoctorChange={setDoctor}
        patient={patient}
        onPatientChange={setPatient}
        canClear={hasCustomFilters}
        onClear={clearFilters}
        showDoctorFilter={!isDoctor}
        onCreate={isDoctor ? undefined : () => setIsCreating(true)}
      />

      <AppointmentsTable
        page={data}
        error={error}
        hasCustomFilters={hasCustomFilters}
        onRetry={() => void load()}
        onPageChange={setPageIndex}
        emptyText={isDoctor ? 'No tienes citas hoy.' : undefined}
        showDoctorColumn={!isDoctor}
        onStart={isDoctor ? setStarting : undefined}
        today={today}
        isDoctor={isDoctor}
      />

      {isCreating && (
        <AppointmentFormDialog
          onClose={() => setIsCreating(false)}
          onSaved={() => void load()}
        />
      )}

      {starting && (
        <StartConsultationDialog
          appointment={starting}
          onClose={() => setStarting(null)}
          onFailed={() => void load()}
        />
      )}
    </div>
  )
}

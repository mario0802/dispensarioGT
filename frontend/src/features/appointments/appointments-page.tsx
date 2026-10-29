import { useCallback, useEffect, useState } from 'react'

import { toLocalDateString } from '@/features/appointments/appointment-labels'
import { getAppointments } from '@/features/appointments/appointments-api'
import {
  AppointmentsFilters,
  type AppointmentStatusFilter,
} from '@/features/appointments/components/appointments-filters'
import { AppointmentFormDialog } from '@/features/appointments/components/appointment-form-dialog'
import { AppointmentsTable } from '@/features/appointments/components/appointments-table'
import type { SearchComboboxOption } from '@/features/appointments/components/search-combobox'
import { useApi } from '@/hooks/use-api'

const pageSize = 10

// Página /appointments: Admin y Recepción listan y agendan citas
export function AppointmentsPage() {
  const { data, error, execute } = useApi(getAppointments)

  // Por defecto, las citas desde hoy: la API ordena por inicio ascendente
  const [today] = useState(() => toLocalDateString(new Date()))
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState('')
  const [status, setStatus] = useState<AppointmentStatusFilter>('all')
  const [doctor, setDoctor] = useState<SearchComboboxOption | null>(null)
  const [patient, setPatient] = useState<SearchComboboxOption | null>(null)
  const [isCreating, setIsCreating] = useState(false)

  // Las cadenas "yyyy-MM-dd" se comparan bien como texto
  const dateRangeInvalid = from !== '' && to !== '' && from > to

  // La página va ligada a los filtros: al cambiar estos se vuelve a la primera (sin efecto extra)
  const filterKey = [from, to, status, doctor?.id, patient?.id].join('|')
  const [page, setPage] = useState({ filterKey, index: 0 })
  const pageIndex = page.filterKey === filterKey ? page.index : 0
  const setPageIndex = (index: number) => setPage({ filterKey, index })

  const hasCustomFilters =
    from !== today ||
    to !== '' ||
    status !== 'all' ||
    doctor !== null ||
    patient !== null

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
    setTo('')
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
        onCreate={() => setIsCreating(true)}
      />

      <AppointmentsTable
        page={data}
        error={error}
        hasCustomFilters={hasCustomFilters}
        onRetry={() => void load()}
        onPageChange={setPageIndex}
      />

      {isCreating && (
        <AppointmentFormDialog
          onClose={() => setIsCreating(false)}
          onSaved={() => void load()}
        />
      )}
    </div>
  )
}

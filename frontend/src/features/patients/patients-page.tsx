import { useCallback, useEffect, useState } from 'react'

import {
  PatientFormDialog,
  type PatientFormTarget,
} from '@/features/patients/components/patient-form-dialog'
import { PatientStatusDialog } from '@/features/patients/components/patient-status-dialog'
import { PatientsTable } from '@/features/patients/components/patients-table'
import {
  PatientsToolbar,
  type PatientStatusFilter,
} from '@/features/patients/components/patients-toolbar'
import type { PatientDto } from '@/features/patients/patient-types'
import { getPatients } from '@/features/patients/patients-api'
import { useApi } from '@/hooks/use-api'
import { useDebouncedValue } from '@/hooks/use-debounced-value'

const pageSize = 10
const searchDebounceMs = 300

const statusToIsActive: Record<PatientStatusFilter, boolean | undefined> = {
  all: undefined,
  active: true,
  inactive: false,
}

// Página /patients: Admin y Recepción listan, crean, editan, activan y desactivan
export function PatientsPage() {
  const { data, error, execute } = useApi(getPatients)

  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<PatientStatusFilter>('all')
  const debouncedSearch = useDebouncedValue(search.trim(), searchDebounceMs)
  const isActive = statusToIsActive[status]

  // La página va ligada a los filtros: al cambiar estos se vuelve a la primera (sin efecto extra)
  const filterKey = `${debouncedSearch}|${status}`
  const [page, setPage] = useState({ filterKey, index: 0 })
  const pageIndex = page.filterKey === filterKey ? page.index : 0
  const setPageIndex = (index: number) => setPage({ filterKey, index })

  const [formTarget, setFormTarget] = useState<PatientFormTarget | null>(null)
  const [statusTarget, setStatusTarget] = useState<PatientDto | null>(null)

  const load = useCallback(
    () =>
      execute({
        pageIndex,
        pageSize,
        search: debouncedSearch || undefined,
        isActive,
      }),
    [execute, pageIndex, debouncedSearch, isActive],
  )

  useEffect(() => {
    void load()
  }, [load])

  // Tras una acción: misma consulta; si la página queda vacía y no es la primera, se retrocede una
  const refresh = async () => {
    const result = await load()
    if (result.data?.data.length === 0 && pageIndex > 0) {
      setPageIndex(pageIndex - 1)
    }
  }

  return (
    <div className="flex flex-col gap-4 p-6">
      <PatientsToolbar
        search={search}
        onSearchChange={setSearch}
        status={status}
        onStatusChange={setStatus}
        onCreate={() => setFormTarget({ mode: 'create' })}
      />

      <PatientsTable
        page={data}
        error={error}
        hasFilters={debouncedSearch !== '' || status !== 'all'}
        onRetry={() => void load()}
        onPageChange={setPageIndex}
        onEdit={(patient) => setFormTarget({ mode: 'edit', patient })}
        onToggleStatus={setStatusTarget}
      />

      {formTarget && (
        <PatientFormDialog
          target={formTarget}
          onClose={() => setFormTarget(null)}
          onSaved={() => void refresh()}
        />
      )}

      {statusTarget && (
        <PatientStatusDialog
          patient={statusTarget}
          onClose={() => setStatusTarget(null)}
          onDone={() => void refresh()}
        />
      )}
    </div>
  )
}

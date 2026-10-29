import { useCallback, useEffect, useState } from 'react'

import { useAuth } from '@/features/auth/use-auth'
import {
  DoctorFormDialog,
  type DoctorFormTarget,
} from '@/features/doctors/components/doctor-form-dialog'
import { DoctorScheduleDialog } from '@/features/doctors/components/doctor-schedule-dialog'
import { DoctorStatusDialog } from '@/features/doctors/components/doctor-status-dialog'
import {
  DoctorsToolbar,
  type DoctorStatusFilter,
} from '@/features/doctors/components/doctors-toolbar'
import { DoctorsTable } from '@/features/doctors/components/doctors-table'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import { getDoctors } from '@/features/doctors/doctors-api'
import { useDebouncedValue } from '@/features/doctors/use-debounced-value'
import { useApi } from '@/hooks/use-api'

const pageSize = 10
const searchDebounceMs = 300

const statusToIsActive: Record<DoctorStatusFilter, boolean | undefined> = {
  all: undefined,
  active: true,
  inactive: false,
}

// Página /doctors: listado para Admin y Recepción; solo el Admin crea, edita, activa y desactiva
export function DoctorsPage() {
  const { user } = useAuth()
  const { data, error, execute } = useApi(getDoctors)

  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<DoctorStatusFilter>('all')
  const debouncedSearch = useDebouncedValue(search.trim(), searchDebounceMs)
  const isActive = statusToIsActive[status]

  // La página va ligada a los filtros: al cambiar estos se vuelve a la primera (sin efecto extra)
  const filterKey = `${debouncedSearch}|${status}`
  const [page, setPage] = useState({ filterKey, index: 0 })
  const pageIndex = page.filterKey === filterKey ? page.index : 0
  const setPageIndex = (index: number) => setPage({ filterKey, index })

  const [formTarget, setFormTarget] = useState<DoctorFormTarget | null>(null)
  const [statusTarget, setStatusTarget] = useState<DoctorDto | null>(null)
  const [scheduleTarget, setScheduleTarget] = useState<DoctorDto | null>(null)

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

  if (!user) {
    return null
  }
  const canManage = user.role === 'Admin'

  return (
    <div className="flex flex-col gap-4 p-6">
      <DoctorsToolbar
        search={search}
        onSearchChange={setSearch}
        status={status}
        onStatusChange={setStatus}
        canManage={canManage}
        onCreate={() => setFormTarget({ mode: 'create' })}
      />

      <DoctorsTable
        page={data}
        error={error}
        hasFilters={debouncedSearch !== '' || status !== 'all'}
        onRetry={() => void load()}
        onPageChange={setPageIndex}
        canManage={canManage}
        onSchedule={setScheduleTarget}
        onEdit={(doctor) => setFormTarget({ mode: 'edit', doctor })}
        onToggleStatus={setStatusTarget}
      />

      {formTarget && (
        <DoctorFormDialog
          target={formTarget}
          onClose={() => setFormTarget(null)}
          onSaved={() => void refresh()}
        />
      )}

      {statusTarget && (
        <DoctorStatusDialog
          doctor={statusTarget}
          onClose={() => setStatusTarget(null)}
          onDone={() => void refresh()}
        />
      )}

      {scheduleTarget && (
        <DoctorScheduleDialog
          doctor={scheduleTarget}
          onClose={() => setScheduleTarget(null)}
        />
      )}
    </div>
  )
}

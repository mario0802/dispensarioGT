import { Plus, Search } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export type DoctorStatusFilter = 'all' | 'active' | 'inactive'

const statusFilterLabels: Record<DoctorStatusFilter, string> = {
  all: 'Todos',
  active: 'Activos',
  inactive: 'Inactivos',
}

const statusFilters = Object.keys(statusFilterLabels) as DoctorStatusFilter[]

interface DoctorsToolbarProps {
  search: string
  onSearchChange: (search: string) => void
  status: DoctorStatusFilter
  onStatusChange: (status: DoctorStatusFilter) => void
  canManage: boolean
  onCreate: () => void
}

// Búsqueda, filtro de estado y, para el Admin, el botón de alta
export function DoctorsToolbar({
  search,
  onSearchChange,
  status,
  onStatusChange,
  canManage,
  onCreate,
}: DoctorsToolbarProps) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="relative sm:max-w-sm sm:flex-1">
        <Search
          className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          className="pl-8"
          placeholder="Buscar por nombre, matrícula o especialidad"
          aria-label="Buscar doctores"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
        />
      </div>

      <Select
        value={status}
        onValueChange={(value) => onStatusChange(value as DoctorStatusFilter)}
      >
        <SelectTrigger
          className="w-full sm:w-40"
          aria-label="Filtrar por estado"
        >
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {statusFilters.map((filter) => (
            <SelectItem key={filter} value={filter}>
              {statusFilterLabels[filter]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {canManage && (
        <Button type="button" className="sm:ml-auto" onClick={onCreate}>
          <Plus />
          Nuevo doctor
        </Button>
      )}
    </div>
  )
}

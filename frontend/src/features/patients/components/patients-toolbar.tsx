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

export type PatientStatusFilter = 'all' | 'active' | 'inactive'

const statusFilterLabels: Record<PatientStatusFilter, string> = {
  all: 'Todos',
  active: 'Activos',
  inactive: 'Inactivos',
}

const statusFilters = Object.keys(statusFilterLabels) as PatientStatusFilter[]

interface PatientsToolbarProps {
  search: string
  onSearchChange: (search: string) => void
  status: PatientStatusFilter
  onStatusChange: (status: PatientStatusFilter) => void
  onCreate: () => void
}

// Búsqueda, filtro de estado y botón de alta
export function PatientsToolbar({
  search,
  onSearchChange,
  status,
  onStatusChange,
  onCreate,
}: PatientsToolbarProps) {
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
          placeholder="Buscar por nombre o documento"
          aria-label="Buscar pacientes"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
        />
      </div>

      <Select
        value={status}
        onValueChange={(value) => onStatusChange(value as PatientStatusFilter)}
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

      <Button type="button" className="sm:ml-auto" onClick={onCreate}>
        <Plus />
        Nuevo paciente
      </Button>
    </div>
  )
}

import {
  CalendarClock,
  MoreHorizontal,
  Pencil,
  UserCheck,
  UserX,
} from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { getDoctorFullName } from '@/features/doctors/doctor-messages'
import type { DoctorDto } from '@/features/doctors/doctor-types'

interface DoctorRowActionsProps {
  doctor: DoctorDto
  canManage: boolean
  onSchedule: (doctor: DoctorDto) => void
  onEdit: (doctor: DoctorDto) => void
  onToggleStatus: (doctor: DoctorDto) => void
}

// Acciones de una fila: el cronograma para todos y, solo para el Admin, el menú ⋯
export function DoctorRowActions({
  doctor,
  canManage,
  onSchedule,
  onEdit,
  onToggleStatus,
}: DoctorRowActionsProps) {
  const fullName = getDoctorFullName(doctor)
  const scheduleLabel = `Ver cronograma de ${fullName}`

  return (
    <div className="flex items-center justify-end gap-1">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={scheduleLabel}
            onClick={() => onSchedule(doctor)}
          >
            <CalendarClock />
          </Button>
        </TooltipTrigger>
        <TooltipContent>{scheduleLabel}</TooltipContent>
      </Tooltip>

      {canManage && (
        // No modal: así el foco y los punteros vuelven bien al abrir un diálogo desde el menú
        <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label={`Más acciones de ${fullName}`}
            >
              <MoreHorizontal />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onSelect={() => onEdit(doctor)}>
              <Pencil />
              Editar
            </DropdownMenuItem>
            <DropdownMenuItem onSelect={() => onToggleStatus(doctor)}>
              {doctor.isActive ? (
                <>
                  <UserX />
                  Desactivar
                </>
              ) : (
                <>
                  <UserCheck />
                  Activar
                </>
              )}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}

import { MoreHorizontal, Pencil, UserCheck, UserX } from 'lucide-react'

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
import { getPatientFullName } from '@/features/patients/patient-labels'
import type { PatientDto } from '@/features/patients/patient-types'

interface PatientRowActionsProps {
  patient: PatientDto
  onEdit: (patient: PatientDto) => void
  onToggleStatus: (patient: PatientDto) => void
}

// Acciones de una fila: el lápiz para editar y el menú ⋯ con Activar/Desactivar
export function PatientRowActions({
  patient,
  onEdit,
  onToggleStatus,
}: PatientRowActionsProps) {
  const fullName = getPatientFullName(patient)
  const editLabel = `Editar a ${fullName}`

  return (
    <div className="flex items-center justify-end gap-1">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={editLabel}
            onClick={() => onEdit(patient)}
          >
            <Pencil />
          </Button>
        </TooltipTrigger>
        <TooltipContent>{editLabel}</TooltipContent>
      </Tooltip>

      {/* No modal: así el foco y los punteros vuelven bien al abrir un diálogo desde el menú */}
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
          <DropdownMenuItem onSelect={() => onToggleStatus(patient)}>
            {patient.isActive ? (
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
    </div>
  )
}

import { Plus, X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  appointmentStatuses,
  statusLabels,
} from '@/features/appointments/appointment-labels'
import type { AppointmentStatus } from '@/features/appointments/appointment-types'
import { DoctorCombobox } from '@/features/appointments/components/doctor-combobox'
import { PatientCombobox } from '@/features/appointments/components/patient-combobox'
import type { SearchComboboxOption } from '@/features/appointments/components/search-combobox'

export type AppointmentStatusFilter = 'all' | AppointmentStatus

export const invalidDateRangeMessage =
  'La fecha final no puede ser anterior a la inicial.'

interface AppointmentsFiltersProps {
  from: string
  onFromChange: (from: string) => void
  to: string
  onToChange: (to: string) => void
  dateRangeInvalid: boolean
  status: AppointmentStatusFilter
  onStatusChange: (status: AppointmentStatusFilter) => void
  doctor: SearchComboboxOption | null
  onDoctorChange: (doctor: SearchComboboxOption | null) => void
  patient: SearchComboboxOption | null
  onPatientChange: (patient: SearchComboboxOption | null) => void
  canClear: boolean
  onClear: () => void
  onCreate: () => void
}

// Filtros del listado (fechas, estado, doctor y paciente) y botón de alta
export function AppointmentsFilters({
  from,
  onFromChange,
  to,
  onToChange,
  dateRangeInvalid,
  status,
  onStatusChange,
  doctor,
  onDoctorChange,
  patient,
  onPatientChange,
  canClear,
  onClear,
  onCreate,
}: AppointmentsFiltersProps) {
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
        <Button
          type="button"
          variant="ghost"
          disabled={!canClear}
          onClick={onClear}
        >
          <X />
          Limpiar filtros
        </Button>
        <Button type="button" onClick={onCreate}>
          <Plus />
          Nueva cita
        </Button>
      </div>

      <div className="grid gap-3 md:grid-cols-3">
        <Field>
          <FieldLabel htmlFor="appointments-from">Desde</FieldLabel>
          <Input
            id="appointments-from"
            type="date"
            value={from}
            onChange={(event) => onFromChange(event.target.value)}
          />
        </Field>
        <Field data-invalid={dateRangeInvalid}>
          <FieldLabel htmlFor="appointments-to">Hasta</FieldLabel>
          <Input
            id="appointments-to"
            type="date"
            value={to}
            aria-invalid={dateRangeInvalid}
            onChange={(event) => onToChange(event.target.value)}
          />
          {dateRangeInvalid && (
            <FieldError>{invalidDateRangeMessage}</FieldError>
          )}
        </Field>
        <Field>
          <FieldLabel htmlFor="appointments-status">Estado</FieldLabel>
          <Select
            value={status}
            onValueChange={(value) =>
              onStatusChange(value as AppointmentStatusFilter)
            }
          >
            <SelectTrigger
              id="appointments-status"
              className="w-full"
              aria-label="Filtrar por estado"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Todos</SelectItem>
              {appointmentStatuses.map((value) => (
                <SelectItem key={value} value={value}>
                  {statusLabels[value]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Field>
          <FieldLabel htmlFor="appointments-doctor">Doctor</FieldLabel>
          <DoctorCombobox
            id="appointments-doctor"
            value={doctor}
            onChange={onDoctorChange}
            ariaLabel="Filtrar por doctor"
            clearable
          />
        </Field>
        <Field>
          <FieldLabel htmlFor="appointments-patient">Paciente</FieldLabel>
          <PatientCombobox
            id="appointments-patient"
            value={patient}
            onChange={onPatientChange}
            ariaLabel="Filtrar por paciente"
            clearable
          />
        </Field>
      </div>
    </div>
  )
}

import { useRef } from 'react'

import {
  SearchCombobox,
  type SearchComboboxOption,
} from '@/features/appointments/components/search-combobox'
import type { DoctorDto } from '@/features/doctors/doctor-types'
import { getDoctors } from '@/features/doctors/doctors-api'

interface DoctorComboboxProps {
  value: SearchComboboxOption | null
  onChange: (option: SearchComboboxOption | null) => void
  // El doctor completo, para mostrar la especialidad y la duración
  onDoctorChange?: (doctor: DoctorDto | null) => void
  ariaLabel: string
  clearable?: boolean
  invalid?: boolean
  id?: string
}

// Combobox de doctores activos, con búsqueda por nombre, matrícula o especialidad
export function DoctorCombobox({
  value,
  onChange,
  onDoctorChange,
  ariaLabel,
  clearable,
  invalid,
  id,
}: DoctorComboboxProps) {
  // Doctores de la última búsqueda, para devolver el elegido completo
  const doctorsRef = useRef(new Map<string, DoctorDto>())

  const search = async (text: string) => {
    const page = await getDoctors({
      pageIndex: 0,
      pageSize: 10,
      search: text || undefined,
      isActive: true,
    })
    doctorsRef.current = new Map(page.data.map((doctor) => [doctor.id, doctor]))
    return page.data.map((doctor) => ({
      id: doctor.id,
      label: `${doctor.firstName} ${doctor.lastName}`,
      description: `${doctor.specialty} · ${doctor.licenseNumber}`,
    }))
  }

  return (
    <SearchCombobox
      id={id}
      value={value}
      onChange={(option) => {
        onChange(option)
        onDoctorChange?.(
          option ? (doctorsRef.current.get(option.id) ?? null) : null,
        )
      }}
      search={search}
      placeholder="Selecciona un doctor"
      searchPlaceholder="Buscar por nombre o especialidad"
      emptyText="No se encontraron doctores activos."
      ariaLabel={ariaLabel}
      clearable={clearable}
      clearLabel="Todos los doctores"
      invalid={invalid}
    />
  )
}

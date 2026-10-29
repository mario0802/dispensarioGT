import {
  SearchCombobox,
  type SearchComboboxOption,
} from '@/features/appointments/components/search-combobox'
import {
  formatDocument,
  getPatientFullName,
} from '@/features/patients/patient-labels'
import { getPatients } from '@/features/patients/patients-api'

interface PatientComboboxProps {
  value: SearchComboboxOption | null
  onChange: (option: SearchComboboxOption | null) => void
  ariaLabel: string
  clearable?: boolean
  invalid?: boolean
  id?: string
}

// Busca entre los pacientes activos por nombre o documento
const searchPatients = async (text: string) => {
  const page = await getPatients({
    pageIndex: 0,
    pageSize: 10,
    search: text || undefined,
    isActive: true,
  })
  return page.data.map((patient) => ({
    id: patient.id,
    label: getPatientFullName(patient),
    description: formatDocument(patient),
  }))
}

// Combobox de pacientes activos
export function PatientCombobox({
  value,
  onChange,
  ariaLabel,
  clearable,
  invalid,
  id,
}: PatientComboboxProps) {
  return (
    <SearchCombobox
      id={id}
      value={value}
      onChange={onChange}
      search={searchPatients}
      placeholder="Selecciona un paciente"
      searchPlaceholder="Buscar por nombre o documento"
      emptyText="No se encontraron pacientes activos."
      ariaLabel={ariaLabel}
      clearable={clearable}
      clearLabel="Todos los pacientes"
      invalid={invalid}
    />
  )
}

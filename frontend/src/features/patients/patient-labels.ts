import { documentTypeLabels } from '@/features/auth/document-type-labels'
import type {
  BloodType,
  Gender,
  PatientDto,
} from '@/features/patients/patient-types'

// Nombres de los géneros para la interfaz; la API los envía en inglés
export const genderLabels: Record<Gender, string> = {
  Male: 'Masculino',
  Female: 'Femenino',
  Other: 'Otro',
}

export const genders = Object.keys(genderLabels) as Gender[]

// Se muestran tal cual: la notación clínica es la misma en español
export const bloodTypes: BloodType[] = [
  'A+',
  'A-',
  'B+',
  'B-',
  'AB+',
  'AB-',
  'O+',
  'O-',
]

export function getPatientFullName(
  patient: Pick<PatientDto, 'firstName' | 'lastName'>,
): string {
  return `${patient.firstName} ${patient.lastName}`
}

// "Cédula 0102030405"
export function formatDocument(
  patient: Pick<PatientDto, 'documentType' | 'documentNumber'>,
): string {
  return `${documentTypeLabels[patient.documentType]} ${patient.documentNumber}`
}

// Se parte la cadena "yyyy-MM-dd": new Date() la leería en UTC y podría restar un día
function parseIsoDate(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return { year, month, day }
}

// "yyyy-MM-dd" → "dd/MM/yyyy"
export function formatBirthDate(birthDate: string): string {
  const [year, month, day] = birthDate.split('-')
  return `${day}/${month}/${year}`
}

// Años cumplidos en la fecha local de today
export function getAge(birthDate: string, today: Date): number {
  const { year, month, day } = parseIsoDate(birthDate)
  const todayMonth = today.getMonth() + 1
  const hadBirthday =
    todayMonth > month || (todayMonth === month && today.getDate() >= day)
  return Math.max(0, today.getFullYear() - year - (hadBirthday ? 0 : 1))
}

// "35 años" / "1 año"
export function formatAge(age: number): string {
  return age === 1 ? '1 año' : `${age} años`
}

// Fecha local de date como "yyyy-MM-dd" (para el max del input date y comparar)
export function toLocalIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

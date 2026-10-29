import { AlertCircle, TriangleAlert } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { DetailItem } from '@/features/consultations/components/consultation-details'
import { loadPatientErrorMessage } from '@/features/consultations/consultation-messages'
import {
  formatAge,
  formatBirthDate,
  formatDocument,
  genderLabels,
  getAge,
  getPatientFullName,
} from '@/features/patients/patient-labels'
import { getPatient } from '@/features/patients/patients-api'
import { useApi } from '@/hooks/use-api'

// Tarjeta Paciente: identificación y datos clínicos; carga aparte para no bloquear la consulta
export function PatientSummaryCard({ patientId }: { patientId: string }) {
  const { data: patient, error, execute } = useApi(getPatient)
  const [today] = useState(() => new Date())

  const load = useCallback(() => execute(patientId), [execute, patientId])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Paciente</CardTitle>
      </CardHeader>
      <CardContent>
        {error ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription className="gap-3">
              <p>{loadPatientErrorMessage}</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void load()}
              >
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>
        ) : !patient ? (
          <div className="flex flex-col gap-3" data-testid="patient-skeleton">
            <Skeleton className="h-5 w-1/2" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">{getPatientFullName(patient)}</span>
              {!patient.isActive && <Badge variant="secondary">Inactivo</Badge>}
            </div>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailItem label="Documento">
                {formatDocument(patient)}
              </DetailItem>
              <DetailItem label="Edad">
                {formatAge(getAge(patient.birthDate, today))} ·{' '}
                {formatBirthDate(patient.birthDate)}
              </DetailItem>
              <DetailItem label="Género">
                {genderLabels[patient.gender]}
              </DetailItem>
              <DetailItem label="Tipo de sangre">
                {patient.bloodType ?? '—'}
              </DetailItem>
              <DetailItem label="Teléfono">{patient.phone ?? '—'}</DetailItem>
            </dl>
            {patient.allergies ? (
              <Alert variant="destructive">
                <TriangleAlert />
                <AlertTitle>Alergias</AlertTitle>
                <AlertDescription>
                  <p className="break-words whitespace-pre-wrap">
                    {patient.allergies}
                  </p>
                </AlertDescription>
              </Alert>
            ) : (
              <div>
                <p className="text-sm text-muted-foreground">Alergias</p>
                <p className="mt-0.5 text-sm">Sin alergias registradas.</p>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

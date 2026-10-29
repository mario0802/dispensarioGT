import { AlertCircle, Info } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { DoctorSchedulePanel } from '@/features/doctors/components/doctor-schedule-panel'
import {
  getDoctorFullName,
  getDoctorsLoadErrorMessage,
} from '@/features/doctors/doctor-messages'
import { getDoctor } from '@/features/doctors/doctors-api'
import { useApi } from '@/hooks/use-api'

// Página /my-schedule: el usuario Doctor ve y edita su propio horario
export function MySchedulePage() {
  const { user } = useAuth()
  const doctorId = user?.doctorId ?? null

  return (
    <div className="flex max-w-3xl flex-col gap-4 p-6">
      {doctorId ? (
        <MyDoctorSchedule doctorId={doctorId} />
      ) : (
        <Alert>
          <Info />
          <AlertDescription>
            Tu usuario todavía no está vinculado a una ficha de doctor. Pide al
            administrador que lo vincule.
          </AlertDescription>
        </Alert>
      )}
    </div>
  )
}

function MyDoctorSchedule({ doctorId }: { doctorId: string }) {
  const { data: doctor, error, execute } = useApi(getDoctor)
  // El 404 del horario llega por el panel; el de la ficha, en el error de la carga
  const [scheduleNotFound, setScheduleNotFound] = useState(false)
  const notFound = scheduleNotFound || error?.status === 404

  const load = useCallback(() => execute(doctorId), [execute, doctorId])

  useEffect(() => {
    void load()
  }, [load])

  if (notFound) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>
          No se encontró tu ficha de doctor. Contacta con el administrador.
        </AlertDescription>
      </Alert>
    )
  }

  if (error && !doctor) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription className="gap-3">
          <p>
            No se pudo cargar tu ficha de doctor.{' '}
            {getDoctorsLoadErrorMessage(error)}
          </p>
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
    )
  }

  if (!doctor) {
    return (
      <div className="flex flex-col gap-2" data-testid="my-doctor-skeleton">
        <Skeleton className="h-6 w-2/3" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  return (
    <>
      <p className="text-sm text-muted-foreground">
        <span className="font-medium text-foreground">
          {getDoctorFullName(doctor)}
        </span>{' '}
        · {doctor.specialty} · Citas de {doctor.appointmentDurationMinutes} min
      </p>
      <DoctorSchedulePanel
        doctor={doctor}
        canEdit
        isOwnSchedule
        onNotFound={() => setScheduleNotFound(true)}
      />
    </>
  )
}

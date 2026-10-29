import { AlertCircle, ArrowLeft, Info } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'

import { DiscardChangesDialog } from '@/components/discard-changes-dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { ConsultationDetails } from '@/features/consultations/components/consultation-details'
import { ConsultationForm } from '@/features/consultations/components/consultation-form'
import { ConsultationHistory } from '@/features/consultations/components/consultation-history'
import { ConsultationReadOnly } from '@/features/consultations/components/consultation-read-only'
import { PatientSummaryCard } from '@/features/consultations/components/patient-summary-card'
import { isConsultationEditable } from '@/features/consultations/consultation-labels'
import {
  canRetryLoadConsultation,
  getCloseConsultationErrorMessage,
  getLoadConsultationErrorMessage,
  getSaveConsultationErrorMessage,
  shouldReloadConsultation,
} from '@/features/consultations/consultation-messages'
import type {
  ConsultationDto,
  UpdateConsultationRequest,
} from '@/features/consultations/consultation-types'
import {
  closeConsultation,
  getConsultation,
  updateConsultation,
} from '@/features/consultations/consultations-api'
import { useApi } from '@/hooks/use-api'
import type { ApiError } from '@/lib/api/api-error'

const appointmentsPath = '/appointments'

// Página /consultations/:consultationId: el doctor atiende, registra y finaliza la consulta
export function ConsultationPage() {
  const { consultationId = '' } = useParams()
  const { user } = useAuth()

  // Sin ficha vinculada la API respondería 403: no se consulta
  if (!user?.doctorId) {
    return (
      <div className="flex max-w-3xl flex-col gap-4 p-4 sm:p-6">
        <Alert>
          <Info />
          <AlertDescription>
            Tu usuario todavía no está vinculado a una ficha de doctor. Pide al
            administrador que lo vincule.
          </AlertDescription>
        </Alert>
      </div>
    )
  }

  // La key reinicia la pantalla si se pasa de una consulta a otra
  return (
    <ConsultationScreen
      key={consultationId}
      consultationId={consultationId}
      doctorId={user.doctorId}
    />
  )
}

function ConsultationScreen({
  consultationId,
  doctorId,
}: {
  consultationId: string
  doctorId: string
}) {
  const navigate = useNavigate()
  const consultation = useApi(getConsultation)
  const saving = useApi(updateConsultation)
  const closing = useApi(closeConsultation)
  const { execute: loadConsultation, setData: setConsultation } = consultation
  const [isDirty, setIsDirty] = useState(false)
  const [confirmingLeave, setConfirmingLeave] = useState(false)

  const load = useCallback(
    () => loadConsultation(consultationId),
    [loadConsultation, consultationId],
  )

  useEffect(() => {
    void load()
  }, [load])

  // Con cambios sin guardar, el navegador avisa al cerrar o recargar la pestaña
  useEffect(() => {
    if (!isDirty) {
      return
    }
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [isDirty])

  const applySaved = (request: UpdateConsultationRequest) =>
    setConsultation((previous) => previous && { ...previous, ...request })

  const save = async (request: UpdateConsultationRequest) => {
    const { error } = await saving.execute(consultationId, request)
    if (error) {
      toast.error(getSaveConsultationErrorMessage(error))
      if (shouldReloadConsultation(error)) {
        void load()
      }
      return false
    }
    toast.success('Cambios guardados.')
    applySaved(request)
    return true
  }

  const failFinish = (error: ApiError) => {
    toast.error(getCloseConsultationErrorMessage(error))
    if (shouldReloadConsultation(error)) {
      void load()
    }
  }

  // El cierre no lleva el motivo: si hay cambios se guardan antes para no perderlos
  const finish = async (
    request: UpdateConsultationRequest,
    hasChanges: boolean,
  ) => {
    if (hasChanges) {
      const { error } = await saving.execute(consultationId, request)
      if (error) {
        failFinish(error)
        return false
      }
      applySaved(request)
    }
    const { error } = await closing.execute(consultationId, {
      diagnosis: request.diagnosis ?? '',
      notes: request.notes,
    })
    if (error) {
      failFinish(error)
      return true
    }
    toast.success('Consulta finalizada.')
    navigate(appointmentsPath)
    return true
  }

  const goBack = () => {
    if (isDirty) {
      setConfirmingLeave(true)
    } else {
      navigate(appointmentsPath)
    }
  }

  const backButton = (
    <Button type="button" variant="outline" onClick={goBack}>
      <ArrowLeft />
      Volver a citas
    </Button>
  )

  if (consultation.error) {
    const { error } = consultation
    return (
      <div className="flex max-w-3xl flex-col items-start gap-4 p-4 sm:p-6">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription className="gap-3">
            <p>{getLoadConsultationErrorMessage(error)}</p>
            {canRetryLoadConsultation(error) && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void load()}
              >
                Reintentar
              </Button>
            )}
          </AlertDescription>
        </Alert>
        {backButton}
      </div>
    )
  }

  const data = consultation.data

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        {data ? (
          <h2 className="text-lg font-semibold">
            Consulta de {data.patientFullName}
          </h2>
        ) : (
          <Skeleton className="h-7 w-64" />
        )}
        <div>{backButton}</div>
      </div>

      {data ? (
        <>
          <PatientSummaryCard patientId={data.patientId} />
          <ConsultationCard
            consultation={data}
            doctorId={doctorId}
            onSave={save}
            onFinish={finish}
            onDirtyChange={setIsDirty}
          />
          <ConsultationHistory
            patientId={data.patientId}
            currentConsultationId={data.id}
          />
        </>
      ) : (
        <div
          className="flex flex-col gap-6"
          data-testid="consultation-skeleton"
        >
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
      )}

      <DiscardChangesDialog
        open={confirmingLeave}
        description="Los cambios de la consulta no se guardarán."
        onKeepEditing={() => setConfirmingLeave(false)}
        onDiscard={() => {
          setConfirmingLeave(false)
          navigate(appointmentsPath)
        }}
      />
    </div>
  )
}

interface ConsultationCardProps {
  consultation: ConsultationDto
  doctorId: string
  onSave: (request: UpdateConsultationRequest) => Promise<boolean>
  onFinish: (
    request: UpdateConsultationRequest,
    hasChanges: boolean,
  ) => Promise<boolean>
  onDirtyChange: (dirty: boolean) => void
}

// Tarjeta Consulta: datos, aviso del modo y formulario o solo lectura
function ConsultationCard({
  consultation,
  doctorId,
  onSave,
  onFinish,
  onDirtyChange,
}: ConsultationCardProps) {
  const editable = isConsultationEditable(consultation, doctorId)

  // Al pasar a solo lectura (p. ej. cerrada en otra pestaña) ya no hay cambios pendientes
  useEffect(() => {
    if (!editable) {
      onDirtyChange(false)
    }
  }, [editable, onDirtyChange])

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Consulta</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <ConsultationDetails consultation={consultation} />
        {consultation.status === 'Closed' ? (
          <Alert>
            <Info />
            <AlertDescription>
              Esta consulta está cerrada y no se puede editar.
            </AlertDescription>
          </Alert>
        ) : (
          !editable && (
            <Alert>
              <Info />
              <AlertDescription>
                Esta consulta es de {consultation.doctorFullName}: solo puedes
                verla.
              </AlertDescription>
            </Alert>
          )
        )}
        {editable ? (
          <ConsultationForm
            consultation={consultation}
            onSave={onSave}
            onFinish={onFinish}
            onDirtyChange={onDirtyChange}
          />
        ) : (
          <ConsultationReadOnly consultation={consultation} />
        )}
      </CardContent>
    </Card>
  )
}

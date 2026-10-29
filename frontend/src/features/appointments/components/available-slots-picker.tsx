import { AlertCircle } from 'lucide-react'
import { useEffect, useLayoutEffect, useRef } from 'react'

import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { formatLocalTime } from '@/features/appointments/appointment-labels'
import { getAvailableSlotsErrorMessage } from '@/features/appointments/appointment-messages'
import { getAvailableSlots } from '@/features/appointments/appointments-api'
import { useApi } from '@/hooks/use-api'

interface AvailableSlotsPickerProps {
  doctorId: string
  date: string
  // Fecha local de hoy ("yyyy-MM-dd"): con una fecha anterior no se consulta
  today: string
  // startLocal del slot elegido, o '' sin elegir
  value: string
  onChange: (startLocal: string) => void
  // El formulario lo incrementa tras un 409 o un 400 del alta para volver a pedir los slots
  reloadKey: number
  invalid?: boolean
}

// Slots libres del doctor en la fecha, como botones para elegir uno
export function AvailableSlotsPicker({
  doctorId,
  date,
  today,
  value,
  onChange,
  reloadKey,
  invalid,
}: AvailableSlotsPickerProps) {
  const { data, error, isLoading, execute } = useApi(getAvailableSlots)
  const canLoad = doctorId !== '' && date !== '' && date >= today

  // value y onChange del último render, sin volver a pedir los slots cuando cambian
  const latest = useRef({ value, onChange })
  useLayoutEffect(() => {
    latest.current = { value, onChange }
  })

  const load = async () => {
    const result = await execute(doctorId, date)
    // El slot elegido ya no está libre: se quita
    const { value: selected, onChange: change } = latest.current
    if (
      result.data &&
      selected &&
      !result.data.slots.some((slot) => slot.startLocal === selected)
    ) {
      change('')
    }
  }

  useEffect(() => {
    if (canLoad) {
      void load()
    }
    // Solo se vuelve a pedir al cambiar el doctor, la fecha o reloadKey
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [doctorId, date, reloadKey, canLoad])

  if (!canLoad) {
    return (
      <p className="text-sm text-muted-foreground">
        Selecciona un doctor y una fecha para ver los horarios libres.
      </p>
    )
  }

  // Los slots guardados son de otro doctor o fecha hasta que llega la nueva respuesta
  const isStale =
    data !== null && (data.doctorId !== doctorId || data.date !== date)

  if (isLoading || (!data && !error) || (isStale && !error)) {
    return (
      <div className="flex flex-wrap gap-2" data-testid="slots-skeleton">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} className="h-8 w-16" />
        ))}
      </div>
    )
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription className="gap-3">
          <p>{getAvailableSlotsErrorMessage(error)}</p>
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

  if (!data || data.slots.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        El doctor no tiene horarios libres ese día.
      </p>
    )
  }

  return (
    <div
      className="flex flex-wrap gap-2"
      role="group"
      aria-label="Horarios libres"
      aria-invalid={invalid || undefined}
    >
      {data.slots.map((slot) => {
        const start = formatLocalTime(slot.startLocal)
        const selected = slot.startLocal === value
        return (
          <Button
            key={slot.startLocal}
            type="button"
            size="sm"
            variant={selected ? 'default' : 'outline'}
            aria-pressed={selected}
            aria-label={`${start} a ${formatLocalTime(slot.endLocal)}`}
            onClick={() => onChange(slot.startLocal)}
          >
            {start}
          </Button>
        )
      })}
    </div>
  )
}

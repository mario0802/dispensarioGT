import { Plus, X } from 'lucide-react'
import {
  Controller,
  useFieldArray,
  useFormContext,
  useWatch,
} from 'react-hook-form'

import { Button } from '@/components/ui/button'
import { FieldError } from '@/components/ui/field'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import {
  defaultBlock,
  getNextBlock,
  hourOptions,
  toMinutes,
  type ScheduleFormValues,
} from '@/features/doctors/doctor-schedule'

// Las horas completas y, si el valor cargado no lo es (p. ej. 09:30), también ese valor
function getTimeOptions(value: string): string[] {
  return hourOptions.includes(value)
    ? hourOptions
    : [...hourOptions, value].sort((a, b) => toMinutes(a) - toMinutes(b))
}

interface TimeSelectProps {
  name: `days.${number}.blocks.${number}.${'startTime' | 'endTime'}`
  label: string
  invalid: boolean
}

function TimeSelect({ name, label, invalid }: TimeSelectProps) {
  const { control } = useFormContext<ScheduleFormValues>()
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <Select
          name={field.name}
          value={field.value}
          onValueChange={field.onChange}
        >
          <SelectTrigger
            className="w-24"
            aria-label={label}
            aria-invalid={invalid}
            onBlur={field.onBlur}
            ref={field.ref}
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {getTimeOptions(field.value).map((time) => (
              <SelectItem key={time} value={time}>
                {time}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    />
  )
}

interface DoctorScheduleDayRowProps {
  dayIndex: number
  dayLabel: string
  appointmentDurationMinutes: number
  disabled: boolean
}

// Fila de un día en edición: el switch de atención y sus bloques
export function DoctorScheduleDayRow({
  dayIndex,
  dayLabel,
  appointmentDurationMinutes,
  disabled,
}: DoctorScheduleDayRowProps) {
  const {
    control,
    setValue,
    formState: { errors },
  } = useFormContext<ScheduleFormValues>()
  const { fields, append, remove, replace } = useFieldArray({
    control,
    name: `days.${dayIndex}.blocks`,
  })
  const enabled = useWatch({ control, name: `days.${dayIndex}.enabled` })
  const blocks = useWatch({ control, name: `days.${dayIndex}.blocks` })
  const nextBlock = getNextBlock(blocks, appointmentDurationMinutes)
  const blockErrors = errors.days?.[dayIndex]?.blocks

  const toggleDay = (checked: boolean) => {
    setValue(`days.${dayIndex}.enabled`, checked, { shouldDirty: true })
    // Encender crea el bloque por defecto; apagar quita todos los del día
    replace(checked ? [{ ...defaultBlock }] : [])
  }

  const removeBlock = (blockIndex: number) => {
    // Quitar el último bloque apaga el día
    if (fields.length === 1) {
      toggleDay(false)
    } else {
      remove(blockIndex)
    }
  }

  return (
    <li className="flex flex-col gap-2 px-3 py-3 sm:flex-row sm:items-start sm:gap-4">
      <div className="flex items-center gap-3 sm:h-9 sm:w-36 sm:shrink-0">
        <Switch
          checked={enabled}
          onCheckedChange={toggleDay}
          disabled={disabled}
          aria-label={`Atiende el ${dayLabel.toLowerCase()}`}
        />
        <span className="text-sm font-medium">{dayLabel}</span>
      </div>

      {enabled ? (
        <div className="flex flex-col gap-2">
          {fields.map((field, blockIndex) => {
            const number = blockIndex + 1
            const day = dayLabel.toLowerCase()
            const error = blockErrors?.[blockIndex]
            return (
              <div key={field.id} className="flex flex-col gap-1">
                <div className="flex items-center gap-2">
                  <TimeSelect
                    name={`days.${dayIndex}.blocks.${blockIndex}.startTime`}
                    label={`Inicio del bloque ${number} del ${day}`}
                    invalid={!!error}
                  />
                  <span aria-hidden>–</span>
                  <TimeSelect
                    name={`days.${dayIndex}.blocks.${blockIndex}.endTime`}
                    label={`Fin del bloque ${number} del ${day}`}
                    invalid={!!error}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    disabled={disabled}
                    aria-label={`Quitar el bloque ${number} del ${day}`}
                    onClick={() => removeBlock(blockIndex)}
                  >
                    <X />
                  </Button>
                </div>
                <FieldError errors={[error]} />
              </div>
            )
          })}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="self-start"
            disabled={disabled || !nextBlock}
            onClick={() => nextBlock && append(nextBlock)}
          >
            <Plus />
            Añadir bloque
          </Button>
        </div>
      ) : (
        <span className="text-sm text-muted-foreground sm:leading-9">
          No atiende
        </span>
      )}
    </li>
  )
}

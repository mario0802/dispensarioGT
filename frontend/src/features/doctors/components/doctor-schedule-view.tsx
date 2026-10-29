import { Badge } from '@/components/ui/badge'
import { weekDays } from '@/features/doctors/doctor-schedule'
import type { ScheduleBlockDto } from '@/features/doctors/doctor-schedule-types'

interface DoctorScheduleViewProps {
  blocks: ScheduleBlockDto[]
  // Aviso cuando no hay ningún bloque
  emptyMessage: string
}

// Horario en solo lectura: una fila por día, de lunes a domingo
export function DoctorScheduleView({
  blocks,
  emptyMessage,
}: DoctorScheduleViewProps) {
  return (
    <div className="flex flex-col gap-3">
      {blocks.length === 0 && (
        <p className="text-sm text-muted-foreground">{emptyMessage}</p>
      )}
      <ul aria-label="Horario semanal" className="divide-y rounded-md border">
        {weekDays.map(({ value, label }) => {
          // La API ya los devuelve ordenados por hora de inicio
          const dayBlocks = blocks.filter((block) => block.dayOfWeek === value)
          return (
            <li
              key={value}
              className="flex flex-col gap-1.5 px-3 py-2.5 sm:flex-row sm:items-center sm:gap-4"
            >
              <span className="text-sm font-medium sm:w-24 sm:shrink-0">
                {label}
              </span>
              {dayBlocks.length > 0 ? (
                <div className="flex flex-wrap gap-1.5">
                  {dayBlocks.map((block) => (
                    <Badge
                      key={`${block.startTime}-${block.endTime}`}
                      variant="outline"
                    >
                      {block.startTime} – {block.endTime}
                    </Badge>
                  ))}
                </div>
              ) : (
                <span className="text-sm text-muted-foreground">
                  No atiende
                </span>
              )}
            </li>
          )
        })}
      </ul>
    </div>
  )
}

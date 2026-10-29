import { Loader2 } from 'lucide-react'

import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'

interface FinishConsultationDialogProps {
  open: boolean
  patientFullName: string
  isFinishing: boolean
  onConfirm: () => void
  onCancel: () => void
}

// Confirmación del cierre de la consulta: después no se puede editar
export function FinishConsultationDialog({
  open,
  patientFullName,
  isFinishing,
  onConfirm,
  onCancel,
}: FinishConsultationDialogProps) {
  return (
    <AlertDialog
      open={open}
      onOpenChange={(isOpen) => {
        if (!isOpen && !isFinishing) {
          onCancel()
        }
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Finalizar consulta</AlertDialogTitle>
          <AlertDialogDescription>
            La consulta de{' '}
            <span className="font-medium text-foreground">
              {patientFullName}
            </span>{' '}
            se cerrará y su cita pasará a Completada. Después no se podrá
            editar.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isFinishing}>Cancelar</AlertDialogCancel>
          {/* Botón propio, no AlertDialogAction: este cerraría el diálogo antes de que responda la API */}
          <Button type="button" disabled={isFinishing} onClick={onConfirm}>
            {isFinishing && <Loader2 className="animate-spin" />}
            {isFinishing ? 'Finalizando…' : 'Finalizar'}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

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

interface DiscardChangesDialogProps {
  open: boolean
  // Qué se pierde, p. ej. "Los cambios del horario no se guardarán."
  description: string
  onKeepEditing: () => void
  onDiscard: () => void
}

// Confirmación antes de perder cambios sin guardar (horario, consulta)
export function DiscardChangesDialog({
  open,
  description,
  onKeepEditing,
  onDiscard,
}: DiscardChangesDialogProps) {
  return (
    <AlertDialog
      open={open}
      onOpenChange={(isOpen) => !isOpen && onKeepEditing()}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>¿Descartar los cambios?</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Seguir editando</AlertDialogCancel>
          <Button type="button" variant="destructive" onClick={onDiscard}>
            Descartar
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

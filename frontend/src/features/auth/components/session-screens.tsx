import { Loader2 } from 'lucide-react'

import { Button } from '@/components/ui/button'

// Pantallas del arranque con un token guardado, mientras GET /api/auth/me responde

export function SessionLoadingScreen() {
  return (
    <div
      role="status"
      className="flex min-h-svh items-center justify-center text-muted-foreground"
    >
      <Loader2 className="size-8 animate-spin" />
      <span className="sr-only">Cargando sesión…</span>
    </div>
  )
}

interface SessionErrorScreenProps {
  onRetry: () => void
  onLogout: () => void
}

export function SessionErrorScreen({
  onRetry,
  onLogout,
}: SessionErrorScreenProps) {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 p-4 text-center">
      <p className="text-muted-foreground">
        No se pudo conectar con el servidor.
      </p>
      <div className="flex gap-2">
        <Button onClick={onRetry}>Reintentar</Button>
        <Button variant="outline" onClick={onLogout}>
          Cerrar sesión
        </Button>
      </div>
    </main>
  )
}

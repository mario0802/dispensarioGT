import { ModeToggle } from '@/components/mode-toggle'
import { Button } from '@/components/ui/button'
import { roleLabels } from '@/features/auth/role-labels'
import { useAuth } from '@/features/auth/use-auth'

// Inicio provisional: lo sustituirá el layout por rol
export function HomePage() {
  const { user, logout } = useAuth()

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-4">
        <h1 className="text-xl font-semibold">Dispensario Médico</h1>
        <div className="flex items-center gap-2">
          <ModeToggle />
          <Button variant="outline" onClick={logout}>
            Cerrar sesión
          </Button>
        </div>
      </header>
      {user && (
        <main className="flex flex-1 flex-col items-center justify-center gap-2 p-6">
          <p className="text-lg">
            Hola, {user.firstName} {user.lastName}
          </p>
          <p className="text-muted-foreground">{roleLabels[user.role]}</p>
        </main>
      )}
    </div>
  )
}

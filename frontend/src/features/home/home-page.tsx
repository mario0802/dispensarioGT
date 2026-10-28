import { roleLabels } from '@/features/auth/role-labels'
import { useAuth } from '@/features/auth/use-auth'

// Inicio: bienvenida dentro del layout de la app
export function HomePage() {
  const { user } = useAuth()

  if (!user) {
    return null
  }

  return (
    <div className="flex flex-col gap-1 p-6">
      <p className="text-lg">
        Hola, {user.firstName} {user.lastName}
      </p>
      <p className="text-muted-foreground">{roleLabels[user.role]}</p>
    </div>
  )
}

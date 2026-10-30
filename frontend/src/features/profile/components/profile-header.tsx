import { UserAvatar } from '@/components/user-avatar'
import type { CurrentUserDto } from '@/features/auth/auth-types'
import { roleLabels } from '@/features/auth/role-labels'

// Encabezado del perfil: avatar grande (iniciales mientras la API no tenga foto), nombre, rol y email
export function ProfileHeader({ user }: { user: CurrentUserDto }) {
  return (
    <div className="flex flex-col items-start gap-4 sm:flex-row sm:items-center">
      <UserAvatar
        firstName={user.firstName}
        lastName={user.lastName}
        size="lg"
      />
      <div className="min-w-0">
        <h2 className="text-xl font-semibold break-words">
          {user.firstName} {user.lastName}
        </h2>
        <p className="text-sm text-muted-foreground">{roleLabels[user.role]}</p>
        <p className="text-sm break-all text-muted-foreground">{user.email}</p>
      </div>
    </div>
  )
}

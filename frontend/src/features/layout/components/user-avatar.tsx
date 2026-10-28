import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { getInitials } from '@/features/layout/initials'

interface UserAvatarProps {
  firstName: string
  lastName: string
}

// Avatar con las iniciales (la API no tiene foto). Decorativo: el nombre siempre va al lado
export function UserAvatar({ firstName, lastName }: UserAvatarProps) {
  return (
    <Avatar className="size-8 rounded-lg" aria-hidden="true">
      <AvatarFallback className="rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
        {getInitials(firstName, lastName)}
      </AvatarFallback>
    </Avatar>
  )
}

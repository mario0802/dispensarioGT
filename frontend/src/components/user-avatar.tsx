import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { getInitials } from '@/lib/initials'
import { cn } from '@/lib/utils'

interface UserAvatarProps {
  firstName: string
  lastName: string
  // Sin foto en la API todavía: hoy nadie lo pasa y siempre se ven las iniciales
  imageUrl?: string | null
  // sm: sidebar (por defecto); lg: encabezado del perfil
  size?: 'sm' | 'lg'
}

// Avatar con la foto o, si no hay o no carga, las iniciales. Decorativo: el nombre siempre va al lado
export function UserAvatar({
  firstName,
  lastName,
  imageUrl,
  size = 'sm',
}: UserAvatarProps) {
  return (
    <Avatar
      className={cn('rounded-lg', size === 'lg' ? 'size-20' : 'size-8')}
      aria-hidden="true"
    >
      {/* Radix solo pinta el <img> cuando la imagen carga */}
      <AvatarImage
        src={imageUrl ?? undefined}
        alt=""
        className="rounded-lg object-cover"
      />
      <AvatarFallback
        className={cn(
          'rounded-lg bg-sidebar-primary text-sidebar-primary-foreground',
          size === 'lg' && 'text-2xl',
        )}
      >
        {getInitials(firstName, lastName)}
      </AvatarFallback>
    </Avatar>
  )
}

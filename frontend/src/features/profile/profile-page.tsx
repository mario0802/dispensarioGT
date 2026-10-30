import { useAuth } from '@/features/auth/use-auth'
import { ChangePasswordCard } from '@/features/profile/components/change-password-card'
import { ProfileDetailsCard } from '@/features/profile/components/profile-details-card'
import { ProfileHeader } from '@/features/profile/components/profile-header'

// Página /profile: cualquier usuario ve sus datos, los edita y cambia su contraseña.
// No pide nada al montar: los datos vienen de /me, ya cargado en la sesión
export function ProfilePage() {
  const { user } = useAuth()

  // Solo se monta dentro de RequireAuth, así que siempre hay usuario
  if (!user) {
    return null
  }

  return (
    <div className="flex w-full max-w-2xl flex-col gap-6 p-4 sm:p-6">
      <ProfileHeader user={user} />
      <ProfileDetailsCard user={user} />
      <ChangePasswordCard />
    </div>
  )
}

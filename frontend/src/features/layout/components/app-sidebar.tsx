import { Stethoscope } from 'lucide-react'
import { Link } from 'react-router'

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar'
import type { CurrentUserDto } from '@/features/auth/auth-types'
import { NavMain } from '@/features/layout/components/nav-main'
import { NavUser } from '@/features/layout/components/nav-user'
import { getNavItems } from '@/features/layout/nav-items'

interface AppSidebarProps {
  user: CurrentUserDto
  onLogout: () => void
}

const toggleLabel = 'Mostrar u ocultar menú'

// Sidebar de la app: nombre del dispensario, menús del rol y bloque del usuario; plegado, solo iconos
export function AppSidebar({ user, onLogout }: AppSidebarProps) {
  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to="/">
                <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <Stethoscope className="size-4" />
                </div>
                <span className="truncate font-semibold">
                  Dispensario Médico
                </span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain items={getNavItems(user.role)} />
      </SidebarContent>
      <SidebarFooter>
        <NavUser user={user} onLogout={onLogout} />
      </SidebarFooter>
      <SidebarRail aria-label={toggleLabel} title={toggleLabel} />
    </Sidebar>
  )
}

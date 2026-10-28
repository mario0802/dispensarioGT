import { NavLink, useLocation } from 'react-router'

import {
  SidebarGroup,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { isNavItemActive, type NavItem } from '@/features/layout/nav-items'

// Lista de menús del sidebar con el de la ruta actual marcado
export function NavMain({ items }: { items: NavItem[] }) {
  const { pathname } = useLocation()
  const { setOpenMobile } = useSidebar()

  return (
    <SidebarGroup>
      <SidebarMenu>
        {items.map((item) => (
          <SidebarMenuItem key={item.url}>
            <SidebarMenuButton
              asChild
              isActive={isNavItemActive(item, pathname)}
              tooltip={item.title}
            >
              {/* En móvil, elegir un menú cierra el panel */}
              <NavLink
                to={item.url}
                end={item.url === '/'}
                onClick={() => setOpenMobile(false)}
              >
                <item.icon />
                <span>{item.title}</span>
              </NavLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        ))}
      </SidebarMenu>
    </SidebarGroup>
  )
}

import { House, type LucideIcon } from 'lucide-react'

import type { UserRole } from '@/features/auth/auth-types'

export interface NavItem {
  title: string
  url: string
  icon: LucideIcon
}

// Comunes a todos los roles
export const commonNavItems: NavItem[] = [
  { title: 'Inicio', url: '/', icon: House },
]

// Propios de cada rol: se rellenan en el spec de cada menú (junto con su ruta hija de AppLayout)
export const roleNavItems: Record<UserRole, NavItem[]> = {
  Admin: [],
  Doctor: [],
  Receptionist: [],
}

// Menús del sidebar: los comunes seguidos de los del rol
export function getNavItems(role: UserRole): NavItem[] {
  return [...commonNavItems, ...roleNavItems[role]]
}

// El ítem de la ruta actual: '/' solo coincide exacta; los demás, con la ruta o sus hijas
export function isNavItemActive(item: NavItem, pathname: string): boolean {
  if (item.url === '/') {
    return pathname === '/'
  }
  return pathname === item.url || pathname.startsWith(`${item.url}/`)
}

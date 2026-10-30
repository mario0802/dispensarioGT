import {
  BriefcaseMedical,
  CalendarClock,
  CalendarDays,
  House,
  Users,
  type LucideIcon,
} from 'lucide-react'

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

const doctorsNavItem: NavItem = {
  title: 'Doctores',
  url: '/doctors',
  icon: BriefcaseMedical,
}

const patientsNavItem: NavItem = {
  title: 'Pacientes',
  url: '/patients',
  icon: Users,
}

const appointmentsNavItem: NavItem = {
  title: 'Citas',
  url: '/appointments',
  icon: CalendarDays,
}

const myScheduleNavItem: NavItem = {
  title: 'Mi horario',
  url: '/my-schedule',
  icon: CalendarClock,
}

// Propios de cada rol: se rellenan en el spec de cada menú (junto con su ruta hija de AppLayout)
export const roleNavItems: Record<UserRole, NavItem[]> = {
  Admin: [doctorsNavItem, patientsNavItem, appointmentsNavItem],
  Doctor: [appointmentsNavItem, myScheduleNavItem],
  Receptionist: [doctorsNavItem, patientsNavItem, appointmentsNavItem],
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

// Rutas sin NavItem que tienen título propio en la barra superior
const routeTitles: { url: string; title: string }[] = [
  { url: '/profile', title: 'Mi perfil' },
]

const defaultPageTitle = 'Dispensario Médico'

// Título de la barra superior: el del NavItem activo; si no hay, el de routeTitles (ruta exacta)
export function getPageTitle(role: UserRole, pathname: string): string {
  const activeItem = getNavItems(role).find((item) =>
    isNavItemActive(item, pathname),
  )
  if (activeItem) {
    return activeItem.title
  }
  return (
    routeTitles.find((route) => route.url === pathname)?.title ??
    defaultPageTitle
  )
}

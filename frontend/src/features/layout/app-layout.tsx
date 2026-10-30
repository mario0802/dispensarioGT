import { Outlet, useLocation } from 'react-router'

import { Separator } from '@/components/ui/separator'
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { Toaster } from '@/components/ui/sonner'
import { useAuth } from '@/features/auth/use-auth'
import { AppSidebar } from '@/features/layout/components/app-sidebar'
import { getPageTitle } from '@/features/layout/nav-items'
import { readSidebarOpen } from '@/features/layout/sidebar-cookie'

// Layout de las rutas protegidas: sidebar a la izquierda y, a la derecha, barra superior y la página activa
export function AppLayout() {
  const { user, logout } = useAuth()
  const { pathname } = useLocation()

  // Solo se monta dentro de RequireAuth, así que siempre hay usuario
  if (!user) {
    return null
  }

  return (
    <SidebarProvider defaultOpen={readSidebarOpen()}>
      <AppSidebar user={user} onLogout={logout} />
      <SidebarInset className="isolate">
        {/* Fondo del área de contenido: imagen fija con un velo del color del tema para que se lea el contenido */}
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-20 bg-[url('/fondo_inicio.jpg')] bg-cover bg-fixed bg-center"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-10 bg-linear-to-br from-background/70 via-background/40 to-background/20"
        />
        <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
          <SidebarTrigger
            className="-ml-1"
            aria-label="Mostrar u ocultar menú"
          />
          <Separator
            orientation="vertical"
            className="mr-2 data-[orientation=vertical]:h-4"
          />
          <h1 className="text-base font-semibold">
            {getPageTitle(user.role, pathname)}
          </h1>
        </header>
        {/* En el inicio el contenido va directo sobre el fondo; en las demás páginas,
            dentro de un panel sólido para que se lea bien */}
        {pathname === '/' ? (
          <div className="flex flex-1 flex-col">
            <Outlet />
          </div>
        ) : (
          <div className="flex flex-1 flex-col p-3 md:p-4">
            <div className="flex flex-1 flex-col rounded-xl border bg-background shadow-lg">
              <Outlet />
            </div>
          </div>
        )}
      </SidebarInset>
      {/* Avisos (toasts) del área con sesión */}
      <Toaster richColors position="top-right" />
    </SidebarProvider>
  )
}

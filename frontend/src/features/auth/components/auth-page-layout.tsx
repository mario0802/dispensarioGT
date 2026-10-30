import type { ReactNode } from 'react'

// Contenedor de las páginas públicas (login y registro): imagen de fondo con
// degradado y el contenido centrado por encima
export function AuthPageLayout({ children }: { children: ReactNode }) {
  return (
    <main className="relative isolate flex min-h-svh items-center justify-center overflow-hidden p-4">
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-20 bg-[url('/fondo_login.webp')] bg-cover bg-center"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-br from-slate-950/85 via-slate-900/55 to-slate-950/85"
      />
      {children}
    </main>
  )
}

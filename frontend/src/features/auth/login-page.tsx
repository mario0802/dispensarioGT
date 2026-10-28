import { Navigate, useLocation } from 'react-router'

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
} from '@/components/ui/card'
import { LoginForm } from '@/features/auth/components/login-form'
import { useAuth } from '@/features/auth/use-auth'

// Destino tras el login: el location que guardó RequireAuth, nunca un parámetro de la URL
function getRedirectTarget(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from
  if (typeof from !== 'object' || from === null) {
    return '/'
  }
  const { pathname, search, hash } = from as Record<string, unknown>
  if (typeof pathname !== 'string' || !pathname.startsWith('/')) {
    return '/'
  }
  return (
    pathname +
    (typeof search === 'string' ? search : '') +
    (typeof hash === 'string' ? hash : '')
  )
}

export function LoginPage() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'authenticated') {
    return <Navigate to={getRedirectTarget(location.state)} replace />
  }

  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <h1 className="text-xl leading-none font-semibold">
            Dispensario Médico
          </h1>
          <CardDescription>Inicia sesión para continuar.</CardDescription>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </main>
  )
}

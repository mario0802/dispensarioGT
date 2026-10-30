import { CheckCircle2 } from 'lucide-react'
import { Link, Navigate, useLocation } from 'react-router'

import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
} from '@/components/ui/card'
import { AuthPageLayout } from '@/features/auth/components/auth-page-layout'
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

// Email de la cuenta recién creada, que deja el registro en el state del router
function getRegisteredEmail(state: unknown): string | null {
  const email = (state as { registeredEmail?: unknown } | null)?.registeredEmail
  return typeof email === 'string' && email !== '' ? email : null
}

export function LoginPage() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'authenticated') {
    return <Navigate to={getRedirectTarget(location.state)} replace />
  }

  const registeredEmail = getRegisteredEmail(location.state)

  return (
    <AuthPageLayout>
      <Card className="w-full max-w-sm bg-card/90 shadow-2xl backdrop-blur-sm">
        <CardHeader>
          <h1 className="text-xl leading-none font-semibold">
            Dispensario Médico
          </h1>
          <CardDescription>Inicia sesión para continuar.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-5">
          {registeredEmail && (
            <Alert role="status">
              <CheckCircle2 />
              <AlertDescription>
                Cuenta creada. Inicia sesión para continuar.
              </AlertDescription>
            </Alert>
          )}
          <LoginForm defaultEmail={registeredEmail ?? ''} />
        </CardContent>
        <CardFooter className="justify-center text-sm text-muted-foreground">
          <p>
            ¿No tienes cuenta?{' '}
            <Link
              to="/register"
              className="font-medium text-foreground underline underline-offset-4"
            >
              Regístrate
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthPageLayout>
  )
}

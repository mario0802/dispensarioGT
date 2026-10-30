import { Link, Navigate } from 'react-router'

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
} from '@/components/ui/card'
import { AuthPageLayout } from '@/features/auth/components/auth-page-layout'
import { RegisterForm } from '@/features/auth/components/register-form'
import { useAuth } from '@/features/auth/use-auth'

// Autoregistro público: la API siempre crea un recepcionista
export function RegisterPage() {
  const { status } = useAuth()

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  return (
    <AuthPageLayout>
      <Card className="w-full max-w-md bg-card/90 shadow-2xl backdrop-blur-sm">
        <CardHeader>
          <h1 className="text-xl leading-none font-semibold">
            Dispensario Médico
          </h1>
          <CardDescription>Crea una cuenta de recepcionista.</CardDescription>
        </CardHeader>
        <CardContent>
          <RegisterForm />
        </CardContent>
        <CardFooter className="justify-center text-sm text-muted-foreground">
          <p>
            ¿Ya tienes cuenta?{' '}
            <Link
              to="/login"
              className="font-medium text-foreground underline underline-offset-4"
            >
              Inicia sesión
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthPageLayout>
  )
}

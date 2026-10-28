import { Navigate, Route, Routes } from 'react-router'

import { RequireAuth } from '@/features/auth/components/require-auth'
import {
  SessionErrorScreen,
  SessionLoadingScreen,
} from '@/features/auth/components/session-screens'
import { LoginPage } from '@/features/auth/login-page'
import { useAuth } from '@/features/auth/use-auth'
import { HomePage } from '@/features/home/home-page'

// Rutas de la app. Mientras se resuelve la sesión del arranque no se renderiza ninguna,
// así las redirecciones solo dependen de authenticated / anonymous
export function AppRoutes() {
  const { status, retry, logout } = useAuth()

  if (status === 'loading') {
    return <SessionLoadingScreen />
  }
  if (status === 'error') {
    return <SessionErrorScreen onRetry={retry} onLogout={logout} />
  }

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

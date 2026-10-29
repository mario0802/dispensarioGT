import { Navigate, Route, Routes } from 'react-router'

import { RequireAuth } from '@/features/auth/components/require-auth'
import { RequireRole } from '@/features/auth/components/require-role'
import {
  SessionErrorScreen,
  SessionLoadingScreen,
} from '@/features/auth/components/session-screens'
import { LoginPage } from '@/features/auth/login-page'
import { RegisterPage } from '@/features/auth/register-page'
import { useAuth } from '@/features/auth/use-auth'
import { DoctorsPage } from '@/features/doctors/doctors-page'
import { MySchedulePage } from '@/features/doctors/my-schedule-page'
import { HomePage } from '@/features/home/home-page'
import { AppLayout } from '@/features/layout/app-layout'
import { PatientsPage } from '@/features/patients/patients-page'

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
      <Route path="/register" element={<RegisterPage />} />
      {/* Rutas protegidas dentro del layout: las páginas de cada menú se añaden como hijas */}
      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route index element={<HomePage />} />
        <Route
          path="doctors"
          element={
            <RequireRole allowedRoles={['Admin', 'Receptionist']}>
              <DoctorsPage />
            </RequireRole>
          }
        />
        <Route
          path="patients"
          element={
            <RequireRole allowedRoles={['Admin', 'Receptionist']}>
              <PatientsPage />
            </RequireRole>
          }
        />
        <Route
          path="my-schedule"
          element={
            <RequireRole allowedRoles={['Doctor']}>
              <MySchedulePage />
            </RequireRole>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

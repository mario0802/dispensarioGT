import { BrowserRouter } from 'react-router'

import { AppRoutes } from '@/app-routes'
import { AuthProvider } from '@/features/auth/auth-provider'

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App

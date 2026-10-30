import {
  type ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'

import { getCurrentUser, login as requestLogin } from '@/features/auth/auth-api'
import type {
  AuthStatus,
  CurrentUserDto,
  LoginRequest,
} from '@/features/auth/auth-types'
import {
  clearStoredToken,
  readStoredToken,
  saveStoredToken,
} from '@/features/auth/token-storage'
import { type AuthContextValue, AuthContext } from '@/features/auth/use-auth'
import { setAccessTokenProvider } from '@/lib/api/access-token'
import { toApiError } from '@/lib/api/api-error'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'

interface SessionState {
  status: AuthStatus
  user: CurrentUserDto | null
}

// Sesión de la app: token en localStorage, usuario de GET /api/auth/me y cierre ante un 401
export function AuthProvider({ children }: { children: ReactNode }) {
  // El token se lee una sola vez al montar; uno caducado o inválido se descarta sin llamar a la API
  const [initialToken] = useState(() => readStoredToken())
  const tokenRef = useRef<string | null>(initialToken?.accessToken ?? null)
  const [session, setSession] = useState<SessionState>({
    status: initialToken ? 'loading' : 'anonymous',
    user: null,
  })

  // Cada petición de /me, logout y desmontaje incrementa el contador: las respuestas viejas se ignoran
  const requestIdRef = useRef(0)

  const logout = useCallback(() => {
    requestIdRef.current++
    tokenRef.current = null
    clearStoredToken()
    setSession({ status: 'anonymous', user: null })
  }, [])

  const fetchCurrentUser = useCallback(async () => {
    const requestId = ++requestIdRef.current
    try {
      const user = await getCurrentUser()
      if (requestId === requestIdRef.current) {
        setSession({ status: 'authenticated', user })
      }
    } catch (reason) {
      // Un 401 ya cerró la sesión desde el manejador global y deja esta petición obsoleta
      if (requestId !== requestIdRef.current) {
        return
      }
      const error = toApiError(reason)
      if (error.status === 401 || error.status === 404) {
        logout()
      } else {
        // Error de red u otro: se conserva el token para poder reintentar
        setSession({ status: 'error', user: null })
      }
    }
  }, [logout])

  // Se registra antes del efecto de arranque para que /me ya lleve el token
  useEffect(() => {
    setAccessTokenProvider(() => tokenRef.current)
    setUnauthorizedHandler(logout)
    return () => {
      setAccessTokenProvider(null)
      setUnauthorizedHandler(null)
    }
  }, [logout])

  useEffect(() => {
    if (tokenRef.current) {
      void fetchCurrentUser()
    }
    const requestIds = requestIdRef
    return () => {
      requestIds.current++
    }
  }, [fetchCurrentUser])

  const login = useCallback(async (request: LoginRequest) => {
    const response = await requestLogin(request)

    const requestId = ++requestIdRef.current
    tokenRef.current = response.accessToken
    saveStoredToken({
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
    })

    try {
      const user = await getCurrentUser()
      if (requestId === requestIdRef.current) {
        setSession({ status: 'authenticated', user })
      }
      return user
    } catch (reason) {
      // Sin usuario no hay sesión: no se deja un token a medias
      if (requestId === requestIdRef.current) {
        tokenRef.current = null
        clearStoredToken()
      }
      throw toApiError(reason)
    }
  }, [])

  // Vuelve a pedir /me sin pasar por loading (desmontaría las rutas). Nunca rechaza
  const refreshUser = useCallback(async () => {
    const requestId = ++requestIdRef.current
    try {
      const user = await getCurrentUser()
      if (requestId === requestIdRef.current) {
        setSession({ status: 'authenticated', user })
      }
    } catch (reason) {
      if (requestId !== requestIdRef.current) {
        return
      }
      const error = toApiError(reason)
      if (error.status === 401 || error.status === 404) {
        logout()
      }
      // Cualquier otro error conserva el usuario actual: los datos se verán en la próxima carga
    }
  }, [logout])

  const retry = useCallback(() => {
    setSession({ status: 'loading', user: null })
    void fetchCurrentUser()
  }, [fetchCurrentUser])

  const value = useMemo<AuthContextValue>(
    () => ({ ...session, login, logout, retry, refreshUser }),
    [session, login, logout, retry, refreshUser],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

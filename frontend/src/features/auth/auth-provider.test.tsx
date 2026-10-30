import { act, renderHook, waitFor } from '@testing-library/react'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { AuthProvider } from '@/features/auth/auth-provider'
import {
  readStoredToken,
  TOKEN_STORAGE_KEY,
} from '@/features/auth/token-storage'
import { useAuth } from '@/features/auth/use-auth'
import { setAccessTokenProvider } from '@/lib/api/access-token'
import { api } from '@/lib/api/api-client'
import { ApiError } from '@/lib/api/api-error'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'
import {
  jsonResponse,
  loginResponse,
  problemResponse,
  storeValidToken,
  testUser,
} from '@/test/api-responses'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAccessTokenProvider(null)
  setUnauthorizedHandler(null)
})

function renderAuth() {
  return renderHook(() => useAuth(), { wrapper: AuthProvider })
}

function requestAt(index: number) {
  const [url, init] = fetchMock.mock.calls[index]
  return { url: String(url), init: init!, headers: new Headers(init!.headers) }
}

describe('AuthProvider', () => {
  it('starts anonymous without calling the API when there is no token', () => {
    const { result } = renderAuth()

    expect(result.current.status).toBe('anonymous')
    expect(result.current.user).toBeNull()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('discards an expired token without calling the API', () => {
    localStorage.setItem(
      TOKEN_STORAGE_KEY,
      JSON.stringify({
        accessToken: 'old',
        expiresAtUtc: '2020-01-01T00:00:00Z',
      }),
    )

    const { result } = renderAuth()

    expect(result.current.status).toBe('anonymous')
    expect(fetchMock).not.toHaveBeenCalled()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('loads the user from /me with the stored token', async () => {
    storeValidToken('stored-token')
    fetchMock.mockResolvedValue(jsonResponse(testUser))

    const { result } = renderAuth()

    expect(result.current.status).toBe('loading')
    await waitFor(() => expect(result.current.status).toBe('authenticated'))
    expect(result.current.user).toEqual(testUser)
    const request = requestAt(0)
    expect(request.url).toMatch(/\/api\/auth\/me$/)
    expect(request.headers.get('Authorization')).toBe('Bearer stored-token')
  })

  it.each([401, 404])(
    'ends anonymous and removes the token when /me answers %i',
    async (status) => {
      storeValidToken()
      fetchMock.mockResolvedValue(problemResponse(status))

      const { result } = renderAuth()

      await waitFor(() => expect(result.current.status).toBe('anonymous'))
      expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
    },
  )

  it('keeps the token on a network error and recovers with retry', async () => {
    const token = storeValidToken()
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('error'))
    expect(readStoredToken()).toEqual(token)

    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    act(() => result.current.retry())

    expect(result.current.status).toBe('loading')
    await waitFor(() => expect(result.current.status).toBe('authenticated'))
    expect(result.current.user).toEqual(testUser)
  })

  it('logs in, stores the token and loads the user', async () => {
    const response = loginResponse('new-token')
    fetchMock
      .mockResolvedValueOnce(jsonResponse(response))
      .mockResolvedValueOnce(jsonResponse(testUser))
    const { result } = renderAuth()

    let user: unknown
    await act(async () => {
      user = await result.current.login({
        email: testUser.email,
        password: 'Passw0rd',
      })
    })

    expect(user).toEqual(testUser)
    expect(result.current.status).toBe('authenticated')
    expect(result.current.user).toEqual(testUser)
    expect(readStoredToken()).toEqual({
      accessToken: 'new-token',
      expiresAtUtc: response.expiresAtUtc,
    })
    const loginRequest = requestAt(0)
    expect(loginRequest.url).toMatch(/\/api\/auth\/login$/)
    expect(loginRequest.init.method).toBe('POST')
    expect(JSON.parse(loginRequest.init.body as string)).toEqual({
      email: testUser.email,
      password: 'Passw0rd',
    })
    expect(requestAt(1).headers.get('Authorization')).toBe('Bearer new-token')
  })

  it('rejects a failed login and stores nothing', async () => {
    fetchMock.mockResolvedValueOnce(problemResponse(401, 'Unauthorized'))
    const { result } = renderAuth()

    let error: unknown
    await act(async () => {
      error = await result.current
        .login({ email: testUser.email, password: 'wrong' })
        .catch((reason: unknown) => reason)
    })

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
    expect(result.current.status).toBe('anonymous')
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
    expect(fetchMock).toHaveBeenCalledOnce()
  })

  it('rejects and removes the token when /me fails right after the login', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(loginResponse()))
      .mockResolvedValueOnce(problemResponse(500))
    const { result } = renderAuth()

    let error: unknown
    await act(async () => {
      error = await result.current
        .login({ email: testUser.email, password: 'Passw0rd' })
        .catch((reason: unknown) => reason)
    })

    expect((error as ApiError).status).toBe(500)
    expect(result.current.status).toBe('anonymous')
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()

    // Sin token, la siguiente petición sale sin Authorization
    fetchMock.mockResolvedValueOnce(jsonResponse({}))
    await api.get('/api/x')
    expect(requestAt(2).headers.has('Authorization')).toBe(false)
  })

  it('logs out, removing the token', async () => {
    storeValidToken()
    fetchMock.mockResolvedValue(jsonResponse(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    act(() => result.current.logout())

    expect(result.current.status).toBe('anonymous')
    expect(result.current.user).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('logs out when another request with the token answers 401', async () => {
    storeValidToken()
    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    fetchMock.mockResolvedValueOnce(problemResponse(401))
    await act(async () => {
      await api.get('/api/patients').catch(() => undefined)
    })

    expect(result.current.status).toBe('anonymous')
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('refreshUser updates the user from /me without going through loading', async () => {
    storeValidToken()
    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    const renamed = { ...testUser, lastName: 'Ruiz', phone: '0991234567' }
    fetchMock.mockResolvedValueOnce(jsonResponse(renamed))
    const statuses: string[] = []
    await act(async () => {
      const refresh = result.current.refreshUser()
      statuses.push(result.current.status)
      await refresh
    })

    expect(statuses).toEqual(['authenticated'])
    expect(result.current.status).toBe('authenticated')
    expect(result.current.user).toEqual(renamed)
    expect(requestAt(1).url).toMatch(/\/api\/auth\/me$/)
  })

  it('refreshUser logs out when /me answers 401', async () => {
    storeValidToken()
    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    fetchMock.mockResolvedValueOnce(problemResponse(401))
    await act(() => result.current.refreshUser())

    expect(result.current.status).toBe('anonymous')
    expect(result.current.user).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('refreshUser keeps the user on a network error', async () => {
    const token = storeValidToken()
    fetchMock.mockResolvedValueOnce(jsonResponse(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await act(() => result.current.refreshUser())

    expect(result.current.status).toBe('authenticated')
    expect(result.current.user).toEqual(testUser)
    expect(readStoredToken()).toEqual(token)
  })

  it('useAuth throws outside an AuthProvider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    expect(() => renderHook(() => useAuth())).toThrow(
      'useAuth must be used within an AuthProvider.',
    )

    vi.mocked(console.error).mockRestore()
  })
})

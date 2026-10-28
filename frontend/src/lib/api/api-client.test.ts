import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { setAccessTokenProvider } from '@/lib/api/access-token'
import { api, apiRequest } from '@/lib/api/api-client'
import { ApiError } from '@/lib/api/api-error'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'
import { config } from '@/lib/config'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi
    .fn<typeof fetch>()
    .mockResolvedValue(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  setAccessTokenProvider(null)
})

function lastRequest() {
  const [url, init] = fetchMock.mock.calls.at(-1)!
  return { url, init: init!, headers: new Headers(init!.headers) }
}

async function captureError(promise: Promise<unknown>): Promise<ApiError> {
  const error = await promise.catch((reason: unknown) => reason)
  expect(error).toBeInstanceOf(ApiError)
  return error as ApiError
}

describe('apiRequest', () => {
  it('builds the URL from the config base URL and the path', async () => {
    await apiRequest('/api/patients')

    expect(lastRequest().url).toBe(`${config.apiBaseUrl}/api/patients`)
  })

  it('encodes the query and skips null and undefined values', async () => {
    await api.get('/api/x', {
      a: 1,
      b: undefined,
      c: null,
      q: 'Ana María',
      active: true,
    })

    expect(lastRequest().url).toBe(
      `${config.apiBaseUrl}/api/x?a=1&q=Ana+Mar%C3%ADa&active=true`,
    )
  })

  it('adds no "?" when no query value is left', async () => {
    await api.get('/api/x', { b: undefined, c: null })

    expect(lastRequest().url).toBe(`${config.apiBaseUrl}/api/x`)
  })

  it('always sends Accept: application/json', async () => {
    await api.get('/api/x')

    expect(lastRequest().headers.get('Accept')).toBe('application/json')
  })

  it('serializes the body with Content-Type: application/json', async () => {
    await api.post('/api/x', { name: 'A' })

    const { init, headers } = lastRequest()
    expect(init.body).toBe('{"name":"A"}')
    expect(headers.get('Content-Type')).toBe('application/json')
  })

  it('sends neither body nor Content-Type without a body', async () => {
    await api.post('/api/x')

    const { init, headers } = lastRequest()
    expect(init.body).toBeUndefined()
    expect(headers.has('Content-Type')).toBe(false)
  })

  it('sends the token from the provider as a Bearer token', async () => {
    setAccessTokenProvider(() => 'abc')

    await api.get('/api/x')

    expect(lastRequest().headers.get('Authorization')).toBe('Bearer abc')
  })

  it('sends no Authorization header without a provider', async () => {
    await api.get('/api/x')

    expect(lastRequest().headers.has('Authorization')).toBe(false)
  })

  it('sends no Authorization header when the provider returns an empty token', async () => {
    setAccessTokenProvider(() => '')

    await api.get('/api/x')

    expect(lastRequest().headers.has('Authorization')).toBe(false)
  })

  it.each([
    ['get', () => api.get('/api/x'), 'GET'],
    ['post', () => api.post('/api/x', {}), 'POST'],
    ['put', () => api.put('/api/x', {}), 'PUT'],
    ['delete', () => api.delete('/api/x'), 'DELETE'],
  ])('api.%s uses the %s method', async (_, call, method) => {
    await call()

    expect(lastRequest().init.method).toBe(method)
  })

  it('throws a network ApiError when fetch rejects', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = await captureError(api.get('/api/x'))

    expect(error.kind).toBe('network')
    expect(error.status).toBe(0)
    expect(error.title).toBe('Network error.')
  })

  it('throws an http ApiError with the ProblemDetails of an error response', async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Conflict',
          detail: 'Document already exists.',
        }),
        {
          status: 409,
          headers: { 'Content-Type': 'application/problem+json' },
        },
      ),
    )

    const error = await captureError(api.post('/api/patients', {}))

    expect(error.kind).toBe('http')
    expect(error.status).toBe(409)
    expect(error.detail).toBe('Document already exists.')
  })

  it('resolves undefined for a 204 response', async () => {
    await expect(api.delete('/api/x')).resolves.toBeUndefined()
  })

  it('resolves the parsed JSON for a JSON response', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ id: '1', name: 'Ana' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json; charset=utf-8' },
      }),
    )

    await expect(api.get('/api/x')).resolves.toEqual({ id: '1', name: 'Ana' })
  })

  it('resolves the text for a non-JSON response', async () => {
    fetchMock.mockResolvedValue(
      new Response('Healthy', {
        status: 200,
        headers: { 'Content-Type': 'text/plain' },
      }),
    )

    await expect(api.get('/health')).resolves.toBe('Healthy')
  })

  it('throws an unknown ApiError for invalid JSON in a successful response', async () => {
    fetchMock.mockResolvedValue(
      new Response('{not json', {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    const error = await captureError(api.get('/api/x'))

    expect(error.kind).toBe('unknown')
    expect(error.status).toBe(200)
  })
})

describe('unauthorized handler', () => {
  const problem = (status: number) =>
    new Response(JSON.stringify({ title: 'Unauthorized' }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    })

  afterEach(() => {
    setUnauthorizedHandler(null)
  })

  it('notifies a 401 of a request with a token and still throws', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    setAccessTokenProvider(() => 'abc')
    fetchMock.mockResolvedValue(problem(401))

    const error = await captureError(api.get('/api/x'))

    expect(handler).toHaveBeenCalledOnce()
    expect(error.kind).toBe('http')
    expect(error.status).toBe(401)
  })

  it('does not notify a 401 of a request without a token', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(problem(401))

    await captureError(api.post('/api/auth/login', {}))

    expect(handler).not.toHaveBeenCalled()
  })

  it('does not notify a 403', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    setAccessTokenProvider(() => 'abc')
    fetchMock.mockResolvedValue(problem(403))

    await captureError(api.get('/api/x'))

    expect(handler).not.toHaveBeenCalled()
  })

  it('only throws on a 401 without a registered handler', async () => {
    setAccessTokenProvider(() => 'abc')
    fetchMock.mockResolvedValue(problem(401))

    const error = await captureError(api.get('/api/x'))

    expect(error.status).toBe(401)
  })
})

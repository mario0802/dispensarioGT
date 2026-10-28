import { describe, expect, it } from 'vitest'

import { ApiError, createHttpError, toApiError } from '@/lib/api/api-error'

function problemResponse(status: number, body: unknown, statusText = '') {
  return new Response(JSON.stringify(body), {
    status,
    statusText,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('createHttpError', () => {
  it('reads the ProblemDetails with ValidationErrors', async () => {
    const response = problemResponse(400, {
      title: 'ValidationException',
      status: 400,
      detail: 'Validation failed.',
      traceId: 'trace-1',
      ValidationErrors: [
        {
          propertyName: 'Email',
          errorMessage: "'Email' must not be empty.",
          attemptedValue: '',
          severity: 'Error',
          errorCode: 'NotEmptyValidator',
        },
      ],
    })

    const error = await createHttpError(response)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.kind).toBe('http')
    expect(error.status).toBe(400)
    expect(error.title).toBe('ValidationException')
    expect(error.detail).toBe('Validation failed.')
    expect(error.traceId).toBe('trace-1')
    expect(error.validationErrors).toEqual([
      { propertyName: 'Email', errorMessage: "'Email' must not be empty." },
    ])
  })

  it('returns no validation errors when the ProblemDetails has none', async () => {
    const response = problemResponse(409, {
      title: 'Conflict',
      detail: 'Duplicated.',
    })

    const error = await createHttpError(response)

    expect(error.status).toBe(409)
    expect(error.validationErrors).toEqual([])
  })

  it('uses the status text when the body is not JSON', async () => {
    const response = new Response('Server exploded', {
      status: 502,
      statusText: 'Bad Gateway',
      headers: { 'Content-Type': 'text/plain' },
    })

    const error = await createHttpError(response)

    expect(error.kind).toBe('http')
    expect(error.status).toBe(502)
    expect(error.title).toBe('Bad Gateway')
    expect(error.detail).toBeNull()
    expect(error.validationErrors).toEqual([])
  })

  it('falls back to "HTTP <status>" when the status text is empty', async () => {
    const response = new Response(null, { status: 500 })

    const error = await createHttpError(response)

    expect(error.title).toBe('HTTP 500')
  })

  it('uses the detail as message, or the title without it', async () => {
    const withDetail = await createHttpError(
      problemResponse(404, {
        title: 'Not Found',
        detail: 'Patient not found.',
      }),
    )
    const withoutDetail = await createHttpError(
      problemResponse(403, { title: 'Forbidden' }),
    )

    expect(withDetail.message).toBe('Patient not found.')
    expect(withoutDetail.message).toBe('Forbidden')
  })
})

describe('toApiError', () => {
  it('returns the same ApiError', () => {
    const error = new ApiError({
      kind: 'network',
      status: 0,
      title: 'Network error.',
    })

    expect(toApiError(error)).toBe(error)
  })

  it('wraps an Error', () => {
    const error = toApiError(new Error('Boom'))

    expect(error.kind).toBe('unknown')
    expect(error.status).toBe(0)
    expect(error.title).toBe('Boom')
  })

  it('wraps a value that is not an Error', () => {
    const error = toApiError('boom')

    expect(error.kind).toBe('unknown')
    expect(error.status).toBe(0)
    expect(error.title).toBe('Unexpected error.')
  })
})

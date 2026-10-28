import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { useApi } from '@/hooks/use-api'
import { ApiError } from '@/lib/api/api-error'

// Promesa que el test resuelve o rechaza cuando quiere, para controlar el orden de las respuestas
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

const conflict = new ApiError({ kind: 'http', status: 409, title: 'Conflict' })

describe('useApi', () => {
  it('starts idle with no data or error', () => {
    const { result } = renderHook(() => useApi(() => Promise.resolve('x')))

    expect(result.current.status).toBe('idle')
    expect(result.current.isLoading).toBe(false)
    expect(result.current.data).toBeNull()
    expect(result.current.error).toBeNull()
  })

  it('goes through loading and ends in success with the data', async () => {
    const request = deferred<string>()
    const fn = vi.fn((id: string) =>
      request.promise.then((value) => `${value}-${id}`),
    )
    const { result } = renderHook(() => useApi(fn))

    let execution!: ReturnType<typeof result.current.execute>
    act(() => {
      execution = result.current.execute('1')
    })

    expect(fn).toHaveBeenCalledWith('1')
    expect(result.current.status).toBe('loading')
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      request.resolve('patient')
      await execution
    })

    expect(result.current.status).toBe('success')
    expect(result.current.isLoading).toBe(false)
    expect(result.current.data).toBe('patient-1')
    await expect(execution).resolves.toEqual({ data: 'patient-1', error: null })
  })

  it('stores the error and resolves it without rejecting when the call fails', async () => {
    const { result } = renderHook(() => useApi(() => Promise.reject(conflict)))

    let outcome!: Awaited<ReturnType<typeof result.current.execute>>
    await act(async () => {
      outcome = await result.current.execute()
    })

    expect(outcome).toEqual({ data: null, error: conflict })
    expect(result.current.status).toBe('error')
    expect(result.current.error).toBe(conflict)
  })

  it('wraps a non-ApiError failure', async () => {
    const { result } = renderHook(() =>
      useApi(() => Promise.reject(new Error('Boom'))),
    )

    await act(async () => {
      await result.current.execute()
    })

    expect(result.current.error).toBeInstanceOf(ApiError)
    expect(result.current.error?.kind).toBe('unknown')
    expect(result.current.error?.title).toBe('Boom')
  })

  it('keeps the previous data when a later call fails', async () => {
    const fn = vi.fn<() => Promise<string>>().mockResolvedValueOnce('first')
    fn.mockRejectedValueOnce(conflict)
    const { result } = renderHook(() => useApi(fn))

    await act(async () => {
      await result.current.execute()
    })
    await act(async () => {
      await result.current.execute()
    })

    expect(result.current.status).toBe('error')
    expect(result.current.error).toBe(conflict)
    expect(result.current.data).toBe('first')
  })

  it('keeps only the last call when an earlier response arrives later', async () => {
    const first = deferred<string>()
    const second = deferred<string>()
    const fn = vi
      .fn<() => Promise<string>>()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const { result } = renderHook(() => useApi(fn))

    let firstExecution!: ReturnType<typeof result.current.execute>
    let secondExecution!: ReturnType<typeof result.current.execute>
    act(() => {
      firstExecution = result.current.execute()
      secondExecution = result.current.execute()
    })
    await act(async () => {
      second.resolve('second')
      await secondExecution
    })
    await act(async () => {
      first.resolve('first')
      await firstExecution
    })

    expect(result.current.status).toBe('success')
    expect(result.current.data).toBe('second')
    // Cada execute resuelve igualmente su propio resultado
    await expect(firstExecution).resolves.toEqual({
      data: 'first',
      error: null,
    })
  })

  it('does not change the state or warn when the response arrives after unmounting', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    const request = deferred<string>()
    const { result, unmount } = renderHook(() => useApi(() => request.promise))

    let execution!: ReturnType<typeof result.current.execute>
    act(() => {
      execution = result.current.execute()
    })
    unmount()
    request.resolve('late')
    await execution

    expect(result.current.status).toBe('loading')
    expect(result.current.data).toBeNull()
    expect(consoleError).not.toHaveBeenCalled()
    consoleError.mockRestore()
  })

  it('reset returns to idle and ignores the call in flight', async () => {
    const request = deferred<string>()
    const fn = vi
      .fn<() => Promise<string>>()
      .mockResolvedValueOnce('first')
      .mockReturnValueOnce(request.promise)
    const { result } = renderHook(() => useApi(fn))

    await act(async () => {
      await result.current.execute()
    })
    let execution!: ReturnType<typeof result.current.execute>
    act(() => {
      execution = result.current.execute()
    })
    act(() => {
      result.current.reset()
    })
    await act(async () => {
      request.resolve('late')
      await execution
    })

    expect(result.current.status).toBe('idle')
    expect(result.current.data).toBeNull()
    expect(result.current.error).toBeNull()
  })

  it('setData changes the data with a value or a function without touching the status', async () => {
    const { result } = renderHook(() => useApi(() => Promise.resolve(1)))

    await act(async () => {
      await result.current.execute()
    })
    act(() => {
      result.current.setData(5)
    })

    expect(result.current.data).toBe(5)
    expect(result.current.status).toBe('success')

    act(() => {
      result.current.setData((previous) => (previous ?? 0) + 1)
    })

    expect(result.current.data).toBe(6)
    expect(result.current.status).toBe('success')
  })

  it('keeps execute, reset and setData stable and uses the latest fn', async () => {
    const { result, rerender } = renderHook(
      ({ value }) => useApi(() => Promise.resolve(value)),
      {
        initialProps: { value: 'a' },
      },
    )
    const { execute, reset, setData } = result.current

    rerender({ value: 'b' })

    expect(result.current.execute).toBe(execute)
    expect(result.current.reset).toBe(reset)
    expect(result.current.setData).toBe(setData)

    await act(async () => {
      await result.current.execute()
    })

    expect(result.current.data).toBe('b')
  })
})

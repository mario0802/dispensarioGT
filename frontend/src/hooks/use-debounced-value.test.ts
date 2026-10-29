import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useDebouncedValue } from '@/hooks/use-debounced-value'

describe('useDebouncedValue', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns the initial value', () => {
    const { result } = renderHook(() => useDebouncedValue('a', 300))

    expect(result.current).toBe('a')
  })

  it('reflects a change only after the delay', () => {
    const { result, rerender } = renderHook(
      ({ value }) => useDebouncedValue(value, 300),
      { initialProps: { value: 'a' } },
    )

    rerender({ value: 'b' })
    act(() => vi.advanceTimersByTime(299))
    expect(result.current).toBe('a')

    act(() => vi.advanceTimersByTime(1))
    expect(result.current).toBe('b')
  })

  it('restarts the timer on consecutive changes', () => {
    const { result, rerender } = renderHook(
      ({ value }) => useDebouncedValue(value, 300),
      { initialProps: { value: 'a' } },
    )

    rerender({ value: 'b' })
    act(() => vi.advanceTimersByTime(200))
    rerender({ value: 'c' })
    act(() => vi.advanceTimersByTime(200))
    expect(result.current).toBe('a')

    act(() => vi.advanceTimersByTime(100))
    expect(result.current).toBe('c')
  })
})

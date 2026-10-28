import { act, render, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ThemeProvider, useTheme } from '@/components/theme-provider'
import { mockPrefersDark } from '@/test/match-media'

const storageKey = 'dispensario-theme'

function wrapper({ children }: { children: React.ReactNode }) {
  return <ThemeProvider>{children}</ThemeProvider>
}

describe('ThemeProvider', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.className = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('applies the dark class when nothing is stored and the system prefers dark', () => {
    mockPrefersDark(true)

    render(<ThemeProvider>content</ThemeProvider>)

    expect(document.documentElement).toHaveClass('dark')
  })

  it('applies the light class and stores the theme on setTheme("light")', () => {
    mockPrefersDark(true)
    const { result } = renderHook(() => useTheme(), { wrapper })

    act(() => result.current.setTheme('light'))

    expect(document.documentElement).toHaveClass('light')
    expect(document.documentElement).not.toHaveClass('dark')
    expect(localStorage.getItem(storageKey)).toBe('light')
  })

  it('respects the stored theme on mount', () => {
    mockPrefersDark(false)
    localStorage.setItem(storageKey, 'dark')

    const { result } = renderHook(() => useTheme(), { wrapper })

    expect(result.current.theme).toBe('dark')
    expect(document.documentElement).toHaveClass('dark')
  })

  it('treats an invalid stored value as the default theme', () => {
    mockPrefersDark(false)
    localStorage.setItem(storageKey, 'purple')

    const { result } = renderHook(() => useTheme(), { wrapper })

    expect(result.current.theme).toBe('system')
    expect(document.documentElement).toHaveClass('light')
  })

  it('throws when useTheme is used outside the provider', () => {
    // React registra el error en consola al fallar el render: se silencia en este test
    vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => renderHook(() => useTheme())).toThrow(
      'useTheme must be used within a ThemeProvider.',
    )
  })
})

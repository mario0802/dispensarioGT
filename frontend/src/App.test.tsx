import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import App from '@/App'
import { ThemeProvider } from '@/components/theme-provider'
import { setAccessTokenProvider } from '@/lib/api/access-token'
import { setUnauthorizedHandler } from '@/lib/api/unauthorized-handler'
import { mockPrefersDark } from '@/test/match-media'

describe('App', () => {
  beforeEach(() => {
    mockPrefersDark(false)
    vi.stubGlobal('fetch', vi.fn<typeof fetch>())
    render(
      <ThemeProvider>
        <App />
      </ThemeProvider>,
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
    window.history.replaceState(null, '', '/')
    setAccessTokenProvider(null)
    setUnauthorizedHandler(null)
  })

  it('shows the login without a stored token', () => {
    expect(
      screen.getByRole('heading', { name: 'Dispensario Médico' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Iniciar sesión' }),
    ).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })
})

import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import App from '@/App'
import { ThemeProvider } from '@/components/theme-provider'
import { mockPrefersDark } from '@/test/match-media'

describe('App', () => {
  beforeEach(() => {
    mockPrefersDark(false)
    render(
      <ThemeProvider>
        <App />
      </ThemeProvider>,
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the app title', () => {
    expect(
      screen.getByRole('heading', { name: 'Dispensario Médico' }),
    ).toBeInTheDocument()
  })

  it('shows the start button', () => {
    expect(screen.getByRole('button', { name: 'Comenzar' })).toBeInTheDocument()
  })

  it('shows the theme toggle', () => {
    expect(
      screen.getByRole('button', { name: 'Cambiar tema' }),
    ).toBeInTheDocument()
  })
})

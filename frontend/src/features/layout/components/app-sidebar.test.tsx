import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ThemeProvider } from '@/components/theme-provider'
import { SidebarProvider } from '@/components/ui/sidebar'
import type { CurrentUserDto } from '@/features/auth/auth-types'
import { AppSidebar } from '@/features/layout/components/app-sidebar'
import { testUser } from '@/test/api-responses'
import { mockPrefersDark } from '@/test/match-media'

beforeEach(() => {
  mockPrefersDark(false)
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  document.documentElement.classList.remove('light', 'dark')
})

function renderSidebar(user: CurrentUserDto = testUser, onLogout = vi.fn()) {
  const userEvents = userEvent.setup()
  render(
    <ThemeProvider>
      <MemoryRouter initialEntries={['/']}>
        <SidebarProvider>
          <AppSidebar user={user} onLogout={onLogout} />
        </SidebarProvider>
      </MemoryRouter>
    </ThemeProvider>,
  )
  return { userEvents, onLogout }
}

const userMenuButton = () =>
  screen.getByRole('button', { name: 'Menú de usuario' })

describe('AppSidebar', () => {
  it('shows the app name, Inicio as the current page and the user', () => {
    renderSidebar()

    expect(screen.getByText('Dispensario Médico')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Inicio' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    const userMenu = userMenuButton()
    expect(within(userMenu).getByText('AT')).toBeInTheDocument()
    expect(within(userMenu).getByText('Ana Torres')).toBeInTheDocument()
    expect(within(userMenu).getByText('Recepcionista')).toBeInTheDocument()
  })

  it('opens the user menu with the email, Tema and Cerrar sesión', async () => {
    const { userEvents } = renderSidebar()

    await userEvents.click(userMenuButton())

    const menu = await screen.findByRole('menu')
    expect(within(menu).getByText(testUser.email)).toBeInTheDocument()
    expect(
      within(menu).getByRole('menuitem', { name: 'Tema' }),
    ).toBeInTheDocument()
    expect(
      within(menu).getByRole('menuitem', { name: 'Cerrar sesión' }),
    ).toBeInTheDocument()
  })

  it('changes the theme from the Tema submenu', async () => {
    const { userEvents } = renderSidebar()

    await userEvents.click(userMenuButton())
    await userEvents.click(
      await screen.findByRole('menuitem', { name: 'Tema' }),
    )
    const system = await screen.findByRole('menuitemradio', {
      name: 'Sistema',
    })
    expect(system).toHaveAttribute('aria-checked', 'true')
    // Con el teclado: en jsdom, el puntero que sale del submenú lo cierra antes del clic
    screen.getByRole('menuitemradio', { name: 'Oscuro' }).focus()
    await userEvents.keyboard('{Enter}')

    expect(document.documentElement).toHaveClass('dark')
    expect(localStorage.getItem('dispensario-theme')).toBe('dark')
  })

  it('calls logout from Cerrar sesión', async () => {
    const { userEvents, onLogout } = renderSidebar()

    await userEvents.click(userMenuButton())
    await userEvents.click(
      await screen.findByRole('menuitem', { name: 'Cerrar sesión' }),
    )

    expect(onLogout).toHaveBeenCalledOnce()
  })

  it.each([
    ['Admin', 'Administrador'],
    ['Doctor', 'Doctor'],
  ] as const)('shows the same sidebar for %s with its role', (role, label) => {
    renderSidebar({ ...testUser, role })

    expect(screen.getByRole('link', { name: 'Inicio' })).toBeInTheDocument()
    expect(within(userMenuButton()).getByText(label)).toBeInTheDocument()
  })
})

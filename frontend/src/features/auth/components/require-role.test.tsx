import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'

import type { UserRole } from '@/features/auth/auth-types'
import { RequireRole } from '@/features/auth/components/require-role'
import { testUser } from '@/test/api-responses'
import { TestAuthProvider } from '@/test/auth-context'

function renderGuard(role: UserRole) {
  render(
    <TestAuthProvider user={{ ...testUser, role }}>
      <MemoryRouter initialEntries={['/doctors']}>
        <Routes>
          <Route path="/" element={<div>Inicio</div>} />
          <Route
            path="/doctors"
            element={
              <RequireRole allowedRoles={['Admin', 'Receptionist']}>
                <div>Doctores</div>
              </RequireRole>
            }
          />
        </Routes>
      </MemoryRouter>
    </TestAuthProvider>,
  )
}

describe('RequireRole', () => {
  it('renders the children for an allowed role', () => {
    renderGuard('Admin')

    expect(screen.getByText('Doctores')).toBeInTheDocument()
  })

  it('redirects to / for a role that is not allowed', () => {
    renderGuard('Doctor')

    expect(screen.getByText('Inicio')).toBeInTheDocument()
    expect(screen.queryByText('Doctores')).not.toBeInTheDocument()
  })
})

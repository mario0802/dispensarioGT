import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { ConsultationPage } from '@/features/consultations/consultation-page'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ConsultationPage', () => {
  it('shows the placeholder and the link back to the appointments', () => {
    render(
      <MemoryRouter initialEntries={['/consultations/0193f2e0']}>
        <Routes>
          <Route
            path="/consultations/:consultationId"
            element={<ConsultationPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    expect(
      screen.getByRole('heading', { name: 'Consulta' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'El detalle de la consulta estará disponible próximamente.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Volver a citas' }),
    ).toHaveAttribute('href', '/appointments')
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

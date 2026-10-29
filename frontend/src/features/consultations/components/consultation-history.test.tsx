import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { ConsultationHistory } from '@/features/consultations/components/consultation-history'
import type { ConsultationDto } from '@/features/consultations/consultation-types'
import type { PaginatedResult } from '@/lib/api/paginated-result'
import { jsonResponse, problemResponse } from '@/test/api-responses'

const patientId = '0193f1c2-0000-0000-0000-000000000001'
const currentId = 'current'

function makeConsultation(
  id: string,
  overrides: Partial<ConsultationDto> = {},
): ConsultationDto {
  return {
    id,
    patientId,
    patientFullName: 'Ana Pérez',
    doctorId: '0193f1b8-0000-0000-0000-000000000001',
    doctorFullName: 'Luis Andrade',
    appointmentId: null,
    reason: `Motivo ${id}`,
    diagnosis: `Diagnóstico ${id}`,
    notes: `Notas ${id}`,
    status: 'Closed',
    startedAtUtc: '2026-10-01T14:00:00Z',
    closedAtUtc: '2026-10-01T14:30:00Z',
    startedAtLocal: '2026-10-01T09:00:00',
    closedAtLocal: '2026-10-01T09:30:00',
    createdAtUtc: '2026-10-01T14:00:00Z',
    ...overrides,
  }
}

const current = makeConsultation(currentId, {
  status: 'Open',
  reason: 'Consulta de hoy',
  closedAtLocal: null,
})

function pageOf(
  data: ConsultationDto[],
  count: number,
  pageIndex = 0,
): PaginatedResult<ConsultationDto> {
  return { pageIndex, pageSize: 5, count, data }
}

let fetchMock: Mock<typeof fetch>
let respond: (url: URL) => Response

beforeEach(() => {
  respond = () => jsonResponse(pageOf([current, makeConsultation('a')], 2))
  fetchMock = vi.fn<typeof fetch>(async (input) =>
    respond(new URL(String(input))),
  )
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

function renderHistory() {
  const user = userEvent.setup()
  render(
    <ConsultationHistory
      patientId={patientId}
      currentConsultationId={currentId}
    />,
  )
  return user
}

const requestedUrls = () =>
  fetchMock.mock.calls.map(([input]) => new URL(String(input)))

describe('ConsultationHistory', () => {
  it('asks for the first page of 5 and hides the current consultation', async () => {
    renderHistory()

    expect(await screen.findByText('1 consulta previa')).toBeInTheDocument()
    const url = requestedUrls()[0]
    expect(url.pathname).toBe(`/api/patients/${patientId}/consultations`)
    expect(url.searchParams.get('pageIndex')).toBe('0')
    expect(url.searchParams.get('pageSize')).toBe('5')
    expect(screen.getByText('Motivo a')).toBeInTheDocument()
    expect(screen.queryByText('Consulta de hoy')).not.toBeInTheDocument()
  })

  it('expands a consultation to show its diagnosis and notes', async () => {
    const user = renderHistory()

    await user.click(await screen.findByRole('button', { name: /Motivo a/ }))

    expect(screen.getByText('Diagnóstico a')).toBeVisible()
    expect(screen.getByText('Notas a')).toBeVisible()
    expect(screen.getByText('Cerrada el 01/10/2026 09:30')).toBeVisible()
  })

  it('moves to the next page', async () => {
    respond = (url) =>
      url.searchParams.get('pageIndex') === '1'
        ? jsonResponse(pageOf([makeConsultation('f')], 6, 1))
        : jsonResponse(
            pageOf(
              [
                current,
                ...['a', 'b', 'c', 'd'].map((id) => makeConsultation(id)),
              ],
              6,
            ),
          )
    const user = renderHistory()

    expect(await screen.findByText('5 consultas previas')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))

    expect(await screen.findByText('Motivo f')).toBeInTheDocument()
    expect(requestedUrls().at(-1)!.searchParams.get('pageIndex')).toBe('1')
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
  })

  it('says when there are no previous consultations', async () => {
    respond = () => jsonResponse(pageOf([current], 1))
    renderHistory()

    expect(
      await screen.findByText('El paciente no tiene consultas previas.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Sin consultas previas')).toBeInTheDocument()
    expect(screen.queryByText('Siguiente')).not.toBeInTheDocument()
  })

  it('retries after an error', async () => {
    respond = () => problemResponse(500)
    const user = renderHistory()

    expect(
      await screen.findByText('No se pudo cargar la historia clínica.'),
    ).toBeInTheDocument()
    respond = () => jsonResponse(pageOf([current, makeConsultation('a')], 2))
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(await screen.findByText('Motivo a')).toBeInTheDocument()
  })
})

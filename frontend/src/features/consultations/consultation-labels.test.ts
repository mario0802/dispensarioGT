import { describe, expect, it } from 'vitest'

import {
  consultationStatusLabels,
  isConsultationEditable,
} from '@/features/consultations/consultation-labels'
import type { ConsultationDto } from '@/features/consultations/consultation-types'

const doctorId = '0193f1b8-0000-0000-0000-000000000001'

const consultation: ConsultationDto = {
  id: '0193f2e0-0000-0000-0000-000000000001',
  patientId: '0193f1c2-0000-0000-0000-000000000001',
  patientFullName: 'Ana Pérez',
  doctorId,
  doctorFullName: 'Luis Andrade',
  appointmentId: '0193f1d0-0000-0000-0000-000000000001',
  reason: 'Dolor de cabeza',
  diagnosis: null,
  notes: null,
  status: 'Open',
  startedAtUtc: '2026-11-03T14:32:10Z',
  closedAtUtc: null,
  startedAtLocal: '2026-11-03T09:32:10',
  closedAtLocal: null,
  createdAtUtc: '2026-11-03T14:32:10Z',
}

describe('consultationStatusLabels', () => {
  it('translates both statuses', () => {
    expect(consultationStatusLabels).toEqual({
      Open: 'Abierta',
      Closed: 'Cerrada',
    })
  })
})

describe('isConsultationEditable', () => {
  it('is editable when open and owned by the doctor', () => {
    expect(isConsultationEditable(consultation, doctorId)).toBe(true)
  })

  it('is not editable when closed', () => {
    expect(
      isConsultationEditable({ ...consultation, status: 'Closed' }, doctorId),
    ).toBe(false)
  })

  it('is not editable when it belongs to another doctor', () => {
    expect(
      isConsultationEditable(
        consultation,
        '0193f1b8-0000-0000-0000-000000000002',
      ),
    ).toBe(false)
  })

  it('is not editable without a linked doctor', () => {
    expect(isConsultationEditable(consultation, null)).toBe(false)
  })
})

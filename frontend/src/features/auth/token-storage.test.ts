import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  clearStoredToken,
  readStoredToken,
  saveStoredToken,
  TOKEN_STORAGE_KEY,
} from '@/features/auth/token-storage'

const now = new Date('2026-10-28T12:00:00Z')
const validToken = {
  accessToken: 'abc',
  expiresAtUtc: '2026-10-28T13:00:00Z',
}

afterEach(() => {
  localStorage.clear()
  vi.restoreAllMocks()
})

describe('token storage', () => {
  it('saves and reads a valid token', () => {
    saveStoredToken(validToken)

    expect(readStoredToken(now)).toEqual(validToken)
  })

  it('returns null without a stored key', () => {
    expect(readStoredToken(now)).toBeNull()
  })

  it.each([
    ['invalid JSON', '{not json'],
    ['a non-object value', '"abc"'],
    [
      'a missing accessToken',
      JSON.stringify({ expiresAtUtc: validToken.expiresAtUtc }),
    ],
    ['a missing expiresAtUtc', JSON.stringify({ accessToken: 'abc' })],
    [
      'a non-string accessToken',
      JSON.stringify({ ...validToken, accessToken: 1 }),
    ],
    [
      'an invalid date',
      JSON.stringify({ ...validToken, expiresAtUtc: 'soon' }),
    ],
    [
      'an expired token',
      JSON.stringify({ ...validToken, expiresAtUtc: '2026-10-28T11:00:00Z' }),
    ],
    [
      'a token expiring right now',
      JSON.stringify({ ...validToken, expiresAtUtc: now.toISOString() }),
    ],
  ])('returns null and removes the key for %s', (_, raw) => {
    localStorage.setItem(TOKEN_STORAGE_KEY, raw)

    expect(readStoredToken(now)).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('clearStoredToken removes the key', () => {
    saveStoredToken(validToken)

    clearStoredToken()

    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('returns null without throwing when localStorage throws', () => {
    const fail = () => {
      throw new DOMException('Blocked', 'SecurityError')
    }
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(fail)
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(fail)
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(fail)

    expect(() => saveStoredToken(validToken)).not.toThrow()
    expect(() => clearStoredToken()).not.toThrow()
    expect(readStoredToken(now)).toBeNull()
  })
})

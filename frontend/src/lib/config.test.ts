import { describe, expect, it } from 'vitest'

import { readConfig } from '@/lib/config'

describe('readConfig', () => {
  it('returns the API base URL without trailing slash', () => {
    const config = readConfig({ VITE_API_BASE_URL: ' http://localhost:5168/ ' })

    expect(config.apiBaseUrl).toBe('http://localhost:5168')
  })

  it('throws when the variable is missing', () => {
    expect(() => readConfig({})).toThrow('VITE_API_BASE_URL is not configured.')
  })

  it.each(['', '   '])('throws when the variable is blank (%j)', (value) => {
    expect(() => readConfig({ VITE_API_BASE_URL: value })).toThrow(
      'VITE_API_BASE_URL is not configured.',
    )
  })
})

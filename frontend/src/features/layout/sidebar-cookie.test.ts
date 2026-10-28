import { afterEach, describe, expect, it } from 'vitest'

import {
  readSidebarOpen,
  SIDEBAR_COOKIE_NAME,
} from '@/features/layout/sidebar-cookie'

function setCookie(name: string, value: string) {
  document.cookie = `${name}=${value}; path=/`
}

afterEach(() => {
  document.cookie = `${SIDEBAR_COOKIE_NAME}=; path=/; max-age=0`
  document.cookie = 'other=; path=/; max-age=0'
})

describe('readSidebarOpen', () => {
  it('is open without the cookie', () => {
    expect(readSidebarOpen()).toBe(true)
  })

  it('is closed when the cookie is false', () => {
    setCookie(SIDEBAR_COOKIE_NAME, 'false')

    expect(readSidebarOpen()).toBe(false)
  })

  it('is open when the cookie is true', () => {
    setCookie(SIDEBAR_COOKIE_NAME, 'true')

    expect(readSidebarOpen()).toBe(true)
  })

  it('ignores other cookies and unexpected values', () => {
    setCookie('other', 'false')
    expect(readSidebarOpen()).toBe(true)

    setCookie(SIDEBAR_COOKIE_NAME, 'maybe')
    expect(readSidebarOpen()).toBe(true)
  })
})

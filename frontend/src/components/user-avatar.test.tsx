import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { UserAvatar } from '@/components/user-avatar'

describe('UserAvatar', () => {
  it('shows the initials and no image without imageUrl', () => {
    const { container } = render(
      <UserAvatar firstName="Ana" lastName="Torres" />,
    )

    expect(screen.getByText('AT')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
  })

  it('uses size-8 by default and size-20 with size lg', () => {
    const { container, rerender } = render(
      <UserAvatar firstName="Ana" lastName="Torres" />,
    )
    const avatar = () => container.querySelector('[data-slot="avatar"]')
    expect(avatar()).toHaveClass('size-8')

    rerender(<UserAvatar firstName="Ana" lastName="Torres" size="lg" />)

    expect(avatar()).toHaveClass('size-20')
    expect(avatar()).not.toHaveClass('size-8')
  })
})

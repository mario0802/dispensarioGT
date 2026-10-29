import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'

import {
  SearchCombobox,
  type SearchComboboxOption,
} from '@/features/appointments/components/search-combobox'
import { ApiError } from '@/lib/api/api-error'

const ana: SearchComboboxOption = {
  id: 'p-1',
  label: 'Ana Pérez',
  description: 'Cédula 0102030401',
}
const luis: SearchComboboxOption = { id: 'p-2', label: 'Luis Mora' }

function renderCombobox({
  search = vi.fn(async () => [ana, luis]),
  clearable = false,
  initialValue = null,
}: {
  search?: (text: string) => Promise<SearchComboboxOption[]>
  clearable?: boolean
  initialValue?: SearchComboboxOption | null
} = {}) {
  const onChange = vi.fn()

  function Harness() {
    const [value, setValue] = useState(initialValue)
    return (
      <SearchCombobox
        value={value}
        onChange={(option) => {
          onChange(option)
          setValue(option)
        }}
        search={search}
        placeholder="Selecciona un paciente"
        searchPlaceholder="Buscar por nombre o documento"
        emptyText="No se encontraron pacientes activos."
        ariaLabel="Paciente"
        clearable={clearable}
        clearLabel="Todos los pacientes"
      />
    )
  }

  const user = userEvent.setup()
  render(<Harness />)
  return { user, onChange, search }
}

const trigger = () => screen.getByRole('combobox', { name: 'Paciente' })

describe('SearchCombobox', () => {
  it('searches without text on open and lists the options', async () => {
    const { user, search } = renderCombobox()

    expect(trigger()).toHaveTextContent('Selecciona un paciente')
    await user.click(trigger())

    expect(
      await screen.findByRole('option', { name: /Ana Pérez/ }),
    ).toBeInTheDocument()
    expect(screen.getByText('Cédula 0102030401')).toBeInTheDocument()
    expect(
      screen.getByRole('option', { name: /Luis Mora/ }),
    ).toBeInTheDocument()
    expect(search).toHaveBeenCalledTimes(1)
    expect(search).toHaveBeenCalledWith('')
  })

  it('searches once with the trimmed text after the debounce', async () => {
    // El servidor decide qué coincide: cmdk no debe ocultar Luis aunque no contenga el texto
    const search = vi.fn(async (text: string) => (text ? [luis] : [ana, luis]))
    const { user } = renderCombobox({ search })
    await user.click(trigger())
    await screen.findByRole('option', { name: /Ana Pérez/ })

    await user.type(
      screen.getByPlaceholderText('Buscar por nombre o documento'),
      ' 0102 ',
    )

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('0102'))
    expect(search).toHaveBeenCalledTimes(2)
    expect(
      await screen.findByRole('option', { name: /Luis Mora/ }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('option', { name: /Ana Pérez/ }),
    ).not.toBeInTheDocument()
  })

  it('selects an option, closes and shows its label', async () => {
    const { user, onChange } = renderCombobox()
    await user.click(trigger())

    await user.click(await screen.findByRole('option', { name: /Luis Mora/ }))

    expect(onChange).toHaveBeenCalledWith(luis)
    expect(trigger()).toHaveTextContent('Luis Mora')
    expect(trigger()).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('option')).not.toBeInTheDocument()
  })

  it('shows the empty text without results', async () => {
    const { user } = renderCombobox({ search: vi.fn(async () => []) })
    await user.click(trigger())

    expect(
      await screen.findByText('No se encontraron pacientes activos.'),
    ).toBeInTheDocument()
  })

  it('shows a message when the search fails', async () => {
    const search = vi.fn(async () => {
      throw new ApiError({ kind: 'network', status: 0, title: 'Network' })
    })
    const { user } = renderCombobox({ search })
    await user.click(trigger())

    expect(
      await screen.findByText('No se pudo buscar. Inténtalo de nuevo.'),
    ).toBeInTheDocument()
  })

  it('clears the value with the clear item', async () => {
    const { user, onChange } = renderCombobox({
      clearable: true,
      initialValue: ana,
    })
    expect(trigger()).toHaveTextContent('Ana Pérez')
    await user.click(trigger())

    await user.click(
      await screen.findByRole('option', { name: /Todos los pacientes/ }),
    )

    expect(onChange).toHaveBeenCalledWith(null)
    expect(trigger()).toHaveTextContent('Selecciona un paciente')
  })
})

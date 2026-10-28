import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest'

import { RegisterForm } from '@/features/auth/components/register-form'
import { jsonResponse, problemResponse } from '@/test/api-responses'

let fetchMock: Mock<typeof fetch>

beforeEach(() => {
  fetchMock = vi.fn<typeof fetch>()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

// Muestra el email que llega al login en el state del router
function LoginStub() {
  const location = useLocation()
  const state = location.state as { registeredEmail?: string } | null
  return <div data-testid="login">{state?.registeredEmail}</div>
}

function renderForm() {
  const user = userEvent.setup()
  render(
    <MemoryRouter initialEntries={['/register']}>
      <Routes>
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/login" element={<LoginStub />} />
      </Routes>
    </MemoryRouter>,
  )
  return {
    user,
    submit: () =>
      user.click(screen.getByRole('button', { name: 'Crear cuenta' })),
  }
}

type Form = ReturnType<typeof renderForm>

interface FormData {
  firstName: string
  lastName: string
  documentNumber: string
  email: string
  phone: string
  password: string
  confirmPassword: string
}

const validData: FormData = {
  firstName: 'Laura',
  lastName: 'Vega',
  documentNumber: '1712345678',
  email: 'laura.vega@dispensario.local',
  phone: '0991234567',
  password: 'Passw0rd',
  confirmPassword: 'Passw0rd',
}

const labels: Record<keyof FormData, string> = {
  firstName: 'Nombre',
  lastName: 'Apellido',
  documentNumber: 'Número de documento',
  email: 'Email',
  phone: 'Teléfono (opcional)',
  password: 'Contraseña',
  confirmPassword: 'Confirmar contraseña',
}

async function fill({ user }: Form, overrides: Partial<FormData> = {}) {
  const data = { ...validData, ...overrides }
  for (const key of Object.keys(labels) as (keyof FormData)[]) {
    if (data[key] !== '') {
      await user.type(screen.getByLabelText(labels[key]), data[key])
    }
  }
}

async function fillAndSubmit(overrides: Partial<FormData> = {}) {
  const form = renderForm()
  await fill(form, overrides)
  await form.submit()
  return form
}

function requestBody(): unknown {
  const init = fetchMock.mock.calls[0][1]
  return JSON.parse(init?.body as string)
}

describe('RegisterForm', () => {
  it('shows the required messages and does not call the API when empty', async () => {
    const { submit } = renderForm()

    await submit()

    expect(await screen.findByText('Ingresa tu nombre.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa tu apellido.')).toBeInTheDocument()
    expect(
      screen.getByText('Ingresa el número de documento.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Ingresa tu email.')).toBeInTheDocument()
    expect(screen.getByText('Ingresa una contraseña.')).toBeInTheDocument()
    expect(screen.getByText('Confirma la contraseña.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('selects Cédula by default', () => {
    renderForm()

    expect(
      screen.getByRole('combobox', { name: 'Tipo de documento' }),
    ).toHaveTextContent('Cédula')
  })

  it('rejects an invalid email', async () => {
    await fillAndSubmit({ email: 'laura' })

    expect(
      await screen.findByText('Ingresa un email válido.'),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects an invalid phone', async () => {
    await fillAndSubmit({ phone: 'abc' })

    expect(
      await screen.findByText(
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      ),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it.each([
    ['Password', 'La contraseña debe tener al menos un dígito.'],
    ['12345678', 'La contraseña debe tener al menos una letra.'],
    ['Pass1', 'La contraseña debe tener al menos 8 caracteres.'],
  ])('rejects the password %s', async (password, message) => {
    await fillAndSubmit({ password, confirmPassword: password })

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects a different confirmation', async () => {
    await fillAndSubmit({ confirmPassword: 'Passw0rd2' })

    expect(
      await screen.findByText('Las contraseñas no coinciden.'),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends the trimmed data with the chosen document type and goes to the login', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: 'new-id' }, 201))
    const form = renderForm()
    await fill(form, {
      firstName: '  Laura ',
      lastName: ' Vega  ',
      documentNumber: ' AB123 ',
      email: ' laura.vega@dispensario.local ',
      phone: ' 0991234567 ',
    })
    await form.user.click(
      screen.getByRole('combobox', { name: 'Tipo de documento' }),
    )
    await form.user.click(screen.getByRole('option', { name: 'Pasaporte' }))

    await form.submit()

    expect(await screen.findByTestId('login')).toHaveTextContent(
      'laura.vega@dispensario.local',
    )
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/auth\/register$/)
    expect(init?.method).toBe('POST')
    expect(requestBody()).toEqual({
      email: 'laura.vega@dispensario.local',
      password: 'Passw0rd',
      firstName: 'Laura',
      lastName: 'Vega',
      documentType: 'Passport',
      documentNumber: 'AB123',
      phone: '0991234567',
    })
  })

  it('sends a null phone when it is empty', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: 'new-id' }, 201))

    await fillAndSubmit({ phone: '' })

    await screen.findByTestId('login')
    expect(requestBody()).toMatchObject({
      documentType: 'NationalId',
      phone: null,
    })
  })

  it('disables the button while submitting', async () => {
    fetchMock.mockReturnValue(new Promise<Response>(() => {}))

    await fillAndSubmit()

    const button = await screen.findByRole('button', {
      name: 'Creando cuenta…',
    })
    expect(button).toBeDisabled()
  })

  it.each([
    [
      'a 409',
      () => problemResponse(409, 'Conflict'),
      'Ya existe un usuario con ese email o esa identificación.',
    ],
    [
      'a 400',
      () => problemResponse(400, 'Bad Request'),
      'Revisa los datos e inténtalo de nuevo.',
    ],
    [
      'a 500',
      () => problemResponse(500),
      'Ocurrió un error inesperado. Inténtalo de nuevo.',
    ],
  ])(
    'shows the message for %s and keeps the values',
    async (_, response, message) => {
      fetchMock.mockResolvedValue(response())

      await fillAndSubmit()

      expect(await screen.findByText(message)).toBeInTheDocument()
      expect(screen.getByLabelText('Email')).toHaveValue(validData.email)
      expect(screen.getByLabelText('Contraseña')).toHaveValue(
        validData.password,
      )
      expect(screen.queryByTestId('login')).not.toBeInTheDocument()
    },
  )

  it('shows the network error message', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    await fillAndSubmit()

    expect(
      await screen.findByText('No se pudo conectar con el servidor.'),
    ).toBeInTheDocument()
  })

  it('toggles the visibility of each password', async () => {
    const { user } = renderForm()
    const password = screen.getByLabelText('Contraseña')
    const confirmation = screen.getByLabelText('Confirmar contraseña')

    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(password).toHaveAttribute('type', 'text')
    expect(confirmation).toHaveAttribute('type', 'password')

    await user.click(
      screen.getByRole('button', { name: 'Mostrar confirmación' }),
    )
    expect(confirmation).toHaveAttribute('type', 'text')

    await user.click(screen.getByRole('button', { name: 'Ocultar contraseña' }))
    await waitFor(() => expect(password).toHaveAttribute('type', 'password'))
  })
})

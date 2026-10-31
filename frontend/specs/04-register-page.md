# SPEC 04 — Página de registro

> **Status:** Implemented
> **Depends on:** SPEC 01, SPEC 02, SPEC 03
> **Date:** 2026-10-28
> **Objective:** Crear la página pública `/register` que da de alta un recepcionista con `POST /api/auth/register` y, tras crearlo, lleva al login con un aviso y el email ya rellenado.

## Por qué este spec existe

El backend expone un autoregistro anónimo (`POST /api/auth/register`) que siempre crea un usuario `Receptionist` activo, pero el spec 03 dejó el registro fuera del frontend.
Este spec añade la página, su formulario con las mismas reglas que el backend y los enlaces entre login y registro.
La API no devuelve token al registrar: después hay que iniciar sesión, y este spec lo resuelve llevando al login con el email ya rellenado.

## Scope

**In:**

- Componente `select` de shadcn/ui (sobre `radix-ui`, que ya es dependencia: no se añade ningún paquete npm).
- Tipos y función de API del registro en `features/auth/` (`auth-types.ts`, `auth-api.ts`).
- Nombres de los tipos de documento en español (`features/auth/document-type-labels.ts`).
- Página `features/auth/register-page.tsx` y formulario `features/auth/components/register-form.tsx`.
- Ruta pública `/register` en `app-routes.tsx` (con sesión, redirige a `/`).
- En el login: enlace a `/register`, aviso de cuenta creada y email precargado cuando se llega desde el registro.
- Simulaciones de punteros en `src/test/setup.ts` que el `select` de Radix necesita en jsdom.
- Tests de Vitest del formulario, de las rutas nuevas y del aviso en el login.
- Actualizar `frontend/CLAUDE.md`: estado, componentes de shadcn, estructura y backlog.

**Out of scope (for future specs):**

- Login automático tras el registro.
- Elegir el rol (la API lo ignora) y el alta de usuarios por el Admin (`POST /api/users`): spec del módulo de usuarios.
- Marcar en cada campo los errores del backend (`ValidationErrors` o el 409 por email o identificación).
- Indicador de fortaleza de la contraseña, captcha, verificación del email y términos de uso.
- Layout por rol y navegación entre módulos.
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/src/
├─ app-routes.tsx                     # + ruta /register
├─ app-routes.test.tsx                # + casos del registro
├─ components/ui/
│  └─ select.tsx                      # nuevo (shadcn)
├─ features/auth/
│  ├─ auth-types.ts                   # + DocumentType, RegisterRequest, RegisterResponse
│  ├─ auth-api.ts                     # + registerAccount
│  ├─ document-type-labels.ts         # nuevo
│  ├─ login-page.tsx                  # + aviso de cuenta creada y enlace a /register
│  ├─ register-page.tsx               # nuevo
│  └─ components/
│     ├─ login-form.tsx               # + prop defaultEmail
│     ├─ register-form.tsx            # nuevo
│     └─ register-form.test.tsx       # nuevo
└─ test/
   └─ setup.ts                        # + hasPointerCapture, releasePointerCapture y scrollIntoView simulados
```

### Tipos (`features/auth/auth-types.ts`)

```ts
export type DocumentType = 'NationalId' | 'Passport'

// POST /api/auth/register: sin rol, la API siempre crea un Receptionist
export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  documentType: DocumentType
  documentNumber: string
  phone: string | null
}

export interface RegisterResponse {
  id: string
}
```

`CurrentUserDto.identificationType` no cambia en este spec.

### API (`features/auth/auth-api.ts`)

```ts
export const registerAccount = (request: RegisterRequest) =>
  api.post<RegisterResponse>('/api/auth/register', request)
```

Se llama `registerAccount` y no `register` para no chocar con el `register` de React Hook Form dentro del formulario.

### Nombres de los tipos de documento (`features/auth/document-type-labels.ts`)

```ts
export const documentTypeLabels: Record<DocumentType, string> = {
  NationalId: 'Cédula',
  Passport: 'Pasaporte',
}
```

Vive en `features/auth/` porque solo lo usa el registro; cuando lo usen usuarios o pacientes, se mueve a `src/` según la regla de componentes compartidos.

### Esquema del formulario (`components/register-form.tsx`)

Mismas reglas que `RegisterUserCommandValidator` y los value objects del backend (`Email`, `FullName`, `Document`, `PhoneNumber` y `ValidPassword`). Mensajes en español:

```ts
const registerSchema = z
  .object({
    firstName: z
      .string()
      .trim()
      .min(1, 'Ingresa tu nombre.')
      .max(100, 'El nombre no puede superar 100 caracteres.'),
    lastName: z
      .string()
      .trim()
      .min(1, 'Ingresa tu apellido.')
      .max(100, 'El apellido no puede superar 100 caracteres.'),
    documentType: z.enum(['NationalId', 'Passport']),
    documentNumber: z
      .string()
      .trim()
      .min(1, 'Ingresa el número de documento.')
      .max(20, 'El número de documento no puede superar 20 caracteres.'),
    email: z
      .string()
      .trim()
      .min(1, 'Ingresa tu email.')
      .max(254, 'El email no puede superar 254 caracteres.')
      .pipe(z.email('Ingresa un email válido.')),
    // Opcional: vacío se envía como null (la API rechaza un teléfono vacío)
    phone: z
      .string()
      .trim()
      .refine(
        (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
        'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
      )
      .transform((value) => (value === '' ? null : value)),
    // Sin trim, como en el login
    password: z
      .string()
      .min(1, 'Ingresa una contraseña.')
      .min(8, 'La contraseña debe tener al menos 8 caracteres.')
      .max(72, 'La contraseña no puede superar 72 caracteres.')
      .regex(/[A-Za-z]/, 'La contraseña debe tener al menos una letra.')
      .regex(/[0-9]/, 'La contraseña debe tener al menos un dígito.'),
    confirmPassword: z.string().min(1, 'Confirma la contraseña.'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Las contraseñas no coinciden.',
  })
```

- Se muestra un solo mensaje por campo, el primero que falle (la sintaxis exacta para cortar en el primer error, como el `.pipe` del login, se confirma con Context7 en el paso 1; los mensajes no cambian).
- `confirmPassword` no se envía a la API: la petición se arma con los demás campos ya transformados (`trim` y `phone` `null`).
- Valores por defecto: todos los textos vacíos y `documentType: 'NationalId'`.

### Formulario (`components/register-form.tsx`)

Usa React Hook Form con `zodResolver` y llama a `registerAccount` con `useApi` (`const { error, isLoading, execute } = useApi(registerAccount)`). El `select` se conecta con `Controller`.

| Elemento                         | Detalle                                                                                                                                                         |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nombre** y **Apellido**        | En la misma fila desde `sm` (una columna en móvil). `autoComplete` `given-name` y `family-name`.                                                                  |
| **Tipo de documento** y **Número de documento** | En la misma fila desde `sm`. El tipo es un `Select` de shadcn con las opciones de `documentTypeLabels` (Cédula por defecto); el número, un `Input` con `autoComplete="off"`. |
| **Email**                        | `type="email"`, `autoComplete="email"`.                                                                                                                          |
| **Teléfono (opcional)**          | `type="tel"`, `autoComplete="tel"`.                                                                                                                              |
| **Contraseña**                   | `type="password"`, `autoComplete="new-password"`, botón del ojo (`Eye`/`EyeOff`) con `aria-label` "Mostrar contraseña" / "Ocultar contraseña". Debajo, `FieldDescription`: "De 8 a 72 caracteres, con al menos una letra y un dígito." |
| **Confirmar contraseña**         | Igual que la contraseña, con su propio botón del ojo (`aria-label` "Mostrar confirmación" / "Ocultar confirmación").                                             |
| Botón **Crear cuenta**           | `type="submit"`, ancho completo. Con `isLoading`: deshabilitado, con `Loader2` girando y el texto "Creando cuenta…".                                              |
| Validación                       | Al enviar; después de un intento, se revalida al escribir (por defecto de React Hook Form). Con errores de validación no se llama a la API.                      |
| Error de la API                  | `Alert` `destructive` encima de los campos con el mensaje de la tabla siguiente; se oculta al volver a enviar. Los valores del formulario se conservan.            |
| Éxito (201)                      | `navigate('/login', { replace: true, state: { registeredEmail } })`, con el email ya recortado.                                                                  |

| Error                          | Mensaje                                                       |
| ------------------------------ | ------------------------------------------------------------- |
| `status === 409`               | Ya existe un usuario con ese email o esa identificación.      |
| `status === 400`               | Revisa los datos e inténtalo de nuevo.                        |
| `kind === 'network'`           | No se pudo conectar con el servidor.                          |
| Cualquier otro (500, …)        | Ocurrió un error inesperado. Inténtalo de nuevo.              |

### Página de registro (`register-page.tsx`)

- Con `status: 'authenticated'`, `<Navigate to="/" replace />`.
- Si no: pantalla completa, `Card` centrado de ancho `max-w-md`, título **Dispensario Médico** (`h1`), descripción "Crea una cuenta de recepcionista.", el `RegisterForm` y, al pie, "¿Ya tienes cuenta? **Inicia sesión**" con un `Link` de React Router a `/login`.

### Cambios en el login

- `LoginPage` lee `location.state.registeredEmail` (solo si es un texto no vacío; cualquier otro valor se ignora):
  - Muestra encima del formulario un `Alert` (variante por defecto, icono `CheckCircle2`, `role="status"`): "Cuenta creada. Inicia sesión para continuar.".
  - Pasa el email a `LoginForm` como `defaultEmail`.
- `LoginForm` acepta `defaultEmail?: string` (por defecto `''`) como valor inicial del campo Email; con email precargado, el foco no se mueve (como hasta ahora, sin autofocus).
- Al pie del card: "¿No tienes cuenta? **Regístrate**" con un `Link` a `/register`.
- El aviso vive en el `state` del router: desaparece al recargar o al navegar, y no deja nada en la URL. Convive con `from` (no lo altera).

### Rutas (`src/app-routes.tsx`)

| Ruta        | Elemento        | Comportamiento                                               |
| ----------- | --------------- | ------------------------------------------------------------ |
| `/register` | `RegisterPage`  | Pública. Con `status: 'authenticated'`, redirige a `/`.        |

Las demás rutas no cambian.

### Tests

Mismas pautas que el spec 03: `fetch` simulado con `vi.stubGlobal`, `@testing-library/user-event`, `MemoryRouter` en las pruebas de rutas y limpieza de `localStorage` y registros globales en `afterEach`. `src/test/setup.ts` añade simulaciones de `Element.prototype.hasPointerCapture`, `releasePointerCapture` y `scrollIntoView` (jsdom no los tiene y el `select` de Radix los usa); solo se definen si no existen.

| Archivo                   | Casos                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `register-form.test.tsx`  | Enviar vacío → mensajes de nombre, apellido, número de documento, email, contraseña y confirmación, sin llamar a `fetch`; email inválido; teléfono inválido (`abc`); contraseña sin dígito, sin letra y de menos de 8; confirmación distinta → "Las contraseñas no coinciden."; datos válidos con Pasaporte elegido en el `select` → `POST /api/auth/register` con el cuerpo exacto (textos recortados, `documentType: 'Passport'`, sin `confirmPassword`); teléfono vacío → `phone: null`; botón deshabilitado con "Creando cuenta…" mientras carga; 409, 400, error de red y 500 → su mensaje y los valores se conservan; el botón del ojo alterna el `type` de cada contraseña. |
| `app-routes.test.tsx`     | (se añade) Sin sesión, `/register` muestra el registro; el enlace **Regístrate** del login lleva al registro y **Inicia sesión** vuelve; con sesión, `/register` → inicio; registro correcto (201) → login con "Cuenta creada. Inicia sesión para continuar." y el email precargado, y un login posterior entra al inicio; `/login` sin `state` no muestra el aviso.                                                                                                                                                   |

## Implementation plan

1. Consultar con Context7 la documentación actual del componente `select` de shadcn/ui (y su uso con React Hook Form) y cómo cortar en el primer error en Zod 4. Añadir `select` con el CLI de shadcn (corrigiendo el import de `cn` a `@/lib/utils`) y comprobar que `package.json` no cambia. Verificar: `pnpm build` y `pnpm lint` pasan.
2. Añadir los tipos a `auth-types.ts`, `registerAccount` a `auth-api.ts` y crear `document-type-labels.ts`. Añadir las simulaciones de punteros a `src/test/setup.ts`. Verificar: `pnpm test` sigue pasando.
3. Crear `components/register-form.tsx` con su test. Verificar: `pnpm test` pasa.
4. Crear `register-page.tsx`, añadir la ruta `/register`, el aviso y el enlace en `login-page.tsx` y la prop `defaultEmail` en `login-form.tsx`, y ampliar `app-routes.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` pasan.
5. Probar a mano con el backend corriendo (`dotnet run`) y `pnpm dev`: registro correcto y login con la cuenta nueva, email repetido (409), documento repetido (409), backend parado, enlaces en ambos sentidos y `/register` con sesión.
6. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** página de registro (`/register`) con aviso y email precargado en el login.
   - **Stack:** `select` en la lista de componentes de shadcn.
   - **Estructura:** `register-page`, `components/register-form` y `document-type-labels` en `features/auth/`; nota de las simulaciones de punteros en `src/test/setup.ts`.
   - **Backlog:** fila 04 con este spec.

## Acceptance criteria

- [x] `package.json` no cambia (solo se añade `src/components/ui/select.tsx`).
- [x] Sin sesión, `/register` muestra **Dispensario Médico**, "Crea una cuenta de recepcionista.", los campos Nombre, Apellido, Tipo de documento (Cédula por defecto), Número de documento, Email, Teléfono (opcional), Contraseña y Confirmar contraseña, y el botón **Crear cuenta**.
- [x] Enviar el formulario vacío muestra los mensajes de los campos obligatorios y no hace ninguna petición.
- [x] Una contraseña sin letra, sin dígito o de menos de 8 caracteres, una confirmación distinta, un email inválido o un teléfono con formato inválido muestran su mensaje y no hacen ninguna petición.
- [x] Con datos válidos se hace `POST /api/auth/register` con `{ email, password, firstName, lastName, documentType, documentNumber, phone }`, los textos recortados, sin `confirmPassword` y con `phone: null` si se dejó vacío.
- [x] Mientras se envía, el botón está deshabilitado y muestra "Creando cuenta…".
- [x] Un 409 muestra "Ya existe un usuario con ese email o esa identificación." y conserva los valores; un 400, "Revisa los datos e inténtalo de nuevo."; sin conexión, "No se pudo conectar con el servidor."; otro error, "Ocurrió un error inesperado. Inténtalo de nuevo.".
- [x] Tras un 201 se llega a `/login` con el aviso "Cuenta creada. Inicia sesión para continuar." y el email ya escrito, y al recargar el aviso desaparece.
- [x] Con la cuenta recién creada se puede iniciar sesión y se ve el inicio con el rol **Recepcionista**.
- [x] El login enlaza a `/register` ("Regístrate") y el registro enlaza a `/login` ("Inicia sesión").
- [x] Con sesión, `/register` redirige al inicio.
- [x] Los botones del ojo alternan cada contraseña entre oculta y visible.
- [x] `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja la estructura nueva y tiene la fila 04 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** tras el registro, ir a `/login` con un aviso y el email precargado. Elegido por el usuario: la API no devuelve token y el usuario confirma su contraseña al entrar.
- **No:** login automático tras el registro. Evita encadenar dos llamadas con estados intermedios (cuenta creada pero login fallido).
- **No:** pantalla de éxito en el propio registro. Un paso más sin aportar nada.
- **Yes:** el aviso y el email viajan en el `state` del router. Elegido por el usuario: no dejan el email en la URL ni en el historial; a cambio, el aviso se pierde al recargar.
- **No:** query string (`/login?registered=1&email=…`).
- **Yes:** `select` de shadcn para el tipo de documento, con Cédula por defecto. Elegido por el usuario: coherente con la UI y reutilizable en usuarios y pacientes; no añade dependencias porque `radix-ui` ya está instalado.
- **No:** `radio-group` ni `<select>` nativo.
- **Yes:** campo de confirmación de contraseña (solo en el cliente) y botón del ojo en ambos campos. Elegido por el usuario.
- **Yes:** el 409 con un mensaje general. Elegido por el usuario: el `detail` de la API viene en inglés y distinguir email de identificación obligaría a depender de su texto.
- **Yes:** los 400 del backend con un mensaje genérico. Elegido por el usuario: Zod replica las reglas del backend (incluido el formato del teléfono), así que un 400 solo llegaría por un desajuste.
- **Yes:** enlaces entre login y registro en ambos sentidos. Elegido por el usuario.
- **Yes:** la página aclara "Crea una cuenta de recepcionista.", sin campo de rol. Elegido por el usuario; la API ignora el rol en el autoregistro.
- **Yes:** `/register` con sesión redirige a `/`, como `/login`.
- **Yes:** el teléfono vacío se envía como `null`: la API rechaza un teléfono vacío (`PhoneNumber.Create`) pero acepta `null`.
- **Yes:** la función de API se llama `registerAccount` para no chocar con `register` de React Hook Form.
- **Yes:** `documentTypeLabels` en `features/auth/` hasta que otra feature lo necesite.
- **Yes:** todo en `features/auth/`, siguiendo la organización del spec 03 (página en la raíz, formulario en `components/`).

## Risks

| Risk                                                                                                         | Mitigation                                                                                                                                                       |
| ------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| El `select` de Radix no se puede manejar en jsdom (faltan `hasPointerCapture`, `scrollIntoView`)              | Simulaciones en `src/test/setup.ts`, definidas solo si no existen. Se confirma en el paso 3 eligiendo Pasaporte en el test.                                     |
| El CLI de shadcn cambia `package.json` al añadir `select` o genera un import de `cn` incorrecto              | Se revisa el diff tras el paso 1: se revierte cualquier dependencia nueva (no hace falta) y se corrige el import a `@/lib/utils`.                               |
| Las reglas de Zod se desalinean del backend (longitudes, formato del teléfono o de la contraseña)            | El esquema copia las constantes del backend; un desajuste acaba en el mensaje genérico del 400, sin romper el flujo.                                            |
| El email de Zod es más estricto o más laxo que el `EmailAddress` de FluentValidation y la regex de `Email`   | Mismo criterio que el login (spec 03); un caso límite acaba en el mensaje del 400.                                                                               |
| El autoregistro anónimo permite crear recepcionistas sin control                                             | Es el comportamiento documentado del backend; este spec solo lo expone. Restringirlo sería un spec del backend.                                                 |

## What is **not** in this spec

- Login automático tras el registro.
- Elegir rol o el alta de usuarios por el Admin.
- Errores del backend marcados en cada campo.
- Fortaleza de la contraseña, captcha o verificación del email.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

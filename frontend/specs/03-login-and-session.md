# SPEC 03 — Página de login y sesión

> **Status:** Approved
> **Depends on:** SPEC 01, SPEC 02
> **Date:** 2026-10-28
> **Objective:** Crear la página de login que autentica con `POST /api/auth/login`, guarda la sesión (token en `localStorage` y usuario de `GET /api/auth/me`) y protege las rutas de la app con React Router.

## Por qué este spec existe

Todas las pantallas de la app necesitan un usuario autenticado: la API rechaza con 401 cualquier petición sin token.
El spec 02 dejó preparado el hueco del token (`setAccessTokenProvider`), pero nadie lo rellena y la app no tiene rutas.
Este spec añade el login, la sesión que sobrevive a recargas, el cierre de sesión (manual y ante un 401) y el router con una ruta protegida de inicio.
También cierra cuatro preguntas abiertas: **router** (React Router), **formularios** (React Hook Form + Zod), **guardado del token** (`localStorage`) e **idioma de la interfaz** (español).

## Scope

**In:**

- Dependencias nuevas: `react-router` (v7, modo declarativo), `react-hook-form`, `zod` y `@hookform/resolvers`.
- Componentes de shadcn/ui: `card`, `input`, `label`, `alert` y `field` (el que shadcn recomienda hoy para React Hook Form; ver **Risks**).
- Módulo `features/auth/`: tipos, funciones de API, guardado del token, `AuthProvider`, `useAuth`, página de login, formulario, guard de rutas y pantallas de arranque.
- Manejador global del 401 en el cliente HTTP (`setUnauthorizedHandler`), que la sesión usa para cerrar sesión.
- Rutas en `src/app-routes.tsx`: `/login`, `/` (protegida) y comodín.
- Página de inicio mínima (`features/home/home-page.tsx`): nombre y rol del usuario, selector de tema y botón **Cerrar sesión**.
- Tests de Vitest de todo lo anterior, con `fetch` simulado.
- Actualizar `frontend/CLAUDE.md`: estado, stack, estructura, contrato (token y 401), backlog y preguntas abiertas.

**Out of scope (for future specs):**

- Layout por rol (menú lateral, navegación entre módulos) y ocultar opciones según el rol: spec siguiente.
- Registro (`POST /api/auth/register`): sin enlace ni página.
- Cierre de sesión automático al llegar `expiresAtUtc` con un temporizador: la caducidad durante el uso se detecta por el 401.
- Renovación del token (la API no tiene refresh token), "recordarme" y recuperación de contraseña.
- Cambio de contraseña y perfil (`/api/users/me*`).
- Sincronizar la sesión entre pestañas (evento `storage`).
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/src/
├─ App.tsx                         # BrowserRouter + AuthProvider + AppRoutes
├─ App.test.tsx                    # se reescribe
├─ app-routes.tsx                  # nuevo: rutas de la app
├─ app-routes.test.tsx             # nuevo
├─ components/ui/                  # + card, input, label, alert, field (shadcn)
├─ features/
│  ├─ auth/
│  │  ├─ auth-types.ts             # DTOs y tipos de la sesión
│  │  ├─ auth-api.ts               # login y getCurrentUser
│  │  ├─ role-labels.ts            # nombres de los roles en español
│  │  ├─ token-storage.ts          # lectura y escritura del token en localStorage
│  │  ├─ token-storage.test.ts
│  │  ├─ auth-provider.tsx         # AuthProvider y AuthContext
│  │  ├─ auth-provider.test.tsx
│  │  ├─ use-auth.ts               # hook useAuth
│  │  ├─ login-page.tsx
│  │  └─ components/
│  │     ├─ login-form.tsx
│  │     ├─ login-form.test.tsx
│  │     ├─ require-auth.tsx       # guard de rutas protegidas
│  │     └─ session-screens.tsx    # SessionLoadingScreen y SessionErrorScreen
│  └─ home/
│     └─ home-page.tsx             # inicio provisional (lo sustituirá el layout por rol)
└─ lib/api/
   ├─ unauthorized-handler.ts      # nuevo: manejador global del 401
   ├─ api-client.ts                # llama al manejador ante un 401 con token
   └─ api-client.test.ts           # + casos del 401
```

`useAuth` es lo que importarán las demás features (`@/features/auth/use-auth`); se documenta en `frontend/CLAUDE.md`.

### Tipos (`features/auth/auth-types.ts`)

```ts
export type UserRole = 'Admin' | 'Doctor' | 'Receptionist'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: 'Bearer'
  expiresAtUtc: string // ISO 8601 UTC
}

// GET /api/auth/me
export interface CurrentUserDto {
  id: string
  email: string
  firstName: string
  lastName: string
  identificationType: string
  identificationNumber: string
  phone: string | null
  role: UserRole
  doctorId: string | null
  isActive: boolean
}

export type AuthStatus = 'loading' | 'error' | 'authenticated' | 'anonymous'
```

### API (`features/auth/auth-api.ts`)

```ts
export const login = (request: LoginRequest) =>
  api.post<LoginResponse>('/api/auth/login', request)

export const getCurrentUser = () => api.get<CurrentUserDto>('/api/auth/me')
```

### Nombres de los roles (`features/auth/role-labels.ts`)

```ts
export const roleLabels: Record<UserRole, string> = {
  Admin: 'Administrador',
  Doctor: 'Doctor',
  Receptionist: 'Recepcionista',
}
```

### Guardado del token (`features/auth/token-storage.ts`)

```ts
export const TOKEN_STORAGE_KEY = 'dispensario-auth'

export interface StoredToken {
  accessToken: string
  expiresAtUtc: string
}

export function readStoredToken(now?: Date): StoredToken | null
export function saveStoredToken(token: StoredToken): void
export function clearStoredToken(): void
```

- Se guarda como JSON `{ accessToken, expiresAtUtc }` en la clave `dispensario-auth`.
- `readStoredToken` devuelve `null` y **borra la clave** si: el JSON no es válido, falta alguno de los dos campos o no es texto, `expiresAtUtc` no es una fecha válida, o la fecha es `<= now` (por defecto `new Date()`).
- Cualquier acceso a `localStorage` va en `try/catch`: si falla (modo privado, almacenamiento bloqueado), `readStoredToken` devuelve `null` y `save`/`clear` no hacen nada. La sesión funciona igual en memoria.

### Manejador global del 401 (`lib/api/unauthorized-handler.ts`)

```ts
export type UnauthorizedHandler = () => void

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void
export function notifyUnauthorized(): void // no hace nada si no hay manejador
```

Cambio en `apiRequest` (`api-client.ts`): si la respuesta es **401** y la petición **llevaba `Authorization`**, llama a `notifyUnauthorized()` y después lanza el `ApiError` como hasta ahora. Un 401 sin token (el del login con credenciales inválidas) no llama al manejador. El 403 no tiene trato especial.

### Sesión (`features/auth/auth-provider.tsx` y `use-auth.ts`)

```ts
export interface AuthContextValue {
  status: AuthStatus
  user: CurrentUserDto | null // solo con status 'authenticated'
  login: (request: LoginRequest) => Promise<CurrentUserDto> // rechaza con ApiError
  logout: () => void
  retry: () => void // vuelve a pedir /me tras un error de arranque
}

export function AuthProvider({ children }: { children: React.ReactNode }): JSX.Element
export function useAuth(): AuthContextValue // lanza Error si se usa fuera de AuthProvider
```

- El token vigente vive en una ref del provider. Al montarse, el provider registra `setAccessTokenProvider(() => tokenRef.current)` y `setUnauthorizedHandler(logout)`; al desmontarse, los quita con `null`.
- **Arranque:**
  - Sin token guardado válido (`readStoredToken()` es `null`) → `status: 'anonymous'`, sin llamar a la API.
  - Con token → `status: 'loading'` y `GET /api/auth/me`:
    - 200 → `status: 'authenticated'` con `user`.
    - 401 → lo resuelve el manejador global (`logout`) → `anonymous`.
    - 404 → `logout()` → `anonymous`.
    - Error de red u otro código → `status: 'error'`, **conserva el token**. `retry()` vuelve a `loading` y repite `/me`.
- **`login(request)`:**
  1. `POST /api/auth/login`. Si falla, rechaza con ese `ApiError` y no cambia nada.
  2. Guarda el token en la ref y en `localStorage` (`saveStoredToken`).
  3. `GET /api/auth/me`. Si falla, borra el token (ref y `localStorage`) y rechaza con ese `ApiError`; el estado sigue `anonymous`.
  4. Si va bien, `status: 'authenticated'` con `user`, y resuelve el usuario.
  - Durante el login el `status` sigue `anonymous`: la carga la muestra el formulario, no la pantalla completa.
- **`logout()`:** borra el token (ref y `localStorage`), `user: null` y `status: 'anonymous'`. No llama a la API (no hay endpoint de logout). La navegación a `/login` la hace `RequireAuth` al ver `anonymous`.
- Las respuestas de `/me` que lleguen después de un `logout` se ignoran (contador interno, como en `useApi`).

### Rutas (`src/app-routes.tsx`)

React Router v7 en modo declarativo (`BrowserRouter`, `Routes`, `Route`, `Navigate`, importados de `react-router`).

| Ruta     | Elemento                                         | Comportamiento                                                                                 |
| -------- | ------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| `/login` | `LoginPage`                                      | Con `status: 'authenticated'`, `<Navigate replace>` al destino recordado (`from`) o a `/`.      |
| `/`      | `<RequireAuth><HomePage /></RequireAuth>`        | Ruta protegida.                                                                                |
| `*`      | `<Navigate to="/" replace />`                    | Cualquier ruta desconocida va al inicio (y de ahí, sin sesión, al login).                       |

- Con `status: 'loading'`, `AppRoutes` muestra `SessionLoadingScreen` (indicador centrado a pantalla completa) y **no renderiza ninguna ruta**.
- Con `status: 'error'`, muestra `SessionErrorScreen`: "No se pudo conectar con el servidor." y un botón **Reintentar** que llama a `retry()`, más un botón **Cerrar sesión** que llama a `logout()`.
- `RequireAuth`: con `authenticated` renderiza sus hijos; con `anonymous`, `<Navigate to="/login" replace state={{ from: location }} />`.
- El destino tras el login es `from.pathname + from.search + from.hash`; sin `from`, `/`. Solo se usa el `location` que guarda el propio router, nunca un parámetro de la URL (evita redirecciones abiertas).
- `App.tsx` queda: `<BrowserRouter><AuthProvider><AppRoutes /></AuthProvider></BrowserRouter>`. `ThemeProvider` sigue en `main.tsx`.

### Página de login (`login-page.tsx` y `components/login-form.tsx`)

- `LoginPage`: pantalla completa, sin selector de tema, con un `Card` centrado con el título **Dispensario Médico** (`h1`), la descripción "Inicia sesión para continuar." y el `LoginForm`.
- `LoginForm` usa React Hook Form con `zodResolver` y llama a `useAuth().login` a través de `useApi` (`const { error, isLoading, execute } = useApi(login)`). No navega: al pasar la sesión a `authenticated`, la ruta `/login` redirige sola.

Esquema de Zod (mensajes en español):

```ts
const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Ingresa tu email.')
    .max(254, 'El email no puede superar 254 caracteres.')
    .email('Ingresa un email válido.'),
  password: z.string().min(1, 'Ingresa tu contraseña.'), // sin trim
})
```

(La sintaxis exacta de Zod 4 para el email se confirma con Context7 en el paso 3; los mensajes y las reglas no cambian. Son las mismas reglas que `LoginCommandValidator` del backend: `NotEmpty`, `MaximumLength(254)` y `EmailAddress`.)

| Elemento                 | Detalle                                                                                                                                                                                                 |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Campo **Email**          | `type="email"`, `autoComplete="username"`. Error de validación debajo del campo.                                                                                                                         |
| Campo **Contraseña**     | `type="password"`, `autoComplete="current-password"`. Botón de icono (`Eye`/`EyeOff` de `lucide-react`) que alterna `password`/`text`, con `aria-label` "Mostrar contraseña" / "Ocultar contraseña".     |
| Botón **Iniciar sesión** | `type="submit"`. Con `isLoading`: deshabilitado, con un `Loader2` girando y el texto "Iniciando sesión…".                                                                                                |
| Validación               | Al enviar; después de un intento, se revalida al escribir (comportamiento por defecto de React Hook Form). Con errores de validación no se llama a la API.                                               |
| Error de la API          | `Alert` `destructive` con `role="alert"` encima de los campos, con el mensaje de la tabla siguiente. Se oculta al volver a enviar.                                                                       |
| Tras un 401              | Se vacía la contraseña y recibe el foco; el email se conserva.                                                                                                                                          |

| Error                          | Mensaje                                                  |
| ------------------------------ | -------------------------------------------------------- |
| `status === 401`               | Email o contraseña incorrectos.                          |
| `kind === 'network'`           | No se pudo conectar con el servidor.                     |
| Cualquier otro (400, 500, …)   | Ocurrió un error inesperado. Inténtalo de nuevo.          |

### Página de inicio (`features/home/home-page.tsx`)

Provisional hasta el spec del layout por rol: cabecera con **Dispensario Médico** (`h1`), `ModeToggle` y botón **Cerrar sesión** (llama a `logout()`); en el cuerpo, "Hola, {firstName} {lastName}" y el rol con `roleLabels`.

### Tests

`fetch` se simula con `vi.stubGlobal('fetch', vi.fn())` y se restaura con `vi.unstubAllGlobals()`. Cada test limpia `localStorage` y los registros globales (`setAccessTokenProvider(null)`, `setUnauthorizedHandler(null)`) en `afterEach`. Las pruebas de rutas usan `MemoryRouter` con `initialEntries`; las de componentes, `@testing-library/user-event`.

| Archivo                  | Casos                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `token-storage.test.ts`  | Guarda y lee un token vigente; sin clave → `null`; JSON inválido, campos que faltan, fecha inválida y token caducado → `null` y la clave se borra; `clearStoredToken` borra la clave; `localStorage` que lanza → `null` sin excepción.                                                                                                                                                                                                                                                                                          |
| `api-client.test.ts`     | (se añade) 401 con token → llama al manejador y lanza `ApiError` 401; 401 sin token → no lo llama; 403 con token → no lo llama; sin manejador registrado, un 401 solo lanza.                                                                                                                                                                                                                                                                                                                                                  |
| `auth-provider.test.tsx` | Sin token → `anonymous` sin llamar a `fetch`; token caducado → `anonymous`, sin `fetch` y clave borrada; token vigente → `loading` y luego `authenticated` con el usuario de `/me` y `Authorization: Bearer`; `/me` 401 → `anonymous` y clave borrada; `/me` 404 → igual; `/me` con error de red → `error` y token conservado, y `retry` con éxito → `authenticated`; `login` correcto → token guardado y `authenticated`; `login` 401 → rechaza, nada guardado; `login` correcto con `/me` fallido → rechaza y token borrado; `logout` → `anonymous` y clave borrada; un 401 de otra petición con sesión → `anonymous`; `useAuth` fuera del provider lanza. |
| `login-form.test.tsx`    | Enviar vacío → "Ingresa tu email." y "Ingresa tu contraseña." sin llamar a `fetch`; email inválido → "Ingresa un email válido."; datos válidos → `POST /api/auth/login` con `{ email, password }` (email sin espacios); botón deshabilitado con "Iniciando sesión…" mientras carga; 401 → "Email o contraseña incorrectos.", contraseña vacía y con foco; error de red → "No se pudo conectar con el servidor."; 500 → "Ocurrió un error inesperado. Inténtalo de nuevo."; el botón del ojo alterna el `type` del campo. |
| `app-routes.test.tsx`    | Sin sesión, `/` → login; ruta desconocida sin sesión → login; con token guardado, mientras `/me` carga no se ve ni el login ni el inicio; `/me` con error de red → pantalla de error, y **Reintentar** → inicio; login correcto desde `/login` → inicio con nombre y rol; entrar a `/?x=1` sin sesión → login, y tras el login vuelve a `/?x=1`; `/login` con sesión → inicio; **Cerrar sesión** → login y clave borrada. |
| `App.test.tsx`           | (se reescribe) Sin token, la app muestra el login (`h1` Dispensario Médico y botón **Iniciar sesión**).                                                                                                                                                                                                                                                                                                                                                                                                                         |

## Implementation plan

1. Consultar con Context7 la documentación actual de React Router v7 (modo declarativo), React Hook Form, Zod 4, `@hookform/resolvers` y el componente `field` de shadcn/ui. Instalar `react-router`, `react-hook-form`, `zod` y `@hookform/resolvers`, y añadir con el CLI de shadcn `card`, `input`, `label`, `alert` y `field` (corrigiendo el import de `cn` a `@/lib/utils` en cada archivo). Verificar: `pnpm build` y `pnpm lint` pasan.
2. Crear `lib/api/unauthorized-handler.ts`, llamarlo desde `apiRequest` ante un 401 con token y ampliar `api-client.test.ts`. Crear `features/auth/auth-types.ts`, `auth-api.ts`, `role-labels.ts` y `token-storage.ts` con su test. Verificar: `pnpm test` pasa.
3. Crear `auth-provider.tsx`, `use-auth.ts` y su test. Verificar: `pnpm test` y `pnpm lint` pasan (reglas de `react-hooks`).
4. Crear `components/login-form.tsx` con su test, `login-page.tsx`, `components/require-auth.tsx`, `components/session-screens.tsx` y `features/home/home-page.tsx`. Verificar: `pnpm test` pasa.
5. Crear `src/app-routes.tsx` con su test, actualizar `App.tsx` y reescribir `App.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` pasan.
6. Probar a mano con el backend corriendo (`dotnet run`) y `pnpm dev`: login con el Admin sembrado, credenciales incorrectas, recarga con sesión (sigue dentro), backend parado al recargar (pantalla de error y Reintentar), cerrar sesión, y entrar a `/` sin sesión.
7. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** login, sesión con token en `localStorage`, React Router con `/login` e inicio provisional.
   - **Stack:** React Router 7 (declarativo), React Hook Form + Zod; quitar router y formularios de "pendiente de decidir".
   - **Estructura:** `app-routes.tsx`, `features/auth/` (con `use-auth` como punto de entrada para las demás features) y `features/home/`.
   - **Contrato con la API:** el `AuthProvider` registra el proveedor de token; un 401 de una petición con token cierra la sesión (`setUnauthorizedHandler`); el 401 del login no.
   - **Backlog:** fila 03 con este spec.
   - **Preguntas abiertas:** cerrar router, formularios, guardado del token e idioma de la interfaz (solo español).

## Acceptance criteria

- [ ] `package.json` añade `react-router`, `react-hook-form`, `zod` y `@hookform/resolvers`, y ninguna otra dependencia.
- [ ] Sin token guardado, abrir `/` lleva a `/login`, y la página muestra **Dispensario Médico**, los campos Email y Contraseña y el botón **Iniciar sesión**.
- [ ] Enviar el formulario vacío muestra "Ingresa tu email." y "Ingresa tu contraseña." y no hace ninguna petición.
- [ ] Con credenciales válidas se hace `POST /api/auth/login`, después `GET /api/auth/me` con `Authorization: Bearer <token>`, se guarda `{ accessToken, expiresAtUtc }` en `localStorage` (`dispensario-auth`) y se muestra el inicio con el nombre y el rol en español.
- [ ] Con credenciales inválidas (401) se muestra "Email o contraseña incorrectos.", la contraseña queda vacía y no se guarda nada.
- [ ] Sin conexión con el backend, el login muestra "No se pudo conectar con el servidor.".
- [ ] Mientras se envía, el botón está deshabilitado y muestra "Iniciando sesión…".
- [ ] El botón del ojo alterna la contraseña entre oculta y visible.
- [ ] Recargar con una sesión vigente mantiene al usuario dentro, sin pasar por el login.
- [ ] Un token caducado en `localStorage` se descarta al arrancar sin llamar a la API.
- [ ] Si `/me` falla por red al arrancar, se muestra la pantalla de error con **Reintentar**, y el token sigue guardado.
- [ ] Un 401 de cualquier petición con token cierra la sesión y lleva a `/login`.
- [ ] Tras ser redirigido al login desde una ruta protegida, el login vuelve a esa ruta.
- [ ] `/login` con sesión redirige al inicio, y una ruta desconocida va a `/`.
- [ ] **Cerrar sesión** borra `dispensario-auth` y lleva a `/login`.
- [ ] `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` terminan sin errores.
- [ ] `frontend/CLAUDE.md` refleja la estructura nueva, tiene la fila 03 en el backlog y las preguntas de router, formularios, guardado del token e idioma están cerradas.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** login, sesión y React Router en un solo spec. Elegido por el usuario: el login necesita a dónde ir y qué proteger.
- **Yes:** React Router 7 en modo declarativo (`BrowserRouter` + `Routes`). Basta para rutas protegidas y redirecciones; el modo data (loaders, actions) no aporta nada sin TanStack Query ni carga por ruta.
- **No:** TanStack Router. Más potente con tipos, pero añade generación de rutas y otro modelo mental para una app de pocas pantallas.
- **Yes:** token en `localStorage` junto con `expiresAtUtc`. Elegido por el usuario: la sesión sobrevive a recargas y pestañas nuevas. El riesgo de XSS se asume en una app interna.
- **No:** `sessionStorage` ni solo memoria. Obligarían a repetir el login al abrir otra pestaña o al recargar.
- **Yes:** usuario desde `GET /api/auth/me`, tras el login y al arrancar. Elegido por el usuario: datos frescos y detecta usuarios desactivados.
- **No:** decodificar el JWT. Acopla el frontend a los claims y no detecta usuarios desactivados.
- **Yes:** React Hook Form + Zod, con los componentes de formulario de shadcn. Elegido por el usuario; cierra la pregunta de formularios para el resto de la app.
- **Yes:** cierre de sesión global ante un 401 de una petición **con token**, con un manejador registrable como el proveedor de token. Elegido por el usuario. El 401 del login (sin token) no cuenta, para que las credenciales inválidas no "cierren" una sesión que no existe.
- **No:** temporizador con `expiresAtUtc`. Elegido por el usuario: solo se descarta el token caducado al arrancar; durante el uso, la caducidad llega como 401.
- **Yes:** volver a la ruta pedida tras el login, con el `location` del router y no con un parámetro de URL, para evitar redirecciones abiertas.
- **Yes:** pantalla de carga a pantalla completa mientras `/me` responde al arrancar, y pantalla de error con **Reintentar** si falla por red. Elegido por el usuario: una caída momentánea del backend no cierra la sesión.
- **Yes:** 404 de `/me` al arrancar cierra la sesión: el usuario del token ya no existe.
- **Yes:** `login` del provider rechaza con `ApiError` y el formulario lo usa con `useApi`. Así la carga y el error del formulario siguen el patrón del spec 02.
- **Yes:** si `/me` falla justo después de un login correcto, se borra el token y se muestra el error. No se deja una sesión a medias.
- **Yes:** el formulario no navega; la ruta `/login` redirige al ver la sesión `authenticated`. Una sola fuente de la redirección.
- **Yes:** interfaz solo en español, con mensajes propios para los errores del login. Elegido por el usuario; los `title`/`detail` de la API vienen en inglés.
- **Yes:** los errores 400 del backend en el login se muestran con el mensaje genérico: Zod aplica las mismas reglas que `LoginCommandValidator`, así que solo llegarían por un desajuste.
- **Yes:** todo el módulo en `features/auth/` (elegido por el usuario), con `useAuth` como punto de entrada para las demás features.
- **Yes:** inicio provisional en `features/home/home-page.tsx`, que el spec del layout por rol sustituirá.
- **Yes:** botón para mostrar la contraseña y botón deshabilitado con spinner al enviar (elegidos por el usuario). **No:** selector de tema en el login ni autofocus en el email.
- **No:** enlace ni página de registro. Elegido por el usuario: el autoregistro, si se expone, va en su propio spec.
- **No:** sincronizar la sesión entre pestañas. Un logout en una pestaña no cierra las demás hasta que se recarguen (el token sigue siendo válido en la API): se acepta hasta que haga falta.

## Risks

| Risk                                                                                                                       | Mitigation                                                                                                                                                                                   |
| -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| El CLI de shadcn ya no ofrece `field` o sigue recomendando `form` para React Hook Form                                       | Se confirma con Context7 en el paso 1. Si `field` no existe, se usa `form`; el comportamiento y los tests del formulario no cambian.                                                           |
| Zod 4 cambió la API del email (`z.email()` frente a `z.string().email()`) o el orden de los mensajes                         | Se confirma en el paso 1. Los tests fijan los mensajes, no la sintaxis.                                                                                                                       |
| Bucle de redirecciones entre `/`, `/login` y el comodín                                                                    | Las redirecciones dependen solo de `status`, y con `loading`/`error` no se renderiza ninguna ruta. `app-routes.test.tsx` cubre cada combinación.                                              |
| `StrictMode` monta el provider dos veces y dispara dos llamadas a `/me` en desarrollo                                       | El contador interno ignora la respuesta de la llamada anterior; solo en desarrollo, sin efecto en producción.                                                                                |
| El manejador del 401 llama a `logout` mientras una pantalla aún espera otras respuestas                                     | `logout` es idempotente y `useApi` ya ignora respuestas tras desmontar.                                                                                                                      |
| `localStorage` lanza en modo privado o con almacenamiento bloqueado                                                        | Todo acceso va en `try/catch`; la sesión funciona en memoria y se pierde al recargar.                                                                                                        |
| Un usuario desactivado conserva un token válido y podría seguir usando la app                                              | Al recargar, `/me` responde 401 y se cierra la sesión; mientras tanto, la API decide qué permite (comportamiento documentado del backend).                                                    |
| Los estados globales (proveedor de token, manejador del 401, `localStorage`) se filtran entre tests                         | Cada test los limpia en `afterEach`.                                                                                                                                                          |

## What is **not** in this spec

- Layout por rol, menú de navegación ni páginas de los módulos.
- Registro, recuperación o cambio de contraseña y perfil.
- Temporizador de caducidad, renovación del token o sincronización entre pestañas.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

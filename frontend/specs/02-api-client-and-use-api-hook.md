# SPEC 02 — Cliente HTTP y hook `useApi`

> **Status:** Approved
> **Depends on:** SPEC 01
> **Date:** 2026-10-28
> **Objective:** Crear un cliente HTTP propio sobre `fetch` (`api`, `ApiError` y un proveedor de token) y el hook `useApi`, que expone `data`, `error`, `status`, `isLoading`, `execute`, `reset` y `setData`, para que todas las llamadas a la API se hagan de la misma manera.

## Por qué este spec existe

Cada módulo (auth, users, doctors, patients, appointments, consultations) va a llamar a la API.
Sin una base común, cada pantalla resolvería por su cuenta la URL base, el JSON, el token, los ProblemDetails y los estados de carga y error.
Este spec fija esa base: los módulos solo declaran funciones como `getPatient = (id) => api.get<PatientDto>(...)` y las usan con `useApi`.
También cierra la pregunta abierta de **estado de servidor**: `fetch` propio, sin TanStack Query.

## Scope

**In:**

- `ApiError` (`src/lib/api/api-error.ts`): un único tipo de error para HTTP (ProblemDetails), fallos de red y errores inesperados.
- Proveedor de token (`src/lib/api/access-token.ts`): un hueco configurable que el spec de login rellenará; mientras nadie lo registre, las peticiones salen sin `Authorization`.
- Cliente HTTP (`src/lib/api/api-client.ts`): `apiRequest<T>` y el objeto `api` con `get`, `post`, `put` y `delete`, que usan `config.apiBaseUrl`, JSON y el token.
- Hook `useApi` (`src/hooks/use-api.ts`) con `data`, `error`, `status`, `isLoading`, `execute`, `reset` y `setData`. `execute` no lanza y solo cuenta la última llamada.
- Tests de Vitest de los tres archivos de `lib/api/` y del hook, con `fetch` simulado.
- Actualizar `frontend/CLAUDE.md`: estado, estructura (`hooks/` y `lib/api/`), cómo llamar a la API, backlog y preguntas abiertas.

**Out of scope (for future specs):**

- Login, sesión, guardado del token y quién registra el proveedor de token (spec de login).
- Reaccionar al 401 (cerrar sesión, redirigir al login): spec de login.
- Cancelar peticiones con `AbortController`, ejecución automática al montar (`immediate`), caché, deduplicación y reintentos.
- Funciones de API y DTOs de los módulos (cada módulo en su spec).
- Mostrar errores en la interfaz (toasts, mensajes de formulario a partir de `validationErrors`).
- Usar el hook en una pantalla o llamar al backend real: este spec se verifica solo con tests.
- TanStack Query.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/src/
├─ hooks/                       # nuevo: hooks compartidos entre features
│  ├─ use-api.ts
│  └─ use-api.test.ts
└─ lib/
   └─ api/                      # nuevo: cliente HTTP
      ├─ api-error.ts
      ├─ api-error.test.ts
      ├─ access-token.ts
      ├─ api-client.ts
      └─ api-client.test.ts     # incluye los casos del proveedor de token
```

No hay archivo `index.ts` (barrel): se importa cada archivo por su ruta (`@/lib/api/api-client`, `@/hooks/use-api`).

### `ApiError` (`src/lib/api/api-error.ts`)

```ts
export type ApiErrorKind = 'http' | 'network' | 'unknown'

export interface ValidationError {
  propertyName: string
  errorMessage: string
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status: number // código HTTP; 0 si no hubo respuesta (network) o es un error inesperado (unknown)
  readonly title: string
  readonly detail: string | null
  readonly validationErrors: ValidationError[] // [] si la respuesta no trae ValidationErrors
  readonly traceId: string | null
  // message = detail ?? title; name = 'ApiError'
}

// Convierte una respuesta no exitosa en ApiError (kind 'http')
export async function createHttpError(response: Response): Promise<ApiError>

// Devuelve el mismo ApiError o envuelve cualquier otro valor lanzado (kind 'unknown')
export function toApiError(error: unknown): ApiError
```

Reglas de `createHttpError`:

- Si el `Content-Type` contiene `json` (`application/json` o `application/problem+json`), lee el cuerpo como ProblemDetails: `title`, `detail`, `traceId` y la extensión `ValidationErrors`.
- De cada elemento de `ValidationErrors` solo se conservan `propertyName` y `errorMessage`.
- `title` sale del cuerpo y, si falta, de `response.statusText`; si también está vacío, `HTTP <status>`.
- Si el cuerpo no es JSON o no se puede leer, el error se crea igual con `status` y ese `title`, `detail: null` y `validationErrors: []`.

Reglas de `toApiError`:

- Un `ApiError` se devuelve tal cual.
- Un `Error` se convierte en `kind: 'unknown'`, `status: 0` y `title` igual a su `message`.
- Cualquier otro valor da `kind: 'unknown'`, `status: 0` y `title: 'Unexpected error.'`.

### Proveedor de token (`src/lib/api/access-token.ts`)

```ts
export type AccessTokenProvider = () => string | null

export function setAccessTokenProvider(provider: AccessTokenProvider | null): void
export function getAccessToken(): string | null // null si no hay proveedor o devuelve vacío
```

- Es estado de módulo: hay un único proveedor para toda la app.
- `setAccessTokenProvider(null)` lo quita, y lo usarán el logout y los tests.

### Cliente HTTP (`src/lib/api/api-client.ts`)

```ts
export type QueryValue = string | number | boolean | null | undefined

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' // 'GET' por defecto
  body?: unknown // se serializa con JSON.stringify
  query?: Record<string, QueryValue>
}

export function apiRequest<T>(path: string, options?: ApiRequestOptions): Promise<T>

export const api = {
  get: <T>(path: string, query?: Record<string, QueryValue>) => Promise<T>,
  post: <T = void>(path: string, body?: unknown) => Promise<T>,
  put: <T = void>(path: string, body?: unknown) => Promise<T>,
  delete: <T = void>(path: string) => Promise<T>,
}
```

Reglas de `apiRequest`:

- La URL es `config.apiBaseUrl + path`. `path` empieza por `/` (`/api/patients`); no se admiten URLs absolutas.
- `query` se añade con `URLSearchParams`, en el orden de sus claves. Las claves con `null` o `undefined` se omiten, y un objeto sin claves válidas no añade `?`.
- Siempre envía `Accept: application/json`.
- Con `body !== undefined`, envía el cuerpo con `JSON.stringify` y `Content-Type: application/json`. Sin `body`, no envía ni cuerpo ni `Content-Type`.
- Si `getAccessToken()` devuelve un token, añade `Authorization: Bearer <token>`.
- Si `fetch` rechaza (sin conexión, CORS, DNS), lanza `ApiError` con `kind: 'network'`, `status: 0` y `title: 'Network error.'`.
- Si `response.ok` es `false`, lanza `await createHttpError(response)`. El 401 no recibe ningún trato especial.
- Si la respuesta es exitosa:
  - con un `204` o un cuerpo vacío, resuelve `undefined`;
  - con un `Content-Type` que contiene `json`, resuelve el JSON, y si no se puede parsear lanza `ApiError` con `kind: 'unknown'` y el `status` de la respuesta;
  - con cualquier otro contenido, resuelve el texto (por ejemplo, `GET /health` → `"Healthy"`).
- No valida en tiempo de ejecución que el JSON coincida con `T`: el tipo es una promesa del contrato del `README.md`.

### Hook `useApi` (`src/hooks/use-api.ts`)

```ts
export type ApiStatus = 'idle' | 'loading' | 'success' | 'error'

export type ApiResult<TData> =
  | { data: TData; error: null }
  | { data: null; error: ApiError }

export interface UseApiResult<TArgs extends unknown[], TData> {
  data: TData | null
  error: ApiError | null
  status: ApiStatus
  isLoading: boolean // status === 'loading'
  execute: (...args: TArgs) => Promise<ApiResult<TData>>
  reset: () => void
  setData: (value: TData | null | ((previous: TData | null) => TData | null)) => void
}

export function useApi<TArgs extends unknown[], TData>(
  fn: (...args: TArgs) => Promise<TData>,
): UseApiResult<TArgs, TData>
```

Uso previsto en un módulo:

```ts
// features/patients/patients-api.ts (spec del módulo)
export const getPatient = (id: string) => api.get<PatientDto>(`/api/patients/${id}`)

// En un componente
const { data, error, isLoading, execute } = useApi(getPatient)
// execute(id) en un useEffect o en un manejador de evento
```

Reglas del hook:

- Estado inicial: `status: 'idle'`, `data: null` y `error: null`.
- `execute(...args)` llama a `fn(...args)` y pasa a `status: 'loading'` con `error: null`. Mientras carga, `data` conserva el valor anterior.
- Si `fn` resuelve, el estado pasa a `status: 'success'` con `data` igual al valor resuelto y `error: null`, y `execute` resuelve `{ data, error: null }`.
- Si `fn` rechaza, el estado pasa a `status: 'error'` con `error` igual a `toApiError(motivo)`, y `data` conserva el valor anterior. `execute` resuelve `{ data: null, error }`: **nunca rechaza**.
- Solo cuenta la última llamada. Un contador interno hace que las respuestas de llamadas anteriores, de antes de un `reset` o de después de desmontar no cambien el estado. Cada `execute` resuelve igualmente su propio `ApiResult`.
- `reset()` vuelve al estado inicial y deja obsoletas las llamadas en curso.
- `setData` cambia `data`, con un valor o con una función del valor anterior, sin tocar `status` ni `error`. Sirve para actualizar la vista tras una edición sin volver a pedir los datos.
- `execute`, `reset` y `setData` conservan su identidad entre renders, así que se pueden poner en las dependencias de un `useEffect`.
- `execute` siempre usa la `fn` del último render (se guarda en una ref), así que se puede pasar una función inline sin que cambie la identidad de `execute`.

### Tests

`fetch` se simula con `vi.stubGlobal('fetch', vi.fn())` y se restaura con `vi.unstubAllGlobals()`. Los tests del hook usan `renderHook` y `act` de `@testing-library/react`. Cada test que registra un proveedor de token lo quita con `setAccessTokenProvider(null)`.

| Archivo              | Casos                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `api-error.test.ts`  | ProblemDetails con `ValidationErrors` → `kind: 'http'`, `status`, `title`, `detail`, `traceId` y `validationErrors` con solo `propertyName` y `errorMessage`; ProblemDetails sin `ValidationErrors` → `validationErrors: []`; cuerpo no JSON → `title` de `statusText`; `statusText` vacío → `HTTP 500`; `message` es `detail` o, sin él, `title`; `toApiError` devuelve el mismo `ApiError`, envuelve un `Error` y envuelve un valor que no es `Error` con `Unexpected error.`.                                                   |
| `api-client.test.ts` | URL con la base de `config` y el `path`; `query` codificada y sin `null`/`undefined`, y sin `?` si no queda ninguna; `Accept` siempre; `body` serializado con `Content-Type` y, sin `body`, sin `Content-Type`; `Authorization` con proveedor y sin cabecera sin proveedor o con token vacío; `get`/`post`/`put`/`delete` usan su método; `fetch` rechaza → `kind: 'network'`; respuesta 409 con ProblemDetails → `ApiError` `http` 409; 204 → `undefined`; JSON → objeto; texto → cadena; JSON inválido con 200 → `kind: 'unknown'`. |
| `use-api.test.ts`    | Estado inicial `idle`; `execute` pasa por `loading` y termina en `success` con `data`; un fallo deja `status: 'error'`, `error` y `execute` resuelve `{ data: null, error }` sin rechazar; un fallo tras un éxito conserva `data`; dos `execute` seguidos con la primera respuesta llegando última → queda la segunda; respuesta tras desmontar → sin cambios de estado ni avisos; `reset` vuelve a `idle` e ignora la llamada en curso; `setData` con valor y con función; `execute` mantiene su identidad entre renders.  |

## Implementation plan

1. Crear `src/lib/api/api-error.ts` con `ApiError`, `createHttpError` y `toApiError`, y su test. Verificar: `pnpm test` pasa.
2. Crear `src/lib/api/access-token.ts` y `src/lib/api/api-client.ts` con `apiRequest` y `api`, y `api-client.test.ts`. Antes de escribir los casos de `ValidationErrors`, confirmar la forma real de sus elementos con el backend corriendo (un `POST /api/auth/register` con datos vacíos) o con el código de `CustomExceptionHandler`. Verificar: `pnpm test` pasa.
3. Crear `src/hooks/use-api.ts` y su test. Verificar: `pnpm test` y `pnpm lint` pasan, incluidas las reglas de `react-hooks`.
4. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** existe el cliente HTTP y `useApi`.
   - **Estructura:** `hooks/` y `lib/api/`.
   - **Contrato con la API:** cómo se declara una función de API con `api` y cómo se usa con `useApi`.
   - **Backlog:** fila 02 con este spec.
   - **Stack:** cerrar la línea de "pendiente de decidir" para el estado de servidor.
   - **Preguntas abiertas:** cerrar **estado de servidor** (`fetch` propio + `useApi`).

## Acceptance criteria

- [ ] Existen `src/lib/api/api-error.ts`, `src/lib/api/access-token.ts`, `src/lib/api/api-client.ts` y `src/hooks/use-api.ts`, con sus tests.
- [ ] `api.get<T>('/api/x', { a: 1, b: undefined })` llama a `fetch` con `<apiBaseUrl>/api/x?a=1`, método `GET` y `Accept: application/json`.
- [ ] `api.post('/api/x', { name: 'A' })` envía `{"name":"A"}` con `Content-Type: application/json`; `api.post('/api/x')` no envía cuerpo ni `Content-Type`.
- [ ] Con `setAccessTokenProvider(() => 'abc')`, las peticiones llevan `Authorization: Bearer abc`; sin proveedor, no llevan `Authorization`.
- [ ] Una respuesta 400 `application/problem+json` con `ValidationErrors` produce un `ApiError` con `kind: 'http'`, `status: 400`, el `title` y el `detail` del cuerpo, y la lista de `{ propertyName, errorMessage }`.
- [ ] Un `fetch` que rechaza produce un `ApiError` con `kind: 'network'` y `status: 0`.
- [ ] Una respuesta 204 resuelve `undefined`, una JSON resuelve el objeto y una de texto resuelve la cadena.
- [ ] `useApi(fn).execute()` nunca rechaza: con un fallo resuelve `{ data: null, error }` y deja `status: 'error'`.
- [ ] Con dos `execute` seguidos en los que la primera respuesta llega después, el estado final es el de la segunda.
- [ ] `reset()` deja `status: 'idle'`, `data: null` y `error: null`, y una respuesta posterior de una llamada anterior no lo cambia.
- [ ] `setData` actualiza `data` sin cambiar `status`.
- [ ] `execute`, `reset` y `setData` son la misma referencia en dos renders consecutivos.
- [ ] `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` terminan sin errores.
- [ ] `package.json` no tiene dependencias nuevas.
- [ ] `frontend/CLAUDE.md` refleja `hooks/` y `lib/api/`, tiene la fila 02 en el backlog y la pregunta de estado de servidor está cerrada.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** `fetch` propio con `useApi` encima. Elegido por el usuario: sin dependencias nuevas y con una sola forma de llamar a la API.
- **No:** TanStack Query, ni directo ni envuelto por `useApi`. Trae caché y reintentos que hoy no hacen falta, a costa de una dependencia y otro modelo mental. Si un día hace falta caché, irá en su propio spec.
- **Yes:** cliente, `ApiError` y proveedor de token en este spec. Así el hook trabaja con un único tipo de error y el spec de login solo tiene que registrar el proveedor.
- **Yes:** proveedor de token como función registrable (`setAccessTokenProvider`). El cliente no sabe dónde se guarda el token: esa decisión sigue abierta y es del spec de login.
- **No:** tratar el 401 en el cliente. Qué hacer (logout, redirigir) depende de la sesión, que aún no existe.
- **Yes:** objeto `api` con `get`/`post`/`put`/`delete` sobre `apiRequest`. Las funciones de los módulos quedan en una línea.
- **No:** solo `apiRequest`. Repetiría `method` en cada llamada.
- **Yes:** `execute` no lanza y devuelve `{ data } | { error }`. El componente no necesita `try/catch` y el error también queda en el estado.
- **No:** `execute` que relanza `ApiError`. Obligaría a capturar el error en cada llamada.
- **Yes:** `status` explícito además de `isLoading`. Distingue "aún no se pidió" (`idle`) de "se pidió y no hay datos".
- **Yes:** `reset` y `setData`, elegidos por el usuario: limpiar un formulario o diálogo y actualizar la vista tras una edición.
- **No:** `AbortController` ni la opción `immediate`. Descartados por el usuario en este spec.
- **Yes:** solo cuenta la última llamada (contador interno). Evita que una respuesta vieja pise a una nueva sin necesidad de cancelar en la red.
- **Yes:** conservar `data` mientras carga y tras un error. Al refrescar, la pantalla no se vacía.
- **Yes:** `fn` en una ref para que `execute` sea estable. Permite pasar funciones inline y usar `execute` en dependencias de `useEffect`.
- **Yes:** `src/hooks/` para hooks compartidos y `src/lib/api/` para el cliente. Elegido por el usuario; se documenta en `frontend/CLAUDE.md`.
- **No:** barrel `index.ts` en `lib/api/`. Una importación por archivo es más explícita y no hace falta mantener el índice.
- **Yes:** solo tests, sin usar el hook en la app. Elegido por el usuario; la primera llamada real llegará con el login.
- **Yes:** los mensajes de `ApiError` creados en el cliente (`Network error.`, `Unexpected error.`) en inglés, como los mensajes de excepción. Los textos para el usuario se deciden cuando haya interfaz de errores.
- **Yes:** sin validación del JSON en tiempo de ejecución. El contrato es el `README.md` del backend; Zod sigue como pregunta abierta de formularios.

## Risks

| Risk                                                                                                           | Mitigation                                                                                                                                                                                                         |
| -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| La forma de los elementos de `ValidationErrors` no es `{ propertyName, errorMessage }` (mayúsculas, otro nombre) | Se confirma en el paso 2 contra el backend real antes de escribir los tests. Si difiere, se ajusta el mapeo en `createHttpError` sin cambiar `ValidationError`.                                                      |
| Las reglas de `eslint-plugin-react-hooks` 7 prohíben escribir una ref durante el render                         | La ref de `fn` se actualiza en un `useLayoutEffect`, no en el cuerpo del hook.                                                                                                                                     |
| `config.ts` lanza al importarse si falta `VITE_API_BASE_URL`, y el cliente lo importa                            | En los tests la variable está en `test.env` de `vite.config.ts` (spec 01), así que los tests usan esa URL.                                                                                                        |
| El estado de módulo del proveedor de token se filtra entre tests                                               | Cada test que lo registra lo quita con `setAccessTokenProvider(null)` en `afterEach`.                                                                                                                              |
| Avisos de React por actualizar el estado tras desmontar                                                        | El contador también marca el desmontaje (efecto de limpieza), y las respuestas posteriores no tocan el estado.                                                                                                     |

## What is **not** in this spec

- Login, sesión, guardado del token ni manejo del 401.
- `AbortController`, `immediate`, caché, deduplicación ni reintentos.
- Funciones de API, DTOs o pantallas de los módulos.
- Interfaz para mostrar errores.
- Llamadas al backend real o cambios en él.
- TanStack Query.

Cada uno de ellos, si llega, va en su propio spec.

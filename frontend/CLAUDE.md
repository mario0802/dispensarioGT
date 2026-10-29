# CLAUDE.md — Frontend

Guía para Claude Code al trabajar en `frontend/`. **Este archivo es la única guía del frontend**: el `CLAUDE.md` de la raíz y `docs/` describen el backend (.NET) y sus reglas de arquitectura **no** se aplican aquí. Del backend solo se usa su contrato HTTP (ver **Contrato con la API**).

## Qué vamos a construir

Una SPA para el personal del dispensario médico que consume la API del backend: login, gestión de usuarios, doctores (y su horario), pacientes, agenda de citas y consultas. Cada rol ve solo lo que la API le permite (`Admin`, `Doctor`, `Receptionist`).

> **Estado actual:** proyecto Vite inicializado (spec 01): React + TypeScript, Tailwind, shadcn/ui (`slate`, con `button`, `dropdown-menu`, `card`, `input`, `label`, `alert`, `field`, `select`, `separator`, `sidebar`, `sheet`, `tooltip`, `skeleton`, `avatar`, `dialog`, `alert-dialog`, `table`, `badge`, `sonner`, `switch` y `textarea`), modo oscuro (`ThemeProvider`), `VITE_API_BASE_URL` tipada en `src/lib/config.ts`, ESLint, Prettier y Vitest. Cliente HTTP propio sobre `fetch` (`api`, `ApiError`, proveedor de token y manejador del 401 en `src/lib/api/`) y hook `useApi` en `src/hooks/` (spec 02). Login y sesión (spec 03): React Router con `/login`, `/` protegida y comodín a `/`; token en `localStorage` y usuario de `GET /api/auth/me` en `AuthProvider`/`useAuth`; un 401 con token cierra la sesión. Registro (spec 04): `/register` pública da de alta un recepcionista con `POST /api/auth/register` y lleva a `/login` con el aviso de cuenta creada y el email precargado (en el `state` del router). Layout de la app (spec 05): las rutas protegidas cuelgan de `AppLayout` (`features/layout/`), con sidebar a la izquierda (plegable a iconos, `Sheet` en móvil, estado en la cookie `sidebar_state`), menús por rol, barra superior con el título del menú activo y, al pie, el avatar con las iniciales y un menú con el tema y **Cerrar sesión**; el inicio (`/`) muestra la bienvenida. Menú **Doctores** (spec 06, `/doctors`, Admin y Recepción): tabla paginada con búsqueda y filtro de estado; el Admin da de alta, edita, activa y desactiva (modales y confirmaciones), y cada fila abre el modal del cronograma. Horario semanal (spec 07): el modal del cronograma muestra los 7 días (lunes a domingo) con sus bloques; el Admin lo edita (switch por día, varios bloques, horas completas, validación de solapes y duración mínima, confirmación al descartar) y Recepción solo lo ve; el rol `Doctor` tiene el menú **Mi horario** (`/my-schedule`) para ver y editar el suyo (`user.doctorId`). Menú **Pacientes** (spec 08, `/patients`, Admin y Recepción, ambos con todas las acciones): tabla paginada con búsqueda (nombre o documento) y filtro de estado, botón **Nuevo paciente**, lápiz por fila que abre el modal de edición (formulario con todos los campos de la API en secciones, fecha con `<input type="date">`) y menú ⋯ para activar y desactivar con confirmación. Guard `RequireRole` (un rol no permitido vuelve a `/`) y toasts con `sonner` (`Toaster` en `AppLayout`).

## Reglas de trabajo (obligatorias)

1. **SDD también en el frontend**: no se escribe código sin un spec `Approved`. Los specs se crean con `/spec` y se implementan con `/spec-impl`. Nada de andamiaje, refactors ni features "de paso".
   - **Excepción, requerimientos sencillos:** si el usuario pide **explícitamente** un cambio pequeño y acotado (corregir un bug, ajustar un texto, un estilo o una validación, renombrar, retocar un test o la documentación), se puede modificar el código sin spec. Solo vale si el cambio no añade páginas, rutas ni features, no añade ni cambia dependencias, no cambia la forma de consumir la API y no altera la estructura del proyecto. Igual se respetan las reglas de este archivo, y hay que compilar (`build`) y pasar lint y tests. Si hay dudas sobre si es "sencillo", preguntar o proponer un spec. La excepción nunca permite modificar el backend.
2. **Los specs del frontend viven en `frontend/specs/NN-slug.md`**, con numeración propia desde `01`, independiente de la del backend. La sesión se abre en la raíz del repositorio (así se puede consultar el backend) y el usuario indica si el trabajo es de frontend. En ese caso, `/spec` y `/spec-impl` sustituyen `specs/` por `frontend/specs/` en todo (listado, numeración, lectura de specs previos, guardado y búsqueda del spec), usan este archivo como memoria del proyecto en lugar del `CLAUDE.md` de la raíz y nombran la rama `spec-fe-NN-slug`. La configuración de ramas sigue en `specs/.spec-config.yml`.
   - El código del backend (`src/`, `tests/`, `README.md`) se puede **leer** para entender el contrato, pero un spec del frontend nunca lo modifica.
3. Si algo es ambiguo, preguntar en vez de suponer.
4. Al terminar un spec: compilar (`build`), pasar lint y tests, verificar los criterios de aceptación y anotar el spec en el backlog de este archivo. El cambio de estado (`Approved` / `Implemented`) lo hace el usuario.
5. **Idioma**: el código (componentes, hooks, tipos, variables, rutas, nombres de archivo) en **inglés**; los comentarios y la documentación, en **español**.
6. No hacer commits ni push salvo que el usuario lo pida.
7. Antes de configurar o usar una librería, consultar su documentación actual con Context7: las versiones y APIs cambian.

## Stack

- **React 19** + **Vite 8** (`@vitejs/plugin-react`) + **TypeScript 6** (modo estricto, que TS 6 activa por defecto) · gestor de paquetes **pnpm** (`packageManager` en `package.json`)
- **Tailwind CSS 4** (`@tailwindcss/vite`) + **shadcn/ui** (componentes copiados al proyecto, no como dependencia; primitivas de `radix-ui`, iconos `lucide-react`)
- Avisos (toasts) con **sonner** (spec 06): `toast.success`/`toast.error` de `sonner`; el `Toaster` de `components/ui/sonner.tsx` usa el `useTheme` propio en lugar de `next-themes` (no instalado)
- **ESLint** (flat config: `typescript-eslint`, `react-hooks`, `react-refresh`, y `eslint-config-prettier` al final) · **Prettier** con `prettier-plugin-tailwindcss` (sin punto y coma, comillas simples)
- **Vitest** + **Testing Library** + **jsdom** (configuración en el bloque `test` de `vite.config.ts`)
- Estado de servidor con **`fetch` propio** + el hook `useApi` (spec 02), sin TanStack Query
- **React Router 7** en modo declarativo (`BrowserRouter`, `Routes`, `Navigate`, importados de `react-router`; spec 03)
- Formularios con **React Hook Form** + **Zod 4** (`zodResolver` de `@hookform/resolvers`) y los componentes `field` de shadcn (`Field`, `FieldLabel`, `FieldError`). En Zod 4, `z.string().email()` está obsoleto: se usa `z.email()` (con `.pipe` si va tras `trim`/`min`/`max`)

Pendiente de decidir (ver **Preguntas abiertas**): tests E2E.

## Estructura

```
frontend/
├─ specs/                  # specs del frontend (NN-slug.md)
├─ .env.development        # VITE_API_BASE_URL (versionado); .env.local lo sobrescribe y está ignorado
├─ components.json         # configuración de shadcn/ui
├─ vite.config.ts          # plugins, alias @, puerto 5173 fijo y bloque test de Vitest
└─ src/
   ├─ main.tsx  App.tsx  index.css   # index.css: Tailwind + variables de shadcn (slate, claro y oscuro)
   ├─ app-routes.tsx      # rutas de la app (App: BrowserRouter > AuthProvider > AppRoutes)
   ├─ vite-env.d.ts       # tipos estrictos de import.meta.env
   ├─ features/<módulo>/  # auth, home, users, doctors, patients, appointments, consultations (se crean en su spec)
   │  ├─ <pagina>-page.tsx   # las páginas de la feature, en la raíz de su carpeta (p. ej. login-page.tsx)
   │  └─ components/         # componentes propios de la feature (p. ej. login-form.tsx)
   ├─ features/auth/      # auth-types, auth-api, role-labels, document-type-labels, token-storage, auth-provider, use-auth, login-page, register-page
   │  └─ components/      # login-form, register-form, require-auth y require-role (guards), session-screens (carga y error del arranque)
   ├─ features/home/      # home-page: bienvenida (ruta index de AppLayout)
   ├─ features/doctors/   # doctor-types, doctor-schedule-types, doctors-api, doctor-messages, doctor-schedule (días, horas, conversión y esquema Zod), doctors-page (/doctors), my-schedule-page (/my-schedule)
   │  └─ components/      # doctors-toolbar, doctors-table, doctor-row-actions, doctor-form, doctor-form-dialog, doctor-status-dialog, doctor-schedule-dialog, doctor-schedule-panel (carga, lectura/edición y guardado; lo comparten el modal y Mi horario), doctor-schedule-view, doctor-schedule-form, doctor-schedule-day-row, discard-changes-dialog
   ├─ features/patients/  # patient-types, patients-api, patient-messages, patient-labels (género, tipos de sangre, documento, edad y fechas sin desfase de zona), patients-page (/patients)
   │  └─ components/      # patients-toolbar, patients-table, patient-row-actions (lápiz y menú ⋯), patient-form (secciones, dirección opcional con calle y ciudad obligatorias), patient-form-dialog (cuerpo con scroll y botones fijos), patient-status-dialog
   ├─ features/layout/    # app-layout (ruta layout con Outlet y Toaster), nav-items (menús por rol), initials, sidebar-cookie
   │  └─ components/      # app-sidebar, nav-main, nav-user (avatar y su menú: tema y cerrar sesión), user-avatar
   ├─ components/         # componentes compartidos entre features (theme-provider)
   │  └─ ui/              # generados por shadcn/ui
   ├─ hooks/              # hooks compartidos entre features (use-api, use-debounced-value; use-mobile, de shadcn, lo usa sidebar)
   ├─ lib/                # utils (cn), config
   │  └─ api/             # cliente HTTP: api-client, api-error, access-token, unauthorized-handler, paginated-result (sin barrel)
   └─ test/               # setup de Vitest (con hasPointerCapture/scrollIntoView/ResizeObserver simulados para Radix) y utilidades de test (matchMedia simulado, api-responses, auth-context)
```
- Alias `@/` → `src/`. Archivos en kebab-case (como los de shadcn); componentes en PascalCase.
- **Organización de cada feature**: en la raíz de `features/<módulo>/` van solo las páginas (y, cuando existan, los archivos propios del módulo como su cliente de API o sus tipos); todos los componentes que solo usa esa feature van en `features/<módulo>/components/`, para que la carpeta no se llene de ruido. Ejemplo: `features/auth/login-page.tsx` y `features/auth/components/login-form.tsx`. Si un componente lo usan dos o más features, se mueve a `src/components/`.
- **Añadir un menú**: su `NavItem` (`title`, `url`, icono de `lucide-react`) en `roleNavItems` del rol (`features/layout/nav-items.ts`) y su página como ruta hija de `AppLayout` en `app-routes.tsx`. El título de la barra superior sale del `NavItem` activo (`/` solo exacta; los demás, con su ruta o sus hijas). Si la ruta es solo de algunos roles, se envuelve la página en `RequireRole allowedRoles={[...]}` (`features/auth/components/require-role`), que lleva a `/` a los demás; las acciones de escritura se ocultan según `user.role`.
- **Sesión**: las demás features la leen con `useAuth()` (`@/features/auth/use-auth`): `status` (`loading` | `error` | `authenticated` | `anonymous`), `user` (`CurrentUserDto`), `login`, `logout` y `retry`. Las rutas protegidas se envuelven en `RequireAuth`.
- Los tests van junto al código (`*.test.ts(x)`), importan `describe`/`it`/`expect` de `vitest` (sin globals) y simulan `matchMedia` con `@/test/match-media`. Las respuestas simuladas de la API (`jsonResponse`, `problemResponse`, `testUser`, `storeValidToken`…) están en `@/test/api-responses`. Los tests que montan `AuthProvider` limpian en `afterEach` `localStorage`, `setAccessTokenProvider(null)` y `setUnauthorizedHandler(null)`. Los que solo necesitan el usuario de `useAuth` usan `TestAuthProvider` (`@/test/auth-context`); las páginas con tooltips fuera del sidebar, un `TooltipProvider`, y las que comprueban toasts montan `<Toaster />`.

## Contrato con la API

- **Fuente de verdad**: la sección **Endpoints** del `README.md` de la raíz (rutas, acceso por rol, peticiones, respuestas y códigos de error). Scalar en `/scalar` con el backend corriendo. El frontend no modifica el backend: si necesita un cambio en la API, es un spec del backend.
- **Backend local**: `dotnet run --project src/Services/Dispensario/Dispensario.Api` desde la raíz, en `http://localhost:5168` (`https://localhost:7247` con el perfil https). El backend admite CORS desde `http://localhost:5173` en Development (`Cors:AllowedOrigins`).
- **Autenticación**: `POST /api/auth/login` devuelve un JWT (HS256) que se envía como `Authorization: Bearer <token>`. `GET /api/auth/me` devuelve el usuario actual. Un token sigue valiendo hasta que caduca aunque el usuario se desactive. El claim `doctor_id` solo aparece en los tokens emitidos después de vincular el usuario con su doctor (hace falta un nuevo login).
- **Errores**: siempre `application/problem+json` (ProblemDetails). Los de validación (400) traen la extensión `ValidationErrors`; 401 = sin sesión o credenciales inválidas (el login responde siempre `Invalid credentials.`), 403 = rol sin permiso, 404, 409 = conflicto (duplicados, cruces de citas).
- **Cómo se llama a la API** (spec 02): cada módulo declara sus funciones con el objeto `api` (`@/lib/api/api-client`) y los componentes las usan con `useApi` (`@/hooks/use-api`). Nadie llama a `fetch` directamente.
  ```ts
  // features/patients/patients-api.ts
  export const getPatient = (id: string) => api.get<PatientDto>(`/api/patients/${id}`)
  // En un componente
  const { data, error, status, isLoading, execute, reset, setData } = useApi(getPatient)
  ```
  - `api.get(path, query)`, `api.post(path, body)`, `api.put(path, body)` y `api.delete(path)` usan `config.apiBaseUrl`, envían JSON y añaden `Authorization: Bearer` si hay proveedor de token (`setAccessTokenProvider`, lo registra el `AuthProvider`). En la `query`, los valores `null`/`undefined` se omiten. Un 204 resuelve `undefined`; una respuesta que no es JSON resuelve el texto.
  - Todo fallo es un `ApiError` (`@/lib/api/api-error`): `kind` (`http` | `network` | `unknown`), `status` (0 sin respuesta), `title`, `detail`, `validationErrors` (`{ propertyName, errorMessage }[]`, de la extensión `ValidationErrors`) y `traceId`.
  - **401**: si la petición llevaba token, el cliente avisa al manejador global (`setUnauthorizedHandler`, lo registra el `AuthProvider`), que cierra la sesión y lleva a `/login`; después lanza el `ApiError` igual. El 401 del login (sin token) no lo dispara, y el 403 no tiene trato especial: cada pantalla lo muestra.
  - **Sesión**: el token se guarda en `localStorage` (`dispensario-auth`, `{ accessToken, expiresAtUtc }`). Al arrancar, uno caducado se descarta sin llamar a la API; uno vigente se valida con `GET /api/auth/me` (401/404 → cierre de sesión; error de red → pantalla con **Reintentar**, conservando el token). No hay temporizador de caducidad: durante el uso, la caducidad llega como 401.
  - `execute(...args)` **nunca rechaza**: resuelve `{ data, error: null }` o `{ data: null, error }`, y el error queda también en el estado. Solo cuenta la última llamada (las respuestas viejas, las de antes de `reset` y las de después de desmontar se ignoran), `data` se conserva mientras carga y tras un error, y `execute`, `reset` y `setData` son estables (se pueden usar en dependencias de `useEffect`). No hay `AbortController`, ejecución al montar ni caché.
- **Listados paginados**: `{ pageIndex, pageSize, count, data }`.
- **Enums** como texto (`"Scheduled"`, `"Receptionist"`…); `bloodType` con notación `"A+"`, `"AB-"`…
- **Horas de las citas**: locales del dispensario y **sin offset** (`"2026-11-03T09:30"`); enviar una con `Z` u offset da 400. Las horas del horario semanal van como `"HH:mm"`; la API admite minutos múltiplo de 5, pero el frontend solo crea bloques en horas completas (`00:00` a `23:00`) y muestra tal cual las que no lo son, pidiendo corregirlas al editar (spec 07).

## Comandos

Desde `frontend/`:
```bash
pnpm install
pnpm dev            # http://localhost:5173 (puerto fijo: si está ocupado, falla)
pnpm build          # tsc -b + vite build → dist/
pnpm lint
pnpm format         # format:check para solo comprobar
pnpm test           # test:watch en modo watch
```

- **shadcn/ui**: añadir componentes con `pnpm dlx shadcn@latest add <componente>`. El CLI actual (4.x) ya no ofrece `slate` en `init`: `components.json` (estilo `new-york`, `baseColor: slate`) y las variables de `index.css` se escribieron a mano desde `colors/slate.json`, así que **no ejecutar `shadcn init`** (sobrescribiría ambos). En Windows el CLI genera `import { cn } from "cn"`: tras cada `add`, corregirlo a `@/lib/utils`.
- Vitest corre en modo `test` y no carga `.env.development`: `VITE_API_BASE_URL` para los tests está en el bloque `test.env` de `vite.config.ts`.
- `sidebar.tsx` y `use-mobile.ts` se retocaron tras generarlos para las reglas de `react-hooks` (`purity` y `set-state-in-effect`): al volver a añadirlos con el CLI, repetir el ajuste. El `SidebarTrigger` y el `SidebarRail` reciben `aria-label` en español (el texto `sr-only` de shadcn está en inglés).
- Prettier no formatea los `.md` ni `src/components/ui` (ver `.prettierignore`).
- Con `core.autocrlf=true` (Windows), los archivos salen con CRLF y `pnpm format:check` los marca todos aunque solo cambie el fin de línea: para comprobar el estilo en local, `pnpm exec prettier --check . --end-of-line auto`. No ejecutar `prettier --write` sobre todo `src`: convierte a LF archivos que no se han tocado.
- El CLI de shadcn añade `next-themes` con `sonner` y pregunta si sobrescribe `button.tsx`: responder que no y quitar `next-themes` (`pnpm remove next-themes`). El botón X de `dialog.tsx` tiene el texto `sr-only` "Cerrar".
- pnpm 11 no instala versiones publicadas hace menos de un día (`minimumReleaseAge`): si lo intenta, añade una excepción en un `pnpm-workspace.yaml` nuevo. Mejor elegir la versión anterior que crear ese archivo.

## Backlog

| #   | Feature                                                                                       | Spec |
| --- | --------------------------------------------------------------------------------------------- | ---- |
| 01  | Andamiaje: proyecto Vite + React + TS, Tailwind, shadcn/ui, conexión con la API en desarrollo | `specs/01-vite-react-project-setup.md` |
| 02  | Cliente HTTP (`api`, `ApiError`, proveedor de token) y hook `useApi`                          | `specs/02-api-client-and-use-api-hook.md` |
| 03  | Página de login y sesión (React Router, token en `localStorage`, `/api/auth/me`, 401 global)  | `specs/03-login-and-session.md` |
| 04  | Página de registro (`/register`, `POST /api/auth/register`, aviso y email precargado en el login) | `specs/04-register-page.md` |
| 05  | Layout con sidebar (menús por rol, solo **Inicio**) y avatar con iniciales, tema y cerrar sesión | `specs/05-app-layout-and-sidebar.md` |
| 06  | Menú **Doctores**: listado, alta, edición, activación/desactivación y modal (vacío) del cronograma | `specs/06-doctors-menu.md` |
| 07  | Horario semanal: ver y editar en el modal del cronograma (Admin) y menú **Mi horario** del `Doctor` | `specs/07-doctor-weekly-schedule.md` |
| 08  | Menú **Pacientes**: listado, alta, edición (lápiz por fila) y activación/desactivación (Admin y Recepción) | `specs/08-patients-menu.md` |

Las siguientes filas se añaden al planificar (un menú o módulo por fila, empezando por los de recepción).

## Preguntas abiertas

- ~~**Router**~~: cerrada en el spec 03: React Router 7 en modo declarativo.
- ~~**Formularios y validación**~~: cerrada en el spec 03: React Hook Form + Zod con los componentes `field` de shadcn.
- **Tests E2E**: ¿Playwright? (los unitarios ya usan Vitest + Testing Library, spec 01).
- ~~**Gestor de paquetes**~~: cerrada en el spec 01 (pnpm).
- ~~**Estado de servidor**~~: cerrada en el spec 02: `fetch` propio + `useApi`, sin TanStack Query.
- ~~**Conexión en desarrollo**~~: cerrada en el spec 01: URL directa con `VITE_API_BASE_URL`; el backend ya admite CORS para `http://localhost:5173` en Development.
- ~~**Guardado del token**~~: cerrada en el spec 03: `localStorage`, con `expiresAtUtc`.
- ~~**Idioma de la interfaz**~~: cerrada en el spec 03: solo español, con mensajes propios (los `title`/`detail` de la API vienen en inglés).

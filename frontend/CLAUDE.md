# CLAUDE.md — Frontend

Guía para Claude Code al trabajar en `frontend/`. **Este archivo es la única guía del frontend**: el `CLAUDE.md` de la raíz y `docs/` describen el backend (.NET) y sus reglas de arquitectura **no** se aplican aquí. Del backend solo se usa su contrato HTTP (ver **Contrato con la API**).

## Qué vamos a construir

Una SPA para el personal del dispensario médico que consume la API del backend: login, gestión de usuarios, doctores (y su horario), pacientes, agenda de citas y consultas. Cada rol ve solo lo que la API le permite (`Admin`, `Doctor`, `Receptionist`).

> **Estado actual:** proyecto Vite inicializado (spec 01): React + TypeScript, Tailwind, shadcn/ui (`slate`, con `button` y `dropdown-menu`), modo oscuro (`ThemeProvider` + `ModeToggle`), `VITE_API_BASE_URL` tipada en `src/lib/config.ts`, ESLint, Prettier y Vitest. La página inicial es estática: todavía no hay llamadas a la API, router ni login.

## Reglas de trabajo (obligatorias)

1. **SDD también en el frontend**: no se escribe código sin un spec `Approved`. Los specs se crean con `/spec` y se implementan con `/spec-impl`. Nada de andamiaje, refactors ni features "de paso".
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
- **ESLint** (flat config: `typescript-eslint`, `react-hooks`, `react-refresh`, y `eslint-config-prettier` al final) · **Prettier** con `prettier-plugin-tailwindcss` (sin punto y coma, comillas simples)
- **Vitest** + **Testing Library** + **jsdom** (configuración en el bloque `test` de `vite.config.ts`)

Pendiente de decidir (ver **Preguntas abiertas**): router, gestión del estado de servidor, formularios y validación, y tests E2E.

## Estructura

```
frontend/
├─ specs/                  # specs del frontend (NN-slug.md)
├─ .env.development        # VITE_API_BASE_URL (versionado); .env.local lo sobrescribe y está ignorado
├─ components.json         # configuración de shadcn/ui
├─ vite.config.ts          # plugins, alias @, puerto 5173 fijo y bloque test de Vitest
└─ src/
   ├─ main.tsx  App.tsx  index.css   # index.css: Tailwind + variables de shadcn (slate, claro y oscuro)
   ├─ vite-env.d.ts       # tipos estrictos de import.meta.env
   ├─ features/<módulo>/  # auth, users, doctors, patients, appointments, consultations (se crean en su spec)
   │  ├─ <pagina>-page.tsx   # las páginas de la feature, en la raíz de su carpeta (p. ej. login-page.tsx)
   │  └─ components/         # componentes propios de la feature (p. ej. login-form.tsx)
   ├─ components/         # componentes compartidos entre features (theme-provider, mode-toggle)
   │  └─ ui/              # generados por shadcn/ui
   ├─ lib/                # utils (cn), config
   └─ test/               # setup de Vitest y utilidades de test (matchMedia simulado)
```
- Alias `@/` → `src/`. Archivos en kebab-case (como los de shadcn); componentes en PascalCase.
- **Organización de cada feature**: en la raíz de `features/<módulo>/` van solo las páginas (y, cuando existan, los archivos propios del módulo como su cliente de API o sus tipos); todos los componentes que solo usa esa feature van en `features/<módulo>/components/`, para que la carpeta no se llene de ruido. Ejemplo: `features/auth/login-page.tsx` y `features/auth/components/login-form.tsx`. Si un componente lo usan dos o más features, se mueve a `src/components/`.
- Los tests van junto al código (`*.test.ts(x)`), importan `describe`/`it`/`expect` de `vitest` (sin globals) y simulan `matchMedia` con `@/test/match-media`.

## Contrato con la API

- **Fuente de verdad**: la sección **Endpoints** del `README.md` de la raíz (rutas, acceso por rol, peticiones, respuestas y códigos de error). Scalar en `/scalar` con el backend corriendo. El frontend no modifica el backend: si necesita un cambio en la API, es un spec del backend.
- **Backend local**: `dotnet run --project src/Services/Dispensario/Dispensario.Api` desde la raíz, en `http://localhost:5168` (`https://localhost:7247` con el perfil https). El backend **no tiene CORS configurado**.
- **Autenticación**: `POST /api/auth/login` devuelve un JWT (HS256) que se envía como `Authorization: Bearer <token>`. `GET /api/auth/me` devuelve el usuario actual. Un token sigue valiendo hasta que caduca aunque el usuario se desactive. El claim `doctor_id` solo aparece en los tokens emitidos después de vincular el usuario con su doctor (hace falta un nuevo login).
- **Errores**: siempre `application/problem+json` (ProblemDetails). Los de validación (400) traen la extensión `ValidationErrors`; 401 = sin sesión o credenciales inválidas (el login responde siempre `Invalid credentials.`), 403 = rol sin permiso, 404, 409 = conflicto (duplicados, cruces de citas).
- **Listados paginados**: `{ pageIndex, pageSize, count, data }`.
- **Enums** como texto (`"Scheduled"`, `"Receptionist"`…); `bloodType` con notación `"A+"`, `"AB-"`…
- **Horas de las citas**: locales del dispensario y **sin offset** (`"2026-11-03T09:30"`); enviar una con `Z` u offset da 400. Las horas del horario semanal van como `"HH:mm"`.

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
- Prettier no formatea los `.md` ni `src/components/ui` (ver `.prettierignore`).

## Backlog

| #   | Feature                                                                                       | Spec |
| --- | --------------------------------------------------------------------------------------------- | ---- |
| 01  | Andamiaje: proyecto Vite + React + TS, Tailwind, shadcn/ui, conexión con la API en desarrollo | `specs/01-vite-react-project-setup.md` |

Las siguientes filas se añaden al planificar (login y sesión, layout por rol, y luego un módulo por fila).

## Preguntas abiertas

- **Router**: React Router o TanStack Router.
- **Estado de servidor**: TanStack Query o `fetch` a mano.
- **Formularios y validación**: React Hook Form + Zod u otra opción.
- **Tests E2E**: ¿Playwright? (los unitarios ya usan Vitest + Testing Library, spec 01).
- ~~**Gestor de paquetes**~~: cerrada en el spec 01 (pnpm).
- ~~**Conexión en desarrollo**~~: cerrada en el spec 01: URL directa con `VITE_API_BASE_URL`. **Pendiente** un spec del backend que añada CORS para `http://localhost:5173` antes de la primera llamada a la API.
- **Guardado del token**: memoria, `sessionStorage` o `localStorage`.
- **Idioma de la interfaz**: ¿solo español?

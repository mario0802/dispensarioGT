# SPEC 01 — Inicialización del proyecto frontend con Vite y React

> **Status:** Implemented
> **Depends on:** —
> **Date:** 2026-10-27
> **Objective:** Crear en `frontend/` el proyecto Vite + React + TypeScript con Tailwind, shadcn/ui (slate y modo oscuro), ESLint, Prettier y Vitest, mostrando una página estática que todavía no llama a la API.

## Por qué este spec existe

Es el primer spec del frontend: deja la base de herramientas sobre la que se construyen todos los demás.
No llama a la API porque el backend todavía no tiene CORS y se eligió la URL directa (`VITE_API_BASE_URL`) en lugar del proxy de Vite.
La primera llamada real llegará cuando exista un spec del backend que añada CORS para el origen del frontend.

## Scope

**In:**

- Proyecto Vite con la plantilla `react-ts` y `@vitejs/plugin-react`, generado en `frontend/`, sin la demo de la plantilla.
- pnpm como gestor de paquetes (`packageManager` en `package.json` y `pnpm-lock.yaml` versionado).
- Servidor de desarrollo fijo en el puerto `5173` (`strictPort: true`), para que el futuro origen CORS sea estable.
- Alias `@/` → `src/` en Vite y en TypeScript.
- Tailwind CSS con el plugin oficial de Vite.
- shadcn/ui inicializado con color base `slate` y variables CSS, con los componentes `button` y `dropdown-menu`.
- Modo oscuro con el patrón oficial de shadcn para Vite: `ThemeProvider` (`light` | `dark` | `system`, por defecto `system`) guardado en `localStorage` con la clave `dispensario-theme`, y un `ModeToggle` con menú de tres opciones.
- Página inicial estática con el nombre de la app, el `ModeToggle` y un `Button` de shadcn.
- `VITE_API_BASE_URL` tipada y leída en `src/lib/config.ts`, con `frontend/.env.development` versionado (`http://localhost:5168`).
- ESLint (configuración de la plantilla) compatible con Prettier.
- Prettier con `prettier-plugin-tailwindcss`.
- Vitest + Testing Library + jsdom, con tests de la página, del `ThemeProvider` y de la configuración.
- Actualizar `frontend/CLAUDE.md` (estado, stack, estructura, comandos, backlog y preguntas abiertas).

**Out of scope (for future specs):**

- CORS en el backend (spec del backend).
- Cliente HTTP (`fetch` con token y ProblemDetails), login y sesión.
- Router, TanStack Query, formularios y validación.
- Layout por rol, navegación y cualquier pantalla de módulo.
- Identidad visual (paleta propia, tipografía, logo, favicon): un spec de diseño con la skill `frontend-design`.
- Tests E2E con Playwright.
- Internacionalización.
- CI, Docker y despliegue.

## Data model

Este spec no introduce datos de dominio. Solo aparecen la configuración de la app y el estado del tema.

### Estructura resultante

```
frontend/
├─ CLAUDE.md                     # existente, se actualiza
├─ specs/01-vite-react-project-setup.md
├─ .env.development              # VITE_API_BASE_URL=http://localhost:5168 (versionado, no es secreto)
├─ .gitignore                    # el de la plantilla (+ .env.local, .env.*.local)
├─ .prettierrc.json  .prettierignore
├─ components.json               # configuración de shadcn
├─ eslint.config.js
├─ index.html                    # lang="es", <title>Dispensario Médico</title>
├─ package.json  pnpm-lock.yaml
├─ tsconfig.json  tsconfig.app.json  tsconfig.node.json
├─ vite.config.ts                # plugins react y tailwind, alias @, server 5173, bloque test de Vitest
├─ public/                       # favicon de la plantilla hasta el spec de diseño
└─ src/
   ├─ main.tsx                   # monta <App /> dentro de <ThemeProvider>
   ├─ App.tsx                    # página estática
   ├─ App.test.tsx
   ├─ index.css                  # Tailwind + variables de shadcn (slate, claro y oscuro)
   ├─ vite-env.d.ts              # tipos de import.meta.env
   ├─ components/
   │  ├─ ui/                     # generados por shadcn: button.tsx, dropdown-menu.tsx
   │  ├─ theme-provider.tsx
   │  ├─ theme-provider.test.tsx
   │  └─ mode-toggle.tsx
   ├─ lib/
   │  ├─ utils.ts                # cn() de shadcn
   │  ├─ config.ts
   │  └─ config.test.ts
   └─ test/setup.ts              # matchers de @testing-library/jest-dom
```

- `src/features/<módulo>/` es la carpeta acordada para los módulos (`auth`, `users`, `doctors`, `patients`, `appointments`, `consultations`), pero este spec **no** la crea: cada módulo la crea en su spec.
- Los nombres de archivo van en kebab-case, como los que genera shadcn; los componentes, en PascalCase.
- Se borran `frontend/.gitkeep` y la demo de la plantilla (`App.css`, `src/assets/`, el contador y los logos).

### `package.json` (scripts)

```json
{
  "name": "dispensario-frontend",
  "private": true,
  "type": "module",
  "packageManager": "pnpm@<versión instalada>",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "lint": "eslint .",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "test": "vitest run",
    "test:watch": "vitest"
  }
}
```

Las versiones de las dependencias son las estables más recientes al implementar, verificadas con Context7 y fijadas en `pnpm-lock.yaml`.

### Configuración (`src/lib/config.ts`)

```ts
// vite-env.d.ts
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
}

// config.ts
export interface AppConfig {
  apiBaseUrl: string // sin barra final
}

export function readConfig(env: Partial<ImportMetaEnv>): AppConfig // lanza Error si falta o está vacía
export const config: AppConfig = readConfig(import.meta.env)
```

- Si `VITE_API_BASE_URL` falta o está vacía, `readConfig` lanza `Error("VITE_API_BASE_URL is not configured.")`.
- Se recortan los espacios y la barra final: `http://localhost:5168/` → `http://localhost:5168`.
- Nadie usa `config` todavía: lo consumirá el cliente HTTP.
- `.env.local` (ignorado por git) permite sobrescribir la URL en cada máquina.

### Tema (`src/components/theme-provider.tsx`)

```ts
type Theme = 'light' | 'dark' | 'system'

interface ThemeProviderProps {
  children: React.ReactNode
  defaultTheme?: Theme // "system"
  storageKey?: string // "dispensario-theme"
}

// useTheme(): { theme: Theme; setTheme: (theme: Theme) => void }
```

- Aplica la clase `dark` o `light` en `<html>`. Con `system` resuelve con `prefers-color-scheme`.
- `setTheme` guarda el valor en `localStorage[storageKey]`. Un valor guardado inválido se trata como `defaultTheme`.
- `useTheme` fuera del provider lanza `Error("useTheme must be used within a ThemeProvider.")`.
- `ModeToggle` usa `DropdownMenu` y `Button` de shadcn con tres opciones: `Claro`, `Oscuro` y `Sistema`.

### Página inicial (`src/App.tsx`)

- Cabecera con el título `Dispensario Médico` y el `ModeToggle` a la derecha.
- Cuerpo con el texto `Frontend en construcción.` y un `Button` de shadcn con el texto `Comenzar`, sin acción.
- Solo clases de Tailwind y variables del tema: se ve bien en claro y en oscuro.

### Tests

| Archivo                   | Casos                                                                                                                                                                                                                                    |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `App.test.tsx`            | Muestra el título `Dispensario Médico`; muestra el botón `Comenzar`; muestra el botón del `ModeToggle` (nombre accesible `Cambiar tema`).                                                                                                |
| `theme-provider.test.tsx` | Sin valor guardado y con el sistema en oscuro → `<html>` tiene `dark`; `setTheme("light")` → clase `light` y `localStorage["dispensario-theme"] = "light"`; un valor guardado se respeta al montar; `useTheme` fuera del provider lanza. |
| `config.test.ts`          | Devuelve la URL sin barra final; lanza si falta; lanza si está vacía o solo tiene espacios.                                                                                                                                              |

`src/test/setup.ts` registra `@testing-library/jest-dom/vitest`, y el bloque `test` de `vite.config.ts` usa `environment: "jsdom"`, `setupFiles` y `globals: false`. `matchMedia` no existe en jsdom: se simula dentro de los tests que lo necesitan.

## Implementation plan

1. Generar la plantilla `react-ts` de Vite en el scratchpad (no dentro de `frontend/`, para no tocar `CLAUDE.md`) y copiarla a `frontend/`. Borrar `.gitkeep` y la demo. Ajustar `index.html` (`lang="es"`, título), `package.json` (`name`, `packageManager`) y `server: { port: 5173, strictPort: true }`. Ejecutar `pnpm install`. Verificar: `pnpm dev` sirve en `5173` y `pnpm build` termina sin errores.
2. Añadir Tailwind con su plugin de Vite y el alias `@/` en `vite.config.ts`, `tsconfig.json` y `tsconfig.app.json`. Verificar: una clase de Tailwind en `App.tsx` se aplica y `pnpm build` pasa.
3. Inicializar shadcn (`slate`, variables CSS) y añadir `button` y `dropdown-menu`. Verificar: `components.json` existe, `src/lib/utils.ts` y `src/components/ui/` están generados y `pnpm build` pasa.
4. Crear `ThemeProvider` y `ModeToggle`, envolver `<App />` en `main.tsx` y escribir la página estática. Verificar a mano: el menú cambia entre claro, oscuro y sistema y el valor persiste al recargar.
5. Crear `vite-env.d.ts`, `src/lib/config.ts` y `.env.development`, y añadir `.env.local` y `.env.*.local` al `.gitignore` si la plantilla no los cubre. Verificar: `pnpm build` pasa.
6. Añadir Prettier, `prettier-plugin-tailwindcss` y `eslint-config-prettier` (último en `eslint.config.js`), con `.prettierrc.json` y `.prettierignore` (`dist`, `pnpm-lock.yaml`, `src/components/ui`). Ejecutar `pnpm format`. Verificar: `pnpm lint` y `pnpm format:check` pasan.
7. Añadir Vitest, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event` y `jsdom`, el bloque `test` y `src/test/setup.ts`, y escribir los tests de la tabla. Verificar: `pnpm test` pasa.
8. Actualizar `frontend/CLAUDE.md`: estado actual, stack (pnpm, Vitest, Prettier), estructura, comandos y fila 01 del backlog con este spec. En preguntas abiertas, cerrar gestor de paquetes, tests unitarios y conexión (URL directa + CORS del backend, pendiente).

## Acceptance criteria

- [ ] `frontend/CLAUDE.md` sigue existiendo con su contenido previo más lo añadido en el paso 8, y `frontend/.gitkeep` ya no existe.
- [ ] `pnpm install` desde `frontend/` termina sin errores y `pnpm-lock.yaml` está versionado.
- [ ] `pnpm dev` sirve la app en `http://localhost:5173`; con el puerto ocupado, falla en lugar de usar otro.
- [ ] La página muestra `Dispensario Médico`, `Frontend en construcción.`, el botón `Comenzar` y el `ModeToggle`, sin errores en la consola del navegador.
- [ ] Elegir `Oscuro` añade la clase `dark` a `<html>` y guarda `dark` en `localStorage["dispensario-theme"]`; tras recargar, sigue en oscuro.
- [ ] Con `Sistema` y el sistema operativo en oscuro, la página se ve en oscuro.
- [ ] La app no hace ninguna petición a `http://localhost:5168` (pestaña Red del navegador).
- [ ] `import.meta.env.VITE_API_BASE_URL` está tipada: un error de escritura en el nombre no compila.
- [ ] `pnpm build` termina sin errores de TypeScript y genera `dist/`.
- [ ] `pnpm lint` y `pnpm format:check` terminan sin errores.
- [ ] `pnpm test` ejecuta los tests de `App`, `ThemeProvider` y `config`, y todos pasan.
- [ ] Una importación con `@/` (por ejemplo `@/components/ui/button`) compila y funciona en los tests.
- [ ] `git status` no muestra `node_modules/`, `dist/` ni `.env.local`.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** pnpm. Ya está instalado, es rápido y es estricto con las dependencias fantasma.
- **No:** npm. Funciona, pero sin ventaja frente a pnpm.
- **Yes:** URL directa con `VITE_API_BASE_URL`. Elegida por el usuario, y es el mismo modelo que en producción.
- **No:** proxy de Vite. Evitaría CORS en desarrollo, pero se descartó.
- **Yes:** no llamar a la API en este spec. El backend no tiene CORS y el frontend no modifica el backend.
- **Yes:** `.env.development` versionado. La URL local no es secreta y así el proyecto funciona al clonar. `.env.local` permite sobrescribirla.
- **No:** solo `.env.example`. Obliga a cada uno a crear su archivo antes del primer arranque.
- **Yes:** solo configuración tipada (`readConfig` + `config`). El cliente HTTP depende de decisiones del login (token y errores) y va en ese spec.
- **Yes:** `readConfig(env)` como función pura. Permite probar los casos de error sin tocar `import.meta.env`.
- **Yes:** puerto `5173` con `strictPort`. El spec de CORS del backend necesita un origen fijo.
- **Yes:** `@vitejs/plugin-react`. Es el de la plantilla oficial y deja abierta la puerta a plugins Babel como React Compiler.
- **No:** `@vitejs/plugin-react-swc`. Su ventaja de velocidad no compensa perder esa compatibilidad.
- **Yes:** shadcn con `slate` y modo oscuro desde ya, con el patrón oficial (`light` | `dark` | `system`, por defecto `system`, clave `dispensario-theme`).
- **No:** toggle simple de dos estados. El patrón oficial respeta la preferencia del sistema.
- **Yes:** estructura `features/` + `components/` + `lib/`. Este spec solo crea las carpetas que usa.
- **No:** `modules/` + `shared/`.
- **Yes:** Prettier con `prettier-plugin-tailwindcss` y `eslint-config-prettier`, para que ESLint no choque con el formato.
- **Yes:** excluir `src/components/ui` de Prettier. Son archivos generados por shadcn y se regeneran con su CLI.
- **Yes:** Vitest con su configuración en `vite.config.ts`. Comparte alias y plugins sin duplicarlos en un `vitest.config.ts`.
- **No:** Playwright en este spec. Llega cuando haya flujos que probar.
- **Yes:** generar la plantilla fuera de `frontend/` y copiarla. `create vite` sobre una carpeta no vacía pide sobrescribir y podría borrar `CLAUDE.md`.
- **Yes:** textos de la página en español. Son tres cadenas; la internacionalización sigue como pregunta abierta.
- **Yes:** versiones estables más recientes al implementar, verificadas con Context7 y fijadas en el lockfile, en lugar de fijarlas ahora en el spec.

## Risks

| Risk                                                                                      | Mitigation                                                                                                                                                                      |
| ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `create vite` o `shadcn init` sobrescriben `frontend/CLAUDE.md`                           | La plantilla se genera en el scratchpad y se copia; se comprueba que `CLAUDE.md` está intacto antes de seguir (primer criterio de aceptación).                                  |
| El CLI de shadcn cambió respecto a la documentación conocida (primitivas, estilos, flags) | Se consulta con Context7 antes del paso 3 y se aceptan los valores por defecto del CLI salvo `slate` y las variables CSS. Lo elegido queda en `components.json`.                |
| `matchMedia` no existe en jsdom y rompe los tests del tema                                | Se simula en los tests que lo necesitan.                                                                                                                                        |
| La regla `*.env` del `.gitignore` de la raíz                                              | No afecta a `.env.development`, que no termina en `.env`. Lo confirma el criterio de `git status`, y se comprueba con `git check-ignore` que `.env.development` sí se versiona. |
| Hoy `config` no lo usa nadie y ESLint podría marcarlo                                     | Es una exportación de módulo, no una variable local: `no-unused-vars` no la marca.                                                                                              |

## What is **not** in this spec

- CORS en el backend ni ninguna llamada a la API.
- Cliente HTTP, login, sesión ni guardado del token.
- Router, TanStack Query, formularios ni validación.
- Layout, navegación ni pantallas de módulos.
- Identidad visual definitiva (paleta propia, tipografía, logo, favicon).
- Playwright, i18n, CI, Docker ni despliegue.

Cada uno de ellos, si llega, va en su propio spec.

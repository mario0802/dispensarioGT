# SPEC 05 — Layout de la app con sidebar y avatar del usuario

> **Status:** Implemented
> **Depends on:** SPEC 01, SPEC 03
> **Date:** 2026-10-28
> **Objective:** Sustituir el inicio provisional por un layout con sidebar a la izquierda (menús según el rol, todavía solo **Inicio**) y, al pie, el avatar del usuario con sus iniciales y un menú con el tema y **Cerrar sesión**.

## Por qué este spec existe

El spec 03 dejó un inicio provisional (cabecera con el tema y **Cerrar sesión**) a la espera del "layout por rol".
Cada rol verá menús distintos, y los de recepción se trabajarán uno a uno, cada uno en su propio spec y junto con su página.
Este spec deja lista la estructura en la que esos menús se irán colgando: la ruta layout, el sidebar, la configuración de menús por rol (vacía salvo **Inicio**) y el bloque del usuario con su avatar.

## Scope

**In:**

- Componentes de shadcn/ui `sidebar` (con lo que arrastra: `sheet`, `tooltip`, `skeleton` y el hook `use-mobile`) y `avatar`, sobre `radix-ui` (sin paquetes npm nuevos).
- Variables CSS `--sidebar-*` en `src/index.css` (claro y oscuro), coherentes con `slate`.
- Feature `features/layout/`: `AppLayout` (ruta layout con `<Outlet>`), configuración de menús por rol (`nav-items.ts`), cálculo de iniciales (`initials.ts`), lectura de la cookie del sidebar y los componentes `app-sidebar`, `nav-main`, `nav-user` y `user-avatar`.
- Barra superior del área principal con el botón para plegar el sidebar y el título de la página activa.
- `HomePage` reducida al contenido de bienvenida ("Hola, {nombre}" y el rol), como ruta index del layout.
- Rutas protegidas anidadas: `RequireAuth` > `AppLayout` > `Outlet`.
- Borrar `src/components/mode-toggle.tsx` (el tema pasa al menú del avatar).
- Tests de Vitest de las iniciales, la cookie, los menús por rol, el sidebar y las rutas.
- Actualizar `frontend/CLAUDE.md`: estado, componentes de shadcn, estructura y backlog.

**Out of scope (for future specs):**

- Los menús de recepción (pacientes, citas, etc.) y sus páginas: un spec por menú.
- Los menús de `Admin` y `Doctor`.
- "Mi perfil", cambio de contraseña (`/api/users/me*`) y foto del usuario (la API no tiene).
- Dashboard o métricas en el inicio.
- Breadcrumbs y títulos de página para rutas anidadas.
- Página 403 o rutas restringidas por rol (cuando existan menús, su spec decide).
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/src/
├─ app-routes.tsx                     # rutas protegidas anidadas en AppLayout
├─ app-routes.test.tsx                # se ajusta: logout desde el menú del avatar, + casos del layout
├─ index.css                          # + variables --sidebar-* (claro y oscuro)
├─ components/
│  ├─ mode-toggle.tsx                 # se borra
│  └─ ui/
│     ├─ sidebar.tsx  sheet.tsx  tooltip.tsx  skeleton.tsx   # nuevos (shadcn)
│     └─ avatar.tsx                   # nuevo (shadcn)
├─ hooks/
│  └─ use-mobile.ts                   # nuevo (shadcn, lo usa sidebar)
└─ features/
   ├─ home/
   │  └─ home-page.tsx                # solo la bienvenida
   └─ layout/
      ├─ app-layout.tsx               # SidebarProvider + AppSidebar + SidebarInset (barra superior + Outlet)
      ├─ nav-items.ts                 # NavItem, menús por rol y getNavItems
      ├─ nav-items.test.ts
      ├─ initials.ts                  # getInitials
      ├─ initials.test.ts
      ├─ sidebar-cookie.ts            # readSidebarOpen
      ├─ sidebar-cookie.test.ts
      └─ components/
         ├─ app-sidebar.tsx           # cabecera, NavMain y NavUser
         ├─ app-sidebar.test.tsx
         ├─ nav-main.tsx              # lista de menús con el activo marcado
         ├─ nav-user.tsx              # bloque del usuario y su dropdown
         └─ user-avatar.tsx           # Avatar con las iniciales
```

`features/layout/` es una feature: en su raíz van el layout (la "página" que envuelve a las demás) y sus archivos propios; los componentes que solo usa ella, en `components/`.

### Menús por rol (`features/layout/nav-items.ts`)

```ts
import type { LucideIcon } from 'lucide-react'

export interface NavItem {
  title: string
  url: string
  icon: LucideIcon
}

// Comunes a todos los roles
export const commonNavItems: NavItem[] = [{ title: 'Inicio', url: '/', icon: House }]

// Propios de cada rol: se rellenan en el spec de cada menú
export const roleNavItems: Record<UserRole, NavItem[]> = {
  Admin: [],
  Doctor: [],
  Receptionist: [],
}

// commonNavItems seguido de los del rol
export function getNavItems(role: UserRole): NavItem[]
```

- El `Record<UserRole, …>` obliga a declarar los tres roles: un rol nuevo no compila hasta tener su entrada.
- Los menús del rol se muestran en el mismo grupo que **Inicio**, en el orden de la lista.

### Iniciales (`features/layout/initials.ts`)

`getInitials(firstName: string, lastName: string): string`

| Entrada                              | Salida |
| ------------------------------------ | ------ |
| `'Ana'`, `'Torres'`                  | `AT`   |
| `'María José'`, `'Pérez Gómez'`      | `MP`   |
| `'  ángel '`, `'ñuñez'`              | `ÁÑ`   |
| `'Ana'`, `''`                        | `A`    |
| `''`, `'Torres'`                     | `T`    |
| `''`, `'  '`                         | `?`    |

- Primera letra del nombre y primera del apellido, tras `trim`, en mayúsculas con `toLocaleUpperCase('es')` (conserva tildes y `Ñ`).
- Se toma el primer carácter completo (`Array.from`, no `[0]`), para no partir caracteres fuera del BMP.

### Cookie del sidebar (`features/layout/sidebar-cookie.ts`)

- El `SidebarProvider` de shadcn ya escribe la cookie `sidebar_state` (`true` / `false`) al plegar o desplegar; el nombre exacto se confirma con Context7 y con el `sidebar.tsx` generado en el paso 1.
- `readSidebarOpen(): boolean` lee `document.cookie`: `false` solo si `sidebar_state=false`; en cualquier otro caso (sin cookie, valor raro) `true`.
- `AppLayout` lo pasa como `defaultOpen` al `SidebarProvider` (no controlado: el provider sigue gestionando el estado).

### Layout (`features/layout/app-layout.tsx`)

```
SidebarProvider (defaultOpen = readSidebarOpen())
├─ AppSidebar (collapsible="icon")
│  ├─ SidebarHeader   → icono Stethoscope + "Dispensario Médico" (plegado: solo el icono)
│  ├─ SidebarContent  → NavMain con getNavItems(user.role)
│  ├─ SidebarFooter   → NavUser
│  └─ SidebarRail
└─ SidebarInset
   ├─ header  → SidebarTrigger + Separator vertical + título de la página activa
   └─ main    → <Outlet />
```

| Elemento                  | Detalle                                                                                                                                                                                                      |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Cabecera del sidebar      | `SidebarMenuButton` `size="lg"` con un `Link` a `/`: icono `Stethoscope` en un cuadrado con `bg-sidebar-primary` y el texto **Dispensario Médico**.                                                           |
| `NavMain`                 | Un `SidebarGroup` con `SidebarMenu`; cada ítem es un `SidebarMenuButton asChild` con un `NavLink` (icono + título) y `tooltip` con el título (visible solo plegado). `isActive` si la ruta coincide (`/` exacta; las demás, la ruta o sus hijas). |
| Activo                    | El ítem activo lleva `aria-current="page"` (lo pone `NavLink`) y el estilo `data-active` del sidebar.                                                                                                       |
| Barra superior            | `SidebarTrigger` (con `sr-only` "Mostrar u ocultar menú"; el texto exacto se ajusta al que permita el componente), separador y un `h1` con el `title` del `NavItem` activo de `getNavItems(user.role)`; si ninguno coincide, **Dispensario Médico**. |
| Móvil (< 768 px)          | El sidebar es un `Sheet` que abre el `SidebarTrigger` (comportamiento de shadcn); al elegir un menú se cierra (`setOpenMobile(false)`).                                                                     |
| Atajo                     | `Ctrl+B` / `Cmd+B` pliega y despliega (de serie en shadcn).                                                                                                                                                  |

`AppLayout` solo se monta dentro de `RequireAuth`, así que `user` siempre existe; si aun así fuera `null`, no renderiza nada.

### Bloque del usuario (`components/nav-user.tsx` y `user-avatar.tsx`)

- `UserAvatar`: `Avatar` de shadcn (`size-8`, `rounded-lg`) con un `AvatarFallback` que muestra `getInitials(firstName, lastName)`. Sin `AvatarImage` (la API no tiene foto). El avatar es decorativo (`aria-hidden`): el nombre va al lado.
- `NavUser`, en el `SidebarFooter`: `SidebarMenuButton` `size="lg"` que es el `DropdownMenuTrigger`, con `UserAvatar`, el nombre completo (`firstName lastName`) y, debajo, el rol con `roleLabels`, más un `ChevronsUpDown` a la derecha. Plegado, solo el avatar.
- Su nombre accesible es "Menú de usuario".
- Dropdown (`side="right"` en escritorio, `"bottom"` en móvil, `align="end"`):

| Ítem                        | Detalle                                                                                                                         |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Cabecera (`DropdownMenuLabel`) | `UserAvatar`, nombre completo y email.                                                                                      |
| Separador                   |                                                                                                                                 |
| **Tema** (submenú)          | `DropdownMenuSub` con icono `SunMoon` y un `DropdownMenuRadioGroup` con **Claro**, **Oscuro** y **Sistema** (`light`, `dark`, `system` de `useTheme`); el tema actual sale marcado. |
| Separador                   |                                                                                                                                 |
| **Cerrar sesión**           | Icono `LogOut`; llama a `logout()` de `useAuth` (el guard lleva a `/login`).                                                     |

### Inicio (`features/home/home-page.tsx`)

- Ya no tiene cabecera, tema ni **Cerrar sesión**: solo el contenido dentro del `main` del layout.
- Muestra "Hola, {firstName} {lastName}" y, debajo, el rol con `roleLabels`, con el padding del área principal (`p-6`).

### Rutas (`src/app-routes.tsx`)

```tsx
<Route
  element={
    <RequireAuth>
      <AppLayout />
    </RequireAuth>
  }
>
  <Route index element={<HomePage />} />
</Route>
```

`/login`, `/register` y el comodín (`*` → `/`) no cambian. Las páginas de los menús futuros se añaden como hijas de esta ruta.

### Tests

Mismas pautas que los specs 03 y 04: `fetch` simulado con `vi.stubGlobal`, `@testing-library/user-event`, `MemoryRouter`, `matchMedia` simulado con `@/test/match-media` (también lo usa `use-mobile`) y limpieza en `afterEach` de `localStorage`, `document.cookie` (`sidebar_state`) y los registros globales.

| Archivo                  | Casos                                                                                                                                                                                                                                                   |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `initials.test.ts`       | Los casos de la tabla de **Iniciales**.                                                                                                                                                                                                                 |
| `sidebar-cookie.test.ts` | Sin cookie → `true`; `sidebar_state=false` → `false`; `sidebar_state=true` → `true`; otra cookie y valor raro → `true`.                                                                                                                                 |
| `nav-items.test.ts`      | Para cada rol, `getNavItems` empieza por **Inicio** (`/`) y no tiene URLs repetidas.                                                                                                                                                                    |
| `app-sidebar.test.tsx`   | Con un `Receptionist` muestra **Dispensario Médico**, el enlace **Inicio** marcado como página actual y "AT", "Ana Torres" y "Recepcionista"; abrir "Menú de usuario" muestra el email, **Tema** y **Cerrar sesión**; elegir **Oscuro** en **Tema** cambia el tema; **Cerrar sesión** llama a `logout`. Con `Admin` y `Doctor`, mismo sidebar con su rol. |
| `app-routes.test.tsx`    | (se ajusta) El cierre de sesión pasa por "Menú de usuario" → **Cerrar sesión** y lleva a `/login`; las comprobaciones del rol se acotan al `main` (ahora sale también en el sidebar). (se añade) Con sesión, `/` muestra el sidebar, el título **Inicio** en la barra superior y "Hola, Ana Torres"; el botón de la barra superior pliega el sidebar (`data-state="collapsed"`) y escribe `sidebar_state=false`; con esa cookie, el sidebar arranca plegado. |

## Implementation plan

1. Consultar con Context7 la documentación actual de `sidebar` y `avatar` de shadcn/ui (instalación, `collapsible="icon"`, cookie, `useSidebar`, patrón `nav-user`) y de `NavLink` de React Router 7. Añadir `sidebar` y `avatar` con el CLI de shadcn, corregir los imports de `cn` a `@/lib/utils`, revisar que `package.json` no cambia y que las variables `--sidebar-*` de `index.css` (claro y oscuro) usan la paleta `slate`; no ejecutar `shadcn init`. Verificar: `pnpm build`, `pnpm lint` y `pnpm test` pasan.
2. Crear `initials.ts`, `sidebar-cookie.ts` y `nav-items.ts` con sus tests. Verificar: `pnpm test` pasa.
3. Crear `user-avatar.tsx`, `nav-user.tsx`, `nav-main.tsx` y `app-sidebar.tsx`, con `app-sidebar.test.tsx`. Verificar: `pnpm test` pasa.
4. Crear `app-layout.tsx`, anidar las rutas protegidas en `app-routes.tsx`, reducir `home-page.tsx` a la bienvenida, borrar `mode-toggle.tsx` y ajustar y ampliar `app-routes.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` pasan.
5. Probar a mano con el backend corriendo (`dotnet run`) y `pnpm dev`: entrar como recepcionista y como Admin, plegar y desplegar (botón, `Ctrl+B` y el rail), recargar con el sidebar plegado, ancho de móvil (sheet), cambiar el tema y cerrar sesión desde el avatar, en claro y oscuro.
6. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** layout con sidebar (`features/layout/`), menús por rol (solo **Inicio**) y avatar con iniciales, tema y **Cerrar sesión**; sin `ModeToggle`.
   - **Stack:** `sidebar`, `sheet`, `tooltip`, `skeleton` y `avatar` en la lista de componentes de shadcn.
   - **Estructura:** `features/layout/` y `hooks/use-mobile`; `features/home/` ya no es provisional; cómo añadir un menú (`roleNavItems` + ruta hija de `AppLayout`).
   - **Backlog:** fila 05 con este spec.

## Acceptance criteria

- [x] `package.json` no cambia (solo se añaden componentes en `src/components/ui/`, `src/hooks/use-mobile.ts` y variables en `index.css`).
- [x] Con sesión, `/` muestra a la izquierda el sidebar con **Dispensario Médico**, el menú **Inicio** marcado como activo y, al pie, el avatar con las iniciales, el nombre completo y el rol en español.
- [x] La barra superior muestra el botón de plegar y el título **Inicio**; el área principal, "Hola, {nombre} {apellido}" y el rol.
- [x] Las iniciales son la primera letra del nombre y la del apellido en mayúsculas ("María José Pérez" → "MP", "ángel ñuñez" → "ÁÑ").
- [x] Recepcionista, Admin y Doctor ven el mismo sidebar, solo con **Inicio**, cada uno con su rol.
- [x] Plegar el sidebar (botón, `Ctrl+B` o el rail) deja solo los iconos y el avatar, y los títulos salen como tooltip; al recargar, conserva el estado plegado o desplegado.
- [x] Por debajo de 768 px el sidebar no ocupa la pantalla: el botón de la barra superior lo abre como panel deslizante.
- [x] Pulsar el avatar abre un menú con el nombre y el email, **Tema** (Claro, Oscuro, Sistema, con el actual marcado) y **Cerrar sesión**.
- [x] Elegir un tema lo aplica y se conserva al recargar (como con el antiguo `ModeToggle`).
- [x] **Cerrar sesión** borra la sesión y lleva a `/login`.
- [x] `src/components/mode-toggle.tsx` ya no existe y nada lo importa.
- [x] Sin sesión, `/` sigue llevando a `/login`, y `/login` y `/register` no muestran el sidebar.
- [x] El sidebar se ve correctamente en tema claro y oscuro.
- [x] `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja la estructura nueva y tiene la fila 05 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** componente `sidebar` de shadcn. Elegido por el usuario: trae plegado, versión móvil, atajo de teclado y cookie de estado; no añade paquetes porque todo va sobre `radix-ui`.
- **No:** `<aside>` propio con Tailwind. Habría que resolver a mano el plegado y el móvil.
- **Yes:** el avatar al pie del sidebar con un dropdown (patrón `nav-user` de shadcn). Elegido por el usuario.
- **No:** avatar en la cabecera del sidebar, ni bloque solo informativo con botones sueltos.
- **Yes:** solo la estructura de menús por rol, con **Inicio** como único ítem. Elegido por el usuario: cada menú de recepción llega en su spec junto con su página, sin ítems "Próximamente".
- **Yes:** los tres roles usan el mismo layout. Elegido por el usuario: evita mantener dos inicios; sus menús llegarán en specs futuros.
- **Yes:** iniciales con la primera letra del nombre y la del apellido. Elegido por el usuario. Con los dos vacíos, `?` (la API los exige, así que es solo defensivo).
- **No:** hasta tres letras.
- **Yes:** barra superior con el botón de plegar y el título de la página activa, y bienvenida en el inicio. Elegido por el usuario. El título sale del `NavItem` activo, así cada menú futuro lo trae sin código extra.
- **Yes:** ruta layout con `<Outlet>` (`RequireAuth` > `AppLayout`). Elegido por el usuario: las páginas futuras se cuelgan como hijas sin repetir el sidebar.
- **No:** el layout dentro de `HomePage`.
- **Yes:** recordar el plegado con la cookie que ya escribe shadcn (`defaultOpen` leído al montar). Elegido por el usuario.
- **Yes:** plegado a iconos (`collapsible="icon"`). Elegido por el usuario.
- **No:** `collapsible="offcanvas"`.
- **Yes:** el tema pasa a un submenú del avatar y se borra `ModeToggle`. Elegido por el usuario: queda sin uso. "Mi perfil" y el cambio de contraseña, en specs futuros.
- **Yes:** cabecera con el icono `Stethoscope` y **Dispensario Médico**. Elegido por el usuario.
- **Yes:** todo en `features/layout/`. Elegido por el usuario: tiene lógica propia (menús por rol), y sigue la organización de las features.
- **No:** `src/components/layout/`.
- **Yes:** `roleNavItems` como `Record<UserRole, NavItem[]>`: el compilador exige una entrada por rol.

## Risks

| Risk                                                                                                     | Mitigation                                                                                                                                                         |
| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| El CLI de shadcn cambia `package.json`, sobrescribe `button`/`input`/`separator` o genera el import de `cn` incorrecto | Se revisa el diff tras el paso 1: se revierte cualquier dependencia nueva, se conservan los componentes existentes si no hay cambios relevantes y se corrige el import a `@/lib/utils`. |
| Las variables `--sidebar-*` que añade el CLI no siguen `slate`                                           | Se escriben a mano desde `colors/slate.json` si hace falta, como en el spec 01.                                                                                     |
| `use-mobile` y el `Sheet` fallan en jsdom (`matchMedia`, punteros)                                       | `@/test/match-media` y las simulaciones de punteros de `src/test/setup.ts` (spec 04); los tests corren en ancho de escritorio.                                       |
| Los tests existentes buscan "Recepcionista" o **Cerrar sesión** y ahora hay dos o están en un dropdown   | Se acotan al `main` con `within` y el cierre de sesión pasa por "Menú de usuario" (paso 4).                                                                         |
| Una cookie `sidebar_state` de otra app en `localhost` (otro puerto comparte cookies)                     | Solo afecta al valor inicial del plegado; un valor raro se trata como desplegado.                                                                                  |

## What is **not** in this spec

- Los menús de recepción y sus páginas (uno por spec).
- Los menús de `Admin` y `Doctor`.
- "Mi perfil", cambio de contraseña y foto del usuario.
- Dashboard en el inicio, breadcrumbs y rutas restringidas por rol.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

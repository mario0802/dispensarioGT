# SPEC 12 — Mi perfil

> **Status:** Approved
> **Depends on:** SPEC 02, SPEC 03, SPEC 04, SPEC 05
> **Date:** 2026-10-30
> **Objective:** Añadir la página `/profile`, a la que se entra desde el menú del avatar, donde cualquier usuario ve sus datos, cambia su nombre, apellido y teléfono y cambia su contraseña.

## Por qué este spec existe

El backend ya ofrece `PUT /api/users/me` (nombre, apellido y teléfono) y `PUT /api/users/me/password` para cualquier usuario autenticado.
Hoy el frontend no tiene cómo usarlas: un recepcionista o un administrador no puede corregir su teléfono ni cambiar su contraseña.
La petición inicial era para Admin y Recepción; se abre a los tres roles porque el endpoint es el mismo y el doctor tiene la misma necesidad.

## Scope

**In:**

- Página `/profile` (los tres roles, sin `RequireRole`) con, de arriba abajo:
  1. Encabezado con el avatar grande (iniciales; preparado para una foto futura), el nombre completo, el rol y el email.
  2. Tarjeta **Datos personales**: email, documento y rol en solo lectura, y el formulario de nombre, apellido y teléfono con **Guardar** (`PUT /api/users/me`).
  3. Tarjeta **Cambiar contraseña**: contraseña actual, nueva y confirmación con **Cambiar contraseña** (`PUT /api/users/me/password`).
- Opción **Mi perfil** en el menú del avatar, encima de **Tema**.
- Título "Mi perfil" en la barra superior para `/profile` (ruta sin `NavItem`).
- `refreshUser()` en `useAuth`: vuelve a pedir `GET /api/auth/me` sin pasar por `loading`, para que el avatar y el menú muestren el nombre nuevo.
- `UserAvatar` ampliado con `imageUrl` opcional y tamaño `sm` | `lg`, movido a `src/components/` (lo usan el layout y el perfil); `getInitials` pasa a `src/lib/`.
- `PasswordInput` (campo con el ojo) movido a `src/components/` y `passwordSchema` a `features/auth/password-schema.ts`, compartidos por el registro y el perfil.
- Tests de Vitest de lo nuevo y de lo que cambia.
- Actualizar `frontend/CLAUDE.md`: estado, estructura y backlog.

**Out of scope (for future specs):**

- Foto del usuario: subirla, guardarla y mostrarla (la API no tiene campo ni endpoint).
- Cambiar email, documento o rol (la API no lo permite a nadie).
- Gestión de usuarios del Admin (`/api/users`: alta, listado, reseteo, activar, vincular doctor).
- Aviso de cambios sin guardar al salir del perfil.
- Cerrar la sesión o las demás sesiones tras cambiar la contraseña.
- Indicador de fuerza de la contraseña.
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
└─ src/
   ├─ app-routes.tsx                        # + ruta hija profile
   ├─ components/
   │  ├─ password-input.tsx                 # movido desde register-form (antes función interna)
   │  ├─ user-avatar.tsx                    # movido desde features/layout/components, + imageUrl y size
   │  └─ user-avatar.test.tsx               # nuevo
   ├─ lib/
   │  ├─ initials.ts                        # movido desde features/layout
   │  └─ initials.test.ts                   # movido
   ├─ test/auth-context.tsx                 # + refreshUser simulado
   └─ features/
      ├─ auth/
      │  ├─ password-schema.ts              # nuevo: passwordSchema extraído de register-form
      │  ├─ use-auth.ts                     # + refreshUser en AuthContextValue
      │  ├─ auth-provider.tsx               # + refreshUser
      │  ├─ auth-provider.test.tsx          # + casos de refreshUser
      │  └─ components/register-form.tsx    # importa PasswordInput y passwordSchema
      ├─ layout/
      │  ├─ nav-items.ts                    # + getPageTitle
      │  ├─ nav-items.test.ts               # + casos de getPageTitle
      │  ├─ app-layout.tsx                  # usa getPageTitle
      │  └─ components/
      │     ├─ nav-user.tsx                 # + opción Mi perfil; import nuevo de UserAvatar
      │     └─ app-sidebar.test.tsx         # + caso de Mi perfil
      └─ profile/
         ├─ profile-types.ts
         ├─ profile-api.ts
         ├─ profile-messages.ts
         ├─ profile-schemas.ts              # Zod de datos personales y de contraseña
         ├─ profile-schemas.test.ts
         ├─ profile-page.tsx                # /profile
         ├─ profile-page.test.tsx
         └─ components/
            ├─ profile-header.tsx           # avatar grande, nombre, rol y email
            ├─ profile-details-card.tsx     # solo lectura + formulario y Guardar
            ├─ profile-details-card.test.tsx
            ├─ change-password-card.tsx     # formulario de contraseña
            └─ change-password-card.test.tsx
```

### Tipos (`profile-types.ts`)

```ts
// Cuerpo de PUT /api/users/me: phone null borra el teléfono
export interface UpdateProfileRequest {
  firstName: string
  lastName: string
  phone: string | null
}

// Cuerpo de PUT /api/users/me/password
export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}
```

### API (`profile-api.ts`)

```ts
export const updateMyProfile = (request: UpdateProfileRequest) =>
  api.put<void>('/api/users/me', request)

export const changeMyPassword = (request: ChangePasswordRequest) =>
  api.put<void>('/api/users/me/password', request)
```

### Sesión (`use-auth.ts` y `auth-provider.tsx`)

```ts
export interface AuthContextValue {
  // … lo de hoy
  // Vuelve a pedir /me sin pasar por loading; nunca rechaza
  refreshUser: () => Promise<void>
}
```

- Usa el mismo contador `requestIdRef` que el resto: una respuesta obsoleta se ignora.
- **200** → `setSession({ status: 'authenticated', user })`.
- **401** o **404** → `logout()`, como al arrancar.
- Cualquier otro error → se conserva el usuario actual sin cambiar `status` (el nombre nuevo se verá en la próxima carga).
- `TestAuthProvider` añade `refreshUser: vi.fn()`.

### Título de la barra superior (`nav-items.ts`)

```ts
// Rutas sin NavItem que tienen título propio en la barra superior
const routeTitles: { url: string; title: string }[] = [
  { url: '/profile', title: 'Mi perfil' },
]

// Título del NavItem activo; si no hay, el de routeTitles (ruta exacta); si tampoco, 'Dispensario Médico'
export function getPageTitle(role: UserRole, pathname: string): string
```

`AppLayout` sustituye su cálculo actual por `getPageTitle(user.role, pathname)`.

### Avatar (`src/components/user-avatar.tsx`)

```ts
interface UserAvatarProps {
  firstName: string
  lastName: string
  // Sin foto en la API todavía: hoy nadie lo pasa y siempre se ven las iniciales
  imageUrl?: string | null
  // sm: size-8 (sidebar, por defecto); lg: size-20 con texto text-2xl (perfil)
  size?: 'sm' | 'lg'
}
```

- Renderiza siempre `AvatarImage` con `src={imageUrl ?? undefined}` y `alt=""`, y `AvatarFallback` con `getInitials`. Radix solo pinta el `<img>` si la imagen carga; si no hay `src` o falla, se ven las iniciales.
- Conserva `aria-hidden="true"`, `rounded-lg` y los colores `bg-sidebar-primary` / `text-sidebar-primary-foreground` en los dos tamaños.
- `nav-user.tsx` lo importa de `@/components/user-avatar` sin pasar `imageUrl` ni `size`: el sidebar no cambia.

### Contraseña compartida

- `features/auth/password-schema.ts` exporta `passwordSchema` tal cual está hoy en `register-form.tsx` (mismas reglas y mensajes que `ValidPassword` del backend).
- `src/components/password-input.tsx` exporta `PasswordInput` tal cual está hoy en `register-form.tsx` (props `showLabel` y `hideLabel`).
- `register-form.tsx` los importa; su comportamiento y sus tests no cambian.

### Esquemas (`profile-schemas.ts`)

```ts
// Mismas reglas y mensajes que el registro (FullName y PhoneNumber del backend)
export const profileSchema = z.object({
  firstName: z.string().trim().min(1, { error: 'Ingresa tu nombre.', abort: true }).max(100, 'El nombre no puede superar 100 caracteres.'),
  lastName: z.string().trim().min(1, { error: 'Ingresa tu apellido.', abort: true }).max(100, 'El apellido no puede superar 100 caracteres.'),
  // Vacío viaja como null (borra el teléfono)
  phone: z.string().trim().refine(/* '' o /^[0-9+\- ]{7,20}$/ */).transform(/* '' → null */),
})

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Ingresa tu contraseña actual.'),
    newPassword: passwordSchema,
    confirmNewPassword: z.string().min(1, 'Confirma la nueva contraseña.'),
  })
  // confirmNewPassword: 'Las contraseñas no coinciden.'
  // newPassword: 'La nueva contraseña debe ser distinta de la actual.' (como NotEqual del backend)
```

- Los dos `refine` usan `path` y `when`, como el registro, para no mostrar el error de comparación mientras los campos tengan errores propios.
- Las contraseñas no llevan `trim`, como en el login y el registro.

### Página (`profile-page.tsx`)

- Lee el usuario con `useAuth()`; no hace peticiones al montar (los datos ya están en la sesión).
- Layout: una columna, `max-w-2xl`, `gap-6`, `p-6` (`p-4` en móvil).
- `ProfileHeader`: `UserAvatar` con `size="lg"`, `h2` con el nombre completo y, debajo, el rol (`roleLabels`) y el email en texto atenuado. En móvil, el avatar encima del texto.
- `ProfileDetailsCard` y `ChangePasswordCard`, cada una con su propio formulario y su propio `useApi`.

### Tarjeta **Datos personales** (`profile-details-card.tsx`)

- Bloque de solo lectura (rejilla de 2 columnas, 1 en móvil): **Email**, **Documento** ("Cédula 1712345678" con `documentTypeLabels`) y **Rol** (`roleLabels`). Debajo, el texto "El email, el documento y el rol no se pueden modificar." en atenuado.
- Formulario (React Hook Form + `profileSchema`): **Nombre**, **Apellido** y **Teléfono** (opcional, con la descripción "Opcional."). Valores iniciales desde `user`, con `phone ?? ''`.
- **Guardar** se deshabilita sin cambios (`isDirty`) y mientras guarda; muestra `Loader2` y "Guardando…".
- **204** → `toast.success('Datos actualizados.')`, `reset` con los valores guardados (tras `trim`; teléfono `null` como `''`) y `refreshUser()`.
- Error → `toast.error` con `getUpdateProfileErrorMessage`; el formulario conserva lo escrito. Un 401 ya lo resuelve el manejador global.

### Tarjeta **Cambiar contraseña** (`change-password-card.tsx`)

- **Contraseña actual**: `Input` `type="password"` sin botón del ojo (`autoComplete="current-password"`).
- **Nueva contraseña** y **Confirmar nueva contraseña**: `PasswordInput` con el ojo (`autoComplete="new-password"`; etiquetas "Mostrar contraseña"/"Ocultar contraseña" y "Mostrar confirmación"/"Ocultar confirmación").
- Bajo **Nueva contraseña**, la descripción "De 8 a 72 caracteres, con al menos una letra y un dígito.".
- **Cambiar contraseña** se deshabilita mientras envía; muestra `Loader2` y "Cambiando…".
- **204** → `toast.success('Contraseña cambiada.')` y `reset` de los tres campos a vacío. La sesión sigue abierta.
- **400 sin `validationErrors`** (la API responde `Current password is incorrect.`) → error en **Contraseña actual** con `setError`: "La contraseña actual no es correcta.". Sin toast.
- Cualquier otro error → `toast.error` con `getChangePasswordErrorMessage`; los campos conservan lo escrito.

### Mensajes (`profile-messages.ts`)

| Error                        | Datos personales (`getUpdateProfileErrorMessage`) | Contraseña (`getChangePasswordErrorMessage`)                   |
| ---------------------------- | ------------------------------------------------- | -------------------------------------------------------------- |
| `400` con `validationErrors` | Revisa los datos e inténtalo de nuevo.            | La nueva contraseña no cumple las reglas o es igual a la actual. |
| `400` sin `validationErrors` | Revisa los datos e inténtalo de nuevo.            | (no llega aquí: va al campo **Contraseña actual**)             |
| `404`                        | Tu usuario ya no existe.                          | Tu usuario ya no existe.                                       |
| `kind === 'network'`         | No se pudo conectar con el servidor.              | No se pudo conectar con el servidor.                           |
| Cualquier otro               | Ocurrió un error inesperado. Inténtalo de nuevo.  | Ocurrió un error inesperado. Inténtalo de nuevo.               |

### Menú del avatar (`nav-user.tsx`)

- Nuevo `DropdownMenuItem` **Mi perfil** (icono `UserPen` de `lucide-react`) tras el primer separador y antes de **Tema**.
- Al elegirlo navega a `/profile` (`useNavigate`) y, en móvil, cierra el `Sheet` del sidebar (`setOpenMobile(false)` de `useSidebar`).

### Ruta (`app-routes.tsx`)

- `<Route path="profile" element={<ProfilePage />} />` como hija de `AppLayout`, sin `RequireRole`.

### Tests

Mismas pautas que los specs anteriores (`fetch` simulado, `user-event`, `MemoryRouter`, `TestAuthProvider`, `<Toaster />`).

| Archivo                            | Casos |
| ---------------------------------- | ----- |
| `user-avatar.test.tsx`             | Sin `imageUrl` muestra las iniciales y no hay `<img>`; `size="lg"` aplica `size-20`. |
| `initials.test.ts`                 | (movido) Los casos de hoy, con el import nuevo. |
| `nav-items.test.ts`                | `getPageTitle`: `/patients` → "Pacientes"; `/profile` → "Mi perfil"; `/profile/x` → "Dispensario Médico"; ruta desconocida → "Dispensario Médico". |
| `auth-provider.test.tsx`           | `refreshUser` actualiza el usuario con la respuesta de `/me` sin pasar por `loading`; un 401 cierra la sesión; un error de red conserva el usuario y el estado `authenticated`. |
| `app-sidebar.test.tsx`             | El menú del avatar tiene **Mi perfil** y al elegirlo navega a `/profile`. |
| `profile-schemas.test.ts`          | Nombre vacío o de 101 → error; teléfono vacío → `null`; teléfono `abc` → error. Contraseña: actual vacía → "Ingresa tu contraseña actual."; nueva débil → mensaje de `passwordSchema`; confirmación distinta → "Las contraseñas no coinciden."; nueva igual a la actual → "La nueva contraseña debe ser distinta de la actual.". |
| `profile-details-card.test.tsx`    | Muestra email, documento y rol en solo lectura y los valores iniciales; **Guardar** deshabilitado sin cambios; guardar envía `PUT /api/users/me` con el teléfono vacío como `null`, muestra "Datos actualizados." y llama a `refreshUser`; un 400 muestra "Revisa los datos e inténtalo de nuevo." y conserva lo escrito. |
| `change-password-card.test.tsx`    | **Contraseña actual** no tiene botón del ojo y las otras dos sí; enviar hace `PUT /api/users/me/password` con `currentPassword` y `newPassword`, muestra "Contraseña cambiada." y vacía los campos; 400 sin `validationErrors` → "La contraseña actual no es correcta." en el campo, sin toast; error de red → toast "No se pudo conectar con el servidor.". |
| `profile-page.test.tsx`            | Muestra el encabezado con iniciales, nombre, rol y email, y las dos tarjetas, para un `Admin`, un `Receptionist` y un `Doctor`, sin hacer peticiones al montar. |
| `register-form.test.tsx`           | Sigue pasando sin cambios tras extraer `PasswordInput` y `passwordSchema`. |

## Implementation plan

1. Mover `initials.ts` (y su test) a `src/lib/` y `user-avatar.tsx` a `src/components/` con `imageUrl` y `size`; crear `user-avatar.test.tsx` y actualizar el import de `nav-user.tsx`. Verificar: `pnpm build` y `pnpm test` pasan y el sidebar se ve igual.
2. Extraer `passwordSchema` a `features/auth/password-schema.ts` y `PasswordInput` a `src/components/password-input.tsx`, y que `register-form.tsx` los importe. Verificar: `pnpm test` pasa (registro sin cambios).
3. Añadir `refreshUser` a `useAuth`, `AuthProvider` y `TestAuthProvider`, con sus casos en `auth-provider.test.tsx`. Verificar: `pnpm test` pasa.
4. Añadir `getPageTitle` a `nav-items.ts` (con sus tests) y usarlo en `AppLayout`. Verificar: `pnpm test` pasa y los títulos de los menús no cambian.
5. Crear `features/profile/` con tipos, API, mensajes y esquemas (con su test). Verificar: `pnpm test` pasa.
6. Crear `profile-header.tsx`, `profile-page.tsx` (con las dos tarjetas vacías de formulario) y la ruta `/profile`; añadir **Mi perfil** al menú del avatar. Verificar: `pnpm test` pasa con los casos de la página y del sidebar.
7. Crear `profile-details-card.tsx` (con su test) y ponerla en la página. Verificar: `pnpm test` pasa.
8. Crear `change-password-card.tsx` (con su test) y ponerla en la página. Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
9. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo con `Seed:SampleData:Password`) y `pnpm dev`: como `recepcion@dispensario.local`, abrir **Mi perfil** desde el avatar (título "Mi perfil"), cambiar el apellido y borrar el teléfono, guardar (toast y nombre nuevo en el avatar), recargar (siguen los datos); cambiar la contraseña con la actual incorrecta (error en el campo), con la nueva igual a la actual (error de validación) y bien (toast); cerrar sesión y entrar con la nueva; repetir el acceso como Admin y como `doctor@dispensario.local`; móvil (el `Sheet` se cierra al elegir **Mi perfil**) y tema claro/oscuro.
10. Actualizar `frontend/CLAUDE.md`:
    - **Estado actual:** la página **Mi perfil** (spec 12), `refreshUser` en la sesión y `getPageTitle`.
    - **Estructura:** `features/profile/` y sus componentes; `user-avatar` y `password-input` en `src/components/`; `initials` en `src/lib/`; `password-schema` en `features/auth/`.
    - **Sesión:** `refreshUser` en la lista de `useAuth()`.
    - **Backlog:** fila 12 con este spec.

## Acceptance criteria

- [ ] El menú del avatar tiene **Mi perfil** encima de **Tema** y lleva a `/profile` con los tres roles; en móvil, el `Sheet` del sidebar se cierra.
- [ ] En `/profile` la barra superior dice "Mi perfil" y los demás menús conservan su título.
- [ ] El encabezado muestra el avatar grande con las iniciales, el nombre completo, el rol y el email.
- [ ] `UserAvatar` sin `imageUrl` no renderiza `<img>` y muestra las iniciales; el avatar del sidebar se ve igual que antes.
- [ ] La tarjeta **Datos personales** muestra email, documento y rol en solo lectura con la nota "El email, el documento y el rol no se pueden modificar.".
- [ ] **Nombre**, **Apellido** y **Teléfono** se validan con las mismas reglas y mensajes que el registro.
- [ ] **Guardar** está deshabilitado sin cambios; al guardar hace `PUT /api/users/me` (teléfono vacío como `null`), muestra "Datos actualizados." y el avatar y el menú muestran el nombre nuevo.
- [ ] La tarjeta **Cambiar contraseña** tiene **Contraseña actual** sin botón del ojo y **Nueva contraseña** y **Confirmar nueva contraseña** con él.
- [ ] La nueva contraseña se valida con las reglas del registro, debe coincidir con la confirmación y ser distinta de la actual.
- [ ] Al cambiarla hace `PUT /api/users/me/password`, muestra "Contraseña cambiada.", vacía los campos y la sesión sigue abierta.
- [ ] Con la contraseña actual incorrecta, el campo **Contraseña actual** muestra "La contraseña actual no es correcta." sin toast.
- [ ] Los demás errores de las dos tarjetas muestran el toast de su mensaje y conservan lo escrito.
- [ ] El registro sigue funcionando igual con `PasswordInput` y `passwordSchema` extraídos.
- [ ] En móvil la página se ve en una columna sin desplazamiento horizontal; bien en tema claro y oscuro.
- [ ] `pnpm build`, `pnpm lint`, el chequeo de Prettier y `pnpm test` terminan sin errores.
- [ ] `frontend/CLAUDE.md` refleja el estado, la sesión y la estructura nuevos y tiene la fila 12 en el backlog.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** datos personales y contraseña en la misma página. Elegido por el usuario: las dos rutas existen y son del mismo tema.
- **Yes:** los tres roles, sin `RequireRole`. Elegido por el usuario: la API acepta a cualquier autenticado y el doctor también lo necesita.
- **No:** solo Admin y Recepción (la petición literal).
- **Yes:** entrada por **Mi perfil** en el menú del avatar. Elegido por el usuario: es donde se busca y no añade un menú al sidebar.
- **No:** `NavItem` en el sidebar.
- **Yes:** título "Mi perfil" con una lista `routeTitles` de rutas sin menú. Elegido por el usuario; deja el sitio para otras rutas sin `NavItem`.
- **Yes:** `refreshUser()` que repite `GET /api/auth/me`. Elegido por el usuario: los datos quedan como los tiene el servidor.
- **No:** actualizar el usuario en memoria con lo enviado.
- **Yes:** `refreshUser` no pasa por `loading` y conserva el usuario ante un error que no sea 401/404: `loading` desmontaría las rutas y el error no debe sacar al usuario de la página.
- **Yes:** tras cambiar la contraseña la sesión sigue abierta y se vacían los campos. Elegido por el usuario: el token sigue valiendo.
- **No:** cerrar la sesión tras el cambio.
- **Yes:** email, documento y rol en solo lectura. Elegido por el usuario. La nota dice que no se pueden modificar (no "que lo hace un administrador"), porque la API no lo permite a nadie.
- **Yes:** dos tarjetas apiladas, cada una con su formulario y su botón. Elegido por el usuario.
- **No:** pestañas (exigirían el componente `tabs`).
- **Yes:** encabezado con el avatar grande, preparado para la foto con `AvatarImage` e `imageUrl` opcional. Elegido por el usuario: la foto llega en otro spec y hoy todos ven las iniciales.
- **Yes:** ampliar `UserAvatar` y moverlo a `src/components/`, con `getInitials` en `src/lib/`. Elegido por el usuario; lo usan dos features y `src/components/` no debe importar de `features/`.
- **No:** un avatar propio del perfil.
- **Yes:** ojo en **Nueva contraseña** y **Confirmar nueva contraseña**, pero no en **Contraseña actual**. Elegido por el usuario: la actual no se muestra nunca.
- **Yes:** extraer `PasswordInput` a `src/components/` y `passwordSchema` a `features/auth/`, para no duplicar las reglas de contraseña del registro.
- **No:** extraer también los esquemas de nombre y teléfono: son tres reglas cortas; el perfil las repite con los mismos mensajes y un comentario que lo indica.
- **Yes:** la contraseña actual incorrecta (400 sin `validationErrors`) se marca en su campo y no en un toast: el usuario sabe exactamente qué corregir.
- **Yes:** módulo nuevo `features/profile/` y no `features/users/`, que queda para la gestión de usuarios del Admin.
- **No:** aviso de cambios sin guardar al salir. Elegido por el usuario: son tres campos cortos y **Guardar** se deshabilita sin cambios.
- **Yes:** la página no pide datos al montar: los de `useAuth()` vienen de `/me`, que es lo que la API devuelve del usuario.

## Risks

| Risk                                                                    | Mitigation                                                                                                      |
| ----------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| El usuario está desactivado: la API responde 401 al guardar            | El manejador global cierra la sesión y lleva a `/login`, como en el resto de la app.                            |
| `refreshUser` falla tras guardar y el avatar muestra el nombre viejo   | Los datos sí se guardaron (toast); el nombre nuevo se ve en la próxima carga. Documentado en **Decisions**.     |
| Mover `UserAvatar`, `getInitials` o `PasswordInput` rompe sus usos     | Pasos 1 y 2 aislados, con los tests del sidebar y del registro pasando sin cambios.                             |
| El navegador autocompleta la contraseña actual en la nueva             | `autoComplete="current-password"` y `"new-password"` en sus campos.                                            |

## What is **not** in this spec

- Subir o mostrar la foto del usuario.
- Cambiar email, documento o rol.
- Gestión de usuarios del Admin.
- Aviso de cambios sin guardar.
- Cerrar sesiones tras cambiar la contraseña.
- Indicador de fuerza de la contraseña.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

# SPEC 06 — Menú Doctores: listado, alta, edición y activación

> **Status:** Implemented
> **Depends on:** SPEC 02, SPEC 03, SPEC 05
> **Date:** 2026-10-28
> **Objective:** Añadir el menú **Doctores** (Admin y Recepción) con el listado paginado de doctores, su alta, edición, activación y desactivación (solo Admin) y un botón por fila que abre el modal, todavía vacío, del cronograma de disponibilidad.

## Por qué este spec existe

El spec 05 dejó el layout con los menús por rol vacíos salvo **Inicio**, a la espera de que cada menú llegue con su página.
El backend ya expone el CRUD de doctores (`/api/doctors`): este spec es el primer menú real y deja dos piezas que reutilizarán los siguientes: el guard de rutas por rol y los avisos (toasts).
El cronograma (horario semanal, `GET`/`PUT /api/doctors/{id}/schedule`) se trabajará en otro spec; aquí solo se deja el botón y el modal.

## Scope

**In:**

- Componentes de shadcn/ui `dialog`, `alert-dialog`, `table`, `badge` y `sonner`. `sonner` añade el paquete npm `sonner` (el único paquete nuevo); su `Toaster` se adapta para usar el `useTheme` propio (`@/components/theme-provider`), sin `next-themes`.
- Feature `features/doctors/`: tipos, cliente de API (`GET /api/doctors`, `GET /api/doctors/{id}`, `POST /api/doctors`, `PUT /api/doctors/{id}`, `POST /api/doctors/{id}/deactivate`, `POST /api/doctors/{id}/activate`), página `/doctors` y sus componentes.
- Listado en tabla con búsqueda (debounce de 300 ms), filtro de estado y paginación (10 por página), con estado local (no en la URL).
- Modal de alta y edición (mismo formulario); la edición carga el detalle con `GET /api/doctors/{id}`.
- Confirmación (`alert-dialog`) antes de activar y antes de desactivar.
- Modal del cronograma con título, especialidad y un aviso de "próximamente", sin llamadas a la API.
- Toasts de éxito y de error de las acciones.
- Guard `RequireRole` (`features/auth/components/require-role.tsx`): un rol no permitido va a `/`.
- Ítem **Doctores** en los menús de `Admin` y `Receptionist`, y ruta hija `/doctors` de `AppLayout`.
- Tests de Vitest de la página, el formulario, las confirmaciones, el guard, los menús y las rutas.
- Actualizar `frontend/CLAUDE.md`: estado, stack (componentes de shadcn y `sonner`), estructura y backlog.

**Out of scope (for future specs):**

- La funcionalidad del cronograma: ver y editar el horario semanal (`/api/doctors/{id}/schedule`) y los slots disponibles.
- Página de detalle de un doctor (`/doctors/:id`).
- Vincular un usuario `Doctor` con su ficha (`POST /api/users/{id}/link-doctor`) y mostrar o enlazar `userId`.
- Filtro por especialidad exacta (`specialty`) y lista de especialidades.
- Filtros y página en la URL, ordenar por columnas y elegir el tamaño de página.
- El menú **Doctores** para el rol `Doctor` (ni ver su propia ficha).
- Página 403 genérica.
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
├─ package.json                         # + sonner
└─ src/
   ├─ app-routes.tsx                    # + ruta /doctors (RequireRole Admin, Receptionist)
   ├─ app-routes.test.tsx               # + casos de /doctors
   ├─ components/ui/
   │  ├─ dialog.tsx  alert-dialog.tsx  table.tsx  badge.tsx   # nuevos (shadcn)
   │  └─ sonner.tsx                     # nuevo (shadcn, adaptado al useTheme propio)
   └─ features/
      ├─ auth/components/
      │  ├─ require-role.tsx            # nuevo: guard por rol
      │  └─ require-role.test.tsx       # nuevo
      ├─ layout/
      │  ├─ app-layout.tsx              # + <Toaster />
      │  ├─ nav-items.ts                # + Doctores en Admin y Receptionist
      │  └─ nav-items.test.ts           # + casos de Doctores
      └─ doctors/
         ├─ doctor-types.ts             # DoctorDto, DoctorRequest, DoctorListQuery, PaginatedResult
         ├─ doctors-api.ts              # getDoctors, getDoctor, createDoctor, updateDoctor, activateDoctor, deactivateDoctor
         ├─ use-debounced-value.ts      # useDebouncedValue
         ├─ use-debounced-value.test.ts
         ├─ doctors-page.tsx            # página /doctors
         ├─ doctors-page.test.tsx
         └─ components/
            ├─ doctors-toolbar.tsx      # búsqueda, filtro de estado y botón Nuevo doctor
            ├─ doctors-table.tsx        # tabla, estados de carga/vacío/error y paginación
            ├─ doctor-row-actions.tsx   # botón Ver cronograma y menú ⋯ (Editar, Activar/Desactivar)
            ├─ doctor-form.tsx          # formulario de alta/edición (React Hook Form + Zod)
            ├─ doctor-form.test.tsx
            ├─ doctor-form-dialog.tsx   # modal de alta/edición (carga el detalle al editar)
            ├─ doctor-status-dialog.tsx # confirmación de activar/desactivar
            ├─ doctor-status-dialog.test.tsx
            └─ doctor-schedule-dialog.tsx # modal del cronograma (placeholder)
```

Los nombres de los componentes de `components/` pueden ajustarse al implementar si se agrupan de otra forma, siempre dentro de `features/doctors/components/`.

### Tipos (`features/doctors/doctor-types.ts`)

```ts
// Respuesta de GET /api/doctors/{id} y cada elemento del listado
export interface DoctorDto {
  id: string
  firstName: string
  lastName: string
  licenseNumber: string
  specialty: string
  email: string
  phone: string | null
  appointmentDurationMinutes: number
  isActive: boolean
  userId: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/doctors y PUT /api/doctors/{id}
export interface DoctorRequest {
  firstName: string
  lastName: string
  licenseNumber: string
  specialty: string
  email: string
  phone: string | null
  appointmentDurationMinutes: number
}

export interface CreateDoctorResponse {
  id: string
}

// Query de GET /api/doctors (los null/undefined los omite el cliente)
export interface DoctorListQuery {
  pageIndex: number
  pageSize: number
  search?: string
  isActive?: boolean
}

export interface PaginatedResult<T> {
  pageIndex: number
  pageSize: number
  count: number
  data: T[]
}
```

`PaginatedResult` vive aquí hasta que otra feature lo use; entonces se mueve a `src/lib/api/`.

### API (`features/doctors/doctors-api.ts`)

```ts
export const getDoctors = (query: DoctorListQuery) =>
  api.get<PaginatedResult<DoctorDto>>('/api/doctors', query)
export const getDoctor = (id: string) => api.get<DoctorDto>(`/api/doctors/${id}`)
export const createDoctor = (request: DoctorRequest) =>
  api.post<CreateDoctorResponse>('/api/doctors', request)
export const updateDoctor = (id: string, request: DoctorRequest) =>
  api.put<void>(`/api/doctors/${id}`, request)
export const activateDoctor = (id: string) => api.post<void>(`/api/doctors/${id}/activate`)
export const deactivateDoctor = (id: string) => api.post<void>(`/api/doctors/${id}/deactivate`)
```

La firma exacta de `api.get` con query y de `api.post` sin cuerpo se ajusta a la del cliente del spec 02.

### Permisos y rutas

| Rol            | Menú **Doctores** | Ruta `/doctors`          | Acciones                                                        |
| -------------- | ----------------- | ------------------------ | --------------------------------------------------------------- |
| `Admin`        | Sí                | Sí                       | Ver cronograma, **Nuevo doctor**, Editar, Activar, Desactivar   |
| `Receptionist` | Sí                | Sí                       | Solo **Ver cronograma** (sin botón Nuevo ni menú ⋯)             |
| `Doctor`       | No                | No: `RequireRole` → `/`  | —                                                               |

- `NavItem` `{ title: 'Doctores', url: '/doctors', icon: BriefcaseMedical }` en `roleNavItems.Admin` y `roleNavItems.Receptionist`. El título de la barra superior sale de él (spec 05).
- `RequireRole({ allowedRoles: UserRole[], children })`: si `user.role` no está en `allowedRoles`, `<Navigate to="/" replace />`. Va dentro de `RequireAuth` (el usuario siempre existe; si fuera `null`, no renderiza nada).

```tsx
<Route element={<RequireAuth><AppLayout /></RequireAuth>}>
  <Route index element={<HomePage />} />
  <Route
    path="doctors"
    element={
      <RequireRole allowedRoles={['Admin', 'Receptionist']}>
        <DoctorsPage />
      </RequireRole>
    }
  />
</Route>
```

- La página decide las acciones con `const canManage = user.role === 'Admin'`. Si la API devolviera 403 en una acción, se muestra su toast (ver **Mensajes**).

### Página `/doctors` (`doctors-page.tsx`)

Estado local de la página: `search` (texto escrito), `status` (`'all' | 'active' | 'inactive'`, por defecto `'all'`) y `pageIndex` (desde 0). `pageSize` fijo en 10.

- La búsqueda usa `useDebouncedValue(search, 300)`; se envía recortada y, si queda vacía, se omite.
- `status` → `isActive`: `'all'` omite el parámetro, `'active'` → `true`, `'inactive'` → `false`.
- Cambiar la búsqueda (ya con debounce) o el estado vuelve a `pageIndex = 0`.
- Un `useEffect` llama a `execute({ pageIndex, pageSize: 10, search, isActive })` de `useApi(getDoctors)` cada vez que cambian; tras crear, editar, activar o desactivar se vuelve a llamar con los mismos parámetros (refresco).
- Mientras recarga se conservan las filas anteriores (`data` de `useApi`); solo la primera carga muestra el skeleton.

#### Barra de herramientas (`doctors-toolbar.tsx`)

| Elemento             | Detalle                                                                                                       |
| -------------------- | ------------------------------------------------------------------------------------------------------------- |
| Búsqueda             | `Input` con icono `Search`, placeholder "Buscar por nombre, matrícula o especialidad", `aria-label` "Buscar doctores". |
| Estado               | `Select` con **Todos**, **Activos** e **Inactivos** (`aria-label` "Filtrar por estado"), **Todos** por defecto. |
| **Nuevo doctor**     | Botón con icono `Plus`, solo para Admin; abre el modal de alta.                                               |

En móvil los tres elementos se apilan; desde `sm`, en una fila con el botón a la derecha.

#### Tabla (`doctors-table.tsx`)

| Columna        | Contenido                                                                                       |
| -------------- | ----------------------------------------------------------------------------------------------- |
| **Doctor**     | `firstName lastName` (en negrita) y debajo el email (texto atenuado).                          |
| **Matrícula**  | `licenseNumber`.                                                                                |
| **Especialidad** | `specialty`.                                                                                  |
| **Teléfono**   | `phone` o "—".                                                                                  |
| **Duración**   | "{appointmentDurationMinutes} min".                                                             |
| **Estado**     | `Badge`: **Activo** (variante por defecto) o **Inactivo** (`secondary`).                        |
| Acciones (sin título visible, `sr-only` "Acciones") | `DoctorRowActions`.                                         |

- La tabla va dentro del contenedor con scroll horizontal de shadcn (`Table`), así en móvil no rompe el layout.
- Orden: el de la API (apellido y nombre).

| Estado                       | Qué se ve                                                                                                              |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Primera carga                | 5 filas de `Skeleton`.                                                                                                 |
| Error al cargar              | `Alert` `destructive` con el mensaje de **Mensajes** y un botón **Reintentar** que repite la última consulta.          |
| Sin doctores y sin filtros   | "Todavía no hay doctores registrados." (y, para Admin, el botón **Nuevo doctor** de la barra sigue disponible).       |
| Sin resultados con filtros   | "No se encontraron doctores con esos filtros."                                                                         |
| Con datos                    | Filas y, debajo, la paginación.                                                                                        |

**Paginación** (bajo la tabla, solo si `count > 0`): texto "Página {pageIndex + 1} de {totalPages} · {count} doctores" (`totalPages = max(1, ceil(count / pageSize))`) y botones **Anterior** (deshabilitado en la primera) y **Siguiente** (deshabilitado en la última), con iconos `ChevronLeft`/`ChevronRight`.

#### Acciones de la fila (`doctor-row-actions.tsx`)

- Botón de icono `CalendarClock` con `aria-label` y tooltip "Ver cronograma de {nombre completo}": abre `DoctorScheduleDialog`. Lo ven Admin y Recepción, también con el doctor inactivo.
- Solo Admin: `DropdownMenu` con un botón de icono `MoreHorizontal` (`aria-label` "Más acciones de {nombre completo}") y los ítems:
  - **Editar** (icono `Pencil`): abre el modal de edición.
  - **Desactivar** (icono `UserX`), si está activo; **Activar** (icono `UserCheck`), si está inactivo: abre `DoctorStatusDialog`.

### Formulario (`doctor-form.tsx`)

Mismas reglas que `CreateDoctorCommandValidator` y los value objects del backend (`FullName`, `Email`, `PhoneNumber`, `Doctor`). Mensajes en español:

```ts
const doctorSchema = z.object({
  firstName: z.string().trim().min(1, 'Ingresa el nombre.').max(100, 'El nombre no puede superar 100 caracteres.'),
  lastName: z.string().trim().min(1, 'Ingresa el apellido.').max(100, 'El apellido no puede superar 100 caracteres.'),
  licenseNumber: z.string().trim().min(1, 'Ingresa la matrícula.').max(30, 'La matrícula no puede superar 30 caracteres.'),
  specialty: z.string().trim().min(1, 'Ingresa la especialidad.').max(100, 'La especialidad no puede superar 100 caracteres.'),
  email: z
    .string()
    .trim()
    .min(1, 'Ingresa el email.')
    .max(254, 'El email no puede superar 254 caracteres.')
    .pipe(z.email('Ingresa un email válido.')),
  // Opcional: vacío se envía como null (en la edición, null borra el teléfono)
  phone: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || /^[0-9+\- ]{7,20}$/.test(value),
      'El teléfono debe tener de 7 a 20 caracteres: dígitos, +, espacios o guiones.',
    )
    .transform((value) => (value === '' ? null : value)),
  // Select de 10 a 120 en pasos de 5: no admite valores inválidos
  appointmentDurationMinutes: z.number().int().min(10).max(120).multipleOf(5),
})
```

- Se muestra un solo mensaje por campo, el primero que falle, como en el registro (spec 04).
- Valores por defecto: alta, textos vacíos y duración 30; edición, los del `DoctorDto` (`phone` `null` → `''`).
- La matrícula se envía tal cual tras el `trim`: la API la guarda en mayúsculas.
- La duración siempre se envía (también en la edición).

| Elemento                          | Detalle                                                                                                       |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| **Nombre** y **Apellido**         | En la misma fila desde `sm`.                                                                                  |
| **Matrícula** y **Especialidad**  | En la misma fila desde `sm`. La matrícula con `autoComplete="off"`.                                            |
| **Email**                         | `type="email"`.                                                                                               |
| **Teléfono (opcional)**           | `type="tel"`.                                                                                                 |
| **Duración de la cita**           | `Select` con "10 min", "15 min" … "120 min" (conectado con `Controller`); debajo, `FieldDescription`: "Duración de cada cita con este doctor.". |
| Botones                           | **Cancelar** (cierra el modal) y **Guardar** (`type="submit"`). Con la petición en curso: ambos deshabilitados y **Guardar** con `Loader2` y "Guardando…". |
| Error de la API                   | 409 → `setError('licenseNumber', 'Ya existe un doctor con esa matrícula.')`; el resto, `Alert` `destructive` encima de los campos (ver **Mensajes**). Los valores se conservan. |

El formulario recibe `defaultValues` y un `onSubmit(request: DoctorRequest)` que devuelve el `ApiError` o `null`, para que el mismo componente sirva al alta y a la edición.

### Modal de alta y edición (`doctor-form-dialog.tsx`)

| Modo     | Título               | Al abrir                                                                                                                                   | Al guardar con éxito                                                     |
| -------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ |
| Alta     | **Nuevo doctor**     | Formulario vacío.                                                                                                                          | `POST /api/doctors` → cierra, toast "Doctor creado." y refresca la tabla. |
| Edición  | **Editar doctor**    | `GET /api/doctors/{id}` con skeleton de los campos mientras carga. Error → `Alert` con **Reintentar** dentro del modal; 404 → cierra, toast "El doctor ya no existe." y refresca. | `PUT /api/doctors/{id}` → cierra, toast "Doctor actualizado." y refresca. |

- Descripción del modal (`DialogDescription`): alta "Registra un doctor activo."; edición "Modifica los datos de {nombre completo}.".
- Mientras se guarda, el modal no se cierra con `Esc` ni clic fuera.
- Un 404 al guardar la edición actúa como el 404 al cargar.
- Editar también funciona con doctores inactivos (la API lo permite).

### Confirmación de activar/desactivar (`doctor-status-dialog.tsx`)

`AlertDialog` con el doctor y la acción:

| Acción       | Título                              | Descripción                                                  | Botón        | Éxito                           |
| ------------ | ----------------------------------- | ------------------------------------------------------------ | ------------ | ------------------------------- |
| Desactivar   | ¿Desactivar a {nombre completo}?    | No se le podrán agendar citas nuevas. Podrás activarlo de nuevo. | **Desactivar** (`destructive`) | toast "Doctor desactivado." |
| Activar      | ¿Activar a {nombre completo}?       | Se le podrán volver a agendar citas.                         | **Activar**  | toast "Doctor activado."        |

- **Cancelar** cierra sin llamar a la API.
- El botón de confirmar no cierra el diálogo por sí solo: llama a la API, muestra `Loader2` y deshabilita ambos botones; al terminar (éxito o error) cierra, muestra el toast y refresca la tabla.
- 400 (ya estaba en ese estado) → toast "El doctor ya estaba activo." / "El doctor ya estaba inactivo." y refresco; 404 → "El doctor ya no existe." y refresco; el resto, según **Mensajes**.

### Modal del cronograma (`doctor-schedule-dialog.tsx`)

- `Dialog` con título "Cronograma de {nombre completo}", descripción `specialty` y el cuerpo: icono `CalendarClock` y el texto "El cronograma de disponibilidad estará disponible próximamente.".
- Botón **Cerrar**. Sin llamadas a la API: la funcionalidad llega en otro spec, que reemplazará este cuerpo.

### Toasts (`sonner`)

- `<Toaster />` (de `@/components/ui/sonner`) se monta una vez en `AppLayout`, así solo existe en el área con sesión; `richColors` y `position="top-right"`.
- `sonner.tsx` de shadcn importa `useTheme` de `next-themes`: se cambia por `useTheme` de `@/components/theme-provider`, pasando `theme` (`light` | `dark` | `system`) al `Toaster`. No se instala `next-themes`.
- Éxito con `toast.success(...)`; errores de acciones (activar/desactivar, 404 del modal) con `toast.error(...)`. Los errores del formulario no van en toast: van dentro del modal.

### Mensajes de error

| Error                           | Mensaje                                                                                                     |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `status === 409` (alta/edición) | En el campo matrícula: "Ya existe un doctor con esa matrícula."                                             |
| `status === 400` (alta)         | Revisa los datos e inténtalo de nuevo.                                                                      |
| `status === 400` (edición)      | Revisa los datos. La duración de la cita no puede ser mayor que un bloque del horario del doctor.          |
| `status === 403`                | No tienes permiso para realizar esta acción.                                                                |
| `status === 404`                | El doctor ya no existe.                                                                                      |
| `kind === 'network'`            | No se pudo conectar con el servidor.                                                                        |
| Cualquier otro (500, …)         | Ocurrió un error inesperado. Inténtalo de nuevo.                                                            |
| Error al cargar el listado      | No se pudieron cargar los doctores. (más el mensaje de red o inesperado según el caso)                     |

El 401 lo sigue gestionando el manejador global (cierra la sesión).

### `useDebouncedValue` (`features/doctors/use-debounced-value.ts`)

`useDebouncedValue<T>(value: T, delayMs: number): T`: devuelve `value` cuando lleva `delayMs` sin cambiar (temporizador que se reinicia en cada cambio y se limpia al desmontar). Vive en la feature hasta que otra lo use; entonces pasa a `src/hooks/`.

### Tests

Mismas pautas que los specs 03–05: `fetch` simulado con `vi.stubGlobal`, `@testing-library/user-event`, `MemoryRouter`, `matchMedia` simulado, las simulaciones de punteros de `src/test/setup.ts` y limpieza en `afterEach`. Para el debounce, temporizadores falsos de Vitest (`vi.useFakeTimers` con `advanceTimers` en `userEvent.setup`) o esperas con `findBy*`; la opción exacta se decide al implementar.

| Archivo                         | Casos                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `use-debounced-value.test.ts`   | Devuelve el valor inicial; un cambio solo se refleja tras el retraso; cambios seguidos reinician el temporizador.                                                                                                                                                                                                                                                                                                                       |
| `require-role.test.tsx`         | Rol permitido → renderiza los hijos; rol no permitido → redirige a `/`.                                                                                                                                                                                                                                                                                                                                                                 |
| `nav-items.test.ts`             | (se añade) `Admin` y `Receptionist` tienen **Doctores** (`/doctors`); `Doctor` no.                                                                                                                                                                                                                                                                                                                                                      |
| `doctor-form.test.tsx`          | Enviar vacío → mensajes de nombre, apellido, matrícula, especialidad y email, sin `onSubmit`; email inválido; teléfono inválido; datos válidos → `onSubmit` con los textos recortados, `phone: null` si está vacío y la duración elegida en el `Select`; valores por defecto de edición; 409 → mensaje bajo la matrícula; 400 y error de red → `Alert`; botones deshabilitados con "Guardando…".                                              |
| `doctor-status-dialog.test.tsx` | Desactivar: título y descripción; **Cancelar** no llama a la API; confirmar → `POST …/deactivate`, toast de éxito y `onDone`; 400 → "El doctor ya estaba inactivo."; activar → `POST …/activate`.                                                                                                                                                                                                                                       |
| `doctors-page.test.tsx`         | Primera carga → `GET /api/doctors?pageIndex=0&pageSize=10` y filas con nombre, matrícula, especialidad, teléfono ("—" si falta), duración y estado; escribir en la búsqueda → una sola petición con `search` tras el debounce y `pageIndex=0`; **Inactivos** → `isActive=false`; **Siguiente**/**Anterior** cambian `pageIndex` y se deshabilitan en los extremos; vacío sin filtros y sin resultados con filtros; error → **Reintentar** repite la consulta; Admin ve **Nuevo doctor** y el menú ⋯, Recepción no (pero sí **Ver cronograma**); alta completa (`POST` y refresco con toast "Doctor creado."); edición carga `GET /api/doctors/{id}`, rellena el formulario y guarda con `PUT`; **Ver cronograma** abre el modal con el nombre y el aviso. |
| `app-routes.test.tsx`           | (se añade) Admin y Recepción ven **Doctores** en el sidebar y, al pulsarlo, la página con el título **Doctores**; un `Doctor` en `/doctors` termina en `/`.                                                                                                                                                                                                                                                                              |

## Implementation plan

1. Consultar con Context7 la documentación actual de `dialog`, `alert-dialog`, `table`, `badge` y `sonner` de shadcn/ui (y de la librería `sonner`: `Toaster`, `toast`, `richColors`, tema). Añadirlos con el CLI de shadcn, corregir los imports de `cn` a `@/lib/utils`, adaptar `sonner.tsx` al `useTheme` propio y revisar que `package.json` solo añade `sonner` (revertir `next-themes` u otros si aparecen). Montar `<Toaster />` en `AppLayout`. Verificar: `pnpm build`, `pnpm lint` y `pnpm test` pasan.
2. Crear `doctor-types.ts`, `doctors-api.ts` y `use-debounced-value.ts` con su test. Verificar: `pnpm test` pasa.
3. Crear `require-role.tsx` con su test, añadir **Doctores** a `nav-items.ts` (y su test) y la ruta `/doctors` con una `DoctorsPage` mínima (solo el listado sin acciones). Verificar: `pnpm build` y `pnpm test` pasan.
4. Completar el listado: `doctors-toolbar.tsx`, `doctors-table.tsx` (estados y paginación), búsqueda con debounce y filtro de estado, y `doctor-row-actions.tsx` con el modal del cronograma. Verificar: `pnpm test` pasa con los casos de listado de `doctors-page.test.tsx`.
5. Crear `doctor-form.tsx` y `doctor-form-dialog.tsx` (alta y edición) con sus tests. Verificar: `pnpm test` pasa.
6. Crear `doctor-status-dialog.tsx` con su test y conectarlo al menú ⋯. Completar `doctors-page.test.tsx` y `app-routes.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` pasan.
7. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo) y `pnpm dev`: como Admin, buscar, filtrar, paginar (crear doctores hasta pasar de 10), crear, crear con matrícula repetida (409), editar, editar con duración mayor que un bloque del horario (400), desactivar y activar, y abrir el cronograma; como recepcionista, ver el listado sin acciones de escritura; como doctor, que no hay menú y `/doctors` lleva al inicio; ancho de móvil; tema claro y oscuro (también los toasts).
8. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** menú **Doctores** (spec 06) para Admin y Recepción, guard `RequireRole` y toasts con `sonner`.
   - **Stack:** `dialog`, `alert-dialog`, `table`, `badge` y `sonner` en la lista de componentes de shadcn; `sonner` como dependencia y su adaptación al `useTheme` propio.
   - **Estructura:** `features/doctors/`, `require-role` en `features/auth/components/`, el `Toaster` en `AppLayout` y cómo restringir una ruta por rol.
   - **Backlog:** fila 06 con este spec.

## Acceptance criteria

- [x] `package.json` solo añade `sonner` (sin `next-themes`); los componentes nuevos están en `src/components/ui/`.
- [x] Admin y Recepción ven el menú **Doctores** en el sidebar; un `Doctor` no lo ve y, si entra a `/doctors`, termina en `/`.
- [x] `/doctors` muestra el título **Doctores** en la barra superior y la tabla con Doctor (nombre y email), Matrícula, Especialidad, Teléfono ("—" si no tiene), Duración ("30 min") y Estado (**Activo**/**Inactivo**).
- [x] La primera carga hace `GET /api/doctors?pageIndex=0&pageSize=10` y muestra un skeleton mientras tanto.
- [x] Escribir en la búsqueda hace una sola petición con `search` 300 ms después de dejar de escribir y vuelve a la primera página.
- [x] El filtro **Activos**/**Inactivos** envía `isActive=true`/`false`; **Todos** no envía `isActive`.
- [x] La paginación muestra "Página X de Y · N doctores", y **Anterior**/**Siguiente** cambian de página y se deshabilitan en los extremos.
- [x] Sin doctores se ve "Todavía no hay doctores registrados."; sin resultados con filtros, "No se encontraron doctores con esos filtros."; un error de carga muestra un aviso con **Reintentar**.
- [x] Recepción no ve **Nuevo doctor** ni el menú ⋯ de las filas; Admin sí.
- [x] Cada fila tiene **Ver cronograma**, que abre un modal con "Cronograma de {nombre}", la especialidad y "El cronograma de disponibilidad estará disponible próximamente.", sin llamadas a la API.
- [x] **Nuevo doctor** abre un modal; enviarlo vacío o con email, teléfono o longitudes inválidas muestra los mensajes sin hacer peticiones.
- [x] Con datos válidos se hace `POST /api/doctors` con los textos recortados, `phone: null` si está vacío y la duración elegida (30 por defecto); al terminar se cierra el modal, sale el toast "Doctor creado." y la tabla se refresca.
- [x] Una matrícula repetida (409) muestra "Ya existe un doctor con esa matrícula." bajo el campo y conserva los valores.
- [x] **Editar** pide `GET /api/doctors/{id}`, rellena el formulario y guarda con `PUT /api/doctors/{id}`; toast "Doctor actualizado." y refresco. Un 400 al editar muestra el aviso de la duración.
- [x] **Desactivar** y **Activar** piden confirmación; **Cancelar** no hace nada; confirmar llama a `POST …/deactivate` o `…/activate`, muestra el toast y actualiza el estado en la tabla.
- [x] Un 404 en editar, activar o desactivar muestra "El doctor ya no existe." y refresca la tabla.
- [x] Sin conexión, las acciones muestran "No se pudo conectar con el servidor.".
- [x] La página se usa en móvil (la tabla se desplaza en horizontal) y se ve bien en tema claro y oscuro, incluidos los toasts.
- [x] `pnpm build`, `pnpm lint`, `pnpm format:check` y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja la estructura nueva y tiene la fila 06 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** menú para Admin (todas las acciones) y Recepción (solo lectura y cronograma). Elegido por el usuario: la API deja leer a cualquier autenticado y escribir solo al Admin.
- **No:** menú solo para Admin, ni para todos los roles.
- **Yes:** incluir el alta (`POST /api/doctors`), aunque no estaba en la lista inicial de endpoints. Elegido por el usuario: "agregar" la necesita.
- **Yes:** alta y edición en un modal (`dialog`) con el mismo formulario. Elegido por el usuario: el listado sigue detrás y se refresca al guardar.
- **No:** páginas propias (`/doctors/new`, `/doctors/:id/edit`) ni panel lateral.
- **Yes:** tabla de shadcn con `badge` para el estado. Elegido por el usuario.
- **No:** tarjetas.
- **Yes:** búsqueda (debounce 300 ms) y filtro de estado. Elegido por el usuario: la búsqueda ya cubre la especialidad.
- **No:** filtro de especialidad exacta (sin endpoint de especialidades, sería texto libre).
- **Yes:** filtros y página en estado local. Elegido por el usuario: al recargar se vuelve a la primera página sin filtros.
- **No:** guardarlos en la URL.
- **Yes:** confirmación antes de activar y antes de desactivar. Elegido por el usuario.
- **Yes:** toasts con `sonner` (el único paquete nuevo). Elegido por el usuario; los errores del formulario siguen dentro del modal. Se adapta al `useTheme` propio en lugar de instalar `next-themes`.
- **No:** solo avisos en `Alert`, sin toasts.
- **Yes:** botón **Ver cronograma** visible en la fila y, para Admin, un menú ⋯ con Editar y Activar/Desactivar. Elegido por el usuario.
- **No:** botones de icono sueltos para todas las acciones.
- **Yes:** el modal del cronograma con título, especialidad y aviso de "próximamente", sin API. Elegido por el usuario: la funcionalidad va en otro spec.
- **Yes:** la edición carga el detalle con `GET /api/doctors/{id}`. Elegido por el usuario: datos frescos y usa el endpoint de detalle.
- **No:** rellenar con los datos de la fila.
- **Yes:** el 409 bajo el campo matrícula. Elegido por el usuario: es el único 409 posible, así que no depende del texto de la API.
- **Yes:** guard `RequireRole` que lleva a `/` a un rol no permitido. Elegido por el usuario; lo reutilizarán los próximos menús.
- **No:** pantalla "Sin permiso", ni dejar al `Doctor` ver el listado.
- **Yes:** duración con un `Select` de 10 a 120 en pasos de 5 (30 por defecto). Elegido por el usuario: no hay valores inválidos que validar.
- **No:** input numérico.
- **Yes:** el 400 de la edición con un mensaje que menciona la duración: es la única regla del backend que Zod no puede comprobar (depende del horario guardado); se evita depender del `detail` en inglés.
- **Yes:** los nombres se muestran sin "Dr."/"Dra.": la API no guarda el género.
- **Yes:** `PaginatedResult` y `useDebouncedValue` dentro de la feature hasta que otra los necesite, siguiendo la regla de compartidos.
- **Yes:** página de tamaño fijo 10 (el valor por defecto de la API).

## Risks

| Risk                                                                                                     | Mitigation                                                                                                                                                         |
| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| El CLI de shadcn añade `next-themes` con `sonner`, cambia otras dependencias o sobrescribe componentes existentes (`button`, `dropdown-menu`) | Se revisa el diff tras el paso 1: solo queda `sonner` en `package.json`, se conservan los componentes existentes y se corrige el import de `cn`.                    |
| pnpm 11 rechaza una versión de `sonner` publicada hace menos de un día (`minimumReleaseAge`)              | Se elige la versión anterior, sin crear `pnpm-workspace.yaml` (regla del `frontend/CLAUDE.md`).                                                                    |
| El `AlertDialogAction` cierra el diálogo antes de que termine la petición                                 | Se usa un `Button` propio (o `preventDefault`) y se cierra a mano al terminar.                                                                                     |
| Respuestas del listado que llegan desordenadas al escribir rápido                                        | `useApi` solo aplica la última llamada (spec 02) y el debounce reduce las peticiones.                                                                               |
| Los tests del debounce son frágiles con temporizadores falsos y `user-event`                             | `userEvent.setup({ advanceTimers: vi.advanceTimersByTime })` o esperas con `findBy*`; se decide en el paso 4.                                                      |
| Dialog, AlertDialog, Select y DropdownMenu de Radix en jsdom (punteros, portales, foco)                  | Las simulaciones de `src/test/setup.ts` (spec 04); se consultan por rol (`getByRole('dialog')`, `alertdialog`).                                                    |
| Un doctor desactivado desaparece de la página actual con el filtro **Activos** y la página queda vacía     | El refresco usa los mismos parámetros; si la página queda vacía y no es la primera, se retrocede una página.                                                        |
| La regla de "duración ≤ bloque del horario" solo la conoce el backend                                    | Mensaje específico para el 400 de la edición.                                                                                                                       |

## What is **not** in this spec

- La funcionalidad del cronograma (horario semanal) y los slots disponibles.
- Página de detalle del doctor y vínculo con su usuario.
- Filtro por especialidad, filtros en la URL, ordenación y tamaño de página variable.
- El menú para el rol `Doctor` y una página 403 genérica.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

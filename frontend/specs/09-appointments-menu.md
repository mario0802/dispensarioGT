# SPEC 09 — Menú Citas: listado y agendamiento

> **Status:** Implemented
> **Depends on:** SPEC 02, SPEC 03, SPEC 05, SPEC 06, SPEC 08
> **Date:** 2026-10-29
> **Objective:** Añadir el menú **Citas** (Admin y Recepción) con el listado paginado y filtrable de citas y un modal para agendar una nueva eligiendo paciente, doctor, fecha y uno de los slots libres del doctor.

## Por qué este spec existe

El backend ya expone el agendamiento (`POST /api/appointments`), el listado con filtros (`GET /api/appointments`) y los slots libres de un doctor (`GET /api/doctors/{id}/available-slots`).
Con doctores, horarios y pacientes ya gestionados desde el frontend (specs 06, 07 y 08), la agenda de citas es el siguiente paso de la recepción.
El listado sigue el patrón de **Doctores** y **Pacientes** (tabla paginada, filtros en estado local, modal de alta), y añade el primer selector con búsqueda (combobox) para elegir paciente y doctor.

## Scope

**In:**

- Componentes de shadcn/ui `popover` y `command` (este añade la dependencia npm `cmdk`; `popover` usa `radix-ui`, ya instalado).
- Feature `features/appointments/`: tipos, cliente de API (`GET /api/appointments`, `POST /api/appointments`, `GET /api/doctors/{id}/available-slots`), etiquetas de estado, formato de fechas y horas locales, mensajes, página `/appointments` y sus componentes.
- Listado en tabla, 10 por página, ordenado por inicio (el de la API), con filtros en estado local: **Desde** (por defecto, hoy), **Hasta**, **Estado**, **Doctor** y **Paciente**, más **Limpiar filtros**.
- Combobox con búsqueda en la API (debounce de 300 ms, solo activos) para el doctor y el paciente, usado en los filtros y en el modal.
- Botón **Nueva cita** que abre el modal de agendamiento: paciente, doctor (con especialidad y duración), fecha (`<input type="date">`, desde hoy), slots libres como botones y motivo.
- Errores del alta, con recarga de los slots ante un 409 o un 400.
- Toasts de éxito y de error.
- Ítem **Citas** en los menús de `Admin` y `Receptionist`, y ruta hija `/appointments` de `AppLayout` con `RequireRole`.
- Tests de Vitest de las funciones, el combobox, el formulario, la página, los menús y las rutas.
- Actualizar `frontend/CLAUDE.md`: estado, stack (componentes y `cmdk`), estructura y backlog.

**Out of scope (for future specs):**

- Acciones del ciclo de vida: confirmar, cancelar, marcar no asistida y reprogramar (`POST …/confirm`, `…/cancel`, `…/no-show`, `PUT …/schedule`).
- Detalle de la cita (`GET /api/appointments/{id}`) y acciones por fila.
- El menú **Citas** o la agenda del rol `Doctor` (solo sus citas), y el inicio de consultas desde una cita.
- Incluir doctores o pacientes inactivos en los combobox (ni en filtros ni al agendar).
- Agendar desde el listado de Pacientes o desde el cronograma de un doctor.
- Ver varios días de slots a la vez, calendario o vista de agenda semanal.
- Filtros y página en la URL, ordenar por columnas y elegir el tamaño de página.
- Date picker con calendario (`react-day-picker`).
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
├─ package.json                           # + cmdk (lo añade el CLI de shadcn con command)
└─ src/
   ├─ app-routes.tsx                      # + ruta /appointments (RequireRole Admin, Receptionist)
   ├─ app-routes.test.tsx                 # + casos de /appointments
   ├─ components/ui/
   │  ├─ popover.tsx                      # nuevo (shadcn)
   │  └─ command.tsx                      # nuevo (shadcn, sobre cmdk)
   └─ features/
      ├─ layout/
      │  ├─ nav-items.ts                  # + Citas en Admin y Receptionist
      │  └─ nav-items.test.ts             # + casos de Citas
      └─ appointments/
         ├─ appointment-types.ts          # AppointmentDto, AppointmentStatus, CreateAppointmentRequest, AppointmentListQuery, AvailableSlotsDto
         ├─ appointments-api.ts           # getAppointments, createAppointment, getAvailableSlots
         ├─ appointment-labels.ts         # statusLabels, statusBadgeClasses, formatLocalDate, formatLocalTimeRange, toLocalDateString
         ├─ appointment-labels.test.ts
         ├─ appointment-messages.ts       # mensajes de error propios
         ├─ appointments-page.tsx         # página /appointments
         ├─ appointments-page.test.tsx
         └─ components/
            ├─ appointments-filters.tsx   # Desde, Hasta, Estado, Doctor, Paciente, Limpiar filtros y Nueva cita
            ├─ appointments-table.tsx     # tabla, estados de carga/vacío/error y paginación
            ├─ appointment-status-badge.tsx
            ├─ search-combobox.tsx        # combobox genérico con búsqueda en la API (Popover + Command)
            ├─ search-combobox.test.tsx
            ├─ doctor-combobox.tsx        # SearchCombobox sobre getDoctors (activos)
            ├─ patient-combobox.tsx       # SearchCombobox sobre getPatients (activos)
            ├─ available-slots-picker.tsx # carga y muestra los slots libres como botones
            ├─ appointment-form.tsx       # formulario de agendamiento (React Hook Form + Zod)
            ├─ appointment-form.test.tsx
            └─ appointment-form-dialog.tsx # modal Nueva cita
```

Los nombres de los componentes de `components/` pueden ajustarse al implementar si se agrupan de otra forma, siempre dentro de `features/appointments/components/`. Los combobox solo los usa esta feature; si otra los necesita, se moverán a `src/components/` en su spec.

### Tipos (`features/appointments/appointment-types.ts`)

```ts
export type AppointmentStatus =
  | 'Scheduled'
  | 'Confirmed'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow'

// Respuesta de GET /api/appointments/{id} y cada elemento del listado
export interface AppointmentDto {
  id: string
  patientId: string
  patientFullName: string
  doctorId: string
  doctorFullName: string
  startUtc: string
  endUtc: string
  startLocal: string // "yyyy-MM-ddTHH:mm:ss", hora local del dispensario sin offset
  endLocal: string
  reason: string
  status: AppointmentStatus
  cancellationReason: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/appointments
export interface CreateAppointmentRequest {
  patientId: string
  doctorId: string
  startLocal: string // "yyyy-MM-ddTHH:mm", sin offset
  reason: string
}

export interface CreateAppointmentResponse {
  id: string
}

// Query de GET /api/appointments (los null/undefined los omite el cliente)
export interface AppointmentListQuery {
  pageIndex: number
  pageSize: number
  doctorId?: string
  patientId?: string
  status?: AppointmentStatus
  from?: string // "yyyy-MM-dd", fecha local, inclusive
  to?: string
}

export interface AvailableSlotDto {
  startLocal: string // "yyyy-MM-ddTHH:mm:ss"
  endLocal: string
}

// Respuesta de GET /api/doctors/{id}/available-slots?date=
export interface AvailableSlotsDto {
  doctorId: string
  date: string // "yyyy-MM-dd"
  durationMinutes: number
  slots: AvailableSlotDto[]
}
```

### API (`features/appointments/appointments-api.ts`)

```ts
export const getAppointments = (query: AppointmentListQuery) =>
  api.get<PaginatedResult<AppointmentDto>>('/api/appointments', { ...query })

export const createAppointment = (request: CreateAppointmentRequest) =>
  api.post<CreateAppointmentResponse>('/api/appointments', request)

// La ruta cuelga de /api/doctors pero pertenece al módulo de citas
export const getAvailableSlots = (doctorId: string, date: string) =>
  api.get<AvailableSlotsDto>(`/api/doctors/${doctorId}/available-slots`, { date })
```

Los combobox reutilizan `getDoctors` (`features/doctors/doctors-api.ts`) y `getPatients` (`features/patients/patients-api.ts`) sin cambiarlos.

### Etiquetas y formato (`features/appointments/appointment-labels.ts`)

- `statusLabels: Record<AppointmentStatus, string>`: `Scheduled` → "Agendada", `Confirmed` → "Confirmada", `Completed` → "Completada", `Cancelled` → "Cancelada", `NoShow` → "No asistió".
- `appointmentStatuses: AppointmentStatus[]`: los 5 en ese orden (para el filtro).
- `statusBadgeClasses: Record<AppointmentStatus, string>`: clases Tailwind del `Badge`, legibles en claro y oscuro:

| Estado       | Aspecto                                                  |
| ------------ | -------------------------------------------------------- |
| `Scheduled`  | Variante `outline` (sin color propio).                    |
| `Confirmed`  | Azul (`bg-blue-100 text-blue-800`, y su par `dark:`).     |
| `Completed`  | Verde (`bg-green-100 text-green-800`, y su par `dark:`).  |
| `Cancelled`  | Variante `destructive`.                                   |
| `NoShow`     | Variante `secondary`.                                     |

- `formatLocalDate(local: string): string`: `"2026-11-03T09:30:00"` → `"03/11/2026"`.
- `formatLocalTime(local: string): string`: `"2026-11-03T09:30:00"` → `"09:30"`.
- `formatLocalTimeRange(startLocal, endLocal): string`: `"09:30 – 10:00"`.
- `toStartLocalRequest(local: string): string`: `"2026-11-03T09:30:00"` → `"2026-11-03T09:30"` (lo que se envía en el `POST`).
- `toLocalDateString(date: Date): string`: `"yyyy-MM-dd"` con la fecha **local** del navegador (para "hoy" y el `min` del input).

Todas parten la cadena sin crear un `Date` desde ella (evita el desfase de zona horaria, como `formatBirthDate` en el spec 08).

### Permisos y rutas

| Rol            | Menú **Citas** | Ruta `/appointments`    | Acciones                    |
| -------------- | -------------- | ----------------------- | --------------------------- |
| `Admin`        | Sí             | Sí                      | Ver listado y **Nueva cita** |
| `Receptionist` | Sí             | Sí                      | Ver listado y **Nueva cita** |
| `Doctor`       | No             | No: `RequireRole` → `/` | —                           |

- `NavItem` `{ title: 'Citas', url: '/appointments', icon: CalendarDays }` en `roleNavItems.Admin` y `roleNavItems.Receptionist`, después de **Pacientes**.
- Ruta hija de `AppLayout`, como `/patients`, envuelta en `RequireRole allowedRoles={['Admin', 'Receptionist']}`.
- Los dos roles pueden agendar (política `Reception`): la página no oculta acciones por rol. Un 403 de la API se muestra con su mensaje (ver **Mensajes**).

### Combobox con búsqueda (`search-combobox.tsx`)

Componente genérico sobre `Popover` + `Command` de shadcn (patrón *Combobox* de su documentación), con el filtrado en el servidor (`Command shouldFilter={false}`).

```ts
interface SearchComboboxOption {
  id: string
  label: string        // texto del botón y de la opción
  description?: string // segunda línea atenuada en la opción
}

interface SearchComboboxProps {
  value: SearchComboboxOption | null
  onChange: (option: SearchComboboxOption | null) => void
  search: (text: string) => Promise<SearchComboboxOption[]> // llama a la API y mapea; el combobox la ejecuta con useApi
  placeholder: string    // "Selecciona un paciente"
  searchPlaceholder: string // "Buscar por nombre o documento"
  emptyText: string      // "No se encontraron pacientes activos."
  ariaLabel: string      // "Paciente"
  clearable?: boolean    // filtros: muestra el ítem "Todos" que deja el valor en null
  clearLabel?: string    // "Todos los pacientes"
  disabled?: boolean
  invalid?: boolean      // aria-invalid para el formulario
}
```

- El disparador es un `Button` `outline` a todo el ancho, `role="combobox"`, `aria-expanded`, `aria-label`, con el `label` elegido o el `placeholder` atenuado, e icono `ChevronsUpDown`.
- Al abrirse pide la primera página **sin** texto; al escribir, pide de nuevo 300 ms después de dejar de escribir (`useDebouncedValue`), con el texto recortado. Al cerrarse, el texto de búsqueda se limpia.
- Estados dentro de la lista: cargando → "Buscando…" con `Loader2`; error → "No se pudo buscar. Inténtalo de nuevo." (sin reintento automático; reabrir o escribir vuelve a pedir); sin resultados → `emptyText`; con datos → hasta 10 opciones (`label` y `description`), con `Check` en la elegida.
- Elegir una opción llama a `onChange` y cierra. Con `clearable`, el primer ítem es `clearLabel`, que llama a `onChange(null)`.
- Solo cuenta la última búsqueda (lo garantiza `useApi`).

Envolturas:

| Componente          | Petición                                                      | `label`                 | `description`                                    |
| ------------------- | ------------------------------------------------------------- | ----------------------- | ------------------------------------------------ |
| `DoctorCombobox`    | `getDoctors({ pageIndex: 0, pageSize: 10, search, isActive: true })` | "{nombre} {apellido}"   | "{especialidad} · {licenseNumber}"               |
| `PatientCombobox`   | `getPatients({ pageIndex: 0, pageSize: 10, search, isActive: true })` | `getPatientFullName`    | `formatDocument` ("Cédula 0102030405")           |

`DoctorCombobox` devuelve además el `DoctorDto` elegido (`onDoctorChange?: (doctor: DoctorDto | null) => void`) para que el formulario muestre la especialidad y la duración. Textos:

| Componente        | `placeholder`             | `searchPlaceholder`                 | `emptyText`                            | `clearLabel`            |
| ----------------- | ------------------------- | ----------------------------------- | -------------------------------------- | ----------------------- |
| `DoctorCombobox`  | Selecciona un doctor      | Buscar por nombre o especialidad    | No se encontraron doctores activos.    | Todos los doctores      |
| `PatientCombobox` | Selecciona un paciente    | Buscar por nombre o documento       | No se encontraron pacientes activos.   | Todos los pacientes     |

### Página `/appointments` (`appointments-page.tsx`)

- Estado local: `from` (por defecto `toLocalDateString(new Date())`), `to` (vacío), `status` (`'all'` o un `AppointmentStatus`, por defecto `'all'`), `doctor` y `patient` (`SearchComboboxOption | null`) y `pageIndex` (desde 0). `pageSize` fijo en 10.
- La query: `from`/`to` vacíos se omiten; `status: 'all'` lo omite; `doctorId`/`patientId` con el `id` elegido o se omiten.
- Si `from` y `to` tienen valor y `from > to`, no se hace la petición y bajo **Hasta** se muestra "La fecha final no puede ser anterior a la inicial."; la tabla conserva las filas anteriores.
- Cambiar cualquier filtro vuelve a `pageIndex = 0`.
- **Limpiar filtros** vuelve a los valores por defecto (`from` = hoy, el resto vacío o `'all'`); se deshabilita si ya están por defecto.
- Tras agendar se repite la consulta con los mismos parámetros.
- Mientras recarga se conservan las filas; solo la primera carga muestra el skeleton.

#### Filtros (`appointments-filters.tsx`)

| Elemento         | Detalle                                                                                                        |
| ---------------- | -------------------------------------------------------------------------------------------------------------- |
| **Desde**        | `<Input type="date">` con `Label`; por defecto hoy. Se puede vaciar (o elegir una fecha anterior) para ver citas pasadas. |
| **Hasta**        | `<Input type="date">` con `Label`, vacío por defecto (sin límite).                                             |
| **Estado**       | `Select` con **Todos** y los 5 estados (`statusLabels`), `aria-label` "Filtrar por estado".                     |
| **Doctor**       | `DoctorCombobox` con `clearable` y `ariaLabel` "Filtrar por doctor".                                           |
| **Paciente**     | `PatientCombobox` con `clearable` y `ariaLabel` "Filtrar por paciente".                                         |
| **Limpiar filtros** | Botón `ghost` con icono `X`.                                                                                |
| **Nueva cita**   | Botón con icono `Plus`; abre el modal de agendamiento.                                                         |

En móvil los filtros se apilan; desde `md`, en una cuadrícula (fechas y estado en una fila, doctor y paciente en otra) con **Nueva cita** arriba a la derecha.

#### Tabla (`appointments-table.tsx`)

| Columna         | Contenido                                                                                       |
| --------------- | ----------------------------------------------------------------------------------------------- |
| **Fecha y hora**| `formatLocalDate(startLocal)` (en negrita) y debajo `formatLocalTimeRange(startLocal, endLocal)` (atenuado). |
| **Paciente**    | `patientFullName`.                                                                              |
| **Doctor**      | `doctorFullName`.                                                                               |
| **Motivo**      | `reason` en una línea truncada (`truncate` con ancho máximo) y el texto completo en `title`.    |
| **Estado**      | `AppointmentStatusBadge` (`statusLabels` y `statusBadgeClasses`).                               |

- Sin columna de acciones (fuera de alcance). Contenedor con scroll horizontal de shadcn (`Table`) para móvil.
- Orden: el de la API (por inicio, ascendente).

| Estado                                | Qué se ve                                                                                                 |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Primera carga                         | 5 filas de `Skeleton`.                                                                                    |
| Error al cargar                       | `Alert` `destructive` "No se pudieron cargar las citas." con el detalle de **Mensajes** y **Reintentar**. |
| Sin citas con los filtros por defecto | "No hay citas próximas."                                                                                  |
| Sin citas con otros filtros           | "No se encontraron citas con esos filtros."                                                               |
| Con datos                             | Filas y, debajo, la paginación.                                                                           |

**Paginación** (solo si `count > 0`): "Página {pageIndex + 1} de {totalPages} · {count} citas" ("1 cita" en singular) y botones **Anterior**/**Siguiente** (`ChevronLeft`/`ChevronRight`), deshabilitados en los extremos.

### Formulario de agendamiento (`appointment-form.tsx`)

```
Paciente   [Selecciona un paciente        ⇕]
Doctor     [Selecciona un doctor          ⇕]
           Pediatría · Citas de 30 min
Fecha      [03/11/2026]
Horario    [08:00] [08:30] [09:00] [10:30] …
Motivo     [                                ]  0/500
                              [Cancelar] [Agendar cita]
```

Esquema orientativo (Zod 4):

```ts
const appointmentSchema = z.object({
  patientId: z.string().min(1, 'Selecciona un paciente.'),
  doctorId: z.string().min(1, 'Selecciona un doctor.'),
  date: z.string().min(1, 'Selecciona la fecha.'), // + refine: no anterior a hoy → 'La fecha no puede ser anterior a hoy.'
  startLocal: z.string().min(1, 'Selecciona un horario.'), // slot.startLocal
  reason: z
    .string()
    .trim()
    .min(1, 'Ingresa el motivo de la cita.')
    .max(500, 'El motivo no puede superar 500 caracteres.'),
})
```

| Campo        | Detalle                                                                                                                                                                  |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Paciente** | `PatientCombobox` (solo activos, sin `clearable`), `ariaLabel` "Paciente".                                                                                              |
| **Doctor**   | `DoctorCombobox` (solo activos, sin `clearable`), `ariaLabel` "Doctor". Con un doctor elegido, `FieldDescription` "{especialidad} · Citas de {duración} min".           |
| **Fecha**    | `<Input type="date">` con `min` = hoy local; por defecto hoy.                                                                                                            |
| **Horario**  | `AvailableSlotsPicker` (ver abajo). El error de Zod se muestra bajo los slots.                                                                                          |
| **Motivo**   | `Textarea` (3 filas) con contador "{n}/500".                                                                                                                            |
| Botones      | **Cancelar** y **Agendar cita** (`type="submit"`). Con la petición en curso: ambos deshabilitados y **Agendar cita** con `Loader2` y "Agendando…".                       |
| Error de la API | `Alert` `destructive` encima de los campos (ver **Mensajes**); los valores se conservan salvo el slot cuando se recargan los slots.                                   |

- Cambiar el doctor o la fecha deja `startLocal` vacío y vuelve a pedir los slots.
- Al enviar: `{ patientId, doctorId, startLocal: toStartLocalRequest(startLocal), reason: reason.trim() }`.
- Recibe `onSubmit(request: CreateAppointmentRequest)` que devuelve el `ApiError` o `null`, y `onCancel`.

### Slots libres (`available-slots-picker.tsx`)

- Recibe `doctorId`, `date`, `value` (el `startLocal` elegido), `onChange` y una señal de recarga (`reloadKey`) que el formulario incrementa tras un 409 o un 400 del alta.
- Pide `getAvailableSlots(doctorId, date)` cuando hay doctor y fecha (y la fecha no es anterior a hoy); vuelve a pedir al cambiar cualquiera de los dos o `reloadKey`.

| Estado                      | Qué se ve                                                                                                    |
| --------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Sin doctor o sin fecha      | "Selecciona un doctor y una fecha para ver los horarios libres." (atenuado).                                 |
| Cargando                    | Fila de 6 `Skeleton` del tamaño de un botón.                                                                 |
| Error                       | `Alert` `destructive` con el mensaje de **Mensajes de los slots** y **Reintentar**.                          |
| Sin slots                   | "El doctor no tiene horarios libres ese día." (atenuado).                                                    |
| Con slots                   | Rejilla de botones (`flex-wrap`) con `formatLocalTime(startLocal)`; el elegido con variante por defecto y `aria-pressed="true"`, el resto `outline`. `aria-label` "{hora} a {hora fin}". |

- Si el slot elegido ya no está en la respuesta recargada, `onChange('')`.

### Modal Nueva cita (`appointment-form-dialog.tsx`)

| Título         | Descripción                                                               | Al guardar con éxito                                                      |
| -------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| **Nueva cita** | "Elige el paciente, el doctor y uno de sus horarios libres."             | `POST /api/appointments` → cierra, toast "Cita agendada." y refresca la tabla. |

- Ancho `sm:max-w-2xl`, cuerpo con scroll vertical y título y botones siempre visibles (como el de Pacientes).
- Mientras se guarda, no se cierra con `Esc` ni clic fuera. Cada apertura empieza con el formulario vacío (y la fecha de hoy).
- Los popover de los combobox se abren por encima del modal y la búsqueda funciona dentro de él.

### Mensajes de error (`appointment-messages.ts`)

**Alta de la cita:**

| Error                | Mensaje                                                                                              | Además                 |
| -------------------- | ---------------------------------------------------------------------------------------------------- | ---------------------- |
| `status === 409`     | Ese horario ya no está disponible: el doctor o el paciente ya tienen una cita que se cruza. Elige otro. | Recarga los slots.     |
| `status === 400`     | No se pudo agendar: revisa que el paciente y el doctor sigan activos y que el horario siga libre.    | Recarga los slots.     |
| `status === 404`     | El paciente o el doctor ya no existe.                                                                |                        |
| `status === 403`     | No tienes permiso para realizar esta acción.                                                         |                        |
| `kind === 'network'` | No se pudo conectar con el servidor.                                                                 |                        |
| Cualquier otro       | Ocurrió un error inesperado. Inténtalo de nuevo.                                                     |                        |

**Slots libres:**

| Error                | Mensaje                                                              |
| -------------------- | -------------------------------------------------------------------- |
| `status === 400`     | No se pueden consultar los horarios: el doctor está inactivo o la fecha ya pasó. |
| `status === 404`     | El doctor ya no existe.                                              |
| `kind === 'network'` | No se pudo conectar con el servidor.                                 |
| Cualquier otro       | Ocurrió un error inesperado. Inténtalo de nuevo.                     |

**Listado:** red → "No se pudo conectar con el servidor."; 403 → "No tienes permiso para realizar esta acción."; el resto (incluido un 400 inesperado de filtros), "Ocurrió un error inesperado. Inténtalo de nuevo.".

El 401 lo sigue gestionando el manejador global.

### Tests

Mismas pautas que los specs 06 y 08 (`fetch` simulado con `vi.stubGlobal`, `user-event`, `MemoryRouter`, `TestAuthProvider`, `TooltipProvider`, `<Toaster />`, simulaciones de `src/test/setup.ts` y limpieza en `afterEach`). Para "hoy", `vi.setSystemTime` (con `shouldAdvanceTime` para no bloquear el debounce) o un `today` inyectado. Si `cmdk` necesita algo más de jsdom (p. ej. `scrollIntoView` en sus ítems), se simula en `src/test/setup.ts` solo si no existe.

| Archivo                         | Casos                                                                                                                                                                                                                                                                                                       |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `appointment-labels.test.ts`    | `statusLabels` de los 5 estados; `formatLocalDate`, `formatLocalTime`, `formatLocalTimeRange`, `toStartLocalRequest` y `toLocalDateString` (incluida una hora cerca de medianoche sin desfase).                                                                                                              |
| `search-combobox.test.tsx`      | Abrir pide la primera página sin texto y lista las opciones; escribir pide con el texto 300 ms después (una sola petición); elegir llama a `onChange` y cierra mostrando el `label`; sin resultados → `emptyText`; error → mensaje; con `clearable`, el ítem "Todos…" llama a `onChange(null)`.                |
| `appointment-form.test.tsx`     | Enviar vacío → mensajes de paciente, doctor, horario y motivo sin `onSubmit`; elegir doctor muestra "{especialidad} · Citas de {n} min" y, con la fecha, pide `GET /api/doctors/{id}/available-slots?date=…` y muestra los botones; día sin slots → aviso; elegir un slot lo marca (`aria-pressed`); cambiar la fecha o el doctor quita el slot y vuelve a pedir; error de slots → **Reintentar**; datos válidos → `onSubmit` con `startLocal` "yyyy-MM-ddTHH:mm" y motivo recortado; motivo de más de 500; 409 → `Alert` y nueva petición de slots; red → `Alert`; "Agendando…". |
| `appointments-page.test.tsx`    | Primera carga → `GET /api/appointments?pageIndex=0&pageSize=10&from={hoy}` y filas con fecha, franja, paciente, doctor, motivo y estado en español; vaciar **Desde** quita `from`; **Hasta** anterior a **Desde** → mensaje y sin petición; filtro de estado → `status=Confirmed`; filtro de doctor y de paciente → `doctorId`/`patientId` y `pageIndex=0`; **Limpiar filtros**; paginación; "No hay citas próximas." y "No se encontraron citas con esos filtros."; error → **Reintentar**; alta completa (paciente, doctor, fecha, slot, motivo → `POST`, toast "Cita agendada." y refresco). |
| `nav-items.test.ts`             | (se añade) `Admin` y `Receptionist` tienen **Citas** (`/appointments`) después de **Pacientes**; `Doctor` no.                                                                                                                                                                                               |
| `app-routes.test.tsx`           | (se añade) Admin y Recepción ven **Citas** y, al pulsarlo, la página con el título **Citas**; un `Doctor` en `/appointments` termina en `/`.                                                                                                                                                               |

## Implementation plan

1. Consultar con Context7 la documentación actual de `popover`, `command` y el patrón *Combobox* de shadcn/ui, y de `cmdk` (`shouldFilter`, `onValueChange`). Añadir `popover` y `command` con el CLI (corregir el import de `cn` a `@/lib/utils`; no sobrescribir componentes existentes como `dialog.tsx` o `button.tsx`; elegir una versión de `cmdk` publicada hace más de un día). Revisar que `package.json` solo añade `cmdk`. Verificar: `pnpm build` y `pnpm test` pasan.
2. Crear `appointment-types.ts`, `appointments-api.ts`, `appointment-messages.ts` y `appointment-labels.ts` con su test. Verificar: `pnpm test` pasa.
3. Añadir **Citas** a `nav-items.ts` (y su test) y la ruta `/appointments` con una `AppointmentsPage` mínima (solo la tabla con `from` = hoy). Verificar: `pnpm build` y `pnpm test` pasan.
4. Crear `search-combobox.tsx` con su test, y `doctor-combobox.tsx` y `patient-combobox.tsx`. Verificar: `pnpm test` pasa.
5. Completar el listado: `appointments-filters.tsx` (fechas con su validación, estado, doctor, paciente, **Limpiar filtros**), `appointments-table.tsx` (estados y paginación) y `appointment-status-badge.tsx`. Verificar: `pnpm test` pasa con los casos de listado de `appointments-page.test.tsx`.
6. Crear `available-slots-picker.tsx` y `appointment-form.tsx` con su test. Verificar: `pnpm test` pasa.
7. Crear `appointment-form-dialog.tsx` y conectarlo a **Nueva cita** (toast y refresco). Completar `appointments-page.test.tsx` y `app-routes.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
8. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo) y `pnpm dev`: como Admin y como recepcionista, agendar con `MG-0001` en un día con horario (slots según su duración), comprobar que el slot agendado desaparece al volver a abrir el modal, agendar al mismo paciente a la misma hora con otro doctor (409 y recarga de slots), elegir un día sin horario (aviso), filtrar por fechas (incluidas pasadas), estado, doctor y paciente, limpiar filtros y paginar; como doctor, que no hay menú y `/appointments` lleva al inicio; ancho de móvil (modal con scroll y combobox utilizables); tema claro y oscuro (colores de los estados).
9. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** menú **Citas** (spec 09) para Admin y Recepción: listado con filtros y modal de agendamiento con combobox y slots libres.
   - **Stack:** `popover` y `command` en la lista de componentes de shadcn, y `cmdk` como dependencia de `command`.
   - **Estructura:** `features/appointments/` y sus componentes.
   - **Backlog:** fila 09 con este spec.

## Acceptance criteria

- [x] `popover.tsx` y `command.tsx` están en `src/components/ui/` con el import de `cn` desde `@/lib/utils`; `package.json` solo añade `cmdk`.
- [x] Admin y Recepción ven el menú **Citas** en el sidebar, después de **Pacientes**; un `Doctor` no lo ve y, si entra a `/appointments`, termina en `/`.
- [x] `/appointments` muestra el título **Citas** y la primera carga hace `GET /api/appointments?pageIndex=0&pageSize=10&from={hoy}` con un skeleton mientras tanto.
- [x] La tabla muestra Fecha y hora ("03/11/2026" y "09:30 – 10:00"), Paciente, Doctor, Motivo truncado (completo al pasar el ratón) y Estado con su etiqueta en español y su color.
- [x] Vaciar **Desde** o elegir una fecha anterior permite ver citas pasadas; **Hasta** anterior a **Desde** muestra "La fecha final no puede ser anterior a la inicial." y no hace la petición.
- [x] Los filtros de estado, doctor y paciente envían `status`, `doctorId` y `patientId`, y vuelven a la primera página; los combobox buscan en la API 300 ms después de dejar de escribir y solo ofrecen activos.
- [x] **Limpiar filtros** vuelve a `from` = hoy sin el resto de filtros.
- [x] La paginación muestra "Página X de Y · N citas" y **Anterior**/**Siguiente** se deshabilitan en los extremos.
- [x] Sin citas se ve "No hay citas próximas." (filtros por defecto) o "No se encontraron citas con esos filtros."; un error de carga muestra un aviso con **Reintentar**.
- [x] **Nueva cita** abre el modal; enviarlo vacío muestra los mensajes de paciente, doctor, horario y motivo sin hacer el `POST`.
- [x] Al elegir un doctor se ve "{especialidad} · Citas de {n} min"; con doctor y fecha se pide `GET /api/doctors/{id}/available-slots?date=…` y los slots libres salen como botones; un día sin slots muestra "El doctor no tiene horarios libres ese día.".
- [x] La fecha no admite días anteriores a hoy; cambiar el doctor o la fecha quita el slot elegido y recarga los slots.
- [x] Con datos válidos se hace `POST /api/appointments` con `startLocal` "yyyy-MM-ddTHH:mm" sin offset y el motivo recortado; se cierra el modal, sale el toast "Cita agendada." y la tabla se refresca.
- [x] Un 409 o un 400 al agendar muestra su aviso, conserva paciente, doctor, fecha y motivo, quita el slot y recarga los slots.
- [x] Sin conexión, el listado, los slots y el alta muestran "No se pudo conectar con el servidor.".
- [x] En móvil la tabla se desplaza en horizontal, los filtros se apilan y el modal tiene scroll con los botones visibles; se ve bien en tema claro y oscuro.
- [x] `pnpm build`, `pnpm lint`, el chequeo de Prettier y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja el stack y la estructura nuevos y tiene la fila 09 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** menú para Admin y Recepción. Elegido por el usuario: son los que pueden agendar (política `Reception`).
- **No:** agenda del `Doctor` (solo sus citas) en este spec; irá junto a las consultas.
- **Yes:** solo listar y agendar. Elegido por el usuario.
- **No:** confirmar, cancelar, no asistida y reprogramar; van en un spec siguiente con acciones por fila.
- **Yes:** filtros de rango de fechas, estado, doctor y paciente. Elegido por el usuario.
- **Yes:** **Desde** = hoy por defecto, pero editable y vaciable para ver citas anteriores. Elegido por el usuario: la API ordena por inicio ascendente y, sin `from`, saldrían primero las más antiguas.
- **Yes:** combobox con búsqueda en la API (shadcn `Popover` + `Command`), aunque añade `cmdk`. Elegido por el usuario: hay muchos pacientes y un `Select` no escala.
- **No:** `Select` simple para doctores ni una lista hecha a mano sin dependencias.
- **Yes:** solo activos en los combobox, también en los filtros. Elegido por el usuario; filtrar citas de un doctor o paciente inactivo queda fuera.
- **Yes:** agendar en un modal desde el listado. Elegido por el usuario, igual que Doctores y Pacientes.
- **No:** página propia `/appointments/new`.
- **Yes:** `<input type="date">` nativo y los slots libres como botones. Elegido por el usuario: un clic por hora y sin dependencias de calendario.
- **No:** `Select` de slots ni varios días de slots a la vez.
- **Yes:** columnas Fecha y hora, Paciente, Doctor, Motivo y Estado. Elegido por el usuario.
- **Yes:** `Badge` con color por estado (clases Tailwind con su par oscuro). Elegido por el usuario.
- **Yes:** especialidad y duración bajo el doctor elegido. Elegido por el usuario.
- **Yes:** ante un 409 (y un 400) al agendar, aviso y recarga de los slots conservando el resto. Elegido por el usuario: el slot pudo ocuparse o pasar entre la consulta y el alta.
- **Yes:** "hoy" con la fecha local del navegador; el backend usa la del dispensario (`America/Guayaquil`). Si difieren, la API responde 400 y se muestra el aviso; en el uso real coinciden.
- **Yes:** el `startLocal` se toma del slot devuelto por la API (sin construirlo a mano) y se envía recortado a minutos, como en el README.
- **Yes:** `SearchCombobox` genérico dentro de `features/appointments/components/`; se moverá a `src/components/` cuando lo use otra feature.
- **Yes:** página de tamaño fijo 10 y filtros en estado local, como en Doctores y Pacientes.

## Risks

| Risk                                                                                        | Mitigation                                                                                                                                 |
| ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| El CLI de shadcn añade dependencias o sobrescribe componentes al añadir `command` (que usa `dialog`) | Responder que no a sobrescribir; revisar el diff tras el paso 1: solo `cmdk` en `package.json` y `dialog.tsx`/`button.tsx` intactos. |
| `cmdk` filtra en cliente por defecto y ocultaría resultados de la API                        | `Command shouldFilter={false}`; caso en `search-combobox.test.tsx` con un resultado que no contiene el texto escrito.                       |
| Popover dentro del `Dialog` de Radix: foco o scroll bloqueados                               | Se sigue el patrón de shadcn (Popover en portal) y se prueba a mano en el paso 8, también en móvil.                                       |
| `cmdk` usa APIs que jsdom no tiene                                                           | Simularlas en `src/test/setup.ts` solo si no existen, como las de Radix.                                                                  |
| Desfase de zona al formatear horas locales con `Date`                                        | Las funciones de `appointment-labels.ts` parten la cadena; casos cerca de medianoche en su test.                                           |
| La zona del navegador difiere de la del dispensario (`min` de la fecha, "hoy")               | La API valida la fecha y responde 400 con su aviso; decisión documentada.                                                                 |
| Un slot se ocupa entre la consulta y el alta                                                 | 409 → aviso y recarga de slots.                                                                                                            |
| pnpm 11 no instala versiones de `cmdk` publicadas hace menos de un día                       | Elegir la versión anterior en vez de crear `pnpm-workspace.yaml` (regla de `frontend/CLAUDE.md`).                                          |

## What is **not** in this spec

- Confirmar, cancelar, marcar no asistida y reprogramar citas.
- Detalle de la cita y acciones por fila.
- El menú o la agenda del rol `Doctor` y las consultas.
- Doctores o pacientes inactivos en los combobox.
- Agendar desde Pacientes o desde el cronograma.
- Calendario, vista semanal o varios días de slots.
- Filtros en la URL, ordenación, tamaño de página variable y date picker con calendario.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

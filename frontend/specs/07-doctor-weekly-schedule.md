# SPEC 07 — Horario semanal del doctor: ver y editar

> **Status:** Implemented
> **Depends on:** SPEC 02, SPEC 03, SPEC 05, SPEC 06
> **Date:** 2026-10-28
> **Objective:** Sustituir el modal provisional del cronograma por el horario semanal del doctor en siete filas (lunes a domingo), visible para Admin y Recepción y editable en horas completas por el Admin y, desde un menú nuevo **Mi horario**, por el propio doctor.

## Por qué este spec existe

El spec 06 dejó en cada fila de **Doctores** un botón **Ver cronograma** que abre un modal con un aviso de "próximamente".
El backend ya expone el horario semanal (`GET`/`PUT /api/doctors/{id}/schedule`), que es la base de la agenda: sin bloques no hay slots ni citas.
Este spec da contenido a ese modal y le da al rol `Doctor` su primer menú, **Mi horario**, para mantener su propio horario.

## Scope

**In:**

- Componente de shadcn/ui `switch` (sobre `radix-ui`, sin paquetes npm nuevos).
- Tipos y cliente de API del horario: `GET /api/doctors/{id}/schedule` y `PUT /api/doctors/{id}/schedule`.
- Funciones puras del horario: días de la semana con su nombre en español, opciones de hora, agrupación por día, conversión al cuerpo del `PUT` y esquema de validación (Zod).
- Panel del horario reutilizable: lectura (7 filas) y edición (switch por día, varios bloques por día, horas completas), con carga, error, guardado y descarte de cambios.
- El modal **Cronograma de {nombre}** de `/doctors` usa el panel: Admin ve y edita; Recepción solo ve.
- Menú **Mi horario** (`/my-schedule`) solo para el rol `Doctor`: su ficha y su horario, editable.
- Tests de Vitest de las funciones, el formulario, el modal, la página nueva, los menús y las rutas; se ajusta el test del modal provisional del spec 06.
- Actualizar `frontend/CLAUDE.md`: estado, componentes de shadcn, estructura y backlog.

**Out of scope (for future specs):**

- Copiar el horario de un día a otros (atajos como "Copiar a lun–vie").
- Slots disponibles (`GET /api/doctors/{id}/available-slots`) y la agenda de citas.
- Bloques con minutos distintos de `:00` creados desde la interfaz (la API los admite en múltiplos de 5; aquí solo se crean horas completas).
- Avisar al salir de **Mi horario** por la navegación del sidebar con cambios sin guardar (solo se confirma al cancelar).
- Que el `Doctor` vea el listado de doctores o el horario de otros doctores.
- Vincular un usuario con su ficha de doctor (`POST /api/users/{id}/link-doctor`).
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/src/
├─ app-routes.tsx                         # + ruta /my-schedule (RequireRole Doctor)
├─ app-routes.test.tsx                    # + casos de Mi horario
├─ components/ui/
│  └─ switch.tsx                          # nuevo (shadcn)
└─ features/
   ├─ layout/
   │  ├─ nav-items.ts                     # + Mi horario en Doctor
   │  └─ nav-items.test.ts                # + casos de Mi horario
   └─ doctors/
      ├─ doctor-schedule-types.ts         # DayOfWeek, ScheduleBlockDto, DoctorScheduleDto, SetDoctorScheduleRequest
      ├─ doctors-api.ts                   # + getDoctorSchedule, setDoctorSchedule
      ├─ doctor-schedule.ts               # días, horas, agrupación, conversión y esquema Zod
      ├─ doctor-schedule.test.ts
      ├─ doctor-messages.ts               # + mensajes del horario
      ├─ doctors-page.tsx                 # pasa onNotFound (refresco) al modal del cronograma
      ├─ doctors-page.test.tsx            # se ajusta el caso del cronograma
      ├─ my-schedule-page.tsx             # página /my-schedule
      ├─ my-schedule-page.test.tsx
      └─ components/
         ├─ doctor-schedule-dialog.tsx    # se reescribe: usa DoctorSchedulePanel
         ├─ doctor-schedule-dialog.test.tsx
         ├─ doctor-schedule-panel.tsx     # carga, lectura/edición, guardado, errores
         ├─ doctor-schedule-view.tsx      # lectura: 7 filas
         ├─ doctor-schedule-form.tsx      # edición (React Hook Form + Zod)
         ├─ doctor-schedule-form.test.tsx
         ├─ doctor-schedule-day-row.tsx   # fila de un día en edición (switch y bloques)
         └─ discard-changes-dialog.tsx    # confirmación "¿Descartar los cambios?"
```

Los nombres de los componentes pueden ajustarse al implementar si se agrupan de otra forma, siempre dentro de `features/doctors/components/`.

### Tipos (`features/doctors/doctor-schedule-types.ts`)

```ts
// Como el enum DayOfWeek de .NET, en texto
export type DayOfWeek =
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'
  | 'Sunday'

// Horas "HH:mm" en hora local del dispensario
export interface ScheduleBlockDto {
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

// Respuesta de GET /api/doctors/{id}/schedule
export interface DoctorScheduleDto {
  doctorId: string
  blocks: ScheduleBlockDto[]
}

// Cuerpo de PUT /api/doctors/{id}/schedule ([] borra el horario)
export interface SetDoctorScheduleRequest {
  blocks: ScheduleBlockDto[]
}
```

### API (`features/doctors/doctors-api.ts`)

```ts
export const getDoctorSchedule = (id: string) =>
  api.get<DoctorScheduleDto>(`/api/doctors/${id}/schedule`)

export const setDoctorSchedule = (id: string, request: SetDoctorScheduleRequest) =>
  api.put(`/api/doctors/${id}/schedule`, request)
```

### Funciones del horario (`features/doctors/doctor-schedule.ts`)

```ts
// Lunes a domingo, el orden en que la API devuelve los bloques
export const weekDays: { value: DayOfWeek; label: string }[] = [
  { value: 'Monday', label: 'Lunes' },
  { value: 'Tuesday', label: 'Martes' },
  { value: 'Wednesday', label: 'Miércoles' },
  { value: 'Thursday', label: 'Jueves' },
  { value: 'Friday', label: 'Viernes' },
  { value: 'Saturday', label: 'Sábado' },
  { value: 'Sunday', label: 'Domingo' },
]

// "00:00" … "23:00": un bloque no cruza la medianoche y "24:00" no es una hora válida
export const hourOptions: string[]

export function toMinutes(time: string): number          // "09:30" → 570
export function isWholeHour(time: string): boolean       // "09:00" → true, "09:30" → false

// Valores del formulario: siempre 7 días, en el orden de weekDays
export interface ScheduleDayValues {
  dayOfWeek: DayOfWeek
  enabled: boolean
  blocks: { startTime: string; endTime: string }[]
}
export interface ScheduleFormValues {
  days: ScheduleDayValues[]
}

export function toScheduleFormValues(blocks: ScheduleBlockDto[]): ScheduleFormValues
export function toScheduleRequest(values: ScheduleFormValues): SetDoctorScheduleRequest
export function createScheduleSchema(appointmentDurationMinutes: number) // z.ZodType<ScheduleFormValues>

// Bloque que propone "+ Añadir bloque", o null si no cabe antes de las 23:00
export function getNextBlock(
  blocks: { startTime: string; endTime: string }[],
  appointmentDurationMinutes: number,
): { startTime: string; endTime: string } | null
```

- `toScheduleFormValues`: un día con bloques queda `enabled: true` con sus bloques ordenados por inicio; sin bloques, `enabled: false` y `blocks: []`. Se conservan las horas tal cual (también las que no son completas).
- `toScheduleRequest`: solo los días `enabled`, en el orden de `weekDays` y, dentro del día, por hora de inicio. Si ningún día está activo, `{ blocks: [] }` (borra el horario).
- `getNextBlock`: empieza donde termina el último bloque del día (o a las `08:00` si no hay) y dura `max(1, ceil(duración / 60))` horas; si el fin pasa de `23:00`, devuelve `null`.

**Reglas del esquema** (`createScheduleSchema(duración)`, con `superRefine` para marcar el bloque concreto). Se valida solo en los días `enabled` y se muestra un solo mensaje por bloque, el primero que falle, en este orden:

| Regla | Mensaje (bajo el bloque) |
| --- | --- |
| Inicio o fin no son hora completa | Usa horas completas. |
| Fin ≤ inicio | La hora de fin debe ser posterior a la de inicio. |
| Duración del bloque < `appointmentDurationMinutes` | El bloque debe durar al menos {duración} min (la duración de cada cita). |
| Se cruza con otro bloque del mismo día (contiguos valen: `09:00–12:00` y `12:00–14:00`) | Se solapa con otro bloque del día. |

Las reglas de la API que no se repiten aquí: el múltiplo de 5 lo cubren las horas completas, y el doctor inactivo se trata antes de editar (ver **Doctor inactivo**).

### Panel del horario (`doctor-schedule-panel.tsx`)

```ts
interface DoctorSchedulePanelProps {
  doctor: DoctorDto            // nombre, appointmentDurationMinutes e isActive
  canEdit: boolean             // Admin en el modal; el Doctor en Mi horario
  onNotFound: () => void       // 404 al cargar o al guardar
  onDirtyChange?: (dirty: boolean) => void   // el modal lo usa para confirmar al cerrar
  onSavingChange?: (saving: boolean) => void // el modal no se cierra mientras guarda
}
```

| Estado | Qué se ve |
| --- | --- |
| Cargando (`GET …/schedule`) | 7 filas de `Skeleton`. |
| Error al cargar | `Alert` `destructive` "No se pudo cargar el horario." más el mensaje de red o inesperado, y **Reintentar**. 404 → `onNotFound`. |
| Lectura | `DoctorScheduleView` y, debajo, la nota "Horas locales del dispensario.". Si `canEdit` y el doctor está activo, botón **Editar horario** (icono `Pencil`). |
| Lectura, doctor inactivo y `canEdit` | Sin **Editar horario**; `Alert` "El doctor está inactivo: actívalo para editar su horario." (en **Mi horario**: "Tu ficha de doctor está inactiva: pide al administrador que la active para editar tu horario."). |
| Edición | `DoctorScheduleForm` con los bloques cargados. |
| Guardado con éxito | `PUT …/schedule` → toast "Horario actualizado.", se vuelve a pedir el `GET` y se vuelve a lectura. |

Recepción (`canEdit: false`) solo ve la lectura, sin botón ni aviso de inactivo.

### Lectura (`doctor-schedule-view.tsx`)

Lista de 7 filas, una por día de `weekDays`:

```
Lunes      08:00 – 12:00   14:00 – 18:00
Martes     09:00 – 13:00
Miércoles  No atiende
…
Domingo    No atiende
```

- Cada bloque como `Badge` `outline` con "{inicio} – {fin}"; un día sin bloques, "No atiende" en texto atenuado.
- Si no hay ningún bloque, encima de las filas: "Este doctor todavía no tiene horario." (en **Mi horario**: "Todavía no tienes horario.").
- En móvil el día va arriba y los bloques debajo; desde `sm`, en la misma fila.

### Edición (`doctor-schedule-form.tsx` y `doctor-schedule-day-row.tsx`)

React Hook Form + `zodResolver(createScheduleSchema(duración))`, con `useFieldArray` para los bloques de cada día y `defaultValues = toScheduleFormValues(blocks)`.

```
Lunes      [●]  [08:00 ▾] – [12:00 ▾]  [✕]
                [14:00 ▾] – [18:00 ▾]  [✕]
                + Añadir bloque
Miércoles  [ ]  No atiende
```

| Elemento | Detalle |
| --- | --- |
| Switch del día | `Switch` con `aria-label` "Atiende el {día}". Encenderlo crea el bloque `08:00–12:00`; apagarlo quita todos los bloques del día y muestra "No atiende". |
| Inicio y fin | Dos `Select` (con `Controller`) con `hourOptions`, `aria-label` "Inicio del bloque {n} del {día}" y "Fin del bloque {n} del {día}". Si el valor cargado no es hora completa (p. ej. `09:30`), el `Select` lo incluye como opción extra para que se vea y el bloque muestra "Usa horas completas." hasta cambiarlo. |
| Quitar bloque | Botón de icono `X`, `aria-label` "Quitar el bloque {n} del {día}". Quitar el último bloque apaga el día. |
| **+ Añadir bloque** | Botón `ghost` con icono `Plus`, solo con el día encendido; añade `getNextBlock(…)`. Deshabilitado si devuelve `null`. |
| Error de un bloque | `FieldError` bajo el bloque con el mensaje de las reglas. |
| Descripción | Encima de las filas: "Horas completas, en hora local del dispensario. Cada bloque dura al menos {duración} min.". |
| Botones | **Cancelar** y **Guardar horario** (`type="submit"`). Con la petición en curso: ambos deshabilitados y **Guardar horario** con `Loader2` y "Guardando…". |
| Error de la API | `Alert` `destructive` encima de las filas (ver **Mensajes**); los valores se conservan. |

- El formulario recibe `defaultValues`, `appointmentDurationMinutes`, `onSubmit(request: SetDoctorScheduleRequest)` (devuelve el `ApiError` o `null`), `onCancel` y `onDirtyChange`.
- **Cancelar** sin cambios (`formState.isDirty` falso) vuelve a lectura; con cambios abre `DiscardChangesDialog`.
- En móvil, cada día ocupa su propio bloque (día y switch arriba, bloques debajo).

### Descartar cambios (`discard-changes-dialog.tsx`)

`AlertDialog` con título "¿Descartar los cambios?", descripción "Los cambios del horario no se guardarán." y botones **Seguir editando** y **Descartar** (`destructive`). **Descartar** vuelve a lectura (o cierra el modal, según de dónde venga).

### Modal del cronograma (`doctor-schedule-dialog.tsx`)

- Mismo título ("Cronograma de {nombre completo}") y descripción (`specialty`) que en el spec 06; el cuerpo pasa a ser `DoctorSchedulePanel` con `canEdit = user.role === 'Admin'`. Ancho `sm:max-w-2xl` y contenido con scroll vertical si no cabe.
- Botón **Cerrar** en el pie solo en lectura (en edición mandan **Cancelar** y **Guardar horario**).
- Cerrar (X, `Esc`, clic fuera o **Cerrar**) con cambios sin guardar abre `DiscardChangesDialog`; sin cambios, cierra. Mientras guarda, no se cierra.
- `onNotFound` → cierra, toast "El doctor ya no existe." y refresca la tabla (`doctors-page.tsx` le pasa el refresco).
- Usa el `DoctorDto` de la fila (duración e `isActive`), sin pedir el detalle del doctor.

### Página **Mi horario** (`my-schedule-page.tsx`, `/my-schedule`)

- Ruta hija de `AppLayout` envuelta en `RequireRole allowedRoles={['Doctor']}`; `NavItem` `{ title: 'Mi horario', url: '/my-schedule', icon: CalendarClock }` en `roleNavItems.Doctor`. El título de la barra superior sale de él.
- `user.doctorId` (de `useAuth`) `null` → `Alert` "Tu usuario todavía no está vinculado a una ficha de doctor. Pide al administrador que lo vincule." sin llamadas a la API.
- Con `doctorId`: `GET /api/doctors/{doctorId}` (skeleton mientras carga) para el nombre, la especialidad, la duración e `isActive`; encabezado "{nombre completo} · {especialidad} · Citas de {duración} min" y, debajo, `DoctorSchedulePanel` con `canEdit: true`.
- Error al cargar la ficha → `Alert` con **Reintentar**; 404 (en la ficha o en el panel) → `Alert` "No se encontró tu ficha de doctor. Contacta con el administrador.".
- Sin confirmación al salir de la página por el sidebar (fuera de alcance).

### Doctor inactivo

La API rechaza el `PUT` de un doctor inactivo con 400. El panel no ofrece **Editar horario** si `doctor.isActive` es `false` y muestra el aviso de la tabla del panel; la lectura sigue disponible.

### Mensajes de error del guardado (`doctor-messages.ts`)

| Error | Mensaje |
| --- | --- |
| `status === 400` | Revisa el horario: algún bloque no cumple las reglas del dispensario. |
| `status === 403` (Mi horario) | No puedes editar este horario. Si tu usuario se vinculó hace poco, cierra sesión y vuelve a entrar. |
| `status === 403` (modal) | No tienes permiso para realizar esta acción. |
| `status === 404` | `onNotFound` (ver modal y página). |
| `kind === 'network'` | No se pudo conectar con el servidor. |
| Cualquier otro | Ocurrió un error inesperado. Inténtalo de nuevo. |

El 403 de **Mi horario** cubre el caso del token emitido antes del vínculo (sin el claim `doctor_id`), aunque `/api/auth/me` ya traiga el `doctorId`. El 401 lo sigue gestionando el manejador global.

### Tests

Mismas pautas que el spec 06: `fetch` simulado con `vi.stubGlobal`, `@testing-library/user-event`, `MemoryRouter`, `TestAuthProvider`, `TooltipProvider` y `<Toaster />` donde haga falta, las simulaciones de punteros de `src/test/setup.ts` y limpieza en `afterEach`.

| Archivo | Casos |
| --- | --- |
| `doctor-schedule.test.ts` | `hourOptions` va de `00:00` a `23:00` (24 opciones); `isWholeHour`; `toScheduleFormValues` agrupa y ordena, y deja apagados los días sin bloques; `toScheduleRequest` ordena lunes→domingo y por inicio, omite los días apagados y devuelve `[]` sin días; el esquema rechaza fin ≤ inicio, horas no completas, bloques más cortos que la duración (90 min → bloque de 1 h) y solapes, y acepta bloques contiguos; `getNextBlock` empieza tras el último bloque, a las `08:00` sin bloques, y devuelve `null` si no cabe. |
| `doctor-schedule-form.test.tsx` | Encender un día crea `08:00–12:00`; apagarlo quita sus bloques; **+ Añadir bloque** añade el siguiente; quitar el último bloque apaga el día; un solape o un bloque corto muestra su mensaje y no llama a `onSubmit`; una hora `09:30` cargada se ve y pide "Usa horas completas."; guardar envía el `SetDoctorScheduleRequest` esperado; 400 y error de red → `Alert`; "Guardando…" con los botones deshabilitados; **Cancelar** con cambios pide confirmación y sin cambios no. |
| `doctor-schedule-dialog.test.tsx` | Carga `GET …/schedule` y muestra las 7 filas ("No atiende" en los días sin bloques); Recepción no ve **Editar horario**; Admin edita y guarda con `PUT`, toast "Horario actualizado." y vuelve a lectura con los datos del nuevo `GET`; doctor inactivo → aviso y sin **Editar horario**; error de carga → **Reintentar**; 404 → `onNotFound`; cerrar con cambios pide confirmación. |
| `my-schedule-page.test.tsx` | Sin `doctorId` → aviso sin llamadas a la API; con `doctorId` → `GET /api/doctors/{id}`, encabezado y horario; editar y guardar con `PUT`; 403 al guardar → mensaje de volver a iniciar sesión; 404 de la ficha → aviso. |
| `doctors-page.test.tsx` | (se ajusta) **Ver cronograma** abre el modal con el nombre y pide `GET …/schedule` (en vez del aviso de "próximamente"). |
| `nav-items.test.ts` | (se añade) `Doctor` tiene **Mi horario** (`/my-schedule`); `Admin` y `Receptionist` no. |
| `app-routes.test.tsx` | (se añade) un `Doctor` ve **Mi horario** en el sidebar y la página con ese título; Admin y Recepción en `/my-schedule` terminan en `/`. |

## Implementation plan

1. Consultar con Context7 la documentación actual de `switch` de shadcn/ui y de `useFieldArray` (anidado) y `superRefine`/`path` de React Hook Form y Zod 4. Añadir `switch` con el CLI de shadcn, corregir el import de `cn` a `@/lib/utils` y comprobar que `package.json` no cambia. Verificar: `pnpm build` y `pnpm test` pasan.
2. Crear `doctor-schedule-types.ts`, añadir `getDoctorSchedule` y `setDoctorSchedule` a `doctors-api.ts`, y crear `doctor-schedule.ts` con su test. Verificar: `pnpm test` pasa.
3. Crear `doctor-schedule-view.tsx` y `doctor-schedule-panel.tsx` en modo solo lectura, y reescribir `doctor-schedule-dialog.tsx` para usarlo (con `onNotFound` desde `doctors-page.tsx`). Ajustar el caso del cronograma en `doctors-page.test.tsx`. Verificar: `pnpm test` pasa.
4. Crear `doctor-schedule-day-row.tsx`, `doctor-schedule-form.tsx` y `discard-changes-dialog.tsx` con su test, y conectar la edición al panel (guardado, toast, nuevo `GET`, doctor inactivo, confirmación al cerrar el modal). Crear `doctor-schedule-dialog.test.tsx`. Verificar: `pnpm test` pasa.
5. Crear `my-schedule-page.tsx` con su test, añadir **Mi horario** a `nav-items.ts` (y su test) y la ruta `/my-schedule` en `app-routes.tsx` (y sus casos). Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
6. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo con `Seed:SampleData:Password`) y `pnpm dev`: como Admin, ver el horario de `MG-0001` (dos bloques por día), editarlo (añadir y quitar bloques, apagar un día, provocar un solape y un bloque corto con un doctor de 90 min), guardar, cancelar con cambios y cerrar con cambios; desactivar un doctor y comprobar que no se puede editar; como recepcionista, ver el horario sin **Editar horario**; como `doctor@dispensario.local`, **Mi horario** con su ficha, editar y guardar; ancho de móvil; tema claro y oscuro.
7. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** horario semanal (spec 07) en el modal del cronograma (Admin edita, Recepción ve) y menú **Mi horario** del `Doctor`.
   - **Stack:** `switch` en la lista de componentes de shadcn.
   - **Estructura:** los archivos nuevos de `features/doctors/` y `my-schedule-page`.
   - **Contrato con la API:** el frontend solo crea bloques en horas completas.
   - **Backlog:** fila 07 con este spec.

## Acceptance criteria

- [ ] `package.json` no cambia; `switch.tsx` está en `src/components/ui/` con el import de `cn` desde `@/lib/utils`.
- [ ] **Ver cronograma** abre el modal, pide `GET /api/doctors/{id}/schedule` y muestra 7 filas de lunes a domingo con los bloques "{inicio} – {fin}" o "No atiende".
- [ ] Recepción ve el horario sin **Editar horario**; Admin ve **Editar horario** si el doctor está activo.
- [ ] Con el doctor inactivo, el Admin ve el aviso "El doctor está inactivo: actívalo para editar su horario." y no puede editar.
- [ ] En edición, cada día tiene un switch; encenderlo crea `08:00–12:00` y apagarlo quita sus bloques.
- [ ] Un día admite varios bloques con **+ Añadir bloque** y **Quitar**; quitar el último apaga el día.
- [ ] Los selectores solo ofrecen horas completas de `00:00` a `23:00`; una hora no completa que venga de la API se ve y bloquea el guardado con "Usa horas completas.".
- [ ] Fin ≤ inicio, un bloque más corto que la duración de la cita o un solape muestran su mensaje bajo el bloque y no llaman a la API; dos bloques contiguos se pueden guardar.
- [ ] **Guardar horario** hace `PUT /api/doctors/{id}/schedule` con los bloques de los días encendidos, de lunes a domingo y por hora de inicio (`blocks: []` si no hay ninguno); al terminar sale el toast "Horario actualizado." y se vuelve a lectura con el horario guardado.
- [ ] Un 400, 403 o error de red al guardar muestra su mensaje en el formulario y conserva los cambios.
- [ ] **Cancelar** o cerrar el modal con cambios pide "¿Descartar los cambios?"; sin cambios, vuelve a lectura o cierra sin preguntar. Mientras guarda, el modal no se cierra.
- [ ] Un 404 en el modal lo cierra, muestra "El doctor ya no existe." y refresca la tabla.
- [ ] Un `Doctor` ve el menú **Mi horario**; Admin y Recepción no, y en `/my-schedule` terminan en `/`.
- [ ] En **Mi horario**, un doctor sin vincular ve el aviso de vínculo sin llamadas a la API; uno vinculado ve su nombre, especialidad, duración y horario, y puede editarlo y guardarlo.
- [ ] Un 403 al guardar en **Mi horario** muestra "No puedes editar este horario. Si tu usuario se vinculó hace poco, cierra sesión y vuelve a entrar.".
- [ ] El modal y la página se usan en móvil y se ven bien en tema claro y oscuro.
- [ ] `pnpm build`, `pnpm lint`, `pnpm test` y `pnpm exec prettier --check . --end-of-line auto` terminan sin errores.
- [ ] `frontend/CLAUDE.md` refleja la estructura nueva y tiene la fila 07 en el backlog.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** 7 filas fijas (lunes a domingo) con varios bloques por día. Elegido por el usuario: la API los admite y el doctor de ejemplo `MG-0001` tiene mañana y tarde.
- **No:** un solo bloque por día (no permite la pausa del mediodía ni mostrar horarios existentes con dos bloques).
- **Yes:** solo horas completas en la interfaz, de `00:00` a `23:00`. Elegido por el usuario; el fin máximo es `23:00` porque un bloque no cruza la medianoche y `24:00` no es un `TimeOnly`.
- **No:** limitar el rango a 06:00–22:00.
- **Yes:** editan el Admin (en el modal) y el propio doctor (en **Mi horario**); Recepción solo ve. Elegido por el usuario; coincide con la política `AdminOrDoctor` del `PUT`.
- **Yes:** menú **Mi horario** (`/my-schedule`) solo para el `Doctor`. Elegido por el usuario: no se le abre `/doctors`, que sigue como en el spec 06.
- **No:** dar al `Doctor` el listado de doctores con solo su fila editable.
- **Yes:** lectura primero y **Editar horario** para pasar a edición. Elegido por el usuario: evita cambios accidentales y Recepción ve la misma lectura.
- **No:** abrir directamente en edición.
- **Yes:** validar en el cliente todas las reglas conocidas (horas completas, fin > inicio, duración mínima, solapes) con el mensaje en el bloque. Elegido por el usuario; el 400 de la API queda como respaldo con un mensaje genérico.
- **Yes:** una hora no completa que venga de la API se muestra y hay que corregirla antes de guardar. Elegido por el usuario: no se cambia nada sin que se vea.
- **No:** redondear automáticamente.
- **Yes:** doctor inactivo en solo lectura con aviso. Elegido por el usuario: la API rechazaría el `PUT` con 400.
- **Yes:** `Switch` por día (shadcn `switch`, sin paquetes nuevos). Elegido por el usuario; encenderlo crea `08:00–12:00` (4 h, válido con cualquier duración de 10 a 120 min).
- **No:** `checkbox`, ni días sin control.
- **Yes:** confirmar el descarte con cambios sin guardar al cancelar o cerrar el modal. Elegido por el usuario.
- **Yes:** tras guardar, toast y vuelta a lectura con un nuevo `GET` (el modal sigue abierto). Elegido por el usuario.
- **No:** cerrar el modal al guardar.
- **No:** atajo para copiar un día a otros. Elegido por el usuario: queda para otro spec.
- **Yes:** React Hook Form + Zod (con `useFieldArray` y un esquema que recibe la duración), siguiendo la regla de formularios del frontend, en lugar de un estado propio con `useState`.
- **Yes:** un panel compartido (`DoctorSchedulePanel`) entre el modal y **Mi horario**, dentro de `features/doctors/`: ambos son de la misma feature.
- **Yes:** el modal usa el `DoctorDto` de la fila y **Mi horario** pide `GET /api/doctors/{doctorId}`: el panel necesita la duración y el estado del doctor.
- **Yes:** "+ Añadir bloque" propone el bloque siguiente al último del día con la duración mínima en horas completas, y se deshabilita si no cabe antes de las `23:00`.

## Risks

| Risk | Mitigation |
| --- | --- |
| El token del doctor no trae `doctor_id` (se vinculó después del login) aunque `/me` devuelva `doctorId`: la lectura funciona y el `PUT` da 403 | Mensaje específico en **Mi horario** que pide cerrar sesión y volver a entrar. |
| `useFieldArray` anidado (7 días × bloques) y `superRefine` con rutas por bloque no muestran el error donde corresponde | Se consulta la documentación actual en el paso 1 y el test del formulario comprueba cada mensaje bajo su bloque. |
| El `Select` de Radix no muestra un valor que no está en sus opciones (`09:30`) | Se añade el valor cargado como opción extra en ese `Select`. |
| El `Dialog` se cierra con `Esc` o clic fuera y pierde los cambios | `onOpenChange` pasa por la comprobación de cambios (`onDirtyChange`) y por el estado de guardado. |
| Un `AlertDialog` dentro de un `Dialog` (portales y foco) en jsdom | Las simulaciones de `src/test/setup.ts`; consultas por rol (`dialog`, `alertdialog`). |
| La duración de la cita cambia en otra pestaña entre la carga y el guardado | El 400 de la API se muestra con el mensaje genérico; al volver a abrir, el modal usa los datos del listado refrescado. |
| El modal crece con muchos bloques y no cabe en pantallas bajas | Contenido con scroll vertical dentro del `DialogContent`. |

## What is **not** in this spec

- Copiar el horario entre días.
- Slots disponibles y agenda de citas.
- Bloques con minutos distintos de `:00` creados desde la interfaz.
- Aviso al navegar fuera de **Mi horario** con cambios sin guardar.
- Que el `Doctor` vea el listado de doctores o el horario de otros.
- Vincular un usuario con su ficha de doctor.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

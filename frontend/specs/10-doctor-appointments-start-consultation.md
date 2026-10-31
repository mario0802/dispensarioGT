# SPEC 10 — Citas del doctor e inicio de la consulta

> **Status:** Approved
> **Depends on:** SPEC 02, SPEC 03, SPEC 05, SPEC 09
> **Date:** 2026-10-29
> **Objective:** Abrir el menú **Citas** al rol `Doctor` para que vea sus citas e inicie la consulta de una cita de hoy, que lo lleva a una página (aún vacía) de la consulta.

## Por qué este spec existe

El backend ya permite al `Doctor` listar solo sus citas (`GET /api/appointments`) e iniciar una consulta a partir de una cita (`POST /api/consultations` con `appointmentId`).
El menú **Citas** (spec 09) solo existe para Admin y Recepción. Este spec reutiliza esa página con una variante para el doctor y añade el primer paso del flujo clínico: iniciar la consulta y llegar a su pantalla.
La pantalla de la consulta (detalle, edición y cierre) es el siguiente spec; aquí solo se llega a una página provisional.

## Scope

**In:**

- Ítem **Citas** (`/appointments`) en el menú del rol `Doctor` y la ruta `/appointments` abierta también a `Doctor`.
- Variante de la página `/appointments` para el `Doctor`: sin **Nueva cita**, sin filtro ni columna **Doctor**, con **Desde** y **Hasta** = hoy por defecto, y con la columna **Acciones** con el botón **Iniciar** en las citas iniciables.
- Doctor sin vincular (`user.doctorId === null`): aviso sin llamar a la API, como en **Mi horario**.
- Confirmación **Iniciar consulta** (al pulsar la fila iniciable o su botón) que hace `POST /api/consultations` con el paciente, la cita y el motivo de la cita, y navega a `/consultations/{id}`.
- Si la cita ya tiene consulta (409): buscarla en `GET /api/patients/{patientId}/consultations` por `appointmentId` y navegar a ella.
- Feature nueva `features/consultations/`: tipos, cliente de API (`POST /api/consultations`, `GET /api/patients/{id}/consultations`), mensajes y página provisional `/consultations/:consultationId` (solo `Doctor`).
- Tests de Vitest de lo nuevo y de lo que cambia (página, menús, rutas).
- Actualizar `frontend/CLAUDE.md`: estado, estructura y backlog.

**Out of scope (for future specs):**

- **Crear citas por el doctor.** El backend lo prohíbe (`POST /api/appointments` es `Reception`, 403 al `Doctor`); requiere primero un spec del backend.
- La pantalla de la consulta: detalle de la cita y de la consulta, edición (`PUT /api/consultations/{id}`), cierre (`POST …/close`) e historia clínica del paciente.
- Iniciar una consulta sin cita.
- Cancelar o marcar como no asistida una cita desde la vista del doctor (el backend lo permite, pero va en otro spec).
- Acceso del Admin a la página de la consulta (solo lectura en la API).
- Cambios en la vista de Admin y Recepción de `/appointments`.
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
└─ src/
   ├─ app-routes.tsx                         # /appointments también para Doctor; + /consultations/:consultationId (RequireRole Doctor)
   ├─ app-routes.test.tsx                    # + casos
   └─ features/
      ├─ layout/
      │  ├─ nav-items.ts                     # + Citas en Doctor, antes de Mi horario
      │  └─ nav-items.test.ts                # + casos
      ├─ appointments/
      │  ├─ appointment-labels.ts            # + canStartConsultation
      │  ├─ appointment-labels.test.ts       # + casos
      │  ├─ appointment-messages.ts          # + mensaje del 403 del listado para el doctor
      │  ├─ appointments-page.tsx            # variante por rol y aviso de doctor sin vincular
      │  ├─ appointments-page.test.tsx       # + casos del doctor
      │  └─ components/
      │     ├─ appointments-filters.tsx      # + showDoctorFilter, canCreate
      │     ├─ appointments-table.tsx        # + showDoctorColumn, onStart (columna Acciones y fila clicable)
      │     ├─ start-consultation-dialog.tsx # nuevo: confirmación, POST, 409 y navegación
      │     └─ start-consultation-dialog.test.tsx
      └─ consultations/                      # nueva feature
         ├─ consultation-types.ts            # ConsultationDto, ConsultationStatus, StartConsultationRequest, StartConsultationResponse
         ├─ consultations-api.ts             # startConsultation, getPatientConsultations
         ├─ consultation-messages.ts         # mensajes de error del inicio
         ├─ consultation-page.tsx            # página provisional /consultations/:consultationId
         └─ consultation-page.test.tsx
```

### Tipos (`features/consultations/consultation-types.ts`)

```ts
export type ConsultationStatus = 'Open' | 'Closed'

// Respuesta de GET /api/consultations/{id} y cada elemento de la historia del paciente
export interface ConsultationDto {
  id: string
  patientId: string
  patientFullName: string
  doctorId: string
  doctorFullName: string
  appointmentId: string | null
  reason: string
  diagnosis: string | null
  notes: string | null
  status: ConsultationStatus
  startedAtUtc: string
  closedAtUtc: string | null
  // "yyyy-MM-ddTHH:mm:ss", hora local del dispensario sin offset
  startedAtLocal: string
  closedAtLocal: string | null
  createdAtUtc: string
}

// Cuerpo de POST /api/consultations (el doctor sale del token)
export interface StartConsultationRequest {
  patientId: string
  appointmentId: string
  reason: string
}

export interface StartConsultationResponse {
  id: string
}
```

`appointmentId` es obligatorio en el request de este spec (siempre se inicia desde una cita); la consulta sin cita queda fuera.

### API (`features/consultations/consultations-api.ts`)

```ts
export const startConsultation = (request: StartConsultationRequest) =>
  api.post<StartConsultationResponse>('/api/consultations', request)

// La ruta cuelga de /api/patients pero pertenece al módulo de consultas
export const getPatientConsultations = (patientId: string, query: { pageIndex: number; pageSize: number }) =>
  api.get<PaginatedResult<ConsultationDto>>(`/api/patients/${patientId}/consultations`, { ...query })
```

### Cita iniciable (`appointment-labels.ts`)

```ts
// Iniciable: Scheduled o Confirmed y de hoy (fecha local); el backend vuelve a validarlo
export function canStartConsultation(appointment: AppointmentDto, today: string): boolean
```

- `today` es `toLocalDateString(new Date())` (fecha del navegador, como en el spec 09); se compara con `startLocal.slice(0, 10)`.

### Permisos y rutas

| Rol            | Menú **Citas** | `/appointments`             | Acciones en `/appointments`       | `/consultations/:consultationId` |
| -------------- | -------------- | --------------------------- | --------------------------------- | -------------------------------- |
| `Admin`        | Sí (sin cambios) | Sí (sin cambios)          | Listado y **Nueva cita**          | No: `RequireRole` → `/`          |
| `Receptionist` | Sí (sin cambios) | Sí (sin cambios)          | Listado y **Nueva cita**          | No: `RequireRole` → `/`          |
| `Doctor`       | Sí, antes de **Mi horario** | Sí              | Sus citas e **Iniciar**           | Sí                               |

- `roleNavItems.Doctor = [appointmentsNavItem, myScheduleNavItem]`.
- `/appointments` pasa a `RequireRole allowedRoles={['Admin', 'Receptionist', 'Doctor']}`.
- `/consultations/:consultationId`: ruta hija de `AppLayout` con `RequireRole allowedRoles={['Doctor']}`. No tiene `NavItem`: la barra superior muestra el título por defecto ("Dispensario Médico").

### Página `/appointments` para el `Doctor` (`appointments-page.tsx`)

La página lee `user.role` y `user.doctorId` con `useAuth()`. Para Admin y Recepción **nada cambia**.

Para el `Doctor`:

- **Sin vincular** (`doctorId === null`): solo un `Alert` con "Tu usuario todavía no está vinculado a una ficha de doctor. Pide al administrador que lo vincule."; no se llama a la API.
- **Vinculado**:
  - Valores por defecto: `from` = hoy y `to` = hoy; `status` `'all'`; `patient` `null`. **Limpiar filtros** vuelve a ellos y se deshabilita si ya están.
  - La query **no** envía `doctorId`: la API devuelve solo las del doctor del token.
  - Filtros: **Desde**, **Hasta**, **Estado** y **Paciente** (con la misma validación de rango del spec 09). Sin filtro **Doctor** ni botón **Nueva cita**.
  - Tabla: Fecha y hora, Paciente, Motivo, Estado y **Acciones** (sin **Doctor**).
  - Vacío con filtros por defecto: "No tienes citas hoy."; con otros filtros, "No se encontraron citas con esos filtros.".

Cambios en los componentes:

| Componente                 | Prop nueva                                  | Por defecto (Admin y Recepción) |
| -------------------------- | ------------------------------------------- | ------------------------------- |
| `AppointmentsFilters`      | `showDoctorFilter: boolean`                 | `true`                          |
| `AppointmentsFilters`      | `onCreate?: () => void` (sin ella, no hay **Nueva cita**) | presente          |
| `AppointmentsTable`        | `showDoctorColumn: boolean`                 | `true`                          |
| `AppointmentsTable`        | `onStart?: (appointment) => void` (con ella, columna **Acciones**) | ausente  |
| `AppointmentsTable`        | `emptyText` del caso por defecto            | "No hay citas próximas."        |

`today` se calcula una vez en la página y se pasa a la tabla (o se pasa ya el predicado) para decidir qué filas son iniciables con `canStartConsultation`.

#### Filas iniciables

- **Iniciable** (`canStartConsultation`): la fila tiene `cursor-pointer` y `hover`, y al pulsarla abre la confirmación; en **Acciones**, botón **Iniciar** (`size="sm"`, icono `Stethoscope`, `aria-label` "Iniciar consulta de {paciente}") que hace lo mismo y hace `stopPropagation`.
- **No iniciable** (otro día u otro estado): fila sin cursor ni clic y celda **Acciones** vacía.
- La fila no es enfocable: el acceso por teclado es el botón **Iniciar** (no se anidan controles interactivos).

### Confirmación (`start-consultation-dialog.tsx`)

`AlertDialog` como `patient-status-dialog.tsx` (botón propio, no `AlertDialogAction`, para no cerrar antes de que responda la API).

- Título: "Iniciar consulta".
- Descripción: "Se iniciará la consulta de **{patientFullName}** de las {formatLocalTimeRange(startLocal, endLocal)} con el motivo de la cita: “{reason}”."
- Botones: **Cancelar** e **Iniciar consulta** (con `Loader2` e "Iniciando…" mientras espera; **Cancelar** deshabilitado mientras tanto).
- Props: `appointment: AppointmentDto`, `onClose: () => void`, `onFailed: () => void` (refresca el listado).

Flujo al confirmar:

1. `startConsultation({ patientId, appointmentId: appointment.id, reason: appointment.reason })`.
2. **201** → `navigate('/consultations/{id}')` (sin toast).
3. **409** → `getPatientConsultations(patientId, { pageIndex: 0, pageSize: 100 })` y se busca la de `appointmentId === appointment.id`:
   - Encontrada → `navigate('/consultations/{id}')`.
   - No encontrada o error → cierra, `toast.error` "La cita ya tiene una consulta, pero no se pudo abrir." y `onFailed()`.
4. Otro error → cierra, `toast.error` con el mensaje de **Mensajes** y `onFailed()` (el estado o el día de la cita pudo cambiar).

La historia sale de la más reciente a la más antigua, así que la consulta de una cita de hoy está en la primera página; buscar más allá de 100 queda fuera.

### Página provisional (`features/consultations/consultation-page.tsx`)

- Lee `consultationId` con `useParams`; **no** llama a la API.
- Muestra un encabezado "Consulta", el texto "El detalle de la consulta estará disponible próximamente." y un enlace/botón **Volver a citas** a `/appointments`.
- El siguiente spec la reemplaza con el detalle.

### Mensajes de error

**Inicio de la consulta** (`consultation-messages.ts`, `getStartConsultationErrorMessage`):

| Error                | Mensaje                                                                                              |
| -------------------- | ---------------------------------------------------------------------------------------------------- |
| `status === 400`     | No se puede iniciar la consulta: la cita ya no está pendiente, no es de hoy o el paciente está inactivo. |
| `status === 403`     | No tienes permiso para iniciar esta consulta.                                                        |
| `status === 404`     | La cita o el paciente ya no existe.                                                                  |
| `kind === 'network'` | No se pudo conectar con el servidor.                                                                 |
| Cualquier otro       | Ocurrió un error inesperado. Inténtalo de nuevo.                                                     |

El 409 no pasa por esta función: dispara la búsqueda de la consulta existente.

**Listado del doctor** (`appointment-messages.ts`): un 403 para el `Doctor` muestra "No se pudieron ver tus citas. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar." (el claim `doctor_id` solo llega en tokens emitidos tras el vínculo). Para Admin y Recepción el 403 sigue con el mensaje del spec 09.

### Tests

Mismas pautas que el spec 09 (`fetch` simulado, `user-event`, `MemoryRouter`, `TestAuthProvider`, `<Toaster />`, `vi.setSystemTime` con `shouldAdvanceTime` para "hoy").

| Archivo                              | Casos |
| ------------------------------------ | ----- |
| `appointment-labels.test.ts`         | (se añade) `canStartConsultation`: hoy `Scheduled` y `Confirmed` → `true`; hoy `Completed`/`Cancelled`/`NoShow` → `false`; otro día `Scheduled` → `false`. |
| `start-consultation-dialog.test.tsx` | Muestra paciente, franja y motivo; confirmar → `POST /api/consultations` con `patientId`, `appointmentId` y `reason`, "Iniciando…" y navegación a `/consultations/{id}`; 409 → `GET /api/patients/{id}/consultations?pageIndex=0&pageSize=100` y navegación a la de ese `appointmentId`; 409 sin coincidencia → toast "La cita ya tiene una consulta, pero no se pudo abrir." y `onFailed`; 400 y red → toast con su mensaje y `onFailed`; **Cancelar** cierra sin petición. |
| `appointments-page.test.tsx`         | (se añade) Doctor vinculado: primera carga `GET /api/appointments?pageIndex=0&pageSize=10&from={hoy}&to={hoy}` sin `doctorId`; no hay **Nueva cita**, filtro ni columna **Doctor**; "No tienes citas hoy."; solo las filas iniciables tienen **Iniciar**; pulsar la fila o **Iniciar** abre la confirmación; una fila no iniciable no la abre; **Limpiar filtros** vuelve a hoy–hoy; 403 → mensaje del doctor. Doctor sin vincular → aviso y ninguna petición. Admin: sin columna **Acciones** (sin cambios). |
| `consultation-page.test.tsx`         | Muestra "Consulta", el texto provisional y **Volver a citas** a `/appointments`; no hace peticiones. |
| `nav-items.test.ts`                  | (se añade) `Doctor` tiene **Citas** (`/appointments`) antes de **Mi horario**. |
| `app-routes.test.tsx`                | (se añade) un `Doctor` entra a `/appointments` y ve el título **Citas**; `/consultations/{id}` lo ve el `Doctor` y lleva a `/` al Admin y a Recepción; el caso del spec 09 "un `Doctor` en `/appointments` termina en `/`" se sustituye. |

## Implementation plan

1. Crear `features/consultations/` con `consultation-types.ts`, `consultations-api.ts`, `consultation-messages.ts` y `consultation-page.tsx` (con su test). Añadir la ruta `/consultations/:consultationId` (`RequireRole ['Doctor']`). Verificar: `pnpm build` y `pnpm test` pasan.
2. Añadir `canStartConsultation` a `appointment-labels.ts` (y sus casos) y el mensaje del 403 del doctor a `appointment-messages.ts`. Verificar: `pnpm test` pasa.
3. Añadir **Citas** al menú del `Doctor` (`nav-items.ts` y su test) y abrir `/appointments` a `Doctor` (`app-routes.tsx` y su test). Verificar: `pnpm test` pasa.
4. Adaptar `appointments-filters.tsx` (`showDoctorFilter`, `onCreate` opcional) y `appointments-table.tsx` (`showDoctorColumn`, `onStart`, `emptyText`, filas iniciables), y la página: variante del doctor (valores por defecto hoy–hoy, sin `doctorId`, aviso sin vincular). Verificar: `pnpm test` pasa, incluidos los casos del spec 09 sin cambios.
5. Crear `start-consultation-dialog.tsx` con su test y conectarlo a la página (`onStart` abre el diálogo, `onFailed` recarga). Completar `appointments-page.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
6. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo con `Seed:SampleData:Password`) y `pnpm dev`: como recepción, agendar para hoy con `MG-0001`; como `doctor@dispensario.local`, ver el menú **Citas**, ver solo sus citas de hoy, iniciar la de hoy (llega a `/consultations/{id}`), volver atrás y pulsarla de nuevo (409 → abre la misma consulta), comprobar que una cita de otro día no tiene **Iniciar**; Admin y Recepción siguen igual y `/consultations/{id}` los lleva al inicio; móvil y tema claro/oscuro.
7. Actualizar `frontend/CLAUDE.md`:
   - **Estado actual:** el `Doctor` usa **Citas** (spec 10) para ver sus citas e iniciar la consulta de una cita de hoy, que lleva a `/consultations/:consultationId` (provisional).
   - **Estructura:** `features/consultations/` y `start-consultation-dialog`.
   - **Backlog:** fila 10 con este spec.

## Acceptance criteria

- [x] El `Doctor` ve el menú **Citas** antes de **Mi horario**; Admin y Recepción siguen viendo su menú sin cambios.
- [x] Un `Doctor` sin vincular ve en `/appointments` el aviso de vinculación y no se hace ninguna petición.
- [x] Un `Doctor` vinculado entra a `/appointments` y la primera carga hace `GET /api/appointments?pageIndex=0&pageSize=10&from={hoy}&to={hoy}` sin `doctorId`.
- [x] El doctor no ve **Nueva cita**, ni el filtro ni la columna **Doctor**; sí **Desde**, **Hasta**, **Estado**, **Paciente** y **Limpiar filtros** (que vuelve a hoy–hoy).
- [x] Sin citas con los filtros por defecto se ve "No tienes citas hoy.".
- [x] Solo las citas de hoy `Scheduled` o `Confirmed` tienen el botón **Iniciar** y la fila clicable; las demás no reaccionan al clic.
- [x] Pulsar una fila iniciable o su **Iniciar** abre "Iniciar consulta" con el paciente, la franja y el motivo; **Cancelar** la cierra sin petición.
- [x] Confirmar hace `POST /api/consultations` con `patientId`, `appointmentId` y el `reason` de la cita, muestra "Iniciando…" y navega a `/consultations/{id}`.
- [x] Si la cita ya tiene consulta (409), se navega a la consulta existente encontrada en `GET /api/patients/{patientId}/consultations`; si no aparece, toast "La cita ya tiene una consulta, pero no se pudo abrir.".
- [x] Un 400, 403, 404 o fallo de red al iniciar muestra el toast de su mensaje y recarga el listado.
- [x] Un 403 del listado para el doctor muestra el mensaje de volver a iniciar sesión.
- [x] `/consultations/{id}` muestra "Consulta", el texto provisional y **Volver a citas**; Admin y Recepción que entran terminan en `/`.
- [x] La vista de Admin y Recepción de `/appointments` no cambia (los tests del spec 09 siguen pasando).
- [x] En móvil la tabla se desplaza en horizontal y el diálogo se ve completo; bien en tema claro y oscuro.
- [x] `pnpm build`, `pnpm lint`, el chequeo de Prettier y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja el estado y la estructura nuevos y tiene la fila 10 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **No:** el doctor crea citas en este spec. Elegido por el usuario: el backend responde 403 (`Reception`); se hará después, con un spec del backend antes.
- **Yes:** reutilizar `/appointments` con una variante para el `Doctor`. Elegido por el usuario.
- **No:** menú propio `/my-appointments`.
- **Yes:** confirmación antes de iniciar y motivo de la cita como `reason`. Elegido por el usuario: la API exige `reason` y el de la cita es el natural; editarlo será de la pantalla de la consulta (`PUT`).
- **No:** iniciar sin confirmar ni diálogo con motivo editable.
- **Yes:** ante el 409, buscar la consulta existente en la historia del paciente y abrirla. Elegido por el usuario: cubre volver atrás tras iniciar. La API no tiene búsqueda por cita, así que se pide la primera página de 100.
- **Yes:** filtros **Desde**/**Hasta** = hoy por defecto, **Estado** y **Paciente**. Elegido por el usuario.
- **Yes:** fila clicable más botón **Iniciar** en una columna **Acciones**. Elegido por el usuario; el botón da el acceso por teclado.
- **Yes:** solo son iniciables las citas de hoy `Scheduled`/`Confirmed` (reglas de la API), calculado con la fecha del navegador como en el spec 09; si difiere de la del dispensario, la API responde 400 con su aviso.
- **Yes:** sin filtro ni columna **Doctor** para el doctor. Elegido por el usuario: siempre es él.
- **Yes:** página de destino `/consultations/:consultationId` solo para `Doctor`, vacía y sin peticiones. Elegido por el usuario; el siguiente spec decide si se abre al Admin.
- **Yes:** sin toast al iniciar con éxito: el cambio de página ya lo comunica.
- **Yes:** la página provisional no tiene `NavItem` y la barra superior muestra el título por defecto.
- **Yes:** las props nuevas de filtros y tabla tienen el comportamiento actual por defecto, para no tocar la vista de Admin y Recepción.

## Risks

| Risk                                                                                       | Mitigation                                                                                                 |
| ------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------- |
| El token del doctor no trae `doctor_id` aunque `/api/auth/me` sí (vinculado tras el login) | El 403 del listado muestra el mensaje de volver a iniciar sesión.                                         |
| La zona del navegador difiere de la del dispensario ("hoy")                                | La API valida y responde 400 con su aviso; mismo criterio documentado en el spec 09.                       |
| La consulta existente no está en la primera página de 100 de la historia                   | Improbable (orden de más reciente a más antigua); toast de error y recarga.                               |
| Romper la vista de Admin y Recepción al añadir props                                       | Props con el comportamiento actual por defecto; los tests del spec 09 siguen pasando.                     |
| Clic en el botón **Iniciar** que también dispara el de la fila (doble apertura)            | `stopPropagation` en el botón; caso en el test.                                                            |

## What is **not** in this spec

- Crear citas desde la vista del doctor (requiere un spec del backend).
- Detalle, edición y cierre de la consulta, e historia clínica.
- Consultas sin cita.
- Cancelar o marcar como no asistida desde la vista del doctor.
- Acceso del Admin a la página de la consulta.
- Cambios en la vista de Admin y Recepción.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

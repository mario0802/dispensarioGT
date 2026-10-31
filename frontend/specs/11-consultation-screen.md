# SPEC 11 — Pantalla de la consulta

> **Status:** Approved
> **Depends on:** SPEC 02, SPEC 03, SPEC 05, SPEC 07, SPEC 08, SPEC 10
> **Date:** 2026-10-29
> **Objective:** Reemplazar la página provisional `/consultations/:consultationId` por la pantalla de la consulta, donde el doctor ve los datos del paciente y de la consulta, registra motivo, diagnóstico y observaciones, la finaliza y consulta la historia clínica del paciente.

## Por qué este spec existe

El spec 10 deja al doctor en `/consultations/:consultationId` tras iniciar la consulta, pero la página no hace nada.
El backend ya ofrece el detalle (`GET /api/consultations/{id}`), la edición (`PUT`), el cierre (`POST …/close`, que además completa la cita) y la historia del paciente (`GET /api/patients/{id}/consultations`).
Este spec cierra el flujo clínico en el frontend: atender, registrar y finalizar.

## Scope

**In:**

- Página `/consultations/:consultationId` (solo `Doctor`, como en el spec 10) con, de arriba abajo:
  1. Encabezado con el nombre del paciente y el botón **Volver a citas**.
  2. Tarjeta **Paciente**: nombre, documento, edad y fecha de nacimiento, género, tipo de sangre, alergias (resaltadas si hay) y teléfono.
  3. Tarjeta **Consulta**: inicio, estado, doctor y cierre (si está cerrada), y el formulario con **Motivo**, **Diagnóstico** y **Observaciones** (las `notes` de la API).
  4. Botones **Guardar** (`PUT`) y **Finalizar consulta** (confirmación, `PUT` si hay cambios y `POST …/close`).
  5. Sección **Historia clínica**: acordeón con las consultas previas del paciente (sin la actual), paginado de 5 en 5 con **Anterior**/**Siguiente**.
- Modo solo lectura cuando la consulta está cerrada o es de otro doctor.
- Doctor sin vincular: aviso sin llamar a la API.
- Aviso al salir con cambios sin guardar: confirmación en **Volver a citas** y `beforeunload` del navegador.
- Al finalizar con éxito: toast "Consulta finalizada." y navegación a `/appointments`.
- Mover `discard-changes-dialog` a `src/components/` (pasa a usarlo una segunda feature), con la descripción como prop.
- Componente `accordion` de shadcn/ui.
- Tests de Vitest de lo nuevo y de lo que cambia.
- Actualizar `frontend/CLAUDE.md`: estado, stack (componente `accordion`), estructura y backlog.

**Out of scope (for future specs):**

- Acceso del Admin a la pantalla (la API le permite leer; hoy no tiene cómo llegar).
- Iniciar una consulta sin cita.
- Autoguardado.
- Interceptar la navegación interna (menú lateral, atrás del navegador) con cambios sin guardar: React Router en modo declarativo no tiene `useBlocker`.
- Mostrar la franja o el estado de la cita (`GET /api/appointments/{id}`).
- Editar los datos del paciente desde la consulta (alergias, tipo de sangre…).
- Reabrir una consulta cerrada (la API no lo permite).
- Imprimir o exportar la consulta o la historia.
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
└─ src/
   ├─ components/
   │  ├─ discard-changes-dialog.tsx            # movido desde features/doctors/components, + prop description
   │  └─ ui/accordion.tsx                      # nuevo (shadcn)
   └─ features/
      ├─ doctors/components/
      │  ├─ doctor-schedule-dialog.tsx         # import nuevo de DiscardChangesDialog
      │  └─ doctor-schedule-form.tsx           # import nuevo de DiscardChangesDialog
      └─ consultations/
         ├─ consultation-types.ts              # + UpdateConsultationRequest, CloseConsultationRequest
         ├─ consultations-api.ts               # + getConsultation, updateConsultation, closeConsultation
         ├─ consultation-labels.ts             # nuevo: estados, colores, isConsultationEditable
         ├─ consultation-labels.test.ts
         ├─ consultation-messages.ts           # + mensajes de carga, guardado, cierre e historia
         ├─ consultation-form-schema.ts        # nuevo: esquemas Zod de guardar y finalizar
         ├─ consultation-form-schema.test.ts
         ├─ consultation-page.tsx              # reescrita: carga, layout, guardado, cierre y salida
         ├─ consultation-page.test.tsx         # reescrito
         └─ components/
            ├─ patient-summary-card.tsx        # tarjeta Paciente (carga propia de GET /api/patients/{id})
            ├─ consultation-status-badge.tsx
            ├─ consultation-details.tsx        # inicio, estado, doctor y cierre
            ├─ consultation-form.tsx           # Motivo, Diagnóstico, Observaciones, Guardar y Finalizar
            ├─ consultation-form.test.tsx
            ├─ consultation-read-only.tsx      # los tres textos en solo lectura
            ├─ finish-consultation-dialog.tsx  # confirmación del cierre
            ├─ consultation-history.tsx        # acordeón paginado de la historia
            └─ consultation-history.test.tsx
```

### Tipos (`consultation-types.ts`, se añaden)

```ts
// Cuerpo de PUT /api/consultations/{id}: reemplaza los tres textos (null borra)
export interface UpdateConsultationRequest {
  reason: string
  diagnosis: string | null
  notes: string | null
}

// Cuerpo de POST /api/consultations/{id}/close (notes null borra)
export interface CloseConsultationRequest {
  diagnosis: string
  notes: string | null
}
```

### API (`consultations-api.ts`, se añaden)

```ts
export const getConsultation = (id: string) =>
  api.get<ConsultationDto>(`/api/consultations/${id}`)

export const updateConsultation = (id: string, request: UpdateConsultationRequest) =>
  api.put<void>(`/api/consultations/${id}`, request)

export const closeConsultation = (id: string, request: CloseConsultationRequest) =>
  api.post<void>(`/api/consultations/${id}/close`, request)
```

Los datos del paciente usan `getPatient` de `features/patients/patients-api.ts` (ya existe), y la historia, `getPatientConsultations` (spec 10).

### Etiquetas (`consultation-labels.ts`)

```ts
export const consultationStatusLabels: Record<ConsultationStatus, string> = {
  Open: 'Abierta',
  Closed: 'Cerrada',
}

// Mismas clases que los estados de cita equivalentes (Open ≈ Confirmed, Closed ≈ Completed)
export const consultationStatusBadgeClasses: Record<ConsultationStatus, string>

// Editable: abierta y del doctor del usuario
export function isConsultationEditable(consultation: ConsultationDto, doctorId: string | null): boolean
```

- Las fechas `startedAtLocal`/`closedAtLocal` (`"yyyy-MM-ddTHH:mm:ss"`) se muestran con `formatLocalDate` y `formatLocalTime` de `appointment-labels.ts` ("03/11/2026 09:32"), sin pasar por `new Date()`.

### Formulario (`consultation-form-schema.ts`)

```ts
// Guardar: motivo obligatorio; diagnóstico y observaciones opcionales
export const saveConsultationSchema = z.object({
  reason: z.string().trim().min(1, 'El motivo es obligatorio.').max(500, 'El motivo admite como máximo 500 caracteres.'),
  diagnosis: z.string().trim().max(1000, 'El diagnóstico admite como máximo 1000 caracteres.'),
  notes: z.string().trim().max(4000, 'Las observaciones admiten como máximo 4000 caracteres.'),
})

// Finalizar: además, diagnóstico obligatorio
export const finishConsultationSchema = saveConsultationSchema.extend({
  diagnosis: z.string().trim().min(1, 'El diagnóstico es obligatorio para finalizar.').max(1000, …),
})

export type ConsultationFormValues = z.infer<typeof saveConsultationSchema>
```

- El formulario usa React Hook Form con `saveConsultationSchema` (`zodResolver`). **Finalizar** valida además con `finishConsultationSchema` y, si falla, marca el error en **Diagnóstico** (`setError`) sin abrir la confirmación.
- Valores iniciales desde la consulta: `reason`, `diagnosis ?? ''`, `notes ?? ''`.
- Al enviar, un texto vacío tras `trim` viaja como `null` (`diagnosis`, `notes`).
- Campos: **Motivo** con `Textarea` de 2 filas, **Diagnóstico** con `Textarea` de 3 y **Observaciones** con `Textarea` de 6; contador "n/máx" bajo cada uno.

### Permisos y modos

| Situación                                       | Qué se ve                                                                             |
| ----------------------------------------------- | ------------------------------------------------------------------------------------- |
| `Doctor` sin vincular (`user.doctorId === null`) | Solo el `Alert` de vinculación del spec 10; ninguna petición.                         |
| Consulta `Open` del propio doctor               | Formulario editable con **Guardar** y **Finalizar consulta**.                         |
| Consulta `Closed`                               | Solo lectura con `Alert` "Esta consulta está cerrada y no se puede editar."           |
| Consulta `Open` de otro doctor                  | Solo lectura con `Alert` "Esta consulta es de {doctorFullName}: solo puedes verla."   |
| `Admin` o `Receptionist`                        | `RequireRole` → `/` (sin cambios respecto al spec 10).                               |

La ruta y su `RequireRole allowedRoles={['Doctor']}` no cambian.

### Página (`consultation-page.tsx`)

**Carga:**

1. `getConsultation(consultationId)` al montar. Mientras carga, `Skeleton`s de las tarjetas.
2. Con la consulta, `PatientSummaryCard` pide `getPatient(patientId)` y `ConsultationHistory` pide la historia; cada una con su propio estado de carga y error, sin bloquear el resto.

**Errores de carga de la consulta** (en lugar de la página, con **Volver a citas**):

| Error                | Mensaje                                                                                                  | Reintentar |
| -------------------- | -------------------------------------------------------------------------------------------------------- | ---------- |
| `status === 404`     | La consulta no existe.                                                                                   | No         |
| `status === 403`     | No se pudo ver la consulta. Si acaban de vincularte a tu ficha, cierra sesión y vuelve a entrar.         | No         |
| `kind === 'network'` | No se pudo conectar con el servidor.                                                                     | Sí         |
| Cualquier otro       | Ocurrió un error inesperado. Inténtalo de nuevo.                                                         | Sí         |

**Layout** (una columna, `max-w-4xl`, `gap-6`, `p-6`; en móvil `p-4`):

- Encabezado: `h2` "Consulta de {patientFullName}" y, a la derecha (debajo en móvil), **Volver a citas** (`variant="outline"`, icono `ArrowLeft`).
- `Card` **Paciente** (`PatientSummaryCard`): rejilla de 2 columnas (1 en móvil) con Documento (`formatDocument`), Edad ("35 años · 14/05/1990" con `getAge`, `formatAge` y `formatBirthDate`), Género, Tipo de sangre, Teléfono y, a todo el ancho, Alergias. Un valor `null` se muestra como "—". Si hay alergias, el bloque usa el `Alert` destructivo con el icono `TriangleAlert` y el texto de las alergias; si no, "Sin alergias registradas.". Si el paciente está inactivo, `Badge` "Inactivo" junto al nombre. Error de carga: `Alert` "No se pudieron cargar los datos del paciente." con **Reintentar**.
- `Card` **Consulta**: `ConsultationDetails` (Inicio, Estado con `ConsultationStatusBadge`, Doctor y, si está cerrada, Cierre) y debajo el `Alert` del modo, si lo hay, y `ConsultationForm` o `ConsultationReadOnly` (Motivo, Diagnóstico y Observaciones como texto con saltos de línea, "—" si son `null`).
- Sección **Historia clínica** (`ConsultationHistory`).

### Guardar

- **Guardar** (`type="submit"`) se deshabilita si el formulario no tiene cambios (`isDirty`) o mientras se guarda o finaliza; muestra `Loader2` y "Guardando…".
- `updateConsultation(id, { reason, diagnosis, notes })`.
- **204** → `toast.success('Cambios guardados.')`, se actualiza la consulta en memoria (`setData`) y el formulario se reinicia con los valores guardados (deja de estar sucio).
- Error → `toast.error` con `getSaveConsultationErrorMessage`; con un 400 de consulta cerrada o un 403/404 se vuelve a pedir la consulta (la página pasa a su modo correcto).

### Finalizar (`finish-consultation-dialog.tsx`)

- **Finalizar consulta** (`variant="default"`, icono `CircleCheck`) valida con `finishConsultationSchema`; si es válido abre un `AlertDialog` (botón propio, como en `start-consultation-dialog.tsx`):
  - Título: "Finalizar consulta".
  - Descripción: "La consulta de **{patientFullName}** se cerrará y su cita pasará a Completada. Después no se podrá editar."
  - Botones: **Cancelar** y **Finalizar** (con `Loader2` y "Finalizando…"; **Cancelar** deshabilitado mientras tanto).
- Al confirmar:
  1. Si el formulario tiene cambios, `updateConsultation` con los tres textos (para no perder el motivo, que el cierre no lleva).
  2. `closeConsultation(id, { diagnosis, notes })`.
  3. **204** → `toast.success('Consulta finalizada.')` y `navigate('/appointments')` (sin aviso de cambios sin guardar).
  4. Error en el paso 1 o 2 → cierra el diálogo, `toast.error` con `getCloseConsultationErrorMessage` y la página sigue igual; con un 400 de consulta cerrada o un 403/404 se vuelve a pedir la consulta. Si el paso 1 tuvo éxito y falla el 2, el formulario queda reiniciado con lo guardado.

### Salir con cambios sin guardar

- **Volver a citas** con el formulario sucio abre `DiscardChangesDialog` con la descripción "Los cambios de la consulta no se guardarán."; **Descartar** navega a `/appointments` y **Seguir editando** cierra. Sin cambios, navega directamente.
- Mientras el formulario está sucio, la página registra un `beforeunload` (`event.preventDefault()`) para que el navegador avise al cerrar o recargar la pestaña; se quita al guardar, al finalizar y al desmontar.
- El menú lateral y el botón atrás del navegador no se interceptan (ver **Decisions**).

### `DiscardChangesDialog` (movido a `src/components/discard-changes-dialog.tsx`)

- Prop nueva `description: string`. Los dos usos del horario pasan "Los cambios del horario no se guardarán." (su texto actual), así que el horario no cambia.

### Historia clínica (`consultation-history.tsx`)

Props: `patientId: string`, `currentConsultationId: string`.

- Encabezado "Historia clínica" con el total "{n} consultas previas" ("1 consulta previa"; "Sin consultas previas" si es 0), donde `n = count - 1` (la API incluye la actual; nunca menor que 0).
- `getPatientConsultations(patientId, { pageIndex, pageSize: 5 })` al montar y al cambiar de página.
- De cada página se quita la consulta actual (`id === currentConsultationId`); por eso una página puede mostrar 4.
- `Accordion` de shadcn (`type="multiple"`), un ítem por consulta, todos cerrados al cargar o cambiar de página:
  - Disparador: fecha y hora de inicio, doctor, `ConsultationStatusBadge` y el motivo truncado a una línea.
  - Contenido: Motivo, Diagnóstico y Observaciones completos (con saltos de línea, "—" si son `null`) y, si está cerrada, la fecha de cierre.
- Paginación como en las tablas: "Página {i} de {total}" con **Anterior** y **Siguiente** (total con `count` y 5), oculta si hay una sola página.
- Cargando: `Skeleton`s de 3 filas. Error: `Alert` "No se pudo cargar la historia clínica." con **Reintentar**. Sin previas: texto "El paciente no tiene consultas previas.".

### Mensajes (`consultation-messages.ts`, se añaden)

`getLoadConsultationErrorMessage` (tabla de **Errores de carga**) y:

**Guardar** (`getSaveConsultationErrorMessage`) y **Finalizar** (`getCloseConsultationErrorMessage`):

| Error                                   | Guardar                                                       | Finalizar                                                     |
| --------------------------------------- | ------------------------------------------------------------- | ------------------------------------------------------------- |
| `400` con `validationErrors`            | Revisa los campos: hay textos vacíos o demasiado largos.      | Revisa los campos: hay textos vacíos o demasiado largos.      |
| `400` sin `validationErrors`            | La consulta ya está cerrada y no se puede editar.             | La consulta ya está cerrada.                                  |
| `403`                                   | No tienes permiso para editar esta consulta.                  | No tienes permiso para finalizar esta consulta.               |
| `404`                                   | La consulta ya no existe.                                     | La consulta ya no existe.                                     |
| `kind === 'network'`                    | No se pudo conectar con el servidor.                          | No se pudo conectar con el servidor.                          |
| Cualquier otro                          | Ocurrió un error inesperado. Inténtalo de nuevo.              | Ocurrió un error inesperado. Inténtalo de nuevo.              |

### Tests

Mismas pautas que los specs 09 y 10 (`fetch` simulado, `user-event`, `MemoryRouter` con `Routes`, `TestAuthProvider`, `<Toaster />`).

| Archivo                            | Casos |
| ---------------------------------- | ----- |
| `consultation-labels.test.ts`      | `isConsultationEditable`: abierta y propia → `true`; cerrada → `false`; de otro doctor → `false`; `doctorId` `null` → `false`. |
| `consultation-form-schema.test.ts` | Guardar: motivo vacío o de 501 → error; diagnóstico vacío vale. Finalizar: diagnóstico vacío → "El diagnóstico es obligatorio para finalizar."; 1001 en diagnóstico y 4001 en observaciones → error. |
| `consultation-form.test.tsx`       | Muestra los valores iniciales; **Guardar** deshabilitado sin cambios; guardar envía `null` en los vacíos; **Finalizar** con diagnóstico vacío marca el error sin abrir la confirmación. |
| `consultation-history.test.tsx`    | Pide `GET /api/patients/{id}/consultations?pageIndex=0&pageSize=5`; oculta la actual y muestra "{count-1} consultas previas"; desplegar un ítem muestra diagnóstico y observaciones; **Siguiente** pide `pageIndex=1`; sin previas → "El paciente no tiene consultas previas."; error → **Reintentar** vuelve a pedir. |
| `consultation-page.test.tsx`       | (reescrito) Pide `GET /api/consultations/{id}` y `GET /api/patients/{patientId}`; muestra el encabezado, la tarjeta del paciente (alergias resaltadas, "—" en nulos) y los datos de la consulta. Guardar → `PUT` con los tres textos y toast "Cambios guardados.". Finalizar → confirmación; sin cambios solo `POST …/close`; con cambios `PUT` y luego `POST …/close`; toast "Consulta finalizada." y navegación a `/appointments`. 400 al cerrar → toast "La consulta ya está cerrada." y nueva petición de la consulta. Consulta cerrada y de otro doctor → solo lectura con su aviso y sin botones. 404 y 403 → su mensaje; error de red → **Reintentar**. Doctor sin vincular → aviso sin peticiones. **Volver a citas** sin cambios navega; con cambios abre "¿Descartar los cambios?" y **Descartar** navega. |
| Tests del horario                  | Siguen pasando sin cambios tras mover `DiscardChangesDialog`. |

## Implementation plan

1. Mover `discard-changes-dialog.tsx` a `src/components/` con la prop `description` y actualizar sus dos imports del horario. Verificar: `pnpm build` y `pnpm test` pasan.
2. Añadir el `accordion` de shadcn (`pnpm dlx shadcn@latest add accordion`, corrigiendo el import de `cn`; consultar antes su documentación con Context7). Verificar: `pnpm build` pasa.
3. Ampliar `consultation-types.ts` y `consultations-api.ts`; crear `consultation-labels.ts` (con su test), `consultation-messages.ts` ampliado y `consultation-form-schema.ts` (con su test). Verificar: `pnpm test` pasa.
4. Crear `consultation-status-badge`, `consultation-details`, `consultation-read-only` y `patient-summary-card`, y reescribir `consultation-page.tsx` con la carga, los errores, el modo solo lectura y el aviso de doctor sin vincular (todavía sin formulario). Verificar: `pnpm test` pasa con los casos de carga y solo lectura.
5. Crear `consultation-form.tsx` (con su test) y conectar **Guardar** en la página. Verificar: `pnpm test` pasa.
6. Crear `finish-consultation-dialog.tsx` y conectar **Finalizar consulta** (PUT si hay cambios, cierre, toast y navegación). Verificar: `pnpm test` pasa.
7. Añadir la salida con cambios sin guardar (`DiscardChangesDialog` en **Volver a citas** y `beforeunload`). Verificar: `pnpm test` pasa.
8. Crear `consultation-history.tsx` (con su test) y añadirla al final de la página. Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
9. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo con `Seed:SampleData:Password`) y `pnpm dev`: como recepción, agendar dos citas de hoy para el mismo paciente con `MG-0001`; como `doctor@dispensario.local`, iniciar la primera, guardar, recargar (los textos siguen), intentar finalizar sin diagnóstico, finalizar (toast, vuelve a **Citas** y la cita sale Completada); iniciar la segunda y ver la primera en la historia; volver con cambios (aviso) y recargar con cambios (aviso del navegador); abrir de nuevo la URL de la primera (solo lectura); móvil y tema claro/oscuro.
10. Actualizar `frontend/CLAUDE.md`:
    - **Estado actual:** la pantalla de la consulta (spec 11) sustituye a la provisional.
    - **Stack/estado:** `accordion` en la lista de componentes de shadcn.
    - **Estructura:** `features/consultations/` y sus componentes; `discard-changes-dialog` en `src/components/`.
    - **Backlog:** fila 11 con este spec.

## Acceptance criteria

- [x] Al abrir `/consultations/{id}` como el doctor de la consulta se piden `GET /api/consultations/{id}` y `GET /api/patients/{patientId}`, y se ven el encabezado "Consulta de {paciente}", la tarjeta **Paciente**, la tarjeta **Consulta** y la **Historia clínica**.
- [x] La tarjeta **Paciente** muestra documento, edad con fecha de nacimiento, género, tipo de sangre, teléfono y alergias; las alergias, si hay, en un aviso destacado; los nulos como "—".
- [x] La tarjeta **Consulta** muestra inicio, estado (Abierta/Cerrada), doctor y, si está cerrada, cierre.
- [x] Con la consulta abierta y propia, **Motivo**, **Diagnóstico** y **Observaciones** son editables con sus límites (500, 1000 y 4000) y el motivo es obligatorio.
- [x] **Guardar** está deshabilitado sin cambios; al guardar hace `PUT /api/consultations/{id}` con los tres textos (vacíos como `null`) y muestra "Cambios guardados.".
- [x] **Finalizar consulta** sin diagnóstico marca "El diagnóstico es obligatorio para finalizar." y no abre la confirmación.
- [x] Confirmar la finalización hace `PUT` solo si hay cambios y después `POST /api/consultations/{id}/close` con diagnóstico y observaciones; muestra "Consulta finalizada." y navega a `/appointments`.
- [x] Un error al guardar o finalizar muestra el toast de su mensaje; un 400 de consulta cerrada, un 403 o un 404 vuelven a pedir la consulta.
- [x] Una consulta cerrada o de otro doctor se ve en solo lectura, con su aviso y sin **Guardar** ni **Finalizar consulta**.
- [x] Un 404 o 403 al cargar la consulta muestra su mensaje y **Volver a citas**; un error de red muestra además **Reintentar**.
- [x] Un `Doctor` sin vincular ve el aviso de vinculación y no se hace ninguna petición.
- [x] Un error al cargar el paciente o la historia muestra su aviso con **Reintentar** sin ocultar el resto de la página.
- [x] **Volver a citas** con cambios sin guardar pide confirmar "¿Descartar los cambios?"; sin cambios navega directamente.
- [x] Con cambios sin guardar, cerrar o recargar la pestaña muestra el aviso del navegador.
- [x] La historia pide `pageSize=5`, no muestra la consulta actual, indica "{n} consultas previas", despliega diagnóstico y observaciones de cada consulta y pagina con **Anterior**/**Siguiente**.
- [x] El modal del cronograma y **Mi horario** siguen mostrando su aviso de descartar cambios igual que antes.
- [x] En móvil la página se ve en una columna sin desplazamiento horizontal y los diálogos se ven completos; bien en tema claro y oscuro.
- [x] `pnpm build`, `pnpm lint`, el chequeo de Prettier y `pnpm test` terminan sin errores.
- [x] `frontend/CLAUDE.md` refleja el estado, los componentes y la estructura nuevos y tiene la fila 11 en el backlog.
- [x] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** motivo, diagnóstico y notas editables; las notas se rotulan **Observaciones**. Elegido por el usuario.
- **No:** motivo en solo lectura.
- **Yes:** botón **Guardar** (`PUT`) más **Finalizar consulta**. Elegido por el usuario: permite guardar a mitad de la consulta.
- **No:** solo **Finalizar** (se pierde lo escrito si se sale) ni autoguardado (estados y errores en segundo plano).
- **Yes:** finalizar hace `PUT` si hay cambios y luego el cierre. Elegido por el usuario: el cierre no lleva el motivo y así no se pierde.
- **No:** exigir guardar antes de finalizar.
- **Yes:** tras finalizar, toast y vuelta a **Citas**. Elegido por el usuario.
- **No:** quedarse en la página en solo lectura tras cerrar.
- **Yes:** pantalla solo para `Doctor`. Elegido por el usuario; el acceso del Admin va en otro spec.
- **Yes:** consulta cerrada o de otro doctor en la misma página en solo lectura. Elegido por el usuario: la API deja leer a cualquier doctor.
- **Yes:** cabecera del paciente con identificación y datos clínicos (sin email ni dirección). Elegido por el usuario.
- **Yes:** alergias resaltadas con un `Alert` destructivo: es el dato clínico que no se puede pasar por alto.
- **Yes:** datos de la consulta sin la cita (inicio, estado, doctor y cierre). Elegido por el usuario: evita otra petición.
- **Yes:** historia en acordeón por consulta (`accordion` de shadcn, `type="multiple"`). Elegido por el usuario.
- **No:** una sola sección plegable con la lista completa.
- **Yes:** historia paginada de 5 en 5 con **Anterior**/**Siguiente**, sin la consulta actual. Elegido por el usuario. Quitarla en el cliente hace que una página pueda mostrar 4; se acepta para no pedir otra API.
- **No:** "Cargar más" ni una sola petición de 100.
- **Yes:** aviso de cambios sin guardar en **Volver a citas** y con `beforeunload`. Elegido por el usuario.
- **No:** interceptar el menú lateral y el atrás del navegador: `useBlocker` exige un data router y la app usa el modo declarativo (spec 03); cambiar de modo es otro spec.
- **Yes:** mover `DiscardChangesDialog` a `src/components/` con `description` como prop: regla del proyecto para componentes usados por dos features.
- **Yes:** el paciente y la historia cargan por separado y sus errores no bloquean la consulta: el doctor puede seguir trabajando.
- **Yes:** tras un 400 de consulta cerrada, un 403 o un 404 al escribir se vuelve a pedir la consulta, para que la página muestre su estado real.

## Risks

| Risk                                                                                      | Mitigation                                                                                                      |
| ----------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| El doctor sale por el menú lateral o el atrás del navegador y pierde lo escrito          | **Guardar** siempre disponible con cambios; aviso en **Volver a citas** y al cerrar o recargar la pestaña.      |
| Finalizar con `PUT` correcto y cierre fallido deja la consulta guardada pero abierta     | Toast del error; el formulario queda con lo guardado y se puede volver a finalizar.                            |
| La consulta se cierra en otra pestaña mientras se edita                                    | La API responde 400; se muestra el aviso y se vuelve a pedir la consulta, que pasa a solo lectura.              |
| Páginas de la historia con 4 elementos al quitar la actual                                | Aceptado y documentado; el contador usa `count - 1`.                                                            |
| Mover `DiscardChangesDialog` rompe el horario                                             | Misma descripción pasada como prop; los tests del horario siguen pasando.                                       |
| El CLI de shadcn genera `import { cn } from "cn"` en Windows                              | Corregirlo a `@/lib/utils` tras el `add`, como indica `frontend/CLAUDE.md`.                                     |

## What is **not** in this spec

- Acceso del Admin a la pantalla de la consulta.
- Consultas sin cita.
- Autoguardado.
- Interceptar el menú lateral o el atrás del navegador con cambios sin guardar.
- Franja y estado de la cita en la pantalla.
- Editar los datos del paciente desde la consulta.
- Reabrir una consulta cerrada.
- Imprimir o exportar.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

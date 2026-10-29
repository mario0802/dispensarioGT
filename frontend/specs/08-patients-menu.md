# SPEC 08 — Menú Pacientes: listado, alta, edición y activación

> **Status:** Implemented
> **Depends on:** SPEC 02, SPEC 03, SPEC 05, SPEC 06
> **Date:** 2026-10-28
> **Objective:** Añadir el menú **Pacientes** (Admin y Recepción) con el listado paginado de pacientes, un botón para darlos de alta, un botón por fila que abre el modal de edición y la activación y desactivación con confirmación.

## Por qué este spec existe

El backend ya expone el CRUD de pacientes (`/api/patients`) y los pacientes son, junto con los doctores, la base de la agenda de citas.
El menú sigue el patrón del menú **Doctores** (spec 06): tabla paginada con búsqueda y filtro de estado, modales para alta y edición y confirmaciones para activar y desactivar.
Es la segunda feature que usa `PaginatedResult` y `useDebouncedValue`, así que, según la regla de compartidos, se mueven fuera de `features/doctors/`.

## Scope

**In:**

- Componente `textarea` de shadcn/ui (sin paquetes npm nuevos).
- Mover `PaginatedResult` a `src/lib/api/paginated-result.ts` y `useDebouncedValue` (con su test) a `src/hooks/use-debounced-value.ts`, actualizando los imports de `features/doctors/`.
- Feature `features/patients/`: tipos, cliente de API (`GET /api/patients`, `GET /api/patients/{id}`, `POST /api/patients`, `PUT /api/patients/{id}`, `POST /api/patients/{id}/deactivate`, `POST /api/patients/{id}/activate`), mensajes, etiquetas, página `/patients` y sus componentes.
- Listado en tabla con búsqueda (debounce de 300 ms), filtro de estado y paginación (10 por página), con estado local (no en la URL).
- Botón **Nuevo paciente** que abre el modal de alta.
- Botón de icono (lápiz) visible en cada fila que abre el modal de edición; la edición carga el detalle con `GET /api/patients/{id}`.
- Formulario con todos los campos de la API: documento, nombre, apellido, fecha de nacimiento, género, email, teléfono, dirección, tipo de sangre y alergias.
- Menú ⋯ por fila con **Activar**/**Desactivar** y confirmación (`alert-dialog`).
- Toasts de éxito y de error de las acciones.
- Ítem **Pacientes** en los menús de `Admin` y `Receptionist`, y ruta hija `/patients` de `AppLayout` con `RequireRole`.
- Tests de Vitest de la página, el formulario, la confirmación, los menús y las rutas.
- Actualizar `frontend/CLAUDE.md`: estado, componentes de shadcn, estructura y backlog.

**Out of scope (for future specs):**

- El menú **Pacientes** para el rol `Doctor` (aunque la API le deja leer).
- Página de detalle del paciente (`/patients/:id`) e historia clínica (`GET /api/patients/{id}/consultations`).
- Búsqueda por documento exacto (`GET /api/patients/by-document`), por ejemplo al agendar una cita.
- Agendar citas desde el listado.
- Filtros y página en la URL, ordenar por columnas y elegir el tamaño de página.
- Date picker con calendario (`react-day-picker`).
- Extraer componentes genéricos compartidos con Doctores (tabla, barra de herramientas, diálogo de estado).
- Tests E2E.
- Cambios en el backend.

## Data model

### Estructura resultante

```
frontend/
└─ src/
   ├─ app-routes.tsx                    # + ruta /patients (RequireRole Admin, Receptionist)
   ├─ app-routes.test.tsx               # + casos de /patients
   ├─ components/ui/
   │  └─ textarea.tsx                   # nuevo (shadcn)
   ├─ hooks/
   │  ├─ use-debounced-value.ts         # movido desde features/doctors/
   │  └─ use-debounced-value.test.ts    # movido
   ├─ lib/api/
   │  └─ paginated-result.ts            # nuevo: PaginatedResult<T> (sale de doctor-types.ts)
   └─ features/
      ├─ doctors/                       # solo cambian los imports de PaginatedResult y useDebouncedValue
      ├─ layout/
      │  ├─ nav-items.ts                # + Pacientes en Admin y Receptionist
      │  └─ nav-items.test.ts           # + casos de Pacientes
      └─ patients/
         ├─ patient-types.ts            # PatientDto, AddressDto, PatientRequest, PatientListQuery, Gender, BloodType
         ├─ patients-api.ts             # getPatients, getPatient, createPatient, updatePatient, activatePatient, deactivatePatient
         ├─ patient-labels.ts           # genderLabels, bloodTypes, getPatientFullName, formatDocument, getAge
         ├─ patient-labels.test.ts
         ├─ patient-messages.ts         # mensajes de error propios
         ├─ patients-page.tsx           # página /patients
         ├─ patients-page.test.tsx
         └─ components/
            ├─ patients-toolbar.tsx     # búsqueda, filtro de estado y botón Nuevo paciente
            ├─ patients-table.tsx       # tabla, estados de carga/vacío/error y paginación
            ├─ patient-row-actions.tsx  # botón Editar (lápiz) y menú ⋯ (Activar/Desactivar)
            ├─ patient-form.tsx         # formulario de alta/edición (React Hook Form + Zod)
            ├─ patient-form.test.tsx
            ├─ patient-form-dialog.tsx  # modal de alta/edición (carga el detalle al editar)
            ├─ patient-status-dialog.tsx # confirmación de activar/desactivar
            └─ patient-status-dialog.test.tsx
```

Los nombres de los componentes de `components/` pueden ajustarse al implementar si se agrupan de otra forma, siempre dentro de `features/patients/components/`.

### Tipos (`features/patients/patient-types.ts`)

```ts
import type { DocumentType } from '@/features/auth/auth-types'

export type Gender = 'Male' | 'Female' | 'Other'
export type BloodType = 'A+' | 'A-' | 'B+' | 'B-' | 'AB+' | 'AB-' | 'O+' | 'O-'

export interface AddressDto {
  street: string
  city: string
  state: string | null
  reference: string | null
}

// Respuesta de GET /api/patients/{id} y cada elemento del listado
export interface PatientDto {
  id: string
  documentType: DocumentType
  documentNumber: string
  firstName: string
  lastName: string
  birthDate: string // "yyyy-MM-dd"
  gender: Gender
  email: string | null
  phone: string | null
  address: AddressDto | null
  bloodType: BloodType | null
  allergies: string | null
  isActive: boolean
  createdAtUtc: string
}

// Cuerpo de POST /api/patients y PUT /api/patients/{id} (el PUT reemplaza todo: null borra)
export interface PatientRequest {
  documentType: DocumentType
  documentNumber: string
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
  email: string | null
  phone: string | null
  address: AddressDto | null
  bloodType: BloodType | null
  allergies: string | null
}

export interface CreatePatientResponse {
  id: string
}

// Query de GET /api/patients (los null/undefined los omite el cliente)
export interface PatientListQuery {
  pageIndex: number
  pageSize: number
  search?: string
  isActive?: boolean
}
```

`PaginatedResult<T>` pasa a `src/lib/api/paginated-result.ts` sin cambios; `doctor-types.ts` deja de declararlo.

### API (`features/patients/patients-api.ts`)

```ts
export const getPatients = (query: PatientListQuery) =>
  api.get<PaginatedResult<PatientDto>>('/api/patients', query)
export const getPatient = (id: string) => api.get<PatientDto>(`/api/patients/${id}`)
export const createPatient = (request: PatientRequest) =>
  api.post<CreatePatientResponse>('/api/patients', request)
export const updatePatient = (id: string, request: PatientRequest) =>
  api.put<void>(`/api/patients/${id}`, request)
export const activatePatient = (id: string) => api.post<void>(`/api/patients/${id}/activate`)
export const deactivatePatient = (id: string) => api.post<void>(`/api/patients/${id}/deactivate`)
```

Mismas firmas que `doctors-api.ts`.

### Etiquetas (`features/patients/patient-labels.ts`)

- `genderLabels: Record<Gender, string>`: `Male` → "Masculino", `Female` → "Femenino", `Other` → "Otro".
- `bloodTypes: BloodType[]`: los 8 valores en el orden `A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+`, `O-` (se muestran tal cual).
- `getPatientFullName(patient)`: `"{firstName} {lastName}"`.
- `formatDocument(patient)`: `"{documentTypeLabels[documentType]} {documentNumber}"` (reutiliza `documentTypeLabels` de `features/auth/document-type-labels.ts`), p. ej. "Cédula 0102030405".
- `getAge(birthDate: string, today: Date): number`: años cumplidos en la fecha local de `today` (0 para menores de un año).
- `formatBirthDate(birthDate: string): string`: `"dd/MM/yyyy"`, sin pasar por `Date` (evita el desfase de zona horaria).

### Permisos y rutas

| Rol            | Menú **Pacientes** | Ruta `/patients`         | Acciones                                                  |
| -------------- | ------------------ | ------------------------ | --------------------------------------------------------- |
| `Admin`        | Sí                 | Sí                       | **Nuevo paciente**, Editar, Activar, Desactivar           |
| `Receptionist` | Sí                 | Sí                       | **Nuevo paciente**, Editar, Activar, Desactivar           |
| `Doctor`       | No                 | No: `RequireRole` → `/`  | —                                                         |

- `NavItem` `{ title: 'Pacientes', url: '/patients', icon: Users }` en `roleNavItems.Admin` y `roleNavItems.Receptionist`, después de **Doctores**.
- Ruta hija de `AppLayout`, como `/doctors`:

```tsx
<Route
  path="patients"
  element={
    <RequireRole allowedRoles={['Admin', 'Receptionist']}>
      <PatientsPage />
    </RequireRole>
  }
/>
```

- Los dos roles con acceso pueden escribir (política `Reception`), así que la página no oculta acciones por rol. Si la API devolviera 403, se muestra su toast (ver **Mensajes**).

### Página `/patients` (`patients-page.tsx`)

Mismo comportamiento que `/doctors` (spec 06):

- Estado local: `search`, `status` (`'all' | 'active' | 'inactive'`, por defecto `'all'`) y `pageIndex` (desde 0). `pageSize` fijo en 10.
- La búsqueda usa `useDebouncedValue(search, 300)`; se envía recortada y, si queda vacía, se omite.
- `status` → `isActive`: `'all'` lo omite, `'active'` → `true`, `'inactive'` → `false`.
- Cambiar la búsqueda (ya con debounce) o el estado vuelve a `pageIndex = 0`.
- Tras crear, editar, activar o desactivar se repite la consulta con los mismos parámetros; si la página queda vacía y no es la primera, se retrocede una.
- Mientras recarga se conservan las filas; solo la primera carga muestra el skeleton.

#### Barra de herramientas (`patients-toolbar.tsx`)

| Elemento             | Detalle                                                                                                          |
| -------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Búsqueda             | `Input` con icono `Search`, placeholder "Buscar por nombre o documento", `aria-label` "Buscar pacientes".       |
| Estado               | `Select` con **Todos**, **Activos** e **Inactivos** (`aria-label` "Filtrar por estado"), **Todos** por defecto.  |
| **Nuevo paciente**   | Botón con icono `Plus`; abre el modal de alta.                                                                   |

En móvil los tres elementos se apilan; desde `sm`, en una fila con el botón a la derecha.

#### Tabla (`patients-table.tsx`)

| Columna        | Contenido                                                                                         |
| -------------- | ------------------------------------------------------------------------------------------------- |
| **Paciente**   | Nombre completo (en negrita) y debajo el email (texto atenuado); sin email, solo el nombre.       |
| **Documento**  | `formatDocument` ("Cédula 0102030405").                                                           |
| **Edad**       | "{edad} años" ("1 año" en singular) y debajo la fecha de nacimiento "dd/MM/yyyy" (atenuada).      |
| **Teléfono**   | `phone` o "—".                                                                                    |
| **Estado**     | `Badge`: **Activo** (variante por defecto) o **Inactivo** (`secondary`).                          |
| Acciones (`sr-only` "Acciones") | `PatientRowActions`.                                                             |

- Contenedor con scroll horizontal de shadcn (`Table`) para móvil.
- Orden: el de la API (apellido y nombre).

| Estado                       | Qué se ve                                                                                                       |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Primera carga                | 5 filas de `Skeleton`.                                                                                          |
| Error al cargar              | `Alert` `destructive` "No se pudieron cargar los pacientes." con el detalle de **Mensajes** y **Reintentar**.   |
| Sin pacientes y sin filtros  | "Todavía no hay pacientes registrados."                                                                         |
| Sin resultados con filtros   | "No se encontraron pacientes con esos filtros."                                                                 |
| Con datos                    | Filas y, debajo, la paginación.                                                                                 |

**Paginación** (solo si `count > 0`): "Página {pageIndex + 1} de {totalPages} · {count} pacientes" y botones **Anterior**/**Siguiente** (`ChevronLeft`/`ChevronRight`), deshabilitados en los extremos.

#### Acciones de la fila (`patient-row-actions.tsx`)

- Botón de icono `Pencil` con `aria-label` y tooltip "Editar a {nombre completo}": abre el modal de edición. También con el paciente inactivo.
- `DropdownMenu` con botón de icono `MoreHorizontal` (`aria-label` "Más acciones de {nombre completo}") y un ítem: **Desactivar** (icono `UserX`) si está activo, o **Activar** (icono `UserCheck`) si está inactivo. Abre `PatientStatusDialog`.

### Formulario (`patient-form.tsx`)

Mismas reglas que el validador y los value objects del backend (`Document`, `FullName`, `Email`, `PhoneNumber`, `Address`, `BloodTypeNotation`, alergias ≤ 1000). Mensajes en español, uno por campo (el primero que falle). Esquema orientativo:

```ts
const optionalText = (max: number, message: string) =>
  z.string().trim().max(max, message) // vacío → null al enviar

const patientSchema = z
  .object({
    documentType: z.enum(['NationalId', 'Passport']),
    documentNumber: z.string().trim().min(1, 'Ingresa el número de documento.').max(20, 'El documento no puede superar 20 caracteres.'),
    firstName: z.string().trim().min(1, 'Ingresa el nombre.').max(100, 'El nombre no puede superar 100 caracteres.'),
    lastName: z.string().trim().min(1, 'Ingresa el apellido.').max(100, 'El apellido no puede superar 100 caracteres.'),
    // "yyyy-MM-dd" del input date; no posterior a hoy (fecha local)
    birthDate: z.string().min(1, 'Ingresa la fecha de nacimiento.'), // + refine: no futura → 'La fecha de nacimiento no puede ser futura.'
    gender: z.enum(['Male', 'Female', 'Other'], 'Selecciona el género.'),
    email: z.string().trim().max(254, 'El email no puede superar 254 caracteres.'), // + refine: vacío o z.email → 'Ingresa un email válido.'
    phone: z.string().trim(), // + refine: vacío o /^[0-9+\- ]{7,20}$/ → mismo mensaje que en Doctores
    street: optionalText(200, 'La calle no puede superar 200 caracteres.'),
    city: optionalText(100, 'La ciudad no puede superar 100 caracteres.'),
    state: optionalText(100, 'La provincia no puede superar 100 caracteres.'),
    reference: optionalText(250, 'La referencia no puede superar 250 caracteres.'),
    bloodType: z.enum([...bloodTypes, 'none']), // 'none' → null
    allergies: optionalText(1000, 'Las alergias no pueden superar 1000 caracteres.'),
  })
  // Dirección: si se llena algún campo, calle y ciudad son obligatorias
  .superRefine(/* 'Ingresa la calle.' en street y 'Ingresa la ciudad.' en city */)
```

- Al enviar se construye el `PatientRequest`: textos recortados; opcionales vacíos → `null`; `address` → `null` si los cuatro campos están vacíos, si no `{ street, city, state, reference }` con `state`/`reference` vacíos → `null`; `bloodType` `'none'` → `null`. El número de documento se envía tal cual tras el `trim` (distingue mayúsculas).
- Valores por defecto: alta, `documentType: 'NationalId'`, `gender` sin valor (placeholder "Selecciona"), `bloodType: 'none'` y textos vacíos; edición, los del `PatientDto` (`null` → `''`, `bloodType` `null` → `'none'`, `address` `null` → cuatro campos vacíos).

Secciones del formulario (con un título pequeño cada una):

| Sección              | Campos                                                                                                                         |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| **Identificación**   | **Tipo de documento** (`Select`: Cédula, Pasaporte) y **Número de documento** (`autoComplete="off"`) en una fila desde `sm`; **Nombre** y **Apellido** en una fila; **Fecha de nacimiento** (`<Input type="date">` con `max` = hoy local) y **Género** (`Select`: Masculino, Femenino, Otro) en una fila. |
| **Contacto**         | **Email (opcional)** (`type="email"`) y **Teléfono (opcional)** (`type="tel"`) en una fila desde `sm`.                          |
| **Dirección (opcional)** | **Calle** a todo el ancho; **Ciudad** y **Provincia** en una fila; **Referencia** a todo el ancho. `FieldDescription`: "Si la indicas, la calle y la ciudad son obligatorias." |
| **Datos clínicos**   | **Tipo de sangre** (`Select`: "Sin especificar" y los 8 tipos); **Alergias (opcional)** en `Textarea` (3 filas) con contador "{n}/1000". |

| Elemento          | Detalle                                                                                                                                         |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Botones           | **Cancelar** y **Guardar** (`type="submit"`), fijos al pie del modal. Con la petición en curso: ambos deshabilitados y **Guardar** con `Loader2` y "Guardando…". |
| Error de la API   | 409 → `setError('documentNumber', 'Ya existe un paciente con ese documento.')`; el resto, `Alert` `destructive` encima de los campos (ver **Mensajes**). Los valores se conservan. |

Como `DoctorForm`, recibe `defaultValues` y `onSubmit(request: PatientRequest)` que devuelve el `ApiError` o `null`.

### Modal de alta y edición (`patient-form-dialog.tsx`)

| Modo     | Título              | Descripción                               | Al abrir                                                                                                                                      | Al guardar con éxito                                                         |
| -------- | ------------------- | ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Alta     | **Nuevo paciente**  | "Registra un paciente activo."            | Formulario vacío.                                                                                                                             | `POST /api/patients` → cierra, toast "Paciente creado." y refresca la tabla. |
| Edición  | **Editar paciente** | "Modifica los datos de {nombre completo}." | `GET /api/patients/{id}` con skeleton de los campos. Error → `Alert` con **Reintentar** en el modal; 404 → cierra, toast "El paciente ya no existe." y refresca. | `PUT /api/patients/{id}` → cierra, toast "Paciente actualizado." y refresca. |

- El modal es más ancho que el de Doctores (`sm:max-w-2xl`) y su cuerpo tiene scroll vertical (`max-h` respecto a la altura de la ventana) con el título y los botones siempre visibles.
- Mientras se guarda, no se cierra con `Esc` ni clic fuera.
- Un 404 al guardar la edición actúa como el 404 al cargar.

### Confirmación de activar/desactivar (`patient-status-dialog.tsx`)

| Acción       | Título                              | Descripción                                                          | Botón                          | Éxito                          |
| ------------ | ----------------------------------- | -------------------------------------------------------------------- | ------------------------------ | ------------------------------ |
| Desactivar   | ¿Desactivar a {nombre completo}?    | No se le podrán agendar citas nuevas. Podrás activarlo de nuevo.     | **Desactivar** (`destructive`) | toast "Paciente desactivado."  |
| Activar      | ¿Activar a {nombre completo}?       | Se le podrán volver a agendar citas.                                 | **Activar**                    | toast "Paciente activado."     |

- Mismo comportamiento que `DoctorStatusDialog`: **Cancelar** no llama a la API; confirmar muestra `Loader2`, deshabilita los botones y al terminar cierra, muestra el toast y refresca.
- 400 → "El paciente ya estaba activo." / "El paciente ya estaba inactivo." y refresco; 404 → "El paciente ya no existe." y refresco; el resto, según **Mensajes**.

### Mensajes de error (`patient-messages.ts`)

| Error                           | Mensaje                                                                     |
| ------------------------------- | --------------------------------------------------------------------------- |
| `status === 409` (alta/edición) | En el campo número de documento: "Ya existe un paciente con ese documento." |
| `status === 400` (alta/edición) | Revisa los datos e inténtalo de nuevo.                                      |
| `status === 403`                | No tienes permiso para realizar esta acción.                                |
| `status === 404`                | El paciente ya no existe.                                                   |
| `kind === 'network'`            | No se pudo conectar con el servidor.                                        |
| Cualquier otro (500, …)         | Ocurrió un error inesperado. Inténtalo de nuevo.                            |

El 401 lo sigue gestionando el manejador global.

### Tests

Mismas pautas que el spec 06 (`fetch` simulado, `user-event`, `MemoryRouter`, `TestAuthProvider`, `TooltipProvider`, `<Toaster />`, limpieza en `afterEach`). Para la fecha de "hoy", `vi.setSystemTime` o un `today` inyectado en `getAge`.

| Archivo                           | Casos                                                                                                                                                                                                                                                                                                                                                           |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `use-debounced-value.test.ts`     | (movido, sin cambios en los casos)                                                                                                                                                                                                                                                                                                                              |
| `patient-labels.test.ts`          | `getAge` antes y después del cumpleaños, el día del cumpleaños y menor de un año; `formatDocument` con cédula y pasaporte; `formatBirthDate`.                                                                                                                                                                                                                     |
| `nav-items.test.ts`               | (se añade) `Admin` y `Receptionist` tienen **Pacientes** (`/patients`); `Doctor` no.                                                                                                                                                                                                                                                                            |
| `patient-form.test.tsx`           | Enviar vacío → mensajes de documento, nombre, apellido, fecha y género, sin `onSubmit`; fecha futura; email y teléfono inválidos; ciudad sin calle → "Ingresa la calle."; alergias de más de 1000; datos válidos → `onSubmit` con textos recortados, opcionales vacíos como `null`, `address: null` sin dirección y `bloodType: null` con "Sin especificar"; dirección y tipo de sangre completos; valores por defecto de edición; 409 → mensaje bajo el documento; 400 y red → `Alert`; "Guardando…". |
| `patient-status-dialog.test.tsx`  | Desactivar: título y descripción; **Cancelar** no llama a la API; confirmar → `POST …/deactivate`, toast y `onDone`; 400 → "El paciente ya estaba inactivo."; activar → `POST …/activate`.                                                                                                                                                                       |
| `patients-page.test.tsx`          | Primera carga → `GET /api/patients?pageIndex=0&pageSize=10` y filas con nombre, email, documento, edad, teléfono ("—" si falta) y estado; búsqueda con debounce y `pageIndex=0`; **Inactivos** → `isActive=false`; paginación; vacío sin filtros y sin resultados; error → **Reintentar**; alta completa (`POST`, toast "Paciente creado." y refresco); el lápiz carga `GET /api/patients/{id}`, rellena el formulario y guarda con `PUT`; desactivar desde el menú ⋯. |
| `app-routes.test.tsx`             | (se añade) Admin y Recepción ven **Pacientes** y, al pulsarlo, la página con el título **Pacientes**; un `Doctor` en `/patients` termina en `/`.                                                                                                                                                                                                                  |

## Implementation plan

1. Mover `PaginatedResult` a `src/lib/api/paginated-result.ts` y `useDebouncedValue` (con su test) a `src/hooks/`, y actualizar los imports de `features/doctors/`. Verificar: `pnpm build`, `pnpm lint` y `pnpm test` pasan sin cambios de comportamiento.
2. Consultar con Context7 la documentación actual del `textarea` de shadcn/ui y añadirlo con el CLI (corregir el import de `cn` a `@/lib/utils`; revisar que `package.json` no cambia). Verificar: `pnpm build` pasa.
3. Crear `patient-types.ts`, `patients-api.ts`, `patient-messages.ts` y `patient-labels.ts` con su test. Verificar: `pnpm test` pasa.
4. Añadir **Pacientes** a `nav-items.ts` (y su test) y la ruta `/patients` con una `PatientsPage` mínima (solo el listado). Verificar: `pnpm build` y `pnpm test` pasan.
5. Completar el listado: `patients-toolbar.tsx`, `patients-table.tsx` (estados y paginación), búsqueda con debounce, filtro de estado y `patient-row-actions.tsx`. Verificar: `pnpm test` pasa con los casos de listado de `patients-page.test.tsx`.
6. Crear `patient-form.tsx` con su test. Verificar: `pnpm test` pasa.
7. Crear `patient-form-dialog.tsx` y conectarlo a **Nuevo paciente** y al lápiz de la fila. Verificar: `pnpm test` pasa con los casos de alta y edición.
8. Crear `patient-status-dialog.tsx` con su test y conectarlo al menú ⋯. Completar `patients-page.test.tsx` y `app-routes.test.tsx`. Verificar: `pnpm build`, `pnpm lint`, `pnpm exec prettier --check . --end-of-line auto` y `pnpm test` pasan.
9. Probar a mano con el backend corriendo (`dotnet run`, datos de ejemplo) y `pnpm dev`: como Admin y como recepcionista, buscar por nombre y por documento, filtrar, paginar, crear con y sin dirección, crear con documento repetido (409), crear con fecha futura (bloqueado en el formulario), editar quitando la dirección y el tipo de sangre (quedan `null`), desactivar y activar; como doctor, que no hay menú y `/patients` lleva al inicio; ancho de móvil (modal con scroll); tema claro y oscuro.
10. Actualizar `frontend/CLAUDE.md`:
    - **Estado actual:** menú **Pacientes** (spec 08) para Admin y Recepción.
    - **Stack/estado:** `textarea` en la lista de componentes de shadcn.
    - **Estructura:** `features/patients/`, `use-debounced-value` en `src/hooks/`, `paginated-result` en `src/lib/api/` y el cambio en la lista de `features/doctors/`.
    - **Backlog:** fila 08 con este spec.

## Acceptance criteria

- [ ] `package.json` no cambia; `textarea.tsx` está en `src/components/ui/`.
- [ ] `PaginatedResult` vive en `src/lib/api/paginated-result.ts` y `useDebouncedValue` en `src/hooks/`; `features/doctors/` ya no los declara y sus tests siguen pasando.
- [ ] Admin y Recepción ven el menú **Pacientes** en el sidebar; un `Doctor` no lo ve y, si entra a `/patients`, termina en `/`.
- [ ] `/patients` muestra el título **Pacientes** y la tabla con Paciente (nombre y email), Documento ("Cédula 0102030405"), Edad ("35 años" y la fecha "dd/MM/yyyy"), Teléfono ("—" si no tiene) y Estado (**Activo**/**Inactivo**).
- [ ] La primera carga hace `GET /api/patients?pageIndex=0&pageSize=10` y muestra un skeleton mientras tanto.
- [ ] Escribir en la búsqueda hace una sola petición con `search` 300 ms después de dejar de escribir y vuelve a la primera página.
- [ ] El filtro **Activos**/**Inactivos** envía `isActive=true`/`false`; **Todos** no envía `isActive`.
- [ ] La paginación muestra "Página X de Y · N pacientes" y **Anterior**/**Siguiente** se deshabilitan en los extremos.
- [ ] Sin pacientes se ve "Todavía no hay pacientes registrados."; sin resultados con filtros, "No se encontraron pacientes con esos filtros."; un error de carga muestra un aviso con **Reintentar**.
- [ ] **Nuevo paciente** abre un modal con las secciones Identificación, Contacto, Dirección y Datos clínicos; enviarlo vacío o con fecha futura, email, teléfono, dirección incompleta o longitudes inválidas muestra los mensajes sin hacer peticiones.
- [ ] Con datos válidos se hace `POST /api/patients` con textos recortados, opcionales vacíos como `null`, `address: null` sin dirección y `bloodType` en notación (`"O+"`) o `null`; se cierra el modal, sale el toast "Paciente creado." y la tabla se refresca.
- [ ] Un documento repetido (409) muestra "Ya existe un paciente con ese documento." bajo el número y conserva los valores.
- [ ] El botón lápiz de cada fila pide `GET /api/patients/{id}`, rellena el formulario y guarda con `PUT /api/patients/{id}`; toast "Paciente actualizado." y refresco. Vaciar un opcional al editar lo envía como `null`.
- [ ] **Desactivar** y **Activar** (menú ⋯) piden confirmación; **Cancelar** no hace nada; confirmar llama a `POST …/deactivate` o `…/activate`, muestra el toast y actualiza el estado en la tabla.
- [ ] Un 404 en editar, activar o desactivar muestra "El paciente ya no existe." y refresca la tabla.
- [ ] Sin conexión, las acciones muestran "No se pudo conectar con el servidor.".
- [ ] En móvil la tabla se desplaza en horizontal y el modal tiene scroll con los botones visibles; se ve bien en tema claro y oscuro.
- [ ] `pnpm build`, `pnpm lint`, el chequeo de Prettier y `pnpm test` terminan sin errores.
- [ ] `frontend/CLAUDE.md` refleja la estructura nueva y tiene la fila 08 en el backlog.
- [ ] No hay cambios fuera de `frontend/`.

## Decisions

- **Yes:** menú para Admin y Recepción, ambos con todas las acciones. Elegido por el usuario: coincide con la política `Reception` de escritura.
- **No:** menú de solo lectura para el `Doctor`; verá pacientes desde las consultas en otro spec.
- **Yes:** activar y desactivar con confirmación, como en Doctores. Elegido por el usuario.
- **Yes:** botón lápiz visible por fila para editar y menú ⋯ solo con Activar/Desactivar. Elegido por el usuario: la edición es la acción principal pedida.
- **No:** todo en el menú ⋯, ni abrir la edición con clic en la fila.
- **Yes:** el formulario con todos los campos de la API. Elegido por el usuario: el `PUT` reemplaza todo, así que un formulario parcial borraría la dirección, el tipo de sangre o las alergias.
- **No:** formulario solo con obligatorios y contacto.
- **Yes:** columnas Paciente, Documento, Edad (con la fecha debajo), Teléfono y Estado. Elegido por el usuario.
- **No:** tipo de sangre y género en la tabla (solo en el formulario).
- **Yes:** `<input type="date">` nativo para la fecha de nacimiento. Elegido por el usuario: sin dependencias y rápido para años lejanos.
- **No:** date picker de shadcn (`react-day-picker`, `date-fns`).
- **Yes:** la edición carga el detalle con `GET /api/patients/{id}`. Elegido por el usuario, igual que Doctores.
- **No:** rellenar con los datos de la fila.
- **Yes:** alergias en un `Textarea` de shadcn con contador. Elegido por el usuario; no añade paquetes.
- **Yes:** la fecha futura se valida con la fecha local; el backend usa la UTC. La diferencia solo afecta a quien nace "hoy" cerca de medianoche y el 400 se muestra igual.
- **Yes:** dirección como cuatro campos sueltos en el formulario; todos vacíos → `address: null`, alguno lleno → calle y ciudad obligatorias. Refleja que `address` es un VO opcional con `street` y `city` obligatorios.
- **Yes:** tipo de sangre con la opción "Sin especificar" → `null`, para poder borrarlo al editar.
- **Yes:** el 409 bajo el número de documento: es el único 409 posible.
- **Yes:** mover `PaginatedResult` a `src/lib/api/` y `useDebouncedValue` a `src/hooks/`, como pide la regla de compartidos al llegar la segunda feature.
- **No:** extraer ahora tabla, barra o diálogo de estado genéricos con Doctores; se duplica el patrón y se evalúa cuando llegue un tercer listado.
- **Yes:** página de tamaño fijo 10 y filtros en estado local, como en Doctores.

## Risks

| Risk                                                                                       | Mitigation                                                                                                                                      |
| ------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| El formulario es largo y no cabe en pantallas pequeñas                                     | Modal `sm:max-w-2xl` con cuerpo con scroll y botones fijos al pie; se revisa en el ancho de móvil (paso 9).                                     |
| `new Date('yyyy-MM-dd')` interpreta la fecha en UTC y puede mostrar el día anterior        | `formatBirthDate` y `getAge` parten la cadena sin crear un `Date` UTC; casos en `patient-labels.test.ts`.                                       |
| El `<input type="date">` se ve distinto según el navegador y en jsdom no abre el selector  | Se acepta el aspecto nativo; los tests escriben la fecha con `user.type` o `fireEvent.change` sobre el input.                                    |
| Mover `PaginatedResult` y `useDebouncedValue` rompe imports de Doctores                    | Paso 1 aislado: solo mueve y compila/prueba antes de empezar Pacientes.                                                                          |
| El CLI de shadcn añade dependencias o pregunta si sobrescribe componentes                   | Se revisa el diff tras el paso 2: `package.json` sin cambios y componentes existentes conservados.                                              |
| Select de Radix con valor vacío (`gender` sin elegir) no admite `value=""`                  | `gender` empieza como `undefined` con placeholder y Zod da "Selecciona el género."; `bloodType` usa el centinela `'none'`.                       |

## What is **not** in this spec

- El menú para el rol `Doctor`.
- Página de detalle del paciente e historia clínica.
- Búsqueda por documento exacto y agendar citas desde el listado.
- Filtros en la URL, ordenación, tamaño de página variable y date picker con calendario.
- Componentes genéricos compartidos con Doctores.
- Tests E2E.
- Cambios en el backend.

Cada uno de ellos, si llega, va en su propio spec.

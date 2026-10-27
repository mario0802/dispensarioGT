# Dispensario Médico

Backend en .NET 10 para gestionar un dispensario médico: doctores, pacientes, citas y consultas.
Arquitectura, plan y convenciones en [`docs/`](docs/) y [`CLAUDE.md`](CLAUDE.md).

## Arranque local
```bash
dotnet user-secrets set "Jwt:Key" "<min-32-chars>" --project src/Services/Dispensario/Dispensario.Api
dotnet user-secrets set "Seed:Admin:Password" "<password>" --project src/Services/Dispensario/Dispensario.Api
dotnet run --project src/Services/Dispensario/Dispensario.Api
```
En Development se crea `dispensario.db` con el usuario Admin (`admin@dispensario.local`) y se abre Scalar en `/scalar`.

## Endpoints
Todas las rutas usan JSON; los enums viajan como texto (`"Admin"`, `"NationalId"`).
Los errores responden con `application/problem+json` (ProblemDetails). Las rutas protegidas piden la cabecera `Authorization: Bearer <accessToken>`.

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| `POST` | `/api/auth/login` | Anónimo | Inicia sesión y devuelve un JWT |
| `POST` | `/api/auth/register` | Anónimo | Autoregistro de un recepcionista |
| `GET` | `/api/auth/me` | Autenticado | Datos del usuario del token |
| `POST` | `/api/users` | `AdminOnly` | Registra un usuario de cualquier rol |
| `GET` | `/api/users` | `AdminOnly` | Lista paginada de usuarios con búsqueda y filtros |
| `GET` | `/api/users/{id}` | `AdminOnly` | Detalle de un usuario |
| `PUT` | `/api/users/{id}` | `AdminOnly` | Cambia el nombre y el teléfono de un usuario |
| `PUT` | `/api/users/{id}/password` | `AdminOnly` | Resetea la contraseña de otro usuario |
| `POST` | `/api/users/{id}/deactivate` | `AdminOnly` | Desactiva un usuario |
| `POST` | `/api/users/{id}/activate` | `AdminOnly` | Reactiva un usuario |
| `POST` | `/api/users/{id}/link-doctor` | `AdminOnly` | Vincula un usuario `Doctor` con su ficha de doctor |
| `PUT` | `/api/users/me` | Autenticado | Cambia el nombre y el teléfono propios |
| `PUT` | `/api/users/me/password` | Autenticado | Cambia la contraseña propia |
| `POST` | `/api/doctors` | `AdminOnly` | Registra un doctor |
| `GET` | `/api/doctors` | Autenticado | Lista paginada de doctores con búsqueda y filtros |
| `GET` | `/api/doctors/{id}` | Autenticado | Detalle de un doctor |
| `PUT` | `/api/doctors/{id}` | `AdminOnly` | Edita los datos de un doctor |
| `POST` | `/api/doctors/{id}/deactivate` | `AdminOnly` | Desactiva un doctor |
| `POST` | `/api/doctors/{id}/activate` | `AdminOnly` | Reactiva un doctor |
| `PUT` | `/api/doctors/{id}/schedule` | `AdminOrDoctor` (el doctor, solo el suyo) | Reemplaza el horario semanal de un doctor |
| `GET` | `/api/doctors/{id}/schedule` | Autenticado | Horario semanal de un doctor |
| `POST` | `/api/patients` | `Reception` | Registra un paciente |
| `GET` | `/api/patients` | Autenticado | Lista paginada de pacientes con búsqueda y filtro de estado |
| `GET` | `/api/patients/{id}` | Autenticado | Detalle de un paciente |
| `GET` | `/api/patients/by-document` | Autenticado | Busca un paciente por su documento exacto |
| `PUT` | `/api/patients/{id}` | `Reception` | Edita los datos de un paciente, incluido su documento |
| `POST` | `/api/patients/{id}/deactivate` | `Reception` | Desactiva un paciente |
| `POST` | `/api/patients/{id}/activate` | `Reception` | Reactiva un paciente |
| `POST` | `/api/appointments` | `Reception` | Agenda una cita en el horario del doctor |
| `GET` | `/api/appointments` | Autenticado (el doctor, solo las suyas) | Lista paginada de citas con filtros |
| `GET` | `/api/appointments/{id}` | Autenticado (el doctor, solo las suyas) | Detalle de una cita |
| `POST` | `/api/appointments/{id}/confirm` | `Reception` | Confirma una cita agendada |
| `POST` | `/api/appointments/{id}/cancel` | Autenticado (el doctor, solo las suyas) | Cancela una cita con un motivo |
| `POST` | `/api/appointments/{id}/no-show` | Autenticado (el doctor, solo las suyas) | Marca una cita como no asistida |
| `PUT` | `/api/appointments/{id}/schedule` | `Reception` | Reprograma una cita a otra hora |
| `GET` | `/health` | Anónimo | Estado de la Api y de la base de datos |
| `GET` | `/scalar`, `/openapi/v1.json` | Solo Development | Referencia interactiva y documento OpenAPI |

Políticas de autorización:

| Política | Roles |
|---|---|
| `AdminOnly` | `Admin` |
| `Reception` | `Admin`, `Receptionist` |
| `Clinical` | `Admin`, `Doctor` |
| `AdminOrDoctor` | `Admin`, `Doctor` (el handler comprueba que el doctor actúe sobre su propia ficha) |

### Auth

#### `POST /api/auth/login`
Autentica con email y contraseña. El email no distingue mayúsculas.

```json
// Petición
{ "email": "admin@dispensario.local", "password": "<password>" }

// 200 OK
{ "accessToken": "eyJhbGciOi...", "tokenType": "Bearer", "expiresAtUtc": "2026-10-26T15:00:00Z" }
```

| Código | Cuándo |
|---|---|
| 200 | Credenciales correctas |
| 400 | Email vacío, demasiado largo o con formato inválido, o contraseña vacía |
| 401 | `Invalid credentials.`: email inexistente, contraseña incorrecta o usuario inactivo (el mismo mensaje en los tres casos) |

El token caduca a los `Jwt:ExpirationMinutes` (60 por defecto) y lleva los claims `sub`, `email`, `name`, `role` y, si el usuario es doctor, `doctor_id`.

#### `POST /api/auth/register`
Autoregistro público: crea siempre un usuario `Receptionist` activo. Si el cuerpo trae un `role`, se ignora. No devuelve token: después se hace login.

```json
// Petición
{
  "email": "laura.vega@dispensario.local",
  "password": "Passw0rd",
  "firstName": "Laura",
  "lastName": "Vega",
  "documentType": "NationalId",
  "documentNumber": "1712345678",
  "phone": "0991234567"
}

// 201 Created  (Location: /api/users/0b6f…)
{ "id": "0b6f…" }
```

| Código | Cuándo |
|---|---|
| 201 | Usuario creado |
| 400 | Datos inválidos (`ValidationErrors`) o teléfono con formato inválido |
| 409 | El email (sin distinguir mayúsculas) o la identificación ya están registrados |

`phone` es opcional. La contraseña debe tener de 8 a 72 caracteres, con al menos una letra y un dígito. La misma regla aplica al alta por el Admin, al cambio y al reseteo.

#### `GET /api/auth/me`
Devuelve los datos actuales del usuario del token, leídos de la base de datos.

```json
// 200 OK
{
  "id": "0b6f…",
  "email": "admin@dispensario.local",
  "firstName": "System",
  "lastName": "Admin",
  "identificationType": "NationalId",
  "identificationNumber": "0000000000",
  "phone": null,
  "role": "Admin",
  "doctorId": null,
  "isActive": true
}
```

| Código | Cuándo |
|---|---|
| 200 | Token válido y usuario activo |
| 401 | Sin token, token inválido o caducado, o usuario desactivado |
| 404 | El usuario del token ya no existe |

### Users
Todas las rutas con `{id}` esperan un GUID. Las de `AdminOnly` responden **401** sin token y **403** con un token de `Doctor` o `Receptionist`; las de `/api/users/me` aceptan cualquier usuario autenticado. Las respuestas de usuario tienen esta forma:

```json
{
  "id": "0b6f…",
  "email": "laura.vega@dispensario.local",
  "firstName": "Laura",
  "lastName": "Vega",
  "identificationType": "NationalId",
  "identificationNumber": "1712345678",
  "phone": "0991234567",
  "role": "Doctor",
  "doctorId": null,
  "isActive": true,
  "createdAtUtc": "2026-10-26T15:00:00Z"
}
```

#### `POST /api/users` — `AdminOnly`
Registra un usuario de cualquier rol (`Admin`, `Doctor`, `Receptionist`). Un `Doctor` queda sin `doctorId` hasta que se vincula con `POST /api/users/{id}/link-doctor`.

```json
// Petición: el mismo cuerpo que /api/auth/register más "role" (obligatorio)
{
  "email": "luis.mora@dispensario.local", "password": "Passw0rd",
  "firstName": "Luis", "lastName": "Mora",
  "documentType": "Passport", "documentNumber": "AB123456",
  "phone": null, "role": "Doctor"
}

// 201 Created  (Location: /api/users/0b6f…)
{ "id": "0b6f…" }
```

| Código | Cuándo |
|---|---|
| 201 | Usuario creado |
| 400 | Datos inválidos, sin `role` o con un `role` desconocido |
| 401 / 403 | Sin token / el usuario no es Admin |
| 409 | El email o la identificación ya están registrados |

#### `GET /api/users` — `AdminOnly`
Lista paginada, ordenada por apellido y nombre. Parámetros de query, todos opcionales:

| Parámetro | Por defecto | Descripción |
|---|---|---|
| `pageIndex` | `0` | Página, desde 0 |
| `pageSize` | `10` | Entre 1 y 100 |
| `search` | — | Contiene, sin distinguir mayúsculas, en nombre, apellido, email o número de identificación |
| `role` | — | `Admin`, `Doctor` o `Receptionist` |
| `isActive` | — | `true` o `false` |

```json
// GET /api/users?role=Doctor&search=mora → 200 OK
{ "pageIndex": 0, "pageSize": 10, "count": 1, "data": [ { "id": "0b6f…", "email": "luis.mora@dispensario.local", "...": "..." } ] }
```

| Código | Cuándo |
|---|---|
| 200 | Siempre que la paginación sea válida (lista vacía si nada coincide) |
| 400 | `pageIndex` negativo o `pageSize` fuera de 1–100 |
| 401 / 403 | Sin token / el usuario no es Admin |

#### `GET /api/users/{id}` — `AdminOnly`
Devuelve el usuario (forma de arriba).

| Código | Cuándo |
|---|---|
| 200 | Usuario encontrado |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un usuario con ese ID |

#### `PUT /api/users/{id}` — `AdminOnly`
Cambia el nombre y el teléfono. El email, la identificación y el rol no se pueden cambiar.

```json
// Petición: "phone": null borra el teléfono
{ "firstName": "Luis", "lastName": "Mora Ruiz", "phone": null }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Perfil actualizado |
| 400 | Nombre o apellido vacíos o demasiado largos, o teléfono inválido |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un usuario con ese ID |

#### `PUT /api/users/{id}/password` — `AdminOnly`
Fija una contraseña nueva a otro usuario sin conocer la actual. También funciona sobre usuarios inactivos.

```json
// Petición
{ "newPassword": "Reset-Passw0rd" }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Contraseña cambiada |
| 400 | Contraseña débil, o el ID es el del propio Admin (debe usar `PUT /api/users/me/password`) |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un usuario con ese ID |

#### `POST /api/users/{id}/deactivate` — `AdminOnly`
Desactiva al usuario: ya no puede iniciar sesión. Sin cuerpo. Los tokens ya emitidos siguen valiendo hasta que caducan.

| Código | Cuándo |
|---|---|
| 204 | Usuario desactivado |
| 400 | El usuario ya está inactivo, o es el propio usuario del token |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un usuario con ese ID |
| 409 | Es el último Admin activo |

#### `POST /api/users/{id}/activate` — `AdminOnly`
Vuelve a activar a un usuario desactivado. Sin cuerpo.

| Código | Cuándo |
|---|---|
| 204 | Usuario activado |
| 400 | El usuario ya está activo |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un usuario con ese ID |

#### `POST /api/users/{id}/link-doctor` — `AdminOnly`
Vincula un usuario con rol `Doctor` con su ficha de doctor. El vínculo es definitivo en v1 (no hay desvinculación). El claim `doctor_id` solo aparece en los tokens emitidos después: el usuario debe volver a hacer login.

```json
// Petición
{ "doctorId": "3c1a…" }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Usuario vinculado |
| 400 | `doctorId` vacío, el usuario no tiene rol `Doctor`, ya está vinculado a un doctor, o el doctor está inactivo |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe el usuario o el doctor |
| 409 | El doctor ya está vinculado a otro usuario |

#### `PUT /api/users/me` — Autenticado
El usuario del token cambia su nombre y su teléfono. Mismo cuerpo que `PUT /api/users/{id}`.

| Código | Cuándo |
|---|---|
| 204 | Perfil actualizado |
| 400 | Datos inválidos |
| 401 | Sin token o usuario desactivado |
| 404 | El usuario del token ya no existe |

#### `PUT /api/users/me/password` — Autenticado
El usuario del token cambia su contraseña. La nueva debe ser distinta de la actual.

```json
// Petición
{ "currentPassword": "Passw0rd", "newPassword": "Changed-Passw0rd" }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Contraseña cambiada |
| 400 | `Current password is incorrect.`, o nueva contraseña débil o igual a la actual |
| 401 | Sin token o usuario desactivado |
| 404 | El usuario del token ya no existe |

### Doctors
Todas las rutas con `{id}` esperan un GUID. Las de escritura son `AdminOnly` (**401** sin token, **403** con un token de `Doctor` o `Receptionist`), salvo el horario, que también puede cambiar el propio doctor; las lecturas aceptan cualquier usuario autenticado. Las respuestas de doctor tienen esta forma:

```json
{
  "id": "3c1a…",
  "firstName": "Luis",
  "lastName": "Mora",
  "licenseNumber": "MP-12345",
  "specialty": "Pediatría",
  "email": "luis.mora@dispensario.local",
  "phone": "0991234567",
  "appointmentDurationMinutes": 30,
  "isActive": true,
  "userId": null,
  "createdAtUtc": "2026-10-26T15:00:00Z"
}
```

`userId` es el usuario vinculado con la ficha, o `null` si el doctor no tiene cuenta.

#### `POST /api/doctors` — `AdminOnly`
Registra un doctor activo. La matrícula se guarda sin espacios alrededor y en mayúsculas, y es única sin distinguir mayúsculas. El email es obligatorio (no tiene que ser único) y `phone` es opcional. `appointmentDurationMinutes` va de 10 a 120 en múltiplos de 5; si no se envía, vale 30.

```json
// Petición
{
  "firstName": "Luis",
  "lastName": "Mora",
  "licenseNumber": "mp-12345",
  "specialty": "Pediatría",
  "email": "luis.mora@dispensario.local",
  "phone": "0991234567",
  "appointmentDurationMinutes": 30
}

// 201 Created  (Location: /api/doctors/3c1a…)
{ "id": "3c1a…" }
```

| Código | Cuándo |
|---|---|
| 201 | Doctor creado |
| 400 | Datos inválidos (`ValidationErrors`): nombre, matrícula (máx. 30), especialidad (máx. 100) o email vacíos o inválidos, duración fuera de 10–120 o no múltiplo de 5; o teléfono con formato inválido |
| 401 / 403 | Sin token / el usuario no es Admin |
| 409 | La matrícula ya está registrada |

#### `GET /api/doctors` — Autenticado
Lista paginada, ordenada por apellido y nombre. Parámetros de query, todos opcionales:

| Parámetro | Descripción |
|---|---|
| `pageIndex` | Página, desde 0 (por defecto 0) |
| `pageSize` | Tamaño de página, de 1 a 100 (por defecto 10) |
| `search` | Texto contenido en el nombre, el apellido, la matrícula o la especialidad, sin distinguir mayúsculas |
| `specialty` | Especialidad exacta, sin distinguir mayúsculas |
| `isActive` | `true` o `false` |

```json
// GET /api/doctors?specialty=pediatría&pageSize=20 → 200 OK
{ "pageIndex": 0, "pageSize": 20, "count": 1, "data": [ { "id": "3c1a…", "lastName": "Mora", "…": "…" } ] }
```

| Código | Cuándo |
|---|---|
| 200 | Página de doctores |
| 400 | `pageIndex` negativo o `pageSize` fuera de 1–100 |
| 401 | Sin token |

#### `GET /api/doctors/{id}` — Autenticado
Devuelve un doctor con la forma de arriba.

| Código | Cuándo |
|---|---|
| 200 | Doctor encontrado |
| 401 | Sin token |
| 404 | No existe un doctor con ese ID |

#### `PUT /api/doctors/{id}` — `AdminOnly`
Reemplaza los datos del doctor; también funciona sobre un doctor inactivo. Mismo cuerpo que el alta. Si falta `appointmentDurationMinutes`, se conserva la duración actual; un `phone` null borra el teléfono.

| Código | Cuándo |
|---|---|
| 204 | Doctor actualizado |
| 400 | Datos inválidos, como en el alta; o la nueva duración es mayor que algún bloque del horario (`Appointment duration cannot be longer than a schedule block.`) |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un doctor con ese ID |
| 409 | La matrícula pertenece a otro doctor |

#### `POST /api/doctors/{id}/deactivate` — `AdminOnly`
Desactiva al doctor. Sin cuerpo. Su usuario vinculado, si lo tiene, no cambia.

| Código | Cuándo |
|---|---|
| 204 | Doctor desactivado |
| 400 | El doctor ya está inactivo |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un doctor con ese ID |

#### `POST /api/doctors/{id}/activate` — `AdminOnly`
Vuelve a activar un doctor desactivado. Sin cuerpo.

| Código | Cuándo |
|---|---|
| 204 | Doctor activado |
| 400 | El doctor ya está activo |
| 401 / 403 | Sin token / el usuario no es Admin |
| 404 | No existe un doctor con ese ID |

#### `PUT /api/doctors/{id}/schedule` — `AdminOrDoctor`
Reemplaza el horario semanal entero del doctor; `"blocks": []` lo borra. Lo usa el Admin con cualquier doctor, o un usuario `Doctor` con su propia ficha (el claim `doctor_id`, que solo llega en los tokens emitidos tras el vínculo).
Las horas van en formato `"HH:mm"`, en hora local del dispensario, con minutos múltiplo de 5 y el inicio antes del fin (un bloque no cruza la medianoche). Cada bloque dura al menos `appointmentDurationMinutes` y los bloques del mismo día no se solapan; dos bloques contiguos (`09:00–12:00` y `12:00–14:00`) sí valen.

```json
// Petición
{
  "blocks": [
    { "dayOfWeek": "Monday", "startTime": "09:00", "endTime": "12:00" },
    { "dayOfWeek": "Monday", "startTime": "14:00", "endTime": "18:00" }
  ]
}

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Horario reemplazado |
| 400 | Formato inválido: hora que no sigue `"HH:mm"` (`ValidationErrors`) o `dayOfWeek` desconocido; o regla del horario rota: minutos no múltiplo de 5, inicio ≥ fin, bloque más corto que la duración de la cita, bloques solapados o doctor inactivo |
| 401 | Sin token |
| 403 | `Receptionist`, o un `Doctor` sobre una ficha que no es la suya o sin ficha vinculada (también si el ID no existe) |
| 404 | No existe un doctor con ese ID (Admin) |

#### `GET /api/doctors/{id}/schedule` — Autenticado
Devuelve el horario del doctor, también si está inactivo. Los bloques salen de lunes a domingo y, dentro del día, por hora de inicio; sin horario, `blocks` es una lista vacía.

```json
// 200 OK
{
  "doctorId": "3c1a…",
  "blocks": [
    { "dayOfWeek": "Monday", "startTime": "09:00", "endTime": "12:00" },
    { "dayOfWeek": "Monday", "startTime": "14:00", "endTime": "18:00" }
  ]
}
```

| Código | Cuándo |
|---|---|
| 200 | Horario del doctor |
| 401 | Sin token |
| 404 | No existe un doctor con ese ID |

### Patients
Todas las rutas con `{id}` esperan un GUID. Las de escritura usan la política `Reception` (**401** sin token, **403** con un token de `Doctor`); las lecturas aceptan cualquier usuario autenticado. Las respuestas de paciente tienen esta forma:

```json
{
  "id": "7b2e…",
  "documentType": "NationalId",
  "documentNumber": "0102030405",
  "firstName": "Ana",
  "lastName": "Pérez",
  "birthDate": "1990-05-14",
  "gender": "Female",
  "email": "ana@example.com",
  "phone": "+593 99 123 4567",
  "address": { "street": "Av. Siempre Viva 742", "city": "Quito", "state": null, "reference": null },
  "bloodType": "O+",
  "allergies": "Penicillin",
  "isActive": true,
  "createdAtUtc": "2026-10-27T15:00:00Z"
}
```

- `documentType`: `NationalId` o `Passport`. El número se guarda sin espacios alrededor y **distingue mayúsculas** (`ab123` y `AB123` son documentos distintos). El documento (tipo + número) es único.
- `gender`: `Male`, `Female` u `Other`. `birthDate` va como `"yyyy-MM-dd"` y no puede ser posterior a hoy (fecha UTC).
- `bloodType` usa la notación clínica: `A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+` u `O-`, exactamente así (mayúsculas, sin espacios).
- `email`, `phone`, `address`, `bloodType` y `allergies` (máx. 1000 caracteres) son opcionales y salen como `null` si no hay valor.

#### `POST /api/patients` — `Reception`
Registra un paciente activo. Obligatorios: `documentType`, `documentNumber`, `firstName`, `lastName`, `birthDate` y `gender`. Si se envía `address`, `street` y `city` son obligatorios.

```json
// Petición
{
  "documentType": "NationalId",
  "documentNumber": "0102030405",
  "firstName": "Ana",
  "lastName": "Pérez",
  "birthDate": "1990-05-14",
  "gender": "Female",
  "email": "ana@example.com",
  "phone": "+593 99 123 4567",
  "address": { "street": "Av. Siempre Viva 742", "city": "Quito", "state": null, "reference": null },
  "bloodType": "O+",
  "allergies": "Penicillin"
}

// 201 Created  (Location: /api/patients/7b2e…)
{ "id": "7b2e…" }
```

| Código | Cuándo |
|---|---|
| 201 | Paciente creado |
| 400 | Datos inválidos (`ValidationErrors`): documento (máx. 20), nombre o apellido vacíos o largos, email inválido, dirección sin calle o ciudad, `bloodType` desconocido o alergias de más de 1000 caracteres. También sin `ValidationErrors`: `documentType`, `gender` o `birthDate` ilegibles, fecha de nacimiento futura o teléfono con formato inválido |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 409 | Otro paciente ya tiene ese documento (mismo tipo y número) |

#### `GET /api/patients` — Autenticado
Lista paginada, ordenada por apellido y nombre. Parámetros de query, todos opcionales:

| Parámetro | Descripción |
|---|---|
| `pageIndex` | Página, desde 0 (por defecto 0) |
| `pageSize` | Tamaño de página, de 1 a 100 (por defecto 10) |
| `search` | Texto contenido en el nombre, el apellido o el número de documento, sin distinguir mayúsculas |
| `isActive` | `true` o `false` |

```json
// GET /api/patients?search=pérez&isActive=true → 200 OK
{ "pageIndex": 0, "pageSize": 10, "count": 1, "data": [ { "id": "7b2e…", "lastName": "Pérez", "…": "…" } ] }
```

| Código | Cuándo |
|---|---|
| 200 | Página de pacientes |
| 400 | `pageIndex` negativo o `pageSize` fuera de 1–100 |
| 401 | Sin token |

#### `GET /api/patients/{id}` — Autenticado
Devuelve un paciente con la forma de arriba.

| Código | Cuándo |
|---|---|
| 200 | Paciente encontrado |
| 401 | Sin token |
| 404 | No existe un paciente con ese ID |

#### `GET /api/patients/by-document` — Autenticado
Busca un paciente por su documento exacto, también si está inactivo. Parámetros de query obligatorios: `type` (`NationalId` o `Passport`) y `number` (se recorta; distingue mayúsculas).

```json
// GET /api/patients/by-document?type=NationalId&number=0102030405 → 200 OK
{ "id": "7b2e…", "documentNumber": "0102030405", "…": "…" }
```

| Código | Cuándo |
|---|---|
| 200 | Paciente encontrado |
| 400 | Falta `type` o `number`, `type` desconocido o `number` en blanco |
| 401 | Sin token |
| 404 | No hay un paciente con ese documento |

#### `PUT /api/patients/{id}` — `Reception`
Reemplaza todos los datos del paciente, incluido el documento; también funciona sobre un paciente inactivo. Mismo cuerpo que el alta. Un opcional `null` u omitido (`email`, `phone`, `address`, `bloodType`, `allergies`) borra el valor actual.

| Código | Cuándo |
|---|---|
| 204 | Paciente actualizado |
| 400 | Datos inválidos, como en el alta |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe un paciente con ese ID |
| 409 | El documento pertenece a otro paciente |

#### `POST /api/patients/{id}/deactivate` — `Reception`
Desactiva al paciente. Sin cuerpo.

| Código | Cuándo |
|---|---|
| 204 | Paciente desactivado |
| 400 | El paciente ya está inactivo |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe un paciente con ese ID |

#### `POST /api/patients/{id}/activate` — `Reception`
Vuelve a activar un paciente desactivado. Sin cuerpo.

| Código | Cuándo |
|---|---|
| 204 | Paciente activado |
| 400 | El paciente ya está activo |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe un paciente con ese ID |

### Appointments
Todas las rutas con `{id}` esperan un GUID. Agendar, confirmar y reprogramar usan la política `Reception` (**401** sin token, **403** con un token de `Doctor`). Las lecturas, la cancelación y la no asistencia aceptan cualquier usuario autenticado, pero un `Doctor` solo actúa sobre las citas de su propia ficha: necesita estar vinculado (`POST /api/users/{id}/link-doctor`) y haber iniciado sesión después del vínculo. Las respuestas de cita tienen esta forma:

```json
{
  "id": "0193f1d0-…",
  "patientId": "0193f1c2-…",
  "patientFullName": "Ana Pérez",
  "doctorId": "0193f1b8-…",
  "doctorFullName": "Luis Andrade",
  "startUtc": "2026-11-03T14:30:00Z",
  "endUtc": "2026-11-03T15:00:00Z",
  "startLocal": "2026-11-03T09:30:00",
  "endLocal": "2026-11-03T10:00:00",
  "reason": "Control de presión arterial",
  "status": "Scheduled",
  "cancellationReason": null,
  "createdAtUtc": "2026-10-27T15:02:11Z"
}
```

- Las horas locales son las del dispensario (`Clinic:TimeZone`, por defecto `America/Guayaquil`, UTC−5) y viajan **sin offset**; las UTC llevan `Z`.
- `status`: `Scheduled`, `Confirmed`, `Completed`, `Cancelled` o `NoShow`. Las citas se crean `Scheduled`; `Completed` llegará al cerrar la consulta.
- `cancellationReason` solo tiene valor en las citas `Cancelled`.

Ciclo de vida de una cita:

```
Scheduled ──confirm──► Confirmed
    │                      │
    ├──cancel──────────────┼──► Cancelled
    ├──no-show─────────────┴──► NoShow
    └──schedule (reprogramar: Scheduled|Confirmed → Scheduled, con nuevas horas)
```

| Acción | Estado de origen | Cuándo |
|---|---|---|
| `confirm` | `Scheduled` | Antes de la hora de inicio |
| `cancel` | `Scheduled`, `Confirmed` | Antes de la hora de inicio |
| `no-show` | `Scheduled`, `Confirmed` | Desde la hora de inicio |
| `schedule` (reprogramar) | `Scheduled`, `Confirmed` | Antes de la hora de inicio actual; la nueva hora también debe ser futura |

`Cancelled` y `NoShow` son finales: cualquier acción sobre ellas responde **400**. Una acción fuera de su estado o de su momento también responde **400**.
- La duración de la cita es la del doctor (`appointmentDurationMinutes`): `endUtc = startUtc + duración`.

#### `POST /api/appointments` — `Reception`
Agenda una cita. Todos los campos son obligatorios. `startLocal` es la hora local del dispensario **sin offset** (`"yyyy-MM-ddTHH:mm"`); `reason` admite hasta 500 caracteres y se guarda sin espacios alrededor.

```json
// Petición
{
  "patientId": "0193f1c2-…",
  "doctorId": "0193f1b8-…",
  "startLocal": "2026-11-03T09:30",
  "reason": "Control de presión arterial"
}

// 201 Created  (Location: /api/appointments/0193f1d0-…)
{ "id": "0193f1d0-…" }
```

Reglas, en este orden:
1. El paciente y el doctor existen y están activos.
2. La cita es futura.
3. La cita cabe entera en un bloque del horario del doctor para ese día de la semana.
4. El inicio está **alineado a los slots del bloque**: los slots se cuentan desde el inicio del bloque en pasos de la duración. Con un bloque `09:00–12:00` y 30 minutos valen `09:00`, `09:30`… `11:30`; con un bloque que empieza a las `09:10`, valen `09:10`, `09:40`…
5. No se cruza con otra cita `Scheduled` o `Confirmed` del mismo doctor ni del mismo paciente. Una cita contigua (termina cuando empieza la otra) no es cruce.

| Código | Cuándo |
|---|---|
| 201 | Cita agendada |
| 400 | Datos inválidos (`ValidationErrors`): IDs vacíos, `startLocal` con `Z` u offset, `reason` vacío o de más de 500 caracteres. También sin `ValidationErrors`: `startLocal` ilegible, paciente o doctor inactivo, cita en el pasado, fuera del horario del doctor o desalineada |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe el paciente o el doctor |
| 409 | El doctor o el paciente ya tienen una cita que se cruza |

#### `GET /api/appointments` — Autenticado
Lista paginada, ordenada por inicio. Parámetros de query, todos opcionales:

| Parámetro | Descripción |
|---|---|
| `pageIndex` | Página, desde 0 (por defecto 0) |
| `pageSize` | Tamaño de página, de 1 a 100 (por defecto 10) |
| `doctorId` | Citas de un doctor. Un `Doctor` sin este filtro ve solo las suyas |
| `patientId` | Citas de un paciente |
| `status` | `Scheduled`, `Confirmed`, `Completed`, `Cancelled` o `NoShow` |
| `from`, `to` | Fechas **locales** del dispensario (`"yyyy-MM-dd"`), ambas inclusive |

```json
// GET /api/appointments?doctorId=0193f1b8-…&from=2026-11-03&to=2026-11-03 → 200 OK
{ "pageIndex": 0, "pageSize": 10, "count": 1, "data": [ { "id": "0193f1d0-…", "startLocal": "2026-11-03T09:30:00", "…": "…" } ] }
```

| Código | Cuándo |
|---|---|
| 200 | Página de citas |
| 400 | `pageIndex` negativo, `pageSize` fuera de 1–100, `from` posterior a `to`, o `status` o fecha ilegibles |
| 401 | Sin token |
| 403 | El usuario es un `Doctor` sin vincular, o filtra por otro doctor |

#### `GET /api/appointments/{id}` — Autenticado
Devuelve una cita con la forma de arriba.

| Código | Cuándo |
|---|---|
| 200 | Cita encontrada |
| 401 | Sin token |
| 403 | El usuario es un `Doctor` sin vincular, o la cita es de otro doctor |
| 404 | No existe una cita con ese ID |

#### `POST /api/appointments/{id}/confirm` — `Reception`
Confirma una cita `Scheduled` antes de su inicio. Sin cuerpo.

| Código | Cuándo |
|---|---|
| 204 | Cita confirmada |
| 400 | La cita no está `Scheduled` o ya empezó |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe una cita con ese ID |

#### `POST /api/appointments/{id}/cancel` — Autenticado
Cancela una cita `Scheduled` o `Confirmed` antes de su inicio. `reason` es obligatorio, admite hasta 500 caracteres y se guarda sin espacios alrededor. La cita cancelada deja libre su horario. El Admin y la recepción cancelan cualquier cita; un `Doctor`, solo las suyas.

```json
// Petición
{ "reason": "El paciente pidió cancelar por viaje" }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Cita cancelada |
| 400 | `reason` vacío o de más de 500 caracteres (`ValidationErrors`). También sin `ValidationErrors`: la cita no está `Scheduled` ni `Confirmed`, o ya empezó |
| 401 | Sin token |
| 403 | El usuario es un `Doctor` sin vincular, o la cita es de otro doctor |
| 404 | No existe una cita con ese ID |

#### `POST /api/appointments/{id}/no-show` — Autenticado
Marca como no asistida una cita `Scheduled` o `Confirmed` cuya hora de inicio ya llegó. Sin cuerpo. El Admin y la recepción marcan cualquier cita; un `Doctor`, solo las suyas.

| Código | Cuándo |
|---|---|
| 204 | Cita marcada como no asistida |
| 400 | La cita no está `Scheduled` ni `Confirmed`, o aún no empezó |
| 401 | Sin token |
| 403 | El usuario es un `Doctor` sin vincular, o la cita es de otro doctor |
| 404 | No existe una cita con ese ID |

#### `PUT /api/appointments/{id}/schedule` — `Reception`
Reprograma una cita `Scheduled` o `Confirmed` a otra hora, con el mismo doctor y paciente. `startLocal` es la nueva hora local del dispensario **sin offset**. Se aplican las mismas reglas que al agendar (paciente y doctor activos, cita futura, horario, alineación y cruces), con la duración **actual** del doctor. La propia cita no cuenta como cruce. La cita vuelve a `Scheduled` aunque estuviera confirmada.

```json
// Petición
{ "startLocal": "2026-11-04T10:00" }

// 204 No Content
```

| Código | Cuándo |
|---|---|
| 204 | Cita reprogramada |
| 400 | `startLocal` vacío o con `Z` u offset (`ValidationErrors`). También sin `ValidationErrors`: `startLocal` ilegible, la misma hora que ya tiene, en el pasado, fuera del horario o desalineada; la cita no está `Scheduled` ni `Confirmed` o ya empezó; paciente o doctor inactivo |
| 401 / 403 | Sin token / el usuario es `Doctor` |
| 404 | No existe una cita con ese ID |
| 409 | El doctor o el paciente ya tienen otra cita que se cruza con la nueva hora |

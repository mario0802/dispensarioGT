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
| `GET` | `/health` | Anónimo | Estado de la Api y de la base de datos |
| `GET` | `/scalar`, `/openapi/v1.json` | Solo Development | Referencia interactiva y documento OpenAPI |

Políticas de autorización:

| Política | Roles |
|---|---|
| `AdminOnly` | `Admin` |
| `Reception` | `Admin`, `Receptionist` |
| `Clinical` | `Admin`, `Doctor` |

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
Todas las rutas con `{id}` esperan un GUID. Las de escritura son `AdminOnly` (**401** sin token, **403** con un token de `Doctor` o `Receptionist`); las lecturas aceptan cualquier usuario autenticado. Las respuestas de doctor tienen esta forma:

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
| 400 | Datos inválidos, como en el alta |
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

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
| `PUT` | `/api/users/me` | Autenticado | Cambia el nombre y el teléfono propios |
| `PUT` | `/api/users/me/password` | Autenticado | Cambia la contraseña propia |
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
Registra un usuario de cualquier rol (`Admin`, `Doctor`, `Receptionist`). Un `Doctor` queda sin `doctorId` hasta que exista el módulo Doctors.

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

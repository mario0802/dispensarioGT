# SPEC 08 — Gestión de usuarios

> **Status:** Implemented
> **Depends on:** SPEC 06, SPEC 07
> **Date:** 2026-10-26
> **Objective:** Exponer la gestión de usuarios (autoregistro de recepcionistas, alta por el Admin, consulta paginada y por ID, edición de perfil, cambio y reseteo de contraseña, activación y desactivación) sobre los métodos de dominio que ya tiene `User`.

## Por qué este spec existe

Hasta ahora el único usuario es el Admin sembrado, y los demás solo se pueden crear a mano en la base de datos.
Este spec es el primero que usa las políticas de la fila 07 en endpoints de negocio.
El aggregate `User` (SPEC 06) ya tiene `ChangePassword`, `UpdateProfile`, `Deactivate` y `Activate`.
Este spec **no cambia el dominio**: añade los casos de uso que los llaman y las reglas que necesitan la base de datos o el usuario actual (unicidad, auto-desactivación, último Admin), que van en los handlers.
Amplía el backlog de la fila 08 con el autoregistro, ActivateUser, GetUserById, la edición de perfil y el reseteo de contraseña por el Admin.

## Scope

**In:**

- Regla de contraseña compartida (`PasswordRules`) para FluentValidation: 8 a 72 caracteres, al menos una letra y un dígito.
- Commands en `Dispensario.Application/Users/Commands/`: `RegisterUser`, `ChangePassword` (propia), `ResetUserPassword` (Admin), `UpdateUserProfile` (Admin), `UpdateCurrentUserProfile` (propio), `DeactivateUser` y `ActivateUser`, con sus validadores y handlers.
- Queries en `Dispensario.Application/Users/Queries/`: `GetUsers` (paginada, con `search`, `role` e `isActive`) y `GetUserById`.
- `UserDto` y su mapeo en `UsersMappingConfig`.
- Endpoints:
  - `POST /api/auth/register` (anónimo, siempre crea un `Receptionist`).
  - `POST /api/users`, `GET /api/users`, `GET /api/users/{id}`, `PUT /api/users/{id}`, `PUT /api/users/{id}/password`, `POST /api/users/{id}/deactivate` y `POST /api/users/{id}/activate` (los siete `AdminOnly`).
  - `PUT /api/users/me` y `PUT /api/users/me/password` (cualquier usuario autenticado).
- Tests unitarios de validadores y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `APPLICATION.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Cambios en el aggregate `User` o en su configuración EF: no hay métodos ni columnas nuevas.
- Cambiar el rol de un usuario (`ChangeRole`): el rol es fijo en v1.
- Vincular un usuario `Doctor` con su `Doctor` (`LinkDoctor`): fila 09. Un `Doctor` registrado aquí queda con `DoctorId = null`.
- Domain events nuevos (`UserPasswordChanged`, `UserDeactivated`…): no hay consumidores.
- Cambiar el email o la identificación de un usuario (son inmutables según `DOMAIN.md` §4).
- Recuperación de contraseña por email, confirmación de email y aprobación del autoregistro.
- Revocar los tokens emitidos al cambiar la contraseña o al desactivar un usuario.
- Rate limiting o captcha en `POST /api/auth/register` y en el login.
- Borrado de usuarios (solo se desactivan).

## Data model

Este spec no añade tablas, columnas ni métodos de dominio. Reutiliza `User` de SPEC 06 y las abstracciones de SPEC 07 (`IPasswordHasher`, `ICurrentUser`).

### Métodos de dominio usados (ya existen en `User`)

| Método | Lo usa | Regla de dominio (ya implementada) |
| --- | --- | --- |
| `User.Create(email, passwordHash, fullName, identification, phone, role)` | `RegisterUser` | hash no vacío, rol definido; emite `UserCreatedDomainEvent` |
| `ChangePassword(newPasswordHash)` | `ChangePassword`, `ResetUserPassword` | hash no vacío |
| `UpdateProfile(fullName, phone)` | `UpdateUserProfile`, `UpdateCurrentUserProfile` | `phone` null borra el teléfono |
| `Deactivate()` | `DeactivateUser` | sobre un inactivo → `DomainException("User is already inactive.")` → 400 |
| `Activate()` | `ActivateUser` | sobre un activo → `DomainException("User is already active.")` → 400 |

### `Dispensario.Application/Users/` (namespaces iguales a las carpetas)

```
Users/
  Validation/PasswordRules.cs
  Commands/
    RegisterUser/              RegisterUserCommand.cs  RegisterUserHandler.cs
    ChangePassword/            ChangePasswordCommand.cs  ChangePasswordHandler.cs
    ResetUserPassword/         ResetUserPasswordCommand.cs  ResetUserPasswordHandler.cs
    UpdateUserProfile/         UpdateUserProfileCommand.cs  UpdateUserProfileHandler.cs
    UpdateCurrentUserProfile/  UpdateCurrentUserProfileCommand.cs  UpdateCurrentUserProfileHandler.cs
    DeactivateUser/            DeactivateUserCommand.cs  DeactivateUserHandler.cs
    ActivateUser/              ActivateUserCommand.cs  ActivateUserHandler.cs
  Queries/
    GetUsers/                  GetUsersQuery.cs  GetUsersHandler.cs
    GetUserById/               GetUserByIdQuery.cs  GetUserByIdHandler.cs
  Dtos/                        UserDto.cs  UsersMappingConfig.cs (cambia)
```

```csharp
// PasswordRules.cs
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 72;   // límite de bcrypt

    // NotEmpty, longitud 8–72, al menos una letra ([A-Za-z]) y un dígito ([0-9])
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule);
}

public sealed record RegisterUserCommand(
    string Email, string Password,
    string FirstName, string LastName,
    DocumentType DocumentType, string DocumentNumber,
    string? Phone, Role Role) : ICommand<RegisterUserResult>;
public sealed record RegisterUserResult(Guid Id);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : ICommand;
public sealed record UpdateUserProfileCommand(Guid UserId, string FirstName, string LastName, string? Phone) : ICommand;
public sealed record UpdateCurrentUserProfileCommand(string FirstName, string LastName, string? Phone) : ICommand;
public sealed record DeactivateUserCommand(Guid UserId) : ICommand;
public sealed record ActivateUserCommand(Guid UserId) : ICommand;

public sealed record GetUsersQuery(PaginationRequest Pagination, string? Search, Role? Role, bool? IsActive)
    : IQuery<PaginatedResult<UserDto>>;
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto>;

public sealed record UserDto(
    Guid Id, string Email, string FirstName, string LastName,
    DocumentType IdentificationType, string IdentificationNumber,
    string? Phone, Role Role, Guid? DoctorId, bool IsActive, DateTime CreatedAtUtc);
```

- `UsersMappingConfig` añade `User → UserDto` con las mismas reglas que `User → CurrentUserDto` más `CreatedAtUtc`.

### Validadores (en el archivo de cada command)

| Command | Reglas |
| --- | --- |
| `RegisterUserCommand` | `Email`: `NotEmpty`, `MaximumLength(Email.MaxLength)`, `EmailAddress`. `Password`: `ValidPassword()`. `FirstName`/`LastName`: `NotEmpty`, `MaximumLength(FullName.MaxLength)`. `DocumentType`: `IsInEnum`. `DocumentNumber`: `NotEmpty`, `MaximumLength(Document.NumberMaxLength)`. `Phone`: `MaximumLength(PhoneNumber.MaxLength)` si no es null. `Role`: `IsInEnum` |
| `ChangePasswordCommand` | `CurrentPassword`: `NotEmpty`. `NewPassword`: `ValidPassword()` y distinta de `CurrentPassword` |
| `ResetUserPasswordCommand` | `UserId`: `NotEmpty`. `NewPassword`: `ValidPassword()` |
| `UpdateUserProfileCommand` | `UserId`: `NotEmpty`. Nombre, apellido y teléfono como en `RegisterUserCommand` |
| `UpdateCurrentUserProfileCommand` | Nombre, apellido y teléfono como en `RegisterUserCommand` |
| `DeactivateUserCommand` / `ActivateUserCommand` | `UserId`: `NotEmpty` |

- El formato del teléfono lo valida el VO `PhoneNumber` (`DomainException` → 400).

### Handlers

Todos reciben `IApplicationDbContext`; los que lo necesitan, también `IPasswordHasher` e `ICurrentUser`. Cada command hace un único `SaveChangesAsync`.

| Handler | Comportamiento, en orden |
| --- | --- |
| `RegisterUserHandler` | Crea los VOs (`Email.Create` normaliza a minúsculas). Email ya usado → `ConflictException("User with email {email} already exists.")`. Identificación ya usada → `ConflictException("User with identification {document} already exists.")`. `User.Create` con `hasher.Hash(password)`; guarda y devuelve el ID |
| `ChangePasswordHandler` | `currentUser.UserId` null → `UnauthorizedException("User is not authenticated.")`. No existe → `NotFoundException(nameof(User), id)`. Inactivo → `UnauthorizedException("User is inactive.")`. `hasher.Verify` falla → `BadRequestException("Current password is incorrect.")`. `ChangePassword(hasher.Hash(new))` y guarda |
| `ResetUserPasswordHandler` | `UserId` igual a `currentUser.UserId` → `BadRequestException("Use /api/users/me/password to change your own password.")`. No existe → `NotFoundException`. `ChangePassword(hasher.Hash(new))` y guarda (también sobre usuarios inactivos) |
| `UpdateUserProfileHandler` | No existe → `NotFoundException`. `UpdateProfile(FullName.Create(...), phone)` y guarda |
| `UpdateCurrentUserProfileHandler` | Igual que `ChangePasswordHandler` para el usuario actual (401 / 404 / 401 inactivo); después `UpdateProfile` y guarda |
| `DeactivateUserHandler` | No existe → `NotFoundException`. Es el usuario actual → `BadRequestException("You cannot deactivate your own user.")`. `Role == Admin`, activo y es el único Admin activo → `ConflictException("Cannot deactivate the last active admin.")`. `Deactivate()` (inactivo → 400 de dominio) y guarda |
| `ActivateUserHandler` | No existe → `NotFoundException`. `Activate()` (activo → 400 de dominio) y guarda |
| `GetUsersHandler` | `PageIndex < 0` o `PageSize` fuera de 1–100 → `BadRequestException`. `AsNoTracking`; filtros opcionales: `Role`, `IsActive` y `Search` (recortado; contiene, sin distinguir mayúsculas, en nombre, apellido, email o número de identificación). Orden por apellido, nombre e ID. Proyecta a `UserDto` con Mapster y devuelve `PaginatedResult<UserDto>` |
| `GetUserByIdHandler` | `AsNoTracking`; no existe → `NotFoundException`; si no, `UserDto` |

- Las queries no pasan por FluentValidation (`CLAUDE.md`), por eso `GetUsersHandler` valida la paginación con `BadRequestException`.

### `Dispensario.Api/Endpoints/`

```
Auth/RegisterEndpoint.cs
Users/
  RegisterUserEndpoint.cs  GetUsersEndpoint.cs  GetUserByIdEndpoint.cs
  UpdateUserProfileEndpoint.cs  ResetUserPasswordEndpoint.cs
  DeactivateUserEndpoint.cs  ActivateUserEndpoint.cs
  UpdateCurrentUserProfileEndpoint.cs  ChangePasswordEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/auth/register` | `RegisterRequest(Email, Password, FirstName, LastName, DocumentType, DocumentNumber, Phone?)` → `RegisterUserResponse(Guid Id)`; el endpoint fija `Role.Receptionist` | `AllowAnonymous()` | 201 (`Location: /api/users/{id}`), 400, 409 |
| `POST /api/users` | `RegisterUserRequest` (lo mismo + `Role`) → `RegisterUserResponse(Guid Id)` | `AdminOnly` | 201 (`Location`), 400, 401, 403, 409 |
| `GET /api/users` | `[AsParameters] GetUsersRequest(int PageIndex = 0, int PageSize = 10, string? Search, Role? Role, bool? IsActive)` → `PaginatedResult<UserResponse>` | `AdminOnly` | 200, 400, 401, 403 |
| `GET /api/users/{id:guid}` | — → `UserResponse` (campos de `UserDto`) | `AdminOnly` | 200, 401, 403, 404 |
| `PUT /api/users/{id:guid}` | `UpdateUserProfileRequest(FirstName, LastName, Phone?)` | `AdminOnly` | 204, 400, 401, 403, 404 |
| `PUT /api/users/{id:guid}/password` | `ResetUserPasswordRequest(NewPassword)` | `AdminOnly` | 204, 400, 401, 403, 404 |
| `POST /api/users/{id:guid}/deactivate` | — | `AdminOnly` | 204, 400, 401, 403, 404, 409 |
| `POST /api/users/{id:guid}/activate` | — | `AdminOnly` | 204, 400, 401, 403, 404 |
| `PUT /api/users/me` | `UpdateCurrentUserProfileRequest(FirstName, LastName, Phone?)` | `RequireAuthorization()` | 204, 400, 401, 404 |
| `PUT /api/users/me/password` | `ChangePasswordRequest(CurrentPassword, NewPassword)` | `RequireAuthorization()` | 204, 400, 401, 404 |

- `POST /api/auth/register` lleva `WithTags("Auth")`; los demás, `WithTags("Users")`. Todos con `WithName` (igual al nombre del command o query, `Register` para el autoregistro) y `WithSummary`.
- La restricción `{id:guid}` evita que `/api/users/me` choque con `/api/users/{id}`.
- Los endpoints no leen `ICurrentUser`: las reglas del usuario actual van en los handlers.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Application.UnitTests/Users/PasswordRulesTests.cs` | Vacía, 7 caracteres, 73 caracteres, sin dígito y sin letra fallan; `Abcdefg1` y una de 72 caracteres pasan |
| `tests/Dispensario.Application.UnitTests/Users/RegisterUserCommandValidatorTests.cs` | Email inválido, nombre vacío, documento demasiado largo, rol o tipo de documento fuera del enum fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Users/ChangePasswordCommandValidatorTests.cs` | Nueva igual a la actual falla; nueva débil falla; correcto pasa |
| `tests/Dispensario.Application.UnitTests/Users/RegisterUserHandlerTests.cs` | Sobre `DispensarioTestDb` con hasher de NSubstitute: crea el usuario con el hash, el rol y el email en minúsculas; email duplicado (con otras mayúsculas) → `ConflictException`; identificación duplicada → `ConflictException` |
| `tests/Dispensario.Application.UnitTests/Users/ChangePasswordHandlerTests.cs` | Actual correcta → guarda el nuevo hash; actual incorrecta → `BadRequestException`; sin usuario → `UnauthorizedException`; inexistente → `NotFoundException`; inactivo → `UnauthorizedException` |
| `tests/Dispensario.Application.UnitTests/Users/ResetUserPasswordHandlerTests.cs` | Cambia el hash de otro usuario; sobre sí mismo → `BadRequestException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Users/UpdateUserProfileHandlerTests.cs` | Cambia nombre y teléfono; `Phone = null` lo borra; inexistente → `NotFoundException`. Incluye `UpdateCurrentUserProfileHandler`: actualiza al usuario actual; sin usuario → 401; inactivo → 401 |
| `tests/Dispensario.Application.UnitTests/Users/DeactivateUserHandlerTests.cs` | Desactiva a otro usuario; a sí mismo → `BadRequestException`; último Admin activo → `ConflictException`; con dos Admin activos sí desactiva uno; ya inactivo → `DomainException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Users/ActivateUserHandlerTests.cs` | Activa a un inactivo; ya activo → `DomainException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Users/GetUsersHandlerTests.cs` | Paginación (`Count` total y tamaño de página); filtros `Role` e `IsActive`; `Search` por apellido, email y número de identificación sin distinguir mayúsculas; orden por apellido; `PageSize = 0` o `101` → `BadRequestException` |
| `tests/Dispensario.Application.UnitTests/Users/GetUserByIdHandlerTests.cs` | Devuelve el `UserDto` con `CreatedAtUtc`; inexistente → `NotFoundException` |
| `tests/Dispensario.Api.IntegrationTests/Auth/RegisterEndpointTests.cs` | Sin token → 201 con `Location` y el usuario puede hacer login con rol `Receptionist`; un `role` en el cuerpo se ignora; email duplicado → 409; contraseña débil → 400 con errores de validación |
| `tests/Dispensario.Api.IntegrationTests/Users/UsersEndpointsTests.cs` | Como Admin: alta de un `Doctor` → 201 y `GET /api/users/{id}` lo devuelve con `"role": "Doctor"`; `GET /api/users?role=Doctor` lo lista; `PUT` de perfil → 204; reset de contraseña → 204 y login con la nueva; desactivar → 204 y el login devuelve 401; activar → 204; `GET` de un ID inexistente → 404; desactivarse a sí mismo → 400. Como `Receptionist` o `Doctor`: los endpoints `AdminOnly` → 403. Sin token → 401 |
| `tests/Dispensario.Api.IntegrationTests/Users/CurrentUserEndpointsTests.cs` | `PUT /api/users/me` → 204 y `/api/auth/me` muestra el cambio; `PUT /api/users/me/password` con la actual correcta → 204 y login con la nueva; con la actual incorrecta → 400; sin token → 401 |

- La regla del último Admin se prueba en los tests unitarios: en integración siempre hay más de un Admin (el sembrado y los de `AuthenticateAsAsync`).
- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `PasswordRules` y `PasswordRulesTests`. Verificar: `dotnet test` en verde.
2. Crear `RegisterUserCommand` con su validador y `RegisterUserHandler`, con `RegisterUserCommandValidatorTests` y `RegisterUserHandlerTests`. Verificar: `dotnet test` en verde.
3. Crear `RegisterUserEndpoint` (`POST /api/users`) y `RegisterEndpoint` (`POST /api/auth/register`), con `RegisterEndpointTests` y los casos de alta de `UsersEndpointsTests`. Verificar: `dotnet test` en verde y alta desde Scalar.
4. Crear `UserDto`, el mapeo en `UsersMappingConfig`, `GetUserByIdQuery`, `GetUsersQuery` y sus handlers, con `GetUserByIdHandlerTests` y `GetUsersHandlerTests`. Crear `GetUserByIdEndpoint` y `GetUsersEndpoint` y sus casos en `UsersEndpointsTests`. Verificar: `dotnet test` en verde.
5. Crear `UpdateUserProfile` y `UpdateCurrentUserProfile` (commands, handlers, validadores y endpoints), con `UpdateUserProfileHandlerTests` y los casos de perfil de `UsersEndpointsTests` y `CurrentUserEndpointsTests`. Verificar: `dotnet test` en verde.
6. Crear `ChangePassword` y `ResetUserPassword` (commands, handlers, validadores y endpoints), con `ChangePasswordCommandValidatorTests`, `ChangePasswordHandlerTests`, `ResetUserPasswordHandlerTests` y sus casos de integración. Verificar: `dotnet test` en verde.
7. Crear `DeactivateUser` y `ActivateUser` (commands, handlers, validadores y endpoints), con `DeactivateUserHandlerTests`, `ActivateUserHandlerTests` y sus casos de integración, incluidos los 403 por rol. Verificar: `dotnet test` en verde.
8. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y apartados `POST /api/auth/register` (en Auth) y **Users** con cada ruta: acceso, ejemplo de petición y respuesta, y tabla de códigos de error.
   - `docs/guides/APPLICATION.md` §2 "Módulo Users": tabla con los casos de uso nuevos y sus reglas (unicidad, auto-desactivación, último Admin, contraseña actual incorrecta → 400), `PasswordRules` y `UserDto`.
   - `docs/guides/API.md` §1: `Endpoints/Auth/RegisterEndpoint.cs` y `Endpoints/Users/`; restricción `{id:guid}` frente a `/api/users/me`.
   - `CLAUDE.md`: "Estado actual" con la gestión de usuarios (feature 08); "Detalles no obvios" con el autoregistro como `Receptionist` y la regla del último Admin.
   - `docs/PLAN.md`: la fila 08 apunta a `specs/08-user-management.md`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 07.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [ ] `User.cs`, `UserConfiguration.cs` y la carpeta `Dispensario.Domain/Users/` no cambian.
- [ ] `POST /api/auth/register` sin token devuelve 201 con `Location: /api/users/{id}`, y el usuario creado tiene rol `Receptionist` aunque el cuerpo traiga otro `role`.
- [ ] `POST /api/users` como Admin crea usuarios `Admin`, `Doctor` y `Receptionist`; un `Doctor` queda con `doctorId = null`.
- [ ] Registrar un email ya usado (también con otras mayúsculas) o una identificación ya usada devuelve 409.
- [ ] Una contraseña de menos de 8 o más de 72 caracteres, sin letra o sin dígito devuelve 400 con errores de validación en el registro, el cambio y el reseteo.
- [ ] `GET /api/users` devuelve `pageIndex`, `pageSize`, `count` y `data`, filtra por `role`, `isActive` y `search`, y responde 400 con `pageSize` fuera de 1–100.
- [ ] `GET /api/users/{id}` devuelve el usuario con `createdAtUtc`, y 404 si no existe.
- [ ] `PUT /api/users/{id}` y `PUT /api/users/me` cambian nombre y teléfono y responden 204; un `phone` null borra el teléfono.
- [ ] `PUT /api/users/me/password` con la contraseña actual correcta responde 204 y el login funciona con la nueva; con la actual incorrecta responde 400 `Current password is incorrect.`.
- [ ] `PUT /api/users/{id}/password` responde 204 y el login funciona con la nueva; sobre el propio Admin responde 400.
- [ ] `POST /api/users/{id}/deactivate` responde 204 y después el login de ese usuario devuelve 401; sobre sí mismo responde 400; sobre el último Admin activo responde 409; sobre un inactivo responde 400.
- [ ] `POST /api/users/{id}/activate` sobre un inactivo responde 204 y el login vuelve a funcionar; sobre un activo responde 400.
- [ ] Los endpoints `AdminOnly` responden 403 a `Receptionist` y `Doctor`, y todos los de `/api/users` responden 401 sin token.
- [ ] Todos los errores salen como `application/problem+json`.
- [ ] Todas las rutas nuevas aparecen en `/openapi/v1.json` con su tag, nombre y resumen.
- [ ] La sección **Endpoints** del `README.md` documenta las diez rutas nuevas con acceso, petición, respuesta y códigos de error.
- [ ] `APPLICATION.md`, `API.md` y `CLAUDE.md` describen la gestión de usuarios, y `docs/PLAN.md` fila 08 apunta a `specs/08-user-management.md`.

## Decisions

- **Yes:** el dominio no cambia. `User` ya tiene `ChangePassword`, `UpdateProfile`, `Deactivate` y `Activate` (SPEC 06), con sus tests.
- **Yes:** las reglas de auto-desactivación, último Admin, contraseña actual y unicidad van en los handlers. Necesitan la base de datos o el usuario actual (`DOMAIN.md` §2 regla 9 y `APPLICATION.md` §3).
- **Yes:** dos vías de alta: `POST /api/auth/register` anónimo, que siempre crea un `Receptionist`, y `POST /api/users` (`AdminOnly`) con cualquier rol. Admin y Doctor solo los crea un Admin. Decisión del usuario.
- **Yes:** las dos vías usan el mismo `RegisterUserCommand`; el rol lo fija el endpoint. Evita duplicar validación y reglas.
- **No:** autoregistro inactivo pendiente de aprobación, ni un solo endpoint con el rol según quién llama. Descartados por el usuario.
- **Yes:** el autoregistro devuelve 201 con el ID, sin token. El cliente hace login después, igual que en SPEC 07.
- **Yes:** cambio de la propia contraseña (`PUT /api/users/me/password`, exige la actual) y reseteo por el Admin (`PUT /api/users/{id}/password`, sin la actual). Cubre el olvido de contraseña sin flujo de email. Decisión del usuario.
- **Yes:** contraseña actual incorrecta → 400 `Current password is incorrect.`. Un 401 haría creer al cliente que el token caducó. Decisión del usuario.
- **Yes:** el Admin no puede resetear su propia contraseña por `/api/users/{id}/password`: debe usar `/me/password`, que exige la actual. Un token robado no basta para cambiarla.
- **Yes:** política de contraseña 8–72 caracteres con al menos una letra y un dígito, en `PasswordRules` compartido. 72 es el límite de bcrypt. Decisión del usuario.
- **No:** reglas de mayúsculas, minúsculas y símbolos. Más fricción sin mucha ganancia.
- **Yes:** la nueva contraseña debe ser distinta de la actual en `ChangePassword`.
- **Yes:** un usuario no puede desactivarse a sí mismo (400) ni se puede desactivar el último Admin activo (409). Evita quedarse sin administración. Decisión del usuario.
- **Yes:** `ActivateUser`, `GetUserById` y la edición de perfil entran en este spec, además de las cuatro operaciones del backlog. Decisión del usuario.
- **Yes:** el perfil lo edita el Admin (`PUT /api/users/{id}`) y el propio usuario (`PUT /api/users/me`). Decisión del usuario.
- **Yes:** las rutas del usuario actual van en `/api/users/me` y `/api/users/me/password`; `GET /api/auth/me` se queda donde está (SPEC 07).
- **Yes:** `{id:guid}` en las rutas con ID, para que `/api/users/me` no se interprete como un ID.
- **No:** `ChangeRole`. El rol es fijo en v1: evita incoherencias con `DoctorId` y con tokens emitidos con el rol anterior. Decisión del usuario.
- **Yes:** un usuario `Doctor` se registra sin vínculo (`DoctorId = null`); la fila 09 decide cómo se llama a `LinkDoctor`. Decisión del usuario.
- **No:** domain events nuevos. Nadie reacciona hoy a esos hechos (`DOMAIN.md` §2 regla 7). Decisión del usuario.
- **Yes:** `GetUsers` paginado con `search`, `role` e `isActive`, orden por apellido y nombre. Decisión del usuario.
- **Yes:** `GetUsersHandler` valida la paginación con `BadRequestException`, porque las queries no pasan por `ValidationBehavior`.
- **Yes:** un solo `UserDto` (campos de `CurrentUserDto` + `CreatedAtUtc`) para lista y detalle. Decisión del usuario.
- **Yes:** `ChangePassword` y `UpdateCurrentUserProfile` rechazan al usuario inactivo con 401, igual que `/api/auth/me`.
- **Yes:** el reseteo de contraseña funciona también sobre usuarios inactivos, para poder reactivarlos con una contraseña nueva.

## Risks

| Risk | Mitigation |
| --- | --- |
| `search` sobre `Email` (columna con `HasConversion`) puede no traducirse a SQL con `Contains` | El paso 4 lo comprueba con `GetUsersHandlerTests`. Si EF no lo traduce, se filtra por la columna con `EF.Property<string>` o `EF.Functions.Like` sobre el valor convertido; nunca en memoria. |
| SQLite `LIKE` solo ignora mayúsculas en ASCII: "Ñ" y "ñ" no coinciden | Aceptado en v1. El email ya se guarda en minúsculas. |
| El autoregistro anónimo permite crear cuentas `Receptionist` a cualquiera | Decisión del usuario. El Admin puede desactivarlas. Rate limiting y aprobación quedan fuera de alcance. |
| El 409 del autoregistro revela qué emails existen | Aceptado en v1, igual que la unicidad del documento. |
| Un usuario desactivado o con la contraseña cambiada conserva su token hasta que caduca (60 min) | Comportamiento aceptado en SPEC 07; la revocación queda para un spec futuro. |
| 72 caracteres pueden superar los 72 bytes de bcrypt con caracteres no ASCII | Aceptado: bcrypt ignora los bytes sobrantes. El límite en caracteres cubre el caso habitual. |
| Dos Admin se desactivan entre sí a la vez y queda ninguno activo | Muy improbable con pocos Admin. Se acepta en v1 sin bloqueo transaccional. |
| La regla del último Admin no se puede probar en integración (siempre hay varios Admin) | Se prueba en `DeactivateUserHandlerTests` sobre `DispensarioTestDb`. |
| Una `dispensario.db` local no cambia (el modelo no cambia) | No aplica: este spec no toca el esquema. |

## What is **not** in this spec

- Cambios en el aggregate `User` o en su persistencia.
- Cambio de rol y vínculo con `Doctor` (fila 09).
- Domain events nuevos.
- Cambio de email o identificación.
- Recuperación de contraseña por email, confirmación de email y aprobación del autoregistro.
- Revocación de tokens, rate limiting y captcha.
- Borrado de usuarios.

Cada uno de ellos, si llega, va en su propio spec.

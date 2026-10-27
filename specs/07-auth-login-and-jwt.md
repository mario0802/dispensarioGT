# SPEC 07 — Auth: login y JWT

> **Status:** Implemented
> **Depends on:** SPEC 05, SPEC 06
> **Date:** 2026-10-
> **Objective:** Autenticar usuarios con email y contraseña (bcrypt) en `POST /api/auth/login`, emitir un JWT HS256, exponer el usuario actual en `GET /api/auth/me` y en `ICurrentUser`, registrar las políticas por rol y sembrar el Admin en Development.

## Por qué este spec existe

Hasta ahora `ICurrentUser` es anónimo y la Api no tiene autenticación: ningún endpoint de negocio se puede proteger.
Este spec cierra el ciclo de identidad para que la fila 08 (gestión de usuarios) y las siguientes ya usen `RequireAuthorization(Policies.X)`.
Cambia una decisión de la guía: el seed del Admin llega ya en la 07, y no en la 17.
Como no hay migraciones hasta la fila 17, `InitialiseDatabaseAsync` usa `EnsureCreatedAsync` en Development; la fila 17 lo cambia por `MigrateAsync`.

## Scope

**In:**

- Paquetes: `BCrypt.Net-Next` y `Microsoft.IdentityModel.JsonWebTokens` en Infrastructure, y `Microsoft.AspNetCore.Authentication.JwtBearer` (ya declarado en `Directory.Packages.props`) en Api. `FrameworkReference` a `Microsoft.AspNetCore.App` en Infrastructure.
- Abstracciones en `Dispensario.Application/Abstractions/`: `IPasswordHasher`, `IJwtTokenGenerator` e `ICurrentUser` en su forma final (`UserId?`, `Role?`, `DoctorId?`, `IsAuthenticated`).
- Infrastructure `Authentication/`: `JwtOptions`, `JwtTokenGenerator`, `BcryptPasswordHasher` (factor de trabajo 12) y `CurrentUser`. Se elimina `AnonymousCurrentUser`.
- `AuditableEntityInterceptor` adaptado al nuevo `ICurrentUser.UserId` (`UserId?`).
- Seed: `AdminSeedOptions`, `DatabaseSeeder` e `InitialiseDatabaseAsync` (`EnsureCreatedAsync` + seed), llamado desde `Program.cs` solo en Development.
- Application `Users/`: `LoginCommand` con su validador y su handler, `GetCurrentUserQuery` con su handler, `CurrentUserDto` y `UsersMappingConfig`.
- Api: `AddJwtBearer` configurado con `JwtOptions`, clase `Policies` (`AdminOnly`, `Reception`, `Clinical`), `UseAuthentication` y `UseAuthorization`, y enums serializados como texto en todo el JSON de la Api.
- Endpoints `POST /api/auth/login` (anónimo) y `GET /api/auth/me` (autenticado) en `Dispensario.Api/Endpoints/Auth/`.
- Secciones `Jwt` y `Seed` en `appsettings.json` (secretos vacíos, cargados desde user-secrets).
- Tests unitarios (hasher, generador de tokens, `CurrentUser`, handlers, validador y seeder) y de integración (login, `/me`, políticas), con el helper `AuthenticateAsAsync(Role)`.
- Actualizar `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `TESTING.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- RegisterUser, ChangePassword, DeactivateUser y GetUsers: fila 08.
- Refresh tokens, logout y revocación de tokens.
- Comprobar `IsActive` en cada petición autenticada: solo `/me` lo comprueba. Un usuario desactivado conserva el acceso hasta que caduca su token (máximo 60 min).
- Bloqueo de cuenta por intentos fallidos y rate limiting del login.
- Igualar el tiempo de respuesta entre email inexistente y contraseña incorrecta.
- Migraciones y `MigrateAsync`: fila 17, que sustituye `EnsureCreatedAsync`.
- Datos de ejemplo distintos del Admin: fila 17.
- Uso de las políticas en endpoints de negocio: desde la fila 08.
- Factor de trabajo de bcrypt configurable.

## Data model

Este spec no añade tablas ni cambia el aggregate `User`. Añade abstracciones, servicios de autenticación, configuración y dos endpoints.

### Paquetes

| Paquete | Versión | Lo referencia |
| --- | --- | --- |
| `BCrypt.Net-Next` | última estable (se verifica en el paso 1) | `Dispensario.Infrastructure` |
| `Microsoft.IdentityModel.JsonWebTokens` | la misma que trae `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 de forma transitiva | `Dispensario.Infrastructure` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.14 (ya declarado) | `Dispensario.Api` |

- `Dispensario.Infrastructure.csproj` añade `<FrameworkReference Include="Microsoft.AspNetCore.App" />` para `IHttpContextAccessor`, `IHost` y el logging.

### `Dispensario.Application/Abstractions/` (namespace `Dispensario.Application.Abstractions`)

```csharp
public interface ICurrentUser
{
    UserId? UserId { get; }
    Role? Role { get; }
    DoctorId? DoctorId { get; }
    bool IsAuthenticated { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}
```

- `AuditableEntityInterceptor` pasa a usar `currentUser.UserId?.Value.ToString()` para `CreatedBy` y `LastModifiedBy`.

### `Dispensario.Infrastructure/Authentication/` (namespace `Dispensario.Infrastructure.Authentication`)

```
Authentication/
  JwtOptions.cs  JwtTokenGenerator.cs  BcryptPasswordHasher.cs  CurrentUser.cs
```

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public required string Issuer { get; init; }
    [Required] public required string Audience { get; init; }
    [Required, MinLength(32)] public required string Key { get; init; }   // user-secrets
    [Range(1, 1440)] public int ExpirationMinutes { get; init; } = 60;
}
```

| Clase | Comportamiento |
| --- | --- |
| `BcryptPasswordHasher` | `Hash` usa `BCrypt.Net.BCrypt.HashPassword(password, WorkFactor)` con `private const int WorkFactor = 12`. `Verify` usa `BCrypt.Net.BCrypt.Verify(password, hash)`. Singleton. |
| `JwtTokenGenerator` | Usa `JsonWebTokenHandler` con HS256 sobre `SymmetricSecurityKey(UTF8(Key))`. `iat` y `nbf` = `TimeProvider.GetUtcNow()`; `exp` = ahora + `ExpirationMinutes`. Claims: `sub` (UserId como Guid), `email`, `name` (`FullName.ToString()`), `role` (`Role.ToString()`), `doctor_id` (solo si `DoctorId` no es `null`) y `jti` (Guid nuevo). Devuelve el token y `ExpiresAtUtc` (`DateTime` UTC). Singleton. |
| `CurrentUser` | Lee `IHttpContextAccessor.HttpContext?.User`. `UserId` desde `sub`, `Role` desde `role` (`Enum.TryParse`), `DoctorId` desde `doctor_id`. Un claim ausente o inválido da `null`. Sin `HttpContext` (por ejemplo, durante el seed), todo es `null` e `IsAuthenticated = false`. Scoped. |

### `Dispensario.Infrastructure/Data/Seed/` y `Data/Extensions/`

```
Data/Seed/AdminSeedOptions.cs  Data/Seed/DatabaseSeeder.cs
Data/Extensions/DatabaseExtensions.cs   # InitialiseDatabaseAsync
```

```csharp
public sealed class AdminSeedOptions
{
    public const string SectionName = "Seed:Admin";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;   // user-secrets
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DocumentType DocumentType { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
}
```

`DatabaseSeeder.SeedAsync(CancellationToken)`:

1. Si `Password` está vacío, registra el warning `Admin seed skipped: 'Seed:Admin:Password' is empty.` y termina.
2. Si ya existe un usuario con ese email (`Email.Create(options.Email)`), termina sin cambios.
3. Si no, crea el usuario con `User.Create(email, hasher.Hash(password), FullName.Create(...), Document.Create(...), phone: null, Role.Admin)` y guarda con `SaveChangesAsync`.

- Los VOs validan los datos de configuración: un email o un documento inválidos lanzan `DomainException` al arrancar.
- `AdminSeedOptions` no usa `ValidateOnStart`, porque la contraseña vacía es un caso válido (el seed se omite).

`InitialiseDatabaseAsync(this IHost host, CancellationToken ct = default)`: abre un scope, ejecuta `Database.EnsureCreatedAsync()` sobre `DispensarioDbContext` y después `DatabaseSeeder.SeedAsync()`.

### `AddInfrastructure` (añadidos)

```csharp
services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);
services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, CurrentUser>();   // reemplaza AnonymousCurrentUser
services.AddScoped<DatabaseSeeder>();
```

### `Dispensario.Application/Users/` (namespaces iguales a las carpetas)

```
Users/
  Commands/Login/      LoginCommand.cs  LoginHandler.cs
  Queries/GetCurrentUser/  GetCurrentUserQuery.cs  GetCurrentUserHandler.cs
  Dtos/                CurrentUserDto.cs  UsersMappingConfig.cs
```

```csharp
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
public sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAtUtc);   // TokenType = "Bearer"

public sealed record GetCurrentUserQuery : IQuery<CurrentUserDto>;

public sealed record CurrentUserDto(
    Guid Id, string Email, string FirstName, string LastName,
    DocumentType IdentificationType, string IdentificationNumber,
    string? Phone, Role Role, Guid? DoctorId, bool IsActive);
```

`LoginCommandValidator` (en `LoginCommand.cs`):

| Campo | Reglas |
| --- | --- |
| `Email` | `NotEmpty`, `MaximumLength(Email.MaxLength)`, `EmailAddress` |
| `Password` | `NotEmpty` |

`LoginHandler(IApplicationDbContext db, IPasswordHasher hasher, IJwtTokenGenerator tokenGenerator)`:

1. `Email.Create(command.Email)` normaliza a minúsculas.
2. Busca el usuario por email.
3. Si no existe, está inactivo o `hasher.Verify` es `false` → `UnauthorizedException("Invalid credentials.")` (el mismo mensaje en los tres casos).
4. Devuelve `LoginResult(token, "Bearer", expiresAtUtc)`.

`GetCurrentUserHandler(IApplicationDbContext db, ICurrentUser currentUser)`:

| Caso | Resultado |
| --- | --- |
| `currentUser.UserId` es `null` | `UnauthorizedException("User is not authenticated.")` |
| El usuario no existe | `NotFoundException(nameof(User), userId)` → 404 |
| El usuario está inactivo | `UnauthorizedException("User is inactive.")` → 401 |
| En otro caso | `CurrentUserDto` (lectura `AsNoTracking`, mapeo con `UsersMappingConfig`) |

`UsersMappingConfig : IRegister` define `User → CurrentUserDto` (IDs a `Guid`, VOs a sus valores, `Phone?.Value`).

### `Dispensario.Api/`

```
Authorization/Policies.cs
Endpoints/Auth/LoginEndpoint.cs  Endpoints/Auth/GetCurrentUserEndpoint.cs
Extensions/ApiServiceExtensions.cs   # añade autenticación, autorización y JSON
Program.cs
```

```csharp
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);   // Admin
    public const string Reception = nameof(Reception);   // Admin, Receptionist
    public const string Clinical  = nameof(Clinical);    // Doctor
}
```

`AddApiServices` añade:

| Servicio | Configuración |
| --- | --- |
| Autenticación | `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`. `JwtBearerOptions` se configura desde `IOptions<JwtOptions>` (`AddOptions<JwtBearerOptions>(...).Configure<IOptions<JwtOptions>>`): `MapInboundClaims = false`, emisor, audiencia, clave y tiempo de vida validados, `ClockSkew = TimeSpan.Zero`, `NameClaimType = "name"` y `RoleClaimType = "role"` |
| Autorización | `AddAuthorizationBuilder()` con las tres políticas de `Policies` (`RequireRole(nameof(Role.X))`) |
| JSON | `ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))` |

Endpoints:

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/auth/login` | `LoginRequest(string Email, string Password)` → `LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc)` | `AllowAnonymous()` | 200, 400 (validación), 401 |
| `GET /api/auth/me` | — → `CurrentUserResponse` (mismos campos que `CurrentUserDto`) | `RequireAuthorization()` | 200, 401, 404 |

- Ambos llevan `WithTags("Auth")`, `WithName` (`Login`, `GetCurrentUser`) y `WithSummary`, como pide `API.md` §2.

`Program.cs` (cambios):

```csharp
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.InitialiseDatabaseAsync();   // EnsureCreated + seed; la fila 17 lo cambia a MigrateAsync
}
```

`appsettings.json` (añadidos):

```json
{
  "Jwt": { "Issuer": "Dispensario", "Audience": "Dispensario.Clients", "Key": "", "ExpirationMinutes": 60 },
  "Seed": {
    "Admin": {
      "Email": "admin@dispensario.local", "Password": "",
      "FirstName": "System", "LastName": "Admin",
      "DocumentType": "NationalId", "DocumentNumber": "0000000000"
    }
  }
}
```

- Secretos: `dotnet user-secrets set "Jwt:Key" "<min-32-chars>"` y `dotnet user-secrets set "Seed:Admin:Password" "<password>"` con `--project src/Services/Dispensario/Dispensario.Api`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Application.UnitTests/Authentication/BcryptPasswordHasherTests.cs` | El hash es bcrypt con coste 12 (contiene `$12$`); `Verify` acepta la contraseña correcta y rechaza otra; dos hashes de la misma contraseña son distintos |
| `tests/Dispensario.Application.UnitTests/Authentication/JwtTokenGeneratorTests.cs` | Con `FakeTimeProvider`: `ExpiresAtUtc` = ahora + `ExpirationMinutes` y `Kind = Utc`; el token lleva `sub`, `email`, `name`, `role` y `jti`; `doctor_id` solo si el usuario tiene `DoctorId`; algoritmo HS256; el token valida con la misma clave, emisor y audiencia |
| `tests/Dispensario.Application.UnitTests/Authentication/CurrentUserTests.cs` | Con un `ClaimsPrincipal` autenticado devuelve `UserId`, `Role` y `DoctorId`; sin `HttpContext` todo es `null` e `IsAuthenticated = false`; un claim `role` desconocido da `Role = null` |
| `tests/Dispensario.Application.UnitTests/Users/LoginHandlerTests.cs` | Sobre `DispensarioTestDb`, con hasher y generador de NSubstitute: credenciales válidas → `LoginResult` con `TokenType = "Bearer"`; email en mayúsculas encuentra al usuario; email inexistente, contraseña incorrecta y usuario inactivo → `UnauthorizedException` con `Invalid credentials.` |
| `tests/Dispensario.Application.UnitTests/Users/LoginCommandValidatorTests.cs` | Email vacío, email sin `@` y contraseña vacía fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Users/GetCurrentUserHandlerTests.cs` | Usuario activo → DTO con todos los campos; sin `UserId` → `UnauthorizedException`; usuario inexistente → `NotFoundException`; usuario inactivo → `UnauthorizedException` |
| `tests/Dispensario.Application.UnitTests/Persistence/Seed/DatabaseSeederTests.cs` | Crea el Admin con rol `Admin`, el documento configurado y un hash que verifica la contraseña; una segunda ejecución no duplica; con contraseña vacía no crea nada |
| `tests/Dispensario.Application.UnitTests/Persistence/DependencyInjectionTests.cs` (cambia) | `ICurrentUser` resuelve a `CurrentUser` y, sin petición HTTP, `UserId` es `null` |
| `tests/Dispensario.Api.IntegrationTests/Common/DispensarioApiFactory.cs` (cambia) | Fija con `UseSetting` `Jwt:Key` (clave de test de 32+ caracteres), `Jwt:Issuer`, `Jwt:Audience` y `Seed:Admin:Password`; expone `AdminEmail` y `AdminPassword` como constantes y `AuthenticateAsAsync(Role)` |
| `tests/Dispensario.Api.IntegrationTests/Auth/LoginEndpointTests.cs` | Admin sembrado → 200 con `accessToken`, `tokenType = "Bearer"` y `expiresAtUtc`; contraseña incorrecta y email inexistente → 401 `application/problem+json`; email vacío → 400 con errores de validación |
| `tests/Dispensario.Api.IntegrationTests/Auth/GetCurrentUserEndpointTests.cs` | Con token → 200 y `"role": "Admin"` como texto; sin token, con token alterado y con token de otra clave → 401 `application/problem+json`; usuario desactivado tras el login → 401 |
| `tests/Dispensario.Api.IntegrationTests/Auth/AuthorizationPoliciesTests.cs` | `IAuthorizationPolicyProvider` de la factory devuelve `AdminOnly`, `Reception` y `Clinical` con los roles de `Policies` |

- `AuthenticateAsAsync(Role role)`: inserta un usuario activo del rol pedido (email y documento únicos por llamada, hash con el `IPasswordHasher` real), hace `POST /api/auth/login` y devuelve un `HttpClient` con la cabecera `Authorization: Bearer <token>`.
- La factory corre en Development, así que `InitialiseDatabaseAsync` crea el esquema y el Admin sobre la conexión en memoria.
- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Consultar en Context7 la documentación actual de `BCrypt.Net-Next`, `Microsoft.IdentityModel.JsonWebTokens` y `JwtBearer` 10. Añadir los paquetes a `Directory.Packages.props` (versiones verificadas) y las referencias a Infrastructure y Api, incluida la `FrameworkReference`. Verificar: `dotnet build` y `dotnet test` en verde.
2. Cambiar `ICurrentUser` a su forma final, adaptar `AuditableEntityInterceptor` y los tests que usan `ICurrentUser.UserId`. Crear `CurrentUser`, registrarlo en lugar de `AnonymousCurrentUser` (que se borra), añadir `AddHttpContextAccessor` y crear `CurrentUserTests`. Verificar: `dotnet test` en verde.
3. Crear `IPasswordHasher`, `BcryptPasswordHasher` y su registro, y `BcryptPasswordHasherTests`. Verificar: `dotnet test` en verde.
4. Crear `IJwtTokenGenerator`, `JwtOptions` (con `ValidateOnStart`), `JwtTokenGenerator` y su registro, y `JwtTokenGeneratorTests`. Añadir la sección `Jwt` a `appsettings.json` y `Jwt:*` con `UseSetting` en `DispensarioApiFactory`. Verificar: `dotnet test` en verde.
5. Configurar `AddJwtBearer`, la clase `Policies`, `AddAuthorizationBuilder`, `JsonStringEnumConverter`, y `UseAuthentication`/`UseAuthorization` en `Program.cs`. Crear `AuthorizationPoliciesTests`. Verificar: `dotnet test` en verde y `/health` sigue respondiendo 200.
6. Crear `AdminSeedOptions`, `DatabaseSeeder`, `InitialiseDatabaseAsync` y su llamada en `Program.cs` (Development), la sección `Seed` de `appsettings.json` y `Seed:Admin:Password` en la factory. Crear `DatabaseSeederTests`. Verificar: `dotnet test` en verde; con `dotnet run` y los user-secrets, `dispensario.db` tiene la tabla `Users` con el Admin.
7. Crear `LoginCommand`, su validador y `LoginHandler`, con `LoginHandlerTests` y `LoginCommandValidatorTests`. Crear `LoginEndpoint` y `LoginEndpointTests`. Verificar: `dotnet test` en verde y login del Admin desde Scalar.
8. Crear `GetCurrentUserQuery`, `CurrentUserDto`, `UsersMappingConfig` y `GetCurrentUserHandler`, con `GetCurrentUserHandlerTests`. Crear `GetCurrentUserEndpoint`, `AuthenticateAsAsync` y `GetCurrentUserEndpointTests`. Verificar: `dotnet test` en verde y `/me` desde Scalar con el token.
9. Actualizar la documentación:
   - `docs/guides/APPLICATION.md` §2: `ICurrentUser` ya en su forma final, sin la nota provisional; módulo `Users/` con `Login` y `GetCurrentUser`.
   - `docs/guides/INFRASTRUCTURE.md`: §1 sin `AnonymousCurrentUser` y con `AdminSeedOptions`; §6 `JwtOptions` con validación, claims con `jti`, factor de trabajo 12 y `CurrentUser` sin `HttpContext`; §7 seed por email, contraseña vacía → warning, documento configurable y `EnsureCreatedAsync` hasta la fila 17; §8 registro real.
   - `docs/guides/API.md`: §1 `Authorization/Policies.cs` y `Endpoints/Auth/`; §4 `ClockSkew = 0` y configuración de `JwtBearerOptions` desde `JwtOptions`; §5 `Program.cs` real; enums como texto en el JSON; §8 secciones `Jwt` y `Seed` actuales (con `DocumentType` y `DocumentNumber`).
   - `docs/guides/TESTING.md` "Integración": secretos de test, Admin sembrado y `AuthenticateAsAsync`.
   - `CLAUDE.md`: "Estado actual" con auth; "Detalles no obvios" con `EnsureCreated` en Development hasta la fila 17 (borrar `dispensario.db` si cambia el modelo), enums como texto, `ICurrentUser` final y `Jwt:Key` obligatoria para arrancar.
   - `docs/PLAN.md`: la fila 07 apunta a este spec; la fila 17 cambia `EnsureCreatedAsync` por `MigrateAsync` en `InitialiseDatabaseAsync`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 06.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [ ] `ICurrentUser` expone exactamente `UserId?`, `Role?`, `DoctorId?` e `IsAuthenticated`, y `AnonymousCurrentUser` ya no existe en `src/`.
- [ ] `BcryptPasswordHasher` genera hashes bcrypt de coste 12 que `Verify` acepta con la contraseña correcta y rechaza con otra (cubierto por `BcryptPasswordHasherTests`).
- [ ] El JWT es HS256, caduca a los `ExpirationMinutes` y lleva `sub`, `email`, `name`, `role` y `jti`, más `doctor_id` solo si el usuario tiene doctor (cubierto por `JwtTokenGeneratorTests`).
- [ ] Sin `Jwt:Key`, o con menos de 32 caracteres, la aplicación no arranca (`ValidateOnStart`).
- [ ] `POST /api/auth/login` con las credenciales del Admin sembrado devuelve 200 con `accessToken`, `tokenType = "Bearer"` y `expiresAtUtc`.
- [ ] Email inexistente, contraseña incorrecta y usuario inactivo devuelven 401 `application/problem+json` con el mismo mensaje `Invalid credentials.`.
- [ ] El login encuentra al usuario aunque el email llegue en mayúsculas.
- [ ] Un login con email vacío devuelve 400 con errores de validación.
- [ ] `GET /api/auth/me` con token válido devuelve 200 con los datos del usuario, y `role` e `identificationType` como texto.
- [ ] `GET /api/auth/me` sin token, con token alterado o firmado con otra clave devuelve 401 `application/problem+json`.
- [ ] `GET /api/auth/me` de un usuario desactivado después del login devuelve 401, y de un usuario inexistente devuelve 404.
- [ ] Las políticas `AdminOnly` (Admin), `Reception` (Admin, Receptionist) y `Clinical` (Doctor) están registradas (cubierto por `AuthorizationPoliciesTests`).
- [ ] En Development, al arrancar se crea el esquema y el Admin de `Seed:Admin`; una segunda ejecución no lo duplica; con `Seed:Admin:Password` vacío se registra un warning y no se crea (cubierto por `DatabaseSeederTests`).
- [ ] Fuera de Development no se llama a `InitialiseDatabaseAsync`.
- [ ] `CreatedBy` de un aggregate guardado en una petición autenticada es el `UserId` del token, y en el seed es `null`.
- [ ] `appsettings.json` contiene las secciones `Jwt` y `Seed` con `Jwt:Key` y `Seed:Admin:Password` vacíos, y no contiene `Clinic`.
- [ ] `AuthenticateAsAsync(Role)` devuelve un `HttpClient` autenticado para cualquiera de los tres roles.
- [ ] Los tests de integración no crean `dispensario.db` en disco.
- [ ] `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `TESTING.md` y `CLAUDE.md` describen la autenticación de este spec, y `docs/PLAN.md` fila 07 apunta a `specs/07-auth-login-and-jwt.md`.

## Decisions

- **Yes:** el seed del Admin llega en la 07 con `InitialiseDatabaseAsync` = `EnsureCreatedAsync` + seed en Development. Permite hacer login con `dotnet run` desde ya. Decisión del usuario.
- **No:** dejar el seeder sin conectar o mover todo el seed a la 17. Sin un usuario no se puede probar la autenticación en local.
- **Yes:** la fila 17 cambia `EnsureCreatedAsync` por `MigrateAsync`. Una base creada con `EnsureCreated` no admite migraciones: habrá que borrar `dispensario.db` una vez.
- **Yes:** el seed es idempotente por email. Es la regla de `INFRASTRUCTURE.md` §7.
- **Yes:** la identificación del Admin se configura con `Seed:Admin:DocumentType` y `Seed:Admin:DocumentNumber` (por defecto `NationalId` y `0000000000`). `User` exige un `Document`. Decisión del usuario.
- **Yes:** login fallido → 401 genérico `Invalid credentials.` para email inexistente, contraseña incorrecta y usuario inactivo. No revela qué emails existen. Decisión del usuario.
- **No:** 403 para el usuario inactivo. Confirmaría que la contraseña era correcta.
- **Yes:** `/me` lee el usuario de la base de datos por el `sub` del token: 404 si no existe y 401 si está inactivo. Devuelve datos actuales, no los del momento del login. Decisión del usuario.
- **No:** `/me` solo con los claims. Podría devolver datos viejos.
- **No:** comprobar `IsActive` en cada petición autenticada (`OnTokenValidated`). Es una consulta extra por petición; se acepta la ventana de hasta 60 minutos.
- **Yes:** la respuesta del login es `{ accessToken, tokenType, expiresAtUtc }`, sin refresh token. Los datos del usuario se piden con `/me`. Decisión del usuario.
- **Yes:** `JwtOptions` con `ValidateOnStart`: sin `Jwt:Key` válida la app no arranca. Con `Seed:Admin:Password` vacío, el seed solo avisa. Decisión del usuario.
- **No:** fallar también sin la contraseña del seed. Obligaría a configurar ese secreto para cualquier `dotnet run`.
- **Yes:** bcrypt con factor de trabajo 12 fijo (`WorkFactor` constante). Decisión del usuario.
- **No:** factor configurable o el valor por defecto (11).
- **Yes:** `ClockSkew = TimeSpan.Zero`. El token caduca exactamente en el `expiresAtUtc` que ve el cliente. Decisión del usuario.
- **Yes:** `MapInboundClaims = false` y `RoleClaimType = "role"`, como fija `API.md` §4, para leer `sub` y `role` sin renombrar.
- **Yes:** claim `jti` además de los de la guía. Identifica cada token y deja preparada una revocación futura.
- **Yes:** `JwtBearerOptions` se configura desde `IOptions<JwtOptions>` y no leyendo la configuración en `AddApiServices`. Los tests sobrescriben `Jwt:*` y el emisor y el validador usan siempre la misma clave.
- **Yes:** `LoginCommand` es un `ICommand`, aunque no escribe, para que pase por `ValidationBehavior`. Las queries no se validan (`CLAUDE.md`).
- **Yes:** el login y `/me` viven en `Application/Users/`. Auth es parte del módulo Users. Los endpoints van en `Api/Endpoints/Auth/` con la ruta `/api/auth`. Decisión del usuario.
- **Yes:** `Policies` en `Dispensario.Api/Authorization/Policies.cs`. Las usarán todos los endpoints, no solo los de Auth.
- **Yes:** enums como texto en todo el JSON de la Api (`JsonStringEnumConverter`). Coincide con cómo se guardan en SQLite y aplica también a los specs futuros. Decisión del usuario.
- **Yes:** `AuthenticateAsAsync(Role)` crea un usuario con el hasher real y hace login por HTTP. Ejercita el flujo completo. Decisión del usuario.
- **No:** generar el token directamente con `IJwtTokenGenerator` en los tests. Más rápido, pero no prueba el login.
- **Yes:** `CurrentUser` sin `HttpContext` devuelve un usuario anónimo. El seed y los procesos en segundo plano guardan con `CreatedBy = null`.
- **Yes:** `FrameworkReference` a `Microsoft.AspNetCore.App` en Infrastructure. Lo necesitan `IHttpContextAccessor`, `IHost` y `ILogger`.
- **Yes (al implementar):** `CustomExceptionHandler` escribe el ProblemDetails con `contentType: "application/problem+json"`. Antes usaba `WriteAsJsonAsync` sin tipo, que pone `application/json`, así que ningún error con excepción cumplía `API.md` §6. Lo cubre `CustomExceptionHandlerTests`. Decisión del usuario.
- **No (al implementar):** ajustar los tests y la documentación para aceptar `application/json`, ni dejar el arreglo para otro spec.

## Risks

| Risk | Mitigation |
| --- | --- |
| Una `dispensario.db` creada con `EnsureCreated` no se actualiza cuando la fila 08 o siguientes añaden tablas | Documentado en `CLAUDE.md`: borrar `dispensario.db` en local. La fila 17 pasa a migraciones. |
| `WebApplicationFactory` con Minimal APIs: los valores de `UseSetting` podrían no tener prioridad sobre `appsettings.json` para `Jwt:*` y `Seed:*` | SPEC 05 ya fija `ConnectionStrings:Database` con `UseSetting` y funciona. `LoginEndpointTests` falla si la clave o la contraseña de test no se aplican. |
| `ValidateOnStart` de `JwtOptions` rompe `DependencyInjectionTests` u otros tests que construyen el proveedor sin `Jwt` | La validación solo se ejecuta al arrancar un host. Los tests que construyen un `ServiceProvider` a mano no la disparan; el paso 4 lo confirma. |
| bcrypt de coste 12 (~250 ms por hash) ralentiza los tests que usan el hasher real | Los tests de handlers usan un hasher de NSubstitute. Solo `BcryptPasswordHasherTests`, el seed y la integración pagan el coste. |
| El tiempo de respuesta revela si un email existe (sin usuario no se ejecuta bcrypt) | Aceptado en v1 y anotado fuera de alcance. Un spec futuro puede verificar contra un hash ficticio. |
| Un usuario desactivado sigue accediendo a los demás endpoints hasta que caduca su token | Ventana máxima de `ExpirationMinutes` (60). `/me` ya lo rechaza; la revocación queda para un spec futuro. |
| Versiones de `Microsoft.IdentityModel.*` desalineadas entre Infrastructure y JwtBearer | El paso 1 fija `Microsoft.IdentityModel.JsonWebTokens` a la versión transitiva de `JwtBearer` 10.0.14. |
| El seed lanza `DomainException` al arrancar si `Seed:Admin` tiene un email o documento inválidos | Es intencionado: la configuración inválida falla al arrancar y no en el primer login. Los valores por defecto de `appsettings.json` son válidos. |
| Otro usuario ya tiene el documento del Admin con un email distinto: el seed lanza `DbUpdateException` | Solo puede pasar con datos insertados a mano en desarrollo. Se corrige cambiando `Seed:Admin:DocumentNumber`. |

## What is **not** in this spec

- RegisterUser, ChangePassword, DeactivateUser y GetUsers.
- Refresh tokens, logout y revocación.
- Comprobación de `IsActive` en cada petición.
- Bloqueo por intentos fallidos, rate limiting y protección frente a ataques de tiempo.
- Migraciones, `MigrateAsync` y datos de ejemplo.
- Uso de las políticas en endpoints de negocio.

Cada uno de ellos va en su propio spec (filas 08, 17 o futuras).

# Guía de la capa Api

Proyecto: `src/Services/Dispensario/Dispensario.Api` (Minimal APIs + **Carter**).
La Api es delgada: traduce HTTP ↔ command/query, aplica la autorización y nada más.

## 1. Estructura
```
Authorization/
  Policies.cs                  # nombres de las políticas por rol
Endpoints/
  Auth/                        # LoginEndpoint, RegisterEndpoint (autoregistro), GetCurrentUserEndpoint
  Users/                       # alta, listado, detalle, perfil, contraseña, activar/desactivar, vínculo con doctor (LinkDoctorEndpoint); /api/users/me y /me/password
  Doctors/                     # alta, listado, detalle, edición, activar/desactivar (escritura AdminOnly, lecturas autenticadas);
                               # horario semanal: SetDoctorScheduleEndpoint (AdminOrDoctor) y GetDoctorScheduleEndpoint
  Patients/                    # alta, listado, detalle, búsqueda por documento (/api/patients/by-document), edición, activar/desactivar
                               # (escritura Reception, lecturas autenticadas)
  Appointments/                # ScheduleAppointmentEndpoint (Reception), GetAppointmentsEndpoint y GetAppointmentByIdEndpoint
                               # (autenticados; el handler limita al Doctor a sus propias citas). Horas locales sin offset
  Consultations/
Extensions/
  ApiServiceExtensions.cs      # AddApiServices: Carter, OpenAPI, JSON, ProblemDetails, exception handler, JWT, políticas, health
  OpenApi/BearerSecuritySchemeTransformer.cs
Program.cs
appsettings.json  appsettings.Development.json
```
Paquetes de la Api: `Carter`, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer` y `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.

**JSON:** `AddApiServices` registra `JsonStringEnumConverter` con `ConfigureHttpJsonOptions`, así que los enums viajan como texto en las peticiones y las respuestas (`"role": "Admin"`), igual que se guardan en SQLite.

## 2. Plantilla de endpoint
```csharp
namespace Dispensario.Api.Endpoints.Patients;

public sealed record CreatePatientRequest(
    DocumentType DocumentType, string DocumentNumber,
    string FirstName, string LastName, DateOnly BirthDate, Gender Gender,
    string? Email, string? Phone);

public sealed record CreatePatientResponse(Guid Id);

/// <summary>Registra un paciente nuevo.</summary>
public sealed class CreatePatientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/patients", async (CreatePatientRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = request.Adapt<CreatePatientCommand>();
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/patients/{result.Id}", result.Adapt<CreatePatientResponse>());
        })
        .WithName("CreatePatient")
        .WithTags("Patients")
        .WithSummary("Create patient")
        .Produces<CreatePatientResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Reception);
    }
}
```
Reglas:
- Un archivo por endpoint; los records `Request` y `Response` viven en el mismo archivo.
- Los parámetros de query de listados se agrupan con `[AsParameters]`.
- Códigos de éxito: `POST` crea → **201** con `Location`; `POST` de acción (`/cancel`) → **204** o 200; `PUT` → **204**; `GET` → **200**.
- **No** se hace `try/catch` en los endpoints: los errores los resuelve `CustomExceptionHandler`.
- Las rutas con ID usan la restricción `{id:guid}`, para que rutas literales como `/api/users/me` no se interpreten como un ID.
- Un enum del cuerpo que decide permisos (como `Role` en `POST /api/users`) se declara nullable en el request: si falta, el valor por defecto del enum (`Admin`) se aplicaría en silencio. El endpoint pasa un valor fuera del enum y el validador lo rechaza con 400.
- Los endpoints no leen `ICurrentUser`: las reglas que dependen del usuario actual van en los handlers.
- Toda ruta lleva `WithTags`, `WithName` y `WithSummary` para que se vea bien en Scalar.
- Cada endpoint nuevo o modificado se documenta en la sección **Endpoints** del `README.md`: fila en la tabla general y apartado con ejemplo de petición/respuesta y tabla de códigos.

## 4. Autorización
```csharp
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);   // Admin
    public const string Reception = nameof(Reception);   // Admin, Receptionist
    public const string Clinical  = nameof(Clinical);    // Doctor
    public const string AdminOrDoctor = nameof(AdminOrDoctor);   // Admin, Doctor (el handler comprueba que el doctor sea el dueño)
}

services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(Role.Admin)))
    .AddPolicy(Policies.Reception, p => p.RequireRole(nameof(Role.Admin), nameof(Role.Receptionist)))
    .AddPolicy(Policies.Clinical,  p => p.RequireRole(nameof(Role.Doctor)))
    .AddPolicy(Policies.AdminOrDoctor, p => p.RequireRole(nameof(Role.Admin), nameof(Role.Doctor)));
```
- `Policies` vive en `Dispensario.Api/Authorization/Policies.cs`. Las lecturas generales solo exigen `RequireAuthorization()`; los endpoints anónimos (login) llevan `AllowAnonymous()`.

Autenticación: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`. `JwtBearerOptions` se configura desde `IOptions<JwtOptions>` (`AddOptions<JwtBearerOptions>(scheme).Configure<IOptions<JwtOptions>>`), así que el emisor y la validación usan siempre la misma clave, también cuando los tests la sobrescriben:
- Valida issuer, audience, clave de firma y tiempo de vida, con `ClockSkew = TimeSpan.Zero` (el token caduca exactamente en `expiresAtUtc`).
- `MapInboundClaims = false` para conservar `sub` y `role`, con `NameClaimType = "name"` y `RoleClaimType = "role"`.
- Sin token o con un token inválido, el middleware responde 401 y `UseStatusCodePages` lo convierte en `application/problem+json`.

Un token sigue siendo válido hasta que caduca aunque el usuario se desactive: solo `/api/auth/me` comprueba `IsActive`.

## 5. Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();              // 404/405 sin cuerpo → ProblemDetails
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                  // /openapi/v1.json
    app.MapScalarApiReference();       // /scalar
    await app.InitialiseDatabaseAsync();   // EnsureCreated + seed del Admin; MigrateAsync desde la fila 17
}

app.MapCarter();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;          // necesario para WebApplicationFactory en los tests
```
- `/openapi/v1.json` y `/scalar` solo existen en Development; `dotnet run` abre `/scalar` (`launchUrl` en `launchSettings.json`).
- `/health` comprueba la base de datos con `AddDbContextCheck<DispensarioDbContext>` y una consulta propia (`SELECT 1`). La prueba por defecto (`CanConnectAsync`) en SQLite solo mira si existe el archivo, y daría Unhealthy hasta que haya migraciones. Abrir la conexión crea `dispensario.db` vacío si no existe.

## 6. Errores (ProblemDetails)
| Excepción | HTTP |
|---|---|
| `ValidationException` (FluentValidation) | 400 + `ValidationErrors` |
| `BadRequestException`, `DomainException` | 400 |
| `BadHttpRequestException` (ASP.NET no puede leer la petición: JSON mal formado, enum desconocido) | su `StatusCode` (400, 413…), sin `ValidationErrors` (fila 10) |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| otra | 500 |

Todas las respuestas de error salen como `application/problem+json`:
- Con excepción: `CustomExceptionHandler` escribe el ProblemDetails con `contentType: "application/problem+json"` (desde la fila 07; antes salía `application/json`).
- Sin excepción (ruta inexistente → 404, método no permitido → 405, sin token → 401): `AddProblemDetails()` + `UseStatusCodePages()`.

## 7. OpenAPI + Scalar
- `builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` declara el esquema `Bearer` (`http`, `bearer`, `JWT`) y un requisito de seguridad global, para poder probar con token desde Scalar.
- .NET 10 usa **Microsoft.OpenApi 2.x** (hoy 2.12.2): los tipos están en el namespace plano `Microsoft.OpenApi` (`OpenApiSecurityScheme`, `OpenApiSecuritySchemeReference(name, document)`, `IOpenApiSecurityScheme`), no en `Microsoft.OpenApi.Models` como en .NET 8/9.

## 8. Configuración (`appsettings.json`)
Hoy `appsettings.json` tiene `ConnectionStrings`, `Jwt`, `MediatR`, `Clinic` (desde la fila 12) y `Seed` (los secretos vacíos). Cada spec añade su sección cuando la lee. Forma final:

```json
{
  "ConnectionStrings": { "Database": "Data Source=dispensario.db" },
  "Jwt": { "Issuer": "Dispensario", "Audience": "Dispensario.Clients", "Key": "", "ExpirationMinutes": 60 },
  "MediatR": { "LicenseKey": "" },
  "Clinic": { "TimeZone": "America/Guayaquil" },
  "Seed": {
    "Admin": {
      "Email": "admin@dispensario.local", "Password": "",
      "FirstName": "System", "LastName": "Admin",
      "DocumentType": "NationalId", "DocumentNumber": "0000000000"
    }
  }
}
```
Los valores vacíos se cargan desde user-secrets (el proyecto Api tiene `UserSecretsId`) o variables de entorno:
- `Jwt:Key` es obligatoria (mínimo 32 caracteres): sin ella la aplicación no arranca.
- `Seed:Admin:Password` es opcional: si está vacía, el seed avisa en el log y no crea el Admin.
- `Clinic:TimeZone` (ID IANA, por defecto `America/Guayaquil`) se valida al arrancar: con una zona inexistente la aplicación no arranca.

```bash
dotnet user-secrets set "Jwt:Key" "<min-32-chars>" --project src/Services/Dispensario/Dispensario.Api
dotnet user-secrets set "Seed:Admin:Password" "<password>" --project src/Services/Dispensario/Dispensario.Api
```
`Data Source=dispensario.db` es relativo al directorio de trabajo: con `dotnet run --project` el archivo se crea en la carpeta del proyecto Api. `*.db` está en `.gitignore`.

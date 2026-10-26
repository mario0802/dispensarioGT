# Guía de la capa Api

Proyecto: `src/Services/Dispensario/Dispensario.Api` (Minimal APIs + **Carter**).
La Api es delgada: traduce HTTP ↔ command/query, aplica la autorización y nada más.

## 1. Estructura
```
Endpoints/
  Auth/  Users/  Doctors/  Patients/  Appointments/  Consultations/
Extensions/
  ApiServiceExtensions.cs      # AddApiServices: Carter, OpenAPI, ProblemDetails, exception handler, health (auth y policies en la fila 07)
  OpenApi/BearerSecuritySchemeTransformer.cs
Program.cs
appsettings.json  appsettings.Development.json
```
Paquetes de la Api: `Carter`, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore` y `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.

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
- Toda ruta lleva `WithTags`, `WithName` y `WithSummary` para que se vea bien en Scalar.

## 4. Autorización
```csharp
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);   // Admin
    public const string Reception = nameof(Reception);   // Admin, Receptionist
    public const string Clinical  = nameof(Clinical);    // Doctor
}

services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(Role.Admin)))
    .AddPolicy(Policies.Reception, p => p.RequireRole(nameof(Role.Admin), nameof(Role.Receptionist)))
    .AddPolicy(Policies.Clinical,  p => p.RequireRole(nameof(Role.Doctor)));
```
Autenticación: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`, que valida el issuer, la audience, la clave y el tiempo de vida con `JwtOptions`. Se usa `MapInboundClaims = false` para conservar `sub` y `role`, con `RoleClaimType = "role"`.

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
// app.UseAuthentication(); app.UseAuthorization();   ← fila 07

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                  // /openapi/v1.json
    app.MapScalarApiReference();       // /scalar
    // await app.InitialiseDatabaseAsync();   ← fila 17
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
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| otra | 500 |

Los errores sin excepción (ruta inexistente → 404, método no permitido → 405) también salen como `application/problem+json` gracias a `AddProblemDetails()` + `UseStatusCodePages()`.

## 7. OpenAPI + Scalar
- `builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` declara el esquema `Bearer` (`http`, `bearer`, `JWT`) y un requisito de seguridad global, para poder probar con token desde Scalar.
- .NET 10 usa **Microsoft.OpenApi 2.x** (hoy 2.12.2): los tipos están en el namespace plano `Microsoft.OpenApi` (`OpenApiSecurityScheme`, `OpenApiSecuritySchemeReference(name, document)`, `IOpenApiSecurityScheme`), no en `Microsoft.OpenApi.Models` como en .NET 8/9.

## 8. Configuración (`appsettings.json`)
Hoy `appsettings.json` solo tiene `ConnectionStrings:Database` y `MediatR:LicenseKey` (vacío). Cada spec añade su sección cuando la lee: `Jwt` (fila 07), `Clinic` (fila 14) y `Seed` (fila 17). Forma final:

```json
{
  "ConnectionStrings": { "Database": "Data Source=dispensario.db" },
  "Jwt": { "Issuer": "Dispensario", "Audience": "Dispensario.Clients", "Key": "", "ExpirationMinutes": 60 },
  "MediatR": { "LicenseKey": "" },
  "Clinic": { "TimeZone": "America/Guayaquil" },
  "Seed": { "Admin": { "Email": "admin@dispensario.local", "Password": "", "FirstName": "System", "LastName": "Admin" } }
}
```
Los valores vacíos se cargan desde user-secrets (el proyecto Api tiene `UserSecretsId`) o variables de entorno.
`Data Source=dispensario.db` es relativo al directorio de trabajo: con `dotnet run --project` el archivo se crea en la carpeta del proyecto Api. `*.db` está en `.gitignore`.

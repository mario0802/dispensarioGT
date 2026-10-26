# Guía de la capa Api

Proyecto: `src/Services/Dispensario/Dispensario.Api` (Minimal APIs + **Carter**).
La Api es delgada: traduce HTTP ↔ command/query, aplica la autorización y nada más.

## 1. Estructura
```
Endpoints/
  Auth/  Users/  Doctors/  Patients/  Appointments/  Consultations/
Extensions/
  ApiServiceExtensions.cs      # AddApiServices: Carter, auth, policies, OpenAPI, health, exception handler
  OpenApi/BearerSecuritySchemeTransformer.cs
Program.cs
appsettings.json  appsettings.Development.json
```

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

## 5. Program.cs (forma esperada)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(_ => { });
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();       // /scalar
    await app.InitialiseDatabaseAsync();
}

app.MapCarter();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;          // necesario para WebApplicationFactory en los tests
```

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

## 7. OpenAPI + Scalar
- `builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` declara el esquema `Bearer` para poder probar con token desde Scalar.
- .NET 10 usa **Microsoft.OpenApi 2.x**: cambian namespaces y referencias de esquemas respecto a .NET 8/9. Hay que consultar la documentación actual (Context7) al implementar el host de la API (feature 04).

## 8. Configuración (`appsettings.json`)
```json
{
  "ConnectionStrings": { "Database": "Data Source=dispensario.db" },
  "Jwt": { "Issuer": "Dispensario", "Audience": "Dispensario.Clients", "Key": "", "ExpirationMinutes": 60 },
  "MediatR": { "LicenseKey": "" },
  "Clinic": { "TimeZone": "America/Guayaquil" },
  "Seed": { "Admin": { "Email": "admin@dispensario.local", "Password": "", "FirstName": "System", "LastName": "Admin" } }
}
```
Los valores vacíos se cargan desde user-secrets o variables de entorno.

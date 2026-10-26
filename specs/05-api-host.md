# SPEC 05 — Host de la API

> **Status:** Approved
> **Depends on:** SPEC 04
> **Date:** 2026-10-26
> **Objective:** Convertir `Dispensario.Api` en un host completo (`AddApplication` con MediatR, behaviors, validadores y Mapster; Carter; OpenAPI + Scalar con esquema Bearer; ProblemDetails; `/health` con chequeo de la base de datos; `appsettings`) y cubrirlo con los primeros tests de integración.

## Por qué este spec existe

Hasta ahora `Program.cs` es un host vacío y `AddInfrastructure` no se llama desde ningún sitio, porque `DispatchDomainEventsInterceptor` necesita el `IPublisher` que registra MediatR.
Este spec conecta las capas para que la fila 06 ya pueda añadir su primer endpoint.
La autenticación completa (JWT, políticas, `Role`) queda fuera: depende del módulo Users (filas 06–07).
Aquí solo se declara el esquema `Bearer` en OpenAPI, para que Scalar ya muestre el campo del token.

## Scope

**In:**

- Paquetes `Carter`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.OpenApi` y `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` en `Directory.Packages.props`, referenciados desde `Dispensario.Api`.
- `AddApplication(IServiceCollection, IConfiguration)` en `Dispensario.Application/DependencyInjection.cs`: MediatR con `LoggingBehavior` y `ValidationBehavior`, validadores de FluentValidation y escaneo de Mapster.
- `AddApiServices(IServiceCollection, IConfiguration)` en `Dispensario.Api/Extensions/ApiServiceExtensions.cs`: Carter, OpenAPI con `BearerSecuritySchemeTransformer`, ProblemDetails, `CustomExceptionHandler` y health checks con `AddDbContextCheck<DispensarioDbContext>()`.
- `BearerSecuritySchemeTransformer` en `Dispensario.Api/Extensions/OpenApi/`.
- `Program.cs` con la tubería completa: `UseExceptionHandler`, `UseStatusCodePages`, OpenAPI y Scalar solo en Development, `MapCarter` y `MapHealthChecks("/health")`, y `public partial class Program;`.
- `appsettings.json` con `ConnectionStrings:Database` y `MediatR:LicenseKey` (vacío).
- `launchSettings.json`: los dos perfiles abren el navegador en `/scalar`.
- Tests de integración: `DispensarioApiFactory` y los tests de `/health`, OpenAPI, Scalar y ProblemDetails en `tests/Dispensario.Api.IntegrationTests/`.
- Test unitario de `AddApplication` en `tests/Dispensario.Application.UnitTests/`.
- Actualizar `API.md`, `APPLICATION.md`, `TESTING.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- `AddAuthentication`, `AddJwtBearer`, `JwtOptions`, `UseAuthentication`, `UseAuthorization`, la clase `Policies` y el helper `AuthenticateAsAsync`: fila 07.
- Las secciones `Jwt`, `Seed` y `Clinic` de `appsettings.json`: las añade el spec que las lee (filas 07, 14 y 17).
- `InitialiseDatabaseAsync`, migraciones y seed: fila 17.
- Endpoints de negocio: desde la fila 06.
- `UseHttpsRedirection`, CORS, rate limiting y versionado de la API.
- Cambios en `CustomExceptionHandler`, `LoggingBehavior` o `ValidationBehavior`.

## Data model

Este spec no introduce estructuras de dominio ni tablas. Introduce configuración, registro de servicios y la tubería HTTP.

### Paquetes

| Paquete | Versión | Lo referencia |
| --- | --- | --- |
| `Carter` | 10.0.0 | `Dispensario.Api` |
| `Scalar.AspNetCore` | 2.18.3 | `Dispensario.Api` |
| `Microsoft.AspNetCore.OpenApi` | 10.0.14 | `Dispensario.Api` |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | 10.0.14 | `Dispensario.Api` |

- Son las últimas versiones estables a 2026-10-26.
- MediatR, FluentValidation y Mapster llegan a Application de forma transitiva desde `BuildingBlocks`.

### `Dispensario.Application/DependencyInjection.cs` (namespace `Dispensario.Application`)

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
{
    var assembly = typeof(DependencyInjection).Assembly;

    services.AddMediatR(cfg =>
    {
        // Solo se asigna si hay clave: sin ella MediatR registra un aviso de licencia
        var licenseKey = configuration["MediatR:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey))
            cfg.LicenseKey = licenseKey;

        cfg.RegisterServicesFromAssembly(assembly);
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });
    services.AddValidatorsFromAssembly(assembly);
    TypeAdapterConfig.GlobalSettings.Scan(assembly);
    return services;
}
```

- El orden de los behaviors es Logging → Validation, como en `ARCHITECTURE.md` §4.

### `Dispensario.Api/` (namespaces iguales a las carpetas)

```
Extensions/
  ApiServiceExtensions.cs                  # AddApiServices
  OpenApi/BearerSecuritySchemeTransformer.cs
Program.cs
appsettings.json  appsettings.Development.json
Properties/launchSettings.json
```

`AddApiServices` registra:

| Servicio | Llamada |
| --- | --- |
| Carter | `services.AddCarter()` |
| OpenAPI | `services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` |
| ProblemDetails | `services.AddProblemDetails()` |
| Manejo de excepciones | `services.AddExceptionHandler<CustomExceptionHandler>()` |
| Health checks | `services.AddHealthChecks().AddDbContextCheck<DispensarioDbContext>(customTestQuery: ...)` con `SELECT 1` *(ajuste al implementar, ver Decisions)* |

`BearerSecuritySchemeTransformer` (`IOpenApiDocumentTransformer`):

- Añade a `components.securitySchemes` un esquema `Bearer` de tipo `http`, `scheme: "bearer"` y `bearerFormat: "JWT"`.
- Añade un requisito de seguridad global con ese esquema, para que Scalar envíe el token en todas las peticiones.
- Se implementa con **Microsoft.OpenApi 2.x**: los tipos y namespaces se consultan en la documentación actual (Context7) al implementar, como pide `API.md` §7.

`Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();       // 404/405 sin cuerpo → ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}

app.MapCarter();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;   // necesario para WebApplicationFactory en los tests
```

`appsettings.json` (se conservan `Logging` y `AllowedHosts`):

```json
{
  "ConnectionStrings": { "Database": "Data Source=dispensario.db" },
  "MediatR": { "LicenseKey": "" }
}
```

- `MediatR:LicenseKey` se carga desde user-secrets: `dotnet user-secrets set "MediatR:LicenseKey" "<key>" --project src/Services/Dispensario/Dispensario.Api`.
- Si el proyecto Api no tiene `UserSecretsId`, se añade al `.csproj` (`dotnet user-secrets init`).
- `appsettings.Development.json` no cambia.

`launchSettings.json`: los perfiles `http` y `https` añaden `"launchUrl": "scalar"`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Api.IntegrationTests/Common/DispensarioApiFactory.cs` | `WebApplicationFactory<Program>` en entorno Development. Sustituye la configuración del `DbContext` por una `SqliteConnection("Data Source=:memory:")` compartida y abierta mientras vive la factory, conservando los interceptores. Fija `ConnectionStrings:Database` y `MediatR:LicenseKey` con `UseSetting` para no depender de `appsettings` ni de `dispensario.db` |
| `tests/Dispensario.Api.IntegrationTests/HealthEndpointTests.cs` | `GET /health` → 200 con cuerpo `Healthy`; el `DispensarioDbContext` de la factory usa `Data Source=:memory:` |
| `tests/Dispensario.Api.IntegrationTests/OpenApiEndpointTests.cs` | `GET /openapi/v1.json` → 200 con el esquema `Bearer` (`type: http`, `scheme: bearer`, `bearerFormat: JWT`) en `components.securitySchemes`; `GET /scalar` → 200 con `text/html` |
| `tests/Dispensario.Api.IntegrationTests/ProblemDetailsTests.cs` | `GET /api/does-not-exist` → 404 con `Content-Type: application/problem+json` y `status: 404` en el cuerpo |
| `tests/Dispensario.Application.UnitTests/ApplicationDependencyInjectionTests.cs` | Tras `AddLogging()` + `AddApplication(...)`, `ISender` y `IPublisher` se resuelven; `IPipelineBehavior<,>` está registrado con `LoggingBehavior<,>` antes que `ValidationBehavior<,>` |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.
- Cada clase de integración usa la factory con `IClassFixture<DispensarioApiFactory>`.

## Implementation plan

1. Añadir los cuatro paquetes a `Directory.Packages.props` y referenciarlos desde `Dispensario.Api.csproj`. Verificar: `dotnet build Dispensario.slnx` y `dotnet test Dispensario.slnx` en verde.
2. Crear `Dispensario.Application/DependencyInjection.cs` con `AddApplication` y `ApplicationDependencyInjectionTests`. Verificar: `dotnet test` en verde.
3. Consultar en Context7 la API de transformers de `Microsoft.AspNetCore.OpenApi` 10 y Microsoft.OpenApi 2.x. Crear `Extensions/OpenApi/BearerSecuritySchemeTransformer.cs` y `Extensions/ApiServiceExtensions.cs` con `AddApiServices`. Verificar: compila.
4. Reescribir `Program.cs`, añadir las secciones a `appsettings.json`, añadir `UserSecretsId` si falta y añadir `launchUrl` a `launchSettings.json`. Verificar: `dotnet run --project src/Services/Dispensario/Dispensario.Api` arranca; `/health`, `/openapi/v1.json` y `/scalar` responden 200 en el navegador o con `curl`.
5. Crear `Common/DispensarioApiFactory.cs` y `HealthEndpointTests`. Verificar: `dotnet test` en verde.
6. Crear `OpenApiEndpointTests` y `ProblemDetailsTests`. Verificar: `dotnet test` en verde.
7. Actualizar la documentación:
   - `docs/guides/API.md`: §1 estructura actual; §5 `Program.cs` real (auth y `InitialiseDatabaseAsync` marcados como filas 07 y 17); §6 404/405 sin excepción → ProblemDetails por `UseStatusCodePages`; §7 transformer real y paquetes; §8 secciones actuales de `appsettings` y cuáles llegan después.
   - `docs/guides/APPLICATION.md` §7: `LicenseKey` solo si no está vacío.
   - `docs/guides/TESTING.md` "Integración": estado actual de `DispensarioApiFactory` (sin seed, sin `Jwt` ni `AuthenticateAsAsync` hasta la fila 07).
   - `CLAUDE.md`: "Estado actual" (host completo, sin auth).
   - `docs/PLAN.md`: la fila 05 apunta a este spec.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 04.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores; `Dispensario.Api.IntegrationTests` ya no informa "no hay ninguna prueba disponible".
- [ ] `Directory.Packages.props` declara `Carter`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.OpenApi` y `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, y ningún `.csproj` lleva atributo `Version`.
- [ ] `AddApplication` registra MediatR con `LoggingBehavior` antes que `ValidationBehavior`, los validadores del ensamblado y el escaneo de Mapster (cubierto por `ApplicationDependencyInjectionTests`).
- [ ] Con `MediatR:LicenseKey` vacío la aplicación arranca, y `cfg.LicenseKey` no se asigna.
- [ ] `Program.cs` llama a `AddApplication`, `AddInfrastructure` y `AddApiServices`, y declara `public partial class Program;`.
- [ ] `GET /health` devuelve 200 y `Healthy` (cubierto por `HealthEndpointTests`).
- [ ] En Development, `GET /openapi/v1.json` devuelve el documento con el esquema `Bearer` (`http`, `bearer`, `JWT`) y `GET /scalar` devuelve HTML (cubierto por `OpenApiEndpointTests`).
- [ ] Fuera de Development no se mapean `/openapi/v1.json` ni `/scalar` (`Program.cs` los registra dentro de `IsDevelopment()`).
- [ ] Una ruta inexistente devuelve 404 con `application/problem+json` (cubierto por `ProblemDetailsTests`).
- [ ] `CustomExceptionHandler` está registrado con `AddExceptionHandler` y `app.UseExceptionHandler()` está en la tubería.
- [ ] `appsettings.json` contiene `ConnectionStrings:Database = "Data Source=dispensario.db"` y `MediatR:LicenseKey = ""`, y no contiene `Jwt`, `Seed` ni `Clinic`.
- [ ] Los perfiles de `launchSettings.json` abren `/scalar`.
- [ ] Los tests de integración no crean `dispensario.db` en disco.
- [ ] No existen `AddAuthentication`, `AddJwtBearer`, `UseAuthentication`, `UseAuthorization` ni `Policies` en `src/`.
- [ ] `API.md`, `APPLICATION.md`, `TESTING.md` y `CLAUDE.md` describen el host actual, y `docs/PLAN.md` fila 05 apunta a `specs/05-api-host.md`.

## Decisions

- **Yes:** solo el esquema `Bearer` en OpenAPI, sin middleware de autenticación ni políticas. `JwtOptions`, la validación del token y las políticas con `Role` dependen de las filas 06–07 y se añaden completos allí.
- **No:** `AddAuthentication().AddJwtBearer()` vacío desde ya. Sería configuración a medias que la fila 07 tendría que rehacer.
- **Yes:** `/health` comprueba la base de datos con `AddDbContextCheck<DispensarioDbContext>()`. Un host vivo sin base de datos no está sano.
- **Yes:** el chequeo de salud se registra en la Api aunque nombre al `DbContext`. Es configuración del host, no acceso a datos desde un endpoint, y la Api ya referencia Infrastructure.
- **Yes (al implementar):** el chequeo de la base de datos usa una consulta propia (`SELECT 1`) en lugar de `CanConnectAsync`. En SQLite, `CanConnectAsync` solo comprueba si existe el archivo, y con `dotnet run` daba 503 Unhealthy hasta que existan migraciones (fila 17). Abrir la conexión crea `dispensario.db` vacío si no existe (ignorado por `.gitignore`).
- **No (al implementar):** aceptar 503 en desarrollo hasta la fila 17, o quitar el chequeo de la base de datos. Decisión del usuario.
- **No:** health check solo del proceso. No detecta el fallo más probable de SQLite (ruta o permisos del archivo).
- **Yes:** `DispensarioApiFactory` y smoke tests en este spec. Sustituir el `DbContext` en la factory es la parte delicada y conviene resolverla antes de que lleguen los endpoints.
- **Yes:** la factory corre en Development. Es donde existen `/openapi` y `/scalar`, y es el entorno que usará `InitialiseDatabaseAsync` (fila 17).
- **No:** probar `CustomExceptionHandler` con un endpoint de prueba. Sus mapeos ya están cubiertos por `CustomExceptionHandlerTests` y no se añaden rutas que no son del producto.
- **Yes:** `appsettings.json` solo con las secciones que se leen hoy (`ConnectionStrings`, `MediatR`). Cada spec añade la suya, igual que los `DbSet`.
- **No:** la forma completa de `API.md` §8 desde ya. Serían claves vacías que nadie lee.
- **Yes:** OpenAPI y Scalar solo en Development, como fija `API.md` §5.
- **Yes:** `AddProblemDetails()` + `UseStatusCodePages()`. Todas las respuestas de error, con o sin excepción, salen como `application/problem+json`.
- **No:** `UseHttpsRedirection`. API interna; el perfil `http` y los tests no necesitan certificado. Se añade al definir el despliegue.
- **Yes:** `cfg.LicenseKey` solo se asigna si la clave no está vacía. Una cadena vacía no es una clave válida y la aplicación debe arrancar sin ella.
- **Yes:** `launchUrl: "scalar"` en los perfiles de desarrollo. `dotnet run` abre directamente la documentación.
- **No:** `InitialiseDatabaseAsync` en este spec. Todavía no hay entidades ni migraciones (fila 17).

## Risks

| Risk | Mitigation |
| --- | --- |
| La API de transformers cambió con Microsoft.OpenApi 2.x (namespaces, `OpenApiSecuritySchemeReference`) | El paso 3 consulta Context7 antes de escribir el transformer; `OpenApiEndpointTests` verifica el JSON resultante. |
| En EF Core 10, volver a llamar a `AddDbContext` en la factory acumula la configuración original (`IDbContextOptionsConfiguration<T>`) y la cadena de `appsettings` sigue activa | La factory elimina `DbContextOptions<DispensarioDbContext>` y `IDbContextOptionsConfiguration<DispensarioDbContext>` antes de registrar SQLite en memoria, y vuelve a añadir los interceptores. El criterio "no se crea `dispensario.db`" lo comprueba. |
| `ValidationBehavior` tiene la restricción `where TRequest : ICommand<TResponse>` y la DI podría fallar al construirlo para una query | La DI de .NET omite las implementaciones genéricas abiertas cuyas restricciones no se cumplen. `ApplicationDependencyInjectionTests` resuelve el pipeline; la fila 06 lo confirma con una query real. |
| Sin `MediatR:LicenseKey`, MediatR 14 registra un aviso o un error de licencia en el log al arrancar | Es el comportamiento documentado en `CLAUDE.md`; no impide arrancar. Los tests fijan la clave vacía y no dependen del log. |
| `Carter` 10 escanea ensamblados y no encuentra módulos todavía | `MapCarter()` sin módulos es válido; la fila 06 añade el primero. |
| `Data Source=dispensario.db` es relativo al directorio de trabajo, no al del proyecto | Con `dotnet run --project` el archivo se crea en la raíz desde la que se ejecuta; `*.db` ya está en `.gitignore`. Se documenta en `API.md` §8. |

## What is **not** in this spec

- Autenticación JWT, `JwtOptions`, políticas y `AuthenticateAsAsync`.
- Secciones `Jwt`, `Seed` y `Clinic` de `appsettings.json`.
- Migraciones, `InitialiseDatabaseAsync` y seed.
- Endpoints de negocio.
- HTTPS, CORS, rate limiting y versionado.
- Cambios en `CustomExceptionHandler` o en los behaviors.

Cada uno de ellos va en su propio spec (filas 06, 07, 17 o futuras).

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Dispensario Médico
Backend en .NET 10 para gestionar un dispensario médico: doctores, pacientes, citas y consultas.
El desarrollo sigue **SDD (Spec-Driven Development)** con las skills `/spec` y `/spec-impl` (en `.claude/skills/`, sincronizadas con `.agents/skills/` mediante `skills-lock.json`). Los specs viven en `specs/NN-slug.md`.

> **Estado actual:** existen `Dispensario.slnx`, `global.json`, los props centrales, `BuildingBlocks` y `BuildingBlocks.Domain` (abstracciones DDD completas), los cuatro proyectos `Dispensario.*` (`Domain` con los VOs de `Common/`, los aggregates `User`, `Doctor`, `Patient`, `Appointment` y `Consultation`, el VO `ScheduleBlock` del horario y `ConsultationClosedDomainEvent`, definido pero sin emitir), la persistencia en `Infrastructure` (`DispensarioDbContext` con `DbSet<User>`, `DbSet<Doctor>`, `DbSet<Patient>`, `DbSet<Appointment>`, `DbSet<Consultation>`, `UserConfiguration`, `DoctorConfiguration` con la tabla propia `DoctorScheduleBlocks`, `PatientConfiguration`, `AppointmentConfiguration` y `ConsultationConfiguration`, interceptores, convenciones y `AddInfrastructure`), la migración `InitialCreate`, la autenticación (`BcryptPasswordHasher`, `JwtTokenGenerator`, `CurrentUser`, seed del Admin), los datos de ejemplo de Development (`SampleDataSeeder`), el host completo de la `Api` (`AddApplication` con la zona horaria del dispensario `IClinicTimeZone`, Carter, OpenAPI + Scalar con esquema Bearer, ProblemDetails, `/health`, JWT Bearer, políticas `AdminOnly`/`Reception`/`Clinical`/`AdminOrDoctor` y los endpoints `POST /api/auth/login`, `POST /api/auth/register` y `GET /api/auth/me`), la gestión de usuarios (`/api/users`: alta, listado paginado, detalle, perfil, reseteo y cambio de contraseña, activar y desactivar, vínculo con doctor), el CRUD de doctores (`/api/doctors`: alta, listado paginado, detalle, edición, activar y desactivar), el horario semanal (`PUT`/`GET /api/doctors/{id}/schedule`), el CRUD de pacientes (`/api/patients`: alta, listado paginado, detalle, búsqueda por documento, edición, activar y desactivar), el agendamiento de citas (`/api/appointments`: alta, listado paginado con filtros y detalle), el ciclo de vida de la cita (confirmar, cancelar, marcar no asistida y reprogramar), los slots disponibles (`GET /api/doctors/{id}/available-slots?date=`), las consultas (`/api/consultations`: iniciar, editar, cerrar y detalle, e historia del paciente en `GET /api/patients/{id}/consultations`) y los proyectos de tests en `tests/`, incluida la `DispensarioApiFactory` de integración con `AuthenticateAsAsync(Role)` (features 01 a 15 y 17, y la 16 en parte: `Appointment.Complete` y el tipo del evento, sin emitirlo ni consumirlo). Funcionan `dotnet build`, `dotnet test` y `dotnet run`; queda pendiente el resto de la 16 y la 18 del backlog (`docs/PLAN.md`).

## Reglas de trabajo (obligatorias)
1. **No escribir código sin un spec `Approved`.** Los specs se crean con `/spec` (que no escribe código) y se implementan con `/spec-impl`. Nada de andamiaje, refactors ni features "de paso".
2. Al redactar o implementar un spec, leer las guías de `docs/` de las capas que toca: los specs deben respetar la arquitectura y las convenciones definidas ahí. Si algo es ambiguo, preguntar en vez de suponer.
3. Al terminar un spec: compilar, ejecutar los tests, verificar los criterios de aceptación, anotar el archivo del spec en el backlog de `docs/PLAN.md` y, si añade o cambia endpoints, actualizar la sección **Endpoints** del `README.md` (ruta, acceso, petición, respuesta y códigos de error). El cambio de estado (`Approved` / `Implemented`) lo hace el usuario.
4. **Idioma**: todo el código (tipos, métodos, variables, rutas, tablas, mensajes de excepción) en **inglés**. Los **comentarios** y la documentación, en **español**.
5. No hacer commits ni push salvo que el usuario lo pida.

## Stack
- .NET 10 / C# (última versión) · ASP.NET Core Minimal APIs + **Carter**
- **Clean Architecture + CQRS** con **MediatR 14** (requiere `MediatR:LicenseKey`) · FluentValidation 12 · Mapster
- **EF Core 10 + SQLite** · un solo `DispensarioDbContext`
- Autenticación con **JWT propio** HS256 (roles `Admin`, `Doctor`, `Receptionist`) y hash de contraseñas con **bcrypt** (`BCrypt.Net-Next`, factor 12)
- OpenAPI nativo + **Scalar** · tests con xUnit v3, Shouldly, NSubstitute y WebApplicationFactory

## Estructura
```
Dispensario.slnx  Directory.Build.props  Directory.Packages.props   # se crean en el spec 01
.claude/skills/  .agents/skills/       # skills SDD: spec, spec-impl
specs/                                 # specs generados por /spec (NN-slug.md) + .spec-config.yml
frontend/                              # se define más adelante
docs/                                  # plan/backlog, arquitectura y guías
src/
├─ BuildingBlocks/
│  ├─ BuildingBlocks/                  # código común: CQRS, behaviors, excepciones, paginación
│  └─ BuildingBlocks.Domain/           # abstracciones DDD puras: Entity, AggregateRoot, ValueObject, IDomainEvent, IAuditable, DomainException
└─ Services/Dispensario/
   ├─ Dispensario.Domain/              # aggregates, value objects, domain events (solo depende de BuildingBlocks.Domain)
   ├─ Dispensario.Application/         # commands/queries/handlers/validators por módulo
   ├─ Dispensario.Infrastructure/      # EF Core/SQLite, interceptores, JWT, seed
   └─ Dispensario.Api/                 # endpoints Carter, auth, OpenAPI, Program.cs
tests/                                 # todos los proyectos de tests, sin subcarpetas
├─ BuildingBlocks.UnitTests/           # tests de BuildingBlocks y BuildingBlocks.Domain (CustomExceptionHandler, Entity…)
├─ Dispensario.Domain.UnitTests/
├─ Dispensario.Application.UnitTests/
└─ Dispensario.Api.IntegrationTests/
```
Dentro de cada capa el código se agrupa **por módulo**: `Users/`, `Doctors/`, `Patients/`, `Appointments/`, `Consultations/`.

## Reglas de arquitectura (resumen)
- Dependencias: `Api → Application, Infrastructure` · `Infrastructure → Application` · `Application → Domain, BuildingBlocks` · `Domain → BuildingBlocks.Domain` · `BuildingBlocks → BuildingBlocks.Domain` · `BuildingBlocks.Domain →` solo `MediatR.Contracts`.
- Los módulos **solo se relacionan por ID** (`PatientId`, `DoctorId`…): nada de navegaciones de EF entre módulos, y un handler nunca modifica un aggregate de otro módulo. Entre módulos se reacciona con domain events.
- Para los commands, queries y handlers se usan `ICommand`, `IQuery`, `ICommandHandler` e `IQueryHandler` de `BuildingBlocks.CQRS`.
- Los errores se señalan con las excepciones de `BuildingBlocks.Exceptions` (NotFound, Conflict, BadRequest, Forbidden…) y con `DomainException` (`BuildingBlocks.Domain`) para las reglas de negocio. `CustomExceptionHandler` las convierte en ProblemDetails, así que no se devuelven códigos de error a mano desde los endpoints.
- Fechas en UTC con `TimeProvider`; nunca `DateTime.Now`.

## Detalles no obvios
- `ValidationBehavior` (BuildingBlocks) solo se aplica a `ICommand<T>`: las queries **no** pasan por FluentValidation.
- `CustomExceptionHandler` mapea `DomainException` → 400, `UnauthorizedException` → 401, `ForbiddenException` → 403 y `BadHttpRequestException` (JSON ilegible, enum desconocido) → su `StatusCode` (400), sin `ValidationErrors`; cualquier excepción no mapeada cae en 500.
- Los tests usan `xunit.v3.mtp-off` (xUnit v3 sin Microsoft.Testing.Platform) para ejecutarse con VSTest: `xunit.v3` 4.x trae MTP 2.x, que en el SDK de .NET 10 bloquea `dotnet test` con VSTest.
- MediatR 14 es de licencia comercial: sin `MediatR:LicenseKey` registra un aviso de licencia.
- SQLite no admite esquemas (los módulos se separan por nombre de tabla y carpeta de configuración) y no ordena ni compara bien `DateTimeOffset`: se usa `DateTime` UTC con el sufijo `Utc`.
- Los tests usan SQLite `:memory:`, nunca el proveedor EF InMemory.
- Los IDs tipados implementan `IStronglyTypedId` (con constructor `(Guid)`) y se convierten a Guid por convención: no se registran converters a mano. Todo `DateTime` leído de SQLite sale con `Kind = Utc`.
- Las propiedades de solo lectura de los VOs no se mapean por convención: cada `ComplexProperty` las declara una a una.
- Mapeo de VOs: los de un solo valor (`Email`, `PhoneNumber`) con `HasConversion` a una columna; los de varios campos con `ComplexProperty`, salvo que necesiten índice (`Document`), que van con `OwnsOne` en la misma tabla porque `ComplexProperty` no admite índices.
- Los domain events solo se despachan con `SaveChangesAsync`; `SaveChanges` síncrono con eventos pendientes lanza `InvalidOperationException`.
- `ICurrentUser` (`UserId?`, `Role?`, `DoctorId?`, `IsAuthenticated`) lo implementa `CurrentUser` con los claims del JWT; sin `HttpContext` (seed, procesos en segundo plano) todo es `null`.
- **`Jwt:Key` es obligatoria** (mínimo 32 caracteres, en user-secrets): sin ella la app no arranca (`ValidateOnStart`). `Seed:Admin:Password` es opcional: vacía, el seed del Admin solo avisa en el log.
- Esquema por migraciones (feature 17, `Infrastructure/Data/Migrations`, la primera `InitialCreate`): en Development, `InitialiseDatabaseAsync` hace `MigrateAsync`, el seed del Admin y el de los datos de ejemplo; fuera de Development no se migra al arrancar (`dotnet ef database update`). Cada cambio del modelo exige una migración nueva (`MigrationsTests.Model_HasNoPendingChanges` falla si falta). Una `dispensario.db` (carpeta del proyecto Api) creada antes con `EnsureCreated` hay que **borrarla una vez**. Los tests (`DispensarioTestDb` y la factory de integración) también crean el esquema con la migración.
- Datos de ejemplo (`SampleDataSeeder`, sección `Seed:SampleData`): solo en Development, `Enabled` true por defecto (la factory de integración lo apaga) y solo si no hay ningún doctor ni paciente. Siembra 3 doctores con horario (`MG-0001`, `PED-0002`, `CAR-0003`) y 5 pacientes; con `Seed:SampleData:Password`, también `recepcion@dispensario.local` y `doctor@dispensario.local` (vinculado a `MG-0001`), omitiendo los emails que ya existan. Sin contraseña, solo avisa en el log y no crea usuarios.
- Los enums viajan como texto en el JSON de la Api (`JsonStringEnumConverter` global).
- Todos los errores salen como `application/problem+json`, también los que vienen de excepciones (`CustomExceptionHandler` fija el content type).
- El login devuelve el mismo 401 `Invalid credentials.` para email inexistente, contraseña incorrecta y usuario inactivo. Un token sigue valiendo hasta que caduca aunque el usuario se desactive; solo `/api/auth/me` y las rutas `/api/users/me*` lo comprueban.
- `POST /api/auth/register` es anónimo y siempre crea un `Receptionist`; los `Admin` y `Doctor` solo los crea un Admin con `POST /api/users`. Nadie puede desactivarse a sí mismo (400) ni desactivar al último Admin activo (409), y el Admin cambia su propia contraseña por `/api/users/me/password`, no por el reseteo.
- `string.Contains` en SQLite se traduce a `instr`, que distingue mayúsculas: las búsquedas usan `EF.Functions.Like` con `LikePatterns` (`Application/Common`, escapa los comodines). Para un VO con `HasConversion` (`Email`) se escribe `(string)(object)u.Email`.
- Un usuario `Doctor` se vincula con su ficha con `POST /api/users/{id}/link-doctor` (handler de Users que lee `Doctors`): `DoctorId` sigue siendo opcional y es único en `Users`. El claim `doctor_id` solo aparece en los tokens emitidos tras el vínculo (hace falta un nuevo login). Desactivar un doctor no toca a su usuario.
- La duración de la cita es por doctor: de 10 a 120 minutos, múltiplo de 5, y 30 por defecto en el alta (`Doctor.DefaultAppointmentDurationMinutes`). En `PUT /api/doctors/{id}`, omitirla conserva la actual. La matrícula se guarda en mayúsculas y es única.
- `DoctorDto.UserId` sale de una subconsulta sobre `Users` en `DoctorProjections.ToDto`: los doctores no usan Mapster para proyectar.
- Horario semanal (`Doctor.Schedule`, VOs `ScheduleBlock` con `OwnsMany` en `DoctorScheduleBlocks`): `PUT /api/doctors/{id}/schedule` lo reemplaza entero (`[]` lo borra) y lo hacen el Admin o el propio doctor; un `Doctor` sobre otra ficha recibe 403 **antes** del 404. Horas `"HH:mm"` en hora local, minutos múltiplo de 5, sin cruzar la medianoche, sin solapes (contiguos sí) y cada bloque ≥ la duración de la cita; con un doctor inactivo, 400. `PUT /api/doctors/{id}` rechaza (400) una duración mayor que algún bloque.
- `Doctor.IsAvailableAt(localStart, localEnd)` recibe horas locales del dispensario y solo dice si el rango cabe en un bloque: no mira `IsActive`, alineación a slots ni citas (eso es del handler de agenda). La clave de `DoctorScheduleBlocks` es solo `Id`: SQLite no autoincrementa una clave compuesta.
- Pacientes (`/api/patients`): escriben `Admin` y `Receptionist` (política `Reception`), leen todos. El documento es editable (409 si choca) y el número distingue mayúsculas, como en Users. `GET /api/patients/by-document?type=&number=` es una ruta literal y también devuelve inactivos. El `PUT` reemplaza todo: un opcional `null` borra el valor.
- `bloodType` viaja como `"A+"`, `"AB-"`…: lo traduce `BloodTypeNotation` (Application), mientras el dominio y la BD usan el enum `BloodType` (`APositive`). En las proyecciones, `ToNotation` lo evalúa EF en cliente.
- `Patient.Create`/`Update` reciben `today` (`DateOnly` UTC del `TimeProvider`, lo calcula el handler) para rechazar un nacimiento futuro. `Address` es un `ComplexProperty` **opcional** (columnas `Address_*` nullable).
- Citas (`/api/appointments`): la Api recibe y devuelve horas **locales del dispensario sin offset** (`startLocal: "2026-11-03T09:30"`) y la BD guarda `StartUtc`/`EndUtc`. La zona es `Clinic:TimeZone` (IANA, `America/Guayaquil` por defecto) y se valida al arrancar; la conversión la hace `IClinicTimeZone` (Application). Un `startLocal` con `Z` u offset da 400 de validación (`Kind != Unspecified`).
- Agendar (`Reception`) exige, en orden: paciente y doctor existentes (404) y activos (400), cita futura, que quepa en un bloque del horario y que el inicio esté alineado **desde el inicio del bloque** en pasos de la duración (400), y sin cruces con citas `Scheduled`/`Confirmed` del doctor ni del paciente (409; contiguas valen). La duración es la del doctor.
- Un `Doctor` solo ve sus citas: sin vincular → 403; `doctorId` ajeno en el listado → 403 antes de leer la BD; detalle ajeno → 403 (tras el 404). `AppointmentDto` trae los nombres por subconsultas (sin Mapster) y las horas locales se calculan **en memoria** tras leer la página (`WithLocalTimes`). Sin domain events ni FKs entre módulos.
- Ciclo de vida (feature 13): `POST /api/appointments/{id}/confirm` y `PUT /api/appointments/{id}/schedule` (reprogramar) son de `Reception`; `POST …/cancel` (motivo obligatorio, `CancellationReason`) y `POST …/no-show` los hacen también el `Doctor` sobre sus citas (sin vincular → 403 antes de leer; ajena → 403 tras el 404). `Confirm`, `Cancel` y `Reschedule` solo antes del inicio; `MarkNoShow` solo desde el inicio; `Cancelled` y `NoShow` son finales; estado o momento inválidos → 400 (`DomainException`). Reprogramar usa la duración vigente del doctor, vuelve a `Scheduled` aunque estuviera `Confirmed`, rechaza la misma hora y no choca consigo misma. Horario, alineación y cruces viven en `AppointmentSlotRules` (`Application/Appointments/Common`), compartida por el alta y la reprogramación. `Complete()` (feature 16) va de `Scheduled`/`Confirmed` a `Completed` (final), sin `nowUtc` ni restricción de hora y sin `CompletedAtUtc`; hoy no lo llama ningún handler ni endpoint.
- Slots disponibles (feature 14): `GET /api/doctors/{id}/available-slots?date=` cuelga de `/api/doctors`, pero la query (`GetAvailableSlots`) y el endpoint viven en Appointments (tag `Appointments`). Cualquier autenticado ve cualquier doctor. Solo devuelve slots libres: generados por `AppointmentSlotRules.GetScheduleSlots` (misma alineación que el alta, sin el sobrante final del bloque), futuros y sin cruce con citas `Scheduled`/`Confirmed` **del doctor**; las del paciente no se miran (el alta da 409). `date` es fecha local: pasada → 400 (antes de leer la BD), doctor inactivo → 400, inexistente → 404, día sin horario → `slots: []`.
- Consultas (feature 15, `/api/consultations`): inician, editan y cierran solo los `Doctor` (política `Clinical`, que es **solo** `Doctor`), cada uno las suyas, y el doctor sale del token (`ICurrentUser.DoctorId`), nunca del cuerpo; leen el Admin y **cualquier** `Doctor` vinculado (`AdminOrDoctor`), y la recepción recibe 403 en todo. `Doctor` sin vincular → 403 antes de leer, también en lecturas; consulta ajena → 403 tras el 404. Con `appointmentId`, la cita debe ser del doctor (si no, 403), del paciente, `Scheduled`/`Confirmed` y de **hoy** en fecha local (400), y sin otra consulta abierta o cerrada (409): una consulta por cita, con índice único en `AppointmentId` (SQLite admite varios `NULL`, así que las consultas sin cita no chocan). `Closed` es final (`Update`/`Close` → 400) y `Close` exige diagnóstico. `Update` y `Close` no miran si el doctor o el paciente siguen activos. Iniciar o cerrar la consulta **no** cambia la cita: `ConsultationClosedDomainEvent(ConsultationId, AppointmentId?)` existe desde la feature 16 (parcial), pero `Close` no lo emite y no hay ningún `INotificationHandler`; emitirlo y el handler en `Appointments/EventHandlers` que llama a `Appointment.Complete` quedan para un spec posterior. La historia (`GET /api/patients/{id}/consultations`) vive en Consultations, incluye `Open` y `Closed` de todos los doctores, de la más reciente a la más antigua, y lee pacientes inactivos.

Detalle completo en:
- `docs/ARCHITECTURE.md`: especificación de la arquitectura
- `docs/PLAN.md`: backlog de features, flujo SDD y preguntas abiertas
- `docs/guides/CONVENTIONS.md`: nombres, estilo y comentarios
- `docs/guides/DOMAIN.md`: modelo de dominio y reglas DDD
- `docs/guides/APPLICATION.md`: CQRS, validación y mapeo
- `docs/guides/INFRASTRUCTURE.md`: EF Core/SQLite, migraciones, auth y seed
- `docs/guides/API.md`: endpoints Carter, rutas, políticas y OpenAPI
- `docs/guides/TESTING.md`: estrategia de tests

## Comandos
```bash
dotnet build Dispensario.slnx
dotnet test Dispensario.slnx
dotnet test tests/Dispensario.Domain.UnitTests --filter "FullyQualifiedName~AppointmentTests"   # una clase o un test
dotnet run --project src/Services/Dispensario/Dispensario.Api            # Scalar en /scalar, health en /health

# Migraciones (requiere la herramienta dotnet-ef 10.x)
dotnet ef migrations add <Name> -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api -o Data/Migrations
dotnet ef database update      -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api

# Secretos locales (nunca en appsettings versionado)
dotnet user-secrets set "Jwt:Key" "<min-32-chars>" --project src/Services/Dispensario/Dispensario.Api
dotnet user-secrets set "Seed:Admin:Password" "<password>" --project src/Services/Dispensario/Dispensario.Api
dotnet user-secrets set "Seed:SampleData:Password" "<password>" --project src/Services/Dispensario/Dispensario.Api   # usuarios de ejemplo (opcional)
dotnet user-secrets set "MediatR:LicenseKey" "<key>" --project src/Services/Dispensario/Dispensario.Api
```

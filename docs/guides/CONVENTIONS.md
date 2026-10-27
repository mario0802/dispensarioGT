# Convenciones de código

## Idioma
- **Inglés**: nombres de proyectos, namespaces, tipos, miembros, variables, rutas HTTP, tablas y columnas, mensajes de excepción y de validación.
- **Español**: comentarios (`//` y `/// <summary>`), documentación en `docs/` y mensajes de commit (opcional).

```csharp
/// <summary>
/// Agenda una cita validando el horario del doctor y que no haya cruces.
/// </summary>
public sealed class ScheduleAppointmentHandler(...) : ICommandHandler<ScheduleAppointmentCommand, ScheduleAppointmentResult>
{
    // Se excluyen las citas canceladas o no asistidas del cálculo de cruces
    ...
}
```

## Comentarios
- Explican **por qué**, no qué. No comentar lo obvio.
- Se pone `<summary>` en los aggregates, en los métodos de negocio públicos y en los handlers.

## Nombres
| Elemento | Convención | Ejemplo |
|---|---|---|
| Namespace | igual a la carpeta | `Dispensario.Application.Patients.Commands.CreatePatient` |
| Aggregate / entidad | sustantivo singular | `Patient`, `Appointment` |
| ID tipado | `<Aggregate>Id` | `PatientId` |
| Domain event | `<Aggregate><Verbo pasado>DomainEvent` | `AppointmentCancelledDomainEvent` |
| Command | `<Verbo><Sustantivo>Command` | `CreatePatientCommand` |
| Query | `Get<Sustantivo>Query` | `GetPatientByIdQuery` |
| Resultado | `<Feature>Result` | `CreatePatientResult(Guid Id)` |
| Handler | `<Feature>Handler` | `CreatePatientHandler` |
| Validator | `<Feature>CommandValidator` | `CreatePatientCommandValidator` |
| DTO | `<Sustantivo>Dto` | `PatientDto` |
| Endpoint | `<Feature>Endpoint` | `CreatePatientEndpoint` |
| Request/Response HTTP | `<Feature>Request` / `<Feature>Response` | `CreatePatientRequest` |
| Configuración EF | `<Entidad>Configuration` | `PatientConfiguration` |
| Tabla | plural PascalCase | `Patients`, `DoctorScheduleBlocks` |

## Estilo C#
- Namespaces *file-scoped*; un tipo público por archivo, salvo el trío command + result + validator, que comparte archivo.
- Constructores primarios para la DI en handlers y servicios.
- `sealed` por defecto en clases concretas (handlers, endpoints, configuraciones).
- `record` para commands, queries, resultados, DTOs, requests y responses.
- Nada de `async void`. Todo método asíncrono recibe y propaga `CancellationToken`.
- Nada de `DateTime.Now` ni `DateTime.UtcNow` directos: se usa `TimeProvider`.
- Nullable activado: no suprimir warnings con `!` sin justificarlo en un comentario.
- Versiones de paquetes **solo** en `Directory.Packages.props`.

## Archivos por feature
```
Application/Patients/Commands/CreatePatient/
  CreatePatientCommand.cs    // command + result + validator
  CreatePatientHandler.cs
Api/Endpoints/Patients/
  CreatePatientEndpoint.cs   // request + response + ICarterModule
```

## Git (cuando se use)
- Una rama por spec, creada por `/spec-impl`: `spec-NN-slug` (p. ej. `spec-10-patients-crud`).
- Los commits los hace el usuario. Deben ser pequeños y referenciar el spec: `spec 10: add Patient aggregate`.

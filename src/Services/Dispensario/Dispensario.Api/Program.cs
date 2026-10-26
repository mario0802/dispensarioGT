using Carter;
using Dispensario.Api.Extensions;
using Dispensario.Application;
using Dispensario.Infrastructure;
using Dispensario.Infrastructure.Data.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
// Los 404/405 sin cuerpo también salen como ProblemDetails
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar

    // EnsureCreated + seed del Admin; la fila 17 lo cambia a MigrateAsync
    await app.InitialiseDatabaseAsync();
}

app.MapCarter();
app.MapHealthChecks("/health");

app.Run();

// Necesario para WebApplicationFactory en los tests de integración
public partial class Program;

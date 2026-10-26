using BuildingBlocks.Exceptions.Handler;
using Carter;
using Dispensario.Api.Extensions.OpenApi;
using Dispensario.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Api.Extensions;

public static class ApiServiceExtensions
{
    /// <summary>
    /// Registra los servicios propios del host HTTP: Carter, OpenAPI, ProblemDetails, manejo de excepciones y health checks.
    /// </summary>
    // La autenticación JWT y las políticas se añaden aquí en la fila 07
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCarter();
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddProblemDetails();
        services.AddExceptionHandler<CustomExceptionHandler>();

        // En SQLite, CanConnectAsync (la prueba por defecto) solo mira si existe el archivo;
        // abrir la conexión con una consulta comprueba de verdad que la base de datos es accesible
        services.AddHealthChecks().AddDbContextCheck<DispensarioDbContext>(
            customTestQuery: async (dbContext, cancellationToken) =>
            {
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                return true;
            });

        return services;
    }
}

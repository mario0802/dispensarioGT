using System.Text;
using System.Text.Json.Serialization;
using BuildingBlocks.Exceptions.Handler;
using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Api.Extensions.OpenApi;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dispensario.Api.Extensions;

public static class ApiServiceExtensions
{
    /// <summary>
    /// Registra los servicios propios del host HTTP: Carter, OpenAPI, ProblemDetails, manejo de excepciones,
    /// autenticación JWT, políticas de autorización y health checks.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCarter();
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        // Los enums viajan como texto ("Admin"), igual que se guardan en SQLite
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails();
        services.AddExceptionHandler<CustomExceptionHandler>();

        services.AddJwtAuthentication();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(nameof(Role.Admin)))
            .AddPolicy(Policies.Reception, policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Receptionist)))
            .AddPolicy(Policies.Clinical, policy => policy.RequireRole(nameof(Role.Doctor)));

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

    // La validación usa las mismas JwtOptions que el emisor, así que una clave sobrescrita (tests) vale para ambos
    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Conserva los nombres de los claims (sub, role) en lugar de mapearlos a los de .NET
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    // El token caduca exactamente en el expiresAtUtc que recibe el cliente
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = CurrentUser.RoleClaimType,
                };
            });
    }
}

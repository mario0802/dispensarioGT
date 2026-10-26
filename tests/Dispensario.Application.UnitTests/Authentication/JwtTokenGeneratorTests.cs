using System.Text;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dispensario.Application.UnitTests.Authentication;

public sealed class JwtTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "Dispensario",
        Audience = "Dispensario.Clients",
        Key = "unit-test-signing-key-with-at-least-32-chars",
        ExpirationMinutes = 45,
    };

    private readonly JwtTokenGenerator _generator =
        new(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

    [Fact]
    public void Generate_Always_ExpiresAfterConfiguredMinutesInUtc()
    {
        var (token, expiresAtUtc) = _generator.Generate(CreateUser(Role.Admin));

        expiresAtUtc.ShouldBe(Now.UtcDateTime.AddMinutes(45));
        expiresAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        var jwt = new JsonWebToken(token);
        jwt.ValidTo.ShouldBe(expiresAtUtc);
        jwt.IssuedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public void Generate_Always_IncludesUserClaims()
    {
        var user = CreateUser(Role.Receptionist);

        var jwt = new JsonWebToken(_generator.Generate(user).Token);

        jwt.Subject.ShouldBe(user.Id.Value.ToString());
        jwt.GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe("ana.perez@dispensario.local");
        jwt.GetClaim(JwtRegisteredClaimNames.Name).Value.ShouldBe("Ana Pérez");
        jwt.GetClaim(CurrentUser.RoleClaimType).Value.ShouldBe("Receptionist");
        Guid.TryParse(jwt.Id, out _).ShouldBeTrue();
        jwt.Issuer.ShouldBe(Options.Issuer);
        jwt.Audiences.ShouldBe([Options.Audience]);
    }

    [Fact]
    public void Generate_WithoutDoctor_OmitsDoctorIdClaim()
    {
        var jwt = new JsonWebToken(_generator.Generate(CreateUser(Role.Doctor)).Token);

        jwt.TryGetClaim(CurrentUser.DoctorIdClaimType, out _).ShouldBeFalse();
    }

    [Fact]
    public void Generate_WithLinkedDoctor_IncludesDoctorIdClaim()
    {
        var user = CreateUser(Role.Doctor);
        var doctorId = DoctorId.New();
        user.LinkDoctor(doctorId);

        var jwt = new JsonWebToken(_generator.Generate(user).Token);

        jwt.GetClaim(CurrentUser.DoctorIdClaimType).Value.ShouldBe(doctorId.Value.ToString());
    }

    [Fact]
    public void Generate_TwoTokens_HaveDifferentJti()
    {
        var user = CreateUser(Role.Admin);

        var first = new JsonWebToken(_generator.Generate(user).Token);
        var second = new JsonWebToken(_generator.Generate(user).Token);

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task Generate_Always_ProducesHs256TokenValidWithSameKey()
    {
        var (token, _) = _generator.Generate(CreateUser(Role.Admin));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Key)),
            // El token se emite con un reloj falso; la caducidad ya se comprueba en otro test
            ValidateLifetime = false,
        });

        result.IsValid.ShouldBeTrue();
        new JsonWebToken(token).Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
    }

    private static User CreateUser(Role role) => User.Create(
        Email.Create("ana.perez@dispensario.local"),
        "hash",
        FullName.Create("Ana", "Pérez"),
        Document.Create(DocumentType.NationalId, "1712345678"),
        phone: null,
        role);
}

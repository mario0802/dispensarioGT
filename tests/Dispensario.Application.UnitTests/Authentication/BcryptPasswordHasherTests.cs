using Dispensario.Infrastructure.Authentication;

namespace Dispensario.Application.UnitTests.Authentication;

public sealed class BcryptPasswordHasherTests
{
    private const string Password = "S3cret-Passw0rd!";

    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_Always_ReturnsBcryptHashWithWorkFactor12()
    {
        var hash = _hasher.Hash(Password);

        hash.ShouldStartWith("$2");
        hash.ShouldContain("$12$");
        hash.Length.ShouldBe(60);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ReturnsDifferentHashes()
    {
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify(hash, Password).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify(hash, "wrong-password").ShouldBeFalse();
    }
}

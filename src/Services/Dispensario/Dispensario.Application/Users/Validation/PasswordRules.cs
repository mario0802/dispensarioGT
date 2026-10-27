using FluentValidation;

namespace Dispensario.Application.Users.Validation;

/// <summary>
/// Política de contraseñas compartida por el registro, el cambio y el reseteo.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    // bcrypt solo usa los primeros 72 bytes
    public const int MaxLength = 72;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .Length(MinLength, MaxLength)
            .Matches("[A-Za-z]").WithMessage("'{PropertyName}' must contain at least one letter.")
            .Matches("[0-9]").WithMessage("'{PropertyName}' must contain at least one digit.");
}

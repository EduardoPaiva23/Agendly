using FluentValidation;

namespace BeautyAgenda.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserDto(string Id, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public interface IAuthService
{
    /// <summary>Returns null for invalid credentials, locked-out or inactive accounts (same answer for all, to avoid user enumeration).</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<CurrentUserDto?> GetUserAsync(string userId, CancellationToken ct);
}

using System.Security.Claims;
using System.Text;
using BeautyAgenda.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BeautyAgenda.Infrastructure.Identity;

public static class AppClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = "name";
    public const string Role = "role";
}

public sealed class AuthService(
    UserManager<ApplicationUser> users,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            return null;

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        var dto = await ToDtoAsync(user);
        var expires = timeProvider.GetUtcNow().AddMinutes(_jwt.ExpiresMinutes);
        return new LoginResponse(CreateToken(dto, expires), expires, dto);
    }

    public async Task<CurrentUserDto?> GetUserAsync(string userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId);
        return user is { IsActive: true } ? await ToDtoAsync(user) : null;
    }

    private async Task<CurrentUserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, roles.Order().ToList());
    }

    private string CreateToken(CurrentUserDto user, DateTimeOffset expires)
    {
        var claims = new List<Claim>
        {
            new(AppClaims.Subject, user.Id),
            new(AppClaims.Email, user.Email),
            new(AppClaims.Name, user.FullName),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(AppClaims.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }
}

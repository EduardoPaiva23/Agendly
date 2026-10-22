using BeautyAgenda.Api.Infrastructure;
using BeautyAgenda.Application.Auth;
using BeautyAgenda.Infrastructure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, IValidator<LoginRequest> validator) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.LoginPolicy)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var result = await auth.LoginAsync(request, ct);
        if (result is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        return Ok(result);
    }

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirst(AppClaims.Subject)?.Value;
        var user = userId is null ? null : await auth.GetUserAsync(userId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}

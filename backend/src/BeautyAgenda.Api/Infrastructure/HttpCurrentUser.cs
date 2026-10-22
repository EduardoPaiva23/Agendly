using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Infrastructure.Identity;

namespace BeautyAgenda.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirst(AppClaims.Subject)?.Value;
}

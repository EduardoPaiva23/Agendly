using System.Text;
using System.Threading.RateLimiting;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace BeautyAgenda.Api.Infrastructure;

public sealed class RateLimitOptions
{
    public const string Section = "RateLimiting";
    public const string PublicWritePolicy = "public-write";
    public const string LoginPolicy = "login";

    public int PublicWritePerMinute { get; set; } = 20;
    public int LoginPerMinute { get; set; } = 10;
}

public static class ApiSetup
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
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
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    // A deactivated account loses access immediately, even with an unexpired token.
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirst(AppClaims.Subject)?.Value;
                        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                        var user = userId is null ? null : await users.FindByIdAsync(userId);
                        if (user is not { IsActive: true })
                            context.Fail("User is inactive or no longer exists.");
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.StaffOrAdmin, p => p.RequireRole(Roles.Admin, Roles.Staff))
            // Secure by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitOptions.PublicWritePolicy, context => FixedWindowPerIp(context, limits.PublicWritePerMinute));
            options.AddPolicy(RateLimitOptions.LoginPolicy, context => FixedWindowPerIp(context, limits.LoginPerMinute));
        });
        return services;
    }

    private static RateLimitPartition<string> FixedWindowPerIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "BeautyAgenda API",
                    Version = "v1",
                    Description = "Agendamento de salão de beleza. Endpoints /api/public/* são anônimos; os demais exigem Bearer token.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token obtido em POST /api/auth/login.",
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                });
                return Task.CompletedTask;
            });
        });
        return services;
    }
}

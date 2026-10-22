namespace BeautyAgenda.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "BeautyAgenda";
    public string Audience { get; set; } = "BeautyAgenda.Admin";
    /// <summary>HMAC-SHA256 key, at least 32 characters. Never commit it: use user-secrets or environment variables.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

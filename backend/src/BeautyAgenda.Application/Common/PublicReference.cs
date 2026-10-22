using System.Buffers.Text;
using System.Security.Cryptography;

namespace BeautyAgenda.Application.Common;

/// <summary>128-bit random, URL-safe booking reference (22 chars). Not derivable from database ids.</summary>
public static class PublicReference
{
    public const int Length = 22;

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    public static bool IsWellFormed(string? value) =>
        value is { Length: Length } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

namespace BeautyAgenda.Application.Common;

public static class PhoneNumber
{
    public const int MinDigits = 10;
    public const int MaxDigits = 13;

    public static string Normalize(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string? value)
    {
        var digits = Normalize(value);
        return digits.Length is >= MinDigits and <= MaxDigits;
    }
}

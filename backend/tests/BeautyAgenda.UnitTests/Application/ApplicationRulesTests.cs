using BeautyAgenda.Application.BusinessHours;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BeautyAgenda.UnitTests.Application;

public class SalonClockTests
{
    private static SalonClock Clock(DateTimeOffset now) =>
        new(new FakeTimeProvider(now), Options.Create(new SalonOptions { TimeZone = "America/Sao_Paulo" }));

    [Fact]
    public void Local_business_time_is_converted_to_utc()
    {
        var clock = Clock(DateTimeOffset.UtcNow);
        var utc = clock.ToUtc(new DateOnly(2030, 3, 4), new TimeOnly(9, 0));

        Assert.Equal(new DateTime(2030, 3, 4, 12, 0, 0, DateTimeKind.Utc), utc); // São Paulo = UTC-3
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void Today_uses_salon_time_zone_not_utc()
    {
        // 01:30 UTC on Mar 5 is still 22:30 on Mar 4 in São Paulo.
        var clock = Clock(new DateTimeOffset(2030, 3, 5, 1, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2030, 3, 4), clock.Today);
    }

    [Fact]
    public void ToLocal_returns_offset_of_salon()
    {
        var clock = Clock(DateTimeOffset.UtcNow);
        var local = clock.ToLocal(new DateTime(2030, 3, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromHours(-3), local.Offset);
        Assert.Equal(9, local.Hour);
    }

    [Fact]
    public void DayRange_covers_the_local_day()
    {
        var range = Clock(DateTimeOffset.UtcNow).DayRange(new DateOnly(2030, 3, 4));
        Assert.Equal(new DateTime(2030, 3, 4, 3, 0, 0, DateTimeKind.Utc), range.Start);
        Assert.Equal(TimeSpan.FromHours(24), range.Duration);
    }
}

public class BusinessHoursValidatorTests
{
    private readonly UpdateBusinessHoursRequestValidator _validator = new();

    private bool IsValid(params BusinessHourDto[] items) => _validator.Validate(new UpdateBusinessHoursRequest(items)).IsValid;

    [Fact]
    public void Accepts_split_day_and_empty_week()
    {
        Assert.True(IsValid(
            new(DayOfWeek.Monday, "09:00", "12:00"),
            new(DayOfWeek.Monday, "12:00", "18:00"),
            new(DayOfWeek.Tuesday, "09:00", "18:00")));
        Assert.True(IsValid());
    }

    [Fact]
    public void Rejects_overlapping_intervals_on_same_day()
    {
        Assert.False(IsValid(new(DayOfWeek.Monday, "09:00", "12:00"), new(DayOfWeek.Monday, "11:00", "15:00")));
    }

    [Theory]
    [InlineData("10:00", "09:00")]
    [InlineData("10:00", "10:00")]
    [InlineData("9h", "12:00")]
    [InlineData("09:00", "24:00")]
    public void Rejects_invalid_times(string start, string end)
    {
        Assert.False(IsValid(new BusinessHourDto(DayOfWeek.Friday, start, end)));
    }
}

public class ValidationLocalizationTests
{
    [Fact]
    public void Built_in_messages_are_in_portuguese_with_portuguese_field_names_and_original_keys()
    {
        ValidationLocalization.Configure();
        var result = new BeautyAgenda.Application.Customers.CustomerRequestValidator()
            .Validate(new BeautyAgenda.Application.Customers.CustomerRequest("", "11987654321", "não-é-email"));

        var name = Assert.Single(result.Errors, e => e.PropertyName == "Name");
        Assert.Equal("'Nome' deve ser informado.", name.ErrorMessage);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email" && e.ErrorMessage.Contains("e-mail", StringComparison.OrdinalIgnoreCase));
    }
}

public class SmallRulesTests
{
    [Theory]
    [InlineData("(11) 98765-4321", "11987654321", true)]
    [InlineData("+55 11 98765-4321", "5511987654321", true)]
    [InlineData("98765-4321", "987654321", false)]
    public void Phone_numbers_are_normalized_to_digits(string input, string digits, bool valid)
    {
        Assert.Equal(digits, PhoneNumber.Normalize(input));
        Assert.Equal(valid, PhoneNumber.IsValid(input));
    }

    [Fact]
    public void Public_references_are_random_url_safe_and_well_formed()
    {
        var refs = Enumerable.Range(0, 1000).Select(_ => PublicReference.New()).ToList();

        Assert.Equal(1000, refs.Distinct().Count());
        Assert.All(refs, r => Assert.True(PublicReference.IsWellFormed(r), r));
        Assert.False(PublicReference.IsWellFormed("1"));
        Assert.False(PublicReference.IsWellFormed("../../etc/passwd/aaaaaaa"));
    }
}

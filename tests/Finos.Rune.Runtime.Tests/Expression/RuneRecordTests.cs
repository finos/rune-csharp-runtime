using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>Constructing the DSL's zonedDateTime record from its parts.</summary>
public class RuneRecordTests
{
    [Fact]
    public void ZonedDateTime_WhenTheZoneIsZ_BuildsAUtcInstant()
    {
        DateTimeOffset? result = RuneRecord.ZonedDateTime(
            new DateOnly(2024, 3, 20), new TimeOnly(11, 30), "Z");

        Assert.Equal(new DateTimeOffset(2024, 3, 20, 11, 30, 0, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData("+01:00", 1, 0)]
    [InlineData("-05:00", -5, 0)]
    [InlineData("+0530", 5, 30)]
    [InlineData("-0330", -3, -30)]
    [InlineData("+02", 2, 0)]
    public void ZonedDateTime_AcrossTheFixedOffsetSpellings_HonoursTheOffset(string timezone, int hours, int minutes)
    {
        DateTimeOffset? result = RuneRecord.ZonedDateTime(
            new DateOnly(2024, 3, 20), new TimeOnly(11, 30), timezone);

        Assert.Equal(new TimeSpan(hours, minutes, 0), result!.Value.Offset);
        Assert.Equal(new TimeOnly(11, 30), TimeOnly.FromDateTime(result.Value.DateTime));
    }

    [Fact]
    public void ZonedDateTime_WithNeitherATimeNorAZone_DefaultsToMidnightUtc()
    {
        DateTimeOffset? result = RuneRecord.ZonedDateTime(new DateOnly(2024, 3, 20), null, null);

        Assert.Equal(new DateTimeOffset(2024, 3, 20, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ZonedDateTime_ForAnAbsentDate_IsAbsent() =>
        Assert.Null(RuneRecord.ZonedDateTime(null, new TimeOnly(11, 30), "Z"));

    [Fact]
    public void ZonedDateTime_ForANamedZone_ThrowsRatherThanResolvingIt()
    {
        var thrown = Assert.Throws<NotSupportedException>(() =>
            RuneRecord.ZonedDateTime(new DateOnly(2024, 3, 20), new TimeOnly(11, 30), "Europe/London"));

        Assert.Contains("not a fixed offset", thrown.Message);
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("+123")]
    [InlineData("+ab:cd")]
    public void ZonedDateTime_AcrossMalformedOffsetSpellings_Throws(string timezone) =>
        Assert.Throws<FormatException>(() =>
            RuneRecord.ZonedDateTime(new DateOnly(2024, 3, 20), new TimeOnly(11, 30), timezone));
}

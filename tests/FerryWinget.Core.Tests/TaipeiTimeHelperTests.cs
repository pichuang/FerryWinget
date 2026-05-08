namespace FerryWinget.Core.Tests;

using FluentAssertions;
using FerryWinget.Core.Helpers;

public class TaipeiTimeHelperTests
{
    [Fact]
    public void Now_ReturnsTaipeiTimezone()
    {
        var now = TaipeiTimeHelper.Now;
        now.Offset.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Convert_UtcToTaipei_AddsEightHours()
    {
        var utc = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);
        var taipei = TaipeiTimeHelper.Convert(utc);
        taipei.Hour.Should().Be(8);
        taipei.Offset.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void FormatTimestamp_IncludesTimezone()
    {
        var time = new DateTimeOffset(2026, 5, 9, 8, 30, 0, TimeSpan.FromHours(8));
        var formatted = TaipeiTimeHelper.FormatTimestamp(time);
        formatted.Should().Contain("2026-05-09 08:30:00");
        formatted.Should().Contain("+08:00");
    }
}

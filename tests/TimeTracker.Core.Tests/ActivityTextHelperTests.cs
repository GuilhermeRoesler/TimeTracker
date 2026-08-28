using Microsoft.Extensions.Logging.Abstractions;
using TimeTracker.Core;
using TimeTracker.Core.Services;

namespace TimeTracker.Core.Tests;

public class ActivityTextHelperTests
{
    [Theory]
    [InlineData(double.NaN, "0m")]
    [InlineData(-10, "0m")]
    [InlineData(0, "0m")]
    [InlineData(300, "5m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(3661, "1h 1m")]
    public void FormatDurationClean_returns_expected(double seconds, string expected)
        => Assert.Equal(expected, ActivityTextHelper.FormatDurationClean(seconds));

    [Fact]
    public void CleanWindowTitle_removes_browser_suffixes()
        => Assert.Equal(
            "GitHub",
            ActivityTextHelper.CleanWindowTitle("GitHub - Google Chrome"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanWindowTitle_empty_returns_sem_titulo(string? title)
        => Assert.Equal("Sem Título", ActivityTextHelper.CleanWindowTitle(title));
}

public class AppCategoriesTests
{
    [Theory]
    [InlineData("Trabalho", "Trabalho")]
    [InlineData(null, "Sem Categoria")]
    [InlineData("Invalida", "Sem Categoria")]
    public void Normalize_returns_expected(string? input, string expected)
        => Assert.Equal(expected, AppCategories.Normalize(input));
}

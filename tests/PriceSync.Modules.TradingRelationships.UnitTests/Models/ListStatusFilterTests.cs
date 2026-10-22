using FluentAssertions;
using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.UnitTests.Models;

public sealed class ListStatusFilterTests
{
    [Theory]
    [InlineData("active", ListStatusFilter.Active)]
    [InlineData("Active", ListStatusFilter.Active)]
    [InlineData("ACTIVE", ListStatusFilter.Active)]
    [InlineData("ended", ListStatusFilter.Ended)]
    [InlineData("Ended", ListStatusFilter.Ended)]
    [InlineData("ENDED", ListStatusFilter.Ended)]
    [InlineData("all", ListStatusFilter.All)]
    [InlineData("All", ListStatusFilter.All)]
    [InlineData("ALL", ListStatusFilter.All)]
    internal void ListStatusFilter_ShouldConvertCaseInsensitive(string input, ListStatusFilter expected)
    {
        // Act
        var actual = Enum.TryParse<ListStatusFilter>(input, ignoreCase: true, out var result) ? result : throw new ArgumentException("Invalid input", nameof(input));

        // Assert
        actual.Should().Be(expected);
    }
}

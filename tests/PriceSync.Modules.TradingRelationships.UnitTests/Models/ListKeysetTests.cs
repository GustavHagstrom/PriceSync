using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Models;

namespace PriceSync.Modules.TradingRelationships.UnitTests.Models;

public sealed class ListKeysetTests
{
    [Fact]
    public void FromCursor_ShouldReturnListKeyset_WhenGivenValidCursor()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var id = TradingRelationshipId.Create();
        var keyset = new ListKeyset(createdAt, id);
        var cursor = keyset.Cursor;

        // Act
        var result = ListKeyset.FromCursor(cursor);

        // Assert
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(id, result.Id);
    }
    [Fact]
    public void FromCursor_ShouldThrowArgumentException_WhenGivenInvalidCursor()
    {
        // Arrange
        var invalidCursor = "invalid_cursor";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListKeyset.FromCursor(invalidCursor));
    }


}

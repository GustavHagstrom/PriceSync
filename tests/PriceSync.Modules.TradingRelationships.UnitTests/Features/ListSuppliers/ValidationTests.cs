using FluentAssertions;
using PriceSync.Modules.TradingRelationships.Features.ListSupplier;
using System;
using System.Collections.Generic;
using System.Text;

namespace PriceSync.Modules.TradingRelationships.UnitTests.Features.ListSuppliers;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public void Validation_ShouldNotBeValid_WhenInputIsNumeric(string input)
    {
        // Arrange
        var validator = new ListSupplierValidator();

        //act   
        var result = validator.Validate(input, null, out var query);

        // Assert
        result.HasErrors.Should().BeTrue();
    }
}

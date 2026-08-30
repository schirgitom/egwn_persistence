using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.Validation;
using FluentAssertions;
using FluentValidation;

namespace EGWNPersistence.Tests.Application;

public sealed class ValidationTests
{
    private readonly IngestMeasurementsCommandValidator _validator = new();

    private static MeasurementEntry ValidEntry(DateTimeOffset? ts = null) =>
        new(ts ?? DateTimeOffset.UtcNow, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA");

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new IngestMeasurementsCommand("AT001", [ValidEntry()]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyMeterPointNumber_Fails(string? mpn)
    {
        var command = new IngestMeasurementsCommand(mpn!, [ValidEntry()]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MeterPointNumber");
    }

    [Fact]
    public void Validate_EmptyMeasurementsList_Fails()
    {
        var command = new IngestMeasurementsCommand("AT001", []);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Measurements");
    }

    [Fact]
    public void Validate_NegativeTotalConsumption_Fails()
    {
        var entry = new MeasurementEntry(DateTimeOffset.UtcNow, -1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA");
        var command = new IngestMeasurementsCommand("AT001", [entry]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("TotalConsumption"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_InvalidGridSharePercent_Fails(double percent)
    {
        var entry = new MeasurementEntry(DateTimeOffset.UtcNow, 1.0m, 0.5m, 0.5m, (decimal)percent, 0.5m, "EDA");
        var command = new IngestMeasurementsCommand("AT001", [entry]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("GridSharePercent"));
    }

    [Fact]
    public void Validate_EmptySource_Fails()
    {
        var entry = new MeasurementEntry(DateTimeOffset.UtcNow, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "");
        var command = new IngestMeasurementsCommand("AT001", [entry]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Source"));
    }

    [Fact]
    public void Validate_DefaultTimestamp_Fails()
    {
        var entry = ValidEntry(default(DateTimeOffset));
        var command = new IngestMeasurementsCommand("AT001", [entry]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Timestamp"));
    }

    [Fact]
    public void Validate_MeterPointNumberExceedsMaxLength_Fails()
    {
        var longNumber = new string('A', 101);
        var command = new IngestMeasurementsCommand(longNumber, [ValidEntry()]);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MeterPointNumber");
    }
}

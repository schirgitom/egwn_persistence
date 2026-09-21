using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.Validation;
using FluentAssertions;

namespace EGWNPersistence.Tests.Application;

public sealed class IngestKpisValidatorTests
{
    private readonly IngestKpisCommandValidator _validator = new();

    private static KpiEntry Entry(DateTimeOffset ts) => new(ts, 0.5m, 0.6m, 1m, 2m, 3m);

    [Fact]
    public void Valid_Command_Passes()
    {
        var command = new IngestKpisCommand("c1", new[] { Entry(DateTimeOffset.UtcNow) });
        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyCommunityId_Fails()
    {
        var command = new IngestKpisCommand("", new[] { Entry(DateTimeOffset.UtcNow) });
        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void NoEntries_Fails()
    {
        var command = new IngestKpisCommand("c1", Array.Empty<KpiEntry>());
        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DuplicateTimestamps_Fails()
    {
        var ts = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var command = new IngestKpisCommand("c1", new[] { Entry(ts), Entry(ts) });
        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DefaultTimestamp_Fails()
    {
        var command = new IngestKpisCommand("c1", new[] { Entry(default) });
        _validator.Validate(command).IsValid.Should().BeFalse();
    }
}

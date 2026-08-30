using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using FluentAssertions;

namespace EGWNPersistence.Tests.Domain;

public sealed class MeterPointTests
{
    [Fact]
    public void Create_WithValidNumber_SetsAllProperties()
    {
        var mp = MeterPoint.Create("AT001234567890");

        mp.Id.Should().NotBe(Guid.Empty);
        mp.MeterPointNumber.Should().Be("AT001234567890");
        mp.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        mp.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidNumber_ThrowsArgumentException(string? number)
    {
        var act = () => MeterPoint.Create(number!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var mp = MeterPoint.Create("  AT001  ");
        mp.MeterPointNumber.Should().Be("AT001");
    }

    [Fact]
    public void Reconstitute_RestoresAllProperties()
    {
        var id = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-10);
        var updated = DateTimeOffset.UtcNow.AddDays(-5);

        var mp = MeterPoint.Reconstitute(id, "AT002", created, updated);

        mp.Id.Should().Be(id);
        mp.MeterPointNumber.Should().Be("AT002");
        mp.CreatedAt.Should().Be(created);
        mp.UpdatedAt.Should().Be(updated);
    }
}

public sealed class MeterMeasurementTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var mpId = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);

        var m = MeterMeasurement.Create(mpId, ts, 1.234m, 0.5m, 0.734m, 0.4053m, 0.5947m, "EDA");

        m.Id.Should().NotBe(Guid.Empty);
        m.MeterPointId.Should().Be(mpId);
        m.Timestamp.Should().Be(ts.ToUniversalTime());
        m.TotalConsumption.Should().Be(1.234m);
        m.GridShare.Should().Be(0.5m);
        m.CommunityShare.Should().Be(0.734m);
        m.GridSharePercent.Should().Be(0.4053m);
        m.CommunitySharePercent.Should().Be(0.5947m);
        m.Source.Should().Be("EDA");
        m.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        m.IngestedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ConvertsTimestampToUtc()
    {
        var mpId = Guid.NewGuid();
        var localTs = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.FromHours(2)); // CET+2

        var m = MeterMeasurement.Create(mpId, localTs, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA");

        m.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        m.Timestamp.Hour.Should().Be(8); // 10:00+02:00 → 08:00 UTC
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptySource_Throws(string? source)
    {
        var act = () => MeterMeasurement.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, 1m, 0.5m, 0.5m, 0.5m, 0.5m, source!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsSource()
    {
        var m = MeterMeasurement.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, 1m, 0.5m, 0.5m, 0.5m, 0.5m, "  EDA  ");
        m.Source.Should().Be("EDA");
    }
}

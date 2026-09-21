using EGWNPersistence.Domain.Entities;
using FluentAssertions;

namespace EGWNPersistence.Tests.Domain;

public sealed class KpiMeasurementTests
{
    [Fact]
    public void Create_WithValidInput_TrimsCommunityIdAndSetsUtcTimestamp()
    {
        var kpi = KpiMeasurement.Create(
            "  community-1  ",
            new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.FromHours(2)),
            0.85m, 0.72m, 12.3m, 4.5m, 8.9m);

        kpi.CommunityId.Should().Be("community-1");
        kpi.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        kpi.Autarky.Should().Be(0.85m);
        kpi.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        kpi.IngestedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_AllowsNullableValues()
    {
        var kpi = KpiMeasurement.Create(
            "c1",
            DateTimeOffset.UtcNow,
            autarky: null,
            ownConsumption: 0.6m,
            community: null,
            feed: 3.2m,
            remainingDemand: null);

        kpi.Autarky.Should().BeNull();
        kpi.OwnConsumption.Should().Be(0.6m);
        kpi.Community.Should().BeNull();
        kpi.RemainingDemand.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidCommunityId_Throws(string? id)
    {
        var act = () => KpiMeasurement.Create(id!, DateTimeOffset.UtcNow, 1m, 1m, 1m, 1m, 1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyUpsert_OverwritesValuesAndUpdatesTimestamps()
    {
        var kpi = KpiMeasurement.Create("c1", DateTimeOffset.UtcNow.AddDays(-1), 0.1m, 0.2m, 3m, 4m, 5m);
        var originalCreated = kpi.CreatedAt;
        var now = DateTimeOffset.UtcNow;

        kpi.ApplyUpsert(0.9m, null, 99m, 88m, 77m, now);

        kpi.Autarky.Should().Be(0.9m);
        kpi.OwnConsumption.Should().BeNull();
        kpi.Community.Should().Be(99m);
        kpi.Feed.Should().Be(88m);
        kpi.RemainingDemand.Should().Be(77m);
        kpi.IngestedAt.Should().Be(now);
        kpi.UpdatedAt.Should().Be(now);
        kpi.CreatedAt.Should().Be(originalCreated);
    }
}

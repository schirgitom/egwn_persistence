using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Tests.Infrastructure;

public sealed class CommunityScopedPersistenceModelTests
{
    [Fact]
    public void RawMeasurements_UseCommunityScopedIdentity()
    {
        var options = new DbContextOptionsBuilder<EGWNPersistenceDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new EGWNPersistenceDbContext(options);

        var entity = context.Model.FindEntityType(typeof(MeterMeasurement))!;
        var keyProperties = entity.FindPrimaryKey()!.Properties.Select(property => property.Name);

        keyProperties.Should().Equal(nameof(MeterMeasurement.MeterPointId),
            nameof(MeterMeasurement.CommunityId),
            nameof(MeterMeasurement.Timestamp));
    }

    [Fact]
    public void MeterPointCommunities_UseManyToManyIdentity()
    {
        var options = new DbContextOptionsBuilder<EGWNPersistenceDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new EGWNPersistenceDbContext(options);

        var entity = context.Model.FindEntityType(typeof(MeterPointCommunity))!;
        var keyProperties = entity.FindPrimaryKey()!.Properties.Select(property => property.Name);

        keyProperties.Should().Equal(nameof(MeterPointCommunity.MeterPointId),
            nameof(MeterPointCommunity.CommunityId));
        entity.GetIndexes().Single(index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(MeterPointCommunity.CommunityId) }))
            .Should()
            .NotBeNull();
    }
}

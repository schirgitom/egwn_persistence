using System.Text.Json;
using EGWNPersistence.Application;
using EGWNPersistence.Application.DTOs;
using FluentAssertions;

namespace EGWNPersistence.Tests.Application;

public sealed class KpiMessageDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_ProductionPayload_MapsAllFieldsIncludingNulls()
    {
        const string json = """
            {
              "communityId": "2OWxVjZ6MMjZzyRB",
              "values": [
                {
                  "timestamp": "2026-09-20T00:00:00+02:00",
                  "values": {
                    "autarky": 0.8532,
                    "ownConsumption": 0.7241,
                    "community": 12.34,
                    "feed": 4.567,
                    "remainingDemand": 8.912
                  }
                },
                {
                  "timestamp": "2026-09-21T00:00:00+02:00",
                  "values": { "autarky": null, "ownConsumption": 0.6, "community": 10.0, "feed": 3.2, "remainingDemand": 7.5 }
                }
              ],
              "createdAtUtc": "2026-09-21T06:57:41.1234567+00:00"
            }
            """;

        var dto = JsonSerializer.Deserialize<KpiMessageDto>(json, JsonOptions);

        dto.Should().NotBeNull();
        dto!.CommunityId.Should().Be("2OWxVjZ6MMjZzyRB");
        dto.Values.Should().HaveCount(2);

        dto.Values[0].Timestamp.Should().Be(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.FromHours(2)));
        dto.Values[0].Values.Autarky.Should().Be(0.8532m);
        dto.Values[0].Values.OwnConsumption.Should().Be(0.7241m);
        dto.Values[0].Values.Community.Should().Be(12.34m);
        dto.Values[0].Values.Feed.Should().Be(4.567m);
        dto.Values[0].Values.RemainingDemand.Should().Be(8.912m);

        dto.Values[1].Values.Autarky.Should().BeNull();
        dto.Values[1].Values.OwnConsumption.Should().Be(0.6m);
    }

    [Fact]
    public void ToCommand_MapsAllEntries()
    {
        var dto = new KpiMessageDto
        {
            CommunityId = "community-1",
            CreatedAtUtc = new DateTimeOffset(2026, 9, 21, 6, 57, 41, TimeSpan.Zero),
            Values =
            [
                new KpiValueEntryDto
                {
                    Timestamp = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.FromHours(2)),
                    Values = new KpiValuesDto
                    {
                        Autarky = 0.85m,
                        OwnConsumption = 0.72m,
                        Community = 12.34m,
                        Feed = 4.5m,
                        RemainingDemand = 8.9m
                    }
                },
                new KpiValueEntryDto
                {
                    Timestamp = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
                    Values = new KpiValuesDto
                    {
                        Autarky = null,
                        OwnConsumption = 0.6m,
                        Community = 10.0m,
                        Feed = 3.2m,
                        RemainingDemand = 7.5m
                    }
                }
            ]
        };

        var command = KpiMessageMapper.ToCommand(dto);

        command.CommunityId.Should().Be("community-1");
        command.Entries.Should().HaveCount(2);
        command.Entries[0].Autarky.Should().Be(0.85m);
        command.Entries[1].Autarky.Should().BeNull();
        command.Entries[1].OwnConsumption.Should().Be(0.6m);
    }
}

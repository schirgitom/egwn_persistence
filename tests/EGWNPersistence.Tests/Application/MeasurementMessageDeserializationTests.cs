using System.Text.Json;
using EGWNPersistence.Application;
using EGWNPersistence.Application.DTOs;
using FluentAssertions;

namespace EGWNPersistence.Tests.Application;

public sealed class MeasurementMessageDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_ValidMessage_MapsAllFields()
    {
        const string json = """
            {
              "meterPointNumber": "AT001234567890",
              "communityId": "2OWxVjZ6MMjZzyRB",
              "createdAtUtc": "2026-08-30T13:04:52Z",
              "readings": [
                {
                  "timestamp": "2026-08-09T00:00:00Z",
                  "totalConsumptionValue": 120.5,
                  "gridShareValue": 20.7,
                  "communityShareValue": 99.8
                }
              ]
            }
            """;

        var dto = JsonSerializer.Deserialize<MeterReadingMessageDto>(json, JsonOptions);

        dto.Should().NotBeNull();
        dto!.MeterPointNumber.Should().Be("AT001234567890");
        dto.CommunityId.Should().Be("2OWxVjZ6MMjZzyRB");
        dto.Readings.Should().HaveCount(1);
        dto.Readings[0].TotalConsumptionValue.Should().Be(120.5m);
        dto.Readings[0].GridShareValue.Should().Be(20.7m);
        dto.Readings[0].CommunityShareValue.Should().Be(99.8m);
    }

    [Fact]
    public void Deserialize_MultipleEntries_MapsAll()
    {
        const string json = """
            {
              "meterPointNumber": "AT001",
              "communityId": "community-1",
              "createdAtUtc": "2026-08-30T13:04:52Z",
              "readings": [
                { "timestamp": "2024-01-15T08:00:00Z", "totalConsumptionValue": 1.0, "gridShareValue": 0.5, "communityShareValue": 0.5 },
                { "timestamp": "2024-01-15T09:00:00Z", "totalConsumptionValue": 2.0, "gridShareValue": 1.0, "communityShareValue": 1.0 }
              ]
            }
            """;

        var dto = JsonSerializer.Deserialize<MeterReadingMessageDto>(json, JsonOptions);
        dto!.Readings.Should().HaveCount(2);
    }

    [Fact]
    public void ToCommand_MapsAllMeasurements()
    {
        var dto = new MeterReadingMessageDto
        {
            MeterPointNumber = "AT001",
            CommunityId = "community-1",
            CreatedAtUtc = new DateTimeOffset(2026, 8, 30, 13, 4, 52, TimeSpan.Zero),
            Readings =
            [
                new MeterReadingEntryDto
                {
                    Timestamp = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero),
                    TotalConsumptionValue = 1.5m,
                    GridShareValue = 0.7m,
                    CommunityShareValue = 0.8m
                }
            ]
        };

        var command = MeasurementMapper.ToCommand(dto);

        command.MeterPointNumber.Should().Be("AT001");
        command.Measurements.Should().HaveCount(1);
        command.Measurements[0].TotalConsumption.Should().Be(1.5m);
        command.Measurements[0].Source.Should().Be("EDA");
        command.Measurements[0].GridSharePercent.Should().BeApproximately(0.4667m, 0.0001m);
    }
}

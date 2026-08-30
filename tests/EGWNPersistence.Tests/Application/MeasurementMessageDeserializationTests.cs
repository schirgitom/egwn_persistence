using EGWNPersistence.Application;
using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.DTOs;
using FluentAssertions;
using System.Text.Json;

namespace EGWNPersistence.Tests.Application;

public sealed class MeasurementMessageDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_ValidMessage_MapsAllFields()
    {
        // Arrange
        const string json = """
            {
              "MeterPointNumber": "AT001234567890",
              "Measurements": [
                {
                  "Timestamp": "2024-01-15T08:00:00Z",
                  "TotalConsumption": 1.234,
                  "GridShare": 0.5,
                  "CommunityShare": 0.734,
                  "GridSharePercent": 0.4053,
                  "CommunitySharePercent": 0.5947,
                  "Source": "EDA"
                }
              ]
            }
            """;

        // Act
        var dto = JsonSerializer.Deserialize<MeasurementMessageDto>(json);

        // Assert
        dto.Should().NotBeNull();
        dto!.MeterPointNumber.Should().Be("AT001234567890");
        dto.Measurements.Should().HaveCount(1);

        var m = dto.Measurements[0];
        m.Timestamp.Should().Be(new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero));
        m.TotalConsumption.Should().Be(1.234m);
        m.GridShare.Should().Be(0.5m);
        m.CommunityShare.Should().Be(0.734m);
        m.GridSharePercent.Should().Be(0.4053m);
        m.CommunitySharePercent.Should().Be(0.5947m);
        m.Source.Should().Be("EDA");
    }

    [Fact]
    public void Deserialize_MultipleEntries_MapsAll()
    {
        const string json = """
            {
              "MeterPointNumber": "AT001",
              "Measurements": [
                { "Timestamp": "2024-01-15T08:00:00Z", "TotalConsumption": 1.0, "GridShare": 0.5, "CommunityShare": 0.5, "GridSharePercent": 0.5, "CommunitySharePercent": 0.5, "Source": "EDA" },
                { "Timestamp": "2024-01-15T09:00:00Z", "TotalConsumption": 2.0, "GridShare": 1.0, "CommunityShare": 1.0, "GridSharePercent": 0.5, "CommunitySharePercent": 0.5, "Source": "EDA" }
              ]
            }
            """;

        var dto = JsonSerializer.Deserialize<MeasurementMessageDto>(json);
        dto!.Measurements.Should().HaveCount(2);
    }

    [Fact]
    public void Deserialize_EmptyMeasurements_ReturnsEmptyList()
    {
        const string json = """{"MeterPointNumber": "AT001", "Measurements": []}""";
        var dto = JsonSerializer.Deserialize<MeasurementMessageDto>(json);
        dto!.Measurements.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_MissingMeterPointNumber_DefaultsToEmpty()
    {
        const string json = """{"Measurements": []}""";
        var dto = JsonSerializer.Deserialize<MeasurementMessageDto>(json);
        dto!.MeterPointNumber.Should().BeEmpty();
    }

    [Fact]
    public void ToCommand_MapsAllMeasurements()
    {
        var dto = new MeasurementMessageDto
        {
            MeterPointNumber = "AT001",
            Measurements =
            [
                new MeasurementEntryDto
                {
                    Timestamp = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero),
                    TotalConsumption = 1.5m,
                    GridShare = 0.7m,
                    CommunityShare = 0.8m,
                    GridSharePercent = 0.467m,
                    CommunitySharePercent = 0.533m,
                    Source = "EDA"
                }
            ]
        };

        var command = MeasurementMapper.ToCommand(dto);

        command.MeterPointNumber.Should().Be("AT001");
        command.Measurements.Should().HaveCount(1);
        command.Measurements[0].TotalConsumption.Should().Be(1.5m);
        command.Measurements[0].Source.Should().Be("EDA");
    }
}

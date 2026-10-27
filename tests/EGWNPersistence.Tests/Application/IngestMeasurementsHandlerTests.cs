using System.Text.Json;
using EGWNPersistence.Application;
using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.Configuration;
using EGWNPersistence.Application.DTOs;
using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EGWNPersistence.Tests.Application;

public sealed class IngestMeasurementsHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IMeterPointRepository> _meterPointRepo;
    private readonly Mock<IMeasurementRepository> _measurementRepo;
    private readonly IngestMeasurementsHandler _handler;

    public IngestMeasurementsHandlerTests()
    {
        _meterPointRepo = new Mock<IMeterPointRepository>();
        _measurementRepo = new Mock<IMeasurementRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.MeterPoints).Returns(_meterPointRepo.Object);
        _uow.Setup(u => u.Measurements).Returns(_measurementRepo.Object);
        _uow.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _handler = new IngestMeasurementsHandler(
            _uow.Object,
            NullLogger<IngestMeasurementsHandler>.Instance,
            Options.Create(new MeasurementCorrectionOptions { CorrectionWindowDays = 21 }));
    }

    [Fact]
    public async Task Handle_WhenMeterPointDoesNotExist_CreatesNewMeterPoint()
    {
        // Arrange
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeterPoint?)null);
        _meterPointRepo.Setup(r => r.AddAsync(It.IsAny<MeterPoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var mp = _meterPointRepo.Invocations
                    .Select(x => x.Arguments.FirstOrDefault())
                    .OfType<MeterPoint>()
                    .LastOrDefault();
                if (mp is null)
                    return;

                typeof(MeterPoint).GetProperty(nameof(MeterPoint.Id))!
                    .SetValue(mp, 1000L);
            })
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.GetExistingAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement>());
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand("AT001", 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _meterPointRepo.Verify(r => r.AddAsync(It.Is<MeterPoint>(mp => mp.MeterPointNumber == "AT001"), It.IsAny<CancellationToken>()), Times.Once);
        result.Inserted.Should().Be(1);
        result.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMeterPointExists_ResolvesExisting()
    {
        // Arrange
        var existingMeterPoint = MeterPoint.Reconstitute(1001L, "AT002", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        _measurementRepo.Setup(r => r.GetExistingAsync(existingMeterPoint.Id, It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement>());
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand("AT002", 2);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _meterPointRepo.Verify(r => r.AddAsync(It.IsAny<MeterPoint>(), It.IsAny<CancellationToken>()), Times.Never);
        result.MeterPointId.Should().Be(existingMeterPoint.Id);
        result.TotalReceived.Should().Be(2);
        result.Inserted.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenMeterPointIsInAnotherCommunity_IngestsSeparateCommunityMeasurement()
    {
        var meterPoint = MeterPoint.Reconstitute(1006L, "AT006", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT006", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meterPoint);
        _measurementRepo.Setup(r => r.GetExistingAsync(
                meterPoint.Id,
                "community-2",
                It.IsAny<IEnumerable<DateTimeOffset>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement>());

        IEnumerable<MeterMeasurement>? inserted = null;
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MeterMeasurement>, CancellationToken>((measurements, _) => inserted = measurements.ToArray())
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IngestMeasurementsCommand(
            "community-2",
            "AT006",
            [new MeasurementEntry(DateTimeOffset.UtcNow.AddHours(-1), 1m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA")]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Inserted.Should().Be(1);
        inserted.Should().ContainSingle().Which.CommunityId.Should().Be("community-2");
        _meterPointRepo.Verify(
            r => r.EnsureCommunityMembershipAsync(meterPoint.Id, "community-2", It.IsAny<CancellationToken>()),
            Times.Once);
        _measurementRepo.Verify(
            r => r.GetExistingAsync(meterPoint.Id, "community-2", It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDuplicateTimestamps_SkipsDuplicates()
    {
        // Arrange
        var existingMeterPoint = MeterPoint.Reconstitute(1002L, "AT003", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var ts1 = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var ts2 = new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);

        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        var existing = MeterMeasurement.Reconstitute(
            existingMeterPoint.Id,
            "community-1",
            ts1,
            1.0m,
            0.5m,
            0.5m,
            0.5m,
            0.5m,
            "EDA",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));

        _measurementRepo.Setup(r => r.GetExistingAsync(existingMeterPoint.Id, It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement> { [ts1] = existing });
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IngestMeasurementsCommand("community-1", "AT003", new[]
        {
            new MeasurementEntry(ts1, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA"),
            new MeasurementEntry(ts2, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA")
        });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalReceived.Should().Be(2);
        result.Inserted.Should().Be(1);
        result.Skipped.Should().Be(1);

        _measurementRepo.Verify(r => r.BulkInsertAsync(
            It.Is<IEnumerable<MeterMeasurement>>(ms => ms.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAllDuplicates_DoesNotCallBulkInsert()
    {
        // Arrange
        var existingMeterPoint = MeterPoint.Reconstitute(1003L, "AT004", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var ts = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);

        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT004", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        var existing = MeterMeasurement.Reconstitute(
            existingMeterPoint.Id,
            "community-1",
            ts,
            1.0m,
            0.5m,
            0.5m,
            0.5m,
            0.5m,
            "EDA",
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow.AddDays(-40),
            DateTimeOffset.UtcNow.AddDays(-30));

        _measurementRepo.Setup(r => r.GetExistingAsync(existingMeterPoint.Id, It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement> { [ts] = existing });

        var command = new IngestMeasurementsCommand("community-1", "AT004", [new MeasurementEntry(ts, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA")]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Inserted.Should().Be(0);
        result.Skipped.Should().Be(1);
        _measurementRepo.Verify(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExistingMeasurementIsWithinCorrectionWindow_UpdatesValue()
    {
        var existingMeterPoint = MeterPoint.Reconstitute(1005L, "AT005", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);

        var existing = MeterMeasurement.Reconstitute(
            existingMeterPoint.Id,
            "community-1",
            timestamp,
            1.0m,
            0.5m,
            0.5m,
            0.5m,
            0.5m,
            "EDA",
            DateTimeOffset.UtcNow.AddDays(-3),
            DateTimeOffset.UtcNow.AddDays(-4),
            DateTimeOffset.UtcNow.AddDays(-3));

        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT005", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        _measurementRepo.Setup(r => r.GetExistingAsync(existingMeterPoint.Id, It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, MeterMeasurement> { [timestamp.ToUniversalTime()] = existing });
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IngestMeasurementsCommand("community-1", "AT005", [new MeasurementEntry(timestamp, 2.5m, 1.0m, 1.5m, 0.4m, 0.6m, "EDA")]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Inserted.Should().Be(0);
        result.Skipped.Should().Be(1);
        _measurementRepo.Verify(r => r.UpdateAsync(
            It.Is<IEnumerable<MeterMeasurement>>(ms => ms.Single().TotalConsumption == 2.5m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DeserializeMeterReadingJson_UsesActualPayloadShape()
    {
        const string json = """
        {
          "meterPointNumber": "AT0080000819000000202209200159122",
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

        var dto = JsonSerializer.Deserialize<MeterReadingMessageDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        dto.Should().NotBeNull();
        dto!.MeterPointNumber.Should().Be("AT0080000819000000202209200159122");
        dto.Readings.Should().HaveCount(1);
        dto.Readings[0].TotalConsumptionValue.Should().Be(120.5m);

        var command = MeasurementMapper.ToCommand(dto);
        command.CommunityId.Should().Be(dto.CommunityId);
        command.MeterPointNumber.Should().Be(dto.MeterPointNumber);
        command.Measurements.Should().HaveCount(1);
        command.Measurements[0].GridSharePercent.Should().BeApproximately(0.1718m, 0.0001m);
    }

    [Fact]
    public async Task Handle_WhenDatabaseFails_RollsBackAndRethrows()
    {
        // Arrange
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var command = BuildCommand("AT005", 1);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB connection lost");
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IngestMeasurementsCommand BuildCommand(string meterPointNumber, int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(i => new MeasurementEntry(
                new DateTimeOffset(2024, 1, 15, 8 + i, 0, 0, TimeSpan.Zero),
                1.234m, 0.5m, 0.734m, 0.4053m, 0.5947m, "EDA"))
            .ToList();
        return new IngestMeasurementsCommand("community-1", meterPointNumber, entries);
    }
}

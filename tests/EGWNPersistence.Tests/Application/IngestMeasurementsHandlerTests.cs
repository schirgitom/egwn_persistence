using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.DTOs;
using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _handler = new IngestMeasurementsHandler(_uow.Object, NullLogger<IngestMeasurementsHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenMeterPointDoesNotExist_CreatesNewMeterPoint()
    {
        // Arrange
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeterPoint?)null);
        _meterPointRepo.Setup(r => r.AddAsync(It.IsAny<MeterPoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _measurementRepo.Setup(r => r.GetExistingTimestampsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateTimeOffset>());
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
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
        var existingMeterPoint = MeterPoint.Reconstitute(Guid.NewGuid(), "AT002", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        _measurementRepo.Setup(r => r.GetExistingTimestampsAsync(existingMeterPoint.Id, It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateTimeOffset>());
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
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
    public async Task Handle_WhenDuplicateTimestamps_SkipsDuplicates()
    {
        // Arrange
        var existingMeterPoint = MeterPoint.Reconstitute(Guid.NewGuid(), "AT003", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var ts1 = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var ts2 = new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);

        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        _measurementRepo.Setup(r => r.GetExistingTimestampsAsync(existingMeterPoint.Id, It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateTimeOffset> { ts1 }); // ts1 already exists
        _measurementRepo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IngestMeasurementsCommand("AT003", new[]
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
        var existingMeterPoint = MeterPoint.Reconstitute(Guid.NewGuid(), "AT004", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var ts = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);

        _meterPointRepo.Setup(r => r.GetByMeterPointNumberAsync("AT004", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMeterPoint);
        _measurementRepo.Setup(r => r.GetExistingTimestampsAsync(existingMeterPoint.Id, It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateTimeOffset> { ts });

        var command = new IngestMeasurementsCommand("AT004", [new MeasurementEntry(ts, 1.0m, 0.5m, 0.5m, 0.5m, 0.5m, "EDA")]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Inserted.Should().Be(0);
        result.Skipped.Should().Be(1);
        _measurementRepo.Verify(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MeterMeasurement>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        return new IngestMeasurementsCommand(meterPointNumber, entries);
    }
}

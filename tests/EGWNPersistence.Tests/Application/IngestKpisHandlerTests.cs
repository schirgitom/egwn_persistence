using EGWNPersistence.Application.Commands;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EGWNPersistence.Tests.Application;

public sealed class IngestKpisHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IKpiMeasurementRepository> _repo;
    private readonly IngestKpisHandler _handler;

    public IngestKpisHandlerTests()
    {
        _repo = new Mock<IKpiMeasurementRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.KpiMeasurements).Returns(_repo.Object);
        _uow.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _handler = new IngestKpisHandler(_uow.Object, NullLogger<IngestKpisHandler>.Instance);
    }

    private static KpiEntry Entry(DateTimeOffset ts, decimal? autarky = 0.5m)
        => new(ts, autarky, 0.7m, 12.3m, 4.5m, 8.9m);

    [Fact]
    public async Task Handle_AllNew_InsertsAll()
    {
        var ts1 = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var ts2 = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

        _repo.Setup(r => r.GetExistingAsync("c1", It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, KpiMeasurement>());

        List<KpiMeasurement>? inserted = null;
        _repo.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<KpiMeasurement>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<KpiMeasurement>, CancellationToken>((list, _) => inserted = list.ToList())
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new IngestKpisCommand("c1", new[] { Entry(ts1), Entry(ts2) }),
            CancellationToken.None);

        result.Inserted.Should().Be(2);
        result.Updated.Should().Be(0);
        inserted.Should().HaveCount(2);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<IEnumerable<KpiMeasurement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRow_UpsertsOverwritesValues()
    {
        var ts = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var existing = KpiMeasurement.Create("c1", ts, 0.1m, 0.2m, 3m, 4m, 5m);

        _repo.Setup(r => r.GetExistingAsync("c1", It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, KpiMeasurement> { [ts] = existing });

        List<KpiMeasurement>? updated = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<IEnumerable<KpiMeasurement>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<KpiMeasurement>, CancellationToken>((list, _) => updated = list.ToList())
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new IngestKpisCommand("c1", new[] { new KpiEntry(ts, 0.9m, null, 99m, 88m, 77m) }),
            CancellationToken.None);

        result.Inserted.Should().Be(0);
        result.Updated.Should().Be(1);
        updated.Should().ContainSingle();
        existing.Autarky.Should().Be(0.9m);
        existing.OwnConsumption.Should().BeNull();
        existing.Community.Should().Be(99m);
    }

    [Fact]
    public async Task Handle_MixedBatch_InsertsAndUpdates()
    {
        var tsExisting = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var tsNew = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
        var existing = KpiMeasurement.Create("c1", tsExisting, 0.1m, 0.2m, 3m, 4m, 5m);

        _repo.Setup(r => r.GetExistingAsync("c1", It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTimeOffset, KpiMeasurement> { [tsExisting] = existing });

        var result = await _handler.Handle(
            new IngestKpisCommand("c1", new[] { Entry(tsExisting), Entry(tsNew) }),
            CancellationToken.None);

        result.Inserted.Should().Be(1);
        result.Updated.Should().Be(1);
        result.TotalReceived.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_RollsBack()
    {
        _repo.Setup(r => r.GetExistingAsync(It.IsAny<string>(), It.IsAny<IEnumerable<DateTimeOffset>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await _handler.Handle(
            new IngestKpisCommand("c1", new[] { Entry(DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

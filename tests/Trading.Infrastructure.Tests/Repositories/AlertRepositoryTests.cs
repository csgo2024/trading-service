using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class AlertRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly AlertRepository _alertRepository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public AlertRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _domainEventDispatcher = fixture.DomainEventDispatcher;
        _alertRepository = new AlertRepository(_fixture.MongoContext!, _domainEventDispatcher);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldReturnOnlyActiveAlerts()
    {
        // Arrange
        var activeAlert = new Alert { Id = "1", Symbol = "BTCUSDT", Status = Status.Running };
        var inactiveAlert = new Alert { Id = "2", Symbol = "ETHUSDT", Status = Status.Paused };
        await _alertRepository.AddAsync(activeAlert);
        await _alertRepository.AddAsync(inactiveAlert);

        // Act
        var result = await _alertRepository.GetActiveAlertsAsync(CancellationToken.None);

        // Assert
        var alerts = result.ToList();
        Assert.Single(alerts);
        Assert.Equal(activeAlert.Id, alerts[0].Id);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldRemoveAllAlerts()
    {
        await _alertRepository.EmptyAsync();
        // Arrange
        var alerts = new[]
        {
            new Alert {  Symbol = "BTCUSDT" },
            new Alert {  Symbol = "ETHUSDT" }
        };
        await Task.WhenAll(alerts.Select(a => _alertRepository.AddAsync(a)));

        // Act
        var deletedCount = await _alertRepository.ClearAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(alerts.Length, deletedCount);
    }

    [Fact]
    public async Task ClearAllAsync_WhenEmpty_ShouldReturnZero()
    {
        await _alertRepository.EmptyAsync();
        // Act
        var deletedCount = await _alertRepository.ClearAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, deletedCount);
    }
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllAlerts()
    {
        await _alertRepository.EmptyAsync();
        // Arrange
        var alert1 = new Alert { Id = "1", Symbol = "BTCUSDT" };
        var alert2 = new Alert { Id = "2", Symbol = "ETHUSDT" };
        var alert3 = new Alert { Id = "3", Symbol = "DOGEUSDT" };
        await Task.WhenAll(
            _alertRepository.AddAsync(alert1),
            _alertRepository.AddAsync(alert2),
            _alertRepository.AddAsync(alert3)
        );

        // Act
        var result = await _alertRepository.GetAllAsync();

        // Assert
        var alerts = result.ToList();
        Assert.Equal(3, alerts.Count);
        Assert.Contains(alerts, a => a.Id == "1");
        Assert.Contains(alerts, a => a.Id == "2");
        Assert.Contains(alerts, a => a.Id == "3");
    }
}

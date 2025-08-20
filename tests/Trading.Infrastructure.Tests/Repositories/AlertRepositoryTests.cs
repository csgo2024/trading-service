using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class AlertRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly AlertRepository _repository;

    private readonly IDomainEventDispatcher _domainEventDispatcher;
    public AlertRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _domainEventDispatcher = fixture.DomainEventDispatcher;
        _repository = new AlertRepository(_fixture.MongoContext!, _domainEventDispatcher);
    }
    [Fact]
    public async Task GetActiveAlertsAsync_ShouldReturnOnlyActiveAlerts()
    {
        // Arrange
        var activeAlert = new Alert { Id = "1", Symbol = "BTCUSDT", Status = Status.Running };
        var inactiveAlert = new Alert { Id = "2", Symbol = "ETHUSDT", Status = Status.Paused };
        await _repository.AddAsync(activeAlert);
        await _repository.AddAsync(inactiveAlert);

        // Act
        var result = await _repository.GetActiveAlertsAsync(CancellationToken.None);

        // Assert
        var alerts = result.ToList();
        Assert.Single(alerts);
        Assert.Equal(activeAlert.Id, alerts[0].Id);
    }

    [Fact]
    public async Task GetActiveAlerts_WithSymbol_ShouldReturnMatchingAlerts()
    {
        await _repository.EmptyAsync();
        // Arrange
        var symbol = "BTCUSDT";
        var matchingAlert = new Alert { Id = "1", Symbol = symbol, Status = Status.Running };
        var differentSymbolAlert = new Alert { Id = "2", Symbol = "ETHUSDT", Status = Status.Running };
        await _repository.AddAsync(matchingAlert);
        await _repository.AddAsync(differentSymbolAlert);

        // Act
        var result = _repository.GetActiveAlerts(symbol);

        // Assert
        var alerts = result.ToList();
        Assert.Single(alerts);
        Assert.Equal(matchingAlert.Id, alerts[0].Id);
    }

    [Fact]
    public async Task ClearAllAlertsAsync_ShouldRemoveAllAlerts()
    {
        await _repository.EmptyAsync();
        // Arrange
        var alerts = new[]
        {
            new Alert {  Symbol = "BTCUSDT" },
            new Alert {  Symbol = "ETHUSDT" }
        };
        await Task.WhenAll(alerts.Select(a => _repository.AddAsync(a)));

        // Act
        var deletedCount = await _repository.ClearAllAlertsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(alerts.Length, deletedCount);
    }

    [Fact]
    public async Task ClearAllAlertsAsync_WhenEmpty_ShouldReturnZero()
    {
        await _repository.EmptyAsync();
        // Act
        var deletedCount = await _repository.ClearAllAlertsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, deletedCount);
    }
    [Fact]
    public async Task GetAllAlerts_ShouldReturnAllAlerts()
    {
        await _repository.EmptyAsync();
        // Arrange
        var alert1 = new Alert { Id = "1", Symbol = "BTCUSDT" };
        var alert2 = new Alert { Id = "2", Symbol = "ETHUSDT" };
        var alert3 = new Alert { Id = "3", Symbol = "DOGEUSDT" };
        await Task.WhenAll(
            _repository.AddAsync(alert1),
            _repository.AddAsync(alert2),
            _repository.AddAsync(alert3)
        );

        // Act
        var result = await _repository.GetAllAlerts();

        // Assert
        var alerts = result.ToList();
        Assert.Equal(3, alerts.Count);
        Assert.Contains(alerts, a => a.Id == "1");
        Assert.Contains(alerts, a => a.Id == "2");
        Assert.Contains(alerts, a => a.Id == "3");
    }
}

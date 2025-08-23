using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class StrategyRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly StrategyRepository _repository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public StrategyRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _domainEventDispatcher = fixture.DomainEventDispatcher;
        _repository = new StrategyRepository(_fixture.MongoContext!, _domainEventDispatcher);
    }

    [Fact]
    public async Task Add_WithDuplicateStrategy_ShouldBeAddedSuccessfully()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Future,
            Amount = 100,
            Status = Status.Running
        };
        await _repository.AddAsync(strategy);

        var duplicateStrategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Future,
            Amount = 200,
            Status = Status.Running
        };

        await _repository.AddAsync(duplicateStrategy);

        // Act & Assert
        var result = await _repository.GetAllAsync();
        Assert.Equal(2, result.Count);

    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "BTC1", AccountType = AccountType.Spot },
            new() { Symbol = "BTC2", AccountType = AccountType.Future }
        };

        foreach (var strategy in strategies)
        {
            await _repository.AddAsync(strategy);
        }

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Symbol == "BTC1");
        Assert.Contains(result, s => s.Symbol == "BTC2");
    }

    [Fact]
    public async Task GetActiveStrategyAsync_ShouldReturnAllActivestrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "S1", AccountType = AccountType.Spot, Status = Status.Running },
            new() { Symbol = "S2", AccountType = AccountType.Spot, Status = Status.Paused },
            new() { Symbol = "F1", AccountType = AccountType.Future, Status = Status.Running }
        };

        foreach (var strategy in strategies)
        {
            await _repository.AddAsync(strategy);
        }

        // Act
        var result = await _repository.GetActiveStrategyAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Single(result, x => x.Symbol.Contains("S1") && x.AccountType == AccountType.Spot);
        Assert.Single(result, x => x.Symbol.Contains("F1") && x.AccountType == AccountType.Future);
    }
}

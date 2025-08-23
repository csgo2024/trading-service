using MongoDB.Driver;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class StrategyRepository : BaseRepository<Strategy>, IStrategyRepository
{
    public StrategyRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
        : base(context, domainEventDispatcher)
    {
    }

    public async Task<List<Strategy>> GetActiveStrategyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _collection.Find(x => x.Status == Status.Running).ToListAsync(cancellationToken);
        return result;
    }

    public async Task<List<Strategy>> GetAllAsync()
    {
        var filter = Builders<Strategy>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.AccountType).ToListAsync();
        return strategies;
    }
}

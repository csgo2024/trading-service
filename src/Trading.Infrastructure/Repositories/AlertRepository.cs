using MongoDB.Driver;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class AlertRepository : BaseRepository<Alert>, IAlertRepository
{
    public AlertRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
        : base(context, domainEventDispatcher)
    {
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.Status == Status.Running).ToListAsync(cancellationToken);
    }
    public IEnumerable<Alert> GetActiveAlerts(string symbol)
    {
        return _collection.Find(x => x.Status == Status.Running && x.Symbol == symbol).ToList();
    }

    public async Task<int> ClearAllAlertsAsync(CancellationToken cancellationToken)
    {
        var deleteResult = await _collection.DeleteManyAsync(
            Builders<Alert>.Filter.Empty,
            cancellationToken);

        return (int)deleteResult.DeletedCount;
    }

    public async Task<List<Alert>> GetAllAlerts()
    {
        var filter = Builders<Alert>.Filter.Empty;
        var alerts = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.CreatedAt).ToListAsync();
        return alerts;
    }
}

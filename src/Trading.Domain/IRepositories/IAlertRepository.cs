using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IAlertRepository : IRepository<Alert>
{
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken);
    Task<int> ClearAllAsync(CancellationToken cancellationToken);
    Task<List<Alert>> GetAllAsync();
}

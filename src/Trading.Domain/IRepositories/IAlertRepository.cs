using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IAlertRepository : IRepository<Alert>
{
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken);
    public IEnumerable<Alert> GetActiveAlerts(string symbol);
    Task<int> ClearAllAlertsAsync(CancellationToken cancellationToken);
    Task<List<Alert>> GetAllAlerts();
}

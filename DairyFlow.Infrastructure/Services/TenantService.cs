namespace DairyFlow.Infrastructure.Services;

public interface ITenantService
{
    Guid CurrentFarmId { get; }
    void SetFarmId(Guid farmId);
}

public class TenantService : ITenantService
{
    private Guid _farmId;

    public Guid CurrentFarmId => _farmId;

    public void SetFarmId(Guid farmId) => _farmId = farmId;
}

// DairyFlow.Core/Entities/BaseEntity.cs
namespace DairyFlow.Core.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? SyncId { get; set; } // offline sync tracking
}

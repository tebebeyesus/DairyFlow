using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly DairyFlowDbContext _db;

    public NotificationService(DairyFlowDbContext db) => _db = db;

    public async Task<IEnumerable<Notification>> GetAllAsync(Guid userId, bool unreadOnly)
    {
        var query = _db.Notifications
            .Where(n => n.UserId == null || n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
    }

    public async Task MarkReadAsync(Guid id)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notification != null)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        var notifications = await _db.Notifications
            .Where(n => !n.IsRead && (n.UserId == null || n.UserId == userId))
            .ToListAsync();
        foreach (var n in notifications) n.IsRead = true;
        await _db.SaveChangesAsync();
    }
}

using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class FeedingTemplateService : IFeedingTemplateService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public FeedingTemplateService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<object>> GetAllAsync()
    {
        var templates = await _db.FeedingTemplates
            .Include(t => t.Items).ThenInclude(i => i.Feed)
            .OrderBy(t => t.AnimalType).ThenBy(t => t.Stage)
            .ToListAsync();

        return templates.Select(t => Project(t)).ToList();
    }

    // One template per (AnimalType, Stage): create it, or replace the items on the existing one.
    public async Task<FeedingTemplate> UpsertAsync(CreateFeedingTemplateDto dto, Guid userId)
    {
        var template = await _db.FeedingTemplates
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.AnimalType == dto.AnimalType && t.Stage == dto.Stage);

        if (template == null)
        {
            template = new FeedingTemplate
            {
                FarmId = _tenant.CurrentFarmId,
                AnimalType = dto.AnimalType,
                Stage = dto.Stage,
                Notes = dto.Notes,
            };
            _db.FeedingTemplates.Add(template);
        }
        else
        {
            template.Notes = dto.Notes;
            template.UpdatedAt = DateTime.UtcNow;
            _db.FeedingTemplateItems.RemoveRange(template.Items);  // replace lines wholesale
        }

        foreach (var item in dto.Items.Where(i => i.FeedId != Guid.Empty && i.QuantityKg > 0))
        {
            _db.FeedingTemplateItems.Add(new FeedingTemplateItem
            {
                FarmId = _tenant.CurrentFarmId,
                Template = template,
                FeedId = item.FeedId,
                QuantityKg = item.QuantityKg,
            });
        }

        await _db.SaveChangesAsync();
        return template;
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await _db.FeedingTemplates.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Template not found.");
        _db.FeedingTemplates.Remove(template);   // items cascade
        await _db.SaveChangesAsync();
    }

    // The ration for a specific animal: exact (type, stage) match first, then fall back to (type, "Any").
    public async Task<IEnumerable<object>> GetForCowAsync(Guid cowId)
    {
        var cow = await _db.Cows.FirstOrDefaultAsync(c => c.Id == cowId);
        if (cow == null) return Enumerable.Empty<object>();

        var templates = await _db.FeedingTemplates
            .Include(t => t.Items).ThenInclude(i => i.Feed)
            .Where(t => t.AnimalType == cow.AnimalType && (t.Stage == cow.Status || t.Stage == "Any"))
            .ToListAsync();

        // Prefer the exact-stage template over the "Any" fallback.
        var match = templates.FirstOrDefault(t => t.Stage == cow.Status)
                    ?? templates.FirstOrDefault(t => t.Stage == "Any");
        if (match == null) return Enumerable.Empty<object>();

        return match.Items
            .Where(i => i.Feed != null)
            .Select(i => (object)new
            {
                feedId = i.FeedId,
                feedName = i.Feed!.Name,
                feedUnit = i.Feed.Unit,
                quantityKg = i.QuantityKg
            })
            .ToList();
    }

    private static object Project(FeedingTemplate t) => new
    {
        id = t.Id,
        animalType = t.AnimalType,
        stage = t.Stage,
        notes = t.Notes,
        items = t.Items.Where(i => i.Feed != null).Select(i => new
        {
            feedId = i.FeedId,
            feedName = i.Feed!.Name,
            feedUnit = i.Feed.Unit,
            quantityKg = i.QuantityKg
        }).ToList()
    };
}

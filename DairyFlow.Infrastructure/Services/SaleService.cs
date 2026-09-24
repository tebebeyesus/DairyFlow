using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public SaleService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<Sale>> GetAllAsync(DateOnly? from, DateOnly? to)
    {
        var query = _db.Sales.AsQueryable();
        if (from.HasValue) query = query.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SaleDate <= to.Value);
        return await query.OrderByDescending(s => s.SaleDate).ToListAsync();
    }

    public async Task<IEnumerable<object>> GetMonthlySummaryAsync(int months)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddMonths(-months));
        var sales = await _db.Sales.Where(s => s.SaleDate >= from).ToListAsync();

        return sales
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => (object)new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                totalLiters = g.Sum(s => s.LitersSold),
                totalRevenue = g.Sum(s => s.LitersSold * s.PricePerLiter),
                avgPrice = g.Average(s => s.PricePerLiter),
                count = g.Count()
            });
    }

    public async Task<Sale> CreateAsync(CreateSaleDto dto, Guid userId)
    {
        var sale = new Sale
        {
            FarmId = _tenant.CurrentFarmId,
            SaleDate = dto.SaleDate,
            LitersSold = dto.LitersSold,
            PricePerLiter = dto.PricePerLiter,
            BuyerName = dto.BuyerName,
            BuyerPhone = dto.BuyerPhone,
            PaymentStatus = dto.PaymentStatus,
            Notes = dto.Notes,
            RecordedBy = userId
        };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        return sale;
    }

    public async Task<Sale> UpdateAsync(Guid id, CreateSaleDto dto)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Sale not found.");

        sale.SaleDate = dto.SaleDate;
        sale.LitersSold = dto.LitersSold;
        sale.PricePerLiter = dto.PricePerLiter;
        sale.BuyerName = dto.BuyerName;
        sale.BuyerPhone = dto.BuyerPhone;
        sale.PaymentStatus = dto.PaymentStatus;
        sale.Notes = dto.Notes;
        sale.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return sale;
    }

    public async Task DeleteAsync(Guid id)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Sale not found.");
        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
    }
}

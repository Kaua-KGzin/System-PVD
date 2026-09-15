using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class LoyaltyService(PdvDbContext db)
{
    private const int PointsPerReal = 1; // 1 point per R$ 1 spent

    public async Task<CustomerLoyaltyResponse?> GetByCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var loyalty = await db.CustomerLoyalties
            .AsNoTracking()
            .Include(l => l.Transactions.OrderByDescending(t => t.CreatedAt).Take(20))
            .FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);

        return loyalty is null ? null : ToResponse(loyalty);
    }

    public async Task<PagedResponse<CustomerLoyaltyResponse>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.CustomerLoyalties.AsNoTracking().Include(l => l.Customer);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.TotalPoints)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);

        return new PagedResponse<CustomerLoyaltyResponse>(
            items.Select(ToResponse).ToArray(),
            page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<CustomerLoyaltyResponse>> EarnPointsAsync(
        Guid customerId, Guid? saleId, decimal amount, CancellationToken ct)
    {
        var points = (int)Math.Floor((double)amount / PointsPerReal);
        if (points <= 0)
            return ServiceResult<CustomerLoyaltyResponse>.Fail("Valor insuficiente para acumular pontos.");

        var loyalty = await db.CustomerLoyalties
            .FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);

        if (loyalty is null)
        {
            loyalty = new CustomerLoyalty { CustomerId = customerId };
            db.CustomerLoyalties.Add(loyalty);
        }

        loyalty.TotalPoints += points;
        loyalty.UpdatedAt = DateTimeOffset.UtcNow;

        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            SaleId = saleId,
            Points = points,
            Type = "Earn",
            Notes = $"Compra R$ {amount:F2}"
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<CustomerLoyaltyResponse>.Ok(ToResponse(loyalty));
    }

    public async Task<ServiceResult<CustomerLoyaltyResponse>> RedeemPointsAsync(
        Guid customerId, int points, string? notes, CancellationToken ct)
    {
        var loyalty = await db.CustomerLoyalties
            .FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);

        if (loyalty is null)
            return ServiceResult<CustomerLoyaltyResponse>.Fail("Cliente nao possui programa de fidelidade.", StatusCodes.Status404NotFound);

        if (loyalty.AvailablePoints < points)
            return ServiceResult<CustomerLoyaltyResponse>.Fail($"Pontos disponiveis: {loyalty.AvailablePoints}.");

        loyalty.RedeemedPoints += points;
        loyalty.UpdatedAt = DateTimeOffset.UtcNow;

        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            Points = points,
            Type = "Redeem",
            Notes = notes ?? $"Resgate de {points} pontos"
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<CustomerLoyaltyResponse>.Ok(ToResponse(loyalty));
    }

    private static CustomerLoyaltyResponse ToResponse(CustomerLoyalty l) =>
        new(
            l.Id,
            l.CustomerId,
            l.Customer?.Name ?? "",
            l.TotalPoints,
            l.RedeemedPoints,
            l.AvailablePoints,
            l.CreatedAt,
            l.Transactions?.Select(t => new LoyaltyTransactionResponse(
                t.Id, t.Points, t.Type, t.Notes, t.CreatedAt)).ToArray() ?? []);
}

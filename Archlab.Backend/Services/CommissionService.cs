using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class CommissionService(PdvDbContext db)
{
    public async Task<PagedResponse<SellerCommissionResponse>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.SellerCommissions.AsNoTracking().Include(sc => sc.User);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(sc => sc.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);

        return new PagedResponse<SellerCommissionResponse>(
            items.Select(ToResponse).ToArray(),
            page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<SellerCommissionResponse>> CreateAsync(
        CreateSellerCommissionRequest request, CancellationToken ct)
    {
        if (request.Percentage <= 0 || request.Percentage > 100)
            return ServiceResult<SellerCommissionResponse>.Fail("Porcentagem deve ser entre 0 e 100.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null)
            return ServiceResult<SellerCommissionResponse>.Fail("Usuario nao encontrado.", StatusCodes.Status404NotFound);

        if (await db.SellerCommissions.AnyAsync(sc => sc.UserId == request.UserId, ct))
            return ServiceResult<SellerCommissionResponse>.Fail("Usuario ja possui comissao configurada.", StatusCodes.Status409Conflict);

        var commission = new SellerCommission
        {
            UserId = request.UserId,
            Percentage = request.Percentage
        };

        db.SellerCommissions.Add(commission);
        await db.SaveChangesAsync(ct);

        commission.User = user;
        return ServiceResult<SellerCommissionResponse>.Ok(ToResponse(commission));
    }

    public async Task<ServiceResult<SellerCommissionResponse>> UpdateAsync(
        Guid id, UpdateSellerCommissionRequest request, CancellationToken ct)
    {
        var commission = await db.SellerCommissions
            .Include(sc => sc.User)
            .FirstOrDefaultAsync(sc => sc.Id == id, ct);

        if (commission is null)
            return ServiceResult<SellerCommissionResponse>.Fail("Comissao nao encontrada.", StatusCodes.Status404NotFound);

        if (request.Percentage.HasValue)
        {
            if (request.Percentage <= 0 || request.Percentage > 100)
                return ServiceResult<SellerCommissionResponse>.Fail("Porcentagem deve ser entre 0 e 100.");
            commission.Percentage = request.Percentage.Value;
        }

        if (request.IsActive.HasValue)
            commission.IsActive = request.IsActive.Value;

        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return ServiceResult<SellerCommissionResponse>.Ok(ToResponse(commission));
    }

    public async Task<CommissionSummary> GetSummaryAsync(
        Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var commission = await db.SellerCommissions
            .AsNoTracking()
            .Include(sc => sc.User)
            .FirstOrDefaultAsync(sc => sc.UserId == userId, ct);

        if (commission is null)
            return new CommissionSummary(0, 0, 0, 0, []);

        var transactions = await db.CommissionTransactions
            .AsNoTracking()
            .Include(ct => ct.Sale)
            .Where(ct => ct.SellerCommissionId == commission.Id
                && ct.CreatedAt >= from && ct.CreatedAt <= to)
            .OrderByDescending(ct => ct.CreatedAt)
            .ToArrayAsync(ct);

        var totalCommission = transactions.Sum(t => t.CommissionAmount);
        var paidCommission = transactions.Where(t => t.IsPaid).Sum(t => t.CommissionAmount);
        var pendingCommission = transactions.Where(t => !t.IsPaid).Sum(t => t.CommissionAmount);

        return new CommissionSummary(
            totalCommission,
            paidCommission,
            pendingCommission,
            transactions.Length,
            transactions.Take(20).Select(t => new CommissionTransactionResponse(
                t.Id,
                t.Sale.Number,
                t.SaleAmount,
                t.CommissionPercentage,
                t.CommissionAmount,
                t.IsPaid,
                t.PaidAt,
                t.CreatedAt)).ToArray());
    }

    public async Task<ServiceResult<SellerCommissionResponse>> RegisterSaleCommissionAsync(
        Guid saleId, decimal saleAmount, CancellationToken ct)
    {
        var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId, ct);
        if (sale?.UserId is null)
            return ServiceResult<SellerCommissionResponse>.Fail("Venda sem operador vinculado.");

        var commission = await db.SellerCommissions
            .Include(sc => sc.User)
            .FirstOrDefaultAsync(sc => sc.UserId == sale.UserId && sc.IsActive, ct);

        if (commission is null)
            return ServiceResult<SellerCommissionResponse>.Fail("Vendedor sem comissao configurada.");

        var commissionAmount = Math.Round(saleAmount * commission.Percentage / 100, 2, MidpointRounding.AwayFromZero);

        var transaction = new CommissionTransaction
        {
            SellerCommissionId = commission.Id,
            SaleId = saleId,
            SaleAmount = saleAmount,
            CommissionPercentage = commission.Percentage,
            CommissionAmount = commissionAmount
        };

        db.CommissionTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        return ServiceResult<SellerCommissionResponse>.Ok(ToResponse(commission));
    }

    private static SellerCommissionResponse ToResponse(SellerCommission sc) =>
        new(
            sc.Id,
            sc.UserId,
            sc.User?.Username ?? "",
            sc.Percentage,
            sc.IsActive,
            sc.CreatedAt);
}

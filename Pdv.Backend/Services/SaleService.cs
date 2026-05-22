using System.Data;
using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class SaleService(PdvDbContext db, FiscalDocumentService fiscalDocumentService, ILogger<SaleService> logger)
{
    public async Task<PagedResponse<SaleResponse>> ListAsync(
        Guid? cashSessionId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Sales.AsNoTracking()
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Include(sale => sale.FiscalDocument)
            .AsQueryable();

        if (cashSessionId.HasValue)
            query = query.Where(sale => sale.CashSessionId == cashSessionId.Value);

        if (from.HasValue)
            query = query.Where(sale => sale.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sale => sale.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(sale => sale.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sale => SaleResponse.From(sale))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<SaleResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<SaleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sale = await LoadSaleAsync(id, asTracking: false, cancellationToken);

        return sale is null
            ? ServiceResult<SaleResponse>.Fail("Venda nao encontrada.", StatusCodes.Status404NotFound)
            : ServiceResult<SaleResponse>.Ok(SaleResponse.From(sale));
    }

    public async Task<ServiceResult<SaleResponse>> CreateAsync(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return ServiceResult<SaleResponse>.Fail(validation);

        // Serializable starts a write transaction early so stock and sale numbering are checked against a stable view.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var cashSession = await db.CashSessions.FirstOrDefaultAsync(
            session => session.Id == request.CashSessionId, cancellationToken);

        if (cashSession is null)
            return ServiceResult<SaleResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);

        if (cashSession.Status != CashSessionStatus.Open)
            return ServiceResult<SaleResponse>.Fail("A venda so pode ser registrada em um caixa aberto.", StatusCodes.Status409Conflict);

        var barcodes = request.Items.Select(item => item.Barcode.Trim()).Distinct().ToArray();
        var products = await db.Products
            .Where(product => barcodes.Contains(product.Barcode))
            .ToDictionaryAsync(product => product.Barcode, cancellationToken);

        var counter = await db.SaleCounters.FirstAsync(cancellationToken);
        counter.LastNumber++;

        if (request.CustomerId.HasValue)
        {
            var customer = await db.Customers.FindAsync([request.CustomerId.Value], cancellationToken);
            if (customer is null)
                return ServiceResult<SaleResponse>.Fail("Cliente nao encontrado.", StatusCodes.Status404NotFound);
        }

        var sale = new Sale
        {
            Number = counter.LastNumber,
            CashSessionId = cashSession.Id,
            TerminalId = cashSession.TerminalId,
            OperatorName = request.OperatorName.Trim(),
            CustomerDocument = string.IsNullOrWhiteSpace(request.CustomerDocument) ? null : request.CustomerDocument.Trim(),
            CustomerId = request.CustomerId,
            SaleDiscountTotal = request.SaleDiscountTotal
        };

        foreach (var requestedItem in request.Items)
        {
            var barcode = requestedItem.Barcode.Trim();
            if (!products.TryGetValue(barcode, out var product))
                return ServiceResult<SaleResponse>.Fail($"Produto nao encontrado para o codigo de barras {barcode}.", StatusCodes.Status404NotFound);

            if (!product.IsActive)
                return ServiceResult<SaleResponse>.Fail($"Produto {product.Name} esta inativo.");

            if (product.StockQuantity < requestedItem.Quantity)
                return ServiceResult<SaleResponse>.Fail($"Estoque insuficiente para {product.Name}. Disponivel: {product.StockQuantity}.");

            var grossTotal = RoundMoney(product.UnitPrice * requestedItem.Quantity);
            var discountTotal = RoundMoney(requestedItem.UnitDiscount * requestedItem.Quantity);
            var netTotal = RoundMoney(grossTotal - discountTotal);

            if (netTotal < 0)
                return ServiceResult<SaleResponse>.Fail($"Desconto maior que o valor do item {product.Name}.");

            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                Barcode = product.Barcode,
                ProductName = product.Name,
                UnitOfMeasure = product.UnitOfMeasure,
                Quantity = requestedItem.Quantity,
                UnitPrice = product.UnitPrice,
                UnitDiscount = requestedItem.UnitDiscount,
                GrossTotal = grossTotal,
                DiscountTotal = discountTotal,
                NetTotal = netTotal
            });

            product.StockQuantity -= requestedItem.Quantity;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                SaleId = sale.Id,
                QuantityDelta = -requestedItem.Quantity,
                Type = InventoryMovementType.Sale,
                Notes = $"Venda {sale.Number}"
            });
        }

        sale.GrossTotal = RoundMoney(sale.Items.Sum(item => item.GrossTotal));
        sale.ItemDiscountTotal = RoundMoney(sale.Items.Sum(item => item.DiscountTotal));
        sale.NetTotal = RoundMoney(sale.GrossTotal - sale.ItemDiscountTotal - sale.SaleDiscountTotal);

        if (sale.NetTotal < 0)
            return ServiceResult<SaleResponse>.Fail("Desconto da venda maior que o total dos itens.");

        foreach (var payment in request.Payments)
        {
            sale.Payments.Add(new SalePayment
            {
                Method = payment.Method,
                Amount = RoundMoney(payment.Amount),
                TransactionReference = string.IsNullOrWhiteSpace(payment.TransactionReference) ? null : payment.TransactionReference.Trim()
            });
        }

        sale.PaidAmount = RoundMoney(sale.Payments.Sum(payment => payment.Amount));

        if (sale.PaidAmount < sale.NetTotal)
            return ServiceResult<SaleResponse>.Fail("Pagamento insuficiente para concluir a venda.");

        sale.ChangeAmount = RoundMoney(sale.PaidAmount - sale.NetTotal);
        var cashPaid = sale.Payments.Where(payment => payment.Method == PaymentMethod.Cash).Sum(payment => payment.Amount);

        if (sale.ChangeAmount > 0 && cashPaid < sale.ChangeAmount)
            return ServiceResult<SaleResponse>.Fail("Troco so pode ser gerado a partir de pagamento em dinheiro.");

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);

        if (request.IssueFiscalDocument)
        {
            var document = await fiscalDocumentService.BuildForSaleAsync(sale, cancellationToken);
            db.FiscalDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Sale created: Id={SaleId} Number={Number} Terminal={Terminal} Operator={Operator} Total={NetTotal:F2}",
            sale.Id, sale.Number, sale.TerminalId, sale.OperatorName, sale.NetTotal);

        var createdSale = await LoadSaleAsync(sale.Id, asTracking: false, cancellationToken);
        return ServiceResult<SaleResponse>.Ok(SaleResponse.From(createdSale!));
    }

    public async Task<ServiceResult<SaleResponse>> CancelAsync(Guid id, CancelSaleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult<SaleResponse>.Fail("Motivo do cancelamento e obrigatorio.");

        var sale = await LoadSaleAsync(id, asTracking: true, cancellationToken);
        if (sale is null)
            return ServiceResult<SaleResponse>.Fail("Venda nao encontrada.", StatusCodes.Status404NotFound);

        if (sale.Status == SaleStatus.Cancelled)
            return ServiceResult<SaleResponse>.Fail("Venda ja cancelada.", StatusCodes.Status409Conflict);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAt = DateTimeOffset.UtcNow;
        sale.CancellationReason = request.Reason.Trim();

        // Load all products in one query to avoid N+1
        var productIds = sale.Items.Select(item => item.ProductId).ToHashSet();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in sale.Items)
        {
            var product = products[item.ProductId];
            product.StockQuantity += item.Quantity;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                SaleId = sale.Id,
                QuantityDelta = item.Quantity,
                Type = InventoryMovementType.SaleCancellation,
                Notes = $"Cancelamento da venda {sale.Number}"
            });
        }

        if (sale.FiscalDocument is not null)
        {
            sale.FiscalDocument.Status = FiscalDocumentStatus.Cancelled;
            sale.FiscalDocument.CancelledAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Sale cancelled: Id={SaleId} Number={Number} Reason={Reason}",
            sale.Id, sale.Number, sale.CancellationReason);

        var cancelledSale = await LoadSaleAsync(sale.Id, asTracking: false, cancellationToken);
        return ServiceResult<SaleResponse>.Ok(SaleResponse.From(cancelledSale!));
    }

    private async Task<Sale?> LoadSaleAsync(Guid id, bool asTracking, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Include(sale => sale.FiscalDocument)
            .Where(sale => sale.Id == id);

        if (!asTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static string? ValidateRequest(CreateSaleRequest request)
    {
        if (request.CashSessionId == Guid.Empty)
            return "Sessao de caixa e obrigatoria.";

        if (string.IsNullOrWhiteSpace(request.OperatorName))
            return "Operador e obrigatorio.";

        if (request.SaleDiscountTotal < 0)
            return "Desconto da venda nao pode ser negativo.";

        if (request.Items is null || request.Items.Count == 0)
            return "A venda precisa ter ao menos um item.";

        if (request.Payments is null || request.Payments.Count == 0)
            return "A venda precisa ter ao menos um pagamento.";

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Barcode))
                return "Todos os itens precisam informar codigo de barras.";

            if (item.Quantity <= 0)
                return "Quantidade do item precisa ser maior que zero.";

            if (item.UnitDiscount < 0)
                return "Desconto unitario nao pode ser negativo.";
        }

        foreach (var payment in request.Payments)
        {
            if (payment.Amount <= 0)
                return "Valor do pagamento precisa ser maior que zero.";
        }

        return null;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

using System.Text;

namespace Archlab.Backend.Hardware;

public sealed class VirtualMockReceiptPrinter(ILogger<VirtualMockReceiptPrinter> logger) : IReceiptPrinter
{
    public Task PrintSaleReceiptAsync(SalePrintModel sale, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine("              ARCHNEXUS PDV             ");
        sb.AppendLine("         CUPOM NAO FISCAL / VENDA       ");
        sb.AppendLine("========================================");
        sb.AppendLine($"Venda: #{sale.Number:D6}   Terminal: {sale.TerminalId}");
        sb.AppendLine($"Data: {sale.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Operador: {sale.OperatorName}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("ITEM  QTD   UNIT(R$)           TOTAL(R$)");
        sb.AppendLine("----------------------------------------");

        var idx = 1;
        foreach (var item in sale.Items)
        {
            var line = $"{idx:D2} {item.Name}";
            if (line.Length > 40) line = line[..40];
            sb.AppendLine(line);
            sb.AppendLine($"     {item.Quantity:N3} x {item.UnitPrice:N2}        {item.NetTotal,10:N2}");
            idx++;
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"SUBTOTAL:                     R$ {sale.GrossTotal,9:N2}");
        if (sale.DiscountTotal > 0)
        {
            sb.AppendLine($"DESCONTOS:                   -R$ {sale.DiscountTotal,9:N2}");
        }
        sb.AppendLine($"TOTAL LIQUIDO:                R$ {sale.NetTotal,9:N2}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("PAGAMENTOS:");
        foreach (var payment in sale.Payments)
        {
            sb.AppendLine($"  {payment.Method,-18}        R$ {payment.Amount,9:N2}");
        }
        if (sale.ChangeAmount > 0)
        {
            sb.AppendLine($"TROCO:                        R$ {sale.ChangeAmount,9:N2}");
        }
        sb.AppendLine("========================================");
        sb.AppendLine("      OBRIGADO E VOLTE SEMPRE!          ");
        sb.AppendLine("========================================");

        logger.LogInformation("VIRTUAL PRINTER OUTPUT:\n{Receipt}", sb.ToString());
        return Task.CompletedTask;
    }

    public Task OpenCashDrawerAsync(CancellationToken ct = default)
    {
        logger.LogInformation("VIRTUAL CASH DRAWER: Pulse sent (Drawer opened).");
        return Task.CompletedTask;
    }

    public Task PrintCashMovementReceiptAsync(CashMovementPrintModel movement, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine("              ARCHNEXUS PDV             ");
        sb.AppendLine($"     COMPROVANTE DE {movement.Type.ToUpperInvariant()}    ");
        sb.AppendLine("========================================");
        sb.AppendLine($"Data: {movement.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Terminal: {movement.TerminalId}  Operador: {movement.OperatorName}");
        sb.AppendLine($"VALOR: R$ {movement.Amount:N2}");
        if (!string.IsNullOrWhiteSpace(movement.Reason))
        {
            sb.AppendLine($"Motivo: {movement.Reason}");
        }
        sb.AppendLine("========================================");
        sb.AppendLine("Assinatura: ___________________________");
        sb.AppendLine("========================================");

        logger.LogInformation("VIRTUAL PRINTER (CASH MOVEMENT):\n{Receipt}", sb.ToString());
        return Task.CompletedTask;
    }
}

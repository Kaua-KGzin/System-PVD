using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class FiscalDocumentService(PdvDbContext db)
{
    private const string SimulatedIssuerDocument = "00000000000000";
    private const string StateCode = "35";
    private const string ModelCode = "65";
    private const string Series = "001";

    public async Task<FiscalDocument> BuildForSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        // FiscalCounter is updated inside the caller's Serializable transaction,
        // guaranteeing unique sequential numbers under concurrent requests.
        var counter = await db.FiscalCounters.FirstAsync(cancellationToken);
        counter.LastNumber++;
        var nextNumber = counter.LastNumber;

        var accessKey = BuildAccessKey(sale.CreatedAt, nextNumber);

        return new FiscalDocument
        {
            SaleId = sale.Id,
            Series = Series,
            Number = nextNumber,
            AccessKey = accessKey,
            XmlPayload = JsonSerializer.Serialize(new
            {
                aviso = "Documento fiscal simulado. Nao substitui emissao NFC-e/SAT autorizada pela SEFAZ.",
                venda = sale.Number,
                total = sale.NetTotal,
                emitidoEm = DateTimeOffset.UtcNow
            })
        };
    }

    public async Task<ServiceResult<FiscalDocumentSummaryResponse>> GetByAccessKeyAsync(string accessKey, CancellationToken cancellationToken)
    {
        var normalizedAccessKey = accessKey.Trim();
        var document = await db.FiscalDocuments.AsNoTracking()
            .FirstOrDefaultAsync(document => document.AccessKey == normalizedAccessKey, cancellationToken);

        return document is null
            ? ServiceResult<FiscalDocumentSummaryResponse>.Fail("Documento fiscal nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<FiscalDocumentSummaryResponse>.Ok(FiscalDocumentSummaryResponse.From(document));
    }

    public async Task<ServiceResult<FiscalDocumentSummaryResponse>> GetBySaleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var document = await db.FiscalDocuments.AsNoTracking()
            .FirstOrDefaultAsync(document => document.SaleId == saleId, cancellationToken);

        return document is null
            ? ServiceResult<FiscalDocumentSummaryResponse>.Fail("Venda sem documento fiscal emitido.", StatusCodes.Status404NotFound)
            : ServiceResult<FiscalDocumentSummaryResponse>.Ok(FiscalDocumentSummaryResponse.From(document));
    }

    // Admin/Manager only — returns the full XML payload.
    public async Task<ServiceResult<FiscalDocumentResponse>> GetXmlBySaleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var document = await db.FiscalDocuments.AsNoTracking()
            .FirstOrDefaultAsync(document => document.SaleId == saleId, cancellationToken);

        return document is null
            ? ServiceResult<FiscalDocumentResponse>.Fail("Venda sem documento fiscal emitido.", StatusCodes.Status404NotFound)
            : ServiceResult<FiscalDocumentResponse>.Ok(FiscalDocumentResponse.From(document));
    }

    private static string BuildAccessKey(DateTimeOffset issuedAt, int number)
    {
        var randomCode = Random.Shared.Next(0, 99_999_999).ToString("D8", CultureInfo.InvariantCulture);
        var body = string.Concat(
            StateCode,
            issuedAt.ToString("yyMM", CultureInfo.InvariantCulture),
            SimulatedIssuerDocument,
            ModelCode,
            Series,
            number.ToString("D9", CultureInfo.InvariantCulture),
            "1",
            randomCode);

        return body + CalculateCheckDigit(body);
    }

    private static int CalculateCheckDigit(string body)
    {
        var multiplier = 2;
        var sum = 0;

        for (var index = body.Length - 1; index >= 0; index--)
        {
            sum += (body[index] - '0') * multiplier;
            multiplier = multiplier == 9 ? 2 : multiplier + 1;
        }

        var remainder = sum % 11;
        var digit = 11 - remainder;

        return digit >= 10 ? 0 : digit;
    }
}

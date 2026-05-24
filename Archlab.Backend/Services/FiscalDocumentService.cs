using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class FiscalDocumentService(PdvDbContext db)
{
    private const string SimulatedIssuerDocument = "00000000000000";
    private const string StateCode = "35";
    private const string ModelCode = "65";
    private const string Series = "001";

    public async Task<FiscalDocument> BuildForSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        // Atomic increment — same pattern as SaleCounter.
        // Avoids the MAX() race condition where two concurrent sales could read the same max
        // and both produce the same fiscal number.
        var nextNumber = await GetNextFiscalNumberAsync(cancellationToken);
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

    public async Task<ServiceResult<FiscalDocumentResponse>> GetByAccessKeyAsync(string accessKey, CancellationToken cancellationToken)
    {
        var normalizedAccessKey = accessKey.Trim();
        var document = await db.FiscalDocuments.AsNoTracking()
            .FirstOrDefaultAsync(document => document.AccessKey == normalizedAccessKey, cancellationToken);

        return document is null
            ? ServiceResult<FiscalDocumentResponse>.Fail("Documento fiscal nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<FiscalDocumentResponse>.Ok(FiscalDocumentResponse.From(document));
    }

    public async Task<ServiceResult<FiscalDocumentResponse>> GetBySaleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var document = await db.FiscalDocuments.AsNoTracking()
            .FirstOrDefaultAsync(document => document.SaleId == saleId, cancellationToken);

        return document is null
            ? ServiceResult<FiscalDocumentResponse>.Fail("Venda sem documento fiscal emitido.", StatusCodes.Status404NotFound)
            : ServiceResult<FiscalDocumentResponse>.Ok(FiscalDocumentResponse.From(document));
    }

    private async Task<int> GetNextFiscalNumberAsync(CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql())
        {
            var result = await db.Database
                .SqlQuery<int>($@"UPDATE ""FiscalCounters"" SET ""LastNumber"" = ""LastNumber"" + 1 WHERE ""Id"" = 1 RETURNING ""LastNumber""")
                .ToListAsync(cancellationToken);
            return result.Single();
        }

        // SQLite: acceptable in single-threaded test environment
        var counter = await db.FiscalCounters.FirstAsync(cancellationToken);
        counter.LastNumber++;
        await db.SaveChangesAsync(cancellationToken);
        return counter.LastNumber;
    }

    private static string BuildAccessKey(DateTimeOffset issuedAt, int number)
    {
        var randomCode = RandomNumberGenerator.GetInt32(0, 99_999_999).ToString("D8", CultureInfo.InvariantCulture);
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

using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

public sealed record FiscalDocumentResponse(
    Guid Id,
    Guid SaleId,
    string Model,
    string Series,
    int Number,
    string AccessKey,
    FiscalDocumentStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? CancelledAt,
    string? XmlPayload)
{
    public static FiscalDocumentResponse From(FiscalDocument document) =>
        new(
            document.Id,
            document.SaleId,
            document.Model,
            document.Series,
            document.Number,
            document.AccessKey,
            document.Status,
            document.IssuedAt,
            document.CancelledAt,
            document.XmlPayload);
}

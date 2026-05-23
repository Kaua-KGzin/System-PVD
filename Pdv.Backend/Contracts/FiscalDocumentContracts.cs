using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

// Default response — no XML payload to avoid exposing fiscal data (CPF, amounts) to all roles.
public sealed record FiscalDocumentSummaryResponse(
    Guid Id,
    Guid SaleId,
    string Model,
    string Series,
    int Number,
    string AccessKey,
    FiscalDocumentStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? CancelledAt)
{
    public static FiscalDocumentSummaryResponse From(FiscalDocument document) =>
        new(
            document.Id,
            document.SaleId,
            document.Model,
            document.Series,
            document.Number,
            document.AccessKey,
            document.Status,
            document.IssuedAt,
            document.CancelledAt);
}

// Full response with XML — restricted to Admin/Manager only.
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

namespace Archlab.Backend.Domain;

public sealed class FiscalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public string Model { get; set; } = "NFC-e";
    public string Series { get; set; } = "001";
    public int Number { get; set; }
    public required string AccessKey { get; set; }
    public FiscalDocumentStatus Status { get; set; } = FiscalDocumentStatus.Issued;
    public bool IsContingency { get; set; }
    public string? Protocol { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? XmlPayload { get; set; }
}

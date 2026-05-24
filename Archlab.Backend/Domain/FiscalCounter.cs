namespace Archlab.Backend.Domain;

// Mirrors SaleCounter — provides an atomic-incrementable fiscal document sequence.
// Numbers must be sequential and gapless per SEFAZ NFC-e requirements.
public sealed class FiscalCounter
{
    public int Id { get; set; } = 1;
    public int LastNumber { get; set; }
}

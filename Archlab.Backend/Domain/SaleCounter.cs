namespace Archlab.Backend.Domain;

public sealed class SaleCounter
{
    public int Id { get; set; } = 1;
    public int LastNumber { get; set; }
    public uint RowVersion { get; set; }

}

using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public interface IFiscalIssuer
{
    Task<FiscalDocument> IssueForSaleAsync(Sale sale, bool contingencyMode = false, CancellationToken ct = default);
}

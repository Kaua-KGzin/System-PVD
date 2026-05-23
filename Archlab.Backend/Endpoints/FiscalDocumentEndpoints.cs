using Archlab.Backend.Common;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class FiscalDocumentEndpoints
{
    public static IEndpointRouteBuilder MapFiscalDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fiscal-documents")
            .WithTags("Fiscal")
            .RequireAuthorization();

        group.MapGet("/{accessKey}", async (
            string accessKey,
            FiscalDocumentService service,
            CancellationToken cancellationToken) =>
            (await service.GetByAccessKeyAsync(accessKey, cancellationToken)).ToHttpResult())
            .WithName("GetFiscalDocumentByAccessKey");

        group.MapGet("/sale/{saleId:guid}", async (
            Guid saleId,
            FiscalDocumentService service,
            CancellationToken cancellationToken) =>
            (await service.GetBySaleAsync(saleId, cancellationToken)).ToHttpResult())
            .WithName("GetFiscalDocumentBySale");

        return app;
    }
}

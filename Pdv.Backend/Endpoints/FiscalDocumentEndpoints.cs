using Pdv.Backend.Common;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class FiscalDocumentEndpoints
{
    public static IEndpointRouteBuilder MapFiscalDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fiscal-documents")
            .WithTags("Fiscal");

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

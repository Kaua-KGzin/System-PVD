using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class CashRegisterEndpoints
{
    public static IEndpointRouteBuilder MapCashRegisterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cash-sessions")
            .WithTags("Caixa")
            .RequireAuthorization();

        group.MapGet("/", async (
            CashRegisterService service,
            string? terminalId = null,
            bool onlyOpen = false,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(terminalId, onlyOpen, resolvedPage, resolvedPageSize, cancellationToken));
        })
            .WithName("ListCashSessions");

        group.MapGet("/{id:guid}", async (
            Guid id,
            CashRegisterService service,
            CancellationToken cancellationToken) =>
            (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetCashSessionById");

        group.MapGet("/open/{terminalId}", async (
            string terminalId,
            CashRegisterService service,
            CancellationToken cancellationToken) =>
            (await service.GetOpenByTerminalAsync(terminalId, cancellationToken)).ToHttpResult())
            .WithName("GetOpenCashSessionByTerminal");

        group.MapPost("/", async (
            OpenCashSessionRequest request,
            CashRegisterService service,
            CancellationToken cancellationToken) =>
            (await service.OpenAsync(request, cancellationToken))
                .ToCreatedResult(session => $"/api/cash-sessions/{session.Id}"))
            .WithName("OpenCashSession")
            .WithValidation<OpenCashSessionRequest>();

        group.MapPost("/{id:guid}/close", async (
            Guid id,
            CloseCashSessionRequest request,
            CashRegisterService service,
            CancellationToken cancellationToken) =>
            (await service.CloseAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("CloseCashSession")
            .WithValidation<CloseCashSessionRequest>();


        return app;
    }
}

using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class CashRegisterEndpoints
{
    public static IEndpointRouteBuilder MapCashRegisterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cash-sessions")
            .WithTags("Caixa");

        group.MapGet("/", async (
            CashRegisterService service,
            string? terminalId = null,
            bool onlyOpen = false,
            CancellationToken cancellationToken = default) =>
            Results.Ok(await service.ListAsync(terminalId, onlyOpen, cancellationToken)))
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
            .WithName("OpenCashSession");

        group.MapPost("/{id:guid}/close", async (
            Guid id,
            CloseCashSessionRequest request,
            CashRegisterService service,
            CancellationToken cancellationToken) =>
            (await service.CloseAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("CloseCashSession");

        return app;
    }
}

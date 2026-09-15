using Archlab.Backend.Domain;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public sealed record CreatePixRequest(decimal Amount, string Description);
public sealed record ProcessTefRequest(decimal Amount, PaymentMethod Method);

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("Pagamentos")
            .RequireAuthorization();

        group.MapPost("/pix", (
            CreatePixRequest request,
            IPaymentGatewayService gateway) =>
        {
            if (request.Amount <= 0)
                return Results.BadRequest(new { error = "O valor do Pix precisa ser maior que zero." });

            var result = gateway.GeneratePixCharge(request.Amount, request.Description);
            return Results.Ok(result);
        }).WithName("GeneratePixCharge");

        group.MapPost("/tef/authorize", (
            ProcessTefRequest request,
            IPaymentGatewayService gateway) =>
        {
            if (request.Amount <= 0)
                return Results.BadRequest(new { error = "O valor da transacao TEF precisa ser maior que zero." });

            var result = gateway.ProcessTefTransaction(request.Amount, request.Method);
            return Results.Ok(result);
        }).WithName("AuthorizeTefTransaction");

        return app;
    }
}

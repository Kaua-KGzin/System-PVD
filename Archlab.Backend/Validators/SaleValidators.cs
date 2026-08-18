using FluentValidation;
using Archlab.Backend.Contracts;

namespace Archlab.Backend.Validators;

public sealed class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.CashSessionId)
            .NotEmpty().WithMessage("Sessao de caixa e obrigatoria.");

        RuleFor(x => x.OperatorName)
            .NotEmpty().WithMessage("Operador e obrigatorio.");

        RuleFor(x => x.SaleDiscountTotal)
            .GreaterThanOrEqualTo(0).WithMessage("Desconto da venda nao pode ser negativo.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("A venda precisa ter ao menos um item.");

        RuleForEach(x => x.Items).SetValidator(new CreateSaleItemRequestValidator());

        RuleFor(x => x.Payments)
            .NotEmpty().WithMessage("A venda precisa ter ao menos um pagamento.");

        RuleForEach(x => x.Payments).SetValidator(new CreatePaymentRequestValidator());
    }
}

public sealed class CreateSaleItemRequestValidator : AbstractValidator<CreateSaleItemRequest>
{
    public CreateSaleItemRequestValidator()
    {
        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Codigo de barras e obrigatorio.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantidade do item precisa ser maior que zero.");

        RuleFor(x => x.UnitDiscount)
            .GreaterThanOrEqualTo(0).WithMessage("Desconto unitario nao pode ser negativo.");
    }
}

public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Valor do pagamento precisa ser maior que zero.");
    }
}

public sealed class CancelSaleRequestValidator : AbstractValidator<CancelSaleRequest>
{
    public CancelSaleRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Motivo do cancelamento e obrigatorio.");
    }
}

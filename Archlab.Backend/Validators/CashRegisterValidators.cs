using FluentValidation;
using Archlab.Backend.Contracts;

namespace Archlab.Backend.Validators;

public sealed class OpenCashSessionRequestValidator : AbstractValidator<OpenCashSessionRequest>
{
    public OpenCashSessionRequestValidator()
    {
        RuleFor(x => x.TerminalId)
            .NotEmpty().WithMessage("Terminal do PDV e obrigatorio.");

        RuleFor(x => x.OperatorName)
            .NotEmpty().WithMessage("Operador do caixa e obrigatorio.");

        RuleFor(x => x.OpeningAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Valor de abertura nao pode ser negativo.");
    }
}

public sealed class CloseCashSessionRequestValidator : AbstractValidator<CloseCashSessionRequest>
{
    public CloseCashSessionRequestValidator()
    {
        RuleFor(x => x.ClosingAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Valor de fechamento nao pode ser negativo.");
    }
}

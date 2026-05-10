using System.Text.RegularExpressions;
using FluentValidation;
using TransactionApi.Domain;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Application.Commands.CreateTransaction;

public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    private static readonly Regex AccountNumberPattern =
        new(@"^ACC-[A-Za-z0-9]+$", RegexOptions.Compiled);

    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Transaction type is required.")
            .Must(t => Enum.TryParse<TransactionType>(t, ignoreCase: true, out _))
            .WithMessage("Transaction type must be Deposit, Withdrawal, or Transfer.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be a positive number greater than zero.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Amount may have at most 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Must(Iso4217Codes.IsValid)
            .WithMessage("Currency must be a valid ISO 4217 alphabetic code (for example USD, EUR, GBP, or JPY).");

        RuleFor(x => x.FromAccount)
            .Must(BeNullOrValidAccount)
            .WithMessage(
                "When provided, From account must match the format ACC- followed by one or more alphanumeric characters (for example ACC-12345).");

        RuleFor(x => x.ToAccount)
            .Must(BeNullOrValidAccount)
            .WithMessage(
                "When provided, To account must match the format ACC- followed by one or more alphanumeric characters (for example ACC-67890).");

        When(IsDeposit, () =>
        {
            RuleFor(x => x.ToAccount)
                .NotEmpty()
                .WithMessage("To account is required for a deposit.");
        });

        When(IsWithdrawal, () =>
        {
            RuleFor(x => x.FromAccount)
                .NotEmpty()
                .WithMessage("From account is required for a withdrawal.");
        });

        When(IsTransfer, () =>
        {
            RuleFor(x => x.FromAccount)
                .NotEmpty()
                .WithMessage("From account is required for a transfer.");
            RuleFor(x => x.ToAccount)
                .NotEmpty()
                .WithMessage("To account is required for a transfer.");
        });
    }

    private static bool BeNullOrValidAccount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        return AccountNumberPattern.IsMatch(value.Trim());
    }

    private static bool IsDeposit(CreateTransactionCommand x) =>
        Enum.TryParse<TransactionType>(x.Type, ignoreCase: true, out var t) && t == TransactionType.Deposit;

    private static bool IsWithdrawal(CreateTransactionCommand x) =>
        Enum.TryParse<TransactionType>(x.Type, ignoreCase: true, out var t) && t == TransactionType.Withdrawal;

    private static bool IsTransfer(CreateTransactionCommand x) =>
        Enum.TryParse<TransactionType>(x.Type, ignoreCase: true, out var t) && t == TransactionType.Transfer;
}

using TransactionApi.Application.Commands.CreateTransaction;

namespace TransactionApi.Tests.Validators;

public class CreateTransactionCommandValidatorTests
{
    private readonly CreateTransactionCommandValidator _validator = new();

    private static CreateTransactionCommand Base(
        string type = "Transfer",
        string? fromAccount = "ACC-10001",
        string? toAccount = "ACC-10002",
        decimal amount = 50m,
        string currency = "USD") =>
        new()
        {
            Type = type,
            FromAccount = fromAccount,
            ToAccount = toAccount,
            Amount = amount,
            Currency = currency
        };

    [Fact]
    public void Valid_transfer_passes()
    {
        var result = _validator.Validate(Base());
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("deposit")]
    [InlineData("DEPOSIT")]
    [InlineData("Deposit")]
    public void Valid_deposit_minimal_passes_case_insensitive_type(string type)
    {
        var result = _validator.Validate(Base(type: type, fromAccount: null, toAccount: "ACC-10001"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("withdrawal")]
    [InlineData("WITHDRAWAL")]
    public void Valid_withdrawal_minimal_passes(string type)
    {
        var result = _validator.Validate(Base(type: type, fromAccount: "ACC-10001", toAccount: null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Type_empty_fails()
    {
        var result = _validator.Validate(Base(type: ""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Type)
            && e.ErrorMessage == "Transaction type is required.");
    }

    [Fact]
    public void Type_whitespace_fails_as_required()
    {
        var result = _validator.Validate(Base(type: "   "));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Type)
            && e.ErrorMessage == "Transaction type is required.");
    }

    [Theory]
    [InlineData("PayIn")]
    [InlineData("Unknown")]
    [InlineData("Transferring")]
    public void Type_unrecognized_fails(string type)
    {
        var result = _validator.Validate(Base(type: type));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Type)
            && e.ErrorMessage == "Transaction type must be Deposit, Withdrawal, or Transfer.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Amount_not_positive_fails(decimal amount)
    {
        var result = _validator.Validate(Base(amount: amount));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Amount)
            && e.ErrorMessage == "Amount must be a positive number greater than zero.");
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(10.123)]
    [InlineData(0.001)]
    public void Amount_more_than_two_decimal_places_fails(decimal amount)
    {
        var result = _validator.Validate(Base(amount: amount));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Amount)
            && e.ErrorMessage == "Amount may have at most 2 decimal places.");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(100)]
    [InlineData(99.99)]
    public void Amount_valid_two_or_fewer_decimal_places_passes_for_transfer(decimal amount)
    {
        var result = _validator.Validate(Base(amount: amount));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Currency_empty_fails()
    {
        var result = _validator.Validate(Base(currency: ""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Currency)
            && e.ErrorMessage == "Currency is required.");
    }

    [Fact]
    public void Currency_whitespace_only_fails()
    {
        var result = _validator.Validate(Base(currency: "   "));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Currency)
            && e.ErrorMessage == "Currency is required.");
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void Currency_known_iso_codes_pass(string currency)
    {
        var result = _validator.Validate(Base(currency: currency));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Currency_invalid_code_fails()
    {
        var result = _validator.Validate(Base(currency: "ZZZ"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.Currency)
            && e.ErrorMessage
            == "Currency must be a valid ISO 4217 alphabetic code (for example USD, EUR, GBP, or JPY).");
    }

    [Fact]
    public void Currency_valid_lowercase_passes_iso_check()
    {
        var result = _validator.Validate(Base(currency: "usd"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("acc-10001")] // prefix must be literal ACC (case-sensitive in regex)
    [InlineData("ACC-")]
    [InlineData("ACC-100 01")]
    [InlineData("BANK-10001")]
    [InlineData("ACC-100_01")]
    public void FromAccount_invalid_format_fails_when_provided_for_transfer(string from)
    {
        var result = _validator.Validate(Base(fromAccount: from));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.FromAccount)
            && e.ErrorMessage
            == "When provided, From account must match the format ACC- followed by one or more alphanumeric characters (for example ACC-12345).");
    }

    [Theory]
    [InlineData("to-wrong")]
    [InlineData("ACC-")]
    public void ToAccount_invalid_format_fails_when_provided_for_transfer(string to)
    {
        var result = _validator.Validate(Base(toAccount: to));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.ToAccount)
            && e.ErrorMessage
            == "When provided, To account must match the format ACC- followed by one or more alphanumeric characters (for example ACC-67890).");
    }

    [Fact]
    public void Deposit_missing_to_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Deposit",
            fromAccount: null,
            toAccount: null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.ToAccount)
            && e.ErrorMessage == "To account is required for a deposit.");
    }

    [Fact]
    public void Deposit_whitespace_to_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Deposit",
            fromAccount: null,
            toAccount: "   "));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.ToAccount)
            && e.ErrorMessage == "To account is required for a deposit.");
    }

    [Fact]
    public void Deposit_invalid_optional_from_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Deposit",
            fromAccount: "not-valid",
            toAccount: "ACC-10001"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTransactionCommand.FromAccount));
    }

    [Fact]
    public void Withdrawal_missing_from_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Withdrawal",
            fromAccount: null,
            toAccount: "ACC-10002"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.FromAccount)
            && e.ErrorMessage == "From account is required for a withdrawal.");
    }

    [Fact]
    public void Withdrawal_invalid_optional_to_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Withdrawal",
            fromAccount: "ACC-10001",
            toAccount: "xyz"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTransactionCommand.ToAccount));
    }

    [Fact]
    public void Transfer_missing_from_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Transfer",
            fromAccount: null,
            toAccount: "ACC-10002"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.FromAccount)
            && e.ErrorMessage == "From account is required for a transfer.");
    }

    [Fact]
    public void Transfer_missing_to_account_fails()
    {
        var result = _validator.Validate(Base(
            type: "Transfer",
            fromAccount: "ACC-10001",
            toAccount: null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.ToAccount)
            && e.ErrorMessage == "To account is required for a transfer.");
    }

    [Fact]
    public void Transfer_missing_both_accounts_reports_both_errors()
    {
        var result = _validator.Validate(Base(
            type: "Transfer",
            fromAccount: null,
            toAccount: null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.FromAccount)
            && e.ErrorMessage == "From account is required for a transfer.");
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateTransactionCommand.ToAccount)
            && e.ErrorMessage == "To account is required for a transfer.");
    }

    [Fact]
    public void Invalid_type_does_not_apply_transfer_account_requirements()
    {
        var result = _validator.Validate(Base(
            type: "Nope",
            fromAccount: null,
            toAccount: null));
        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == "From account is required for a transfer.");
        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == "To account is required for a transfer.");
    }
}

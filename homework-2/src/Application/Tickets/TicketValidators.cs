using Domain.Tickets;
using FluentValidation;

namespace Application.Tickets;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(command => command.CustomerName).NotEmpty();
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MinimumLength(10).MaximumLength(2000);
        RuleFor(command => command.Category).MustBeDefinedEnum();
        RuleFor(command => command.Priority).MustBeDefinedEnum();
        RuleFor(command => command.Status).MustBeDefinedEnum();
        RuleFor(command => command.Metadata).NotNull();
        When(command => command.Metadata is not null, () =>
        {
            RuleFor(command => command.Metadata!.Source).MustBeDefinedEnum();
            RuleFor(command => command.Metadata!.DeviceType).MustBeDefinedEnum();
        });
    }
}

public sealed class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.CustomerEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.CustomerEmail));
        RuleFor(command => command.CustomerName).NotEmpty().When(command => command.CustomerName is not null);
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(200).When(command => command.Subject is not null);
        RuleFor(command => command.Description).NotEmpty().MinimumLength(10).MaximumLength(2000).When(command => command.Description is not null);
        RuleFor(command => command.Category).MustBeDefinedEnum().When(command => command.Category.HasValue);
        RuleFor(command => command.Priority).MustBeDefinedEnum().When(command => command.Priority.HasValue);
        RuleFor(command => command.Status).MustBeDefinedEnum().When(command => command.Status.HasValue);
        When(command => command.Metadata is not null, () =>
        {
            RuleFor(command => command.Metadata!.Source).MustBeDefinedEnum();
            RuleFor(command => command.Metadata!.DeviceType).MustBeDefinedEnum();
        });
    }
}

public sealed class GetTicketQueryValidator : AbstractValidator<GetTicketQuery>
{
    public GetTicketQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

public sealed class ListTicketsQueryValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsQueryValidator()
    {
        RuleFor(query => query.Category).MustBeDefinedEnum().When(query => query.Category.HasValue);
        RuleFor(query => query.Priority).MustBeDefinedEnum().When(query => query.Priority.HasValue);
        RuleFor(query => query.Status).MustBeDefinedEnum().When(query => query.Status.HasValue);
    }
}

public sealed class DeleteTicketCommandValidator : AbstractValidator<DeleteTicketCommand>
{
    public DeleteTicketCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public sealed class AutoClassifyTicketCommandValidator : AbstractValidator<AutoClassifyTicketCommand>
{
    public AutoClassifyTicketCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

internal static class EnumValidationExtensions
{
    public static IRuleBuilderOptions<T, TEnum?> MustBeDefinedEnum<T, TEnum>(this IRuleBuilder<T, TEnum?> ruleBuilder)
        where TEnum : struct, Enum
    {
        return ruleBuilder.Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("{PropertyName} must be a defined value.");
    }
}

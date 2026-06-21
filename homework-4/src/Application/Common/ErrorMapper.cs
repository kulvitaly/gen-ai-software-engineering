using Domain.Tickets;
using FluentValidation.Results;

namespace Application.Common;

internal static class ErrorMapper
{
    public static IReadOnlyList<ApplicationError> FromValidationFailures(IEnumerable<ValidationFailure> failures)
    {
        return failures
            .Select(failure => new ApplicationError(failure.PropertyName, failure.ErrorMessage))
            .ToArray();
    }

    public static IReadOnlyList<ApplicationError> FromDomainErrors(IEnumerable<ValidationError> errors)
    {
        return errors
            .Select(error => new ApplicationError(error.Field, error.Message))
            .ToArray();
    }
}

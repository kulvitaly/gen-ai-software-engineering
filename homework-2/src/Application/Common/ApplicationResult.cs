namespace Application.Common;

public enum ApplicationResultStatus
{
    Success,
    ValidationError,
    NotFound
}

public sealed class ApplicationResult<T>
{
    private ApplicationResult(ApplicationResultStatus status, T? value, IReadOnlyList<ApplicationError> errors)
    {
        Status = status;
        Value = value;
        Errors = errors;
    }

    public ApplicationResultStatus Status { get; }

    public bool IsSuccess => Status == ApplicationResultStatus.Success;

    public T? Value { get; }

    public IReadOnlyList<ApplicationError> Errors { get; }

    public static ApplicationResult<T> Success(T value)
    {
        return new ApplicationResult<T>(ApplicationResultStatus.Success, value, []);
    }

    public static ApplicationResult<T> ValidationFailure(IReadOnlyList<ApplicationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new ApplicationResult<T>(ApplicationResultStatus.ValidationError, default, errors);
    }

    public static ApplicationResult<T> NotFound(string field, string message)
    {
        return new ApplicationResult<T>(ApplicationResultStatus.NotFound, default, [new ApplicationError(field, message)]);
    }
}

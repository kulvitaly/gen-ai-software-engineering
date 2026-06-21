namespace Domain.Tickets;

public sealed class ValidationResult<T>
{
    private ValidationResult(T? value, IReadOnlyList<ValidationError> errors)
    {
        Value = value;
        Errors = errors;
    }

    public bool IsValid => Errors.Count == 0;

    public T? Value { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public static ValidationResult<T> Success(T value)
    {
        return new ValidationResult<T>(value, []);
    }

    public static ValidationResult<T> Failure(IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new ValidationResult<T>(default, errors);
    }
}

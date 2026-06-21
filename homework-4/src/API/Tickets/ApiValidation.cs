using System.ComponentModel.DataAnnotations;
using Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API.Tickets;

internal static class ApiValidation
{
    public static bool TryValidate(object model, out Dictionary<string, string[]> errors)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);

        Validator.TryValidateObject(model, context, validationResults, validateAllProperties: true);
        ValidateNestedObjects(model, validationResults);

        errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty), (result, memberName) => new
            {
                Field = memberName,
                Message = result.ErrorMessage ?? "The value is invalid."
            })
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray());

        return errors.Count == 0;
    }

    public static ValidationProblem FromErrors(IReadOnlyList<ApplicationError> errors)
    {
        return TypedResults.ValidationProblem(errors
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray()));
    }

    private static void ValidateNestedObjects(object model, ICollection<ValidationResult> validationResults)
    {
        foreach (var property in model.GetType().GetProperties())
        {
            var value = property.GetValue(model);
            if (value is null || value is string || value.GetType().IsValueType)
            {
                continue;
            }

            var nestedResults = new List<ValidationResult>();
            var nestedContext = new ValidationContext(value);
            Validator.TryValidateObject(value, nestedContext, nestedResults, validateAllProperties: true);

            foreach (var result in nestedResults)
            {
                var memberNames = result.MemberNames.Select(memberName => $"{property.Name}.{memberName}").ToArray();
                validationResults.Add(new ValidationResult(result.ErrorMessage, memberNames));
            }
        }
    }
}

using FluentValidation.Results;
using ECommerceOrderProcessing.Shared.Models;

namespace ECommerceOrderProcessing.Shared.Validation;

public static class ValidationResultExtensions
{
    public static ServiceResponse<T> ToFailureResponse<T>(this ValidationResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
        return ServiceResponse<T>.Failure("VALIDATION_FAILED", errors);
    }
}

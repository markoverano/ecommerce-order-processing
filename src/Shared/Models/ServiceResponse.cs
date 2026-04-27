namespace ECommerceOrderProcessing.Shared.Models;

/// <summary>Uniform envelope returned by all command and query handlers at service boundaries.</summary>
public sealed record ServiceResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public ErrorResponse? Error { get; init; }

    private ServiceResponse() { }

    public static ServiceResponse<T> Success(T data) =>
        new() { IsSuccess = true, Data = data };

    public static ServiceResponse<T> Failure(string code, string message) =>
        new() { IsSuccess = false, Error = new ErrorResponse(code, message) };

    public static ServiceResponse<T> Failure(ErrorResponse error) =>
        new() { IsSuccess = false, Error = error };
}

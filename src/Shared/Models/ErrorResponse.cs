namespace ECommerceOrderProcessing.Shared.Models;

/// <summary>Structured error returned from service operations.</summary>
public sealed record ErrorResponse(string Code, string Message)
{
    public IReadOnlyList<string>? Details { get; init; }
}

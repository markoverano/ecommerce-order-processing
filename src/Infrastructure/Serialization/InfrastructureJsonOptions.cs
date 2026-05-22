using System.Text.Json;

namespace ECommerceOrderProcessing.Infrastructure.Serialization;

public static class InfrastructureJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

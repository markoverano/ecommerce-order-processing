FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/ShippingService/Domain/ShippingService.Domain.csproj", "src/ShippingService/Domain/"]
COPY ["src/ShippingService/Application/ShippingService.Application.csproj", "src/ShippingService/Application/"]
COPY ["src/ShippingService/Infrastructure/ShippingService.Infrastructure.csproj", "src/ShippingService/Infrastructure/"]
COPY ["src/ShippingService/Api/ShippingService.Api.csproj", "src/ShippingService/Api/"]
RUN dotnet restore "src/ShippingService/Api/ShippingService.Api.csproj"

COPY . .
RUN dotnet publish "src/ShippingService/Api/ShippingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShippingService.Api.dll"]

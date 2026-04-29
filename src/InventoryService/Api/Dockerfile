FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/InventoryService/Domain/InventoryService.Domain.csproj", "src/InventoryService/Domain/"]
COPY ["src/InventoryService/Application/InventoryService.Application.csproj", "src/InventoryService/Application/"]
COPY ["src/InventoryService/Infrastructure/InventoryService.Infrastructure.csproj", "src/InventoryService/Infrastructure/"]
COPY ["src/InventoryService/Api/InventoryService.Api.csproj", "src/InventoryService/Api/"]
RUN dotnet restore "src/InventoryService/Api/InventoryService.Api.csproj"

COPY . .
RUN dotnet publish "src/InventoryService/Api/InventoryService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]

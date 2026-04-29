FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/OrderService/Domain/OrderService.Domain.csproj", "src/OrderService/Domain/"]
COPY ["src/OrderService/Application/OrderService.Application.csproj", "src/OrderService/Application/"]
COPY ["src/OrderService/Infrastructure/OrderService.Infrastructure.csproj", "src/OrderService/Infrastructure/"]
COPY ["src/OrderService/Api/OrderService.Api.csproj", "src/OrderService/Api/"]
RUN dotnet restore "src/OrderService/Api/OrderService.Api.csproj"

COPY . .
RUN dotnet publish "src/OrderService/Api/OrderService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/AnalyticsService/Domain/AnalyticsService.Domain.csproj", "src/AnalyticsService/Domain/"]
COPY ["src/AnalyticsService/Application/AnalyticsService.Application.csproj", "src/AnalyticsService/Application/"]
COPY ["src/AnalyticsService/Infrastructure/AnalyticsService.Infrastructure.csproj", "src/AnalyticsService/Infrastructure/"]
COPY ["src/AnalyticsService/Api/AnalyticsService.Api.csproj", "src/AnalyticsService/Api/"]
RUN dotnet restore "src/AnalyticsService/Api/AnalyticsService.Api.csproj"

COPY . .
RUN dotnet publish "src/AnalyticsService/Api/AnalyticsService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AnalyticsService.Api.dll"]

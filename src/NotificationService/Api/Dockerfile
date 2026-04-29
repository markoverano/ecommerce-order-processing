FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/NotificationService/Domain/NotificationService.Domain.csproj", "src/NotificationService/Domain/"]
COPY ["src/NotificationService/Application/NotificationService.Application.csproj", "src/NotificationService/Application/"]
COPY ["src/NotificationService/Infrastructure/NotificationService.Infrastructure.csproj", "src/NotificationService/Infrastructure/"]
COPY ["src/NotificationService/Api/NotificationService.Api.csproj", "src/NotificationService/Api/"]
RUN dotnet restore "src/NotificationService/Api/NotificationService.Api.csproj"

COPY . .
RUN dotnet publish "src/NotificationService/Api/NotificationService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]

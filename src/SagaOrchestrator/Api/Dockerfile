FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/SagaOrchestrator/Domain/SagaOrchestrator.Domain.csproj", "src/SagaOrchestrator/Domain/"]
COPY ["src/SagaOrchestrator/Application/SagaOrchestrator.Application.csproj", "src/SagaOrchestrator/Application/"]
COPY ["src/SagaOrchestrator/Infrastructure/SagaOrchestrator.Infrastructure.csproj", "src/SagaOrchestrator/Infrastructure/"]
COPY ["src/SagaOrchestrator/Api/SagaOrchestrator.Api.csproj", "src/SagaOrchestrator/Api/"]
RUN dotnet restore "src/SagaOrchestrator/Api/SagaOrchestrator.Api.csproj"

COPY . .
RUN dotnet publish "src/SagaOrchestrator/Api/SagaOrchestrator.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SagaOrchestrator.Api.dll"]

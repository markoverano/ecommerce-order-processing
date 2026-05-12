FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/PaymentService/Api/PaymentService.Api.csproj", "src/PaymentService/Api/"]
COPY ["src/PaymentService/Application/PaymentService.Application.csproj", "src/PaymentService/Application/"]
COPY ["src/PaymentService/Domain/PaymentService.Domain.csproj", "src/PaymentService/Domain/"]
COPY ["src/PaymentService/Infrastructure/PaymentService.Infrastructure.csproj", "src/PaymentService/Infrastructure/"]
COPY ["src/Shared/ECommerceOrderProcessing.Shared.csproj", "src/Shared/"]
COPY ["src/Infrastructure/ECommerceOrderProcessing.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "src/PaymentService/Api/PaymentService.Api.csproj"

COPY . .
WORKDIR "/src/src/PaymentService/Api"
RUN dotnet publish "PaymentService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
HEALTHCHECK CMD curl -f http://localhost:8080/health/live || exit 1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]

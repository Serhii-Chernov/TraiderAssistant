FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TechnicalAnalysisWebApi/TechnicalAnalysis.WebApi.csproj", "TechnicalAnalysisWebApi/"]
COPY ["TechnicalAnalysis.Domain/TechnicalAnalysis.Domain.csproj", "TechnicalAnalysis.Domain/"]
COPY ["TechnicalAnalysis.Shared/TechnicalAnalysis.Shared.csproj", "TechnicalAnalysis.Shared/"]
COPY ["TraiderAssistant.Infrastructure/TraiderAssistant.Infrastructure.csproj", "TraiderAssistant.Infrastructure/"]
RUN dotnet restore "TechnicalAnalysisWebApi/TechnicalAnalysis.WebApi.csproj"

COPY . .
WORKDIR "/src/TechnicalAnalysisWebApi"
RUN dotnet publish "TechnicalAnalysis.WebApi.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TechnicalAnalysis.WebApi.dll"]

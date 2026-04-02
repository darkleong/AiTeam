# AiTeam.Dashboard Dockerfile
# Build context 必須為 repo root（docker build -f src/AiTeam.Dashboard/Dockerfile .）

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/AiTeam.Dashboard/AiTeam.Dashboard.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Blazor Server 預設監聽 8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiTeam.Dashboard.dll"]

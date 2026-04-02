# AiTeam.Bot Dockerfile
# Build context 必須為 repo root（docker build -f src/AiTeam.Bot/Dockerfile .）
# 因為各專案之間有 project reference，需從 root 複製整個 src 目錄

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/AiTeam.Bot/AiTeam.Bot.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# LibGit2Sharp 需要 libgit2 native library
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgit2-1.8 \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "AiTeam.Bot.dll"]

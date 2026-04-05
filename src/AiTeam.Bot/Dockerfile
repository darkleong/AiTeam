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

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# 安裝：Npgsql GSSAPI 支援 + Node.js 22（Claude Code CLI 需要）+ git（Claude Code 操作 repo 需要）
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    curl \
    git \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g @anthropic-ai/claude-code \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "AiTeam.Bot.dll"]

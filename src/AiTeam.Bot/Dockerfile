# AiTeam.Bot Dockerfile
# Build context 必須為 repo root（docker build -f src/AiTeam.Bot/Dockerfile .）
# 因為各專案之間有 project reference，需從 root 複製整個 src 目錄

# ── Stage 1：編譯 ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AiTeam.Bot/AiTeam.Bot.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

# ── Stage 2：安裝 Claude Code（npm）────────────────────────────────────────────
# 使用官方 node:22 image 安裝套件，避免在 runtime stage 執行 apt-get + NodeSource
# （NodeSource apt 安裝在 GitHub Actions runner 上極慢，改由 node image 提供）
FROM node:22-slim AS claude-installer
RUN npm install -g @anthropic-ai/claude-code \
    && npm cache clean --force

# ── Stage 3：Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

# 安裝 libgssapi（Npgsql GSSAPI 支援）+ git（Claude Code 操作 repo 需要）+ gosu（Stage 65 子項 3：compose entrypoint chown 後 drop privilege 到 appuser）
# 不再需要安裝 Node.js / npm（由下方 COPY 直接帶入）
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    git \
    gosu \
    && rm -rf /var/lib/apt/lists/*

# 從 node:22-slim 複製 Node binary 與 claude-code，避免 apt NodeSource 緩慢問題
COPY --from=node:22-slim /usr/local/bin/node  /usr/local/bin/node
COPY --from=node:22-slim /usr/local/lib       /usr/local/lib
COPY --from=claude-installer /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=claude-installer /usr/local/bin/claude       /usr/local/bin/claude

# 建立非 root 使用者（Claude Code 拒絕在 root 下執行 --dangerously-skip-permissions）
RUN groupadd -r appuser && useradd -r -g appuser -m appuser

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "AiTeam.Bot.dll"]

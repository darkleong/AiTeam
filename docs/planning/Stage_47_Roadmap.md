# Stage 47：FF 四十七 Token limit SoT 統一 + CI/CD 部署可靠性（路線 b：DB AppSettings 動態化）+ 順帶完成 FF 十一

> 對應 Future Feature：FF 四十七（Token SoT + CI/CD ops 補丁）+ 順帶 FF 十一（Dashboard 可調整 Token 守門全域限額）
> 對應版本：v3.34.0（預計）
> 建立日期：2026-05-02
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：FF 四十七 — 把 Token limit 從「docker-compose env / appsettings.json 雙 SoT」收斂到「DB AppSettings + agent_configs 動態化 + Dashboard UI 即時改」。

**Trial_v5 揭露的根因**（commit `f7e476b` / `df3bfb7` 的踩坑紀錄）：
- 議題 C（Token SoT 分歧）：改 appsettings.json 完全沒生效，因為 `docker-compose.prod.yml` env 寫死 `MonthlyTokenLimitK: 2000` 完全 override（.NET Configuration 順序 env > appsettings → 改 appsettings 但 env 沒對齊 = 靜默無效）
- 議題 D（CI/CD 部署不重啟容器）：手動 `docker restart` 不 reload env，要 `docker compose up --force-recreate` 才 trigger recreate

**Christ 拍板**（2026-05-02 brainstorm）：採**路線 (b) DB AppSettings 動態化**（非 (a) 純 appsettings 統一），順帶把 FF 十一（Dashboard 可調 Token 全域限額）大半解掉。

**戰略意義**：
- 解 Trial 期間頻繁調 token limit 痛點（Trial_v5 commit `f7e476b` 即典型場景）
- 把「ops 改 limit」流程從「commit + push + 等 CI/CD」降為「Dashboard 點按鈕即時生效」
- 為未來 Dashboard 控制中心願景累積基礎建設（AppSettings 表 + AgentConfig 動態化）
- v4 落地前先補 ops 坑（v4 部署也會踩同類問題）

**搭車**：FF 十一（Dashboard 可調 Token 守門全域限額）— 路線 (b) 自然吸收，不需獨立 Stage。

---

## 設計決策（Christ 2026-05-02 拍板）

### 主路線拍板

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **Token limit SoT 路線** | **(b) DB AppSettings 動態化** — 全域 limit 走 AppSettings 表 + per-agent limit 走 agent_configs 表，Dashboard UI 即時改 | (a) 純 appsettings.json 統一（保守快路徑，但不解 FF 十一）|
| **fallback 機制** | **DB 沒寫入時 fallback appsettings.json 預設值** — 系統可在「DB 設定全清空」時退回 image 內 appsettings 安全運作 | 無 fallback（DB 必填，初始化邏輯複雜）|
| **per-agent limit 是否動態化** | **是** — agent_configs 加 DailyTokenLimitK / MonthlyTokenLimitK 欄位（呼應 Stage 38 Provider/Model 動態化模式）| 只動全域 limit，per-agent 留 appsettings（FF 十一只解一半）|
| **既有 Stage 22 守門邏輯** | **保留 4 個 Check（單次 / agent 日 / agent 月 / 全域月）**，只改數值來源 | 重構守門邏輯（scope 爆炸）|

### 子項 B（CI/CD 部署可靠性）重新評估

**🟢 探索期發現**：`.github/workflows/deploy.yml:102` 已用 `docker compose up -d --force-recreate`，**FF 四十七子項 B 第二項實際已做**。

→ Trial_v5 議題 D 的根因不是 CI/CD workflow 缺陷，是「Christ 手動 `docker restart` 不會 reload env」的認知差。

→ **子項 B 只需做文件補強**（CLAUDE.md 加「ops 配置改動 SoP」段），不需動 .github/workflows。

### Stage 47 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **1** | DB schema：agent_configs 加 DailyTokenLimitK / MonthlyTokenLimitK + Migration | S |
| **2** | AppSettingsService 擴充：GetIntAsync(key, fallback) helper | XS |
| **3** | TokenTrackingProvider 改寫：4 個 Check 改讀 AppSettings + AgentConfig | M |
| **4** | DbSeeder 補 seed：首次啟動從 appsettings.json 把 per-agent limit 寫進 agent_configs | S |
| **5** | Dashboard SystemSettings.razor 加 Token 守門設定區塊（全域月限 / 單次上限）| S |
| **6** | Dashboard AgentConfig 編輯頁加 Daily / Monthly Token Limit 欄位 | S |
| **7** | docker-compose.prod.yml 移除所有 AgentSettings__*Token* env（共 26 個） | XS |
| **8** | CLAUDE.md 加「ops 配置改動 SoP」段（子項 B 文件補強） | XS |

**總工時估**：1 週左右（5-7 個工作日）

---

## 子項 1：DB schema — agent_configs 加 Token Limit 欄位

### 實作項目

**位置**：`src/AiTeam.Data/Entities.cs` `AgentConfig` class

**新增欄位**：

```csharp
/// <summary>Stage 47：Agent 日 Token 上限（千 token）。null = 啟動時從 appsettings.json AgentSettings:Agents:{Name}:DailyTokenLimitK 補 seed。</summary>
public int? DailyTokenLimitK { get; set; }

/// <summary>Stage 47：Agent 月 Token 上限（千 token）。null = 啟動時從 appsettings.json AgentSettings:Agents:{Name}:MonthlyTokenLimitK 補 seed。</summary>
public int? MonthlyTokenLimitK { get; set; }
```

**Migration `Stage47AgentConfigTokenLimits`**：
```bash
dotnet ef migrations add Stage47AgentConfigTokenLimits \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

**設計理由**：對齊 Stage 38 `Provider` / `Model` 欄位的 nullable + DbSeeder 補 seed 模式，向後相容。

---

## 子項 2：AppSettingsService 擴充

### 實作項目

**位置**：`src/AiTeam.Bot/Services/AppSettingsService.cs`

**新增 helper**：

```csharp
/// <summary>取得 int 設定值，找不到 / 解析失敗時回傳 fallback。</summary>
public async Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default)
{
    var value = await GetAsync(key, cancellationToken);
    return int.TryParse(value, out var result) ? result : fallback;
}
```

**Dashboard 端 `DashboardAppSettingsService` 不需要 GetIntAsync**（Dashboard UI 用 `AppSetting?.Value` + 自行 parse）。

---

## 子項 3：TokenTrackingProvider 改寫（核心）

### 實作項目

**位置**：`src/AiTeam.Bot/Agents/TokenTrackingProvider.cs`

**注入新依賴**：

```csharp
public class TokenTrackingProvider(
    ILlmProvider inner,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    DiscordAlertService discordAlert,
    BotAgentSettings agentSettings,    // 保留作 fallback
    BotAgentConfig agentConfig,         // 保留作 fallback
    AppSettingsService appSettings,     // ★ 新增
    AgentConfigCache agentConfigCache,  // ★ 新增（讀動態 per-agent limit）
    ILogger<TokenTrackingProvider> logger,
    string agentName,
    string model) : ILlmProvider
```

**4 個 Check 改寫策略**：

| Check | 數值來源（依優先順序）| Fallback 順序 |
|---|---|---|
| **Check 1** 單次請求上限 | `AppSettings:Token:SingleRequestLimitK` → `agentSettings.SingleRequestTokenLimitK` | DB → appsettings.json |
| **Check 2** Agent 日限 | `agentConfigCache.Get(agentName).DailyTokenLimitK` → `agentConfig.DailyTokenLimitK` | DB → appsettings.json |
| **Check 3** Agent 月限 | `agentConfigCache.Get(agentName).MonthlyTokenLimitK` → `agentConfig.MonthlyTokenLimitK` | DB → appsettings.json |
| **Check 4** 全域月限 | `AppSettings:Token:GlobalMonthlyLimitK` → `agentSettings.MonthlyTokenLimitK` | DB → appsettings.json |

**Check 4 提示訊息更新**（FF 十一 解掉的核心訴求）：

```csharp
// 舊：「請修改 AgentSettings:MonthlyTokenLimitK 並重啟 Bot 後恢復。」
// 新：「請至 Dashboard【系統設定 → Token 守門設定】調整全域月限，5 分鐘內自動生效。」
```

**注意事項**：
- 每次 `CompleteAsync` 多 4 次 `AppSettingsService.GetIntAsync`（cache 5 min TTL，第一次後幾乎零成本）
- AgentConfigCache 已有 cache（Stage 38 起），讀 per-agent limit 不需新增 cache
- Cache 失效機制延用既有：寫 AppSettings 後 5 min 內生效；Dashboard「套用變更」按鈕可立即 reload

---

## 子項 4：DbSeeder 補 seed 邏輯

### 實作項目

**位置**：`src/AiTeam.Data/DbSeeder.cs`

**邏輯**：首次啟動時，若 `agent_configs.{DailyTokenLimitK, MonthlyTokenLimitK}` 為 null，從 `appsettings.json` 的 `AgentSettings:Agents:{Name}` 對應值寫入。

**注意**：
- **Idempotent**：seed 只填 null 欄位，不覆蓋使用者在 Dashboard 改過的值（呼應 Stage 38 Provider/Model seed 邏輯）
- 對齊 Stage 38 `SeedAgentConfigsAsync` 既有模式
- 若 appsettings.json 內找不到對應 Agent 的設定（例如新加 Agent），則保持 null（runtime fallback 走 BotAgentConfig 預設值）

---

## 子項 5：Dashboard SystemSettings 加 Token 守門設定區塊

### 實作項目

**位置**：
- `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor`
- `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor.cs`

**新增區塊「Token 守門設定」**（在「Mock Mode」下方）：

| 欄位 | AppSettings Key | 預設 fallback |
|---|---|---|
| 全域月費上限（千 token） | `Token:GlobalMonthlyLimitK` | 讀 appsettings.json `AgentSettings:MonthlyTokenLimitK` |
| 單次請求上限（千 token） | `Token:SingleRequestLimitK` | 讀 appsettings.json `AgentSettings:SingleRequestTokenLimitK` |

**UI 風格**：對齊既有 SystemSettings 卡片風格（system-config-card class），含「Save」按鈕 + 「5 分鐘內自動生效」提示。

**注意**：
- per-agent limit 不放 SystemSettings 頁（量太多會擠爆），放 AgentConfig 編輯頁（子項 6）
- 加「快速套用變更」按鈕呼叫既有 `BotService.ReloadCacheAsync("all")` — 可即時生效（不等 5 min TTL）

---

## 子項 6：Dashboard AgentConfig 編輯頁擴充

### 實作項目

**位置**：`src/AiTeam.Dashboard/Components/Pages/Settings/AgentSettings.razor` (+ .cs)

**在現有 Provider/Model 欄位旁新增**：
- Daily Token Limit（千 token）— 數字輸入，預設 fallback 顯示
- Monthly Token Limit（千 token）— 數字輸入，預設 fallback 顯示

**儲存邏輯**：寫入 `agent_configs.{DailyTokenLimitK, MonthlyTokenLimitK}` → `AgentConfigCache.Invalidate()` 觸發下次讀取重載。

**注意**：表格 / 列表式 UI 對齊既有風格（沿用 MudBlazor 樣式）。

---

## 子項 7：docker-compose.prod.yml 移除 env

### 實作項目

**位置**：`docker-compose.prod.yml`

**移除以下 env**（共 26 個）：

**aiteam-bot service**（2 個）：
```yaml
AgentSettings__MonthlyTokenLimitK: "20000"
AgentSettings__SingleRequestTokenLimitK: "200"
```

**aiteam-dashboard service**（24 個）：
```yaml
AgentSettings__MonthlyTokenLimitK: "20000"
AgentSettings__SingleRequestTokenLimitK: "200"
AgentSettings__Agents__CEO__DailyTokenLimitK: "1000"
AgentSettings__Agents__CEO__MonthlyTokenLimitK: "5000"
# ...（11 個 Agents × Daily + Monthly = 22 個）
```

**保留 env**（與 Token 無關）：
- `AgentSettings__RulesCacheTtlMinutes`
- `AgentSettings__DailyReportCron`
- `AgentSettings__InternalApiKey`
- `AgentSettings__SkipCeoConfirm`

**注意**：
- appsettings.json 的對應預設值**不刪**（fallback 安全網）
- 移除 env 後系統行為應與「DB AppSettings 為空時 fallback appsettings.json」等價

---

## 子項 8：CLAUDE.md 加「ops 配置改動 SoP」段（子項 B 文件補強）

### 實作項目

**位置**：`CLAUDE.md` 新增段落（建議在「部署環境」段下方）

**內容範例**：

```markdown
---

## ops 配置改動 SoP（Stage 47 起）

修改 Token / 系統設定 / docker-compose 配置時，依下列分類選對的方式：

### Token limit / 系統設定（5 分鐘內生效，不重啟）

→ **走 Dashboard**：系統設定頁 / Agent 設定頁
- 全域月限 / 單次請求上限 → 系統設定 → Token 守門設定
- per-agent 日限 / 月限 → Agent 設定 → 編輯該 Agent
- 修改後 AppSettings cache 5 分鐘 TTL，或點「套用變更」立即 reload

### docker-compose.prod.yml / appsettings.json 預設值（push 觸發 CI/CD）

→ **commit + push 到 main**：GitHub Actions self-hosted runner 自動 `docker compose up -d --force-recreate`
- ⚠️ **不要單獨 `docker restart aiteam-bot`** — restart 不 reload env，必須 recreate
- 若需要不 push 直接 recreate，手動執行：`docker compose -f docker-compose.prod.yml up -d --force-recreate`

### 配置改動 SoT 確認（避免 Stage 47 前的踩坑）

修改任何 token limit 前，先確認 SoT：
1. **Token limit**：DB AppSettings + agent_configs 為 SoT，appsettings.json 為 fallback
2. **其他 AgentSettings__*** env**：仍以 docker-compose.prod.yml env 為 SoT（如 `RulesCacheTtlMinutes`）
3. **Discord / GitHub / DB 連線**：docker-compose.prod.yml env 為 SoT（含敏感資訊）
```

---

## 驗收情境

> Stage 47 主軸是 ops 配置 / DB / UI，**無新 Mock 場景需求**（Mock 流程不動）。驗收以 Dashboard UI 操作 + DB 查詢 + 系統行為驗證為主。

### 場景 A：Dashboard 改全域月限即時生效

1. 部署 Stage 47 build → 先觀察 token_logs 累積到接近月限
2. 在 Dashboard 系統設定 → Token 守門設定 → 全域月限改大
3. 點「套用變更」按鈕（或等 5 分鐘）
4. 觸發任意 LLM 呼叫（例：Discord `/status` 或 Mock `/mock new_feature`）
5. **驗證**：
   - Bot log 不再出現「全域月限 攔截」
   - DB `app_settings` 表新增 `Token:GlobalMonthlyLimitK` row
   - 不需重啟容器

### 場景 B：Dashboard 改 per-agent 日限即時生效

1. 在 Dashboard Agent 設定頁編輯 Cody，改 Daily Token Limit
2. 儲存 → 點「套用變更」
3. 觸發 Cody 任務（例：Mock 流程跑 Dev 階段）
4. **驗證**：
   - DB `agent_configs.DailyTokenLimitK` 該 row 更新
   - Bot log 守門 Check 2 用新數值（從 log 看 `上限=新值`）
   - AgentConfigCache 自動 invalidate

### 場景 C：DB AppSettings 為空時 fallback appsettings.json

1. 清空 `app_settings` 表中 `Token:*` 相關 row（直接 SQL）
2. 重啟容器（觸發 cache 重載）
3. 觀察 Bot 啟動 log
4. **驗證**：
   - 守門 Check 1 / Check 4 用 appsettings.json 的 `MonthlyTokenLimitK` / `SingleRequestTokenLimitK`（fallback 生效）
   - 系統正常運作不報錯

### 場景 D：移除 docker-compose env 後系統正常

1. push Stage 47 commit → CI/CD 觸發 `--force-recreate`
2. 觀察兩個容器啟動 log（aiteam-bot + aiteam-dashboard）
3. 觀察 Token 監控頁面數值是否正確
4. **驗證**：
   - 啟動無錯誤（沒有 IOptions 綁定失敗）
   - Token 監控頁顯示的 limit 從 DB / appsettings 讀取（非 env override 的舊值）
   - DbSeeder 首次跑後 agent_configs 表 12 個 Agent 的 Daily/MonthlyTokenLimitK 都填值

### 場景 E：全域月限超限時 Discord 提示訊息正確

1. 將 `AppSettings:Token:GlobalMonthlyLimitK` 設為極小值（例如 1）
2. 觸發任意 LLM 呼叫
3. **驗證**：
   - Bot 守門攔截 Check 4
   - Discord 警報訊息包含「請至 Dashboard【系統設定 → Token 守門設定】調整」
   - 不再出現舊訊息「請修改 AgentSettings:MonthlyTokenLimitK 並重啟 Bot」

---

## 風險點 / 注意事項

### 1. TokenTrackingProvider DI 變更（中風險）

新增 `AppSettingsService` + `AgentConfigCache` 注入。兩者都是 Singleton（依 Stage 38 / Stage 32 既有設計），與既有 Singleton TokenTrackingProvider 配對 OK。需驗證 Bot Program.cs 的 `LlmProviderFactory.Create()` 內部建構 TokenTrackingProvider 時 DI 路徑能拿到這兩個服務。

### 2. Cache TTL vs 即時性 trade-off

AppSettings cache 5 min TTL — 老闆改 limit 後最壞要等 5 分鐘生效。提供「套用變更」按鈕（既有 ReloadCacheAsync）作為立即生效機制。

### 3. Migration 向後相容

`agent_configs` 加 nullable 欄位，既有資料 null 安全。DbSeeder 首次啟動補 seed 不破壞既有運作。

### 4. fallback 路徑驗證（場景 C 重要性）

如果 DB / cache 整體掛掉但 appsettings.json fallback 沒做對 → 守門可能完全失效（極危險，會耗光 token）。**驗收必須包含場景 C 完整驗證**。

### 5. Dashboard UI 風格對齊

新加區塊與現有 SystemSettings 風格不一致會違和。沿用 `system-config-card` class + 既有按鈕風格。需 Playwright 截圖驗收。

### 6. 不踩 production code / Agent prompt（Aria 自省點 #21）

本 Stage 涉及：
- ✅ **DB schema + Migration**（資料層）
- ✅ **Service 層改寫**（TokenTrackingProvider / AppSettingsService / DbSeeder）
- ✅ **Dashboard UI**（純 razor + razor.cs）
- ✅ **docker-compose / CLAUDE.md**（ops + 文件）
- ❌ **不動**：Agent prompt（CLAUDE_*.md） / WorkflowEngine 業務邏輯 / pipeline 流程

---

## 工時估 / Model 建議

### 工時估

| 子項 | 工時（理想）|
|---|---|
| 1 + 4（DB + Migration + Seed）| 0.5 天 |
| 2 + 3（Service 改寫 + Provider）| 1.5 天 |
| 5 + 6（Dashboard UI 兩處）| 2 天 |
| 7 + 8（docker-compose env 移除 + CLAUDE.md）| 0.5 天 |
| 驗收（5 場景）+ 修補 | 1 天 |
| **總計** | **5.5 天**（單純 Forge），最壞 7 天（含驗收期 follow-up）|

### Model / Effort 建議（Aria 待 brief Forge 時提供）

依 `workflow_aria_model_effort.md` 四維度評估：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 中 — 涉及 DI 多處 + Migration + 跨 Bot/Dashboard，但無深度演算法/架構決策 |
| **改動範圍** | M — 6-8 檔 production code + 1 Migration + 2 razor + docker-compose |
| **歷史包袱** | 中 — 需理解 Stage 22 守門 + Stage 38 AgentConfig 動態化 + Stage 32 AppSettingsService |
| **判斷品質要求** | 中 — fallback 邊界 + Cache invalidation 時序需細心 |

**建議**：**Sonnet 200K + medium**（保守選 high）— 任務範圍清楚有範本可抄（Stage 38 = AgentConfig 動態化前例），不需 ultrathink。

### Context 預估

依 ×1.6 公式（`workflow_aria_model_effort.md` 校準錨）：
- Plan Mode 預估 ~80K
- 實作期 ~150K
- 驗收期 ~30K
- **總計約 ~260K**（Sonnet 200K 邊界，需要時可升級 Opus 1M）

---

## 與 v4 路線的關係

**獨立性**：FF 四十七 v4 兼容性段已明寫「與 framework 無關，v4 落地後仍可獨立做（甚至應該優先做，因為 v4 部署也會踩同類坑）」。

**順序確認**：
1. **Stage 47**（本 FF，1 週）— ops 補丁先
2. **Stage 48** = FF 四十九 spike（MS Agent Framework Phase A，2-3 週）
3. spike 結論驅動 Stage 49+：正向 → 漸進遷移 / 負向 → 維持手刻 + 補丁

**FF 十一 後續**：Stage 47 順帶完成 FF 十一大半（Dashboard 可調 Token 守門全域限額），剩餘小尾巴（如「Agent 角色設定 Dashboard 化」FF 十的子項）留 v4 spike 後重評估。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）|

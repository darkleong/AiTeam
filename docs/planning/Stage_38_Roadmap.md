# Stage 38：Dashboard Provider/Model 動態化（FF 四第二階段 2-A）

> 對應 Future Feature：四第二階段 2-A（Dashboard Provider/Model 動態化）
> 對應版本：v3.25.0
> 建立日期：2026-04-25
> 狀態：🟢 實作完成，等驗收
> 文件版本：v1.3

---

## 概述

**主題**：把 Agent 的 `Provider` / `Model` 設定從 **`appsettings.json` 單向下讀** 升級為 **「DB 為主 + Dashboard UI 可改 + appsettings 作啟動 seed」** 三位一體模式。

**動機**：
- Stage 37-1 原計劃書列了第 4 項「Dashboard Agent 設定頁 Provider 下拉」，實作 Session 探索後發現 `AgentConfig` entity 沒有 `Provider` / `Model` 欄位（只在 appsettings 有），砍掉此項移至第二階段——這就是本 Stage 的核心工作。
- Stage 37-2 驗收期間 **PR #107 self-implement 試驗**嘗試做此功能，Cody 選了「Dashboard 自己讀一份 appsettings」的繞道方案，導致「Bot 實際用 vs Dashboard UI 顯示」會分裂——已 close，FF 四第二階段明寫此路線禁止。
- Rena 切 Gemini 作為 Stage 37-1 實測驗收時，Christ 只能編輯 `docker-compose.prod.yml` 的 env var 並重啟——「只動嘴的老闆」的 UX 痛點。

**實作方向**：**Single Source of Truth = DB**。
- `AgentConfig` entity 加 `Provider` / `Model` 欄位（nullable）
- Bot 啟動 seed：DB 欄位為 null 時從 `appsettings.json` 補回
- `LlmProviderFactory` 改讀 DB（透過既有服務或新 service），appsettings 只作 seed 來源
- Dashboard `AgentSettings.razor` 加 Provider 下拉 + Model 輸入框

---

## ⚠️ PR #107 教訓：禁止的設計路線

本 Stage 實作時**絕對禁止**以下三種繞道方案（PR #107 已驗證會造成架構分裂）：

| ❌ 禁止 | 為什麼錯 |
|--------|---------|
| Dashboard UI 自己讀一份 `appsettings.json`（或寫獨立 DB 欄位） | Bot 實際用 vs Dashboard 顯示會分裂、互不同步 |
| 不做 EF Migration，只在 service layer 加 cache | 沒有持久化、重啟後設定消失 |
| DB + appsettings 兩個來源並行（Bot 讀兩邊 merge） | 誰贏誰輸邏輯不透明、debug 噩夢 |

**必須的設計原則**：
- ✅ **DB 是唯一 source of truth**（runtime 只讀 DB）
- ✅ **appsettings.json 只在「Bot 啟動時 + DB 欄位為 null」時作 seed 值**
- ✅ **修改只能透過 Dashboard UI**（修改 appsettings 不會覆蓋 DB 已有值）

---

## 現況

### appsettings.json 結構

```json
"Agents": {
  "CEO": { "Provider": "Anthropic", "Model": "claude-sonnet-4-6", "DailyTokenLimitK": ... },
  "Dev": { "Provider": "Anthropic", "Model": "claude-sonnet-4-6", ... },
  "Release": { "Provider": "Gemini", "Model": "gemini-2.5-flash", ... }
  // ... 其他 Agent
}
```

### 兩個同名的 `AgentConfig` 類（實作時易混淆）

| 類別 | namespace | 用途 |
|------|-----------|------|
| `AiTeam.Bot.Configuration.AgentConfig` | Configuration | `IOptions<AgentSettings>` binding 用，從 appsettings.json 讀 |
| `AiTeam.Data.AgentConfig` | Data（Entity） | DB entity，目前**沒有** `Provider` / `Model` 欄位 |

[`LlmProviderFactory.cs:6-7`](../../src/AiTeam.Bot/Agents/LlmProviderFactory.cs:6) 用 alias 區分：
```csharp
using BotAgentSettings = AiTeam.Bot.Configuration.AgentSettings;
using BotAgentConfig   = AiTeam.Bot.Configuration.AgentConfig;
```

**本 Stage 要讓 DB entity 成為權威來源**，Configuration binding 降級為 seed 資料源。

### `LlmProviderFactory.Create()` 現況

```csharp
if (!_settings.Agents.TryGetValue(agentName, out var config))
    throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

var inner = config.Provider.ToUpperInvariant() switch { ... };
```

`_settings.Agents` 是 `Dictionary<string, BotAgentConfig>`，從 `IOptions<AgentSettings>` 取得。本 Stage 改為「從 DB 查詢 → 找不到才 fallback Configuration」。

### Dashboard AgentSettings.razor 現況

只有 `IsActive` 切換 + 信任等級（`TrustLevel`）滑桿、沒有 Provider / Model 欄位。

---

## 實作項目（7 件）

### 1. `AgentConfig` entity 加欄位 + EF Migration

[`src/AiTeam.Data/Entities.cs`](../../src/AiTeam.Data/Entities.cs) 的 `AgentConfig`（L30）加：

```csharp
/// <summary>Stage 38：LLM Provider（"Anthropic" / "Gemini"）。null = 啟動時從 appsettings.json 補 seed。</summary>
public string? Provider { get; set; }

/// <summary>Stage 38：Model 名稱（如 "claude-sonnet-4-6" / "gemini-2.5-flash"）。null = 啟動時從 appsettings.json 補 seed。</summary>
public string? Model { get; set; }
```

**Migration**：`Stage38AgentConfigProviderModel`（`AddColumn` 兩個 nullable 欄位，既有 row 預設 null）。

### 2. Bot 啟動 seed 邏輯

位置建議：`Program.cs:174` `MigrateAsync` 之後，加一個新的 seed step（或用 `IHostedService` 執行）：

對每個 `appsettings.json Agents:*` 條目：
- 找 DB `AgentConfig` WHERE `Name = agentName`（已存在 entity 是 Stage 5 建立的動態 Agent 清單）
- 若 `Provider` is null → 從 Configuration 補
- 若 `Model` is null → 從 Configuration 補
- `SaveChanges`

**seed 時機**：每次 Bot 啟動都跑一次，但只對 null 欄位補值（已有值不覆蓋）。這確保：
- 新 Agent 加入 appsettings.json → 重啟自動 seed
- Dashboard 已改過的 Agent → 啟動不覆蓋 Dashboard 設定
- 既有部署升級 → Provider/Model 自動從 appsettings 填入 DB

### 3. `LlmProviderFactory.Create()` 改讀 DB

**核心改動**：從 `_settings.Agents.TryGetValue(agentName, ...)` 改為「查 DB AgentConfig」：

```csharp
// 前（現況）
if (!_settings.Agents.TryGetValue(agentName, out var config))
    throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

var inner = config.Provider.ToUpperInvariant() switch { ... };

// 後（Stage 38）
var dbConfig = await agentConfigService.GetAsync(agentName, cancellationToken);
if (dbConfig is null)
    throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

// Provider/Model 取 DB，若為 null fallback Configuration（邊界保護，seed 應該已補）
var provider = dbConfig.Provider ?? _settings.Agents.GetValueOrDefault(agentName)?.Provider
    ?? throw new InvalidOperationException($"Agent {agentName} 無 Provider 設定");
var model    = dbConfig.Model    ?? _settings.Agents.GetValueOrDefault(agentName)?.Model
    ?? throw new InvalidOperationException($"Agent {agentName} 無 Model 設定");

var inner = provider.ToUpperInvariant() switch { ... };
```

**設計細節**（實作 Session 決定）：
- `Create()` 目前是**同步**（`public ILlmProvider Create(string agentName)`），改讀 DB 需要 async 或 block-on-async
- 建議改成 `CreateAsync(string agentName)` + 更新所有 caller
- 或者用 `AgentConfigService` 在啟動時一次載入所有 AgentConfig 到 in-memory cache（TTL），`Create()` 同步讀 cache
- 後者更符合「每個 Create 都打 DB」會過載的考量，對齊既有 `AppSettingsService` 1hr TTL cache pattern

**cache 失效策略**：Dashboard 改完存檔後要讓 Bot 端的 cache 失效，方式：
- Dashboard 呼叫 Bot 的 internal API（對齊 Stage 29 系統設定 cache reload 模式）
- 或 SignalR 通知

### 4. 新增 `AgentConfigService`（若採 cache 方案）

位置：`src/AiTeam.Bot/Services/AgentConfigService.cs`

職責：
- `GetAsync(string agentName)` — DB 查詢 + cache
- `InvalidateCache()` — 讓 cache 失效（Dashboard 改完後呼叫）
- `SeedFromConfigurationAsync(...)` — 啟動時 seed 邏輯（或分開放 Program.cs）

參考 pattern：[`AppSettingsService`](../../src/AiTeam.Bot/Services/AppSettingsService.cs)（1hr TTL cache + explicit invalidate）。

### 5. 新增 `LlmModels.cs` 常數檔（下拉清單資料源）

位置：`src/AiTeam.Shared/Constants/LlmModels.cs`（Shared 專案，讓 Dashboard + Bot 兩邊都能引用）

初始清單（2026-04-25 查到的主流版本，實作 Session 可用 WebFetch 官網確認後微調）：

```csharp
namespace AiTeam.Shared.Constants;

public static class LlmModels
{
    public const string ProviderAnthropic = "Anthropic";
    public const string ProviderGemini    = "Gemini";

    /// <summary>可用 Provider 清單（Dashboard 下拉用）。</summary>
    public static readonly IReadOnlyList<string> AvailableProviders =
        [ProviderAnthropic, ProviderGemini];

    /// <summary>依 Provider 返回對應的 Model 清單（Dashboard Model 下拉依 Provider 動態切換）。</summary>
    public static IReadOnlyList<string> GetModelsForProvider(string provider) =>
        provider switch
        {
            ProviderAnthropic => AnthropicModels,
            ProviderGemini    => GeminiModels,
            _                 => []
        };

    public static readonly IReadOnlyList<string> AnthropicModels =
    [
        "claude-opus-4-7",
        "claude-sonnet-4-6",
        "claude-haiku-4-5",
    ];

    // ⚠️ 時效（2026-04-25 Aria WebFetch 確認）：
    // Gemini 2.5 系列 Google 官方將於 2026-06-17 deprecated，屆時需評估遷移到
    // Gemini 3 stable GA 版本（當前 Gemini 3 仍為 -preview，不建議 production）。
    // 這個事件是 FF 二十六 的第一個實際升級 trigger。
    public static readonly IReadOnlyList<string> GeminiModels =
    [
        "gemini-2.5-pro",
        "gemini-2.5-flash",
    ];
}
```

**維護慣例**：
- 新 model 上線時由 Aria（或 Christ 授權下） WebFetch 驗證官網 → 加字串 → commit + push → CI/CD 部署（5-10 分鐘內生效）
- 清單按新到舊排列，預設顯示第一個作為 UX hint
- **已知遷移時點**：2026-06-17 Gemini 2.5 deprecating → 屆時要評估 Gemini 3 GA 狀態並遷移
- 未來需要更頻繁的動態更新 → 升級到 DB 化（見 [FF 二十六](../planning/Future_Feature.md)）

### 6. Dashboard `AgentSettings.razor` 加 UI

[`src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor`](../../src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor)：

每個 Agent 卡片加：
- **Provider 下拉**（`MudSelect<string>`）：來源 `LlmModels.AvailableProviders`
- **Model 下拉**（`MudSelect<string>`）：來源 `LlmModels.GetModelsForProvider(currentProvider)`，**Provider 變更時 Model 清單動態切換**

Provider 變更時的 UX 處理：
- 若目前 Model 不在新 Provider 的清單中 → 自動選 clear / 或預設第一個 Model
- 建議：clear + 紅字提示「請選 Model」，避免誤存成混搭（Provider=Anthropic + Model=gemini-xxx）

儲存邏輯：對應 DB `AgentConfig.Provider` / `Model`；存檔後呼叫 Bot 端 cache invalidate API。

### 7. 呼叫 Bot 端 cache invalidate 的 Internal API

新增 `POST /internal/agent-config/reload-cache`（或對齊 Stage 29 系統設定 cache reload 的既有 endpoint 模式）。Dashboard 改完 AgentConfig 後呼叫。

---

## 設計決策（供實作 Session 探索時確認）

| 決策 | 建議 | 替代方案 |
|------|------|---------|
| 欄位 nullable | ✅ `string?` | empty string default（但會讓 seed 判斷變 `IsNullOrEmpty`，語意模糊）|
| `Create()` async 化 vs cache | 建議 **cache**（對齊 AppSettingsService pattern）| 全改 async（caller 多、動靜大）|
| seed 執行位置 | Program.cs MigrateAsync 之後的 scope 內 | `IHostedService`（更優雅但要新 class）|
| Dashboard → Bot cache invalidate 通道 | Internal HTTP API（既有 pattern）| SignalR 推送 |
| Provider 下拉 vs 自由輸入 | **下拉**（封閉枚舉、避免 typo）| 自由輸入（彈性但易錯）|
| Model 下拉來源 | **`LlmModels.cs` constants 檔**（Shared 專案，Aria 維護）| DB 化（FF 二十六，待觀察升級）/ 自由輸入（易 typo 打到未支援 model 噴 500）|
| Model 下拉依 Provider 切換 | **是**（`Provider=Anthropic` → Claude models、`Provider=Gemini` → Gemini models）| 混合全列（但會讓 Anthropic Provider 選到 Gemini model 的錯誤組合）|

**關鍵驗證點**（實作 Session Plan Mode 前務必確認）：
- `LlmProviderFactory` 是 Scoped DI（`Program.cs:39`）——cache 要設計成 Singleton service 或 class static field？
- Dashboard Agent 卡片目前是否已用 `AgentConfigRepository`？若是，直接擴充，否則要新增
- `appsettings.json` 有 10+ 個 Agent 條目，seed 邏輯要對所有 entry 跑

---

## 驗收情境

### A. 基本功能

1. **Dashboard 改 Rosa 的 Provider/Model**
   - 操作：Dashboard → Agent 設定頁 → Rosa → Provider 改 `Gemini`、Model 填 `gemini-2.5-flash` → 儲存
   - 驗證：重新下 NewFeature 需求 → Rosa 使用 Gemini 跑 → Token 監控頁顯示 `gemini-2.5-flash`
   - 不需重啟 Bot（cache invalidate 立即生效）

2. **Dashboard 改 Rena 的 Provider 從 Gemini 改回 Anthropic**
   - 操作：Rena 設定改 `Provider=Anthropic`、`Model=claude-sonnet-4-6` → 儲存
   - 驗證：下次 release 流程 → Rena 使用 Anthropic、Token 監控顯示 `claude-sonnet-4-6`

### B. Seed 行為

3. **Fresh Install / 空 DB 場景**
   - 操作：清空 DB 的 `AgentConfig` 表、重啟 Bot
   - 驗證：seed 邏輯從 appsettings.json 建立所有 Agent 條目，`Provider` / `Model` 正確填入；Dashboard 顯示出全部 Agent

4. **既有部署升級場景**
   - 操作：DB 已有 `AgentConfig` 列（但 `Provider` / `Model` 為 null，migration 新欄位狀態）、重啟 Bot
   - 驗證：seed 補上 null 欄位，Dashboard 顯示的 Provider/Model 對齊 appsettings

5. **Dashboard 改值後不被 appsettings 覆蓋**
   - 操作：Dashboard 改 Rosa 為 `Gemini` → 儲存 → 重啟 Bot
   - 驗證：重啟後 Dashboard 仍顯示 `Gemini`（seed 只對 null 欄位補值，不覆蓋 Dashboard 設定）
   - **這個情境驗證了「Dashboard 為 source of truth」的核心設計**

### C. 邊界與錯誤處理

6. **Provider 寫未支援的值**
   - 操作：直接 SQL 把 Rosa 的 `Provider` 改成 `"OpenAI"` → 觸發 Rosa
   - 驗證：`LlmProviderFactory` 拋 `NotSupportedException`，log 清楚顯示錯誤 Agent 名

7. **Model 字串錯**（如 `"claude-zzz"`）
   - 操作：Dashboard 改 Rosa 的 Model 為不存在的值 → 觸發 Rosa
   - 驗證：Anthropic API 回 error，Bot 端顯示清楚錯誤訊息（不 crash）

---

## 技術約束 & 注意事項

1. **`LlmProviderFactory` 生命週期**：既有 `AddScoped<LlmProviderFactory>()`——若新增 `AgentConfigService` 是 Singleton（為了 cache），注意依賴注入順序（Scoped 依賴 Singleton 合法）。
2. **Migration 執行方式**：照 CLAUDE.md 規範（`startup-project` 用 `AiTeam.Dashboard`、`--context AppDbContext`）。
3. **Stage 5 動態 Agent 清單相容**：`AgentConfig` entity 本來就是 Stage 5 建立的動態 Agent 清單，本 Stage 只加兩個欄位，不破壞既有 CRUD。
4. **Provider / Model 驗證**：Dashboard 存檔前驗證 `Provider` 是白名單內值（`Anthropic` / `Gemini`），避免自由輸入造成 runtime crash。
5. **Token 監控頁**：`token_logs.model` 欄位會反映實際使用的 Model（經 `TokenTrackingProvider`），改完 Dashboard 後真實呼叫的 Model 會立刻對齊。
6. **appsettings.json 保留**：不要刪 appsettings 的 `Agents` 區塊——它是 seed 來源 + 文件作用（新部署者可以直接看 appsettings 了解預設 Provider/Model）。

---

## 版本

`v3.24.0 → v3.25.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Opus 1M + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | 中（5 層：Entity + Migration + Factory + new Service + Dashboard UI + seed logic + Internal API）|
| **邏輯複雜度** | 中（cache 設計 + sync/async 邊界 + seed 只補 null 不覆蓋 + DI 生命週期）|
| **風險代價** | 中偏高（動 `LlmProviderFactory` = 所有 Agent 都會經過、錯了全系統倒）|
| **範本可用度** | 中（AppSettingsService cache pattern 可抄、AgentConfig entity CRUD 既有、但 seed 邏輯屬新設計）|

**Context 粗估**：~90-110K × 1.6 = ~145-175K

**選 Opus 1M + medium 理由**：
- Sonnet 200K + high 會吃到 73-88% 邊界（Stage 31 警戒區）
- 動核心 `LlmProviderFactory`、**風險集中度高**、值得用 Opus
- 有 PR #107 的「繞道教訓」在前，規劃書裡的**禁止路線 3 條**要實作 Session 特別警覺，Opus 1M 的判斷品質更穩
- Effort medium 即可（有範本、非 spike）

**替代方案**：Sonnet 200K + high（成本低、但要接受 ~75% 吃緊；適合 Christ 想省 Opus quota 時）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-25 | 計劃書建立（Aria），對應 FF 四第二階段 2-A，含 PR #107 禁止路線明寫 |
| v1.1 | 2026-04-25 | Model UI 改為「依 Provider 動態下拉」（原為自由輸入）— 下拉清單從新增的 `src/AiTeam.Shared/Constants/LlmModels.cs` 讀；Aria 查網路維護常數 + commit + 5-10 分鐘部署；新增 FF 二十六（Model 清單 DB 化，待觀察升級）|
| v1.2 | 2026-04-25 | Aria WebFetch 官方文件確認 Gemini 現況：2.5 Pro/Flash 仍 stable 但 2026-06-17 deprecating、Gemini 3 系列仍 preview；`LlmModels.cs` 範例 `GeminiModels` 加時效註解，清單內容維持（stable 首選）；已知遷移時點（2026-06-17）寫進維護慣例 |
| v1.3 | 2026-04-25 | 實作完成、build 通過、新增「實作紀錄」章節 |

---

## 實作紀錄（2026-04-25）

> 實作 Session：Claude Opus 4.7 (1M context) + medium effort
> Aria 複檢後放行計劃書 → 實作 → build 綠燈

### 實際產出（7 項全數完成）

| # | 檔案 | 動作 |
|---|------|------|
| 1 | [src/AiTeam.Data/Entities.cs](../../src/AiTeam.Data/Entities.cs):39-42 | `AgentConfig` 加 `string? Provider` / `string? Model` 兩個 nullable 欄位 |
| 2 | `src/AiTeam.Data/Migrations/20260424180339_Stage38AgentConfigProviderModel.cs` | EF Migration（兩個 `AddColumn<string>(nullable: true)`，`text` 型別，無資料回填）|
| 3 | [src/AiTeam.Shared/Constants/LlmModels.cs](../../src/AiTeam.Shared/Constants/LlmModels.cs) | 新增常數：`ProviderAnthropic` / `ProviderGemini` / `AvailableProviders` / `GetModelsForProvider()` / `AnthropicModels`（opus-4-7、sonnet-4-6、haiku-4-5）/ `GeminiModels`（2.5-pro、2.5-flash + 2026-06-17 deprecating 註解）|
| 4 | [src/AiTeam.Bot/Services/AgentConfigCache.cs](../../src/AiTeam.Bot/Services/AgentConfigCache.cs) | 新增 Singleton 快取（TTL 5 分、SemaphoreSlim double-check lock、失敗保留上次快取）|
| 5 | [src/AiTeam.Bot/Program.cs](../../src/AiTeam.Bot/Program.cs):18, 56, 181-195 | `using Microsoft.Extensions.Options` + `AddSingleton<AgentConfigCache>()` + seed block（兩欄同 null 才補）+ `WarmupAsync` 預熱 |
| 6 | [src/AiTeam.Bot/Agents/LlmProviderFactory.cs](../../src/AiTeam.Bot/Agents/LlmProviderFactory.cs) | 注入 `AgentConfigCache`；`Create()` 改 `dbOverride ?? configConfig` per-field fallback；**關鍵修正**：TokenTrackingProvider 第 9 參數改傳 `finalModel`（否則 Dashboard 改完後 Token 監控頁會顯示舊 model）|
| 7 | [src/AiTeam.Bot/Api/InternalController.cs](../../src/AiTeam.Bot/Api/InternalController.cs):35 | `ReloadCache` 加 `scope=agent-config` 分支（`scope=all` 順帶刷新）；既有 `agents`（AppSettings）分支保留不動，兩個 scope 加 code comment 區分 |
| 8 | [src/AiTeam.Dashboard/Services/DashboardAgentService.cs](../../src/AiTeam.Dashboard/Services/DashboardAgentService.cs) | 新增 `UpdateProviderModelAsync`（server-side 白名單驗證）；`GetAgentConfigsAsync` / `CreateAgentAsync` DTO 投影加 Provider/Model |
| 9 | [src/AiTeam.Shared/Dtos/AgentConfigDto.cs](../../src/AiTeam.Shared/Dtos/AgentConfigDto.cs) | DTO 加 `Provider` / `Model` 兩欄 |
| 10 | [src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor](../../src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor) | 右欄加「LLM 設定」區塊：兩個 `MudSelect<string>` + Model 不在 Provider 清單時顯示 MudAlert warning |
| 11 | [src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor.cs](../../src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor.cs) | 注入 `ISnackbar`；加 `OnProviderChangedAsync` / `OnModelChangedAsync` / `SaveProviderModelAsync`；auto-save + ReloadCacheAsync("agent-config")；類別加 XML doc 提醒未來清單變動 edge case |

### 設計決策落地（對齊計劃書）

- **Single Source of Truth**：runtime 只讀 DB（`AgentConfigCache`），appsettings 降為 seed + fallback 來源 ✅
- **Sync-over-async**：`AgentConfigCache.Get()` 保持同步（`.GetAwaiter().GetResult()`），不動 12 個 LlmProviderFactory caller ✅ — 理由沿用既有 `MockMode` 行中已有此慣例
- **Seed 原子性**：只當 Provider/Model 兩欄位同時為 null 才補（避免半途被改過的列被 appsettings 回補覆蓋另一半）；runtime 仍允許 per-field fallback 保留未來擴充空間 ✅
- **Scope 命名**：legacy `scope=agents`（AppSettings）保留不動；新 `scope=agent-config`（AgentConfigCache）獨立；`scope=all` 順帶刷新兩者 ✅
- **Token log 同步**：TokenTrackingProvider 第 9 參數從 `config.Model`（appsettings）改傳 `finalModel`（解析後實際值），確保 Token 監控頁即時反映 Dashboard 變更 ✅（Aria 計劃書外抓出的關鍵修正）

### 驗收 Gate 狀態

| Gate | 狀態 | 備註 |
|------|------|------|
| `dotnet build AiTeam.slnx` | ✅ 綠燈 | 0 Error / 47 Warning（全為既有 NU1902 vulnerability 與 MSTEST0037，非本 Stage 新增）|
| EF Migration 產生 | ✅ | `20260424180339_Stage38AgentConfigProviderModel`，`AddColumn<string>(nullable: true)` × 2 |
| EF `database update` | ⏸ 待 Christ 執行（或待 CI/CD 部署後 `MigrateAsync` 自動套用）|

### 待 Christ 驗收項目（Docker 環境）

1. **Fresh Install / 既有升級**：DB `AgentConfigs` 舊 row 的 Provider/Model 為 null → 重啟 Bot 後 seed 應補回對應 appsettings 值
2. **UI 出現**：Dashboard `/agents` → 選 Agent → 右欄新「LLM 設定」區塊應顯示 Provider + Model 兩個下拉
3. **Provider 切換行為**：Anthropic → Gemini 時 Model 清單動態更新；舊 Model 不在新清單 → 清空 + Snackbar warning
4. **儲存 + cache invalidate**：改完 Model → Snackbar 成功提示 → 下次任務 Token 監控頁應記錄新 model（不需重啟 Bot）
5. **覆蓋性驗證（核心設計）**：Dashboard 把 Rosa 改 Gemini → 重啟 Bot → Dashboard 仍顯示 Gemini（seed 只補 null，不覆蓋已設值）

### 技術債 / 未做事項

- **資料回填腳本**：未提供（既有 row 依賴 seed 自動補）— 若上 prod 後發現 seed 邏輯未生效可手動 SQL
- **LlmModels 清單刪除 edge case**：若未來從清單移除某 model（例 Gemini 2.5 2026-06-17 deprecated 後）但 DB 仍有舊值 → UI 顯示空白 + warning；本 Stage 不做自動遷移，發生時再補（razor.cs 已加 XML doc 提醒維護者）
- **Dashboard「清空欄位回到 appsettings default」UX**：runtime 支援 per-field null fallback，但 UI 沒開放操作（Dashboard 存檔一律兩欄同寫）— 用例小眾，留待 FF 後續
- **結案第二段**（Aria 負責）：版本 bump `v3.24.0 → v3.25.0`（`src/Directory.Build.props`）+ Master Plan / Future_Feature 同步

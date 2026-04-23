# Stage 37：GeminiProvider API 層 + Crash Recovery 全面涵蓋

> 對應 Future Feature：四（多 LLM 供應商支援）第一階段 + Stage 31 延伸（搭車）
> 對應版本：v3.24.0
> 建立日期：2026-04-23
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

**主菜**：FF 四第一階段——實作 `GeminiProvider : ILlmProvider`，讓 API 層 Agent（Rosa / Demi / Sage / Release / Ops）可選 Google Gemini Flash。解鎖免費額度，為後續多 Provider 戰略（FF 二 Agent 個性、交叉驗證）鋪路。

**搭車**：FF 二十清零、怪物檔案拆解後的下一個系統性任務——**Crash Recovery 全面涵蓋**。Stage 31 做了 Meeting 層（Kickoff / Design），但 Review Appeal / Dev_plan Appeal / QA Petra 路由判斷還有 gap。統一改造成一致模式，把 `ActiveMeetingType` 欄位升級為 `ActiveOrchestration`，五種編排流程全部有 Crash Recovery。

**為什麼合併做**：兩者工程性質不同但都是「單一 Stage 範圍內獨立可驗收」的工作，Session 可分可合（見末段「建議執行路徑」）。合併一個 Stage 的理由是 **FF 二十清零後換氣**——連續 6 個 Stage 拆技術債，該出一個「新能力 + 系統性補強」組合讓士氣回血。

---

## 第一部分：GeminiProvider API 層

### 現況

`LlmProviderFactory` 目前只支援 `Anthropic`（`AnthropicProvider`）。架構已預留抽象：

| 元素 | 現況 |
|------|------|
| `ILlmProvider` | 既有介面，Rosa / Demi / Sage / Release / Ops / Rena 都透過它呼叫 API |
| `AnthropicProvider` | 既有實作，支援文字 + Vision + Token 計費 |
| `TokenTrackingProvider` | decorator 包裝，對新 Provider 自動適用 |
| `LlmProviderFactory.Create()` | switch case 入口，加新 Provider 只需擴充 |
| `AgentConfig.Provider` / `Model` 欄位 | 既有，DB + appsettings 都有 |
| Dashboard Agent 設定頁 | 既有 Provider 選單，目前只有 `Anthropic` 一個選項 |

**意思**：架構層面不需要新設計，**純擴充**。

### 實作項目

#### 1. `GeminiProvider : ILlmProvider`

位置：`src/AiTeam.Bot/Llm/GeminiProvider.cs`（照 AnthropicProvider 位置）

**串接 Google Gemini API（v1beta 或 v1）**：
- 認證：API key（從 `appsettings.json` 或環境變數）
- Endpoint：`https://generativelanguage.googleapis.com/v1/models/{model}:generateContent`
- 請求格式：Gemini 自己的 JSON 結構（`contents` 陣列、`systemInstruction`、`generationConfig`）
- 回應解析：抽 `candidates[0].content.parts[0].text`
- Token 用量：`usageMetadata.promptTokenCount` / `candidatesTokenCount` / `totalTokenCount`

**範圍限制（第一階段）**：
- ✅ 純文字生成（input / output text）
- ✅ System prompt 支援
- ✅ Temperature / max_tokens 參數
- ✅ Token 計費
- ❌ **Vision 暫不支援**（第一階段先求可用，Victoria / Quinn 傳圖片不會走 Gemini，留給第二階段）
- ❌ Tool Use 暫不支援（目前 API 層 Agent 都不用 tool）

**錯誤處理**：
- 對齊 `AnthropicProvider` 的 exception 型別
- Rate limit（429）要特殊處理（Gemini Flash 免費額度限制 15 req/min）

#### 2. `LlmProviderFactory.Create()` 擴充

`switch` 加 `"GEMINI"` case，回傳 `GeminiProvider`。

#### 3. appsettings.json Gemini 設定區塊

新增：
```json
"Gemini": {
  "ApiKey": "${AITEAM_GEMINI_KEY}",
  "DefaultModel": "gemini-2.5-flash",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1"
}
```

環境變數：`AITEAM_GEMINI_KEY`（docker-compose.yml / docker-compose.prod.yml 都要加）。

#### 4. Dashboard Agent 設定頁 Provider 下拉

`AgentSettings.razor`（或對應檔）的 Provider 下拉新增 `Gemini` 選項。

#### 5. Model 名稱對應

Gemini Flash 2.5 目前主力：`gemini-2.5-flash`（免費額度最優）。
Dashboard Model 輸入框保持自由輸入（不限制下拉），讓 Christ 可以填其他型號。

### 不在本 Stage 範圍（列為 FF 四第二階段）

- Gemini CLI 整合（`IClaudeCodeService` 對應）
- OpenAI / Codex 整合
- Vision 支援
- Tool Use 支援
- Agent 切 Gemini 後的實際成本監控儀表板

---

## 第二部分：Crash Recovery 全面涵蓋

### 現況與 gap

Stage 31 實作了 `ActiveMeetingType` 欄位 + `MeetingOrchestrationService.RecoverStuckMeetingsAsync`，涵蓋 Kickoff / Design 會議。Bot 啟動時 [`AgentQueueProcessor.ExecuteAsync`](src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs:63) 會先呼叫 `RecoverStuckTasksAsync`（佇列任務）+ `RecoverStuckMeetingsAsync`（會議）。

**gap**：
- **Review Appeal 迴圈** 跑在 `AppealOrchestrationService.HandleReviewerCompletedAsync` 的同一方法 `while` loop 內。容器重啟 → call stack 蒸發 → 沒有 `Active*` 欄位標記 → **卡住**
- **Dev_plan Appeal 迴圈**：同上
- **QA Petra 路由判斷**：`QaCoordinationService.HandleQaCompletedAsync` 單次執行的 Petra 判斷（`AssessQaFailureAsync` / `AssessNoApplicableTestsAsync`），容器剛好在這幾秒內重啟 → 無人接手 → group 卡在 QA done 後不動

### 設計決策

| 決策 | 值 | 理由 |
|------|---|------|
| 欄位改名 | `ActiveMeetingType` → `ActiveOrchestration` | 統一命名、語意涵蓋「Meeting / Appeal / QA」三類編排；Christ 決策：辛苦一次、乾淨永久 |
| 值域擴充 | `Kickoff` / `Design` / `ReviewAppeal` / `DevPlanAppeal` / `QaRouting` / null | 一欄代表「目前這個 group 正在執行哪個非佇列化的編排流程」 |
| Recovery 策略 | **全部「整場重跑」** | 與 Stage 31 Meeting 一致；避免續跑的 session state 複雜度；Review / Dev_plan Appeal 最多 3-5 分鐘重跑成本可接受 |
| Petra 入佇列 | ❌ 不做 | Petra 是多層嵌套 inline 閘門、入佇列會阻塞上游；FF 九 已討論過；改 Crash Recovery anchor 即可達到目的 |

### 實作項目

#### 1. Entity 欄位改名 + Migration

[`src/AiTeam.Data/Entities.cs:95`](src/AiTeam.Data/Entities.cs:95)：

```csharp
// 前
public string? ActiveMeetingType { get; set; }

// 後
public string? ActiveOrchestration { get; set; }
```

Migration：`RenameColumn`（EF Core 支援），不用資料遷移（欄位本來就可能為 null）。

Migration 名稱建議：`Stage37RenameActiveMeetingToOrchestration`

#### 2. 既有 Meeting 邏輯改名

[`MeetingOrchestrationService.cs`](src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs)：

- 所有 `ActiveMeetingType` 引用改為 `ActiveOrchestration`
- `RunKickoffMeetingAndWaitAsync` / `RunDesignPhaseAsync` 設值邏輯不變（仍設 `"Kickoff"` / `"Design"`）
- `RecoverStuckMeetingsAsync` 改名為 `RecoverStuckOrchestrationsAsync`
- Dispatcher 擴充五個分支（見 #5）

[`TaskGroupService.cs:522`](src/AiTeam.Bot/Orchestration/TaskGroupService.cs:522) 的 facade：
```csharp
public Task RecoverStuckOrchestrationsAsync(CancellationToken ct)
    => meetingOrchestration.RecoverStuckOrchestrationsAsync(ct);
```

[`AgentQueueProcessor.cs:68`](src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs:68) 呼叫處改名。

#### 3. Review Appeal try-finally

[`AppealOrchestrationService.HandleReviewerCompletedAsync`](src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs:51)：

在進入 `while` loop 前設 `ActiveOrchestration = "ReviewAppeal"`，`finally` 清回 `null`。

```csharp
// 進 while loop 前
await db.TaskGroups.Where(g => g.Id == group.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "ReviewAppeal"), ct);

try
{
    while (...) { /* Cody-Vera 對話 */ }
    // 可能接 Petra 仲裁 / 閘門
}
finally
{
    await db.TaskGroups.Where(g => g.Id == group.Id)
        .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, (string?)null), CancellationToken.None);
}
```

**注意**：Petra 仲裁 / Gate 本身就在 while loop 的 catch 區之外；這個 try-finally 要涵蓋「從 `HandleReviewerCompletedAsync` 進入到 `RunPetraGateAsync` / `RunPetraArbitrationAsync` 回傳為止」的整個流程。

#### 4. Dev_plan Appeal try-finally

[`AppealOrchestrationService`](src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs) Dev_plan Appeal 方法群（`RunDevPlanAppealLoop` / `RunPetraDevPlanReview` / `FinalizePetraDevPlanTask`）的上層入口點：設 `ActiveOrchestration = "DevPlanAppeal"`，`finally` 清 null。

#### 5. QA 路由判斷 try-finally

[`QaCoordinationService.HandleQaCompletedAsync`](src/AiTeam.Bot/Orchestration/Qa/QaCoordinationService.cs:29) 整個方法包 try-finally：

- try 開頭設 `ActiveOrchestration = "QaRouting"`
- finally 清 null
- Recovery 時重新呼叫 `HandleQaCompletedAsync(group, result, ...)`（result 可從 group 最後一筆 Quinn 任務重建）

**注意**：QA 的 result 重建比 Meeting 複雜——需要確認 `group.TestReport` 是否已落地到 DB（現有邏輯在呼叫 `HandleQaCompletedAsync` 前 Quinn 已 save），以及 `result` 其他欄位（`ReviewBody` / `CriticalReviewCount`）是否需要。

#### 6. Dispatcher 擴充

`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync`：

```csharp
foreach (var group in stuckGroups)
{
    try
    {
        switch (group.ActiveOrchestration)
        {
            case "Kickoff":        await RunKickoffMeetingAndWaitAsync(group, ct); break;
            case "Design":         await RunDesignPhaseAsync(group, ct); break;
            case "ReviewAppeal":   await RestartReviewAppealAsync(group, ct); break;
            case "DevPlanAppeal":  await RestartDevPlanAppealAsync(group, ct); break;
            case "QaRouting":     await RestartQaRoutingAsync(group, ct); break;
        }
    }
    catch (Exception ex) { logger.LogError(ex, ...); }
}
```

三個 `Restart*` 方法各自知道如何從 `group` + DB 狀態重建呼叫參數，呼叫對應 service。

**架構決策**：`RestartReviewAppealAsync` 等可能寫在 `MeetingOrchestrationService`（dispatcher 聚攏）或分散到各 service（職責分離）。建議**寫在 dispatcher 所在檔**，因為重啟邏輯是 Recovery 特例，不是 Appeal / QA 的正常入口。

---

## 驗收情境

### Gemini Provider（4 情境）

| 情境 | 驗收方法 |
|------|---------|
| Rosa 切 Gemini 跑 NewFeature 提案 | Dashboard 改 Rosa provider = Gemini；Discord 下新功能需求；觀察 Rosa 輸出 Issue 成功 |
| Sage 切 Gemini 做歸檔摘要 | 任務完成後觀察 Sage archive content 生成成功 |
| Gemini rate limit 錯誤處理 | 快速連發 20 個小任務逼超 15 req/min；觀察錯誤處理（不 crash，lazy 降級或 log warn） |
| Token 追蹤對 Gemini 生效 | Dashboard Token 監控頁 Rosa 用量正確累計，單位換算 Gemini 回傳的 tokens |

### Crash Recovery（5 情境）

使用 `/mock` Dashboard 化場景 + **手動重啟 Bot 容器**模擬 crash：

| 情境 | 操作 | 驗收 |
|------|------|------|
| Kickoff 會議中斷 | `/mock new_feature` 後 10 秒內重啟 Bot | 啟動 log 出現「會議 Crash Recovery」；Kickoff 重跑到完成 |
| Design 會議中斷 | Kickoff 完成後設計會議進行中重啟 | Design 重跑 |
| Review Appeal 中斷 | `/mock fail_review` Round 1 開始後重啟 | `ActiveOrchestration=ReviewAppeal` 被掃到；整場 Appeal 從 Round 0 重跑；ReviewAppealLog 保留舊紀錄 |
| Dev_plan Appeal 中斷 | `/mock fail_dev_plan` Round 1 開始後重啟 | 整場 Dev_plan Appeal 重跑 |
| QA Petra 路由判斷中斷 | `/mock fail_qa` Quinn 跑完、Petra 開始 Assess 時重啟 | `ActiveOrchestration=QaRouting` 被掃到；`HandleQaCompletedAsync` 重新呼叫、產生正確的下一步（Dev_fix / back_to_reviewer / escalate） |

**重啟方式**：Christ 在另一台終端 `docker compose restart bot`（或直接 docker-compose.prod.yml 的 Bot 容器）。

---

## 技術約束 & 注意事項

1. **Gemini API Key 管理**：用環境變數 `AITEAM_GEMINI_KEY`，對齊 `AITEAM_ANTHROPIC_KEY` 模式。docker-compose.yml 和 docker-compose.prod.yml 都要加。
2. **Migration 執行**：
   ```bash
   dotnet ef migrations add Stage37RenameActiveMeetingToOrchestration \
     --project src/AiTeam.Data \
     --startup-project src/AiTeam.Dashboard \
     --context AppDbContext
   dotnet ef database update \
     --project src/AiTeam.Data \
     --startup-project src/AiTeam.Dashboard \
     --context AppDbContext
   ```
3. **Crash Recovery 搜尋範圍**：`ActiveMeetingType` 全檔案引用（grep 確認），包含 Entity / Migration / Service / Dashboard DTO / PipelineView。
4. **Dashboard Gemini 下拉**：如果 Provider 值是 `Gemini` 但沒設 ApiKey，存檔後 Agent 實際跑會 crash——建議 Dashboard 存檔前 validate API key 有設定（可選，非必要）。
5. **Gemini 2.5 Flash 的 rate limit**：免費 15 req/min、1500 req/day。Stage 37 驗收不要連發太快，否則會被 429。

---

## 建議執行路徑

### 選項 A：拆兩個 Session（推薦）

| Session | 範圍 | Model + Effort | 粗估 Context |
|---------|------|---------------|-------------|
| **Stage 37-1** | GeminiProvider API 層（5 個實作項目） | Sonnet 200K + medium | ~50K × 1.6 = **~80K**（舒適） |
| **Stage 37-2** | Crash Recovery 全面涵蓋（6 個實作項目 + Migration） | Sonnet 200K + high | ~70K × 1.6 = **~112K**（接近邊界但夠用） |

**優點**：
- 兩個 Session 都在 Sonnet 200K 舒適區（遵循「粗估 > 80K 拆 Session」原則）
- 驗收可分段：37-1 跑 Gemini 驗收後，37-2 開始做 Crash Recovery
- 工程性質不同（新增 Provider vs 補既有流程）拆開更聚焦

**缺點**：
- 兩次規劃書 + 兩次實作 briefing + 兩次結案 → 總時長略長

### 選項 B：單一 Session（Opus 1M）

| Session | Model + Effort | 粗估 |
|---------|---------------|------|
| **Stage 37（完整）** | Opus 1M + high | ~120K × 1.6 = **~192K**（Opus 1M 19% 舒適） |

**優點**：一次結案，無 Session 切換成本
**缺點**：Opus 1M 成本較高、範圍跨兩類工作可能分散注意力

### Aria 建議

**選項 A**。理由：
1. Stage 37 已經「主菜 + 搭車」組合，拆 Session 比一口氣做更清晰
2. Sonnet 200K 成本低，兩個 Session 合計成本仍低於一個 Opus 1M Session
3. 驗收分段（37-1 Gemini 先上線驗證、37-2 Crash Recovery 再做）風險低
4. Stage 31 的教訓（Sonnet 200K + high 吃 75% 邊界）已經由「拆 Session」避開

---

## 實作紀錄 — 第一部分（2026-04-23）

> 狀態：第一部分完成，第二部分接續完成（見下節），等合併驗收。

### 交付內容

| 檔案 | 動作 | 重點 |
|------|------|------|
| [src/AiTeam.Bot/Agents/GeminiProvider.cs](../../src/AiTeam.Bot/Agents/GeminiProvider.cs) | 新增 | 實作 `ILlmProvider`；HttpClient + API key query string；System.Text.Json + `[JsonPropertyName]` 對應 camelCase |
| [src/AiTeam.Bot/Agents/LlmProviderFactory.cs](../../src/AiTeam.Bot/Agents/LlmProviderFactory.cs) | 改 | switch 加 `"GEMINI"` 分支；建構子多吃 `IHttpClientFactory` / `IConfiguration` / `ILoggerFactory` |
| [src/AiTeam.Bot/Program.cs](../../src/AiTeam.Bot/Program.cs) | 改 | 新增 `AddHttpClient("Gemini", ...)`，`BaseAddress` 末尾帶 slash |
| [src/AiTeam.Bot/appsettings.json](../../src/AiTeam.Bot/appsettings.json) | 改 | 新增頂層 `"Gemini"` 區塊（`ApiKey` / `DefaultModel` / `BaseUrl`） |
| [docker-compose.prod.yml](../../docker-compose.prod.yml) | 改 | 加 `Gemini__ApiKey: "${AITEAM_GEMINI_KEY}"` |

**編譯**：`dotnet build AiTeam.slnx` → 0 error、無新增 warning。

### 設計決策

1. **沒有包裝 `GeminiClient` wrapper 型別**（不像 `AnthropicClient`）——Gemini 沒 SDK，HttpClient + API key 足矣。多做一層包裝是過度設計，違反 CLAUDE.md「不做超出任務需要的抽象」。
2. **API key 走 query string** 而非 Authorization header——Gemini 官方文件標準做法（`?key={apiKey}`）。
3. **所有 DTO 私有化** 在 `GeminiProvider` 內（`private sealed class`）——不對外暴露，未來改簽名不會擴散破壞。
4. **雙保險序列化設定**：`JsonSerializerOptions.PropertyNamingPolicy = CamelCase` + 每個欄位明確標 `[JsonPropertyName]`。Roadmap 踩坑 #3 提醒的根因——避免某處設定被覆蓋時默默壞掉。
5. **Images 參數忽略 + log warning**（而非 throw）——interface 相容性優先，讓 Rosa/Demi 若意外收到圖片不會整個 pipeline 爆掉；warning 會在 Dashboard log 看得到。
6. **Rate limit（429）用 `InvalidOperationException`** 帶可識別前綴字串 `"Gemini API rate limit exceeded (429)"`——對齊 `TokenTrackingProvider` 既有例外風格（單次守門也是 `InvalidOperationException`），讓 Orchestration 層捕捉時型別一致。

### 範圍變更（對照原計劃書）

**原計劃書第一部分有 5 項實作項目，實際交付 4 項**。砍掉的是第 4 項「Dashboard Agent 設定頁 Provider 下拉」。

**完整理由**：
- 探索後確認 [AgentSettings.razor](../../src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor) **根本沒有 Provider 下拉**（只有 `IsActive` 切換 + 信任等級滑桿）。
- 探索後確認 DB 的 `AgentConfig` entity（[Entities.cs:30](../../src/AiTeam.Data/Entities.cs:30)）**沒有 `Provider` / `Model` 欄位**——這兩個值目前只存 `appsettings.json`。
- 要真正做出「Dashboard Provider 下拉」完整體驗，需：
  (1) DB migration 把 `Provider` / `Model` 加入 `AgentConfig` entity
  (2) `LlmProviderFactory` 改讀 DB 優先（或覆蓋）appsettings
  (3) Dashboard UI 加下拉 + Model 輸入框 + 儲存邏輯
  這個工作量等同第二部分 Crash Recovery，不該悄悄塞進 Stage 37-1。
- **替代方案**：Gemini 切換走 `appsettings.json`（編輯後重啟 Bot），或 `docker-compose.prod.yml` env `AgentSettings__Agents__Requirements__Provider: "Gemini"` + `...__Model: "gemini-2.5-flash"`，和現有 Anthropic 切模型一樣。
- **下一步建議**（Aria 結案第二段更新）：
  - Stage Roadmap 第一部分實作項目改記為 4 項交付
  - Future_Feature.md FF 四更新描述：第二階段包含「Dashboard Provider/Model Dashboard 化（DB migration）」+ Gemini CLI 整合 + OpenAI/Codex + Vision/Tool Use

### 踩坑與注意

1. **HttpClient BaseAddress 末尾 slash**：設為 `.../v1/`（末尾有 slash），因此 `PostAsJsonAsync` 的相對路徑絕對不能開頭加 slash（`models/...` 而非 `/models/...`），否則會覆寫 BaseAddress 的 path 部分變成根路徑請求。計劃書階段 Christ 有特別提醒，實作時留下註解提醒未來改動者。
2. **`LlmProviderFactory` 建構子多吃三個依賴**（`IHttpClientFactory` / `IConfiguration` / `ILoggerFactory`）——Scoped DI 下沒問題，但呼叫處（`builder.Services.AddScoped<LlmProviderFactory>()`）不變。
3. **appsettings.json 的 `Gemini:DefaultModel` 目前不讀**——實際 Model 由 `Agents:{Name}:Model` 決定。保留欄位給未來預設 fallback 用，現在是文件性欄位。
4. **`AITEAM_GEMINI_KEY` 需 Christ 在部署機 `.env` 設定**後重啟容器才生效（和 `AITEAM_ANTHROPIC_KEY` 相同流程）。
5. **Rate limit 驗證方式**：Gemini 2.5 Flash 免費額度 15 req/min、1500 req/day，連發 20 個小任務可超限。

### 驗收建議（需 Christ 設 key 後進行）

1. 編輯 `appsettings.json`（或 prod env）把 Rosa/Sage 的 `Provider` 改 `"Gemini"`、`Model` 改 `"gemini-2.5-flash"`
2. 重啟 Bot 容器
3. Discord 下新功能需求 → 觀察 Rosa 輸出 Issue、Sage archive 生成成功
4. 打開 Dashboard Token 監控頁 → `token_logs.model` 欄位顯示 `gemini-2.5-flash`、累計用量正確
5. 若要驗證 rate limit：快速連發 ≥20 小任務 → 觀察不 crash、警報頻道 log 出 429 訊息

### 未觸及（第一部分）

- ❌ Vision / Tool Use（第一階段排除）
- ❌ Dashboard Provider 下拉（見上方範圍變更）
- ❌ 第一部分實測驗收（合併到結案第二段，Rena 跑 release 時驗 Gemini）

---

## 實作紀錄 — 第二部分（2026-04-23）

> 狀態：第二部分完成，等合併驗收（與第一部分一起進結案第二段）。

### 交付內容（6 件實作項目 + 1 搭車 refactor）

| # | 檔案 | 動作 | 重點 |
|---|------|------|------|
| 1 | [src/AiTeam.Data/Entities.cs](../../src/AiTeam.Data/Entities.cs) | 改 | `ActiveMeetingType` → `ActiveOrchestration`（+ 註解升級至 5 種值說明） |
| 1 | `Migrations/20260423071516_Stage37RenameActiveMeetingToOrchestration.cs` | 新增 | EF 自動產生 `RenameColumn`（非 Drop+Add，無資料遺失） |
| 2 | [src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs) | 改 | 全檔 `ActiveMeetingType` → `ActiveOrchestration`（7 處）；`RecoverStuckMeetingsAsync` 改名 + 擴 5 分支；3 個 `Restart*` private helper |
| 2 | [src/AiTeam.Bot/Orchestration/TaskGroupService.cs](../../src/AiTeam.Bot/Orchestration/TaskGroupService.cs) | 改 | facade `RecoverStuckMeetingsAsync` → `RecoverStuckOrchestrationsAsync`；Dev_plan dispatcher 38 行 → 7 行（搭車 refactor） |
| 2 | [src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs](../../src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs) | 改 | 啟動 recovery 呼叫處改名 + 註解升級 |
| 3 | [src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs](../../src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs) | 改 | `HandleReviewerCompletedAsync` 整段包 try-finally；`ExtractCriticalIdsFromReviewBody` `private static` → `internal static` |
| 4 | （同上） | 改 | 新增 `HandleDevPlanCompletedAsync(...) : Task<bool>` wrapper（搬 TaskGroupService L169-207 的 38 行邏輯，集中 try-finally） |
| 5 | [src/AiTeam.Bot/Orchestration/Qa/QaCoordinationService.cs](../../src/AiTeam.Bot/Orchestration/Qa/QaCoordinationService.cs) | 改 | `HandleQaCompletedAsync` 拆為「外層 try-finally + 內層 `HandleQaCompletedInnerAsync`」（避免 4 個 return 點包 try-finally 後遺症） |

**編譯**：`dotnet build AiTeam.slnx` → 0 error、無新 warning。
**Migration**：EF 產生 `RenameColumn`（檢查確認），Bot 啟動時 [`Program.cs:174`](../../src/AiTeam.Bot/Program.cs:174) `MigrateAsync` 自動套用，Christ 重啟 Aspire 即可。

### 設計決策

1. **欄位改名 vs 新增**：選改名（Stage 31 既有欄位語意已被升級）+ EF `RenameColumn`，避免 Drop+Add 遺失歷史 stuck record。Christ 拍板「辛苦一次、乾淨永久」。
2. **Dev_plan Appeal try-finally 包裝位置**：Aria 推薦選項 B（搬到 `AppealOrchestrationService.HandleDevPlanCompletedAsync` wrapper），Christ 同意——順便修正 Stage 36 拆解時 dispatcher 邏輯沒完全搬乾淨的遺漏，符合「Agent vs Orchestration 歸屬原則」。對稱於既有的 `HandleReviewerCompletedAsync` / `HandleDevBlockerAsync` 命名。
3. **`HandleDevPlanCompletedAsync` 回傳 `Task<bool>`**：approve → `true`（caller 繼續走 dispatcher 觸發 Dev）；revise/escalate → `false`（已處理，caller 直接 return）。讓控制流明確。
4. **`HandleQaCompletedAsync` 拆內層 method**：原 method 有 4 個 `return` 點（passed / no_test approve / no_test reject / failed escalate），整段包 try-finally 不夠優雅；改成「公開薄殼包 try-finally → 呼叫內層 `HandleQaCompletedInnerAsync` 跑原邏輯」，原 4 return 點完全不動。
5. **Restart\* helper 寫在 `MeetingOrchestrationService`**（非 Appeal/Qa service 內）：Christ 提示——重啟邏輯是 Recovery 特例，不是 Appeal/QA 的正常入口，集中在 dispatcher 所在檔較合理。透過 `serviceProvider.CreateAsyncScope()` lazy-resolve 跨 service 依賴，無循環依賴。
6. **「整場重跑」策略**：3 個 Restart helper 都重置 RoundA = 0，但保留 `*Log` 歷史（debug 用）；`QaFixRound` 例外不重置（這是 fix 計數，跨 Recovery 應保留）。
7. **`ExtractCriticalIdsFromReviewBody` 改 internal static**：Restart helper 需要解析 ReviewBody 重建 `CriticalReviewCount`。原 private 改 internal，限同 namespace 跨 service 用，避免完全 public。

### 搭車 refactor（必須記錄）

**搭車修正 Stage 36 Dev_plan dispatcher 未完全搬進 AppealOrchestrationService 的遺漏**：

Stage 36（FF 二十-A+B）拆解 TaskGroupService 時，Review/QA dispatcher 已抽到對應 service，但 Dev_plan dispatcher 仍留在 TaskGroupService L169-207（38 行：Petra 初審 → switch on Decision → revise/escalate）。本次因 Crash Recovery 需要在 Dev_plan 流程上層入口包 try-finally，順手把這段搬進 `AppealOrchestrationService.HandleDevPlanCompletedAsync`。

完成後三個 `Handle*Completed` wrapper 對稱（Reviewer / DevPlan in Appeal、Qa in QaCoordination），TaskGroupService dispatcher 收乾淨——這部分應該在 Aria 結案第二段更新 Master Plan 時標註為 Stage 36 結案的補完（不是 Stage 37 新增工作）。

### 踩坑與注意

1. **`HandleDevPlanCompletedAsync` 內呼叫 `TaskGroupService.FireStepsAsync`**：Appeal ← TaskGroup ← Appeal 會循環依賴。解法：用 `dbScope.ServiceProvider.GetRequiredService<TaskGroupService>()` lazy-resolve，避開建構子注入循環。
2. **EF `AsTracking()` 在 Restart helper**：取出 freshGroup 後傳給下游 service，下游可能修改 group 屬性（`group.ReviewAppealRoundA++` 之類）並 `taskRepo.SaveAsync`——needs tracked entity。
3. **Migration 名稱前綴 `Stage37`**：對齊 Stage 29-30 的 migration 命名習慣（如 `Stage29aArchiveContent`），方便後續對照 Roadmap。
4. **`QaCoordinationService` 已注入 `IServiceProvider`**：grep 確認過，try-finally 直接用即可，不需改建構子。
5. **`AppealOrchestrationService` 加 `using Microsoft.EntityFrameworkCore;`**：try-finally 用 `ExecuteUpdateAsync` extension method 需要這個 namespace。
6. **Recovery query 條件不變**：`Where(g => g.ActiveOrchestration != null)` —— 純看 flag，不用時戳閾值（與 Stage 31 一致）。

### 驗收建議（Christ 重啟 Aspire 後執行）

| 情境 | 操作 | 驗收 |
|------|------|------|
| Kickoff 中斷（既有路徑回歸測試） | `/mock new_feature` 後 10 秒內 restart Bot | 啟動 log「Crash Recovery」；Kickoff 重跑完成 |
| Design 中斷（既有路徑回歸） | Kickoff 完成、Design 進行中 restart | Design 重跑 |
| **Review Appeal 中斷**（新涵蓋） | `/mock fail_review` Round 1 開始後 restart | `ActiveOrchestration=ReviewAppeal` 被掃到；ReviewAppealRoundA 重置；ReviewAppealLog 保留舊紀錄；整場 Appeal 從 Round 0 重跑 |
| **Dev_plan Appeal 中斷**（新涵蓋） | `/mock fail_dev_plan` Round 1 開始後 restart | `ActiveOrchestration=DevPlanAppeal` 被掃到；DevPlanAppealRoundA 重置；Petra 初審重跑、視 Decision 走 revise/escalate |
| **QA Routing 中斷**（新涵蓋） | `/mock fail_qa` Quinn 跑完、Petra Assess 開始時 restart | `ActiveOrchestration=QaRouting` 被掃到；`HandleQaCompletedAsync` 用 `group.TestReport` 重建 result 重新呼叫；產生正確下一步 |

### 未觸及（第二部分）

- ❌ 版本號 / Master Plan / Future_Feature 更新（Aria 結案第二段處理）
- ❌ Gemini 實測驗收（合併到結案第二段，Rena 跑 release 時改 `Agents:Release:Provider=Gemini` 一次驗）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-23 | 計劃書建立（Aria） |
| v1.1 | 2026-04-23 | 第一部分實作完成紀錄 |
| v1.2 | 2026-04-23 | 第二部分實作完成紀錄（含搭車修正 Stage 36 Dev_plan dispatcher 遺漏） |

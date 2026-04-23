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

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-23 | 計劃書建立（Aria） |

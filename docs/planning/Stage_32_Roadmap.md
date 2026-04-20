# Stage 32：/mock Dashboard 化 + 系統設定擴充

> 對應 Future Feature：十五（Dashboard 與 Discord 功能平等 — `/mock` 子項）+ 系統設定頁擴充（Mock Delay + 各流程輪次上限）
> 對應版本：v3.19.0
> 建立日期：2026-04-20
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

主題「**Dashboard 老闆控制中心擴充**」，三項合併：

| 子項 | 對應 FF | 目的 |
|----|---------|------|
| A | 系統設定擴充（Mock Delay）| Mock 複驗時不用等 30-60 秒，可自訂（1-3 秒也 OK）|
| B | 系統設定擴充（各流程輪次上限動態化）| 驗收時可拉到 1 輪加快、正式使用拉回 3 輪；升級 `WorkflowSettings` 讀取源 |
| C | 十五 | `/mock` Dashboard 化 — 老闆驗收不用再切到 Discord 輸入 |

三項同屬「dogfooding 體驗優化」，共通點：都是「Dashboard 系統設定 / 首頁觸發 UI + Bot 端 AppSettingsService / Internal API」。

---

## 子項 A：Mock Delay 可調整

### 背景

目前 `MockClaudeCodeService.cs` 6 處 + `MockLlmProvider.cs` 1 處都硬碼 `Random.Shared.Next(30000, 60000)`（30-60 秒）。複驗單一缺失時，等待成本過高。

### 實作步驟

1. **新增 AppSettings keys**（沿用 `AppSettingsService.GetAsync` / `SetAsync`）
   - `Mock:DelayMinMs`（預設 `30000`）
   - `Mock:DelayMaxMs`（預設 `60000`）

2. **`MockClaudeCodeService` + `MockLlmProvider` 改為動態讀取**
   - 注入 `AppSettingsService`
   - 新增私有 helper `GetMockDelayAsync(CancellationToken)`：
     - 讀 `Mock:DelayMinMs` + `Mock:DelayMaxMs`（都用 `int.TryParse`，失敗時 fallback 預設值）
     - 回傳 `Random.Shared.Next(min, max)`
   - 7 處 `Task.Delay` 改為 `await Task.Delay(await GetMockDelayAsync(ct), ct)`

3. **系統設定頁新增「Mock Mode 延遲範圍」區塊**（放在 Mock Mode 開關下方）
   - 兩個 `MudNumericField`（最小 ms / 最大 ms）
   - 下方灰色說明文字（沿用現有風格）：
     > 模擬 Agent 執行的隨機延遲範圍（毫秒）。預設 30000–60000（30–60 秒），模擬真實 Claude Code 執行時間。複驗單一缺失時可調小（如 1000–3000）加快速度。修改後 5 分鐘內自動生效。
   - 驗證：min < max、min >= 0、max <= 600000（10 分鐘上限，防誤輸入）
   - 右側「儲存」按鈕，沿用現有 pattern

---

## 子項 B：各流程輪次上限動態化

### 背景

`WorkflowSettings` class 讀 `appsettings.json`：

| Key | 預設 | 位置 |
|---|---|---|
| `ReviewAppealMaxRounds` | 3 | `MeetingService` + `TaskGroupService` |
| `QaFixMaxRounds` | 3 | `TaskGroupService.cs` line 825 |
| `DevPlanAppealMaxRounds` | 3 | `PmAgentService` |
| `KickoffMaxRounds` | 3 | `MeetingService.cs` line 85 |
| `DesignMeetingMaxRounds` | 3 | `MeetingService.cs` line 387 |

改設定檔要改 docker-compose.prod.yml 環境變數 + 重新部署。驗收時想「先跑 1 輪看看」成本太高。

### 實作策略（仿 FF 十一 Token 守門模式）

**動態 AppSettings 優先、appsettings.json fallback**：

1. 原 `WorkflowSettings` class 保留（當作預設值 source）
2. 新增 `WorkflowSettingsResolver`（或直接在 `WorkflowSettings` 加 async getter），讀取順序：
   - 先查 `AppSettingsService.GetAsync($"Workflow:{KeyName}")`
   - 找不到或 parse 失敗 → fallback 到 `IOptions<WorkflowSettings>` 讀 appsettings.json

### 實作步驟

1. **新增 AppSettings keys**
   - `Workflow:ReviewAppealMaxRounds`
   - `Workflow:QaFixMaxRounds`
   - `Workflow:DevPlanAppealMaxRounds`
   - `Workflow:KickoffMaxRounds`
   - `Workflow:DesignMeetingMaxRounds`

2. **封裝 `WorkflowSettingsResolver : IWorkflowSettings`**
   - 5 個 async getter（`GetReviewAppealMaxRoundsAsync` 等）
   - 每個 getter：AppSettings → int.TryParse → fallback appsettings.json
   - DI：`AddSingleton<IWorkflowSettings, WorkflowSettingsResolver>`

3. **既有 call sites 改為 async**
   - `MeetingService` / `TaskGroupService` / `PmAgentService` 原本 `_workflow.ReviewAppealMaxRounds` 改為 `await _workflow.GetReviewAppealMaxRoundsAsync(ct)`
   - 若改造範圍過大（特別是 loop 條件裡），可以在方法開頭一次讀出來存 local variable，避免每輪都讀 cache

4. **系統設定頁新增「流程輪次上限」區塊**（放在 CEO 指令通道之後，單獨 section）
   - 5 個 `MudNumericField`
   - 每欄都有獨立說明文字，例如：
     > **Review Appeal 最大輪次**（預設 3）：Cody 反駁 Vera Review 的最大輪次。拉高讓討論更充分，拉低（例如 1）加速驗收。超過此輪次後自動 escalate 給 Petra 仲裁。
   - 下方總說明：
     > 各流程輪次上限設定。修改後 5 分鐘內自動生效，新任務會採用新設定（執行中任務沿用原設定）。

---

## 子項 C：`/mock` Dashboard 化（FF 十五核心）

### 背景

Discord `/mock <scenario>` 指令是驗收最常用工具，目前**只在 Discord 可用**，Dashboard 沒有對應入口。老闆每次驗收都要切到 Discord 輸入文字。

Stage 29-5「Dashboard 下達指令給 Victoria」已建立 pattern：Bot internal API + DashboardBotService + UI 卡片 + fire-and-forget。本子項沿用。

### 實作步驟

1. **`CommandHandler` 中的 Mock 處理邏輯抽成 shared service**
   - 新增 `MockScenarioService`（或放在 `AiTeam.Bot/Services/`）
   - 將 `HandleMockProposalFlowAsync` 等私有方法搬進去
   - `CommandHandler` 的 `/mock` slash command 改為薄 wrapper（驗證 MockMode on → call `MockScenarioService.RunScenarioAsync(scenario, title, project)`）

2. **Bot Internal API 新增端點**
   - `POST /internal/mock/scenario`
   - Body：`{ scenario: "new_feature" | "bug_fix" | "new_feature_with_proposal" | "fail_review" | "fail_qa" | "fail_dev_plan" | ..., title?: string, project?: string }`
   - 驗證：MockMode 必須啟用，否則 `BadRequest`
   - fire-and-forget 呼叫 `MockScenarioService.RunScenarioAsync`
   - 立即回 `202 Accepted`，後續進度透過既有 SignalR push

3. **`DashboardBotService.TriggerMockScenarioAsync`**
   - 仿 `RequeueTaskAsync` / `ReloadCacheAsync` 模式
   - 回 `bool`（送出是否成功）

4. **Dashboard UI — Mock 觸發卡片**
   - 位置：首頁 Agent 狀態區域下方（Mock Mode 啟用時才顯示），或獨立「驗收工具」小區塊
   - 元件：
     - `MudSelect` 選擇情境（對應所有 `/mock` scenarios）
     - 選擇性輸入：Title / Project（預設帶入 Mock 範本值）
     - 「🎬 觸發」按鈕
     - 按下後 Snackbar 提示「已送出 → 等 Dashboard 任務卡出現」
   - Mock Mode 未啟用時顯示警示「請先啟用 Mock Mode」

---

## 子項順序建議

A + B 同源（系統設定頁擴充），先一起做；C 是重構 + 新 UI，最後做獨立驗證。

1. **A（Mock Delay）** — 5 個檔案改動、最機械化
2. **B（輪次上限動態化）** — 涉及 4-5 個 Service 改為 async，需要小心 loop 語意
3. **C（`/mock` Dashboard 化）** — 抽 service + Internal API + UI 卡片，範圍最大

---

## 驗收情境

- **A**：
  - 系統設定頁調 Mock Delay 為 1000/3000 → 啟動 Mock Mode `/mock new_feature` → 各 Agent 延遲確實變 1-3 秒
  - 調回 30000/60000 → 延遲恢復
  - 輸入非法值（min > max、負數、超過 600000）→ UI 驗證阻擋

- **B**：
  - `Workflow:ReviewAppealMaxRounds` 設為 1 → `/mock fail_review` → Review Appeal 跑 1 輪後立即 escalate 給 Petra
  - 清空 AppSettings → 恢復 appsettings.json 的 3 輪
  - 執行中任務：修改後，執行中的 Stage 不受影響（用原值跑完）；新任務採用新值

- **C**：
  - Dashboard Mock 觸發卡片選「new_feature_with_proposal」→ 按「🎬 觸發」→ 跟 Discord `/mock` 一樣的流程觸發
  - Mock Mode 關閉時卡片顯示禁用狀態
  - 所有 6 種情境（new_feature / bug_fix / new_feature_with_proposal / fail_review / fail_qa / fail_dev_plan）都能從 Dashboard 觸發

---

## 版本

`v3.18.0 → v3.19.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Opus 1M + high**

四維度評估：

| 維度 | 評估 |
|---|---|
| **Context 量** | 中-重（三子項 + 多 layer + 5 個 call sites 要改 async） |
| **邏輯複雜度** | 中（async 改造要小心 loop、Resolver pattern 要設計清楚） |
| **風險代價** | 中（動核心 WorkflowSettings，錯了會影響所有會議流程） |
| **範本可用度** | 高（A 抄 Stage 17 MockLlmProvider、B 抄 FF 十一 token 守門 pattern、C 抄 Stage 29-5 fire-and-forget）|

**選 Opus 1M 的理由**（校準 Stage 31 經驗）：
- 子項 ≥ 3 且 Christ 希望一氣呵成
- 多 layer（MockService / Resolver / Controller / UI）累積 context 會超過 60%
- Stage 31 Sonnet 200K 用到 75%，本 Stage 規模類似但多一個子項 → Sonnet 200K 高機率 compact
- Opus 1M 一次做完 context 充裕，品質也更穩

---

## 設計約定（本 Stage 共通）

**新增每個設定欄位都要附「說明文字」**（Christ 明確要求 2026-04-20），沿用現有系統設定頁灰色小字風格：

- 格式：`<strong>設定名稱</strong>（預設值）：一句話說明用途 + 典型使用場景`
- 避免只放 placeholder 或 label，老闆看欄位時要能秒懂「這個改了會發生什麼」

---

## 結案檢查清單

完成後記得「兩段式分工」（見 `feedback_impl_session_briefing.md` 第五節）：

- **實作 Session 做**：Stage_32_Roadmap header v2.0、狀態 ✅、補「實作紀錄」章節、版本歷史
- **Aria 做**：Master Plan header / 索引 / changelog + Future_Feature header / FF 十五移入已完成 / changelog

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-20 | 初版規劃書，三子項（Mock Delay / 輪次上限動態化 / `/mock` Dashboard 化）合併為「Dashboard 老闆控制中心擴充」；Opus 1M + high（取 Stage 31 校準教訓）|

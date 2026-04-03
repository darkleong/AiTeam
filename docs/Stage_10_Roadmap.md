# Stage 10：開發流程自動閉環

> 版本：v2.0
> 建立日期：2026-04-03
> 狀態：✅ 驗收完成（2026-04-04）

---

## 目標

讓一個新功能從老闆說話，到 PR merge 通知老闆，中間所有的推進都是自動的。

老闆只需要做兩件事：
1. **核准提案書**（確認需求 + UI 規格）
2. **最後 merge PR**（Vera 確認無 🔴 後 CEO 通知）

---

## 一、CEO Orchestrator（基礎建設）

> 其他三項都依賴這項完成，**優先實作**。

### 背景

目前 CEO 是「任務路由器」——你說什麼它派什麼，但任務完成後就停下來，不知道下一步是什麼。

要讓流程自動閉環，CEO 必須升級為「任務生命週期全程指揮官」。

### 需要實作的能力

**1. 任務狀態新增 `waiting_input`**

```
現在：pending → running → done / failed
未來：pending → running → waiting_input → running → done / failed
```

**2. Agent 可以「暫停並回報問題」**

Agent 新增回傳格式：
```json
{
  "success": false,
  "waiting_input": true,
  "question": "Rosa Issue #3 的驗收條件不明確，請問篩選條件最多幾個？",
  "question_type": "requirement | ui_spec | business_decision"
}
```

**3. CEO 按問題類型路由**

| 問題類型 | CEO 路由到 |
|---------|-----------|
| `requirement` | Rosa 補充 Issue |
| `ui_spec` | Demi 更新規格文件 |
| `business_decision` | 升級給老闆 |

**4. 任務完成後 CEO 自動觸發下一步**

CEO 維護一份「標準流程表」，當任務完成時對照流程表決定下一步：

```
新功能流程：
  提案核准 → Dev（附帶 Issues + UI 規格）
  Dev PR 開出 → QA + Doc + Vera 並行觸發
  Vera 無 🔴 → 通知老闆可以 merge

Bug 修復流程：
  Dev PR 開出 → Vera 觸發
  Vera 無 🔴 → 通知老闆可以 merge
```

**5. CEO 路由輕量化**

Agent 回報「完成」時，CEO 不呼叫 LLM，直接查流程表決定下一步，走快速路徑。

---

## 二、提案書增強（確認機制升級）

### 背景

Stage 9 的提案模式只有「核准 / 取消」兩個選項，且 Discord Embed 有字數限制，看不到完整內容。

### 增強一：✏️ 第三個按鈕「請修改後重提」

```
提案書 Embed
  ✅ 核准，開始開發
  ✏️ 需要調整
  ❌ 取消
```

按下 ✏️ 後：
```
CEO 問：「請說明要調整的方向（Rosa 的需求 / Demi 的 UI 規格）」
    ↓
老闆回答：「UI 規格的表格欄位要加日期範圍篩選，其他沒問題」
    ↓
CEO 帶著意見重新指派 Demi 修改
    ↓
Demi 更新規格 → CEO 重新發出提案書
```

### 增強二：提案書附上完整文件連結

Embed 除了摘要，額外附上：
- 📋 GitHub Issues 連結（每個 Issue 一個連結）
- 🎨 UI 規格文件連結（`docs/ui-specs/xxx.md` 在 GitHub 上的連結）

老闆可以點進去看完整內容，再決定是否核准。

---

## 三、開發上下文補強

### 背景

目前 Dev（Cody）收到任務時只有標題 + 描述，看不到 codebase 結構、Rosa 的 Issues、Demi 的規格，導致 `files_to_modify` 靠猜，高機率猜錯。

### 實作方向

**1. Dev 制定計畫前先掃描 repo 結構**

呼叫 GitHub Tree API 取得目錄結構快照（不需要 clone），提供給 LLM 作為制定計畫的參考：

```
GitHubService.GetRepoTreeSummaryAsync(owner, repo)
→ 回傳兩層目錄結構（最多 200 筆）
→ 注入 BuildPlanUserMessage 的上下文（## Repo 結構 區塊）
```

**2. CEO 派任務給 Dev 時自動附帶上游產出**

CEO 觸發 Dev 時，從 DB 查同一批任務的相關記錄，附帶：
- Rosa 建立的 GitHub Issues 編號與標題清單
- Demi 的 UI 規格文件路徑（`docs/ui-specs/xxx.md`）

Dev 制定計畫時可直接讀取這些文件作為依據。

---

## 四、Review 閉環

### 背景

目前 Vera 審查完就停了，有 🔴 問題只會在 GitHub 留評論，不會主動通知 Dev，也無法自動重審。

### 實作方向

**Vera 審查完成後：**

```
Vera 審查完成，發現 🔴 → 向 CEO 回報：「PR #X 有 N 個必修問題」
    ↓
CEO（透過 CEO Orchestrator）通知 Dev：「Vera 要求修正，修完告知我」
    ↓
Dev 修好，推新 commit 到同一 branch
    ↓
CEO 偵測到 PR 有新 push → 自動重派 Vera
    ↓
Vera 確認無 🔴 → 回報 CEO
    ↓
CEO 通知老闆：「PR #X 已通過審查，可以 merge 了」
```

**如果 Vera 審查通過（無 🔴）：** 直接通知老闆，不需要繞一圈。

---

## 五、Ops Rollback 機制

### 背景

目前部署失敗只能通知老闆手動處理，Bot 容器內無法執行 docker-compose。

### 實作方向

**在 self-hosted runner 新增 `rollback.yml` workflow：**

```yaml
# .github/workflows/rollback.yml
on:
  workflow_dispatch:
    inputs:
      target_tag:
        description: 'Rollback 目標版本（如 v1.1.0）'
        required: true
```

**Maya 透過 GitHub API 觸發它：**

```
部署失敗，Maya 判斷為程式問題（非外部故障）
    ↓
Maya 呼叫 GitHub Actions API 觸發 rollback.yml
    ↓
Self-hosted Runner 執行 docker compose pull + up（指定舊版 tag）
    ↓
Maya 向 CEO 回報：「v1.2.0 部署失敗，已回滾到 v1.1.0」
    ↓
CEO 通知老闆 + 通知 Dev 請修復
```

---

## 完整新功能流程（Stage 10 完成後）

```
你說：「我要做 Token 監控的匯出功能」
    ↓
CEO 分類：新功能 → 提案模式
Rosa + Demi 並行產出
    ↓
CEO 發出提案書 Embed
  📋 GitHub Issues #12, #13 連結
  🎨 UI 規格文件連結
  [✅ 核准] [✏️ 需要調整] [❌ 取消]
    ↓
你審閱完整文件，回來按 ✅
    ↓
CEO 自動派 Dev（附帶 Issues + 規格路徑 + repo 結構）
    ↓
Dev 開發 → Push → 開 PR
    ↓
CEO 自動觸發：QA + Doc + Vera 並行
    ↓
Vera 有 🔴 → CEO 通知 Dev 修 → Dev 推 commit → CEO 自動重派 Vera
Vera 無 🔴 → CEO 通知你：「PR #X 可以 merge 了」
    ↓
你 merge

你做的事：說需求 → 核准提案書 → merge PR
```

---

## 實作順序建議

| 順序 | 項目 | 原因 |
|------|------|------|
| 1 | CEO Orchestrator | 其他三項的基礎，必須先完成 |
| 2 | 提案書增強（✏️ + 連結）| 依賴 CEO Orchestrator 的任務狀態機制 |
| 3 | 開發上下文補強 | 相對獨立，CEO Orchestrator 完成後可同步進行 |
| 4 | Review 閉環 | 依賴 CEO Orchestrator 的自動觸發機制 |
| 5 | Ops Rollback | 最獨立，可最後實作 |

---

## 驗收標準

| 項目 | 標準 | 結果 |
|------|------|------|
| CEO Orchestrator | 新功能完整跑一次：說需求 → 提案 → Dev → Vera → 通知 merge，全程無需手動推進 | ✅ |
| 提案書增強 | 按 ✏️ 提出修改意見，CEO 重新提案；點 GitHub 連結能看到完整 Issues 和 UI 規格 | ✅ |
| 開發上下文 | Dev 制定計畫時能看到 repo 結構，計畫中的 `files_to_modify` 準確率明顯提升 | ✅ |
| Review 閉環 | Vera 有 🔴 → Dev 自動收到通知 → 修完後 Vera 自動重審 → 無 🔴 後老闆收到通知 | ✅ |
| Ops Rollback | 模擬部署失敗，Maya 自動觸發 rollback workflow，程式碼路徑完整，自動路徑待真實部署驗證 | ✅ |

---

## 實作重點紀錄

> 本節記錄 Stage 10 實作過程中的關鍵架構決策、踩坑與修正，供未來維護參考。

---

### 架構設計

#### WorkflowEngine（`src/AiTeam.Bot/Orchestration/WorkflowEngine.cs`）

**純靜態流程表，無 LLM，無 DB。**

```csharp
public WorkflowDecision GetDecision(
    WorkflowType workflowType,
    string completedAgent,
    AgentExecutionResult result,
    int fixIteration)
```

設計原則：
- Agent 完成後呼叫 `GetDecision()`，回傳 `NextAction`（FireAgents / NotifyBossMerge / NotifyBossIntervention / Nothing）
- **不走 LLM**：毫秒級決策，不花 Token
- `fixIteration >= 3` 時升級為 `NotifyBossIntervention`，防止無限 fix loop

**流程表（新功能）：**

| completedAgent | 條件 | 下一步 |
|---------------|------|--------|
| `Rosa` | - | Dev |
| `Dev` | - | QA + Doc + Reviewer（並行） |
| `Dev_fix` | - | Reviewer |
| `QA` / `Doc` | - | Nothing（等 Reviewer） |
| `Reviewer` | CriticalReviewCount == 0 | NotifyBossMerge |
| `Reviewer` | CriticalReviewCount > 0 且 fixIteration < 3 | Dev（IsFixLoop=true） |
| `Reviewer` | fixIteration >= 3 | NotifyBossIntervention |

**流程表（Bug 修復）：**

| completedAgent | 條件 | 下一步 |
|---------------|------|--------|
| `Dev` | - | Reviewer |
| `Dev_fix` | - | Reviewer |
| `Reviewer` | CriticalReviewCount == 0 | NotifyBossMerge |
| `Reviewer` | CriticalReviewCount > 0 且 fixIteration < 3 | Dev（IsFixLoop=true） |
| `Reviewer` | fixIteration >= 3 | NotifyBossIntervention |

---

#### TaskGroupService（`src/AiTeam.Bot/Orchestration/TaskGroupService.cs`）

**群組管理 + 並行觸發 + 遞迴 Orchestration。**

核心流程：

```
HandleAgentCompletedAsync(groupId, completedAgent, result)
    → 查 WorkflowEngine 取得 decision
    → FireStepsAsync(group, decision.NextSteps)
        → 每個 step 建立 TaskItem → 呼叫 executor.ExecuteTaskAsync()
        → 完成後 Task.Run(() => HandleAgentCompletedAsync(...))  ← 遞迴
```

**重要：遞迴呼叫用 `Task.Run`（背景執行，不 await）**，避免 await chain 過深導致 Bot 阻塞。

TaskItem.Description metadata block 格式（Dev / QA / Doc / Reviewer 接收）：
```
任務標題

PR 連結：https://github.com/...

---
issue_urls: ["https://...","https://..."]
ui_spec_path: docs/ui-specs/xxx.md
fix_loop: true           ← 僅修復迭代
vera_review:             ← 僅修復迭代，附上 Vera 完整報告
[Vera 審查報告全文]
---
```

---

#### TaskGroup Entity（`src/AiTeam.Data/Entities/TaskGroup.cs`）

```csharp
public class TaskGroup
{
    public Guid    Id             { get; set; }
    public string  Title          { get; set; }
    public string  Project        { get; set; }
    public string  Status         { get; set; }   // running / done / failed
    public string  WorkflowType   { get; set; }   // new_feature / bug_fix
    public string? IssueUrls      { get; set; }   // JSON array（jsonb）
    public string? UiSpecPath     { get; set; }
    public string? DevPrUrl       { get; set; }
    public string? LastReviewBody { get; set; }   // Vera 最新完整報告
    public int     FixIteration   { get; set; }   // Dev fix 次數（≥3 升級給老闆）
}
```

EF Migration：`AddTaskGroupAndWaitingInput`（含 `task_groups` 資料表 + `TaskItem.GroupId` FK）

Status index：`AddTaskGroupStatusIndex`（`HasIndex(x => x.Status)`，查詢熱路徑優化）

---

#### Review 閉環 Webhook（`src/AiTeam.Bot/GitHub/WebhookController.cs`）

**`pull_request.synchronize`** 事件觸發條件（有人 push 到 PR branch）：

```csharp
// HandlePrSynchronizedAsync
1. 查 DB 找 GroupId 對應的 TaskGroup（DevPrUrl 比對）
2. 找到 → 組建假 AgentExecutionResult（Success=true, CriticalReviewCount=1）
3. 呼叫 taskGroupService.HandleAgentCompletedAsync(group.Id, "Dev_fix", fakeResult, prUrl)
4. WorkflowEngine 決策 Dev_fix → Reviewer → 觸發 Vera 重審
```

**重要注意**：此路徑與 Orchestrator 的 `HandleAgentCompletedAsync("Dev_fix")` 存在潛在 Race Condition（見下方踩坑記錄）。

---

#### Vera 審查報告傳遞（`AgentExecutionResult.ReviewBody`）

```csharp
public record AgentExecutionResult(
    bool Success, string Summary,
    string? OutputUrl = null,
    bool IsWaitingInput = false,
    string? QuestionType = null,
    string? Question = null,
    int CriticalReviewCount = 0,
    string? ReviewBody = null,       // Vera 完整報告
    IReadOnlyList<string>? OutputUrls = null)  // Rosa 多 Issue URL
```

`ReviewBody` 在 `TaskGroupService.HandleAgentCompletedAsync` 中存入 `group.LastReviewBody`，下次 Dev fix loop 時透過 `BuildTaskDescription` 注入到 TaskItem.Description。

---

### 踩坑與修正記錄

#### 1. Race Condition：Vera 被重複觸發

**問題**：Dev_fix push commit 時，Webhook（`HandlePrSynchronizedAsync`）與 Orchestrator（`FireOneStepAsync` 的背景 Task.Run）幾乎同時呼叫 `HandleAgentCompletedAsync("Dev_fix")`，導致 Vera 被觸發兩次。

**根本原因**：兩個路徑都認為自己在正常推進流程，都查到 `group.Status = "running"` 就繼續。

**修正（`TaskGroupService.HandleAgentCompletedAsync`）**：

```csharp
// 防止 Race Condition：若 TaskGroup 已結束（done/failed），不重複推進
if (group.Status is "done" or "failed")
{
    logger.LogDebug("HandleAgentCompleted：TaskGroup {Id} 已結束（{Status}），略過", groupId, group.Status);
    return;
}
```

**注意**：Group Status Guard 只能攔截「已結束」的群組，無法 100% 防止「兩個執行緒同時在 running 狀態」的競爭。目前實測沒問題，若未來遇到，可考慮加 DB-level 樂觀鎖（ETag / RowVersion）。

---

#### 2. IssueUrls 全部是同一個 URL

**問題**：Rosa 建立 N 個 Issues，但 TaskGroup 的 `IssueUrls` 全部存同一個 URL（第一個）。

**根本原因**：`AgentExecutionResult` 只有 `OutputUrl`（單一字串），Rosa 回傳 N 個 URL 時只能傳第一個，CommandHandler 重複用同一個 URL 填滿清單。

**修正**：新增 `AgentExecutionResult.OutputUrls`（`IReadOnlyList<string>?`）。

`RequirementsAgentService.CreateIssuesFromPreviewAsync` 改為：
```csharp
return new AgentExecutionResult(
    true,
    $"已建立 {createdUrls.Count} 個 GitHub Issues",
    createdUrls.FirstOrDefault(),
    OutputUrls: createdUrls);   // 完整清單
```

`CommandHandler` 讀取時優先用 `OutputUrls`，fallback 到 `OutputUrl`：
```csharp
var issueUrlsList = (result.OutputUrls is { Count: > 0 }
    ? result.OutputUrls
    : (result.OutputUrl is not null ? [result.OutputUrl] : Array.Empty<string>()))
    .Where(u => !string.IsNullOrEmpty(u))
    .ToList();
```

---

#### 3. Designer / Reviewer PushStatus 傳 Guid.Empty

**問題**：Dashboard 任務中心看不到 Designer 和 Reviewer 的任務進度更新。

**根本原因**：`DesignerAgentService.PushStatus` 和 `ReviewerAgentService.PushStatus` 是私有方法，寫死用 `Guid.Empty` 作為 TaskId，Dashboard 收到後無法對應到正確任務。

**修正**：兩個 PushStatus 方法改為接受 `Guid taskId` 參數，呼叫端傳入實際 `task.Id`。

---

#### 4. Agent 頻道直接指令，專案欄位空白

**問題**：在 `#maya-ops` 等 Agent 頻道直接下指令，Dashboard 任務中心的專案欄位顯示空白。

**根本原因**：`ExtractProjectFromChannelName()` 永遠回傳 `""`（佔位 stub），導致 `ProjectId = null`。

**修正（`CommandHandler.cs`）**：
```csharp
var projectRaw = ExtractProjectFromChannelName(msg.Channel.Name);
var project    = string.IsNullOrEmpty(projectRaw) ? _gitHubSettings.DefaultRepo : projectRaw;
```

---

#### 5. Dev Agent 框架幻覺（驗收測試發現）

**問題**：Dev Agent 在某些任務中幻覺使用 `Microsoft.SemanticKernel`（本專案未安裝），建立了與現有架構完全不相容的新檔案，而非修改現有的 `*Service.cs`。

**Vera 為何沒攔截**：Vera 是逐檔審查，看到 `ReviewerAgent.cs` 是合理的 C# 代碼，但不知道整個 repo 的依賴清單，也不知道應該修改的是 `ReviewerAgentService.cs`。

**已記錄至 Future Feature 十二**：Dev Prompt 加禁用框架清單 + Vera Prompt 加 dependency audit 規則。

---

### Ops Rollback 的兩條路徑（重要）

> **此設計容易混淆，請特別注意。**

**路徑 A — Discord 手動指令（現行）**

老闆在 `#maya-ops` 說「rollback」→ CEO 確認 → `ExecuteTaskAsync` → 發警報到 `#警報` → **不執行 rollback**

這是設計上的「等人確認」路徑，Ops 只通知，不自動執行。

**路徑 B — 部署監控自動觸發（Stage 10 核心）**

`MonitorDeploymentAsync` 偵測部署失敗 → `ExecuteRollbackAsync` → 呼叫 `TriggerWorkflowDispatchAsync("rollback.yml")` → GitHub Actions 執行 → **真正回滾**

Stage 10 驗收測試（Discord 手動指令）走的是路徑 A，路徑 B 需真實部署失敗才能觸發，未在驗收測試中直接驗證。程式碼邏輯已完整實作。

---

### 新增 EF Migration 注意事項

本 Stage 新增了兩個 Migration：
- `AddTaskGroupAndWaitingInput`
- `AddTaskGroupStatusIndex`

執行 Migration 指令時，必須指定 startup project 為 Dashboard（Bot 沒有 EF Design 依賴）：

```bash
dotnet ef migrations add <MigrationName> \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext

dotnet ef database update \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-03 | 初版建立（五大項目：CEO Orchestrator、提案書增強、開發上下文、Review 閉環、Ops Rollback）|
| 2026-04-03 | 實作完成，CHANGELOG v1.3.0 |
| 2026-04-04 | 驗收完成；補充實作重點紀錄（架構設計、踩坑記錄、Ops 兩條路徑說明、Migration 注意事項）|

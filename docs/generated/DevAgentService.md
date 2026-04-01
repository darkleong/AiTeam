# DevAgentService 技術文件

## 類別概覽

**命名空間**：`AiTeam.Bot.Agents`
**檔案路徑**：`src/AiTeam.Bot/Agents/DevAgentService.cs`

`DevAgentService` 是 AI 軟體團隊中的開發者代理人（Dev Agent），實作 `IAgentExecutor` 介面。其職責為接收 CEO Agent 分派的任務，透過 LLM 分析任務內容並自動產生執行計畫，隨後操作 GitHub Repository（修改程式碼、建立 Branch、Commit、Push），最終開啟 Pull Request。

### 主要功能

| 功能 | 說明 |
|------|------|
| 計畫產生 | 呼叫 LLM 分析任務，產出結構化的 `DevPlan` JSON |
| 程式碼修改 | 依計畫逐一讀取現有程式碼，交由 LLM 修改後寫入檔案 |
| Git 操作 | Clone/Pull、建立 Branch、Commit、Push |
| PR 建立 | 自動開啟 Pull Request 並附上任務說明與變更摘要 |
| Code Review | 支援 `code_review` 任務類型，產出整體架構評估與改善建議 |
| 狀態推播 | 即時透過 `DashboardPushService` 更新 Dashboard 狀態 |

### 建構函式

```csharp
public DevAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ILogger<DevAgentService> logger)
```

#### 相依注入參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `providerFactory` | `LlmProviderFactory` | 建立對應角色的 LLM Provider |
| `gitHubService` | `GitHubService` | GitHub 操作服務（Clone、Commit、PR 等） |
| `taskRepository` | `TaskRepository` | 任務資料存取層，用於記錄 Log 與更新狀態 |
| `dashboardPush` | `DashboardPushService` | 即時推播 Agent 狀態至 Dashboard |
| `logger` | `ILogger<DevAgentService>` | 結構化日誌記錄 |

---

## Public 方法

### `BuildPlanAsync`

分析 CEO 分派的任務，呼叫 LLM 產生結構化執行計畫（`DevPlan`），供第二層老闆確認使用。最多重試 2 次解析 LLM 回應，若仍失敗則回傳預設的失敗計畫物件。

```csharp
public async Task<DevPlan> BuildPlanAsync(
    TaskItem task,
    string owner,
    string repo,
    IReadOnlyList<string> rules,
    CancellationToken cancellationToken = default)
```

#### 參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `task` | `TaskItem` | 待執行的任務項目 |
| `owner` | `string` | GitHub Repository 擁有者（使用者名稱或組織） |
| `repo` | `string` | GitHub Repository 名稱 |
| `rules` | `IReadOnlyList<string>` | 規則清單，作為 LLM System Prompt 的一部分 |
| `cancellationToken` | `CancellationToken` | 非同步取消令牌（選填） |

#### 回傳值

`Task<DevPlan>`：包含任務類型、Branch 名稱、待修改檔案清單、Commit 訊息與摘要的執行計畫。

#### 失敗計畫結構（解析失敗時）

```json
{
  "summary": "Dev Agent 無法解析任務，請查看 log。",
  "task_type": "unknown",
  "branch_name": "",
  "files_to_modify": [],
  "commit_message": ""
}
```

---

### `ExecuteAsync`

依照 `DevPlan` 執行完整的開發流程：Clone Repo → 建立 Branch → 修改程式碼 → Commit & Push → 開 PR。若任務類型為 `code_review`，則改為執行 Code Review 流程。

```csharp
public async Task<string> ExecuteAsync(
    TaskItem task,
    DevPlan plan,
    string owner,
    string repo,
    CancellationToken cancellationToken = default)
```

#### 參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `task` | `TaskItem` | 待執行的任務項目 |
| `plan` | `DevPlan` | 由 `BuildPlanAsync` 產生的執行計畫 |
| `owner` | `string` | GitHub Repository 擁有者 |
| `repo` | `string` | GitHub Repository 名稱 |
| `cancellationToken` | `CancellationToken` | 非同步取消令牌（選填） |

#### 回傳值

`Task<string>`：
- 一般任務：PR URL 字串（如 `https://github.com/owner/repo/pull/1`）
- `code_review` 任務：LLM 產出的 Code Review 內容文字

#### 執行流程

```
開始執行
  │
  ├─ [code_review] ──→ ExecuteCodeReviewAsync → 回傳 Review 內容
  │
  └─ [其他類型]
       ├─ Clone / Pull repo
       ├─ 建立並切換至 feature branch
       ├─ 逐檔呼叫 LLM 修改程式碼並寫入
       ├─ Commit All + Push
       ├─ 開啟 Pull Request
       └─ 回傳 PR URL
```

#### 例外處理

執行過程中若發生任何例外：
1. 記錄錯誤 Log
2. 更新任務狀態為 `failed`
3. 推播 `error` 狀態至 Dashboard
4. 重新拋出例外（`throw`）
5. `finally` 區塊確保清除本地 Clone 目錄

---

### `ExecuteTaskAsync`

實作 `IAgentExecutor` 介面的統一入口方法，整合 `BuildPlanAsync` 與 `ExecuteAsync`，提供包裝後的執行結果物件，**不會重新拋出例外**。

```csharp
public async Task<AgentExecutionResult> ExecuteTaskAsync(
    TaskItem task,
    string owner,
    string repo,
    IReadOnlyList<string> rules,
    CancellationToken cancellationToken = default)
```

#### 參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `task` | `TaskItem` | 待執行的任務項目 |
| `owner` | `string` | GitHub Repository 擁有者 |
| `repo` | `string` | GitHub Repository 名稱 |
| `rules` | `IReadOnlyList<string>` | 規則清單 |
| `cancellationToken` | `CancellationToken` | 非同步取消令牌（選填） |

#### 回傳值

`Task<AgentExecutionResult>`：

| 情況 | `Success` | `Message` | `Payload` |
|------|-----------|-----------|-----------|
| 成功 | `true` | `"PR 已開啟：{prUrl}"` | PR URL |
| 失敗 | `false` | `"Dev Agent 執行失敗：{ex.Message}"` | `null` |

---

## DevPlan 類別

Dev Agent 產出的執行計畫資料模型，由 LLM 以 JSON 格式回傳後反序列化。

```csharp
public class DevPlan
```

### 屬性

| 屬性 | JSON 欄位 | 型別 | 說明 |
|------|-----------|------|------|
| `TaskType` | `task_type` | `string` | 任務類型：`bug_fix`、`feature`、`refactor`、`code_review` |
| `BranchName` | `branch_name` | `string` | Git Branch 名稱（如 `feature/add-login`、`fix/null-exception`） |
| `FilesToModify` | `files_to_modify` | `List<string>` | 需要修改的檔案路徑清單 |
| `CommitMessage` | `commit_message` | `string` | Git Commit 訊息（建議遵循 Conventional Commits 格式） |
| `Summary` | `summary` | `string` | 此次變更的簡要說明 |

### LLM 回應範例

```json
{
  "task_type": "feature",
  "branch_name": "feature/add-user-profile",
  "files_to_modify": [
    "src/AiTeam.Web/Pages/Profile.razor",
    "src/AiTeam.Data/Models/UserProfile.cs"
  ],
  "commit_message": "feat: 新增使用者個人資料頁面",
  "summary": "建立使用者個人資料頁面，包含顯示與編輯功能"
}
```

---

## 使用範例

### 透過 `ExecuteTaskAsync` 執行任務（建議方式）

```csharp
// 注入 DevAgentService
public class CeoAgentService(DevAgentService devAgent)
{
    public async Task DispatchTaskAsync(TaskItem task)
    {
        var rules = new List<string>
        {
            "所有公開 API 必須加上 XML 文件註解",
            "禁止使用 var 宣告集合型別"
        };

        var result = await devAgent.ExecuteTaskAsync(
            task,
            owner: "my-org",
            repo: "my-repo",
            rules: rules);

        if (result.Success)
            Console.WriteLine($"任務完成，{result.Message}");
        else
            Console.WriteLine($"任務失敗：{result.Message}");
    }
}
```

### 分步驟執行（需要中間確認計畫）

```csharp
// Step 1：產生計畫供人工審核
var plan = await devAgent.BuildPlanAsync(task, "my-org", "my-repo", rules);

Console.WriteLine($"計畫類型：{plan.TaskType}");
Console.WriteLine($"Branch：{plan.BranchName}");
Console.WriteLine($"摘要：{plan.Summary}");

// Step 2：確認後執行
if (await ConfirmPlanAsync(plan))
{
    var prUrl = await devAgent.ExecuteAsync(task, plan, "my-org", "my-repo");
    Console.WriteLine($"PR 已開啟：{prUrl}");
}
```

---

## 注意事項

> ⚠️ **本地目錄清理**：`ExecuteAsync` 的 `finally` 區塊會呼叫 `gitHubService.CleanupLocalRepo(localPath)` 清除 Clone 的本地目錄，確保不留下暫存檔案。

> ⚠️ **LLM 解析重試**：`BuildPlanAsync` 最多重試 2 次解析 JSON，若均失敗會回傳 `TaskType = "unknown"` 的計畫，**不會拋出例外**，呼叫端應自行判斷 `TaskType` 是否合法。

> ℹ️ **Code Review 流程差異**：當 `plan.TaskType == "code_review"` 時，`ExecuteAsync` 不會執行 Git 操作，而是直接回傳 LLM 產出的文字內容，回傳值並非 PR URL。
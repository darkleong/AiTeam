# RequirementsAgentService

## 類別概覽

**命名空間：** `AiTeam.Bot.Agents`

`RequirementsAgentService` 是需求分析師 Agent（Requirements Analyst Agent），實作 `IAgentExecutor` 介面。其核心職責為透過 LLM 將原始需求文字拆解為結構化的 GitHub Issues，並依照工作流程逐步建立於指定的 GitHub 儲存庫中。

### 主要功能

- 呼叫 LLM 分析需求，產出符合 GitHub Issues 格式的 JSON 清單
- 支援「分析預覽」與「實際建立」兩階段流程，供 CommandHandler 在建立前進行雙層確認
- 每個步驟均記錄執行日誌（`TaskLog`）並透過 `DashboardPushService` 推播即時狀態

### 依賴服務

| 服務 | 用途 |
|------|------|
| `LlmProviderFactory` | 建立 LLM 提供者，執行文字補全 |
| `GitHubService` | 在 GitHub 上建立 Issue |
| `TaskRepository` | 儲存任務與執行日誌 |
| `DashboardPushService` | 向儀表板推播 Agent 狀態 |
| `ILogger<RequirementsAgentService>` | 記錄系統日誌 |

---

## Public 方法

### `ExecuteTaskAsync`

```csharp
public async Task<AgentExecutionResult> ExecuteTaskAsync(
    TaskItem task,
    string owner,
    string repo,
    IReadOnlyList<string> rules,
    CancellationToken cancellationToken = default)
```

Agent 主要進入點，完整執行「需求分析 → 建立 GitHub Issues」全流程。

#### 參數

| 參數名稱 | 型別 | 說明 |
|----------|------|------|
| `task` | `TaskItem` | 待執行的任務，包含標題與描述等需求內容 |
| `owner` | `string` | GitHub 儲存庫擁有者（使用者名稱或組織名稱） |
| `repo` | `string` | GitHub 儲存庫名稱 |
| `rules` | `IReadOnlyList<string>` | 執行規則清單（本 Agent 保留供未來使用） |
| `cancellationToken` | `CancellationToken` | 非同步取消權杖，預設為 `default` |

#### 回傳值

`Task<AgentExecutionResult>`：執行結果，包含：
- 成功時：`Success = true`，訊息說明已建立的 Issue 數量，以及第一個 Issue 的 URL
- 失敗時：`Success = false`，錯誤訊息說明失敗原因

#### 執行流程

```
1. 推播狀態 → "running"
2. 呼叫 AnalyzeOnlyAsync() 取得 Issue 預覽清單
3. 若清單為空 → 回傳失敗
4. 呼叫 CreateIssuesFromPreviewAsync() 逐一建立 GitHub Issues
5. 推播狀態 → "idle"
6. 例外發生時推播 → "error"，回傳失敗結果
```

---

## Internal 方法

> 以下方法供 `CommandHandler` 在雙層確認流程中使用，不對外公開。

---

### `AnalyzeOnlyAsync`

```csharp
internal async Task<List<RequirementIssuePreview>> AnalyzeOnlyAsync(
    TaskItem task,
    CancellationToken cancellationToken = default)
```

**僅執行 LLM 需求分析**，回傳 Issue 預覽清單，**不會實際建立 GitHub Issues**。

供 CommandHandler 在第三層確認（`req_yes`）前，取得預覽內容供使用者確認。

#### 參數

| 參數名稱 | 型別 | 說明 |
|----------|------|------|
| `task` | `TaskItem` | 待分析的任務 |
| `cancellationToken` | `CancellationToken` | 非同步取消權杖 |

#### 回傳值

`Task<List<RequirementIssuePreview>>`：LLM 分析產出的 Issue 預覽清單。若 LLM 兩次解析均失敗，回傳空清單。

---

### `CreateIssuesFromPreviewAsync`

```csharp
internal async Task<AgentExecutionResult> CreateIssuesFromPreviewAsync(
    TaskItem task,
    string owner,
    string repo,
    IReadOnlyList<RequirementIssuePreview> issues,
    CancellationToken cancellationToken = default)
```

根據**已確認的預覽清單**，實際於 GitHub 建立 Issues。

供 CommandHandler 在使用者確認（`req_yes`）後呼叫。

#### 參數

| 參數名稱 | 型別 | 說明 |
|----------|------|------|
| `task` | `TaskItem` | 對應的任務物件（用於日誌記錄） |
| `owner` | `string` | GitHub 儲存庫擁有者 |
| `repo` | `string` | GitHub 儲存庫名稱 |
| `issues` | `IReadOnlyList<RequirementIssuePreview>` | 已確認的 Issue 預覽清單 |
| `cancellationToken` | `CancellationToken` | 非同步取消權杖 |

#### 回傳值

`Task<AgentExecutionResult>`：執行結果，包含：
- 成功時：`Success = true`，說明已建立的 Issue 數量，以及第一個 Issue 的 URL
- 失敗時：`Success = false`，錯誤訊息

---

## 輔助型別

### `RequirementIssuePreview`

```csharp
internal sealed record RequirementIssuePreview(
    string Title,
    string Body,
    IReadOnlyList<string> Labels);
```

需求分析結果的 Issue 預覽資料，供 CommandHandler 雙層確認流程使用。

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Title` | `string` | Issue 標題（動詞開頭的繁體中文描述） |
| `Body` | `string` | Issue 內容，包含背景說明與驗收條件 |
| `Labels` | `IReadOnlyList<string>` | Issue 標籤清單，例如 `feature`、`P1` |

---

## LLM 系統提示規格

Agent 使用以下規則指示 LLM 產出結構化輸出：

**輸出格式**：僅回傳 JSON Array，不加任何說明文字

```json
[
  {
    "title": "動詞開頭的具體標題（繁體中文）",
    "body": "## 背景\n說明此需求的背景...\n\n## 驗收條件\n- [ ] 條件一\n- [ ] 條件二",
    "labels": ["feature", "P1"]
  }
]
```

**Labels 規則**

| 類型標籤 | 說明 |
|----------|------|
| `feature` | 功能需求 |
| `bug` | 錯誤修復 |
| `chore` | 維護工作 |

| 優先度標籤 | 說明 |
|------------|------|
| `P0` | 緊急 |
| `P1` | 高優先度 |
| `P2` | 一般 |
| `P3` | 低優先度 |

> LLM 回應解析採容錯機制，最多重試 **2 次**。若兩次均無法解析出有效 JSON Array，則回傳空清單。

---

## 使用範例

### 完整執行流程（直接建立 Issues）

```csharp
var result = await requirementsAgentService.ExecuteTaskAsync(
    task: taskItem,
    owner: "my-org",
    repo: "my-repo",
    rules: [],
    cancellationToken: cancellationToken);

if (result.Success)
{
    Console.WriteLine($"執行成功：{result.Message}");
    Console.WriteLine($"第一個 Issue URL：{result.ResultUrl}");
}
else
{
    Console.WriteLine($"執行失敗：{result.Message}");
}
```

### 雙層確認流程（CommandHandler 使用）

```csharp
// 第一階段：僅分析，取得預覽清單
var previews = await requirementsAgentService.AnalyzeOnlyAsync(
    task: taskItem,
    cancellationToken: cancellationToken);

// 顯示預覽給使用者確認
foreach (var preview in previews)
{
    Console.WriteLine($"- [{string.Join(", ", preview.Labels)}] {preview.Title}");
}

// 第二階段：使用者確認後，根據預覽清單實際建立 Issues
var result = await requirementsAgentService.CreateIssuesFromPreviewAsync(
    task: taskItem,
    owner: "my-org",
    repo: "my-repo",
    issues: previews,
    cancellationToken: cancellationToken);
```
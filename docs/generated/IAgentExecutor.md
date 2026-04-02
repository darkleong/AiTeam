# IAgentExecutor 介面文件

## 概覽

`IAgentExecutor` 定義了所有可被 CEO Agent 分派任務的執行 Agent 的標準契約。透過此介面，`CommandHandler` 僅依賴抽象，不需要知道具體 Agent 的實作類型，從而實現動態分派的架構設計。

**命名空間：** `AiTeam.Bot.Agents`

---

## 介面定義

### `IAgentExecutor`

所有可執行任務的 Agent 必須實作此介面，負責管理任務規劃與執行的完整生命週期。

#### 方法

##### `ExecuteTaskAsync`

執行由 CEO 分派的任務，並回傳結果摘要。實作方自行負責內部的規劃與執行步驟。

```csharp
Task<AgentExecutionResult> ExecuteTaskAsync(
    TaskItem task,
    string owner,
    string repo,
    IReadOnlyList<string> rules,
    CancellationToken cancellationToken = default);
```

| 參數 | 型別 | 說明 |
|------|------|------|
| `task` | `TaskItem` | CEO 分派的任務項目，包含任務描述與相關資訊 |
| `owner` | `string` | GitHub 儲存庫的擁有者名稱 |
| `repo` | `string` | GitHub 儲存庫名稱 |
| `rules` | `IReadOnlyList<string>` | 任務執行時須遵守的規則清單 |
| `cancellationToken` | `CancellationToken` | 用於取消非同步操作的權杖（預設為 `default`） |

**回傳值：** `Task<AgentExecutionResult>`
> 包含執行成功狀態、摘要描述及輸出連結的結果物件。

---

## 相關型別

### `AgentExecutionResult`

Agent 執行完畢後回傳的結果資料，用於告知呼叫方執行狀態及輸出資訊。

```csharp
public record AgentExecutionResult(bool Success, string Summary, string? OutputUrl = null);
```

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Success` | `bool` | 指示任務是否執行成功 |
| `Summary` | `string` | 供 Discord Embed 顯示用的單行摘要文字 |
| `OutputUrl` | `string?` | 任務產出的連結（如 PR URL、Issue URL），無輸出時為 `null` |

---

### `AgentDescriptor`

Agent 的描述資訊，供 CEO 系統提示參考，並作為依賴注入（DI）動態分派的依據。

```csharp
public record AgentDescriptor(string Name, string Description);
```

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Name` | `string` | Agent 名稱，須與 DI 容器中的 Key 一致 |
| `Description` | `string` | Agent 的職責描述，供 CEO LLM 判斷應將任務分派給哪個 Agent |

---

## 使用範例

### 實作 IAgentExecutor

```csharp
public class CodeReviewAgent : IAgentExecutor
{
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        // 實作 Code Review 的規劃與執行邏輯
        var prUrl = await SubmitReviewAsync(task, owner, repo, rules, cancellationToken);

        return new AgentExecutionResult(
            Success: true,
            Summary: $"Code Review 已完成，共檢視 {task.Title}",
            OutputUrl: prUrl
        );
    }
}
```

### 註冊至 DI 容器

```csharp
// 使用 Keyed DI 註冊，Name 須與 AgentDescriptor.Name 一致
builder.Services.AddKeyedSingleton<IAgentExecutor, CodeReviewAgent>("code-review");

// 對應的 AgentDescriptor
var descriptor = new AgentDescriptor(
    Name: "code-review",
    Description: "負責對指定 Pull Request 進行程式碼審查，並提出改善建議。"
);
```

### 透過 AgentDescriptor 動態分派

```csharp
// CEO 根據 AgentDescriptor 列表決定分派對象
var selectedAgent = serviceProvider.GetRequiredKeyedService<IAgentExecutor>(descriptor.Name);

var result = await selectedAgent.ExecuteTaskAsync(task, owner, repo, rules, cancellationToken);

if (result.Success)
{
    Console.WriteLine($"執行成功：{result.Summary}");
    if (result.OutputUrl is not null)
        Console.WriteLine($"輸出連結：{result.OutputUrl}");
}
```

---

## 架構說明

```
CEO Agent
   │
   │ 根據 AgentDescriptor 選擇分派對象
   ▼
IAgentExecutor  ◄─── CommandHandler 僅依賴此介面
   │
   ├── CodeReviewAgent
   ├── FeatureDevelopAgent
   └── BugFixAgent
```

> **設計原則：** `CommandHandler` 透過 `IAgentExecutor` 介面與各 Agent 解耦，新增 Agent 時只需實作介面並註冊至 DI 容器，無需修改既有的分派邏輯，符合**開放封閉原則（OCP）**。
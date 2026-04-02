# DocAgentService

## 類別概覽

`DocAgentService` 是 AiTeam Bot 的文件自動化代理（Documentation Agent），實作 `IAgentExecutor` 介面。

此服務的主要職責為：
- 自動掃描指定 GitHub 儲存庫路徑下的 C# 原始碼檔案
- 透過 LLM（大型語言模型）產生對應的 **Markdown 技術文件** 或 **XML 文件註解**
- 將產出的文件提交至新分支，並自動開啟 Pull Request

---

## 建構子參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `providerFactory` | `LlmProviderFactory` | LLM 提供者工廠，用於取得對應的 AI 完成服務 |
| `gitHubService` | `GitHubService` | GitHub 操作服務（檔案列舉、Clone、Push、PR 等） |
| `taskRepository` | `TaskRepository` | 任務資料儲存庫，用於紀錄執行日誌與狀態持久化 |
| `dashboardPush` | `DashboardPushService` | 儀表板即時推送服務 |
| `logger` | `ILogger<DocAgentService>` | 日誌記錄器 |

---

## Public 方法

### `ExecuteTaskAsync`

執行文件生成任務的主要入口點。

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
| `task` | `TaskItem` | 任務物件，包含任務 ID、標題與描述等資訊 |
| `owner` | `string` | GitHub 儲存庫擁有者（使用者名稱或組織名稱） |
| `repo` | `string` | GitHub 儲存庫名稱 |
| `rules` | `IReadOnlyList<string>` | 執行規則清單（保留供後續擴充使用） |
| `cancellationToken` | `CancellationToken` | 非同步取消令牌，預設為 `default` |

#### 回傳值

`Task<AgentExecutionResult>` — 包含執行結果的非同步任務：

| 欄位 | 說明 |
|------|------|
| `Success` | 布林值，表示任務是否成功完成 |
| `Message` | 執行結果的說明訊息 |
| `PrUrl` | （成功時）已開啟的 Pull Request URL |

#### 執行流程

```
1. 更新任務狀態為 running
2. 從任務描述或標題解析目標路徑前綴
3. 列舉該路徑下所有 .cs 檔案
4. Clone / Pull 目標儲存庫
5. 建立並切換至新分支（docs/auto-{taskId前8碼}）
6. 對每個 .cs 檔案呼叫 LLM 產生文件
7. Commit 所有變更並 Push 至遠端
8. 開啟 Pull Request
9. 清理本地儲存庫（finally 區塊）
```

#### 執行模式判斷

方法會依據任務標題自動判斷執行模式：

| 模式 | 觸發條件 | 輸出內容 |
|------|----------|----------|
| **XML 註解模式** | 標題包含 `XML` 或 `xml 註解` | 在原始 `.cs` 檔案中補充 XML 文件註解 |
| **Markdown 文件模式** | 預設 | 於 `docs/generated/` 目錄產生對應 `.md` 文件 |

---

## 使用範例

```csharp
// 注入 DocAgentService（通常由 DI 容器管理）
var docAgent = serviceProvider.GetRequiredService<DocAgentService>();

// 建立任務（產生 Markdown 文件）
var task = new TaskItem
{
    Id = Guid.NewGuid(),
    Title = "產生 Agents 目錄文件",
    Description = "src/AiTeam.Bot/Agents"
};

var result = await docAgent.ExecuteTaskAsync(
    task: task,
    owner: "my-org",
    repo: "my-repo",
    rules: Array.Empty<string>(),
    cancellationToken: CancellationToken.None
);

if (result.Success)
{
    Console.WriteLine($"文件已產出，PR：{result.PrUrl}");
}
else
{
    Console.WriteLine($"執行失敗：{result.Message}");
}
```

```csharp
// 建立任務（補充 XML 註解模式）
var xmlTask = new TaskItem
{
    Id = Guid.NewGuid(),
    Title = "補充 XML 註解 - Services 目錄",
    Description = "src/AiTeam.Bot/Services"
};

var result = await docAgent.ExecuteTaskAsync(
    task: xmlTask,
    owner: "my-org",
    repo: "my-repo",
    rules: Array.Empty<string>()
);
```

---

## 注意事項

- 若指定路徑下**找不到任何 `.cs` 檔案**，方法將立即返回失敗結果，不進行後續操作。
- 本地 Clone 的儲存庫無論成功或失敗，都會在 `finally` 區塊中透過 `CleanupLocalRepo` 自動清除。
- 分支命名格式為 `docs/auto-{taskId前8碼}`，可由 Task ID 追溯對應任務。
- Markdown 文件預設輸出至儲存庫內的 `docs/generated/` 目錄。
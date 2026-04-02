# QaAgentService 技術文件

## 類別概覽

**命名空間**：`AiTeam.Bot.Agents`
**檔案路徑**：`src/AiTeam.Bot/Agents/QaAgentService.cs`

`QaAgentService` 是一個 QA 自動化代理人服務，實作 `IAgentExecutor` 介面。其主要職責為：

1. 讀取指定 GitHub Pull Request 中變更的 C# 原始碼檔案
2. 透過 LLM（大型語言模型）自動產生 **xUnit + NSubstitute + FluentAssertions** 風格的單元測試
3. 將產生的測試程式碼提交至新分支，並自動開啟測試 PR

### 建構子參數

| 參數 | 型別 | 說明 |
|------|------|------|
| `providerFactory` | `LlmProviderFactory` | LLM 提供者工廠，用於建立 AI 完成請求 |
| `gitHubService` | `GitHubService` | GitHub 操作服務（Clone、Push、PR 等） |
| `taskRepository` | `TaskRepository` | 任務資料存取層，負責記錄日誌與儲存 |
| `dashboardPush` | `DashboardPushService` | 即時推送代理人狀態至儀表板 |
| `logger` | `ILogger<QaAgentService>` | 日誌記錄器 |

---

## Public 方法

### `ExecuteTaskAsync`

執行 QA Agent 主要流程：解析 PR 編號、取得變更檔案、產生測試程式碼、建立測試 PR。

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
| `task` | `TaskItem` | 任務項目，標題或描述須包含 PR 編號（格式：`PR #123`） |
| `owner` | `string` | GitHub 儲存庫擁有者（使用者名稱或組織名稱） |
| `repo` | `string` | GitHub 儲存庫名稱 |
| `rules` | `IReadOnlyList<string>` | 代理人規則清單（保留參數，目前未使用） |
| `cancellationToken` | `CancellationToken` | 取消操作的 Token，預設為 `default` |

#### 回傳值

`Task<AgentExecutionResult>`：包含執行結果的非同步任務。

| 情境 | `Success` | `Message` | `Url` |
|------|-----------|-----------|-------|
| 成功開啟測試 PR | `true` | 說明產生的測試檔數量 | 測試 PR 的 URL |
| 無法取得 PR 編號 | `false` | 提示格式錯誤訊息 | 無 |
| PR 無 .cs 變更檔案 | `false` | 說明略過原因 | 無 |
| 執行發生例外 | `false` | 例外訊息內容 | 無 |

#### 執行流程

```
1. 設定任務狀態為 "running"
2. 從任務標題/描述解析 PR 編號
3. 取得 PR 的 head 分支與變更檔案清單
4. 過濾出非測試用途的 .cs 檔案
5. Clone/Pull 儲存庫至本地
6. 建立並切換至新分支（test/qa-{taskId前8碼}）
7. 逐一為每個 .cs 檔案呼叫 LLM 產生測試程式碼
8. Commit 並 Push 測試程式碼
9. 開啟測試 PR 並回報結果
10. （finally）清理本地暫存儲存庫
```

#### 檔案過濾規則

以下檔案會被排除，不進行測試產生：

- 副檔名非 `.cs` 的檔案
- 以 `Tests.cs` 結尾的檔案
- 以 `Spec.cs` 結尾的檔案
- 路徑包含 `.Tests/` 的檔案
- 路徑包含 `.Test/` 的檔案

---

## 使用範例

### 任務描述格式

任務的 `Title` 或 `Description` 欄位須包含 PR 編號，支援以下格式：

```
PR #123
PR#456
pr #789   （不區分大小寫）
```

### 呼叫範例

```csharp
var result = await qaAgentService.ExecuteTaskAsync(
    task: new TaskItem
    {
        Id = Guid.NewGuid(),
        Title = "QA：針對 PR #42 產生測試",
        Description = "請為 PR #42 的變更自動產生單元測試"
    },
    owner: "my-org",
    repo: "my-repository",
    rules: Array.Empty<string>(),
    cancellationToken: CancellationToken.None
);

if (result.Success)
{
    Console.WriteLine($"測試 PR 已建立：{result.Url}");
}
else
{
    Console.WriteLine($"執行失敗：{result.Message}");
}
```

### 產生測試的輸出路徑規則

原始碼路徑會依以下規則轉換為測試檔路徑：

| 原始碼路徑 | 產生的測試路徑 |
|-----------|--------------|
| `src/Services/OrderService.cs` | `tests/Generated/src/Services/OrderServiceTests.cs` |
| `src/Agents/QaAgentService.cs` | `tests/Generated/src/Agents/QaAgentServiceTests.cs` |

### LLM 測試產生規範

產生的測試程式碼遵循以下規則：

- **測試類別命名**：`{原始類別名稱}Tests`
- **測試方法命名**：繁體中文，格式為 `中文_條件_期望結果`
- **Mock 建立**：使用 `NSubstitute.Substitute.For<T>()`
- **斷言語法**：使用 `FluentAssertions` 的 `.Should()` 系列方法
- **覆蓋率要求**：每個 public 方法至少 2 個測試（happy path + edge case）

```csharp
// 產生的測試範例（示意）
public class OrderServiceTests
{
    [Fact]
    public void 建立訂單_傳入有效資料_應回傳成功結果()
    {
        // Arrange
        var repo = Substitute.For<IOrderRepository>();
        var sut = new OrderService(repo);

        // Act
        var result = sut.CreateOrder(new OrderDto { ... });

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }
}
```

---

## 注意事項

> ⚠️ **PR 編號格式**：任務標題或描述中必須包含 `PR #數字` 格式，否則 Agent 將直接回傳失敗結果。

> ⚠️ **本地儲存庫清理**：無論執行成功或失敗，`finally` 區塊皆會自動清理 Clone 至本地的儲存庫，確保不佔用磁碟空間。

> ℹ️ **分支命名**：自動建立的分支名稱格式為 `test/qa-{taskId前8碼}`，例如 `test/qa-a1b2c3d4`。
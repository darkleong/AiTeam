# Stage 5 — 動態 Agent 框架 + 三個新 Agent

> 所屬專案：AI 團隊實作總規劃
> 狀態：✅ 已完成（2026-04-01）
> 最後更新：2026-04-01

---

## 目標

建立動態 Agent 框架（從 DB 載入、keyed DI 分派），並新增 QA、Documentation、Requirements 三個 Agent。新增 Agent 後只需 DB 新增記錄 + 實作 Service + 註冊 keyed DI，CEO 自動偵測，不需修改任何其他程式碼。

---

## 實作重點紀錄

### Phase 1：動態 Agent 框架

#### IAgentExecutor 介面
新增 `src/AiTeam.Bot/Agents/IAgentExecutor.cs`，定義統一執行介面：

```csharp
public interface IAgentExecutor
{
    Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task, string owner, string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default);
}
public record AgentExecutionResult(bool Success, string Summary, string? OutputUrl = null);
public record AgentDescriptor(string Name, string Description);
```

#### Keyed DI 註冊（Program.cs）
```csharp
builder.Services.AddKeyedScoped<IAgentExecutor, DevAgentService>(AgentNames.Dev);
builder.Services.AddKeyedSingleton<IAgentExecutor, OpsAgentService>(AgentNames.Ops);
builder.Services.AddKeyedScoped<IAgentExecutor, QaAgentService>(AgentNames.Qa);
builder.Services.AddKeyedScoped<IAgentExecutor, DocAgentService>(AgentNames.Doc);
builder.Services.AddKeyedScoped<IAgentExecutor, RequirementsAgentService>(AgentNames.Requirements);
```

#### AgentRepository（AiTeam.Data）
查詢啟用中非 CEO 的 Agent，供 CommandHandler 與 WebhookController 動態載入：
```csharp
await db.AgentConfigs
    .AsNoTracking()
    .Where(a => a.IsActive && a.Name != "CEO")
    .OrderBy(a => a.Name)
    .ToListAsync(cancellationToken);
```

#### CommandHandler 重構
- 移除 `["Dev", "Ops"]` 硬編碼 → 改為 `AgentRepository.GetActiveExecutorAgentsAsync()`
- 移除 if/else Agent 分派 → 改為 `GetKeyedService<IAgentExecutor>(targetAgent)`
- WebhookController 同步更新

#### EF Core Migrations
- `AddAgentConfigDescription`：新增 `AgentConfig.Description` 欄位
- `AddTaskItemDescription`：新增 `TaskItem.Description` 欄位（nullable）

#### DbSeeder（共用，Bot 與 Dashboard 皆呼叫）
啟動時 idempotent seed 5 筆 AgentConfig：Dev/Ops 預設啟用，QA/Doc/Requirements 預設停用。

#### CeoAgentService
- 參數從 `IReadOnlyList<string>` 改為 `IReadOnlyList<AgentDescriptor>`
- System Prompt 中包含每個 Agent 的名稱與描述，協助 CEO 正確分派

---

### Phase 2：三個新 Agent

#### QaAgentService
1. 從 task 取得 PR number
2. `GitHubService.GetPullRequestFilesAsync` 取得變更檔案
3. LLM 逐檔生成 xUnit + NSubstitute + FluentAssertions 測試
4. 開 PR 至 `test/qa-{taskId}` 分支

#### DocAgentService
1. 從任務描述取得目標路徑前綴
2. `GitHubService.ListFilesAsync` 列舉 `.cs` 檔
3. LLM 生成 Markdown 文件（預設）或 XML comments
4. 開 PR 至 `docs/auto-{taskId}` 分支

#### RequirementsAgentService
1. LLM 分析需求 → 回傳 JSON 陣列 `[{ title, body, labels }]`
2. 逐一呼叫 `GitHubService.CreateIssueAsync`（Octokit）
3. 回傳第一個 Issue URL

---

### Dashboard 擴充

#### IsActive 開關（AgentSettings.razor）
- 每個 Agent 卡片新增 checkbox 切換啟用/停用
- 即時寫回 DB（`DashboardAgentService.UpdateIsActiveAsync`）
- 不需重啟 Bot，下次 `/task` 時生效

#### AgentConfigDto 新增 Description
顯示在 Agent 卡片中，讓 Dashboard 使用者了解各 Agent 職責。

---

### Notion Rules 欄位對應

| 表格 | 欄位 | 類型 |
|------|------|------|
| Rules | `Name`（標題）、`Agent`（Select）、`Rule Content`（Text） | 讀取規則 |
| Agent Status | `Agent Name`（標題）、`Trust Level`（Number） | 讀寫信任等級 |
| Task Summary | `Name`（標題）、`Agent`（Text）、`Command`（Text）、`Result`（Text）、`Date`（Date） | 寫入任務摘要 |

`全域` Agent 的規則套用至所有 Agent；新 Agent 在 Notion Rules DB 加入 select 選項後即可設定規則。

---

### 陷阱紀錄

| 問題 | 原因 | 解法 |
|------|------|------|
| `TelerikSlider @bind-Value="@_dict[key]"` 導致 circuit crash | Blazor `FieldIdentifier` 不支援 dictionary 索引運算式 | 改用 `Value` + `ValueChanged` lambda |
| `TelerikSlider Step` 參數找不到 | Telerik v8.x 改名為 `SmallStep` | 將 `Step` 改為 `SmallStep` |
| Notion Rules 查詢拋出 exception（新 Agent）| Notion select 欄位不存在時直接 throw，非回傳空陣列 | catch `NotionApiException` when message contains "not found for property"，降級為 Warning |
| `AgentConfig` 型別衝突 | Bot 有 `AiTeam.Bot.Configuration.AgentConfig` 與 `AiTeam.Data.AgentConfig` 同名 | 在 Program.cs 使用完整命名空間 `AiTeam.Data.AgentConfig` |
| PostgreSQL 重啟資料消失 | Aspire 預設不持久化 volume | AppHost 加 `.WithDataVolume("aiteam-postgres-data")` |

---

## 新增 Agent 標準流程（完成後確立）

1. DB 新增 `AgentConfig` 記錄（`IsActive` 預設 `false`）
2. 實作 `XxxAgentService : IAgentExecutor`
3. `Program.cs` 加 `AddKeyedScoped<IAgentExecutor, XxxAgentService>(AgentNames.Xxx)`
4. `AgentNames.cs` 加常數
5. `appsettings.json` 加模型設定
6. Notion Rules DB 的 `Agent` select 欄位加新選項
7. Dashboard 切換 `IsActive = true`
8. CEO 下次被呼叫時自動感知新成員

---

## 驗收狀態

| 項目 | 狀態 |
|------|------|
| `dotnet build` 無錯誤 | ✅ |
| Aspire 啟動，5 筆 Agent seed 資料 | ✅ |
| Dashboard Agent 設定頁面正常顯示 | ✅ |
| IsActive 開關即時寫回 DB | ✅ |
| Notion 規則欄位對應正確 | ✅ |
| Discord 端對端測試（/task 全流程）| ✅（Stage 6 第 12 項驗收完成）|

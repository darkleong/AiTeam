# Stage 17 — Mock Mode（模擬模式）

> 版本：v2.0
> 建立日期：2026-04-08
> 完成日期：2026-04-08
> 狀態：✅ 已完成

---

## 目標

在 Bot 中加入 Mock Mode，讓 Dashboard / Discord 的功能開發與測試可以在不消耗 API 費用的情況下進行。透過 Runtime 代理模式切換，Dashboard 一鍵開關，即時生效無需重啟容器。

---

## 一、架構設計

### 1.1 核心概念

只需 mock 兩個元件，其餘所有流程（WorkflowEngine、TaskGroupService、CommandHandler、Discord、SignalR、DB 寫入）照常運作：

| 元件 | 正式模式 | 模擬模式 |
|------|---------|---------|
| Claude Code 呼叫 | `ClaudeCodeService`（真的跑 `claude -p`） | `MockClaudeCodeService`（回傳預設結果 + 延遲） |
| LLM 直接呼叫 | `AnthropicProvider`（真的呼叫 API） | `MockLlmProvider`（回傳預設 JSON + 延遲） |

### 1.2 Runtime 切換（代理模式）

不用 DI 啟動時切換（需重啟），而是 Runtime 代理模式：

```
ClaudeCodeProxy : IClaudeCodeService
  ├── _real: ClaudeCodeService
  ├── _mock: MockClaudeCodeService
  └── _settings: AppSettingsService  →  GetBoolAsync("MockMode", false)
      每次呼叫時檢查旗標，決定走真的還是假的
```

`LlmProviderFactory` 同理：`Create()` 方法內檢查 MockMode 旗標，決定建立真的 Provider 還是 MockLlmProvider。

### 1.3 旗標存儲

使用現有的 `app_settings` 表（Stage 8 已建立）：

```
Key: "MockMode"
Value: "true" / "false"
```

Bot 端透過 `AppSettingsService.GetBoolAsync("MockMode", false)` 讀取，5 分鐘 TTL cache。

---

## 二、新增元件

### 2.1 `IClaudeCodeService` 介面

```csharp
public interface IClaudeCodeService
{
    Task<ClaudeCodeResult> RunAsync(...);
    Task<ClaudeCodeResult> RunVictoriaAsync(...);
    Task<ClaudeCodeResult> RunReadOnlyAsync(...);
    Task<ClaudeCodeResult> RunQaAsync(...);
    Task<ClaudeCodeResult> RunReviewAsync(...);
}
```

### 2.2 `MockClaudeCodeService : IClaudeCodeService`

每個方法回傳預設 `ClaudeCodeResult`，延遲 **30~60 秒**（模擬真實執行時間，讓 Dashboard 可觀測）：

| 方法 | 回傳內容（格式必須與真實解析一致） |
|------|--------------------------------|
| `RunAsync` | `[MOCK] 開發完成...\nhttps://github.com/mock/repo/pull/999` |
| `RunVictoriaAsync` | `<ACTION>{"action":"reply","reply":"[MOCK]...","require_confirmation":false}</ACTION>` |
| `RunReadOnlyAsync` | `[MOCK] 探索完成\n[{"title":"[MOCK]...","body":"...","labels":["enhancement"]}]` |
| `RunQaAsync` | `{"generated":["[MOCK] MockFeatureTest.cs"],"summary":"[MOCK] QA 測試通過，0 個失敗"}` |
| `RunReviewAsync` | `{"critical":[],"warning":[],"info":[],"summary":"[MOCK] 模擬審查通過","impact":"[MOCK] 無影響範圍"}` |

### 2.3 `MockLlmProvider : ILlmProvider`

依 `systemPrompt` 關鍵字偵測呼叫情境，回傳對應格式：

| 偵測關鍵字 | 回傳格式 |
|-----------|---------|
| `decision` / `approve` / `revise` / `Petra` / `審核` | `{"decision":"approve","summary":"[MOCK]...","issues":[]}` |
| `Victoria` / `CEO` / `delegate` / `分類` | `{"action":"reply","reply":"[MOCK]...","require_confirmation":false}` |
| `Dev` / `branch` / `commit` / `計畫` | DevPlan JSON |
| 其他 | `[MOCK] 模擬 LLM 回應` |

延遲：**30~60 秒**（與 MockClaudeCodeService 一致，有意跳過 TokenTrackingProvider，避免產生假的 Token 統計污染 Dashboard）。

### 2.4 `ClaudeCodeProxy : IClaudeCodeService`

代理類別，每次呼叫都 `await settings.GetBoolAsync("MockMode", false)` 動態決定路由：

```csharp
public class ClaudeCodeProxy(ClaudeCodeService real, MockClaudeCodeService mock,
    AppSettingsService settings, ILogger<ClaudeCodeProxy> logger) : IClaudeCodeService
{
    public async Task<ClaudeCodeResult> RunAsync(...)
    {
        if (await settings.GetBoolAsync("MockMode", false)) return await mock.RunAsync(...);
        return await real.RunAsync(...);
    }
    // 其餘 4 個方法同理
}
```

---

## 三、修改的元件

### 3.1 `LlmProviderFactory`

`Create()` 方法開頭加入 MockMode 判斷：

```csharp
public ILlmProvider Create(string agentName)
{
    if (_appSettings.GetBoolAsync("MockMode", false).GetAwaiter().GetResult())
        return new MockLlmProvider();
    // 現有邏輯不變...
}
```

採用 `GetAwaiter().GetResult()` 因為 `Create()` 是同步方法（AppSettingsService 有 in-memory cache，不會真的阻塞）。

### 3.2 `Program.cs` — DI 註冊

```csharp
builder.Services.AddSingleton<ClaudeCodeService>();
builder.Services.AddSingleton<MockClaudeCodeService>();
builder.Services.AddSingleton<IClaudeCodeService, ClaudeCodeProxy>();
```

### 3.3 Agent Services — 型別更新

以下 8 個 Service 的 constructor 改為注入 `IClaudeCodeService`：
`DevAgentService`、`ReviewerAgentService`、`QaAgentService`、`RequirementsAgentService`、`DesignerAgentService`、`DocAgentService`、`PmAgentService`、`CeoAgentService`。

### 3.4 MockMode Early Return（防止真實 GitHub API 呼叫）

`Dev`、`Reviewer`、`QA`、`Doc`、`Rosa` 的 `ExecuteTaskAsync` 開頭加 early return，跳過所有 GitHub 操作：

```csharp
if (await appSettings.GetBoolAsync("MockMode", false, cancellationToken))
{
    AddLog(task, "[MOCK] ... 模擬執行完成", "done");
    // ...回傳假結果
    return new AgentExecutionResult(true, "[MOCK] ...");
}
```

### 3.5 Dashboard — AgentSettings.razor

新增 Mock Mode 開關卡（橘色邊框，與 SkipCeoConfirm 模式一致），5 分鐘 TTL 提示。

### 3.6 `/mock` 斜線指令

新增 `/mock` 指令供 MockMode 測試用：

| 選項 | 起始步驟 | 流程 |
|------|---------|------|
| `新功能（new_feature）` | Dev_plan | Dev_plan → Dev → Reviewer → Petra → QA → Doc |
| `新功能（含提案）` | FireMockProposalAndContinueAsync | Requirements/PM/Designer/PM（mock done）→ Dev_plan → ... |
| `Bug 修復（bug_fix）` | Dev | Dev → Reviewer → Petra → QA |
| `技術改善（tech_improvement）` | Dev_plan | Dev_plan → Dev → Reviewer → Petra → QA |

指令設有 MockMode 守衛，未啟用時拒絕執行。

### 3.7 `TaskGroupService.FireMockProposalAndContinueAsync`

新增方法，模擬「新功能含提案」的完整流程：

```
建立 Requirements / PM（Rosa review）/ Designer / PM（Demi review）四個 mock done TaskItem
    ↓
FireStepsAsync([new WorkflowStep("Dev_plan")])  ← 從 proposal_approved 後正式啟動
```

**為什麼不從 WorkflowEngine 的 `Requirements` 步驟啟動：**
`NewFeatureTable` 沒有 `["Requirements"]` 的映射，`GetDecision` 會回傳 `Nothing`，流程在 Rosa 完成後直接停住。提案流程（Rosa → Petra → Demi → Petra → 老闆確認）是 `CommandHandler.ShowProposalAsync` 的互動式 Discord 流程，不在 WorkflowEngine 裡，故需要這個專用方法模擬。

---

## 四、踩坑紀錄

### 坑一：QA / Doc 缺少 MockMode Early Return（GitHub API 404）

**現象**：新功能 mock 流程跑到 QA / Doc 時失敗，log 顯示 GitHub 404。

**根因**：QA 和 Doc 在呼叫 Claude Code 之前，都先呼叫 `gitHubService.GetPullRequestFilesAsync(999)`，對假 PR #999 發出真實 GitHub API 請求，回傳 404。

**修正**：在 `QaAgentService.ExecuteTaskAsync` 和 `DocAgentService.ExecuteTaskAsync` 開頭補上 MockMode early return，在任何 GitHub 呼叫之前直接回傳 mock 結果。

---

### 坑二：`/mock 新功能（含提案）` 從 WorkflowEngine Requirements 步驟啟動導致流程卡死

**現象**：Dashboard 顯示 Requirements 任務失敗（或完成後流程停住），後續 Petra / Demi 都不觸發。

**根因一**：`WorkflowEngine.NewFeatureTable` 沒有 `["Requirements"]` 這個 key，`GetDecision("Requirements")` 回傳 `NextAction.Nothing`，流程在 Rosa 後停住。

**根因二**：Rosa 的 `ExecuteTaskAsync` 沒有 MockMode early return，會嘗試 `gitHubService.CreateIssueAsync` 在真實 repo 建立 GitHub Issues。

**修正**：
1. 新增 `TaskGroupService.FireMockProposalAndContinueAsync`，直接建立四個 mock done 任務後從 `Dev_plan` 啟動。
2. `CommandHandler.HandleMockCommandAsync` 的 `new_feature_with_proposal` 分支改呼叫此方法，不走 `FireStepsAsync([new WorkflowStep("Requirements")])`。

---

### 坑三：MockLlmProvider 延遲過短（1~2 秒），Dashboard 來不及觀察

**現象**：mock 流程幾秒內跑完，Dashboard 幾乎看不到中間狀態。

**修正**：所有 mock 延遲統一改為 `Random.Shared.Next(30000, 60000)`（30~60 秒），包含 `MockClaudeCodeService` 的 5 個方法和 `MockLlmProvider.CompleteAsync`。

---

### 已知小問題（不影響正確性）

Petra 的 `ReviewVera` 路徑設計為「先嘗試 Claude Code，失敗再 fallback LLM」。Mock 模式下，Claude Code 回傳 `[MOCK] 探索完成\n[...]`（Rosa 格式），Petra 無法解析為 `{"decision":"..."}` JSON，觸發 fallback 走 `MockLlmProvider`，仍正確回傳 `approve`。Log 會出現：

```
Petra Claude Code 輸出無法解析為 JSON：[MOCK] 探索完成
Petra LLM fallback 審核完成（第 1 次）：approve
```

邏輯正確，只是多了一次無謂的 mock Claude Code 呼叫。`ReviewVera` 本來就是設計為 LLM only（只看 review 報告，不需要 codebase），未來可將此路徑直接走 LLM 略過 Claude Code 嘗試。

---

## 五、驗收結果

| 驗收項目 | 結果 |
|---------|------|
| Dashboard Mock Mode 開關 | ✅ |
| 開關後 5 分鐘內生效，不需重啟容器 | ✅ |
| Victoria 在 Mock Mode 下回應帶 `[MOCK]` 標記 | ✅ |
| `/mock workflow:新功能` 全流程跑完 | ✅ |
| `/mock workflow:新功能（含提案）` 全流程跑完（含 Requirements/PM/Designer/PM mock 任務） | ✅ |
| `/mock workflow:bug_fix` 全流程跑完（Dev→Reviewer→QA，無 Doc） | ✅ |
| `/mock workflow:tech_improvement` 全流程跑完（Dev_plan→Dev→Reviewer→QA，無 Doc） | ✅ |
| Mock Mode 關閉後 `/mock` 指令正確拒絕 | ✅ |
| 所有 MockMode early return 正確跳過 GitHub API | ✅ |
| SignalR / Dashboard 任務中心即時更新正常 | ✅ |
| Mock 不建立真實 GitHub branch / PR / Issues | ✅ |
| `dotnet build` 0 Error | ✅ |
| EF Migration 無新增 | ✅ |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-08 | v1.0 初版建立（規劃） |
| 2026-04-08 | v2.0 實作完成，補充實作細節、踩坑三件組、驗收結果 |

# Stage 17 — Mock Mode（模擬模式）

> 版本：v1.0
> 建立日期：2026-04-08
> 狀態：📋 規劃中

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

Bot 端透過 `AppSettingsService.GetBoolAsync("MockMode", false)` 讀取，5 分鐘 TTL cache。Dashboard 修改後最多 5 分鐘生效，或搭配 `InvalidateCache()` 即時生效。

---

## 二、需要新增的元件

### 2.1 `IClaudeCodeService` 介面

從現有 `ClaudeCodeService` 抽取介面，包含 5 個 public 方法：

```csharp
public interface IClaudeCodeService
{
    Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default);
    Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default);
    Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default);
    Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default);
    Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default);
}
```

### 2.2 `MockClaudeCodeService : IClaudeCodeService`

每個方法回傳預設的 `ClaudeCodeResult`，並加入延遲模擬真實執行時間：

| 方法 | 延遲 | 回傳內容 |
|------|------|---------|
| `RunAsync` | 5-8 秒 | 模擬 Dev 開發成功（含假 PR 訊息） |
| `RunVictoriaAsync` | 2-3 秒 | 模擬 CEO 分類/回覆 |
| `RunReadOnlyAsync` | 2-3 秒 | 模擬 Rosa/Demi/Petra 探索結果（JSON 格式） |
| `RunQaAsync` | 3-5 秒 | 模擬 QA 測試產出 |
| `RunReviewAsync` | 3-5 秒 | 模擬 Vera review 結果（JSON 格式） |

重點：回傳的內容格式必須與真實輸出一致（JSON 結構），否則下游解析會失敗。

### 2.3 `MockLlmProvider : ILlmProvider`

回傳預設的 `LlmResponse`，內含合理的 JSON 格式回應：

| 使用場景 | 回傳內容 |
|---------|---------|
| Petra 審核 | `{"decision":"approve","summary":"模擬審核通過","issues":[]}` |
| Victoria 分類 | 模擬分類結果（new_feature / bug_fix 等） |
| 其他 Agent | 通用成功回應 |

延遲：1-2 秒。

### 2.4 `ClaudeCodeProxy : IClaudeCodeService`

代理類別，注入真的和假的實作，根據 `AppSettingsService` 的 MockMode 旗標決定路由：

```csharp
public class ClaudeCodeProxy(
    ClaudeCodeService real,
    MockClaudeCodeService mock,
    AppSettingsService settings,
    ILogger<ClaudeCodeProxy> logger) : IClaudeCodeService
{
    public async Task<ClaudeCodeResult> RunAsync(...)
    {
        if (await settings.GetBoolAsync("MockMode", false))
        {
            logger.LogInformation("MockMode: RunAsync 使用模擬結果");
            return await mock.RunAsync(...);
        }
        return await real.RunAsync(...);
    }
    // 其餘 4 個方法同理
}
```

---

## 三、需要修改的元件

### 3.1 `LlmProviderFactory`

在 `Create()` 方法中加入 MockMode 檢查：

```csharp
public ILlmProvider Create(string agentName)
{
    if (_appSettings.GetBoolAsync("MockMode", false).GetAwaiter().GetResult())
        return new MockLlmProvider();

    // 現有邏輯不變...
}
```

注意：`LlmProviderFactory.Create()` 目前是同步方法。可以：
- 使用 `GetAwaiter().GetResult()`（簡單但不理想）
- 或改為 `CreateAsync()`（更乾淨但影響所有呼叫端）
- 或在 Factory 中 cache 一份 MockMode 狀態（推薦，配合 AppSettingsService 的 TTL）

### 3.2 `Program.cs` — DI 註冊

```csharp
// 原本：
// builder.Services.AddSingleton<ClaudeCodeService>();

// 改為：
builder.Services.AddSingleton<ClaudeCodeService>();           // 真的實作
builder.Services.AddSingleton<MockClaudeCodeService>();       // 假的實作
builder.Services.AddSingleton<IClaudeCodeService, ClaudeCodeProxy>();  // 代理
```

所有注入 `ClaudeCodeService` 的地方改為注入 `IClaudeCodeService`。

### 3.3 Agent Services — 型別更新

以下 Service 的 constructor 中 `ClaudeCodeService` 改為 `IClaudeCodeService`：

- `DevAgentService`
- `ReviewerAgentService`
- `QaAgentService`
- `RequirementsAgentService`（Rosa）
- `DesignerAgentService`（Demi）
- `DocAgentService`（Sage）
- `PmAgentService`（Petra）
- `CeoAgentService`（Victoria）

改動極小：只是把參數型別從 `ClaudeCodeService` 換成 `IClaudeCodeService`，程式碼邏輯不變。

### 3.4 Dashboard — AgentSettings.razor

在現有的系統設定區塊中新增 Mock Mode 開關（參考 SkipCeoConfirm 的模式）：

```razor
<MudSwitch T="bool" @bind-Value="_mockMode" Color="Color.Warning"
           Label="Mock Mode（模擬模式）"
           ValueChanged="OnMockModeChanged" />
<MudText Typo="Typo.caption">
    啟用後所有 AI 呼叫將使用模擬結果，不消耗 API 費用。適用於 Dashboard / Discord 功能開發測試。
</MudText>
```

---

## 四、Mock 回傳內容設計原則

### 4.1 格式一致性

Mock 回傳的 JSON 必須能通過現有的解析邏輯（`TryParseReviewReport`、`TryParseReview` 等），否則下游會走 fallback 甚至失敗。

### 4.2 模擬延遲

每個方法加入隨機延遲（`Task.Delay(Random.Shared.Next(min, max))`），讓 Dashboard 和 Discord 的即時更新體驗接近真實情境。

### 4.3 可辨識性

Mock 產出的內容應包含明確標記（例如 `[MOCK]` 前綴），方便在 Dashboard / Discord / DB 中辨識哪些是模擬資料。

### 4.4 不建立真實 GitHub 資源

Mock Dev 不應真的建 branch / 開 PR。GitHubService 的呼叫需要在 Mock 路徑中跳過或 mock 掉。

---

## 五、GitHub 相關處理

Dev Agent 在正式模式下會：Clone repo → 建 branch → 寫 code → push → 開 PR。

在 Mock 模式下，需要跳過這些 GitHub 操作。有兩種做法：

**方案 A：在 DevAgentService 中檢查 MockMode**
- `ExecuteTaskAsync` 開頭檢查 MockMode
- 若為 mock，直接回傳假的 `AgentExecutionResult`（含假 PR URL）
- 不進入 CloneOrPull / CreateBranch 流程

**方案 B：也 mock GitHubService**
- 抽 `IGitHubService` 介面 + MockGitHubService
- 工作量較大，但更乾淨

**建議採方案 A**：只有 DevAgentService 會真正操作 GitHub（建 branch、開 PR），其他 Agent 只是讀取。在 DevAgentService 頂層做一個 early return 最簡單，不需要 mock 整個 GitHubService。

同理，Vera 的 `CloneOrPull` + `CreateAndCheckoutBranch` 也需要在 Mock 模式下跳過。

---

## 六、驗收條件

- [ ] `IClaudeCodeService` 介面抽取完成，所有 Agent Service 改用介面注入
- [ ] `MockClaudeCodeService` 實作完成，5 個方法各有合理的模擬回傳
- [ ] `MockLlmProvider` 實作完成，Petra / Victoria 等回傳格式正確
- [ ] `ClaudeCodeProxy` 代理模式運作正常，根據 MockMode 旗標切換
- [ ] `LlmProviderFactory` 支援 MockMode 檢查
- [ ] Dashboard AgentSettings 頁面有 Mock Mode 開關（MudSwitch）
- [ ] 開關切換後即時生效（或 5 分鐘內），不需重啟容器
- [ ] Mock 模式下完整 NewFeature 流程可跑完（Discord 觸發 → 全流程 → Discord 通知完成）
- [ ] 所有 TaskItem / TaskLog / SignalR 推送在 Mock 模式下正常運作
- [ ] Mock 產出內容帶 `[MOCK]` 標記，可辨識
- [ ] Mock 模式不建立真實 GitHub branch / PR
- [ ] `dotnet build` 通過
- [ ] EF Migration 無新增（純程式碼變更）

---

## 七、風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| Mock JSON 格式與解析不匹配 | 下游報錯或走 fallback | 測試所有解析路徑，確保 mock 內容通過 |
| Agent Service 漏改型別 | 編譯失敗 | `IClaudeCodeService` 改完後 `dotnet build` 即可發現 |
| MockMode 旗標 cache 延遲 | 切換後最多 5 分鐘才生效 | 搭配 `InvalidateCache()` 或加一個 API 讓 Dashboard 通知 Bot |
| 混用 Mock 資料和真實資料 | DB 中混雜模擬紀錄 | `[MOCK]` 標記 + 未來可加 `IsMock` 欄位篩選 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-08 | v1.0 初版建立 |

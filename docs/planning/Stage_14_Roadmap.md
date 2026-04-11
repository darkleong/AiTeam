# Stage 14：CEO 分類與流程完整性補強

> 版本：v1.1
> 建立日期：2026-04-06
> 狀態：✅ 已完成（2026-04-06）

---

## 目標

Stage 11~13 把整條開發鏈打通了，但 Victoria 的入口只有 4 條路：新功能、Bug、正常行為、疑問。老闆說什麼只要不是新功能或 Bug，Victoria 就只能回「我不知道」。

Stage 14 補齊 CEO 的分類與路由能力，讓老闆**只在 #victoria-ceo 就能指揮所有 Agent**，不用再繞去個人頻道。

這也是未來 Victoria 接上 Claude Code（十一）的前提——**有腦之前，先有路。**

---

## 目前的路（Stage 13 後）

### CEO 分類（4 類）

| 分類 | Action | 後續流程 |
|------|--------|---------|
| 新功能 | `propose` | Rosa → Demi → 提案書 → ✅ → Dev → Reviewer → QA → Doc → 通知 merge |
| Bug | `delegate` → Dev | Orchestrator：Dev → Reviewer → QA → 通知 merge |
| 正常行為 | `reply` | 直接回覆解釋 |
| 疑問 | `reply` | 直接回覆回答 |

### 走不了的路（只能繞去個人頻道）

| 老闆想做的事 | 該派誰 | Victoria 現在的反應 |
|-------------|--------|-------------------|
| 「重構 TaskGroupService」 | Dev（技術改善） | 歸為新功能 → Rosa + Demi 白做工 |
| 「幫我發布 v1.4.0」 | Rena（Release） | 歸為疑問 → 回一段廢話 |
| 「部署到正式環境」 | Maya（Ops） | 同上 |
| 「更新 README」 | Sage（Doc） | 同上 |
| 「停掉 Cody 在跑的任務」 | 取消 | 完全沒這個能力 |

---

## 一、新增「技術改善」分類與流程

### 背景

「重構」、「效能優化」、「技術債清償」不是新功能（不需要 Rosa / Demi），也不是 Bug（沒壞）。目前被錯誤歸類為新功能，啟動整套提案流程浪費資源。

### 修正

**CeoAgentService**：System Prompt 分類從 4 類擴充為 5 類，新增「技術改善」：
- 觸發條件：重構、效能優化、技術債、程式碼改善等
- Action：`delegate` → Dev
- 不經過 Rosa / Demi，不觸發提案流程

**WorkflowEngine**：新增 `WorkflowType.TechImprovement`，流程等同 BugFix：
```
Dev → Reviewer → QA → 通知 merge（含 fix loop 最多 3 輪）
```

### 需要實作的

- [x] `WorkflowType` enum 新增 `TechImprovement`
- [x] `WorkflowEngine` 新增 `TechImprovementTable`（與 BugFixTable 相同）
- [x] `CeoAgentService.BuildSystemPrompt` 新增第五分類「技術改善」
- [x] `CommandHandler` 處理 delegate 時，根據分類選擇正確的 `WorkflowType`
- [x] `TaskGroupService` 建立 TaskGroup 時支援 `TechImprovement` 類型

---

## 二、新增 Release / Ops / Doc 直接路由

### 背景

Rena（Release）、Maya（Ops）、Sage（Doc）都已註冊在 DI 中，個人頻道也能用。但 CEO 不知道它們的存在，無法從 #victoria-ceo 派任務給它們。

### 修正

**CeoAgentService**：System Prompt 新增三類「操作指派」的識別規則：

| 老闆意圖 | CEO 判斷 | Action | TargetAgent |
|---------|---------|--------|-------------|
| 發版、建立 Release | 操作指派 | `delegate` | Release |
| 部署、重啟、Rollback | 操作指派 | `delegate` | Ops |
| 更新文件、寫 README | 操作指派 | `delegate` | Doc |

這三類都是**單次任務**，不需要 Orchestrator 自動管線，走既有的 `delegate → 確認 → 執行` 流程即可。

### 需要實作的

- [x] `CeoAgentService.BuildSystemPrompt` 新增 Release / Ops / Doc 的識別規則
- [x] 確認現有 `delegate` 流程已能正確處理 `TargetAgent = "Release" / "Ops" / "Doc"`
- [x] 確認 Rena / Maya / Sage 的 `ExecuteTaskAsync` 在 Orchestrator 外也能正常執行

---

## 三、任務取消能力

### 背景

老闆說「停掉 Cody 在跑的任務」，Victoria 完全沒有這個能力。進行中的任務只能等它跑完或自己 timeout。

### 修正

**TaskGroupService**：新增 `CancelAsync(Guid groupId)` 方法：
1. 將 TaskGroup 狀態改為 `cancelled`
2. 將所有 `running` 狀態的 TaskItem 改為 `cancelled`
3. 通知 Dashboard（SignalR push）
4. 通知 CEO 頻道

**CeoAgentService**：System Prompt 新增「取消」類指令識別：
- 觸發條件：「停掉」、「取消」、「中斷」等
- Action：新增 `cancel`（或複用 `delegate` 搭配特殊處理）
- 需要一個機制讓 CEO 找到「目前進行中的任務」來取消

**CommandHandler**：處理取消指令：
1. 查詢目前 `running` 狀態的 TaskGroup
2. 如果只有一個 → 確認後取消
3. 如果有多個 → 列出讓老闆選擇

### 注意事項

- 已啟動的 Claude Code subprocess 需要另外處理 kill（`CancellationToken` 或 `Process.Kill`）
- 已經 push 到 GitHub 的 commit 不會被回滾（取消只是停止後續步驟）

### 需要實作的

- [x] `TaskGroupService.CancelAsync(Guid groupId)` 方法
- [x] `TaskRepository` 新增批量更新 TaskItem 狀態的方法
- [x] `CeoAgentService.BuildSystemPrompt` 新增「取消」指令識別
- [x] `CommandHandler` 處理取消流程（查詢進行中任務 → 確認 → 取消）
- [x] `ClaudeCodeService` 支援中斷正在執行的 subprocess

---

## 不做的事

- **複合指令拆解** — 難度較高，留給 Stage 15（Victoria 接 Claude Code 後 Session 模式自然能處理）
- **Victoria 接 Claude Code** — Stage 15 的目標
- **CEO 文件記錄** — 被 Stage 15 完全吸收，不需要單獨做

---

## 受影響檔案（預估）

| 檔案 | 項目 |
|------|------|
| `src/AiTeam.Bot/Agents/CeoAgentService.cs` | 一、二、三 |
| `src/AiTeam.Bot/Orchestration/WorkflowEngine.cs` | 一 |
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | 一、三 |
| `src/AiTeam.Bot/Discord/CommandHandler.cs` | 一、二、三 |
| `src/AiTeam.Bot/Services/ClaudeCodeService.cs` | 三 |
| `src/AiTeam.Data/Repositories/TaskRepository.cs` | 三 |

---

## 實作順序

1. 一（技術改善分類）— 新增 enum + 流程表 + System Prompt，最小改動最大收益
2. 二（Release / Ops / Doc 路由）— 只改 System Prompt + 驗證既有 delegate 流程
3. 三（任務取消）— 工程量最大，需要新增方法 + 處理 subprocess 中斷

---

## 驗收標準

1. 老闆在 #victoria-ceo 說「重構 XX」→ Victoria 分類為技術改善 → Dev → Reviewer → QA → 通知 merge（不經過 Rosa/Demi）
2. 老闆說「幫我發布 v1.4.1」→ Victoria 派 Rena 執行 Release
3. 老闆說「部署到正式環境」→ Victoria 派 Maya 執行
4. 老闆說「更新 README」→ Victoria 派 Sage 執行
5. 老闆說「停掉 Cody 在跑的任務」→ Victoria 列出進行中任務 → 確認後取消
6. 以上所有操作都在 #victoria-ceo 完成，不需要切換到個人頻道
7. `dotnet build` 整個 solution 通過

---

---

## 實作紀錄（Stage 14）

### 架構設計

#### CEO 分類從 4 類擴充為 6 類

`CeoAgentService.BuildSystemPrompt()` 新增：

| 新增分類 | 觸發條件 | action | 備註 |
|---------|---------|--------|------|
| 技術改善 | 重構、效能優化、技術債清理 | `delegate` → Dev | `workflow_type = "tech_improvement"` |
| 操作指派 | 發版 / 部署 / 更新文件 | `delegate` → Release / Ops / Doc | `workflow_type = null`（單次任務）|
| 取消任務 | 停止、取消、中斷進行中任務 | `cancel` | 不填 target_agent / task |

#### workflow_type 欄位設計

`CeoResponse` 新增 `workflow_type` 欄位，讓 `CommandHandler` 知道要建哪種 `TaskGroup`：
- `"bug_fix"` → `WorkflowType.BugFix`（Dev→Reviewer→QA→Merge）
- `"tech_improvement"` → `WorkflowType.TechImprovement`（同 BugFix 流程，不含 Doc）
- `null` → Release / Ops / Doc 單次任務，不建 TaskGroup

#### ResolveWorkflowType 輔助方法

`CommandHandler` 新增靜態方法，集中判斷是否需要 Orchestrator：

```csharp
private static WorkflowType? ResolveWorkflowType(CeoResponse ceoResponse)
{
    // Release / Ops / Doc 是單次任務，不走 Orchestrator pipeline
    if (ceoResponse.TargetAgent is AgentNames.Release or AgentNames.Ops or AgentNames.Doc)
        return null;

    return ceoResponse.WorkflowType switch
    {
        "tech_improvement" => WorkflowType.TechImprovement,
        _                  => WorkflowType.BugFix
    };
}
```

#### exec_yes 建立 TaskGroup（重要修正）

原本 Bug fix 的 `delegate` 路徑中，`exec_yes` 按下後 `pending.GroupId` 永遠是 `Guid.Empty`，導致 Dev 執行完後 Orchestrator 不會自動觸發 Reviewer → QA。

Stage 14 修正：`exec_yes` 時若 `ResolveWorkflowType` 有值，先建立 TaskGroup 再執行：

```csharp
var wfType = ResolveWorkflowType(pending.CeoResponse);
if (wfType.HasValue && pending.GroupId == Guid.Empty)
{
    var group = await taskGroupService.CreateGroupAsync(..., wfType.Value);
    pending = pending with { GroupId = group.Id };
}
```

此改動同時修正了 **Bug fix Orchestrator 未啟動**的既有問題。

#### 任務取消機制

**CTS 字典管理：**

`TaskGroupService` 維護 `ConcurrentDictionary<Guid, CancellationTokenSource>`（TaskItem.Id → CTS），在 `FireOneStepAsync` 中建立 linked CTS 並存入：

```csharp
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
_runningCts[taskItem.Id] = linkedCts;
try { /* 執行 Agent */ }
catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
{ /* 外部取消，標記 cancelled */ }
finally { _runningCts.TryRemove(taskItem.Id, out _); }
```

**CancelAsync 流程：**
1. 對所有 running TaskItem 的 CTS 呼叫 `.Cancel()`
2. CancellationToken 傳入 ClaudeCodeService → `process.WaitForExitAsync(ct)` 拋出 `OperationCanceledException`
3. `ClaudeCodeService` 的外部 cancel 路徑 `process.Kill(entireProcessTree: true)`（Stage 14 補齊）
4. DB 更新：TaskGroup → `cancelled`，未完成 TaskItem → `cancelled`

**多個 running TaskGroup 的選擇：**

使用 `_pendingCancelSelections` 字典（類似 `_pendingAdjustments`）攔截老闆的下一則訊息，解析序號或名稱後執行取消，避免被重新送進 CEO 分類流程。

---

### 踩坑紀錄

#### ClaudeCodeService 外部 cancel 不 kill subprocess

原本 `RunCoreAsync` 只有逾時路徑有 `process.Kill`，外部 CancellationToken 取消時 `OperationCanceledException` 直接向上傳播，process 成為孤立進程。

**修正**：補一個 catch 分支：
```csharp
catch (OperationCanceledException)
{
    try { process.Kill(entireProcessTree: true); } catch { }
    throw;
}
```

#### Bug fix Orchestrator 未啟動（Stage 14 順帶修正）

Stage 9 新增 CEO 智慧分類後，`delegate` 的確認流程並未建立 TaskGroup，導致所有 Bug fix 任務跑完 Dev 後 Orchestrator 不會繼續推進。

這個問題在 Stage 14 的 `exec_yes` 改動中同時修正。

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-06 | 初版建立，來源為 Future Feature 第九項（CEO 分類與流程完整性補強） |
| 2026-04-06 | 實作完成，驗收通過（commit 1086903）；補充實作紀錄與踩坑 |

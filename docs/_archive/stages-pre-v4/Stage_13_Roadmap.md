# Stage 13：系統穩定性與流程修正

> 版本：v2.0
> 建立日期：2026-04-06
> 狀態：✅ 已完成（2026-04-06）

---

## 目標

Stage 11 修了地基（Cody 寫好 code），Stage 12 修了源頭（Rosa / Demi 看懂 codebase 做好規格），Stage 13 修正**中間的管線和穩定性**。

做完後，整條鏈從頭到尾都到位。

**本 Stage 包含四個項目：技術債清償、Orchestrator 流程修正、單一 PR 整合、Dashboard 可觀測性。**

---

## 一、Stage 10 技術債清償（Future Feature 十三）

### 背景

Stage 10 實作 Orchestrator 時留下多項技術債，其中 3 項影響系統穩定性。

### 🔴 高優先（本 Stage 必須修）

**1. TaskGroupService 並行 SaveAsync Race Condition**

`HandleAgentCompletedAsync` 中對 `DevPrUrl` 和 `LastReviewBody` 分別呼叫 `SaveAsync`，並行 Agent 完成時可能覆蓋彼此更新。

修正：所有欄位更新完再呼叫一次 `SaveAsync`。

**2. 遞迴 Orchestration 無法優雅停止**

`Task.Run(CancellationToken.None)` 使得 Bot 關閉時背景工作鏈無法被取消。

修正：接入 `IHostApplicationLifetime` 的 `ApplicationStopping` token，或改用 `System.Threading.Channels` 背景佇列。

**3. WebhookController PR synchronize handler 缺少 try-catch**

GitHub 傳來格式異常的 JSON 時會 crash 整個 endpoint。

修正：用 `TryGetProperty()` 或包 try-catch。

### 🟡 中優先（本 Stage 順手處理）

**4. TaskGroup.Project 用 string 不用 FK**

`TaskGroup.Project` 是字串（專案名稱），而 `TaskItem.ProjectId` 是 Guid FK，兩者不一致。

修正：加 `Guid? ProjectId` FK 到 TaskGroup。

**5. Dev fix loop 取不到 PR number 時繼續執行**

`ExtractPrNumberFromText()` 返回 0 時應中斷 fix loop 而非讓 LLM 猜 branch。

**6. LLM JSON 解析用 IndexOf('{') 很脆弱**

CEO、Dev 都用 `IndexOf('{')` / `LastIndexOf('}')` 抓 JSON。

修正：要求 LLM 用 markdown code fence 包 JSON，解析時先找 code fence。

### 需要實作的

- [x] 修正 `TaskGroupService.HandleAgentCompletedAsync` 的 SaveAsync 合併
- [x] `Task.Run` 改用 `ApplicationStopping` token 或 Channel 背景佇列
- [x] `WebhookController` PR handler 加 try-catch + `TryGetProperty`
- [x] `TaskGroup` 新增 `ProjectId` FK + Migration
- [x] `ExtractPrNumberFromText` 返回 0 時中斷 fix loop
- [x] JSON 解析改用 code fence 提取

---

## 二、Orchestrator 流程修正（Future Feature 十四，剩餘問題）

### 背景

十四原有五個問題，Stage 12 已解決三個（Doc 猜路徑、fix loop 後 QA/Doc 不更新、✏️ 舊規格不刪除）。剩餘兩個問題需要修改 `WorkflowEngine` 流程表。

### 問題一：QA / Doc / Vera 不應同時並行

目前 Dev PR 開出後，QA + Doc + Vera 三個同時跑。Vera 發現 🔴 時，QA 和 Doc 已基於舊程式碼產出——等於做白工。

修改 WorkflowEngine 流程表：
```
目前：Dev → QA + Doc + Vera（並行）
修正：Dev → Vera → Vera ✅ → QA + Doc（並行）→ 通知 merge
```

### 問題二：Bug 修復流程缺少 QA

目前 Bug 修復流程是 `Dev → Vera → 通知 merge`，完全沒有 QA 回歸測試。

修改 BugFixTable：
```
目前：Dev → Vera ✅ → 通知 merge
修正：Dev → Vera ✅ → QA（回歸測試）→ 通知 merge
```

Bug 修復不需要 Doc（沒有新功能），只加 QA。

### 修正後的完整流程

**新功能：**
```
提案核准 → Dev → Vera 審查
  🔴 → Dev 修 → Vera 重審（最多 3 輪）
  ✅ → QA → Doc → 通知 merge
```

**Bug 修復：**
```
Dev → Vera 審查
  🔴 → Dev 修 → Vera 重審（最多 3 輪）
  ✅ → QA（回歸測試）→ 通知 merge
```

> 注意：QA 和 Doc 改為串行（不再並行），原因見第三項「單一 PR 整合」。

### 需要實作的

- [x] `WorkflowEngine.NewFeatureTable`：Dev 後只觸發 Reviewer（不再並行 QA/Doc）
- [x] `WorkflowEngine.NewFeatureTable`：Reviewer ✅ 後觸發 QA → QA ✅ 後觸發 Doc
- [x] `WorkflowEngine.BugFixTable`：Reviewer ✅ 後觸發 QA
- [x] `WorkflowEngine.GetDecision` 調整 Reviewer ✅ 的路由邏輯
- [x] Doc 完成後（新功能）或 QA 完成後（Bug 修復）通知 merge

---

## 三、單一 PR 整合（QA / Doc 推到 Dev 同一個 branch）

### 背景

目前一個需求會產生三個 PR：Dev 一個、Doc 一個、QA 一個。老闆要 merge 三次，PR 之間有依賴關係，git 歷史也很碎。

### 目標

一個需求 = 一個 PR。所有 Agent 的產出（code + tests + docs）都推到 Dev 開的同一個 branch。

### 為什麼 QA 和 Doc 要改成串行

如果 QA 和 Doc 同時 push 到同一個 branch，第二個推的會被 git 拒絕（remote 已更新）。為了零風險，改成串行：

```
目前：QA + Doc 並行 → 各開自己的 PR（3 個 PR）
修正：QA → Doc 串行 → 都推到 Dev 的 branch（1 個 PR）
```

代價是 QA + Doc 變成串行（慢一點），但換來的是：
- 老闆只要 merge 一次
- PR 內容完整（code + tests + docs）
- git 歷史乾淨

### 修正後的完整流程

**新功能（一個 PR）：**
```
Dev 開 branch feature/xxx → 寫 code → 開 PR
  ↓
Vera 審查 PR
  🔴 → Dev 修（推到同一個 branch）→ Vera 重審
  ✅ → 程式碼穩定
  ↓
QA 推測試到同一個 feature/xxx branch
  ↓
Doc 推文件到同一個 feature/xxx branch
  ↓
通知你：「PR 可以 merge 了」（一個 PR 包含一切）
```

**Bug 修復（一個 PR）：**
```
Dev 開 branch → 寫修復 → 開 PR
  ↓
Vera 審查
  ✅ → QA 推回歸測試到同一個 branch → 通知 merge
```

### 需要實作的

- [x] QA Agent 改為推 commit 到 Dev 的 branch（而非自己開新 branch + PR）
- [x] Doc Agent 改為推 commit 到 Dev 的 branch（而非自己開新 branch + PR）
- [x] `TaskGroupService` 傳遞 Dev 的 branch name 給 QA / Doc
- [x] WorkflowEngine 流程表：Vera ✅ → QA → Doc → 通知 merge（串行）

---

## 四、Dashboard 任務詳情修正（Future Feature 十一）

### 背景

目前 Dashboard 任務中心的詳情顯示有兩個問題，影響日常監控和 debug。

### 問題一：失敗任務看不到失敗原因

點開失敗任務時，執行步驟只顯示最後一筆「執行中」，沒有錯誤訊息。

修正：Bot 在寫入失敗狀態時，同步寫入一筆 `failed` 步驟，內容為 exception message 或 Agent 的錯誤說明。

### 問題二：完成任務的最後步驟不是完成

點開完成任務時，最後一筆步驟是業務步驟（例如「PR 已開啟」），缺少最終的「完成」步驟。

修正：Bot 在寫入完成狀態時，同步寫入一筆最終 `done` 步驟。

### 需要實作的

- [x] 找到 TaskItem 狀態寫入 `failed` 的地方，同步新增一筆 TaskLog
- [x] 找到 TaskItem 狀態寫入 `done` 的地方，同步新增一筆 TaskLog
- [x] Dashboard 確認 TaskLog 列表正確顯示新步驟

---

## 不做的事

- CEO 分類補強（十五）→ 有價值但不影響現有流程正確性
- CEO 文件記錄（十六）→ 新功能，不是修正
- Victoria 技術顧問（二十）→ 長期願景

---

## 驗收標準

1. 並行 Agent 完成時，TaskGroup 欄位不互相覆蓋（Race Condition 修復）
2. Bot graceful shutdown 時，背景 Orchestration 任務能被取消
3. GitHub webhook 收到異常 JSON 時不 crash
4. 新功能流程：Dev → Vera ✅ → QA → Doc → 通知 merge（串行）
5. Bug 修復流程：Dev → Vera ✅ → QA → 通知 merge
6. 一個需求只產生一個 PR（QA / Doc 推到 Dev 的 branch，不另開 PR）
7. Dashboard 失敗任務能看到失敗原因
8. Dashboard 完成任務有最終「完成」步驟
9. `dotnet build` 整個 solution 通過

---

---

## 實作紀錄（2026-04-06）

### 受影響檔案

| 檔案 | 修改內容 |
|------|---------|
| `WorkflowEngine.cs` | 替換整個 NewFeatureTable（Dev → Reviewer 串行；QA → Doc 串行）；BugFixTable 新增 QA；GetDecision 調整路由 |
| `TaskGroupService.cs` | 合併兩次 SaveAsync（needsSave flag）；注入 IHostApplicationLifetime；Task.Run 改用 ApplicationStopping token；補全 TaskLog（done/failed）；NotifyBossMerge 訊息更新 |
| `WebhookController.cs` | HandlePrSynchronizedAsync 改用 TryGetProperty，欄位缺失時記 warning 並 return |
| `DevAgentService.cs` | fix loop prNum ≤ 0 時拋 InvalidOperationException；TryParsePlan 優先解析 code fence；BuildClosesSection() 從 issue_urls 產生 `Closes #XX` |
| `CeoAgentService.cs` | TryParseResponse 優先解析 code fence，IndexOf 作為 fallback |
| `QaAgentService.cs` | 推 commit 到 Dev 的 headRef（不另開 branch/PR）；新增 StripCodeFence() 剝除 LLM 產生的 Markdown fence |
| `DocAgentService.cs` | 推 commit 到 Dev 的 headRef（不另開 branch/PR）|
| `Entities.cs` | TaskGroup 新增 `Guid? ProjectId`、`Project? ProjectRef` |
| `AppDbContext.cs` | TaskGroup HasOne ProjectRef FK 設定 |
| Migration | `AddTaskGroupProjectId`（uuid nullable FK） |
| `playwright.yml` | runs-on 改為 self-hosted（雲端無法連本機 Dashboard）|

### 踩坑紀錄

**1. WorkflowEngine NewFeatureTable 替換陷阱**

計畫只說「新增 `["QA"]` entry」，實作時差點只 add 新 entry 而保留舊的 `["Dev"] = [QA, Doc, Reviewer]`（並行）。正確做法是**替換整個 dictionary literal**，確保舊的並行行被完全移除。

**2. QA Agent code fence 污染 .cs 檔**

Quinn 產出的 Playwright 測試內容含 ` ```csharp ``` ` fence，直接寫入 `.cs` 導致 CI CS1056 build 失敗。修正：新增 `StripCodeFence()` helper，在 `WriteAllTextAsync` 前統一剝除。

**3. Playwright CI 截圖無法上傳（runs-on: ubuntu-latest）**

`playwright.yml` 原本跑在雲端 ubuntu，無法連到本機 `localhost:5051`，截圖資料夾為空，Artifacts 顯示「–」。改為 `self-hosted` 後，runner 在同一台機器上，能正常連到 Dashboard。

**4. dotnet ef migrations add 路徑問題**

從 `AiTeam.Bot` 目錄跑 `--startup-project` 失敗（Bot 專案缺 EF Design 工具）。改為進入 `AiTeam.Data` 目錄直接執行，不帶 `--startup-project`。

### 驗收結果

| 驗收項目 | 結果 |
|---------|------|
| dotnet build 通過 | ✅ |
| 並行 Agent SaveAsync 不互相覆蓋 | ✅ |
| Bot graceful shutdown 時背景工作可被取消 | ✅ |
| GitHub webhook 缺欄位時不 crash | ✅ |
| 新功能串行流程：Dev → Reviewer → QA → Doc → 通知 merge | ✅ |
| Bug 修復流程：Dev → Reviewer → QA → 通知 merge | ✅ |
| 單一 PR（code + tests + docs 三個 commit）| ✅ |
| PR description 含 Closes #XX，merge 後 Issues 自動關閉 | ✅ |
| Dashboard 失敗任務能看到失敗原因 | ✅ |
| Dashboard 完成任務最後一筆 log 為 done | ✅ |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-06 | 初版建立，整合 Future Feature 十一、十三、十四（剩餘問題） |
| 2026-04-06 | v1.1：新增第三項「單一 PR 整合」；QA/Doc 從並行改串行，全部推到 Dev 同一個 branch |
| 2026-04-06 | v2.0：Stage 13 全部實作完成並驗收通過；補充踩坑紀錄與受影響檔案清單 |

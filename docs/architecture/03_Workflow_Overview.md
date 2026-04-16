# 開發流程全景圖

> 版本：v1.0
> 建立日期：2026-04-16
> 對應系統版本：v3.13.0（Stage 27b）

---

## 目錄

1. [Agent 清單與執行方式](#agent-清單與執行方式)
2. [三種工作流程類型](#三種工作流程類型)
3. [NewFeature 完整流程](#newfeature-完整流程)
4. [BugFix 流程](#bugfix-流程)
5. [TechImprovement 流程](#techimprovement-流程)
6. [各階段詳細說明](#各階段詳細說明)
7. [佇列機制與狀態管理](#佇列機制與狀態管理)
8. [關鍵程式碼位置索引](#關鍵程式碼位置索引)

---

## Agent 清單與執行方式

系統中 Agent 的執行路徑分為三種：

| 類型 | 說明 |
|------|------|
| **Claude Code CLI** | 透過 `claude -p` subprocess 執行，可存取本機 codebase |
| **LLM API** | 透過 `ILlmProvider.CompleteAsync()` 直接呼叫 Anthropic API |
| **純程式邏輯** | 不呼叫任何 LLM，由 C# 程式碼直接執行 |

### Agent 總覽

| Agent | 角色 | 執行方式 | 模型 | Claude Code 模式 | 程式碼位置 |
|-------|------|---------|------|-----------------|-----------|
| **Victoria（CEO）** | 任務分類、對話、技術顧問 | Claude Code CLI + LLM API fallback | `claude-sonnet-4-6` | `RunVictoriaAsync`（讀寫 + Git） | `Agents/CeoAgentService.cs` |
| **Cody（Dev）** | 程式碼開發、實作計畫 | Claude Code CLI | `claude-sonnet-4-6` | `RunAsync`（完整開發模式） | `Agents/DevAgentService.cs` |
| **Vera（Reviewer）** | 程式碼審查 | Claude Code CLI | `claude-sonnet-4-6` | `RunReviewAsync`（唯讀 + Bash） | `Agents/ReviewerAgentService.cs` |
| **Quinn（QA）** | 測試撰寫與執行 | Claude Code CLI | `claude-sonnet-4-6` | `RunQaAsync`（可寫測試檔） | `Agents/QaAgentService.cs` |
| **Sage（Doc）** | 收尾歸檔、CHANGELOG | Claude Code CLI | `claude-haiku-4-5` | `RunAsync`（完整開發模式） | `Agents/DocAgentService.cs` |
| **Rosa（Requirements）** | 需求分析、建立 GitHub Issues | Claude Code CLI | `claude-haiku-4-5` | `RunReadOnlyAsync`（唯讀探索） | `Agents/RequirementsAgentService.cs` |
| **Demi（Designer）** | UI/UX 規格設計 | Claude Code CLI | `claude-haiku-4-5` | `RunReadOnlyAsync`（唯讀探索） | `Agents/DesignerAgentService.cs` |
| **Petra（PM）** | 品質審核、流程協調、會議主持 | Claude Code CLI + LLM API fallback | `claude-haiku-4-5` | `RunReadOnlyAsync`（唯讀探索） | `Agents/PmAgentService.cs` |
| **Rena（Release）** | 版本發布 | LLM API | `claude-sonnet-4-6` | — | `Agents/ReleaseAgentService.cs` |
| **Maya（Ops）** | 部署監控、健康檢查 | 純程式邏輯（不呼叫 LLM） | — | — | `Ops/OpsAgentService.cs` |

### Claude Code 模式說明

定義在 `Agents/IClaudeCodeService.cs`：

| 模式 | 方法 | 權限 | 使用者 |
|------|------|------|--------|
| `RunAsync` | 完整開發模式 | 讀 + 寫 + Build + Git | Cody、Sage |
| `RunReadOnlyAsync` | 唯讀探索模式 | Glob / Grep / Read only | Rosa、Demi、Petra |
| `RunVictoriaAsync` | CEO 模式 | 讀 + 文件 + Git | Victoria |
| `RunReviewAsync` | 審查模式 | Glob / Grep / Read + Bash | Vera |
| `RunQaAsync` | QA 模式 | 可寫測試檔 + 執行測試 | Quinn |
| `RunMeetingSessionAsync` | 會議模式 | 持久化 Session（`--session-id` / `--resume`） | 會議中的所有 Agent |

### Petra 的特殊執行模式

Petra 不是單一執行路徑，依功能分為兩種：

| 功能 | 執行方式 | 原因 |
|------|---------|------|
| 審閱 Rosa/Demi 產出、審閱 Dev_plan | Claude Code CLI（`RunReadOnlyAsync`），失敗時 fallback 到 LLM API | 需要讀取 codebase 才能評估 |
| 審閱 Vera Review、申訴仲裁、QA 失敗路由、阻礙報告評估 | LLM API（`ILlmProvider.CompleteAsync`） | 只需分析文字內容，不需存取 codebase |

---

## 三種工作流程類型

Victoria（CEO）接收 Christ 的指令後，分類為三種工作流程類型：

| 類型 | 說明 | 起始步驟 | 典型場景 |
|------|------|---------|---------|
| **NewFeature** | 完整流程（含需求 + 設計階段） | Kickoff 會議 | 新功能開發 |
| **BugFix** | 精簡流程（跳過需求/設計） | Dev（直接開發） | Bug 修復 |
| **TechImprovement** | 中等流程（含計畫書） | Dev_plan（先寫計畫） | 技術改善、重構 |

定義在 `Orchestration/WorkflowEngine.cs`。

---

## NewFeature 完整流程

```
Christ 在 Discord #victoria-ceo 頻道下指令
        │
        ▼
┌─────────────────────────────────┐
│  Victoria（CEO）分類與提案      │ ← Claude Code CLI / LLM API
│  判斷為 NewFeature              │
│  產出提案書（標題 + 描述）       │
└───────────────┬─────────────────┘
                │
                ▼
        Christ 確認提案（Discord 按鈕）
                │
                ▼
┌─────────────────────────────────┐
│  ① Kick-off 會議                │ ← 詳見「Kick-off 會議」
│  Petra 主持                     │
│  Rosa / Demi / Cody / Quinn 發言│
│  產出：任務計畫書                │
└───────────────┬─────────────────┘
                │
                ▼
        Christ 確認計畫書
        （Discord 按鈕：繼續 / 修改 / 停止 / 重開）
                │
                ▼
┌─────────────────────────────────┐
│  ② 設計會議                     │ ← 詳見「設計會議」
│  Petra 主持                     │
│  Rosa 建立 GitHub Issues        │
│  Demi 產出 UI 規格（需要時）     │
│  產出：設計規劃書                │
└───────────────┬─────────────────┘
                │
        〔consensus → 直接繼續〕
        〔escalate → Christ 確認按鈕〕
                │
                ▼
┌─────────────────────────────────┐
│  ③ Dev_plan（實作計畫書）        │ ← Cody（Claude Code CLI）
│  Cody 根據設計規劃書制定         │
│  實作計畫書                      │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ③a Petra 審閱 Dev_plan         │ ← Petra（Claude Code CLI / LLM API）
│  approve → 繼續                  │
│  revise → Cody 申訴迴圈（≤5輪） │
│  escalate → Christ 確認          │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ④ Dev（開發）                   │ ← Cody（Claude Code CLI）
│  Clone repo → 建立 branch       │
│  寫程式碼 → dotnet build        │
│  git commit → push → 開 PR      │
│  產出：GitHub PR                │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ⑤ Reviewer（程式碼審查）        │ ← Vera（Claude Code CLI）
│  讀取 PR diff + codebase        │
│  產出：Review 報告 + Critical 數 │
│  發布 GitHub PR Review          │
└───────────────┬─────────────────┘
                │
          Critical 數 = 0？
           ╱          ╲
         是            否
          │            │
          ▼            ▼
    ┌──────────┐  ┌──────────────────────┐
    │ Petra    │  │ 申訴迴圈（≤5輪）      │
    │ 審閱通過 │  │ Cody 反駁 ↔ Vera 再評 │
    └────┬─────┘  └──────────┬───────────┘
         │               仍有 Critical？
         │              ╱          ╲
         │            否            是
         │             │            │
         │             ▼            ▼
         │       ┌──────────┐  ┌──────────────┐
         │       │ Petra    │  │ Petra 仲裁    │
         │       │ 審閱通過 │  │ 判定哪些必修  │
         │       └────┬─────┘  └──────┬───────┘
         │            │               │
         ├────────────┤         必修數 > 0？
         │            │        ╱          ╲
         │            │      是            否
         │            │       │            │
         │            │       ▼            │
         │            │  Dev_fix 迴圈      │
         │            │  （≤3輪）          │
         │            │  Cody 修復         │
         │            │  → Vera 重審       │
         │            │       │            │
         ▼            ▼       ▼            ▼
┌─────────────────────────────────┐
│  ⑥ QA（測試）                    │ ← Quinn（Claude Code CLI）
│  讀取 PR diff + codebase        │
│  撰寫測試 → dotnet test         │
│  產出：TestReport JSON          │
└───────────────┬─────────────────┘
                │
          測試結果？
        ╱    │     ╲
     passed  │   failed
       │     │      │
       │  no_tests  ▼
       │     │   ┌──────────────────────┐
       │     ▼   │ Petra QA 路由判斷     │
       │  Petra  │ code_bug → Dev_fix    │
       │  評估   │   （跳過 Vera 直接 QA）│
       │     │   │ back_to_reviewer      │
       │     │   │   → Dev_fix → Vera    │
       │     │   │ env_or_test → 視為通過│
       │     │   │ escalate → Christ     │
       │     │   └──────────┬───────────┘
       │     │              │
       ▼     ▼              ▼
┌─────────────────────────────────┐
│  ⑦ Doc（收尾歸檔）              │ ← Sage（Claude Code CLI）
│  產出歸檔報告（archive.md）      │
│  更新 CHANGELOG.md              │
│  git commit → push              │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ⑧ 完成通知                     │
│  Discord 通知 Christ merge PR   │
│  TaskGroup.Status = "done"      │
└─────────────────────────────────┘
```

---

## BugFix 流程

BugFix 跳過需求和設計階段，Victoria 分類後直接進入開發。

```
Christ 下指令 → Victoria 分類為 BugFix
        │
        ▼
  Christ 雙層確認（CEO 決策確認 + Agent 執行確認）
        │
        ▼
   ④ Dev（Cody）→ ⑤ Reviewer（Vera）→ ⑥ QA（Quinn）→ ⑧ 完成通知
                        │                      │
                   （同 NewFeature            （同 NewFeature
                    的申訴迴圈）               的 QA 路由）
```

**注意：** BugFix 沒有 Doc 階段 — QA 通過後直接通知 merge。

---

## TechImprovement 流程

TechImprovement 跳過需求和設計階段，但包含 Dev_plan（實作計畫書）。

```
Christ 下指令 → Victoria 分類為 TechImprovement
        │
        ▼
  Christ 雙層確認
        │
        ▼
   ③ Dev_plan（Cody）→ ③a Petra 審閱 → ④ Dev → ⑤ Reviewer → ⑥ QA → ⑧ 完成通知
                              │                      │              │
                        （同 NewFeature         （同 NewFeature    （同 NewFeature
                         的審閱機制）            的申訴迴圈）       的 QA 路由）
```

**注意：** TechImprovement 同樣沒有 Doc 階段。

---

## 各階段詳細說明

### 0. Victoria 任務分類

**觸發方式：** Christ 在 Discord `#victoria-ceo` 頻道發訊息，或在各 Agent 專屬頻道（如 `#cody-dev`）直接對話。

**Victoria 的回應分四種：**

| Action | 說明 | 後續 |
|--------|------|------|
| `reply` | 純回覆（反問、閒聊、技術討論） | 等待 Christ 下一輪回應 |
| `propose` | 判定為新功能，進入提案模式 | 顯示提案書 → Christ 確認按鈕 |
| `delegate` | 判定為可直接派工的任務 | 雙層確認 → 執行 |
| `cancel` | 取消任務 | 取消指定 TaskGroup |

**雙層確認機制（delegate 路徑）：**
1. **第一層 — CEO 決策確認：** Victoria 說明分類結果與目標 Agent，Christ 按「確認」或「取消」
2. **第二層 — Agent 執行確認：** 顯示即將執行的 Agent 與任務內容，Christ 按「執行」或「取消」

可透過 `SkipCeoConfirm` AppSettings 跳過第一層。

**程式碼：** `Discord/CommandHandler.cs`

---

### 1. Kick-off 會議（僅 NewFeature）

**主持人：** Petra（PM）
**參與者：** Rosa、Demi、Cody、Quinn（四人平行發言）
**執行方式：** 全員使用 `RunMeetingSessionAsync`（Claude Code CLI，持久化 Session）

**流程：**
1. 每輪（Round）先由四位 Agent 平行發言（各自分析需求/技術/測試/UI 面向）
2. Petra 收集四人意見，做出判斷：
   - `consensus`：認知對齊，產出任務計畫書
   - `needs_discussion`：需要再討論，進入下一輪（最多 `KickoffMaxRounds` 輪）
   - `escalate`：需要 Christ 介入
3. 結束後 Petra 產出**任務計畫書**（TaskPlan），存入 `TaskGroup.TaskPlan`
4. Discord 通知 Christ，顯示四個按鈕：繼續 / 修改 / 停止 / 重開

**Session 機制：**
- Petra 使用固定 Session ID（`TaskGroup.Id`），支援 Christ 修改後 `--resume` 繼續
- 其他四人使用臨時 UUID，每輪 `--resume` 延續前一輪上下文

**程式碼：** `Orchestration/MeetingService.cs` (`RunKickoffMeetingAsync`)

---

### 2. 設計會議（僅 NewFeature）

**主持人：** Petra（PM）
**參與者：** Rosa、Demi（條件式）、Cody、Quinn
**執行方式：** 全員使用 `RunMeetingSessionAsync`

**流程分兩階段：**

**前置作業：**
1. Petra 判斷是否需要 Demi（UI 相關才需要）
2. Rosa 分析需求，建立 GitHub Issues
3. Demi 產出 UI/UX 規格（若 Petra 判定需要）

**設計輪次：**
1. 四人平行發言討論設計方向
2. Petra 判斷：
   - `consensus`：產出設計規劃書，直接進入 Dev_plan
   - `needs_discussion`：繼續討論
   - `needs_adjustment`：指定 Rosa 或 Demi 調整產出（修改 Issues / 修改 UI 規格）
   - `escalate`：需要 Christ 確認（Discord 按鈕：繼續 / 修改 / 停止）

**產出：**
- 設計規劃書（`TaskGroup.DesignPlan`）
- GitHub Issues URL（`TaskGroup.IssueUrls`）
- UI 規格（`TaskGroup.UiSpecContent`，若有 Demi 參與）

**程式碼：** `Orchestration/MeetingService.cs` (`RunDesignMeetingAsync`)

---

### 3. Dev_plan（實作計畫書）

**執行者：** Cody（Dev Agent）
**執行方式：** Claude Code CLI（`RunAsync`，完整開發模式）

Cody 根據前置階段的產出（設計規劃書 + Issues + UI 規格）制定詳細的實作計畫書。此階段只產出計畫，不寫程式碼。

**Petra 審閱閘門：**
- Petra 審閱 Dev_plan 內容（Claude Code CLI / LLM API）
- `approve`：通過，進入 Dev 開發階段
- `revise`：要求修改，進入 **Dev_plan 申訴迴圈**（最多 5 輪）
  - Cody 根據 Petra 意見修改計畫（LLM API）
  - Petra 重新評估（LLM API）
  - 若 5 輪內未達共識 → escalate
- `escalate`：通知 Christ，顯示 Skip / Abort 按鈕

**程式碼：** `Agents/DevAgentService.cs` + `Orchestration/TaskGroupService.cs` (`RunPetraDevPlanReviewAsync`)

---

### 4. Dev（開發）

**執行者：** Cody（Dev Agent）
**執行方式：** Claude Code CLI（`RunAsync`，完整開發模式）

**執行步驟：**
1. Clone repo 到 workspace
2. 建立 feature branch
3. 寫程式碼（根據 Dev_plan / Issues / UI 規格）
4. `dotnet build` 驗證編譯
5. `git add` / `git commit` / `git push`
6. 透過 `gh pr create` 開 Pull Request

**產出：** GitHub PR URL（存入 `TaskGroup.DevPrUrl`）

**阻礙報告：** 若 Cody 遇到無法解決的問題，回傳 `[BLOCKED]` 標記：
- Petra 評估：`continue`（繼續嘗試）/ `escalate_victoria`（Victoria 介入）/ `escalate_boss`（Christ 介入）

**程式碼：** `Agents/DevAgentService.cs`

---

### 5. Reviewer（程式碼審查）

**執行者：** Vera（Reviewer Agent）
**執行方式：** Claude Code CLI（`RunReviewAsync`，唯讀 + Bash）

**執行步驟：**
1. 從 GitHub 取得 PR diff
2. 讀取 codebase（Glob / Grep / Read）
3. 分析程式碼品質、安全性、最佳實踐
4. 產出結構化 Review 報告
5. 透過 GitHub API 發布 PR Review

**產出：**
- Review 報告（`TaskGroup.LastReviewBody`）
- Critical 問題數（`CriticalReviewCount`）

**後續流程（由 `TaskGroupService.HandleReviewerCompletedAsync` 處理）：**

```
CriticalReviewCount = 0？
     ╱          ╲
   是            否
    │            │
    ▼            ▼
 Petra         申訴迴圈（Review Appeal）
 審閱 Review   Cody 逐一反駁 Critical
    │          Vera 重新評估
    │            │
    │          仍有 Critical 且輪次用盡？
    │           ╱          ╲
    │         否            是
    │          │            │
    │          ▼            ▼
    │       Petra         Petra 仲裁
    │       審閱通過      判定哪些必修、哪些可忽略
    │          │            │
    ▼          ▼            ▼
          必修數 > 0 → Dev_fix（Cody 修復 → Vera 重審，≤3輪）
          必修數 = 0 → 通過，進入 QA
```

**Petra 審閱 Review 報告（`ReviewVeraAsync`）：** 使用 LLM API（不需 codebase），判斷 Review 品質是否合理。

**申訴迴圈細節：**
- Cody 和 Vera 的每輪申訴都使用 LLM API（`RunCodyAppealAsync` / `RunVeraAppealAsync`）
- Petra 仲裁使用 LLM API（`ArbitrateReviewAppealAsync`）

**Dev_fix 迴圈：**
- Cody 修復 → Vera 重審，最多 3 輪（`FixIteration`）
- 若 3 輪後仍有 Critical → `NotifyBossIntervention`（Christ 介入）
- 若 Petra 仲裁後設定 `SkipReviewerAfterArbitration = true`，Dev_fix 後跳過 Vera 直接進 QA

**程式碼：** `Agents/ReviewerAgentService.cs` + `Orchestration/TaskGroupService.cs`

---

### 6. QA（測試）

**執行者：** Quinn（QA Agent）
**執行方式：** Claude Code CLI（`RunQaAsync`，可寫測試檔）

**執行步驟：**
1. 讀取 PR diff + codebase
2. 撰寫測試（xUnit / Playwright）
3. 執行 `dotnet test`
4. 修復測試失敗（若有）
5. 產出 TestReport JSON

**產出：** TestReport JSON（`TaskGroup.TestReport`），格式：
```json
{
  "status": "passed | failed | no_applicable_tests",
  "passed_tests": [...],
  "failed_tests": [...]
}
```

**後續流程（由 `TaskGroupService.HandleQaCompletedAsync` 處理）：**

| 測試結果 | 處理 |
|---------|------|
| `passed` | 通過，進入 Doc（NewFeature）或完成通知（BugFix / TechImprovement） |
| `no_applicable_tests` | Petra 評估（`AssessNoApplicableTestsAsync`，LLM API）：approve 或 escalate |
| `failed` | Petra 路由判斷（`AssessQaFailureAsync`，LLM API），四條路徑 ↓ |

**QA 失敗路由：**

| 路由 | 說明 | 後續 |
|------|------|------|
| `code_bug` | 程式碼 Bug | Dev_fix（Cody 修復）→ 重新 QA（跳過 Vera） |
| `back_to_reviewer` | 需要 Review 層級的修正 | Dev_fix → Vera 重審 → QA |
| `env_or_test_issue` | 環境或測試本身的問題 | 視為通過 |
| `escalate` | 無法判斷 | 通知 Christ |

**QA 修復迴圈：** 受 `QaFixRound` 計數器限制，超過上限 → escalate。

**程式碼：** `Agents/QaAgentService.cs` + `Orchestration/TaskGroupService.cs`

---

### 7. Doc（收尾歸檔）（僅 NewFeature）

**執行者：** Sage（Doc Agent）
**執行方式：** Claude Code CLI（`RunAsync`，完整開發模式）

**執行步驟：**
1. 讀取 Cody 的 ImplementationNote + Vera 的 Review + Quinn 的 TestReport
2. 產出歸檔報告（`docs/archive/pr{N}-archive.md`）
3. 更新 CHANGELOG.md
4. git commit → push

**程式碼：** `Agents/DocAgentService.cs`

---

### 8. 完成通知

**流程結束：** `TaskGroupService` 設定 `TaskGroup.Status = "done"`，透過 Discord 通知 Christ merge PR。

---

## 佇列機制與狀態管理

### Per-Agent 佇列（Stage 27a）

所有 Agent 任務透過 DB-as-Queue 機制排隊執行，每個 Agent 一次只執行一個任務。

**核心元件：**
- `AgentQueueService`（Singleton）：Enqueue / Dequeue / CTS 管理
- `AgentQueueProcessor`（BackgroundService）：3 秒輪詢 + Signal 喚醒

**Semaphore 分組（8 組）：**

| Semaphore Key | 對應的 Agent |
|--------------|-------------|
| `Dev` | Dev + Dev_plan（共用，同一時間只跑一個） |
| `Reviewer` | Reviewer |
| `QA` | QA |
| `Doc` | Doc |
| `Requirements` | Requirements |
| `Designer` | Designer |
| `Release` | Release |
| `Ops` | Ops |

**PM（Petra）不在佇列中。** Petra 是 `TaskGroupService` 中 `await` 的內嵌閘門，在 pipeline 流程中同步執行，不是獨立的 BackgroundService。

### Agent 狀態管理（Stage 27b）

每個 Agent 有四種狀態，儲存在 `AppSettings` 資料表（key 格式：`AgentState:{executorKey}`）：

```
Active（預設）— 正常處理佇列中的任務
    ├── /pause {agent} → Paused — 佇列凍結，不消費任務
    └── /stop-all → 所有 Agent → Stopping → Stopped

Paused — 佇列凍結，任務持續累積
    └── /resume {agent} → Active

Stopping — 完成手頭任務後停止，不接新任務
    └── 手頭完成 → Stopped（自動轉換）

Stopped — 完全停止
    └── /resume {agent} 或 /resume-all → Active
```

**Discord 指令：** `/pause`、`/resume`、`/stop-all`、`/resume-all`、`/queue`

**Dashboard：** Agent 卡片顯示狀態 Badge + 佇列深度 Chip，SignalR 即時更新。

---

## 各流程類型的階段對照表

| 階段 | NewFeature | BugFix | TechImprovement | 參與 Agent | 執行方式 |
|------|:----------:|:------:|:---------------:|-----------|---------|
| Victoria 分類 | ✅ | ✅ | ✅ | Victoria | Claude Code CLI / LLM API |
| Christ 確認 | 提案確認 | 雙層確認 | 雙層確認 | — | Discord 按鈕 |
| ① Kick-off 會議 | ✅ | — | — | Petra + Rosa + Demi + Cody + Quinn | Claude Code CLI（Meeting Session） |
| Christ 確認計畫書 | ✅ | — | — | — | Discord 按鈕 |
| ② 設計會議 | ✅ | — | — | Petra + Rosa + Demi（條件式）+ Cody + Quinn | Claude Code CLI（Meeting Session） |
| ③ Dev_plan | ✅ | — | ✅ | Cody → Petra 審閱 | Claude Code CLI → Claude Code CLI / LLM API |
| ④ Dev | ✅ | ✅ | ✅ | Cody | Claude Code CLI |
| ⑤ Reviewer | ✅ | ✅ | ✅ | Vera → Petra 審閱 | Claude Code CLI → LLM API |
| 申訴迴圈 | ✅ | ✅ | ✅ | Cody + Vera + Petra（仲裁） | LLM API |
| ⑥ QA | ✅ | ✅ | ✅ | Quinn → Petra 路由 | Claude Code CLI → LLM API |
| ⑦ Doc | ✅ | — | — | Sage | Claude Code CLI |
| ⑧ 完成通知 | ✅ | ✅ | ✅ | — | Discord 訊息 |

---

## 關鍵程式碼位置索引

| 功能 | 檔案 | 說明 |
|------|------|------|
| 流程決策表 | `Orchestration/WorkflowEngine.cs` | 三種流程類型的步驟定義 + Decision 邏輯 |
| 流程協調中樞 | `Orchestration/TaskGroupService.cs` | Petra 閘門、申訴迴圈、QA 路由、阻礙報告 |
| 佇列處理器 | `Orchestration/AgentQueueProcessor.cs` | Semaphore 分組、輪詢 + Signal、狀態檢查 |
| 佇列服務 | `Orchestration/AgentQueueService.cs` | Enqueue / Dequeue / CTS 管理 |
| 會議服務 | `Orchestration/MeetingService.cs` | Kick-off 會議 + 設計會議 |
| Discord 指令 | `Discord/CommandHandler.cs` | 任務觸發、雙層確認、按鈕處理 |
| Claude Code 介面 | `Agents/IClaudeCodeService.cs` | 六種執行模式定義 |
| Agent 設定 | `appsettings.json` | Provider / Model / 各項參數 |

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-16 | v1.0 | 初版建立 — 記錄 v3.13.0 完整流程 |

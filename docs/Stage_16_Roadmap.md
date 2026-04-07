# Stage 16 — PM Agent（Petra）品質審核閘門

> 版本：v2.0
> 建立日期：2026-04-07
> 完成日期：2026-04-07
> 狀態：✅ 已完成

---

## 目標

在 WorkflowEngine 的 Agent 產出環節之間加入 PM Agent（Petra）作為品質審核閘門，讓團隊產出在交給老闆之前先經過內部審核，減少老闆的審核負擔。

---

## 一、新增 PM Agent（Petra）

### 1.1 PmAgentService

- 新增 `PmAgentService`，透過 `ClaudeCodeService.RunReadOnlyAsync` 審核 Agent 產出
- 模型：`claude-haiku-4-5`（成本優先）
- Timeout：10 分鐘 / Max Turns：10
- CLAUDE_Petra.md 模板已建立

### 1.2 appsettings.json 設定

```json
"PM": {
  "Provider": "Anthropic",
  "Model": "claude-haiku-4-5",
  "DailyTokenLimitK": 10,
  "MonthlyTokenLimitK": 200
}
```

### 1.3 DB Agent 紀錄

- `agents` 資料表新增 Petra（PM Agent）

### 1.4 Discord 頻道

- 新增 `#petra-pm` 頻道，顯示審核過程與結果

---

## 二、WorkflowEngine 整合審核閘門

### 2.1 審核點

| 觸發時機 | 前一步 Agent | Petra 審查什麼 |
|---------|-------------|-------------|
| Rosa 完成後 | Rosa（Requirements） | Issues 規格完整性、是否遺漏情境 |
| Demi 完成後 | Demi（Designer） | UI 規格與 Issues 的一致性 |
| Cody 計畫書完成後 | Cody（Dev_plan） | 實作計畫是否對齊規格與設計、檔案清單正確性、架構合理性 |
| Vera 完成後 | Vera（Reviewer） | Review 結果的嚴重度判斷 |

### 2.2 審核流程

```
Agent 完成任務 → WorkflowEngine 呼叫 Petra 審核
    ↓
Petra 回傳 JSON：
  { "decision": "approve" | "revise" | "escalate", ... }
    ↓
┌─ approve  → 自動進入下一步
├─ revise   → 打回給原 Agent（帶 revision_instructions），最多 2 次
└─ escalate → 上呈給 Victoria，由 Victoria 轉達老闆
```

### 2.3 打回修正機制

- 每個審核點最多打回 2 次
- 超過 2 次自動 escalate
- 打回時 Petra 提供具體修改指示
- 修改後的產出再次經過 Petra 審核

### 2.4 TaskItem 狀態擴充

新增 `reviewing`（審核中）和 `revision`（修正中）狀態：

```
pending → running → reviewing → [approved → 下一步]
                              → [revision → running → reviewing → ...]
                              → [escalated → 通知老闆]
```

---

### 2.5 Cody 實作計畫書（Dev_plan）

在 NewFeature 和 TechImprovement 流程中，老闆確認提案後 Cody 先產出實作計畫書（不寫程式碼），經 Petra 審核通過才進入實際開發。BugFix 流程跳過此步驟。

**WorkflowEngine 變更**：
- NewFeature：`proposal_approved → Dev_plan → Dev → Reviewer → ...`
- TechImprovement：`Dev_plan → Dev → Reviewer → ...`
- BugFix：不變（`Dev → Reviewer → QA`）

**計畫書儲存**：TaskGroup 新增 `DevPlan` 欄位，Petra 審核通過的計畫書存入此欄位，Cody 實際開發時讀取。

---

## 三、不審核的環節（不經過 Petra）

| 環節 | 原因 |
|------|------|
| Cody 實際開發後 | Vera 已專門審查程式碼，職責不重疊 |
| Quinn QA 後 | pass/fail 是客觀結果，不需主觀判斷 |
| Sage 文件後 | 風險低，有 PR 流程保底 |
| BugFix 的 Cody 開發前 | 規模小，不需要計畫書 |

---

## 四、全 Agent 任務可見性

### 4.1 問題

目前 Rosa / Demi 在提案階段由 `CommandHandler` 直接呼叫，不經過 `TaskGroupService`，沒有建立獨立 TaskItem。Dashboard 任務中心完全看不到這兩個 Agent 的工作紀錄。

### 4.2 目前 TaskItem 建立點

| 建立位置 | 涵蓋的 Agent |
|---------|-------------|
| `TaskGroupService.cs` — WorkflowEngine 觸發 | Cody / Vera / Quinn / Sage |
| `CommandHandler.cs` — delegate 路徑 | Rena / Maya / Sage（單一任務） |
| `CommandHandler.cs` — propose 路徑 | 僅 CEO（一筆），Rosa / Demi 無紀錄 |
| `OpsAgentService.cs` / `InternalController.cs` | Maya（部署監控） |

### 4.3 改善方案

1. **提案流程補建 TaskItem**：在 `CommandHandler` 的 propose 路徑中，Rosa 執行前建立 TaskItem（assigned=Rosa），完成後更新狀態；Demi 同理
2. **審核環節補建 TaskItem**：Petra 審核時也建立 TaskItem（assigned=Petra），記錄 approve / revise / escalate 結果
3. **確保所有 Agent 工作都有對應 TaskItem**：Dashboard 任務中心可追蹤完整流程

---

## 五、驗收條件

- [x] Petra PM Agent 正常運作（RunReadOnlyAsync + CLAUDE_Petra.md）
- [x] Rosa 產出後 Petra 自動審核，approve 時自動進入 Demi
- [x] Demi 產出後 Petra 自動審核，approve 時交給老闆確認
- [x] 老闆確認後 Cody 產出實作計畫書（非程式碼）
- [x] 計畫書產出後 Petra 自動審核，approve 時 Cody 開始 coding
- [x] Vera 審查後 Petra 自動判斷，blocking 打回 Cody / minor 放行
- [x] 打回修正機制正常（設計完成，Cody fix loop 實際觸發並 retry 成功）
- [x] #petra-pm 頻道自動建立並顯示審核過程
- [x] BugFix 流程不受影響（無 Dev_plan、無額外 Petra 審核）
- [x] TechImprovement 流程包含 Dev_plan 計畫階段
- [x] Rosa / Demi 任務在 Dashboard 任務中心可見
- [x] Petra 審核任務在 Dashboard 任務中心可見
- [x] DevPlan 正確儲存並傳給 Cody coding 階段
- [x] EF Migration 正常執行
- [x] `dotnet build` 通過
- [ ] escalate 機制實際觸發（設計完成，未刻意製造連續失敗場景；未來機會驗證）

---

## 六、風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| 審核增加整體 workflow 時間 | 每個審核點 +5~10 分鐘 | Haiku 模型快速回應；只審核關鍵環節 |
| Petra 誤判（過嚴或過鬆） | 不必要的打回或漏放問題 | 初期觀察並調整 CLAUDE_Petra.md prompt |
| 打回迴圈卡住 | 2 次打回後仍不通過 | 強制 escalate 機制保底 |
| API 成本增加 | 每個 workflow 多 2-3 次 Haiku 呼叫 | Haiku 成本極低（$1/$5 per MTok） |

---

---

## 七、實作重點紀錄

### 7.1 架構決策

| 決策 | 說明 |
|------|------|
| Petra 不實作 IAgentExecutor | 直接由 TaskGroupService / CommandHandler 呼叫，不走 WorkflowEngine dispatch |
| Rosa/Demi revision 計數 | CommandHandler ShowProposalAsync 的 local int，獨立計數 |
| Vera revision 計數 | 沿用 group.FixIteration（打回 Dev 即現有 fix loop） |
| Dev_plan revision 計數 | TaskGroup.DevPlanRevision 欄位，獨立於 FixIteration |
| CEO TaskItem 不被污染 | revisionContext 透過獨立參數傳入 AnalyzeOnlyAsync / GenerateDraftAsync，不修改 CEO TaskItem.Description |
| CLAUDE.md 備份還原 | 每次 ClaudeCode session 前備份原始 CLAUDE.md，session 結束後 finally 還原，確保不污染 repo |

### 7.2 新增檔案清單

| 檔案 | 說明 |
|------|------|
| `src/AiTeam.Bot/Agents/PmAgentService.cs` | Petra 核心服務：四個審核方法（ReviewRosaAsync / ReviewDemiAsync / ReviewDevPlanAsync / ReviewVeraAsync） |
| `src/AiTeam.Bot/Resources/CLAUDE_Petra.md` | Petra 行為約束模板（Claude Code 唯讀探索 + 嚴格審核標準） |
| `src/AiTeam.Bot/Resources/CLAUDE_Vera.md` | Vera 重寫（只審 `+` diff 行、strict Critical 定義、Bash 唯讀診斷） |
| `src/AiTeam.Bot/Resources/CLAUDE_QA.md` | Quinn 行為約束模板（Write 測試檔 + dotnet build 驗證） |

### 7.3 主要修改檔案

| 檔案 | 變更 |
|------|------|
| `ClaudeCodeService.cs` | 新增 `RunReviewAsync`（Vera 用，Glob/Grep/Read/Bash，15 turns）、`RunQaAsync`（Quinn 用，全工具，40 turns）；maxTurns 提升至 40 |
| `ReviewerAgentService.cs` | 重構為單一 Claude Code session（patch only，不帶完整檔案）；移除 LLM 逐檔呼叫；ReviewReport 新增 `impact` 欄位 |
| `QaAgentService.cs` | 重構為單一 Claude Code session；移除 LlmProviderFactory、StripCodeFence；Claude Code 直接 Write 測試檔並 dotnet build 驗證 |
| `TaskGroupService.cs` | 加入 Dev_plan→Petra 審核、Vera→Petra 審核；Dev 失敗 guard；BuildTaskDescription 注入 DevPlan |
| `CommandHandler.cs` | ShowProposalAsync 加入 Rosa/Demi 審核迴圈（各最多 3 輪）；建立 Rosa/Demi/Petra TaskItem；workspace 整個迴圈期間保留 |
| `WorkflowEngine.cs` | NewFeature / TechImprovement 加入 `Dev_plan` 步驟 |
| `Entities.cs` | TaskGroup 新增 `DevPlan`（string?）和 `DevPlanRevision`（int）欄位 |
| `.github/workflows/playwright.yml` | 移除 Start/Stop Dashboard；改為 health check + 直接打 production；全 step 加 `shell: pwsh` |

### 7.4 EF Core Migration

```
AddTaskGroupDevPlan：新增 TaskGroup.DevPlan（text null）和 TaskGroup.DevPlanRevision（int default 0）
```

### 7.5 踩坑紀錄

**1. Vera false Critical 誤判（最重要）**

**問題**：舊版 Vera 把完整檔案內容 + diff 同時送給 LLM，LLM 混淆了 `-`（已刪除）和 `+`（新增）的程式碼，把已移除的舊程式碼當成新增的程式碼審查，導致大量 Critical 誤報。

**修正**：改為單一 Claude Code session，prompt 只帶 patch（diff），不帶完整檔案。Claude Code 在需要理解上下文時自行用 `Read` 工具讀取。同時 CLAUDE_Vera.md 明確限制「只審 `+` 開頭的行」。結果：PR #93 時 7 個 false Critical → PR #94 後只有 1 個 Info。

**2. Dev 失敗後觸發 Reviewer 的 Not Found cascade**

**問題**：Dev 執行失敗（branch 未建立）→ WorkflowEngine 仍觸發 Reviewer → Reviewer 呼叫 `GetPullRequestHeadRefAsync` → PR 不存在 → `Octokit.NotFoundException` 未捕捉 → 整個流程崩潰。

**修正**：
- `HandleAgentCompletedAsync` 加入 Dev 失敗 guard（非 fix loop 的 Dev 失敗即停止流程）
- `GetPullRequestHeadRefAsync` 加入 `NotFoundException` catch，回傳空字串

**3. Claude Code exit code=1 無診斷資訊**

**問題**：Claude Code subprocess 回傳 exit code=1 但 log 完全看不出原因。

**修正**：
- `ClaudeCodeService` 失敗時將 stdout 尾段（3000 chars）升為 `LogError`
- `DevAgentService` 失敗時將 RawJson 尾段（1500 chars）存入 TaskLog
- 實際效果：TaskLog 記錄到 `error_max_turns`（num_turns=21），精確定位 maxTurns 不足問題

**4. QA Agent StripCodeFence 截斷問題**

**問題**：LLM 輸出超過長度被截斷（無結尾 ` ``` `），StripCodeFence 正則不匹配，直接把含反引號的內容寫入 .cs 檔，導致 CI build 失敗。

**修正**：QA Agent 改用 Claude Code session（`RunQaAsync`），Claude Code 使用 `Write` 工具直接寫檔，完全沒有 markdown code fence 問題；同時 Claude Code 會跑 `dotnet build` 驗證。

**5. Playwright workflow Stop Dashboard 打到 production**

**問題**：`playwright.yml` 的 `Stop Dashboard` 步驟有 `if: always()`，每次 CI 跑完（不論成功失敗）都執行 `docker compose down aiteam-dashboard`。Self-hosted runner 是本機 Windows，直接打掉 production Dashboard 容器。

**修正**：移除 Start/Stop Dashboard 步驟，改為 health check 確認 production Dashboard 是否在線，在線才跑測試，否則 skip。

### 7.6 成本觀察

- NewFeature 全流程（Rosa→Demi→Dev_plan→Dev→Vera fix→QA→Sage）約 $5
- 主要消耗：Cody Dev（`RunAsync` 全工具 40 turns）+ Vera Review（Claude Code session）
- Cody fix loop 因 maxTurns 不足失敗一次（$0.50），重試後成功
- Petra 每次審核約 $0.01-0.05（Haiku 模型，成本極低）

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-07 | v1.0 初版建立 |
| 2026-04-07 | v1.1 新增第四章：全 Agent 任務可見性（Rosa/Demi/Petra TaskItem 補建） |
| 2026-04-07 | v1.2 新增 Cody 實作計畫書審核（Dev_plan → Petra → Dev）；WorkflowEngine 變更；TaskGroup.DevPlan 欄位 |
| 2026-04-07 | v2.0 Stage 16 驗收完成；補充第七章實作重點紀錄（架構決策、踩坑五件組、成本觀察）；驗收條件全部勾選 |

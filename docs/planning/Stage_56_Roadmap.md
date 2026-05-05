# Stage 56：Trial v6 前置條件統包 — Dashboard MockScenarioCard 補全 + FF 四十二/四十三修 + WorkflowEngine 殘留評估補註

> 對應 Future Feature：Trial v6 前置條件鋪路（Stage 55B 拍板）— v4 路線 9/9 達成後最後一個觀察類整理 Stage
> 對應版本：**v3.45.0**（Stage 55B v3.44.0 + 1）
> 建立日期：2026-05-05
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 49-55B](Stage_55B_Roadmap.md) 完成 v4 漸進遷移完整 9 步路線（Appeal / Kickoff / HITL 試點 / Design / macro Pipeline / 子流程 / Crash Recovery + idempotency / Kickoff/Design 整合 / HITL 全面 wire）— **v4 路線 9/9 達成 🎉**。**Stage 56 = Trial_v6 前置條件統包**：把 v4 路線累積的 framework_* Mock 場景在 Dashboard 補全（Christ 線下驗收 + Trial_v6 觀察期工具完備）+ 兩個觀察類 FF（四十二 / 四十三）一次清完 + WorkflowEngine.cs 殘留性質評估補註。完成後即可進入 Trial_v6 v4 動態架構驗證。

### 範圍邊界

- ✅ **Dashboard MockScenarioCard 補全 33 個 framework_* 場景**（Stage 49 5 個 / Stage 50 5 個 / Stage 51 4 個 / Stage 52 6 個 / Stage 53A 6 個 / Stage 54 2 個 / Stage 55B 5 個 — 對齊 `MockScenarioService.cs` 既有 service 端 case 全清單）
- ✅ **FF 四十二 修**：`TryParseDesignIssues` 邊界判斷重構（對齊 `TryParseDesignPetraDecision` / `TryParseDesignAdjustmentEvaluation` 既有 line-based scan pattern）
- ✅ **FF 四十三 修**：`token_logs.TotalCostUsd` 寫入覆蓋率 spike + 修一氣呵成（Christ 拍板議題 2 = B：信任 Forge 自驗能力，spike + 修綁一起）
- ✅ **WorkflowEngine.cs 殘留性質補註**（Aria 預掃揭露：剩 `WorkflowType` enum + `WorkflowStep` record 是跨 23 service fundamental type，不是 dead code — Stage 55A 既有註解已說明，Stage 56 補進 `docs/conventions/csharp.md` 一段「跨 service fundamental type 不可移除」備註）
- ✅ **Stage 48 PATHEXT shim 候選 FF 落地**：寫入 `docs/conventions/csharp.md` 一段「Windows dev 機 Process.Start + .cmd 解法」備註（Forge 拍板：寫 conventions，不立 FF — Linux Docker production 無此問題）

- ❌ **不動**：v4 框架本體任何 production code（Stage 49-55B 已收口，Stage 56 純觀察類整理）
- ❌ **不動**：FF 三十六 Phase B 動態流程架構評估（留 Trial_v6 後）
- ❌ **不動**：WorkflowEngine.cs `WorkflowType` enum + `WorkflowStep` record 本體（grep 揭露 23 個 service 用，跨 service fundamental type 不是殘留）

### Trial_v6 前置條件達成判定

Stage 56 完成後 = Trial_v6 開跑前工具完備：

- Dashboard MockScenarioCard 涵蓋 v4 全 9 步驟全 framework_* 場景（Christ 線下挑樣驗收 + Forge auto-approve 自驗）
- FF 四十三修後 token_logs.TotalCostUsd 寫入覆蓋率達 production-grade（Trial_v6 cost 對照可信）
- FF 四十二修後 design issues 解析 robust（不依賴 `[MOCK]` workaround）

### v4 路線 9/9 達成後第一個 Stage 風險預警

- **純觀察類整理性質** — 預估校準錨 ×0.7-1.0（mid 帶下半，對齊 model_effort 速查表「觀察類 FF 整理 + spike + 純機械化」分類）
- **FF 四十三 spike 範圍可能擴張**：根因若涉及多 LLM provider（Claude CLI / Gemini API / Anthropic API）不同 cost 回傳機制 → spike 結論可能限縮修法範圍（接受「先修 Claude CLI path 涵蓋 80%+」並 follow-up 處理 Gemini/Anthropic API path）
- **Dashboard 補場景純機械化** — Forge 對齊既有 6+4 場景 pattern 機械擴充，主要工作量在 frameworkHint switch case 補完 + emoji map 補

---

## 設計決策（Christ 2026-05-05 拍板）

### 主路線拍板

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 1：Dashboard MockScenarioCard 補場景範圍** | **A：補全 33 個 framework_* 場景**（Stage 49-55B 全到位 — Trial_v6 後續觀察期完整工具，避免二次工）| B：只補 Trial_v6 必用的 ~20 個核心（卡片乾淨但未來補後續觀察 case 還要再追加） |
| **議題 2：FF 四十三 修法策略** | **B：Forge spike + 修一氣呵成**（信任 Forge 自驗能力，spike 結論驅動修法綁同 session — Stage 56 是觀察類整理性質，把 spike + 修綁一起符合範圍）| A：spike 找根因 → 二次評估再修（多一輪 session 但根治） |

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | FF 四十二 修法 pattern | **對齊 `TryParseDesignPetraDecision` (line 288) + `TryParseDesignAdjustmentEvaluation` (line 305) 既有 line-based scan pattern** — 逐行掃 `startsWith('[')` → `JsonDocument.Parse` 嘗試 → 成功則 deserialize。避開 `IndexOf('[')` + `LastIndexOf(']')` 的全域邊界誤判（`[MOCK]` 前綴 / 多 `[` 嵌套等 case 都失敗）|
| 2 | FF 四十三 spike 邊界 | Forge Plan Mode 第一步先 spike 16 個 `LogCliUsageAsync` caller 對 `usage` 參數的傳遞鏈 + 各 LLM provider（Claude CLI subprocess `--output-format json` 內 `total_cost_usd` 欄位、Gemini API、Anthropic API）的 cost 來源行為。spike 結論若揭露「需修 LLM provider 多 path」→ Forge 在計劃書內 escalate 給 Christ 拍範圍（接受縮範圍 80%+ 涵蓋 vs 全 path 修） |
| 3 | WorkflowEngine.cs 殘留性質 | **不動 + 補 conventions 註明**（Aria grep 揭露 23 個 service 用 `WorkflowType` enum + `WorkflowStep` record，已是跨 service fundamental type 不是 dead code，Stage 55A 既有註解已說明 — Stage 56 補一段進 `docs/conventions/csharp.md` 「跨 service fundamental type 不可移除」備註讓未來不誤刪） |
| 4 | Stage 48 PATHEXT 候選 FF 處理 | **寫入 `docs/conventions/csharp.md`「Windows dev 機 PATHEXT + .cmd 解法」段**（Linux Docker production 無此問題不立 FF，但 Christ Windows dev 機若未來 local 跑 framework path 仍會踩，conventions 一段備註避免 onboarding 踩坑） |
| 5 | Mock 場景 alias 機制 | 對齊 Stage 49-55B 既有 `MockClaudeCodeService.FailScenario` static + `/internal/mock/scenario` HTTP API + BossInteraction auto-approver 既有機制 — Stage 56 純 Dashboard UI 補（service 端 case 既有，不重複修） |
| 6 | Forge 自驗範圍 | **不需新 Mock 場景**（Stage 56 子項 1 是 Dashboard UI 補既有 service 端 case，不新增 service 端邏輯）— Forge 自驗 = 抽樣 3 個新補的 framework_* 場景 POST `/internal/mock/scenario` 看回應 + dotnet build + 對 FF 四十二/四十三 加單元 test |
| 7 | Token 計費 / CLAUDE_*.md prompt | 不動（Stage 56 不引入新 LLM call / Agent prompt 不變） |

### Stage 56 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— read `MockScenarioCard.razor` 既有 10 framework_* MudSelectItem pattern + `MockScenarioService.cs` 全 framework_* case 清單 + `DesignPrompts.cs` `TryParseDesignPetraDecision` line-based pattern + `TokenLogService.LogCliUsageAsync` 16 caller `usage` 來源鏈 spike | XS |
| **1** | Dashboard MockScenarioCard 補全 33 個 framework_* 場景（5+5+4+6+6+2+5）+ 對應 emoji map + frameworkHint switch case 補（feature flag 提示 + 流程說明）| M |
| **2** | FF 四十三 spike + 修：根因確認（16 caller `usage` 來源 / LLM provider cost 回傳機制）→ 修 LogCliUsageAsync caller 端傳 `usage` 路徑（spike 結論若涉及多 LLM provider → escalate Christ 縮範圍）| M |
| **3** | FF 四十二 修：`TryParseDesignIssues` 改 line-based scan pattern 對齊既有 helper（`startsWith('[')` → `JsonDocument.Parse` 嘗試）+ 加單元 test 涵蓋 `[MOCK]` 前綴 / 多 `[` 嵌套 / 純 array 三 case | S |
| **4** | conventions 補註：① `docs/conventions/csharp.md` 加「WorkflowType / WorkflowStep 跨 service fundamental type 不可移除」段（對齊 WorkflowEngine.cs Stage 55A 既有註解）② 加「Windows dev 機 Process.Start + .cmd PATHEXT 解法」段（Stage 48 候選 FF 落地）| XS |
| **5** | Forge 自驗：抽樣 3 framework_* 新場景 POST `/internal/mock/scenario` 看回應正確 + FF 四十二/四十三 單元 test pass + dotnet build AiTeam.slnx 0 errors | S |
| **6** | Version bump v3.45.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Dashboard/Components/Pages/Home/MockScenarioCard.razor` 既有 10 個 framework_* MudSelectItem（line 67-77 Stage 53B + Stage 55A） | 補新場景的 MudSelectItem pattern 對齊（emoji + label 中文 + Stage 標註慣例） |
| F2 | `src/AiTeam.Bot/Services/MockScenarioService.cs` 全 framework_* case 清單（line 84-203 + workflowType switch line 226-276 + emoji map line 334-349 + frameworkHint switch line 354-372） | 補 Dashboard UI 對應的 service 端完整名單 + 確認哪些已在哪些未在 |
| F3 | `src/AiTeam.Bot/Workflows/Design/DesignPrompts.cs` `TryParseDesignPetraDecision` (line 288) + `TryParseDesignAdjustmentEvaluation` (line 305) line-based scan pattern | FF 四十二 修法 reference（對齊既有 helper pattern）|
| F4 | `src/AiTeam.Bot/Services/TokenLogService.cs` `LogCliUsageAsync` (line 23-64) + 16 個 caller 全鏈路 grep `LogCliUsageAsync(` | FF 四十三 spike 根因第一步 — caller 對 `usage` 參數傳遞鏈 |
| F5 | `src/AiTeam.Bot/Agents/TokenUsage.cs` `TotalCostUsd` 欄位來源（CLI subprocess `--output-format json` 解析） + Claude CLI / Gemini / Anthropic API path 對 cost 回傳行為 | FF 四十三 spike 根因第二步 — LLM provider cost 來源差異 |

### 寫入點 spike 報告（在計劃書 Plan Mode 內）

Forge 完成 read 後在 Plan Mode 計劃書內報告：
1. **Dashboard 補場景對照表**：current 10 vs target 43 — 列出 33 個要補的 scenario key + 對應 emoji 建議 + frameworkHint 文案建議
2. **FF 四十三 根因確認**：16 caller `usage` 來源分類（CLI subprocess vs API call vs MockMode 早 return） + 各 LLM provider cost 行為 — 若涉及多 provider 需在計劃書 escalate 範圍縮放議題給 Christ 拍
3. **FF 四十二 line-based pattern 確認**：`startsWith('[')` line 是否會漏 `\n[\n  {...\n  }\n]\n` multi-line array literal case → 若漏，計劃書內提出 fallback 策略

---

## 子項 1：Dashboard MockScenarioCard 補全 33 個 framework_* 場景

### 補場景清單對照（service 端已有，Dashboard UI 缺）

| Stage | 場景數 | scenario keys |
|---|---|---|
| Stage 49 Appeal Loop | 5 | `framework_appeal_loop_fast_approve` / `_max_iter_approve` / `_max_iter_reject` / `_max_iter_escalate` / `_crash_recovery` |
| Stage 50 Kickoff | 5 | `framework_kickoff_consensus_round1` / `_round2` / `_max_iter` / `_escalate` / `_crash_recovery` |
| Stage 51 HITL 中途介入 | 4 | `framework_kickoff_mid_interrupt_apply` / `_cancel` / `_crash_during_wait` / `_no_trigger` |
| Stage 52 Design | 6 | `framework_design_consensus_round1` / `_round2` / `_needs_adjustment_approved` / `_needs_adjustment_needs_meeting` / `_no_demi` / `_crash_recovery_during_round` |
| Stage 53A Pipeline | 6 | `framework_pipeline_happy_path` / `_dev_plan_resume` / `_dev_resume` / `_qa_no_tests` / `_reviewer_fallback` / `_dev_plan_failed_escalate` |
| Stage 54 idempotency | 2 | `framework_design_crash_recovery_issue_idempotency` / `pipeline_dev_blocker_retry_idempotency` |
| Stage 55B HITL routing | 5 | `framework_pipeline_dev_intervention_hitl` / `_qa_intervention_hitl` / `_devplan_escalate_hitl` / `_devplan_unable_hitl` / `_split_task_proposal_hitl` |
| **合計** | **33** | |

### 範本（對齊 Stage 53B / 55A 既有 6+4 MudSelectItem）

```razor
@* Stage 49：FF 四十九 framework Appeal Loop 5 場景 *@
<MudSelectItem Value="@("framework_appeal_loop_fast_approve")">🚀 Stage49 — Appeal fast approve</MudSelectItem>
...
```

emoji + label 慣例：
- Appeal Loop: 🚀
- Kickoff: 🤝
- HITL 中途介入: ✏️
- Design: 🎨
- Pipeline: 🔧
- Crash Recovery: 💥
- Idempotency: 🛡️
- HITL routing: ⚠️

---

## 子項 2：FF 四十三 — TotalCostUsd 寫入覆蓋率 spike + 修

### 修法策略

1. **Spike 根因確認**（Forge Plan Mode 內）：
   - grep `LogCliUsageAsync(` 16 個 caller 對 `usage` 參數傳遞鏈
   - 確認各 LLM provider（Claude CLI subprocess / Gemini API / Anthropic API）對 `total_cost_usd` 的回傳行為
2. **修 caller 端 `usage` 來源**（spike 結論驅動）：根因分類後修對應 caller 端 `usage` 構造邏輯
3. **若涉及多 LLM provider** → 計劃書 Plan Mode 內 escalate 給 Christ 拍範圍縮放議題（接受 80%+ 涵蓋 vs 全 path 修）

### 驗證方法

- 跑 Mock `new_feature_with_proposal` 完整 pipeline → SQL 查 `SELECT COUNT(*) FILTER (WHERE TotalCostUsd IS NOT NULL) * 100.0 / COUNT(*) FROM token_logs WHERE created_at >= NOW() - INTERVAL '10 minutes'` → **目標寫入率 >= 90%**（vs 目前 0.3%）
- 若 spike 結論限縮範圍（只修 Claude CLI path）→ 對應 query 加 filter `WHERE Model LIKE 'claude-%'` 達 90%+ 即達標

---

## 子項 3：FF 四十二 — TryParseDesignIssues 邊界判斷重構

### 修法策略

對齊 `TryParseDesignPetraDecision` (line 288) + `TryParseDesignAdjustmentEvaluation` (line 305) 既有 line-based scan pattern：

1. 把 input 用 `\n` split 成 lines（保留順序）
2. 從**開頭往下掃**（因為 design issues content 通常在 prompt 輸出主體前段，不像 Petra decision 在尾段）
3. 找 `startsWith('[')` 的 line → 嘗試 `JsonDocument.Parse` → 成功 + 是 array → deserialize 回傳；失敗 → 繼續往下
4. 若 multi-line array literal（`\n[\n  {...\n  }\n]\n`）需特別處理 → fallback：找到 `[` 後往下 join lines 直到匹配 `]` 平衡（spike 階段確認此 case 是否存在）

### 驗證方法

加單元 test 涵蓋 3 case：
1. `[MOCK] 開頭` + 後接合法 array → 修法後正確 parse（vs 目前 `IndexOf('[')` 抓到 `[MOCK]` 邊界失敗）
2. 純 array 開頭（無前綴）→ 修法後正確 parse（regression 確認既有 case 不破壞）
3. 多 `[` 嵌套（如 prompt 範例中含 `[example]` 字串）→ 修法後跳過字串 `[`，找到真正 array

---

## 子項 4：conventions 補註

### 4.1 WorkflowType / WorkflowStep 跨 service fundamental type 段

加進 `docs/conventions/csharp.md`：

> **跨 service fundamental type 標記**：`WorkflowType` enum + `WorkflowStep` record（`src/AiTeam.Bot/Orchestration/WorkflowEngine.cs`）是 Stage 55A v4 漸進遷移後保留的跨 23 service fundamental type，**不是 dead code**。Stage 55A 已刪除原 `WorkflowEngine` class + `GetDecision` method + `NextAction` enum + `WorkflowDecision` record（v4 Pipeline framework 接管 routing），剩 type 定義廣泛被 `TaskGroupService` / `ProposalConfirmationService` / `ButtonCallbackRouter` / `MockScenarioService` 等使用。Stage 56 評估後拍板**不可移除** — 若未來重構需動，先 grep 全 reference 評估影響面。

### 4.2 Windows dev 機 Process.Start + .cmd PATHEXT 解法段

加進 `docs/conventions/csharp.md`（對應 Stage 48 候選 FF 落地）：

> **Windows dev 機 .NET `Process.Start` + `UseShellExecute=false` 不 honor PATHEXT for `.cmd` 解法**：production Linux Docker 容器內 `claude` 是 node-installed 無副檔名 binary 不踩此問題；Windows dev 機本機跑 framework workflow 需 (a) 建一個 `claude.exe` shim 把呼叫導向 `cmd.exe /c claude.cmd` 或 (b) `ClaudeCodeService` 內判 OS 改 invoke 方式。Stage 48 spike 揭露，Stage 56 落地 conventions 避免未來 onboarding 踩坑。

---

## 驗收情境

> 計劃書硬規則：本節獨立列出，不分散到子項內。每個非顯然點都有 Mock 場景或手動驗證步驟。

### V1：Dashboard MockScenarioCard 補全 33 場景 — UI 觸發抽樣驗

**觸發**：開 Dashboard → 任務中心 → MockScenarioCard 卡片 → 點「情境」下拉選單

**驗證**：
- 下拉選單可見 Stage 49-55B 全 33 個新 framework_* 場景（每場景 emoji + 中文 label + Stage 標註）
- **抽樣驗 3 場景**（Forge 自驗）：
  - `framework_appeal_loop_fast_approve`（Stage 49）→ 點觸發 → Snackbar 顯示啟動成功 + frameworkHint 提示「請啟用 MS Agent Framework Appeal Loop feature flag」
  - `framework_kickoff_consensus_round1`（Stage 50）→ 同上 + frameworkHint 提示 Kickoff feature flag
  - `framework_pipeline_dev_intervention_hitl`（Stage 55B）→ 同上 + Pipeline failure path 提示
- 其餘 30 場景由 Christ 線下挑樣 + Trial_v6 觀察期累積驗證（不在 Stage 56 自驗範圍）

### V2：FF 四十三 — token_logs.TotalCostUsd 寫入覆蓋率達標

**觸發**：跑 Mock `new_feature_with_proposal` 完整 pipeline（從 CEO proposal → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → done）

**驗證**：
- SQL 查 `SELECT COUNT(*) FILTER (WHERE TotalCostUsd IS NOT NULL) * 100.0 / COUNT(*) FROM token_logs WHERE created_at >= NOW() - INTERVAL '10 minutes'`
- **基線目標**：寫入率 >= 90%（vs 目前 0.3%）
- **spike 縮範圍 fallback**：若 spike 結論限縮 Claude CLI path → query 加 filter `WHERE Model LIKE 'claude-%'` 達 90%+ 即達標（在 Forge 結案紀錄揭露縮範圍理由 + Gemini/Anthropic API path 留 follow-up FF）

### V3：FF 四十二 — TryParseDesignIssues `[MOCK]` 前綴解析

**觸發**：單元 test 跑 3 case

**驗證**：
- Test 1：`"[MOCK] 開頭文字\n[\n  { \"line\": 10, \"file\": \"a.cs\", \"severity\": \"warning\", \"message\": \"x\" }\n]"` → 修法後回傳 1 element list（vs 目前 fall back null）
- Test 2：純 array 開頭 `"[\n  { ... }\n]"` → regression 確認 OK
- Test 3：含字串 `[example]` 嵌套 `"前綴 [example] text\n[\n  { ... }\n]"` → 跳過字串邊界正確 parse

### V4：conventions 補註 — 兩段文件加入

**觸發**：開 `docs/conventions/csharp.md`

**驗證**：
- 包含「跨 service fundamental type 標記」段（涵蓋 WorkflowType / WorkflowStep 不可移除說明）
- 包含「Windows dev 機 .NET Process.Start + .cmd PATHEXT 解法」段（涵蓋 (a) shim / (b) 判 OS invoke 兩條路）

### V5：build / regression 不破壞

**觸發**：`dotnet build AiTeam.slnx`

**驗證**：
- 0 errors / 0 new warnings
- v3.45.0 version bump 在 `src/Directory.Build.props` 正確套用
- Dashboard 既有 10 個 framework_* MudSelectItem 不受新加場景干擾（regression 確認下拉選單其他 case 點擊仍 OK）

---

## 技術約束

- v3.45.0 version bump（Stage 55B v3.44.0 + 1）
- `dotnet build AiTeam.slnx` 0 errors
- 不引入新 Migration（純 UI / parse / token logging caller 修，無 schema 改動）
- 不動既有 v4 framework path（Stage 49-55B 範圍邊界守住）
- Mock 場景 alias 用既有 service 端 case，不重複新增 service 端 switch case
- conventions 補註不加 `docs/conventions/refactor-sop.md` 或新檔，全進 `csharp.md` 既有檔案

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-05 | 初版規劃書建立（Aria）— Stage 56 = Trial_v6 前置條件統包 = Dashboard MockScenarioCard 補全 33 framework_* 場景（議題 1 = A 全補）+ FF 四十三 spike + 修一氣呵成（議題 2 = B Forge 自驗能力信任）+ FF 四十二 對齊既有 line-based pattern 重構 + WorkflowEngine 殘留評估補註（Aria 拿捏：grep 揭露 23 service 用 WorkflowType/WorkflowStep 是 fundamental type 不是 dead code → conventions 補註） + Stage 48 PATHEXT 候選 FF 落地 conventions（Aria 拿捏：Linux Docker production 無此問題不立 FF）。**規劃前期已 grep**：MockScenarioCard.razor 既有 10 framework_* / MockScenarioService.cs 全 43 framework_* case + workflowType switch + frameworkHint switch / DesignPrompts.cs 3 個 TryParseXxx helper pattern / TokenLogService.LogCliUsageAsync + TokenUsage record / WorkflowEngine.cs Stage 55A 既有註解 — 對齊自省點 #23 規劃前期 grep 紀律。|

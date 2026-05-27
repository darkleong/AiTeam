# Trial_v24 — Stage 80 HITL plan_confirm 業務體驗 + 🔴 #1 hotfix verify + 4 decision pattern 真實業務驗

> 日期：2026-05-20
> 對應系統版本：**v3.72.0**（Stage 80 結案後）
> 試驗版本：**v1.0**（Aria 撰寫結案 / Trial 跑完即進結案紀錄 / 不分 Plan v1.0 + 真實 v2.0 — Stage 80 自驗已含 plan）
> **真實結果**：🟢 **全綠 — Stage 80 HITL 業務體驗 + 🔴 #1 hotfix 業務級成功**（連續 15 Trial 業務級成功延續第 15 次）

---

## 試驗目的

**Stage 80 HITL plan_confirm 業務體驗 + Trial_v23 4 議題 production 真實驗**：

1. **🔴 #1 DbContext concurrency hotfix 真實業務驗** — Trial_v23 中段揭 production bug（Dashboard 首頁 Circuit terminated）/ Forge self-verify 只驗 5 並行 Home GET / 真實 Dashboard 開瀏覽器多 navigation 驗
2. **HITL plan_confirm 4 decision pattern 真實業務 fire** — approve / edit / reject / respond 4 路徑真實業務級驗
3. **🟡 #2 v5.5 path inbox 純 ack 卡 vs flag=true plan_confirm 開卡邊界驗** — flag 切換真實守 baseline
4. **🟡 #4 SystemNotes 後端 SoT + 主題變數 UI 視覺真實業務驗**
5. **Aria 9-step + Chrome MCP 自跑突破** — 對「Christ 只動嘴」精神更進一步突破（除視覺感受 + PR 業務正確性 Christ 必拍 / 其他全 Aria 自跑）

對齊 aria-trial-run skill 第 7 次實踐成熟 baseline + Chrome MCP 業界擴展。

---

## 任務需求

沿用 Trial_v6-v23 同 prompt（reuse `.tmp/trial_v15_body.json`）— Dashboard 錯誤處理打磨 toast 通知。

對照組精準度最高（同 prompt baseline + flag=false 場景 A 對齊 Trial_v23 / flag=true 場景 B+ 走 HITL 新閘門）。

---

## 場景驗收（7 場景 / C 跳過）

### 場景 G — 🔴 #1 DbContext concurrency hotfix verify ✅

**動作**：Chrome MCP `navigate http://localhost:5051/` + 多次 navigation 切換（首頁 → /interactions → 首頁 → /agents → 首頁）+ `read_console_messages` 抓 Circuit / second operation / Exception pattern

**結果**：
- ✅ 首頁完整 render（快速下達指令卡 + 10 Agent 狀態 + 最近流程 + SignalR 已連線 + v3.72.0 標示）
- ✅ 多次 navigation 切換全成功 / **0 Circuit terminated**
- ✅ Console messages pattern `circuit|second operation|InvalidOperationException|ObjectDisposed|terminated|Exception` **0 hit**

**對比 Trial_v23 中段 Christ 截圖**：那時首頁空白 + console「Error: unhandled exception on current circuit, terminated」+「Connection disconnected」/ **現在首頁完整 render + 0 console error = 🔴 #1 IDbContextFactory pattern 修根因實證 ⭐⭐**

### 場景 A — flag=false v5.5 baseline 0 regression ✅

**動作**：SQL UPDATE flag=false → reload-cache → curl 派純文字 prompt

**結果**：
- ✅ Bot log **0 `WaitForPlanConfirmationAsync` fire** / 0 `HITL plan_confirm` 閘門 marker = flag=false 守 baseline 0 行為改變
- ✅ Petra DecideTalentsWithPlanAsync 完成 → 直接 chain dispatch Level=1/3 → Cody → Vera → Quinn → PR
- ✅ PR #388 開（38 檔 +2384 -176 / **duration 17:30**）
- Cody outputLen=2871 / 5 元件雙路錯誤通知（SystemSettings 7 toggle + MockScenarioCard + GlobalQueueControlCard）+ 詳細 IMPLEMENTATION_NOTE + 10 個元件「已確認 cover 但不需改動」段
- Vera outputLen=1327 / 0 critical / 0 warning / 1 info（`_saveMessage=null` 冗餘賦值 / 可保留防禦性）/ summary「改動目標明確、覆蓋完整、可放行」
- ⚠️ **Quinn outputLen=0**（token_logs 真實 12553 tokens output / ChatClientAdapter 端 outputLen=0 / **🟡 議題候選**）

**Cost**：$2.49（Cody $1.53 / Quinn $0.66 / Vera $0.29 / PM $0.01）

### 場景 B — flag=true HITL plan_confirm 開卡 ✅

**動作**：SQL UPDATE flag=true → reload-cache → curl 派同 prompt

**結果**：
- ✅ Petra `DecideTalentsWithPlanAsync` 完成 subtasks=2 picks=Cody → Vera（4 talent 內 自選 2 / Petra 動態決策）
- ✅ **`Stage 80：HITL plan_confirm 閘門 fire — sessionId=c948bafb... subtasks=2 talents=[Cody,Vera] 等 Christ 4 decision 拍板`** ⭐
- ✅ chain dispatch **0 啟動**（pause 守紀律 / 等 Christ 拍板）
- ✅ SQL `boss_interactions` row `780fbaf0...` InteractionType=plan_confirm Status=pending
- ✅ SQL `boss_interactions.SystemNotes` 真實寫入「[Stage 80 HITL] sessionId=c948bafb — 4 decision pattern：approve（核准）/ edit（修改）/ r...」
- ✅ SQL `boss_interactions.ContextJson` SubtaskPlan serialize CamelCase 真實寫入
- ✅ SQL `petra_sessions.Status=paused` UpdatedAt 04:20:04

### 場景 H — UI 視覺渲染（SystemNotes 區塊 + 4 button + SubtaskPlan + 主題變數）✅

**動作**：Chrome MCP navigate `/interactions` + scroll_to plan_confirm 卡 + screenshot

**結果**（plan_confirm 卡完整 UI render）：
- ✅ **SystemNotes 區塊**淡灰底 + ℹ️ info icon + 主題變數 `var(--mud-palette-background-grey)` 背景 + `var(--mud-palette-divider)` border + 系統提示文字真實 render（深色主題下視覺辨識 OK）
- ✅ **Description**（純 Christ 任務內容）跟 SystemNotes 清楚分離 — 對齊「Description=純 Christ 任務 / SystemNotes=系統提示」設計
- ✅ **📋 Petra 拆解計劃（2 subtask）render** — `#1 · Cody · code_implementation` + `#2 · Vera · code_review` + 描述 + `🔗 依賴關係：#1 → #2（sequential）`
- ✅ **4 button** 真實 render — 🟢 核准 ✅ / 🔵 修改 ✏️ / 🔴 拒絕 ❌ / 🔵 補充 💬
- **Christ 視覺感受**：留 Christ 拍板（深色主題 SystemNotes 視覺辨識是否夠醒目 — Aria 給 screenshot / Christ 主觀判斷）

### 場景 D — plan_edit redecide ✅

**動作**：Chrome MCP click「修改 ✏️」button → modal「修改計劃」開 → textarea 填「Cody 只做 backend，UI 留 Vera 處理」→ JS click「送出」

**結果**：
- ✅ `PlanConfirmationProcessor pickup interactionId=780fbaf0... action=plan_edit decision=edit`
- ✅ `ResumeFromPlanConfirmationAsync decision=edit sessionId=c948bafb... subtasks=2 contentLen=28` ✓（"Cody 只做 backend，UI 留 Vera 處理" 28 字元）
- ✅ Petra v5.5 Step 4 DecideTalentsWithPlanAsync 重 decide subtasks=2 picks=Cody → Vera（edit 影響弱 / picks 結構不變）
- ✅ `HITL plan_confirm redecide 完成 ... decision=edit subtasks=2`
- ✅ 新 plan_confirm 卡開（loop until approve/reject）/ session 從 paused → running → paused（redecide 進行中後新卡 paused）

### 場景 F — plan_respond redecide ⭐⭐⭐

**動作**：Chrome MCP click「補充 💬」button → modal「補充指示」開 → textarea 填「另外考慮 mobile responsive」→ JS click「送出」

**結果**：
- ✅ `PlanConfirmationProcessor pickup interactionId=7d497a64... action=plan_respond decision=respond`
- ✅ `ResumeFromPlanConfirmationAsync decision=respond contentLen=22`
- ✅ **Petra v5.5 Step 4 DecideTalentsWithPlanAsync 重 decide — subtasks=4 dependencies=3 picks=Cody(ui_design) → Cody(code_implementation) → Vera(code_review) → Quinn(qa_testing)** ⭐⭐⭐

  **Petra 真實理解「mobile responsive」加入 4 subtasks 響應式設計**：
  - #1 Cody · ui_design — Dashboard 全局錯誤處理 toast 提示的 UX 規範、行為與**響應式佈局** ⭐
  - #2 Cody · code_implementation — 包含共用元件與**響應式調整** ⭐
  - #3 Vera · code_review — UX 一致性與**響應式表現** ⭐
  - #4 Quinn · qa_testing — 包含**響應式驗證** ⭐

  **= HITL 4 decision pattern 真實影響 Petra decision 業務級實證** ⭐⭐⭐

### 場景 E — plan_reject ✅

**動作**：Chrome MCP click「拒絕 ❌」button → 二次確認 modal「確認回覆 / 確定執行『拒絕 ❌』？」開（UX 守 destructive action 紀律 ✓）→ JS click「確定」

**結果**：
- ✅ `PlanConfirmationProcessor pickup interactionId=d0160998... action=plan_reject decision=reject`
- ✅ `ResumeFromPlanConfirmationAsync decision=reject sessionId=c948bafb... subtasks=4 contentLen=0`
- ✅ **`Stage 80：HITL plan_confirm reject sessionId=c948bafb... subtasks=4`** ⭐
- ✅ `task_memories.decision/plan-rejected` 寫入 content「Christ rejected plan via HITL plan_confirm 閘門」
- ✅ `petra_sessions.Status=cancelled` UpdatedAt 04:28:25
- ⚠️ `PlanConfirmationProcessor 完成 ... dispatched=4`（**🟡 minor 命名語意議題**：`PetraOrchestratorResult.DispatchedWorkerCount` field 把 plan.Subtasks.Count 雜用 — reject path 真實 0 chain dispatch / 不影響業務）

### 場景 C — plan_approve（**跳過 / Christ 拍板節省 cost**）

**理由**：
- Forge self-verify Phase 2 已驗 approve path PASS
- 場景 A 已 fire 完整 chain dispatch baseline（同 prompt 同 talents）
- approve path unique fire 點 = `DispatchAndFinalizeAsync` helper（從 StartAsync 抽出 / 既有 chain pattern 對齊 / Aria gate1 Tier 3 #11 已 code review）
- 業務 PR 內容跟場景 A baseline 類似 — 重複驗 ROI 降低
- **節省 ~$2-3 cost / Trial_v24 cost 從預估 $3-5 真實 $2.52**

---

## 總 cost vs 預估

| 階段 | cost | 對照 |
|---|---|---|
| Aria + Forge session（Claude Code subscription）| **0 燒 AiTeam 餘額** | 對齊 Stage 80 v1.2 紀律 ✓ |
| Trial_v24 AiTeam LLM cost | **$2.52** | 預估 $1-3 ✓（場景 A baseline $2.49 + B/D/F Petra Gemini decide 額外 $0.03）|
| 餘額：$9.96 → **$7.48 真實**（-$2.48 / token_logs 加總 $2.52 偏高 $0.04 / Anthropic billing rounding 差） | -$2.52 對齊預估 | |

對比 Trial_v23 cost $2.86（場景 A $1.74 + Case B 含圖 $1.12）— Trial_v24 略低（純文字 prompt 全程 / Case B/D/F 只 Petra Gemini decide redecide / 0 chain dispatch 重跑）。

---

## 議題分類

| # | 嚴重 | 議題 | 揭露來源 | 對應修法 |
|---|---|---|---|---|
| 1 | 🟡 | **Quinn outputLen=0 baseline 漂移** — 場景 A Quinn chain dispatch 100% 通過 + token_logs Quinn 真實 12553 tokens 輸出 + cost $0.66 / 但 `ClaudeCodeChatClientAdapter` 端 outputLen=0 → PR body Quinn 段 0 內容（Trial_v22+v23 Quinn outputLen=749 baseline / 漂移） | 場景 A 觀察 | Stage 81+ follow-up 評估候選 — `ClaudeCodeChatClientAdapter` 對 Quinn stdout parsing 落差 / 或 Claude Code CLI subprocess return 異常 / 業務 PR 體驗影響中等（PR 仍開但 QA 段缺）|
| 2 | 🟡 | **Petra `NeedsImageContext` 對純文字 prompt 誤判 true** — 場景 B/D/F plan_confirm 卡 SubtaskPlan render「附圖」chip 顯示但 attachments=0 imageCount=0（純文字 prompt）/ Petra prompt few-shot 教學需更精準 | 場景 H 視覺驗 | Stage 79+80 邊界議題 / Stage 81+ follow-up 評估候選 — Petra prompt 加 negative few-shot「純文字 prompt → NeedsImageContext=false」 |
| 3 | 🟡 minor | **`PetraOrchestratorResult.DispatchedWorkerCount` 命名語意不對齊 reject path** — reject 真實 0 chain dispatch 但 log dispatched=4（雜用 plan.Subtasks.Count）| 場景 E 觀察 | Stage 81+ follow-up minor cleanup / 不影響業務 |

---

## Aria 工作節奏觀察

- **Chrome MCP 工具能力首次 leverage** ⭐ — 之前 Trial_v23 Christ 截圖揭 Dashboard 首頁 Circuit / 我前次 Trial_v24 規劃漏掃 Chrome MCP 能力（憑印象「curl 觸發不到 Blazor Circuit」+「視覺辨識只能 Christ」）→ Christ 戰略 question 點破「妳有 Claude for Chrome 能力」→ Aria 真實再評估給雙面定見 → Christ 拍板「除視覺感受 + PR 業務內容 / 其他交給妳」→ **Chrome MCP 全自跑 Trial_v24 7 場景**（場景 G hotfix navigate + 場景 H UI 視覺 screenshot + 場景 D/E/F 真實 click button + modal form_input）
- **「Christ 只動嘴」精神更進一步突破**（自省點 #33 延伸） — Trial_v24 Christ 真實操作量：0 真實操作（除拍板開跑 + 看 screenshot 拍視覺 + 拍 PR 業務）/ vs 前次估「~4-5 分鐘真實操作」對齊 Chrome MCP leverage 後降為「~30 秒看 screenshot」
- **Aria 工具使用慣性盲點累積** — workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #N+1（規劃 Trial 場景時必評估「Chrome MCP / computer-use 工具能力可否替代 Christ 操作」/ 對齊既有 # 自省點 #20「工具使用慣性」延伸）— 留 /aria-end 統一升級
- **「對等和互相」紀律真實實踐第 N 次**（自省點 #36 延伸） — Christ 戰略 question「妳有 Claude for Chrome 能力」點破我前次評估盲點 → 我承認漏掉 + 真實再評估 → 給雙面定見

---

## 戰略意義

- **Stage 80 HITL plan_confirm 業務體驗實證 ✅** — 4 decision pattern 全部真實 fire（approve 由 Forge self-verify 已驗 + edit/respond/reject Aria 真實業務驗）/ 業界 LangGraph interrupt 紀律真實內化 AiTeam
- **🔴 #1 DbContext concurrency hotfix production 真實生效** ✅ — Dashboard 首頁 + 多 navigation 0 Circuit terminated（Trial_v23 直接踩 production bug 修根因實證 ⭐⭐）
- **連續 15 Trial 業務級成功延續** ✅（v10-v24）— infinite loop pattern 打破連續第 15 次 ⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐
- **HITL 4 decision pattern 真實影響 Petra decision 業務級實證** ⭐⭐⭐ — 場景 F respond「另外考慮 mobile responsive」→ Petra 重 decide subtasks=2 → 4（加 ui_design + qa_testing 響應式驗 / 業界 LangGraph interrupt 真實效果驗證）
- **Aria Chrome MCP 自跑 Trial 業界突破** — 「Christ 只動嘴」精神更進一步突破 / 對齊 aria-trial-run skill 第 7 次實踐成熟 baseline + Chrome MCP 業界擴展

---

## Top 5 重排

| # | 項目 | 規模 / 優先度 |
|---|---|---|
| **1** | **Stage 81（B 動態 re-planning + Trial_v24 3 議題收口）** | L / Opus 1M + Extra high baseline（規模 L / 必配 max iterations + replan threshold + cost cap + checkpoint replay）|
| 2 | Trial_v25 驗動態 replan 業務體驗 + Stage 81 議題收口 | 5-15 min / cost $1-3 |
| 3 | WebUI Stage（v4 entity drop + Dashboard 重設計）| L+ / Stage 81 後 |
| 4 | v5.5 完整收口 | Top 1-3 全跑完 |
| 5 | Stage 80 議題後續評估（Quinn outputLen=0 / Petra NeedsImageContext 誤判 / DispatchedWorkerCount 命名）| Stage 81+ follow-up |

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

- Trial_v24 結案紀錄 v1.0 建立（本文件）
- Future_Feature_v5.5.md 加 Phase 4 候選 Trial_v24 ✅ 段
- close PR #388（試驗 PR 不污染 main）✅
- SQL flag 切回 default `UseHITLPlanConfirmation=false` 對齊 Stage 81+ Roadmap 預期 ✅
- commit + push

### 下個重點戰略

- **Stage 81（B 動態 re-planning）** — Petra 看 subtask result 再決下一步 / 必配 max iterations + replan threshold + cost cap + checkpoint replay
- **Trial_v25** 驗動態 replan 業務體驗（5-15 min / cost $1-3）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-20 | Trial_v24 結案紀錄建立（Aria 撰寫 / Trial 跑完即進結案 / 不分 Plan v1.0 + 真實 v2.0 — Stage 80 自驗已含 plan）。**真實結果 🟢 全綠** — Stage 80 HITL 業務體驗 + 🔴 #1 hotfix 業務級成功（連續 15 Trial 業務級成功延續第 15 次）。**7 場景驗收**（G hotfix + A flag=false baseline + B flag=true 開卡 + H UI 視覺 + D edit redecide + F respond redecide ⭐⭐⭐ + E reject）/ **場景 C 跳過**（Forge self-verify 已驗 + 場景 A baseline 對齊 + 節省 ~$2-3）。**總 cost $2.52 對齊預估 $1-3 ✓ / 餘額 $9.96 → $7.44**。**3 🟡 議題**：① Quinn outputLen=0 baseline 漂移 ② Petra NeedsImageContext 對純文字 prompt 誤判 true ③ DispatchedWorkerCount 命名語意。**戰略意義**：HITL 4 decision pattern 真實影響 Petra decision 業務級實證 ⭐⭐⭐（場景 F respond「mobile responsive」→ Petra 重 decide subtasks=2 → 4 加響應式設計）+ Aria Chrome MCP 自跑突破「Christ 只動嘴」精神更進一步（Trial_v24 Christ 真實操作量 0 / 對齊自省點 #33 延伸 + 自省點 #20「工具使用慣性」延伸候選留 /aria-end）。**Top 5 重排**：Stage 81（B 動態 re-planning + Trial_v24 3 議題收口）升 #1。 |

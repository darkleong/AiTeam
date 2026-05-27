# Trial_v26 — Stage 82 雙修法驗證（Quinn outputLen + PetraSessionId 透傳）+ Stage 81 動態 replan production 真實 fire 補驗（部分達成）

> 日期：2026-05-21 規劃 + 真實跑 + 結案 in-place
> 對應系統版本：**v3.74.0**（Stage 82 結案後）
> 試驗版本：**v1.0**（Trial 真實跑完 Aria 結案 in-place — Trial_v26 Plan 跳過 v1.0 規劃步驟直接 9-step 開跑 / 對齊「對冗餘不容忍」紀律延伸 / 結案紀錄為唯一文件）
> 真實結果：🟢 **核心驗收全綠 + 1 部分**
> - **Case A baseline ✅✅✅** Stage 82 雙修法（Quinn outputLen + PetraSessionId 透傳）production 真實生效驗證完整
> - **Case C 動態 replan 🟡 部分**：Stage 80 HITL plan_confirm fire + Stage 81 DetectReplanTrigger 紀律正確不誤觸發 / 但 replan_confirm UI 卡 production 真實 fire 沒驗到（Vera 真實 review 評 OK 無 critical / W1 預警 LLM nature 不可控真實踩）
> - **戰略 finding ⭐ — 業界對齊驗證**：WebSearch 3 query 揭 AiTeam Petra LLM API call + worker Claude Code CLI + HITL 兜底三層分工**對齊業界主流 supervisor pattern**（LangGraph / Databricks / Claude Agent SDK 共識）

---

## 試驗目的

1. **Stage 82 子項 1 ⭐⭐⭐ Quinn outputLen 修根因 production 真實生效驗** — Trial_v22-v25 連續 4 Trial Quinn outputLen=0 → Stage 82 stream-json accumulate fallback → Trial_v26 outputLen 必非 0
2. **Stage 82 子項 2 ⭐⭐⭐ PetraSessionId 透傳 production 真實生效驗** — Trial_v25 PM 24 row 但 PetraSessionId 0/24 全 NULL → Stage 82 AsyncLocal scope 透傳 → Trial_v26 PM token_logs PetraSessionId 必非 NULL
3. **Stage 82 子項 3 SubtaskPlanParser preamble 防呆 production 也不踩** — xUnit 已 cover / Trial_v26 production Sonnet 4.6 純 JSON 紀律維持
4. **Stage 81 動態 replan + HITL retry gate production 真實 fire 補驗**（Trial_v25 abort 留 Trial_v26）— 4 decision routing wire 接通

---

## 任務需求

### Case A baseline（沿用 Trial_v22-v25 同 prompt）

`docs/experiments/Trial_v15_body.json` Dashboard 錯誤處理打磨 prompt — flag 全 default false。

**重要驗收**：嵌驗 Stage 82 子項 1 ⭐⭐⭐ Quinn outputLen > 0 + 子項 2 ⭐⭐⭐ PM PetraSessionId 透傳。

### Case C 動態 replan production 真實 fire（Trial_v25 abort 補驗）

`.tmp/trial_v25_case_c_body.json`（PetraDispatchWorker retry + thread safety + SemaphoreSlim lock 改動 / 容易觸發 Vera critical）

雙 flag 切：`UseDynamicReplanning=true` + `UseHITLPlanConfirmation=true`（Stage 81 補強 #A 紀律 — 雙 flag 綁定）

期望 chain：Petra DecideTalentsWithPlanAsync → plan_confirm 卡 → Christ approve → Cody → Vera 標 critical → DetectReplanTrigger fire → InvokePetraReplanAsync → replan_confirm 卡 → Christ approve（C1）→ 同 subtask 重 dispatch → chain 完成 + PR

### Case D max iter / cost cap intervention（可選）

對齊 Trial_v25 Plan / 餘額充足才跑。

---

## 真實跑結果

### Case A baseline 全綠 ⭐⭐⭐⭐⭐

**Petra Sonnet 4.6 拆 4 subtasks**：Cody → Cody fix → Vera → Quinn（dependencies=3 / 對齊 baseline + 加 review-fix middle）

| Worker | InputTokens | OutputTokens | TotalCostUsd | outputLen | PetraSessionId |
|---|---|---|---|---|---|
| **PM** | 3748 | 325 | $0.02 | — | **`3c4e95a6...` ✅** |
| Cody (1) | 28 | 35072 | $1.63 | 2801 ✅ | `3c4e95a6...` ✅ |
| Cody (2 fix) | 3 | 768 | $0.08 | 933 ✅ | `3c4e95a6...` ✅ |
| Vera | 15 | 18168 | $0.71 | 1249 ✅ | `3c4e95a6...` ✅ |
| **Quinn** | 22 | 25849 | $1.02 | **1352 ✅✅✅** | `3c4e95a6...` ✅ |

🎯 **Stage 82 子項 1 ⭐⭐⭐ Quinn outputLen 修根因 完全成功**：
- Trial_v22-v25 連續 4 Trial Quinn outputLen=0 → **Trial_v26 outputLen=1352** ✅
- Quinn 真實燒 25849 output tokens / outputLen 1352 = stream-json accumulate fallback 真實 fire（`type=result` row `result` 欄位空 → fallback `type=assistant.message.content[].text` accumulated）

🎯 **Stage 82 子項 2 ⭐⭐⭐ PetraSessionId 透傳 完全成功**：
- Trial_v25 PM 24 row PetraSessionId 0/24 全 NULL → **Trial_v26 PM + 4 worker 5 row 100% PetraSessionId 透傳** ✅
- session SessionCostUsd `$3.457007` 對齊 token_logs 加總 $3.46 ✅（Stage 81 子項 5 cost tracking 連動正確）

🎯 **Stage 82 子項 3 SubtaskPlanParser preamble 防呆**：production Sonnet 4.6 純 JSON 紀律維持（xUnit 已 cover preamble strip robust path / Trial_v26 production 不踩）

**PR #391 開啟**：45 檔 / +2771 -178 / 業務內容對齊 Trial_v22-v25 baseline / close 對齊「Trial PR 不污染 main」紀律 ✅

**Case A cost**：$3.46

---

### Case C 動態 replan production 真實 fire 🟡 部分達成

**雙 flag 切 + reload-cache** ✅

**Petra Sonnet 4.6 拆 2 subtasks**：Cody → Vera（dependencies=1 / 為 Vera-critical scenario 不加 Quinn）

**HITL plan_confirm fire** ✅：
- `1f170880` plan_confirm BossInteraction row 開卡 / Status=pending
- Petra `PauseAsync` + return `Paused` 工廠 fire
- Chrome MCP click ref_154「核准」按鈕 **沒生效**（Blazor render 後 ref 失效 / 🟡 Trial 期間 Chrome MCP click ref 議題）
- SQL fallback update Status=responded + ResponseAction=plan_approve（對齊 aria-trial-run skill「Trial 期間環境設定議題直接 fallback」紀律）
- PlanConfirmationProcessor 3s polling 接手 → `ResumeFromPlanConfirmationAsync` decision=approve ✅

**chain dispatch 完成**：
- Level 1 Cody outputLen=3125 ✅
- Level 2 Vera outputLen=2801 ✅
- **Vera review `"critical":[]` 空 array** + 2 warning + 1 info / Vera 評「整體實作正確，無 critical 問題」

**🟢 Stage 81 DetectReplanTrigger 紀律正確不誤觸發** ✅：
- Vera 真實 review OK = `DetectReplanTrigger` Regex pattern `"critical":\[{...}]` 非空 不 match → 不 fire ✅
- 對齊紀律設計（避免 false positive）

**🟡 但 Case C 主軸 ⭐⭐⭐ replan_confirm UI 卡 production 真實 fire 沒驗到**：
- Vera 不可控 LLM nature 真實踩（Trial_v25 W1 預警接受）
- `InvokePetraReplanAsync` + `ResumeFromReplanConfirmationAsync` 4 decision routing **production 真實 fire 留 Trial_v27 補**
- Stage 81 xUnit unit test layer 已 cover 4 decision routing（126 pass）/ production layer 留 Trial_v27

**PR #392 開啟**：close 對齊紀律 ✅

**Case C round 1 cost**：$1.91

### Case D skip

Case C round 1 已揭 Vera 不可控真實 / 餘額管理 + 「LLM 隨機性」議題 Trial_v27 補驗效率更高（vs Case D max iter / cost cap 模擬 intervention 也有同類 LLM 觸發隨機性議題）。

---

## 業務評分矩陣

| 維度 | 預期 | 真實 |
|---|---|---|
| Stage 82 子項 1 Quinn outputLen > 0 | ✅ 必非 0 | **✅✅✅ outputLen=1352** vs Trial_v25=0 連續 4 Trial 0 突破 |
| Stage 82 子項 2 PM PetraSessionId 透傳 | ✅ 非 NULL | **✅✅✅ 5 row 100% 透傳** vs Trial_v25 PM 0/24 NULL |
| Stage 82 子項 3 SubtaskPlanParser preamble 防呆 | xUnit cover | ✅ production Sonnet 4.6 純 JSON 紀律維持 / 0 production 踩 |
| Stage 80 HITL plan_confirm fire | ✅ | ✅ |
| Stage 81 DetectReplanTrigger 不誤觸發 | ✅ | ✅（Vera "critical":[] 空 → 不 fire 紀律正確）|
| Stage 81 動態 replan UI 卡 production 真實 fire | ✅ | 🟡 Vera 不可控 → 沒觸發 / 留 Trial_v27 |
| Case A baseline 0 regression PR 真開業務內容 | ✅ | ✅ PR #391 45 檔 +2771 -178 |
| Aria 9-step + Chrome MCP 自跑 | ✅ | ✅ 9-step 第 14 次實踐 / Chrome MCP click ref 失效 議題揭 |
| cost vs token_logs 一致性 | ✅ | ✅ Case A SessionCostUsd $3.46 對齊 |

**Aria 業務評分** ⭐⭐⭐⭐ 4/5（Case A 完全成功 + Case C 部分達成 / Case C 主軸 replan UI 卡 production fire 留 Trial_v27）

---

## 戰略 finding ⭐ — 業界對齊驗證（WebSearch 3 query）

Christ 戰略 question 點破「Aria 是否憑推論」+ 要 WebSearch verify「Petra 純 LLM API 設計 vs Claude Code CLI 業界主流」。

### 業界主流結論

1. **Supervisor / Orchestrator 用 LLM API call 是主流** ✅
   - LangGraph / Databricks / Claude Agent SDK 都是這個 pattern
   - Quote: 「Nodes are functions (LLM calls, tool invocations, conditional checks), edges define flow, state is typed and persisted」
   - **AiTeam Petra 純 LLM API call 設計對齊業界主流**

2. **「Supervisor 不該動 codebase」是業界紀律** ✅
   - Quote: 「The orchestrator maintains global state, handles error recovery, and decides when the overall task is complete, while **workers are stateless and focus on a single capability**」
   - Quote: 「an agent that only knows about specific files writes better code than one juggling an entire codebase」
   - 理由：supervisor 看 codebase 增加 context overflow + hallucination 風險

3. **Claude Code CLI 是「開發工具」不是 runtime orchestration**
   - Quote: 「Use Claude Code to write and debug your LangGraph application, then deploy that application to run in production」
   - **AiTeam 分工正好對齊**：Cody/Vera/Quinn 用 Claude Code CLI（執行層）/ Petra 用 LLM API call（runtime orchestration 層）

4. **業界共識真實風險 — Hierarchical Information Loss**
   - Quote: 「each summarization step between levels risks dropping details that turn out to be essential」
   - Quote: 「if the orchestrator hallucinates the plan, all downstream work is wasted」
   - **Trial_v26 Case C 踩的就是這個** — Vera review 寬鬆 → Petra 收到「沒 critical」訊號 false negative

### 業界推薦 3 解法（不是「supervisor 切 CLI」）

1. **升級 worker review 紀律** — 加強 Vera prompt 紀律品質
2. **Read-only Codebase Explorer agent**（獨立角色 / 不污染 supervisor）
   - Quote: 「a fast, read-only agent can explore codebases without modifying files, useful for quickly finding files by patterns, searching code for keywords, or answering questions about the codebase」
3. **HITL 兜底**（接受 supervisor LLM 局限 / 人類拍板補強）— **AiTeam Stage 80 plan_confirm + Stage 81 replan_confirm 4 decision 卡已實現 ✅**

### 戰略意義

**AiTeam v5.5 三層分工（Petra LLM + worker CLI + HITL 拍板）業界對齊驗證 ⭐⭐⭐**：
- Petra 用 LLM API call ✅
- Worker 用 Claude Code CLI ✅
- HITL plan_confirm + replan_confirm 4 decision 拍板 ✅

撤回前面「Petra 切 CLI」FF 候選（業界不推薦 / cost 5x + hallucination 風險）。

### Sources（業界 reference / Forge 補查用）

- [Multi-Agent Orchestration Platforms: Build vs Buy in 2026 (Augment Code)](https://www.augmentcode.com/tools/multi-agent-orchestration-platforms-build-vs-buy)
- [LangGraph Supervisor Pattern: Orchestrating Multi-Agent Teams in 2026 (CallSphere)](https://callsphere.ai/blog/langgraph-supervisor-multi-agent-orchestration-2026)
- [Why You Can't Use Claude Sub-Agents to Run AI Agents Like LangGraph (Medium)](https://medium.com/@himanshum14/why-you-cant-use-claude-sub-agents-to-run-ai-agents-like-langgraph-or-langchain-f7ea8b067649)
- [LangGraph + Claude Agent SDK: Ultimate Guide 2026 (mager.co)](https://www.mager.co/blog/2026-03-07-langgraph-claude-agent-sdk-ultimate-guide/)
- [Swarm vs. Supervisor: Multi-Agent Architecture Guide (Augment Code)](https://www.augmentcode.com/guides/swarm-vs-supervisor)
- [Supervisor Agent Architecture: Orchestrating Enterprise AI at Scale (Databricks)](https://www.databricks.com/blog/multi-agent-supervisor-architecture-orchestrating-enterprise-ai-scale)
- [The Code Agent Orchestra (AddyOsmani)](https://addyosmani.com/blog/code-agent-orchestra/)

---

## 議題分類

| 議題 | 嚴重度 | 修法方向 | Stage / Trial |
|---|---|---|---|
| Stage 82 子項 1 Quinn outputLen 修根因 production 真實生效 | 🟢 已驗證 | — | Trial_v26 ✅ |
| Stage 82 子項 2 PetraSessionId 透傳 production 真實生效 | 🟢 已驗證 | — | Trial_v26 ✅ |
| Stage 81 動態 replan UI 卡 production 真實 fire 沒驗到 | 🟡 W1 預警接受 | Trial_v27 重派 Vera-critical prompt 或 SQL fallback 反向驗 routing wire | Trial_v27 |
| Chrome MCP click ref 失效（plan_confirm 卡點 ref_154 沒生效）| 🟡 工具邊界 | Chrome MCP 對 Blazor InteractiveServer + signalR 重 render 後 ref 可能 stale / 改用 SQL fallback path 對齊 aria-trial-run skill 紀律 / 未來評估 find + click 兩步重新拍時序 | （未來 Trial 觀察）|
| Petra 拆 plan 規模差異（Case A 4 subtask vs Case C 2 subtask）| 🟢 LLM 自適應 | — | （Sonnet 4.6 規劃力對任務複雜度自適應 / 對齊 Trial_v25 5-subtask review-fix cycle 同 LLM nature）|
| 「Vera review 寬鬆 → Petra false negative」hierarchical information loss | 🟡 戰略級 新 FF 候選 | 業界推薦 3 解法 — ① Vera review 紀律升級 ② Read-only Codebase Explorer agent ③ HITL 兜底（已實現） | 新 FF 候選 / Christ 拍板 |

---

## Cost 真實 vs 預估

| 階段 | cost 來源 | 預估 | 真實 |
|---|---|---|---|
| Aria + Forge session | Claude Code subscription | 0 燒餘額 | ✅ 0 |
| Case A baseline（Sonnet 5 worker call）| Cody+Vera+Quinn+PM | $2-3 | **$3.46**（接近上緣）|
| Case C round 1（plan_confirm + Cody+Vera）| PM+Cody+Vera | $1-2 | **$1.91** |
| Case D skip | — | $0.5-1 | $0 |
| **Trial_v26 total** | | $5-8 | **$5.37** ✅ |
| 餘額 | | $12.93 → ~$5-8 buffer | $12.93 → **$7.56** ✅ |

---

## 新 FF 候選（看 Christ 拍板）

對齊業界 reference + Trial_v26 揭真實風險：

1. **🟡 FF — Vera review 紀律升級**（system prompt / few-shot 更多「該標 critical 的反例」/ 對 production safety 紅線違反更嚴）
2. **🟡 FF — Read-only Codebase Explorer agent**（獨立 worker / 給 Petra 上報 task 時補一份「code 真實狀態摘要」/ Vera review 寬鬆時 Petra 有第二來源訊號 / 對齊業界「fast read-only agent」pattern）

**撤回**：前面 Aria 提的「Petra 切 Claude Code CLI」FF 候選（WebSearch 確認業界不推薦）。

---

## Top 5 重排

1. **#1 Trial_v27** — 補驗 Stage 81 動態 replan UI 卡 production 真實 fire（Vera-critical prompt 強化 / 或 SQL fallback 反向驗 routing wire / 預估 $2-4 / 餘額 $7.56 buffer 充足）
2. **#2 WebUI Stage** — Phase 4 最後一個 / v4 entity drop + Dashboard 重設計為 PetraSession-based / 規模 L+ / Trial_v27 後評估
3. **#3 新 FF 候選**（Vera review 紀律升級 / Codebase Explorer agent / Christ 拍板要不要立）
4. **#4 FF 五十四 怪物大檔追蹤**
5. **#5 保留群組**

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

- ✅ PR #391 + #392 close（對齊 Trial 不污染 main 紀律）
- ✅ SQL flag 切回 default（UseDynamicReplanning + UseHITLPlanConfirmation → false / reload-cache scope=all）
- ✅ Trial_v26 結案紀錄建立 v1.0 in-place
- 更新 Future_Feature_v5.5.md — Phase 4 路徑修正 + Trial_v26 候選段
- commit + push

### 下個重點戰略

- Trial_v27 補驗 Stage 81 動態 replan UI 卡 production 真實 fire
- 評估新 FF 候選（Vera review 紀律 / Codebase Explorer agent）
- WebUI Stage 規劃啟動條件達成評估

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-21 | Trial_v26 結案紀錄建立 in-place（對齊「對冗餘不容忍」紀律 — Trial_v26 跳過 v1.0 規劃步驟直接 9-step 開跑 / 結案紀錄為唯一文件）。**核心結果**：Case A baseline 全綠 ⭐⭐⭐⭐⭐（Stage 82 雙修法 Quinn outputLen=1352 vs Trial_v25=0 + PM PetraSessionId 100% 透傳 vs Trial_v25 NULL / cost $3.46 / PR #391 close）+ Case C 動態 replan 🟡 部分（Stage 80 HITL plan_confirm fire ✅ + Stage 81 DetectReplanTrigger 紀律正確不誤觸發 ✅ + replan UI 卡 production 真實 fire 留 Trial_v27 / Vera 真實 review OK W1 預警接受 / cost $1.91 / PR #392 close）+ Case D skip。**戰略 finding ⭐** — Christ 戰略 question 點破「Aria 推論 vs 業界 reference」+ WebSearch 3 query 揭 AiTeam Petra LLM + worker CLI + HITL 三層分工**對齊業界主流 supervisor pattern**（LangGraph / Databricks / Claude Agent SDK 共識）/ 撤回前面「Petra 切 CLI」FF 候選（業界不推薦）。**真實 cost** $5.37 / 餘額 $12.93 → $7.56 buffer 充足。**Aria 業務評分** ⭐⭐⭐⭐ 4/5。 |

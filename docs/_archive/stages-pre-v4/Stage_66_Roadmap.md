# Stage 66 Roadmap — v5 動態架構 Trial_v11 揭 3 🟡 議題收口（Vera 0 work 修根因 + PetraOrchestratorService 自管 chain）

> 目標版本：**v3.56.0**（minor — Trial_v11 3 🟡 議題收口 / production-ready 補強第三波 / v5 上線前最後一個工程 Stage）
> 狀態：規劃中
> 範圍：PetraOrchestratorService 自管 chain（取代 BuildSequential framework chain — 修 Vera 0 work 根因）+ PetraSessionMessages tool role 寫入 + Cody 廣範圍指令範圍對照表 enforce（Petra dispatch user prompt 動態 enforce / 不寫死 CLAUDE_Cody.md）+ Stage 64 spike notes errata
> 規模：M

---

## 戰略脈絡

**Trial_v11 結案後第二波 production-ready 補強 — 修 framework limitation 根因（不依賴 framework 隱式 chain）**：

- Trial_v11 揭 3 🟡 工程議題（0 🔴 戰略級新類型維持）— v5 動態架構設計層仍對，但「多 worker chain」實質單 worker 是 production 切 default flag 前必修
- Stage 65 計劃前 WebSearch 紀律 + Gate1 Tier 升級紀律連續第二次實踐成功（Stage 64+65 連續兩次 ×0.79 校準錨穩定）→ Stage 66 沿用同套紀律
- **Stage 66 子項 1 計劃前 WebSearch 揭真實 root cause + 推翻 Stage 64 結論**（連續第三次 framework 結論誤判同類根因第六次累積修根因）：
  - Trial_v11 觀察：Vera dispatch + CLAUDE template 載入正常但 subprocess 0 token 0 cost 0 deliverable
  - WebSearch 揭：[microsoft/agent-framework #1308](https://github.com/microsoft/agent-framework/issues/1308)（open）+ [#2696](https://github.com/microsoft/agent-framework/issues/2696)（closed 2026-02 含 fix pattern）+ [#1975](https://github.com/microsoft/agent-framework/issues/1975)（open）— **`AgentWorkflowBuilder.BuildSequential` + `ChatClientAgent` direct chain 在 nuget 1.0.0-rc3 不會自動把 input 餵下個 agent**，第二個 agent 收 blank message 立刻 exit success
  - 推翻 Stage 64 計劃前 WebSearch 結論「framework 自動 chain」一部分 — 當時看 official sample 但沒看穿真實 wire pattern（sample 用 Executor + adapter 手動轉接，不是 ChatClientAgent direct chain）
  - **連續第三次 framework 結論誤判**（Stage 63A (a) 誤判 / Stage 63A (b) 誤判 / Stage 64「自動 chain」誤判）→ workflow_aria.md 第三節 A 第 9 條 WebSearch 紀律延伸觸發點（結案第二段 step 0 升級評估 — 含「sample 完整 wire pattern 含 adapter executor 都要看穿不只 build API 名稱」）
- **修法路線 B 拍板（自管 chain）vs 路線 A（插 adapter Executor）**：
  - 路線 B 對齊 v5 動態架構「Petra 自主 orchestrate」精神（Petra 既然已是動態決策 orchestrator，chain 也該由它自管）
  - 路線 B 不依賴 framework open issue 修法時間表（#1308 / #1975 都還 open）
  - 路線 B 工作量中等（PetraOrchestratorService 已有動態決策 + 動態 dispatch 基礎，extend「跑完一個拿 result 拼下一個」邏輯不大）
  - 路線 B 未來 4 cap Kickoff trigger（多 worker chain 場景）也適用 — 不限定 sequential 兩個 agent

**範圍邊界刻意收緊**（對齊 infinite loop pattern + 「production-ready 漸進 path 優先」紀律）：
- 只動 v5 PetraOrchestratorService chain wire + adapter user prompt template + CLAUDE_Cody.md 紀律段
- **不動 framework BuildSequential 邊角**（保 ResumeAsync / xUnit Test 7 BuildSequential 三層 wrapper 既有 path 對齊）— 自管 chain 取代主路徑，BuildSequential 留 reference / 未來 framework 修 #1308 後評估回切
- **不含 prompt DB 化**（Christ 2026-05-14 拍板 — v5 上線後再評估）/ 不含 v4 既有 7 worker AgentService 路徑改動
- 不含 8 CLAUDE_*.md partial → full 重寫（FF 六十一其餘 8+ 點 Stage 67+ 評估動工）

**對 Trial_v12 預期效果**：
- 子項 1 修 → Vera 真實 cover review work（token > 0 + tool role message 寫入 + PR body Worker summary 段含 Vera review 結果）
- 子項 2 修 → PetraSessionMessages tool role 真實寫入 + PR body「Worker 完成 summary」段顯示完整 chain
- 子項 3 修 → Cody PR body / commit message 含「範圍對照表」段 + 廣範圍指令 5/5 cover（含 InteractionCenter）

---

## 子項清單

### 1. PetraOrchestratorService 自管 chain — 取代 BuildSequential framework chain（議題 1 — Vera 0 work 修根因）

**修法位置**：[src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `StartAsync` 內 BuildSequential + InProcessExecution + TurnToken 三層段（既有 Stage 63B PoC 註明的 spike 已驗 path）

**目標**：picks 序列改由 Petra 自管 dispatch loop — 跑完 worker A → 拼 result 進 user prompt → 跑 worker B → ... → 全跑完後 FinalizeGitAsync

**Forge 實作前 grep 對齊**：
- `IAgentTool.CreateAgent(PetraSessionContext)` 真實簽名 + 回傳型（[src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs](src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs)）
- `AIAgent.RunAsync` / `RunStreamingAsync` 官方 API（Microsoft.Agents.AI 1.0.0-rc1）— 確認 ChatClientAgent direct call 是否需要 TurnToken（直接 RunAsync 應該不需要 — TurnToken 是 BuildSequential workflow trigger）
- `ClaudeCodeChatClientAdapter.GetResponseAsync` 既有 sync return 路徑（IChatClient 同步返回完整 ChatResponse，不需 streaming yield）

**自管 chain 設計重點**：
- **first worker input** = 原 task input（對齊既有 `new ChatMessage(ChatRole.User, taskInput)`）
- **後續 worker input** = `BuildNextWorkerInputAsync(taskInput, prevPicks, prevResults)` — 拼出 user prompt 含原 task input + 前面 worker 已做的 capability + 結果摘要（讓 Vera 知道「Cody 改了什麼」可以真 review）
- **dispatch 失敗處理**：單一 worker 失敗 → escalate session + 不繼續後續 worker（對齊既有 catch path）
- **per-worker tool role message 寫入**（合併子項 2 — 修法位置高度重合）：每個 worker dispatch 完成後 `sessionRepo.AppendMessage(sessionId, "tool", BuildToolMessage(workerName, capability, result))` + `db.SaveChangesAsync(ct)`

**WebSearch reference**：
- [microsoft/agent-framework #1308](https://github.com/microsoft/agent-framework/issues/1308)（open）— ChatClientAgent direct chain 第二個 agent 收 blank message 確認
- [microsoft/agent-framework #2696](https://github.com/microsoft/agent-framework/issues/2696)（closed）— official sample fix pattern reference（路線 A 參考但本 Stage 採路線 B）
- [microsoft/agent-framework #1975](https://github.com/microsoft/agent-framework/issues/1975)（open）— Workflow as Agent AgentThread 不更新（同根因）

**xUnit test 補強**（既有 [src/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs](src/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs) 7 case）：
- Test 7 BuildSequential + ChatClientAgent + Adapter 三層 wrapper 改寫對齊自管 chain（chain wire 變 PetraOrchestratorService 內部 loop，adapter 仍經 ChatClientAgent wrap 用 RunAsync direct call）
- 新增 chain 多 worker pass-through test：mock 兩個 worker，驗第二個 worker 真收到非空 input + result 含前面 worker 的脈絡

### 2. PetraSessionMessages tool role 寫入（議題 2）

**Trial_v11 揭事實**：`SELECT Role FROM petra_session_messages WHERE SessionId='97f5710d...'` 只 user (400 char) + assistant (31 char) = **0 tool role**。連帶 PR body「Worker 完成 summary」段顯示「（無 tool role 紀錄）」。

**修法位置**：合併進子項 1 自管 chain loop（每個 worker dispatch 完成後 append tool role message 進 PetraSessionMessages）

**tool role message 內容**（[src/AiTeam.Bot/Orchestration/Petra/FinalizeGitAsync] 對應 PR body 拼接邏輯需同步看 — Forge 確認）：
- workerName + capability + dispatch 結果摘要（output 截前 N 字 / 完整 outputLen / token usage）
- ToolCallId 用 worker dispatch GUID（既有 PetraSessionMessage entity 已有 ToolCallId column）

**驗證**：Trial_v12 跑後 SQL `SELECT Role, COUNT(*) FROM petra_session_messages WHERE SessionId=<新 id> GROUP BY Role` 應有 user / assistant / **tool**（≥ picks.Count 條）

### 3. Cody 廣範圍指令範圍對照表 enforce（議題 3 — Petra dispatch user prompt 動態 enforce / 不寫死 CLAUDE_Cody.md）

**Trial_v11 揭事實**：Cody cover 範圍 4/5（連兩 Trial 漏 InteractionCenter）+ PR body 0 含「範圍對照表」段。Stage 65 子項 4 加進 CLAUDE_Cody.md 的「廣範圍指令處理紀律」紀律強度不夠（intent 有但 enforce 不足）。

**Christ 拍板設計原則**（2026-05-14）：
- ❌ **不在 CLAUDE_Cody.md 加專案特定 mapping**（如「操作中心 = `Components/Pages/Interactions/InteractionCenter.razor*`」）— CLAUDE_Cody.md 是 Cody **跨專案的工作守則**，未來 AiTeam 客戶專案 Cody 會繼續用同份檔案，寫死專案特定 mapping 會污染未來客戶專案的判斷
- ✅ **改動態 user prompt 層 enforce**（Petra 給 Cody 的 dispatch prompt — 每次 dispatch 拼接時動態產生，知道當下任務脈絡）

**修法位置**（Forge 自決 A vs B）：
- **路線 A（推薦）**：[src/AiTeam.Bot/Orchestration/Petra/PetraWorkerHelper.cs](src/AiTeam.Bot/Orchestration/Petra/PetraWorkerHelper.cs) `BuildAgent` 內 user prompt template 加固定段「廣範圍指令處理紀律 enforce」 — 對 capability=code_implementation 加段
- **路線 B**：[src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs) `GetResponseAsync` 內 prompt flatten 後 prepend enforce 段 — 對所有 worker 統一加（過廣 — 不推薦）

**enforce 段內容**（generic / 不含專案特定 mapping）：
- 任務原文若含廣範圍措辭（「整個 X」/「所有 Y」/「凡是 Z」/「之類」/「等等」）→ Cody 必須做：
  - 步驟 1：用 `git ls-files` / Glob 在 workspace 內 grep 範圍對應檔案 / 頁面 / 模組
  - 步驟 2：產出範圍對照表（任務點名項 → 對應實際檔案 → 已 cover ✓ / 待 cover ⏳ / 不適用 ❌）
  - 步驟 3：commit message **必含**「範圍對照表」段 + PR body **必含**「範圍對照表」段
  - 步驟 4：若範圍對照表有 ⏳ 項，commit / PR 前必須 cover 完或在 PR body 註明 deferred 理由

**保留**：CLAUDE_Cody.md Stage 65 子項 4 加的「廣範圍指令處理紀律段」維持（generic 守則層仍有價值），Stage 66 只在 Petra dispatch user prompt 層加 enforce 步驟。

**驗證**：Trial_v12 PR body / commit message 含「範圍對照表」段 + 5/5 範圍 cover（含 InteractionCenter — 因為 Cody 真實 grep `Components/Pages/Interactions/` 對應「操作中心」）

### 4. Stage 64 spike notes errata（配套，非主修法）

**位置**：[docs/architecture/v5_charter/05_Stage_63A_Spike_Notes.md](docs/architecture/v5_charter/05_Stage_63A_Spike_Notes.md) + [docs/architecture/v5_charter/02_Architecture_Wire.md](docs/architecture/v5_charter/02_Architecture_Wire.md) + [docs/architecture/v5_charter/04_Stage_63_PoC_Roadmap_Draft.md](docs/architecture/v5_charter/04_Stage_63_PoC_Roadmap_Draft.md)

**內容**：補一段 errata「Stage 64 計劃前 WebSearch『framework 自動 chain』結論 Trial_v11 揭部分誤判 — `AgentWorkflowBuilder.BuildSequential` + `ChatClientAgent` direct chain 不會自動把第一個 agent output 餵下一個 agent，需要 adapter Executor + manual TurnToken（路線 A）或自管 chain（Stage 66 採路線 B）。對應 GitHub issue #1308 / #2696 / #1975。」

**為什麼**：避免未來 Stage 67+ 規劃 reference Stage 63A spike notes 結論時再次誤判（對齊 Stage 63A 限制 (b) errata 既有模式 — 把連續實證更新進 spike notes 防漂移）

---

## 設計決策

1. **路線 B（自管 chain）vs 路線 A（插 adapter Executor）**：採路線 B。對齊 v5 動態架構「Petra 自主 orchestrate」精神 + 不依賴 framework open issue 修法時間表 + 工作量中等 + 未來多 worker chain 場景通用
2. **子項 1 + 子項 2 修法位置合併**：PetraOrchestratorService 自管 chain loop 內順手做 PetraSessionMessages tool role append（同位置 + 同 transaction，避免兩段獨立寫入時序漏洞）
3. **Cody 廣範圍紀律不動 CLAUDE_Cody.md 寫死 mapping**：CLAUDE_Cody.md 是 Cody 跨專案工作守則，動態 enforce 改 Petra dispatch user prompt 層（見子項 3 Christ 拍板原則）
4. **prompt DB 化候選 standby**（Christ 2026-05-14 拍板）：v5 上線後評估 — Stage 66 子項 3 動 PetraWorkerHelper user prompt template 抽 method 時，**為未來 prompt DB 化留 inject 點**（不增 Stage 66 工作量但給未來方便 — Forge 自決抽 method 時保此可換性）
5. **BuildSequential framework chain 路徑保留**（不刪 既有 PetraOrchestratorServiceTests Test 7 三層 wrapper test）：自管 chain 取代主路徑生效，BuildSequential 留 reference / 未來 framework 修 #1308 後評估回切
6. **Stage 65 子項 1 `--append-system-prompt` + Stage 65 子項 2 token_logs finally 紀律 全保留**（Trial_v11 證實兩條真實生效 — 不動 Stage 65 既有修法）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：Vera 真實 cover review work（議題 1 修根因驗 — 自管 chain 生效）

**觸發**（Forge 自驗 — Mock 模式）：
- `dotnet test --filter "FullyQualifiedName~PetraOrchestratorServiceTests"` 跑既有 7 case + 新增 chain pass-through case
- 新 case：mock 兩個 worker（worker A 回固定 output / worker B 驗 input 含 worker A output 摘要），跑 `StartAsync` 後驗 worker B 真實接到 worker A 的 result 作為 input 一部分

**驗證**：
- `dotnet test` PASS — 新 case 驗 worker B input 包含 worker A output 摘要 + worker B 回非空 response
- 既有 7 case 全 PASS（保 BuildSequential 三層 wrapper 既有測 path）

### 場景 B：PetraSessionMessages tool role 寫入（議題 2 驗）

**觸發**（Forge 自驗 — Mock 模式）：
- xUnit test mock `PetraSessionRepository.AppendMessage` 觀察 dispatch loop 內 tool role 寫入 call
- 或跑 chain pass-through case 後 SQLite memory DB 查 `SELECT Role, COUNT(*) FROM petra_session_messages GROUP BY Role`

**驗證**：
- 每個 worker dispatch 後 `AppendMessage(sessionId, "tool", ...)` 被呼叫 ≥ picks.Count 次
- `petra_session_messages` 真實寫入 ≥ picks.Count 條 tool role 紀錄
- ToolCallId 含 worker GUID（非空 / 非預設）

### 場景 C：Cody 廣範圍指令範圍對照表 enforce（議題 3 驗）

**觸發**（Trial_v12 真實任務驗 — Stage 66 結案後）：
- Trial_v12 沿用 Trial_v6-v11 同 prompt（Dashboard 錯誤處理打磨）— 任務含廣範圍措辭「整個 Dashboard 凡是有錯誤處理的地方（系統設定、操作中心、Agent 設定、規則管理之類）」

**驗證**：
- `gh pr diff <num>` Cody PR body 含「範圍對照表」段（任務 5 範圍逐項 ✓ / ⏳ / ❌）
- 範圍 cover 5/5 含 `Components/Pages/Interactions/InteractionCenter.razor*`（vs Trial_v10/v11 連兩次漏）
- commit message 含「範圍對照表」段對齊

### 場景 D：FinalizeGitAsync PR body 含完整 chain summary（子項 1+2 整合驗）

**觸發**：Trial_v12 真實任務跑完 PR 開出後

**驗證**：
- `gh pr view <num> --json body` PR body「Worker 完成 summary」段**不再**顯示「（無 tool role 紀錄）」
- 段內含每個 worker（Cody / Vera）capability + dispatch 結果摘要
- 對應 SQL `petra_session_messages` tool role 紀錄條數

### 場景 E：Stage 65 既有修法未 regression（保護驗）

**觸發**：Trial_v12 真實任務跑完後

**驗證**：
- `gh pr diff <num>` 0 含 CLAUDE.md 改動（Stage 65 子項 1 `--append-system-prompt` 仍生效）
- SQL `token_logs` Vera 一行寫入 + InputTokens > 0 / OutputTokens > 0 / TotalCostUsd > 0（vs Trial_v11 Vera 0 token — Stage 66 子項 1 自管 chain 修後 Vera 真實做事）
- workspace owner=appuser 開跑前 0 手動 chown（Stage 65 子項 3 entrypoint chown 仍生效）

### 場景 F：v4 production path 0 regression（feature flag default false 守護驗）

**觸發**：Mock 場景跑 既有 v4 framework path 全套（kickoff / design / dev_plan / implementation / appeal / qa / 5 routing HITL）

**驗證**：
- `dotnet test` 全 PASS（既有 168/168 baseline 維持）
- `Workflow:UsePetraOrchestratorV5=false` 走 v4 path 0 改動（PetraOrchestratorService 自管 chain 修法只影響 v5 path）

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律（port 5052 + X-Api-Key + container 名 `aiteam-aiteam-bot-1` + Petra session 表 snake_case PascalCase quote 等 — 對齊 Stage 65 同段紀律）
- **Stage 66 在 main branch 直接做**（vs Stage 64/65 在 feature/v5-poc — main 已含 v5 全部 commits + Directory.Build.props v3.55.0）
- feature flag `Workflow:UsePetraOrchestratorV5` default false 維持（Trial_v12 ✅ 後 Christ 拍板才切 default true = v5 正式上線）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊既有
- 對齊 workflow_aria.md 第三節 A 第 5+6 條紀律（不寫整段 code 範例 + 大檔 reference 標精準 line + 簽名）— Forge Plan Mode 階段對齊範本可省 ~30K context
- 對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍段 #3（Aria reference 既有 method 簽名 / pattern 必 grep 既有 caller positional args 順序 + grep interface 實作類完整列出 — Stage 65 揭同類根因第六次累積修根因）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-14 | 規劃書建立（Aria）— Stage 66 = Trial_v11 揭 3 🟡 議題收口（v5 動態架構 production-ready 補強第三波 / v5 上線前最後一個工程 Stage）。**戰略脈絡**：Trial_v11 揭 Vera 0 work 業務級議題（多 worker chain 實質單 worker — production 切 default flag 後 review path 0 保護），計劃前 WebSearch 揭真實 root cause = Microsoft Agent Framework `BuildSequential + ChatClientAgent direct chain` 在 nuget 1.0.0-rc3 不會自動把 input 餵下個 agent（GitHub #1308 open / #2696 closed 含 fix pattern / #1975 open）— 推翻 Stage 64 計劃前 WebSearch「framework 自動 chain」結論一部分（連續第三次 framework 結論誤判同類根因第六次累積修根因 → workflow_aria.md 第三節 A 第 9 條 WebSearch 紀律延伸觸發點）。**3 子項 + 1 配套**：① PetraOrchestratorService 自管 chain（取代 BuildSequential 路線 B 拍板 — 對齊 v5 動態架構「Petra 自主 orchestrate」精神 / 不依賴 framework open issue / 工作量中等 / 多 worker chain 場景通用）+ PetraSessionMessages tool role 寫入合併（修法位置高度重合）② PetraSessionMessages tool role 寫入（合併進子項 1 dispatch loop）③ Cody 廣範圍指令範圍對照表 enforce（Petra dispatch user prompt 動態 enforce 路線 A — 不寫死 CLAUDE_Cody.md 專案特定 mapping，Christ 2026-05-14 拍板「跨專案守則」原則）④ Stage 64 spike notes errata（補 framework 自動 chain 部分誤判 + GitHub issue reference 防未來 Stage 67+ 規劃再次誤判）。**6 設計決策**：路線 B 拍板 / 子項 1+2 修法位置合併 / Cody 紀律不寫 CLAUDE_Cody.md 寫死 mapping / prompt DB 化候選 standby v5 上線後評估（PetraWorkerHelper user prompt template 抽 method 留 inject 點）/ BuildSequential framework chain 路徑保留 / Stage 65 既有修法全保留。**6 驗收場景**：A Vera 真實 cover review work（自管 chain 生效）/ B PetraSessionMessages tool role 寫入 / C Cody 廣範圍指令範圍對照表 enforce（Trial_v12 真實任務驗）/ D FinalizeGitAsync PR body chain summary 整合 / E Stage 65 既有修法未 regression / F v4 production path 0 regression。**範圍邊界刻意收緊**：只動 v5 PetraOrchestratorService chain wire + adapter user prompt template + CLAUDE_Cody.md 段（不動 BuildSequential 邊角 / 不含 prompt DB 化 / 不動 v4 既有 7 worker / 不含 8 CLAUDE_*.md partial→full 重寫）。**規模 M 預估 mid 350-450K**（對齊 production-ready 補強區間 Stage 56/58/60/61/64/65 ×0.78-0.99 6 資料點 — Stage 66 性質類似但加 framework chain 重寫 spike + WebSearch 補強紀律累積成熟度 Stage 64+65 連續 ×0.79 證明 → 預期維持 ×0.78-0.99 區間下緣）。**Trial_v12 預期啟動條件**：Stage 66 Mock 全綠 → Trial_v12 沿用 Trial_v6-v11 同 prompt + Aria 全程自跑 9-step 模板第 3 次實踐 → 通過 → Christ 拍板切 default flag = v5 動態架構正式上線。 |
| v2.0 | 2026-05-14 | **Stage 66 結案 ✅ — Trial_v11 揭 3 🟡 議題完整收口 / v5 上線前最後一個工程 Stage 完成 / Trial_v12 啟動條件達成**。**Forge 一氣呵成完成全 4 子項**：commit `1807972 feat(stage66)` 8 檔（PetraOrchestratorService.cs +92/-17 / WorkerDispatchSummary.cs 新檔 +11 / ClaudeCodeChatClientAdapter.cs +25 / PetraOrchestratorServiceTests.cs +155 / 3 charter docs errata +35 / Directory.Build.props +6 v3.55.0→v3.56.0 minor bump）。**子項 1+2 合併**：PetraOrchestratorService.StartAsync 內 BuildSequential + InProcessExecution + TurnToken 三層段刪除（既有 import / LogWorkflowEvent / `_executorAccumulators` 保留 reference）→ 抽 `DispatchWorkersAsync` 自管 chain for loop（first input = 原 task input / 後續 input = User(taskInput) + Assistant(前面 worker 摘要) 拼接）+ per-worker `RunAsync(messages, session: null, options: null, ct)` direct call + tool role message 同 transaction append 進 PetraSessionMessages（議題 1+2 合併修法位置避免時序漏洞）+ 抽 helper `BuildNextWorkerInput` / `BuildToolMessage` 留 future prompt DB 化 inject 點（Christ 2026-05-14 拍板）。**子項 3**：ClaudeCodeChatClientAdapter.GetResponseAsync 內 `capability == "code_implementation"` `OrdinalIgnoreCase` 條件 prepend 廣範圍 enforce 段（4 步驟：grep 範圍 → 對照表 → commit/PR body 雙重 enforce → ⏳ 處理）+ `BuildBroadScopeEnforceSection` 抽 method 留 future prompt DB 化 inject 點 + CLAUDE_Cody.md 0 改（保 Stage 65 子項 4 generic 守則層）。**子項 4**：3 個 v5_charter docs errata（02_Architecture_Wire / 04_Stage_63_PoC_Roadmap_Draft / 05_Stage_63A_Spike_Notes 各補「Stage 66 errata」段 reference GitHub #1308 / #2696 / #1975 + 路線 B 拍板理由）。**xUnit Test 12+13 補強 5 case**（PetraOrchestratorServiceTests 23 → 28 PASS — Test 12 chain 多 worker pass-through 整合驗 / Test 13 廣範圍 enforce prepend 4 InlineData 只對 code_implementation 加）。**Aria gate1 通過 0 critical 修正**（Tier 0 + Tier 1 + Tier 2 dotnet build 0 Error / 102 Warning baseline 維持 0 新 warning — Trial 前最後一個 Stage tier 升級紀律首次實踐）。**Aria 自診 source of truth 紀律根因第 7+8 次累積**：① **第 7 次** nuget 版本字串憑印象寫（Roadmap 寫 1.0.0-rc3 / 真實 csproj `Microsoft.Agents.AI 1.3.0` stable — Forge plan 階段 grep 揭）② **第 8 次** reference 既有 method 沒 grep caller（Roadmap 子項 3 路線 A 寫「PetraWorkerHelper.BuildAgent」/ 真實 0 caller dead code — Forge plan 階段 grep 揭，落點修正為 ClaudeCodeChatClientAdapter）— 兩條 Forge healthy 偏離 plan Aria gate1 全 endorse / step 0 升級 workflow_aria.md 第三節 A 第 7 條延伸範圍段 #4「framework version 字串 + reference method dead code」紀律累積到第 7+8 次必升級。**計劃前 WebSearch 紀律連續第三次實證生效**（Stage 64 揭 (b) 誤判 + Stage 65 揭 (a) 誤判 + Stage 66 揭「framework 自動 chain」誤判）— 紀律累積成熟度進化曲線。**Forge 自驗 escalate 1 議題 Christ 拍板路線 B**（DB Workflow:UsePetraOrchestratorV5=true 殘留 Trial_v11 跑時切沒切回 vs Roadmap 場景 F 期望 default false — Christ 拍切回 false 對齊「default false 守 v4 production」紀律 + Trial_v12 開跑時 Aria 9-step 第 3 步切回 true）— **新立 aria-trial-summary skill 紀律**「Trial 結束後 Aria 切回 feature flag default 對齊下個 Stage Roadmap 預期」+ aria-gate1 skill Tier 0「順手 SQL 確認 feature flag 真實狀態 vs Roadmap 預期」候選紀律。**Aria 校準錨 ×0.58**（Forge 真實結案 232K vs 預估 mid 400K — vs Stage 64+65 連續 ×0.79 -27% 偏低 / **production-ready 補強區間 6 → 7 資料點 ×0.78-0.99 → ×0.58-0.99 新下界擴展 +27%**）。校準錨偏低四因素疊加：① 計劃前 + plan reviewer 雙重 WebSearch 揭真實 nuget 1.3.0 + framework wire 全攤 → Forge 0 spike 找 framework wire ② Forge plan 階段自診揭兩條 Aria 源頭（dead code + 版本字串）省 implementation iteration ③ 子項 1+2 合併實作（修法位置高度重合）省 context ④ 0 critical Aria gate1 → Forge 一氣呵成不返工。**Trial_v12 啟動條件達成**：Stage 66 Mock 全綠 + Aria gate1 通過 + Forge 自驗 4 場景全 PASS（場景 A/B 透過 xUnit Test 12 cover / 場景 C 透過 xUnit Test 13 cover / 場景 E+F 透過 dotnet build + DB flag 切回 false 後驗 v4 mock 對照）+ 場景 D Trial_v12 真實任務驗 — Aria 全程自跑 9-step 模板第 3 次實踐啟動條件達成。**戰略主軸**：v5 動態架構工程準備全完成（Stage 64+65+66 三波 production-ready 補強 + Aria 紀律累積成熟度進化曲線實證）→ Trial_v12 是 v5 上線前最後一道閘門 → 通過 → Christ 拍板切 default flag = v5 動態架構正式上線。 |

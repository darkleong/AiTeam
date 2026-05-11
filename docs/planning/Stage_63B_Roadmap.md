# Stage 63B Roadmap — FF 三十六 Phase B PoC spike（v5 動態架構 production-ready 實作 + Mock 全綠）

> 目標版本：**v3.53.0**（minor — PoC code + EF Migration + feature flag，不含真實任務 Trial — Trial_v9 結案後另排）
> 狀態：規劃中
> 範圍：v5 動態架構 production-ready 實作 — PetraOrchestratorService 自寫 + ClaudeCodeChatClientAdapter + 8 Worker prompt 重寫 + EF Migration + feature flag + Mock 全綠
> 規模：L

---

## 戰略脈絡

Stage 62 Charter spike ✅ + Stage 63A API spike ✅ **硬通過 + 揭 2 framework limitation 戰略級早期 derisk** → Stage 63B PoC 啟動條件達成。

**Stage 跟 Trial 分開拍板**（Christ 2026-05-11）— 對齊 Trial_v2-v8 既有獨立試驗計劃模式：
- **Stage 63B = PoC 架構基底 + Mock 全綠**（純實作 + 0 LLM cost）
- **Trial_v9 = Stage 63B 結案後另開** `docs/experiments/Trial_v9_Plan.md`（5 向對照真實任務驗證 + 7 驗證項 #4 Crash Recovery + #6 遷移成本 ROI 量化）

**Stage 63A 揭露 2 framework limitation workaround**（Charter 02/04 errata 補釘）：
- **Limitation (a)** base `GroupChatManager` subclass 不啟動 manager loop → **PetraOrchestratorService 自寫 + `DecideAsync` + `BuildSequential` path**（不走 framework GroupChat loop — Stage 63A spike 已驗）
- **Limitation (b)** base `AIAgent` subclass 不被 framework workflow dispatch → **必走 `ChatClientAgent(IChatClient, ...)` ctor + 新建 `ClaudeCodeChatClientAdapter : IChatClient`**（包既有 ClaudeCodeService — 從「可選」升「必走」）

**Charter 04 inconsistency 揭露**（Aria 規劃 grep 紀律對齊 source of truth）：
- Charter 02/04 寫「9 Worker capability mapping」實際 **7 個 AgentService**（DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService / ReleaseAgentService / RequirementsAgentService / DesignerAgentService — CeoAgentService 是 Layer 2 Victoria 不算 Worker / Petra 是 Layer 3 orchestrator 不算 Worker / Designer 與 Demi 同 service）
- Charter 02 寫「ClaudeCodeService.RunReadOnlyAsync / **RunWriteAsync**」實際 method 名是 `RunAsync`（完整開發模式）+ `RunReadOnlyAsync` + `RunVictoriaAsync` + `RunQaAsync` + `RunReviewAsync` + `RunMeetingSessionAsync` — 沒有 `RunWriteAsync`
- 本 Roadmap 對齊真實 codebase 量化（不憑印象 — 自省點 #31 第五次紀律實踐）

---

## 子項清單

### 1. PetraOrchestratorService 自寫實作（核心架構基底）

新建 `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs`：

- **動態決策 path**：Petra 一次性 LLM `DecideAsync(taskInput, agents)` → 回 agent name 序列 → `AgentWorkflowBuilder.BuildSequential(...)` → `InProcessExecution.RunStreamingAsync` 跑 workflow（對齊 Stage 63A spike 已驗 path / 不走 framework GroupChat loop）
- **Trigger 條件 prompt**（CLAUDE_Petra.md 寫死 — 對齊 FF 三十六既有 Kickoff/Design/1-on-1 三 trigger）
- **per-task session 持久化**：包 EF Core read/write `petra_sessions` + `petra_session_messages` 兩表（schema 對齊 Charter 02 候選）
- **重啟 rebuild context**：Bot 重啟 → scan `petra_sessions Where Status='running'` → rebuild 從 task input + 已 responded BossInteraction 紀錄（對齊 5 挑戰拍板 #5「重啟重跑不做 Checkpointing」）+ 不雙重 ask Christ
- **DI 註冊**：scoped registration + 透過 `IEnumerable<IAgentTool>` DI scan 取所有 Workers

### 2. Victoria Router prompt 重寫 + RouteToPetra Tool Set

- **`src/AiTeam.Bot/Resources/CLAUDE_Victoria.md` 93 行 partial 重寫**：Discord 秘書定位 + 移除「業務邏輯 / codebase scan」段（Trial_v7 揭 +224% cost 段）+ 補「純 facade Router + Tool Set 介面」段
- **`src/AiTeam.Bot/Agents/CeoAgentService.cs` 544 行 prompt 重寫** + 加 `RouteToPetra(taskDescription, taskGroupId)` Tool Set 入口（呼叫 PetraOrchestratorService.StartAsync）+ 移除既有 codebase scan 段

### 3. CLAUDE_*.md 8 個 prompt 重寫（1 全砍 + 7 partial）

對齊 Charter 03 audit 範圍：

- **`CLAUDE_Petra.md` 221 行全砍重寫**（質變定位 — 從「品質審核閘門」變「全程動態 orchestrator」）+ **開頭加「v5 PoC 期間紀律」段**（FF 五十九 hand-off 落實 — Petra 看到 codebase 含 `Orchestration/Petra/` v5 痕跡 + `Workflows/**` v4 漸進遷移痕跡 + Stage 60+61 prompt 字樣**不要 escalate 困惑「為什麼有兩套架構」**— spike + 漸進遷移期間是預期狀態繼續跑當前任務）
- **`CLAUDE_Cody/Vera/Sage/Quinn/Rosa/Demi.md` 7 個 partial 重寫**：去除「審核 / 申訴 / 固定 pipeline」字樣 + 補「Petra 動態調度 / Tool Set 介面 / v5 PoC 期間紀律」段

### 4. ClaudeCodeChatClientAdapter + 7 Worker IAgentTool wire（Stage 63A errata 必走）

- **新建 `src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs`**：實作 `Microsoft.Extensions.AI.IChatClient` interface — 包既有 `IClaudeCodeService`（透過 DI 注入）+ 把 `IList<ChatMessage>` 拼成 single prompt string → call `RunAsync` / `RunReadOnlyAsync` / `RunReviewAsync` / `RunQaAsync` / `RunMeetingSessionAsync`（依 capability dispatch） → 把 `ClaudeCodeResult.Output` 包成 `ChatResponse`
- **新建 `src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs` interface**：Capabilities + ExecuteAsync
- **新建 `[AgentCapability("...")]` attribute**（class-level）
- **7 Worker AgentService 加 `IAgentTool` 介面實作 + capability attribute**：
  - DevAgentService → `code_implementation`
  - ReviewerAgentService → `code_review`
  - QaAgentService → `qa_testing`
  - DocAgentService → `documentation`
  - RequirementsAgentService → `requirements_extraction`
  - DesignerAgentService → `ui_design`
  - ReleaseAgentService → `release_publishing`
- **每個 Worker 內部走 `ChatClientAgent(claudeCodeAdapter, ...)` ctor**（對齊 Stage 63A framework limitation (b) 推導 — base AIAgent subclass 不被 framework dispatch）
- **Worker-as-Tool 真實 API**：`AIAgentExtensions.AsAIFunction(this AIAgent, AIFunctionFactoryOptions, AgentSession)` 把任一 AIAgent 包成 `Microsoft.Extensions.AI.AIFunction`（Petra orchestrator 動態 dispatch 用）
- **DI multi-registration**：每個 Worker 兩重註冊（既有 service interface + `IAgentTool` — Petra 透過 `IEnumerable<IAgentTool>` DI scan 取所有 Workers）

### 5. EF Migration `Stage63PetraSessionTables`

- 新建 entity：`PetraSession` + `PetraSessionMessage`（schema 對齊 Charter 02 候選）：
  - `petra_sessions`：Id PK / TaskGroupId FK / Status enum (running/escalated/done) / CreatedAt / UpdatedAt
  - `petra_session_messages`：Id PK / SessionId FK / Role enum (system/user/tool/assistant) / Content text / ToolCallId nullable / CreatedAt index
- Index：`petra_sessions(TaskGroupId)` + `petra_session_messages(SessionId, CreatedAt)` composite
- Repository pattern + DbSet 註冊（對齊既有 EF Core entity pattern src/AiTeam.Data/Entities/）
- Migration 命令：`dotnet ef migrations add Stage63PetraSessionTables --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`

### 6. Feature flag `Workflow:UsePetraOrchestratorV5`

對齊 Stage 49+ 漸進遷移既有 feature flag pattern（`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` 既有 UseFrameworkAppealLoop / UseFrameworkKickoff / UseFrameworkKickoffMidInterrupt / UseFrameworkDesign / UseFrameworkPipeline 5 flag）：

- **新加 `Workflow:UsePetraOrchestratorV5`** 進 WorkflowSettings.cs + WorkflowSettingsResolver.cs（DB 優先 / appsettings.json fallback）
- **default = false**（Stage 63B 階段 production 仍 main v4 服務 / Trial_v9 開跑時切 true 跑 v5）
- **切換點**：CeoAgentService 收 Christ 訊息時 check flag → true → 走 RouteToPetra 進 PetraOrchestratorService / false → 既有 v4 path 不動
- **dev 切換**：Dashboard 系統設定頁手動切（既有 AppSettings 既有 5 分鐘內生效機制）

### 7. Mock 模式 wire（Petra=Gemini Flash + Workers=hardcoded）

- **Petra Provider 切 Gemini**：Dashboard Agent 設定頁切 Petra Provider→Gemini（Stage 38 既有功能延續 / 0 改動）
- **Workers Mock**：對齊既有 `MockClaudeCodeService.cs` 545 行 fixture pattern（v5 不改既有 — 透過 IClaudeCodeService proxy 切 MockMode）
- **production Petra provider 留 Forge spike 時自決**：Mock=Gemini Flash 拍板 / production Trial_v9 階段 Petra 走 Gemini Flash 免費 tier vs Anthropic preview vs Anthropic stable — Forge spike 寫 PetraOrchestratorService 時看 IChatClient adapter 寫起來 ROI 拍板

### 8. Mock 場景 wire — 7 驗證項 ≥5 PASS

對齊 Charter 01 Spike Plan 7 驗證項（Trial 階段才驗 #4 + #6，本 Stage Mock 階段驗 ≥5 項）：

| 驗證項 | Stage 63B Mock 階段驗法 | 留 Trial_v9 真實任務驗 |
|---|---|---|
| #1 Victoria Router | Mock 場景跑 Victoria 收 Christ 訊息 → 觀察是否 RouteToPetra（不直接 call Workers）+ cost 對照 | Trial_v9 真實 cost ≤ baseline ×0.3-0.5 |
| #2 Petra 自主調度 | Mock 場景對齊 Stage 63A spike 3 場景（小/中/大 — 1-on-1 / Design / Kickoff trigger）3 場景 trigger 命中率 100% | Trial_v9 真實任務動態決策軌跡 |
| #3 per-task session | Mock 場景跑完整 5 stage（Kickoff → Dev → Vera → QA → Sage）→ 觀察 Petra context 跨階段保留 task 原始 input | Trial_v9 真實任務跨階段記憶率 |
| #4 Crash Recovery | **留 Trial_v9** | Trial_v9 真實 BossInteraction responded + Bot 重啟測試（Mock 階段物理限制 — 對齊 Stage 60 「Mock 全綠 ≠ production 全綠」自省點 #25 紀律） |
| #5 Mock Gemini Flash | Petra Provider→Gemini 跑 Mock 場景 + cost ≤ $0.05 / 任務 | Trial_v9 真實 cost 紀錄 |
| #6 遷移成本量化 | **留 Trial_v9** | Trial_v9 結案紀錄 v4 dead code LoC + 對齊 Charter 03 audit 預估 |
| #7 Hybrid 會議 trigger | Mock 3 場景 trigger 各 fire 1 次（對齊 #2） | Trial_v9 真實任務 trigger 命中率 |

### 9. Version bump v3.53.0

`src/Directory.Build.props` `<Version>` 標籤（PoC 架構基底 + Mock 全綠對齊 Stage 完成精神 — minor bump）。

---

## 設計決策

### 1. branch 策略：feature/v5-poc

→ **拍板 Christ 既有對齊（Charter 04 第 21-27 行）** — main 保留 v4 + feature/v5-poc branch 開發 + 失敗回滾 branch 不 merge + 成功 PoC 通過後 Stage 64+ 全量遷移再 merge。

### 2. feature flag `Workflow:UsePetraOrchestratorV5` default=false

→ **拍板對齊 Stage 49+ 漸進遷移既有 feature flag pattern**（WorkflowSettings.cs 既有 5 flag 延伸）— Christ 日常 main v4 服務 0 中斷 + Trial_v9 開跑切 flag=true 跑 v5 / 失敗即時切回 false / Stage 64+ 全量遷移時 default 切 true 對齊「換引擎不換車身」精神。**不走純 branch 隔離 / 不走獨立 docker stack**。

### 3. PetraOrchestratorService 自寫 path（候選 (B)）

→ **拍板 Stage 63A framework limitation (a) 推導** — base GroupChatManager subclass 不啟動 manager loop → 自寫 orchestrator + DecideAsync + BuildSequential（spike 已驗 path / 不走 framework GroupChat loop）。**HandoffWorkflowBuilder 留 fallback**（若 BuildSequential path 有實作議題 Forge spike 時切）。

### 4. ClaudeCodeChatClientAdapter 必走

→ **拍板 Stage 63A framework limitation (b) 推導** — base AIAgent subclass 不被 framework workflow dispatch → 必走 ChatClientAgent + IChatClient adapter（包既有 ClaudeCodeService）。**從 Charter 04 「可選」升「必走」**。

### 5. EF Migration 寫真實兩張表

→ **拍板 Stage 63B production-ready 寫 Migration `Stage63PetraSessionTables`**（Stage 63A spike 階段拍板 (c) in-memory only spike 範圍，Stage 63B production-ready 真寫 — 對齊 Charter 02 schema 候選 + 5 挑戰拍板 #5 Crash Recovery）。

### 6. production Petra provider 留 Forge spike 時自決

→ **拍板 Forge spike 自決**（對齊 Stage 62/63A Forge spike 5+6 自決點精神）— Mock 拍板 Gemini Flash / production Trial_v9 階段 Petra 走 Gemini Flash 免費 tier vs Anthropic preview（`Microsoft.Agents.AI.Anthropic` 1.3.0-preview）vs Anthropic stable（既有 `Anthropic.SDK` 5.10.0 走 IChatClient adapter）— Forge 寫 PetraOrchestratorService 時看 IChatClient adapter 寫起來 ROI 拍板。**escalate trigger**：Forge spike 認為三 provider 都不可行 → escalate Christ + Aria 評估。

### 7. Mock 場景 ≥5 驗證項對齊

→ **拍板 Mock 階段驗 ≥5 項**（#1 Victoria Router / #2 Petra 自主調度 / #3 per-task session / #5 Mock Gemini Flash / #7 Hybrid 會議 trigger）+ **留 #4 Crash Recovery + #6 遷移成本給 Trial_v9 真實任務驗**（Mock 物理限制 — 對齊 Stage 60 揭露「Mock 全綠 ≠ production 全綠」自省點 #25 紀律）。

### 8. Charter 04「9 Worker」更正為 7 Worker AgentService

→ **拍板 Roadmap 對齊真實 7 AgentService**（DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService / ReleaseAgentService / RequirementsAgentService / DesignerAgentService — Charter 02 Lore vs Service mapping 既有 7 個 service / Charter 04 寫「9」對齊 lore 9 角色含 Petra + Designer-Demi 重複）。**Charter 02/04 不重寫**，本 Roadmap 紀錄即可 errata 對應 lore 9 角色 vs 真實 7 service 的差異。

### 9. 拆 Session 評估（不強制拆）

→ **拍板先一個 Forge session 跑試試**（對齊 Stage 55B Compact 戰術 know-how — Forge 邊跑邊判斷做不完用 Compact 模式拆，不一開始就強制拆）。Aria 預估規模 L 預期 Forge context ~600-900K 可能踩 1M 邊際 — 建議 Opus 1M + high。**escalate trigger**：Forge spike 階段判斷做不完一個 session → 主動 Christ + Aria 拍板拆 Session A（架構基底 + Mock 全綠）/ Session B（prompt 重寫 + Migration）。

### 10. Stage 跟 Trial 分開（Christ 2026-05-11 拍板）

→ **Stage 63B 結案標準 = PoC 架構基底 + Mock 全綠**（純實作 + 0 LLM cost）— **Trial_v9 結案後另開**（Stage 63B v2.0 結案後寫 docs/experiments/Trial_v9_Plan.md / 對齊 Trial_v2-v8 既有獨立試驗計劃模式）。

### 11. v3.53.0 minor bump

→ **拍板 minor bump**（PoC 架構基底 + Mock 全綠對齊 Stage 完成精神）。

---

## 驗收情境

### 場景 A：Petra 動態決策 Mock 3 trigger 命中率 100%

**觸發**：Dashboard Agent 設定頁切 Petra Provider→Gemini + 切 feature flag `UsePetraOrchestratorV5=true` + Discord/Dashboard 送出 3 種規模任務（對齊 Stage 63A spike 3 場景）

**驗證**：
- 場景 A1：「修 typo 1 行」→ Petra log 含「跳 Kickoff + Design」+「直接 dispatch DevAgentService」軌跡（1-on-1 trigger 命中）
- 場景 A2：「Dashboard 錯誤處理跨 5 元件」→ Petra log 含「跳 Kickoff」+「開 Design」+「DevAgentService → ReviewerAgentService」軌跡（Design trigger 命中）
- 場景 A3：「Token 守門架構級重構」→ Petra log 含「Kickoff 開 + Design 開」+「動態組合多 Worker」軌跡（Kickoff trigger 命中）
- 三 trigger 各自至少 fire 1 次 / 命中率 100%

### 場景 B：7 Worker IAgentTool dispatch + ClaudeCodeChatClientAdapter 接通

**觸發**：Mock 場景跑 Petra orchestrator dispatch 7 Worker（cody / vera / quinn / sage(doc) / sage(release) / rosa / demi）— Petra 動態選 worker 走 IAgentTool.ExecuteAsync → ClaudeCodeChatClientAdapter 包 IClaudeCodeService 既有 method dispatch

**驗證**：
- 7 Worker 全部至少 dispatch 1 次（dotnet test 含 IAgentTool dispatch test 7 case）
- ClaudeCodeChatClientAdapter 把 IList<ChatMessage> 正確拼成 single prompt + Result 正確包回 ChatResponse
- Petra 透過 `IEnumerable<IAgentTool>` DI scan 取所有 7 Worker（DI multi-registration 驗證）
- capability attribute reflection 命中對應 worker name

### 場景 C：per-task session 持久化 + 重啟 rebuild context

**觸發**：Mock 場景跑完整任務 5 stage（Kickoff → Dev → Vera → QA → Sage）→ 跑到 Dev 階段時 `docker compose restart aiteam-aiteam-bot-1` Bot 重啟 → Petra rebuild context 從 `petra_sessions Where Status='running'` + 已 responded BossInteraction → 繼續跑剩餘 stage

**驗證**：
- EF Migration `Stage63PetraSessionTables` apply 後 DB 含 `petra_sessions` + `petra_session_messages` 兩表（PascalCase quote 命名對齊既有 EF Core convention）
- 重啟前 Mock 場景跑到 Dev 階段時 SQL 查 `SELECT * FROM "petra_sessions" WHERE "Status"='running'` 有對應 row + `petra_session_messages` 含 task 原始 input + Cody dev_plan 摘要
- 重啟後 Petra context 含原始 task input + 不雙重 ask Christ（已 responded BossInteraction 紀錄被讀進 task input — 對齊 5 挑戰拍板 #5）
- 重啟恢復時間 ≤ 1 次 Petra LLM call cost

### 場景 D：feature flag default=false 對齊 v4 不影響 / 切 true 跑 v5

**觸發**：
- 子場景 D1：default=false → Christ 從 Discord/Dashboard 送任務 → 走既有 v4 path（CeoAgentService 既有 scan codebase + 既有 Kickoff/Design 流程）
- 子場景 D2：Dashboard 系統設定頁切 `Workflow:UsePetraOrchestratorV5=true` → 5 分鐘內 Bot Cache reload → 下個任務走 RouteToPetra → PetraOrchestratorService

**驗證**：
- D1：v4 既有 11 Mock 場景仍綠（dotnet test 134 passed baseline 維持 / Stage 60+61 既有 Mock 場景仍綠 / Stage 49-58 既有 feature flag 行為不受影響）
- D2：flag 切換後 5 分鐘內 Bot Cache reload（既有 AppSettings 機制驗證）+ 新任務走 v5 path（Petra orchestrator log 證據）
- 切回 D1（flag=false）即時切回 v4 — 既有任務不受影響

### 場景 E：Christ 視覺驗收 Dashboard Agent 設定頁切 Petra Provider→Gemini

**觸發**：Dashboard Agent 設定頁切 Petra Provider→Gemini（既有 Stage 38 功能延續）

**驗證**：
- Petra 卡片顯示 Provider=Gemini + Model=gemini-2.5-flash（既有 LlmModels constants）
- 切換後 ReloadCache 自動觸發（既有 Stage 47 機制）
- 下個 Petra 任務走 GeminiProvider 既有 wire（ILlmProvider 走 GeminiProvider — Petra 自寫 orchestrator 直接 call GeminiProvider.CompleteAsync）

### Christ 視覺驗收（必驗）

- Stage 63B 5 場景全綠 Mock 模式跑通
- Christ 拍板 PoC 通過 → Stage 63B 結案 + Trial_v9 規劃啟動條件達成

### 0 regression 確認

- Stage 60+61 既有 11 Mock 場景仍綠（feature flag default=false 對齊 v4 不影響）
- dotnet build 0 error / dotnet test ≥ 134 passed baseline（Stage 63A 含 3 spike test silently pass 維持）
- 既有 v4 hierarchical static production 仍服務 Christ 日常（feature/v5-poc branch 未 merge 不動 main v4）

### 失敗條件（escalate Christ + Aria 評估路線）

- ClaudeCodeChatClientAdapter 接不通 ChatClientAgent path（IList<ChatMessage> 轉 subprocess prompt 卡 + Result 包不回 ChatResponse）→ 切自寫 worker dispatch path 完全不依賴 framework（escalate Christ 拍板範圍變更）
- HandoffWorkflowBuilder fallback 路徑也走不通（BuildSequential + HandoffWorkflowBuilder 雙路徑都崩潰 — Stage 63A framework limitation (a) 推導 fallback 失效）→ escalate Christ + Aria 評估路線
- 3 個以上驗證項 Mock 階段無法 deliver（spike 失敗整體判斷標準 — Charter 01_Spike_Plan.md）
- Forge spike 階段判斷做不完一個 session → 主動 escalate 拍板拆 Session A/B（Compact 模式）

---

## 不在範圍（留 Trial_v9 / Stage 64+）

明確排除避免 scope creep：

- **真實任務跑 Trial_v9 5 向對照**（Stage 63B 結案後另開 `docs/experiments/Trial_v9_Plan.md` 對齊 Trial_v2-v8 既有獨立試驗計劃模式 — cost $5-15 預估 / 建議 Christ 儲值 ≥ $30 buffer）
- **驗證項 #4 Crash Recovery 真實 BossInteraction responded 跨任務測試**（Mock 物理限制 — Trial_v9 真實任務驗）
- **驗證項 #6 遷移成本量化結案紀錄**（Stage 64+ 全量遷移時 dead code 廢棄量化）
- **production 切 default flag**（Stage 64+ feature flag 從 default=v4 切到 default=v5）
- **v4 既有 ~16K LoC 廢棄 deprecation comment**（Stage 65+ 觀察期通過後）
- **v4 dead code 移除**（Stage 66+ 對齊「修根因 > 補丁」精神）
- **HandoffWorkflowBuilder fallback path 實作**（本 Stage 走 BuildSequential 自寫 orchestrator path / Handoff 留 fallback 候選 Forge spike 失敗才實作）

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7+8 條紀律（不寫 code 範例 / 大檔 reference 標精準 line + method 簽名 / 環境細節 reference 標 source of truth / Pipeline 接管 decision 配對檢查）
- **環境細節 source of truth**：
  - Bot Internal API port `5052` 見 `docker-compose.prod.yml`
  - X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
  - MS Agent Framework nuget 版本見 `src/AiTeam.Bot/AiTeam.Bot.csproj`（Microsoft.Agents.AI 1.3.0 stable + Microsoft.Agents.AI.Workflows 1.3.0 stable + Microsoft.Agents.AI.Anthropic 1.3.0-preview.260423.1）
  - **Stage 63A 揭露 2 framework limitation workaround** 必走（Limitation (a) → 自寫 orchestrator + BuildSequential / Limitation (b) → ClaudeCodeChatClientAdapter : IChatClient + ChatClientAgent ctor）
  - ClaudeCodeService 真實 method 簽名見 `src/AiTeam.Bot/Agents/IClaudeCodeService.cs`（**沒有 RunWriteAsync** — 完整開發模式是 `RunAsync` / 含 RunReadOnlyAsync / RunVictoriaAsync / RunQaAsync / RunReviewAsync / RunMeetingSessionAsync）
  - feature flag pattern 對齊 `src/AiTeam.Bot/Configuration/WorkflowSettings.cs` 既有 5 flag（UseFrameworkAppealLoop / UseFrameworkKickoff / UseFrameworkKickoffMidInterrupt / UseFrameworkDesign / UseFrameworkPipeline）+ WorkflowSettingsResolver DB 優先 / appsettings.json fallback
  - EF Migration 命令：`dotnet ef migrations add Stage63PetraSessionTables --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`
  - PostgreSQL PascalCase quote 對齊既有 entity 既有 reference（src/AiTeam.Data/Entities/）
  - Gemini provider 對接點 `src/AiTeam.Bot/Agents/GeminiProvider.cs`（164 行 — 既有 wire 不改 / Petra Mock 用既有 ILlmProvider 路徑 / production Petra provider 選擇 Forge spike 時自決）
- **PoC branch 紀律**：feature/v5-poc 開發 / main 保留 v4 / 失敗 branch 不 merge / 成功 Stage 64+ 全量遷移時 merge
- 對齊 Stage 49+ 漸進遷移既有 feature flag pattern + AppSettings DB 優先 + appsettings.json fallback + WorkflowSettingsResolver

---

## 規模 / Model 預估

- **規模**：**L**（架構級新建 PetraOrchestratorService + ClaudeCodeChatClientAdapter + 7 Worker IAgentTool wire + 8 prompt 重寫 + EF Migration + feature flag + Mock 全綠 — 不含 Trial 真實任務）
- **Aria session model**：Opus 1M + high effort
- **Forge context 預估**：~600-900K mid **750K**（vs Charter 04 含 Trial ~800K — 去掉 Trial 真實任務跑 -100K / 規模 L 跨多檔對齊 production-ready 補強區間 ×0.85-1.10 + Stage 63A 揭 framework limitation 早期 derisk 範圍可控）
- **LLM cost 預估**：**$0**（純 Mock 階段 + 0 真實任務跑 / Trial_v9 另排階段才有 cost ~$5-15 — 餘額 $17.22 足夠跑完 Stage 63B 不影響）
- **時程預估**：1-2 個 Aria session（先試一個 / Forge spike 階段判斷做不完用 Compact 模式拆 Session A/B）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-11 | 初版計劃書建立（Aria）— Stage 63B = FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠。**戰略脈絡**：Stage 62 Charter ✅ + Stage 63A API spike ✅ 硬通過 + 揭 2 framework limitation 戰略級早期 derisk → Stage 63B PoC 啟動條件達成。**Stage 跟 Trial 分開拍板**（Christ 2026-05-11）— Stage 63B = PoC 架構基底 + Mock 全綠（0 LLM cost）/ Trial_v9 = Stage 63B 結案後另開 docs/experiments/Trial_v9_Plan.md（對齊 Trial_v2-v8 既有獨立試驗計劃模式）。**範圍 9 子項**：① PetraOrchestratorService 自寫 + 持久化（候選 (B) BuildSequential path）② Victoria + CeoAgentService prompt 重寫 + RouteToPetra Tool Set ③ 8 個 CLAUDE_*.md prompt 重寫含 FF 五十九 hand-off 落實（1 全砍 + 7 partial）④ ClaudeCodeChatClientAdapter + 7 Worker IAgentTool wire + Capability attribute + DI multi-registration（Stage 63A framework limitation (b) 推導必走）⑤ EF Migration `Stage63PetraSessionTables`（兩張表 + Repository pattern）⑥ Feature flag `Workflow:UsePetraOrchestratorV5` default=false（對齊 Stage 49+ 既有 pattern）⑦ Mock 模式 wire（Petra=Gemini Flash + Workers=hardcoded）⑧ Mock 場景 ≥5 驗證項 PASS（留 #4+#6 給 Trial_v9）⑨ v3.53.0 bump。**設計決策 11 條拍板**（含 feature flag default=false / 自寫 orchestrator / ClaudeCodeChatClientAdapter 必走 / EF Migration 真寫 / production Petra provider 留 Forge spike 自決 / 不強制拆 Session / Charter 04「9 Worker」更正 7 Worker）。**Charter 04 inconsistency 揭露**（Aria grep 對齊 source of truth 紀律）：① Charter 02/04「9 Worker」實際 7 個 AgentService ② Charter 02「RunWriteAsync」實際 `RunAsync`。**規模 L** / Opus 1M + high / Forge context 預估 ~600-900K mid 750K / LLM cost $0（Mock only）。 |


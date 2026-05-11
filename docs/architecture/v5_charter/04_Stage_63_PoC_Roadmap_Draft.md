# 04 Stage 63 PoC Roadmap 草稿

> Charter spike deliverable 4/4。Stage 63 PoC spike Roadmap 草稿（Charter 通過後升 Stage 63 v1.0 正式版）。
>
> 對齊 Stage 62 Roadmap 子項 4 範圍 + Stage 53A 拆 Stage 模式 + Stage 51 spike Charter 模板。

---

## 戰略脈絡

Stage 62 Charter spike 通過 → Stage 63 PoC spike 啟動條件達成：

- Charter Plan 4 deliverable 完整性檢查 PASS（驗收場景 A/B/C/D — Stage 62 Roadmap）
- Christ 視覺驗收 Charter Plan 通過拍板
- 自動條件：Stage 60+61 既有 11 Mock 場景仍綠 + dotnet test 131 passed

PoC spike 目標：**小範圍真實任務驗證 v5 動態架構 ROI** — 跑 Trial_v6/v7/v8 同任務（Dashboard 錯誤處理打磨）+ 5 向對照（v5 vs v6 vs v7 vs v8 vs v8-dynamic）。

---

## branch 策略

- **主 branch `main`**：保留 v4 hierarchical static 不動，Christ 日常仍走 v4 服務
- **PoC branch `feature/v5-poc`**：v5 動態架構實作 + 跑 PoC 任務 + 累積 spike 驗證紀錄
- **失敗回滾**：`feature/v5-poc` 不 merge / main v4 持續服務 / 揭露議題寫進 FF 三十六 evolution log
- **成功路徑**：PoC 通過 + Christ 拍板採用 → 規劃 Stage 64+ 全量遷移（含 Worker prompt 全重寫 + production 切換 default flag）

---

## PoC 範圍（6 子項）

### 1. Petra Orchestrator service 實作

- **新建** `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs`
- ~~MS Agent Framework Magentic Orchestration class wire~~ **errata（Stage 63A spike 揭露 — 詳 [05_Stage_63A_Spike_Notes.md 第 1+2 段](./05_Stage_63A_Spike_Notes.md)）**：
  - 「Magentic」命名空間在 nuget 1.3.0 不存在 — 真實 pattern 候選有 (A) `HandoffWorkflowBuilder` / (B) **Petra 自寫 orchestrator + `BuildSequential`**
  - **Stage 63A 揭 framework limitation (a)** — base `GroupChatManager` subclass 透過 `CreateGroupChatBuilderWith` 建構的 workflow，manager hook 全 0 invoke / 1 superstep 結束 → Stage 63B **先走候選 (B) 自寫 orchestrator path**（spike 已驗），候選 (A) Handoff 留 fallback
  - **跨 assembly override 紀律**：GroupChatManager 3 個 hook 跨 assembly subclass 須宣告 `protected override`（不是 `protected internal override` — Stage 63A 實作首發踩坑）
- per-task session 持久化包 EF Core read/write `petra_sessions` + `petra_session_messages` 兩表
- Tool Set 接 9 Workers + Capability-based 標籤（Worker-as-Tool 真實 API = `AIAgentExtensions.AsAIFunction(this AIAgent, ...)` — Stage 63A spike 補釘）

### 2. Victoria Router prompt 重寫

- `src/AiTeam.Bot/Resources/CLAUDE_Victoria.md` 93 行 partial 重寫（Discord 秘書定位 + 移除「業務邏輯 / codebase scan」+ Tool Set 介面）
- `src/AiTeam.Bot/Agents/CeoAgentService.cs` 544 行 prompt 重寫 + 加 `RouteToPetra` Tool Set
- 移除既有 codebase scan 段（Trial_v7 揭 +224% cost 段 — 對齊 [Future_Feature.md FF 三十六 Trial_v7 揭露補強](../../planning/Future_Feature.md)）

### 3. CLAUDE_*.md 8 個 prompt 重寫（1 全砍 + 7 partial）

- `CLAUDE_Petra.md` 221 行 **全砍重寫**（質變定位 — 從「品質審核閘門」變「全程動態 orchestrator」）
- `CLAUDE_Cody/Vera/Sage/Quinn/Rosa/Demi.md` 7 個 partial 重寫（去除「審核 / 申訴 / 固定 pipeline」字樣 + 補「Petra 動態調度 / Tool Set 介面」段）

### 4. Tool Set wire（Workers 接 9 工具 + Capability attribute + IAgentTool interface）

- 新建 `src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs` interface（Capabilities + ExecuteAsync）
- 新建 `[AgentCapability("...")]` attribute
- 9 Worker Service 加 `IAgentTool` 介面實作（包既有 RunReadOnlyAsync / RunWriteAsync）+ class-level capability attribute
- DI multi-registration（每個 Worker 兩重註冊：service interface + IAgentTool — Petra orchestrator 透過 `IEnumerable<IAgentTool>` DI scan）
- **Stage 63A errata：framework limitation (b)** — base `AIAgent` subclass 不被 framework workflow dispatch（`BuildSequential` ExecutorInvoked 但 `RunCoreAsync`/`RunCoreStreamingAsync` 0 invoke）→ **Stage 63B 必走 `ChatClientAgent(IChatClient, ...)` ctor + 新建 `ClaudeCodeChatClientAdapter : IChatClient`**（包既有 ClaudeCodeService.RunReadOnlyAsync/RunWriteAsync + 把 IList<ChatMessage> 轉 subprocess CLI prompt）— 不是「可選」而是「必走」

### 5. Mock 模式

- Petra=Gemini Flash（Dashboard Agent 設定頁切換 Provider→Gemini，Stage 38 既有功能延續）
- Workers=hardcoded mock（既有 `MockClaudeCodeService` 545 行 fixture 不改）
- 對齊 5 挑戰拍板 #4

### 6. per-task session 持久化（EF Migration `Stage63PetraSessionTables`）

- 寫 EF Migration：`dotnet ef migrations add Stage63PetraSessionTables --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`
- 兩新 entity：`PetraSession` + `PetraSessionMessage`（schema 對齊 Charter `02_Architecture_Wire.md` 候選）
- Repository pattern + DbSet 註冊（對齊既有 EF Core entity pattern）
- **Stage 63A errata**：spike 階段走 Christ 拍板路線 (c) in-memory `List<ChatMessage>` 0 Migration（詳 [05_Stage_63A_Spike_Notes.md 第 3 段](./05_Stage_63A_Spike_Notes.md#第-3-段--ef-migration-跨-branch-策略拍板紀錄)）— Stage 63B production-ready 才寫 Migration 維持不變

---

## 驗證任務

### 主驗證任務：Trial_v6/v7/v8 同 prompt 重跑（Dashboard 錯誤處理打磨）

- **任務 prompt**：對齊 Trial_v6 既有 prompt（Dashboard 錯誤處理打磨任務 — 跨 5 元件 + MudBlazor ISnackbar + Error toast）
- **5 向對照數據組**：
  - Trial_v5（baseline — v3 hierarchical static 全手刻 framework）
  - Trial_v6（v4 framework hierarchical static 第一試）
  - Trial_v7（v4 framework production-ready 補強第一波）
  - Trial_v8（v4 framework production-ready 補強完整 + 揭 6 🔴）
  - **Trial_v8-dynamic**（v5 動態架構 PoC — 本驗證）

### 7 驗證項對照（Charter spike Plan 預期數據逐項驗）

| 驗證項 | 預期數據 | 實測 |
|---|---|---|
| #1 Victoria Router | cost ≤ Trial_v6 v8 baseline ×0.3-0.5 | TBD |
| #2 Petra 自主調度 | LLM call ≤ Trial_v6 17 call ×0.5 + 三 trigger 各 fire 1 次 | TBD |
| #3 per-task session | context tokens -30-50% / 100% task input 命中 | TBD |
| #4 Crash Recovery | 重啟恢復時間 ≤ ~$0.05-0.1 / 0 雙重 ask | TBD |
| #5 Mock Gemini Flash | Mock cost ≤ $0.05 | TBD |
| #6 遷移成本量化 | 詳見 03_v4_Code_Audit.md（已 deliver — Charter spike 階段已驗）| ✅ |
| #7 Hybrid 會議 trigger | 三 trigger 各 fire 1 次 / 命中率 100% | TBD |

---

## 驗收標準

### 主驗收標準

- **Phase 1 完整 deliver**（推翻 Trial_v8 0 deliver 揭 6 🔴 假設）— 任務跑通 Christ 收到完整 PR + Sage 文件 + Quinn QA 結果
- **cost 對齊 Trial_v5 baseline ±50%**（v5 動態架構不爆 cost — 7 驗證項預期數據邊界內）
- **0 🔴 新類型議題揭露**（Trial_v8 揭 6 🔴 戰略級議題不再復發）
- **7 驗證項通過 ≥ 5 項**（spike Plan 預測項對齊 — 強信心 5 項全 PASS）

### 失敗條件

- 3 個以上驗證項無法 deliver（spike 失敗整體判斷標準 — Charter `01_Spike_Plan.md`）
- 驗證項 #2 Magentic Orchestration nuget 1.3.0 API 不支援動態決策（架構基底失效）
- cost 爆 +200%（v5 動態架構 ROI 為負 — 反向實證）

---

## 規模 / cost 預估

- **規模**：**L**（架構級實作 + PoC 驗證任務跑通 + 5 向對照數據蒐集）
- **Aria session model**：Opus 1M + high effort
- **cost 預估**：~600-1000K（**Aria 規劃用 token 預估** — 不是 PoC 任務 LLM cost）
  - **PoC 任務 LLM cost 預估**：~$5-15（5 向對照重跑 + Trial_v8 baseline 對齊）
- **時程預估**：1-2 個 Aria session（含 1 session Forge 實作 + 1 session Aria 規劃 / 結案 / 拍板）

---

## Stage 64+ 後續路徑（成功時）

PoC 通過 + Christ 拍板採用 → 規劃 Stage 64+ 全量遷移：

- Stage 64：Worker prompt 全重寫對齊 v5 動態架構（除 Petra 已 Stage 63 PoC 全砍 + Victoria 重寫 — Stage 64 補完其他 7 個 partial 重寫深化）
- Stage 65：production 切換 default flag（feature flag 從 v4 default 切到 v5 default）+ v4 既有 Workflows / framework Routers / Pm/* / Appeal 等 ~16K LoC 廢棄 deprecation comment（**保留 1-2 Stage 觀察期** — 真出問題可即時 fallback）
- Stage 66+：v4 dead code 移除（觀察期通過後 — 對齊「修根因 > 補丁」精神）

> ⚠️ 本路徑 Stage 64+ 為**草稿候選** — Stage 63 PoC 結案 Christ 視覺驗收後才正式拍板。

---

## 失敗時的回滾路徑

PoC 失敗 → `feature/v5-poc` branch 不 merge：

- 揭露議題寫進 FF 三十六 evolution log（PoC 失敗根因分析 + 哪些驗證項通過 / 哪些失敗）
- v4 hierarchical static main 持續服務 Christ 日常
- 評估替代路線（路線 B 大砍複雜度 / 路線 C 其他 framework / 路線 A 繼續 v4 修補但目前 ROI 為負）

---

## 對齊既有 Stage 拆 Stage 模式

對齊 Stage 53A 拆 Stage 模式：
- Stage 53A 拆出 WorkflowEngine pipeline 獨立 Stage 53B（v4 路線 6→7 Stage）
- Stage 62/63 拆出 Charter spike + PoC spike（FF 三十六 Phase B 拆 1→2 Stage — Charter 通過才 commit PoC 投資）

對齊既有 Stage 結案精神：
- Charter spike → minor bump v3.51.0（純文件 deliverable）
- Stage 63 PoC spike → minor bump v3.52.0（PoC code + Migration + 5 向對照）
- 全量遷移（Stage 64+）→ 預估 minor bump 累積（最終 v3 → v5 視為 major bump 由 Christ 拍板）

---

## 對齊 80% 既有設計拍板（不重評估）

本草稿不重新評估 80% 既有設計拍板（[Future_Feature.md:160-290](../../planning/Future_Feature.md)）：
- 4 層 Hierarchy（Christ → Victoria → Petra Orchestrator → Workers）
- 5 個關鍵挑戰拍板（Victoria 角色 / Petra 決策邊界 / 老闆介入機制 / Mock 模式 / Crash Recovery）
- Hybrid 會議模式
- Tool Set + Capability-based Multi-Agent + MS Agent Framework Magentic Orchestration
- 7 驗證項清單（FF 三十六既有 — 不新增不刪減）
- Christ 拍板路線 D（Trial_v8 結案後拍板）

---

## 環境細節 source of truth 對齊（Stage 63 PoC 落實時）

對齊 [workflow_aria.md 第 7 條延伸範圍段](../../../../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria.md)：

| 細節 | source of truth | Stage 63 PoC 處理 |
|---|---|---|
| MS Agent Framework Magentic Orchestration nuget 1.3.0 真實 class / interface 名 | nuget 1.3.0 stable + xml doc | grep 驗證後紀錄到 PetraOrchestratorService 註解 |
| EF Migration 命名 `Stage63PetraSessionTables` | 既有 Migrations 命名 pattern（src/AiTeam.Data/Migrations/）| `dotnet ef migrations add Stage63PetraSessionTables --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext` |
| PostgreSQL PascalCase quote | 既有 entity 既有 reference | `dotnet ef migrations add` 預設 + entity 配置 |
| Bot Internal API port 5052 / X-Api-Key | `docker-compose.prod.yml` + `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey` | PoC 自驗時 grep 驗證不憑印象 |
| Stage 27 DB-as-Queue | `agent_queue` table + `AgentQueueProcessor` | 對齊新 `petra_sessions` 多 row 設計 |
| BossInteraction 樂觀鎖 | Stage 28a/b 既有 `BossInteractions` table + `Status` enum | Layer 3 重啟流程 grep 驗證 already-responded 條件 |

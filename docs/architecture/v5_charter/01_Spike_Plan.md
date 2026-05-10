# 01 Spike Plan — 7 驗證項細節

> Charter spike deliverable 1/4。對齊 Stage 51 spike Charter 模板（驗證題 + 驗證方法 + 影響）+ 補「deliverable / wire 點 / Mock 場景 / 預期數據 / 失敗條件」六欄擴增。
>
> 7 驗證項對齊 [Future_Feature.md:284 FF 三十六既有清單](../../planning/Future_Feature.md)（不新增不刪減）。

---

## 對照 baseline（Trial v6 + v8 雙軸對照）

v5 PoC（Stage 63）將跑 Trial_v6/v7/v8 同任務（Dashboard 錯誤處理打磨）。預期數據對照雙 baseline：

- **Trial_v6 baseline**（2026-05-09，cost $15.81 / 11/12 deliver）— **對照組精準度**最佳（小 codebase + 同 prompt）
- **Trial_v8 baseline**（2026-05-10，cost $X.XX / 0 deliver 揭 6 🔴）— **最近真實狀態**（最新 codebase / 最貼近 v5 PoC 實際對照場景）

**Aria 補強建議採納**（任務 2 驗證項 1）— 兩個 baseline 都列：v6 為對照組精準度 / v8 為最近真實 codebase 狀態。

---

## 驗證項 1：Victoria Router 模式

| 欄位 | 內容 |
|---|---|
| **驗證題** | Victoria 純 facade + Router + Tool Set 接 Petra-only 派工是否能取代既有 codebase scan + 業務邏輯，cost 是否降到目標範圍 |
| **驗證方法** | Mock 場景跑 Victoria 收 Christ 訊息 → 觀察是否仍主動 scan codebase + 觀察是否直接呼叫 Workers vs 走 RouteToPetra 工具 |
| **deliverable** | Victoria 行為 log（無 codebase scan 段 + Tool calls 全 RouteToPetra）+ cost 對照表（v5 vs Trial_v6 v8 baseline）|
| **wire 點** | `src/AiTeam.Bot/Agents/CeoAgentService.cs` 544 行 prompt 重寫 + `src/AiTeam.Bot/Resources/CLAUDE_Victoria.md` 93 行 prompt 重寫（移除 codebase scan 段）+ 新增 1 工具 `RouteToPetra(taskDescription, taskGroupId) → PetraOrchestratorService` 入口（Charter spike 階段只寫工具 signature 候選 — Stage 63 PoC 落 attribute / interface 真實名 grep 驗證）|
| **Mock 場景** | Victoria 收 Christ「修 Dashboard 錯誤處理」→ Mock 模式預期：(a) prompt 內無「我先掃描 codebase」字樣 (b) Tool calls 列表只含 RouteToPetra (c) 不直接 call Cody/Vera/Quinn/Sage 任一 |
| **預期數據** | cost ≤ Trial_v6 baseline ($0.0567 Victoria 階段) ×0.5 / cost ≤ Trial_v8 baseline ($0.1838 Victoria 階段，cost +224% vs v6 因 codebase 變大 — [FF 三十六 Trial_v7 揭露補強](../../planning/Future_Feature.md)) ×0.3-0.5（移除 scan 段省 ~$0.1-0.3 / 任務 + 隨 codebase 變大放大 ROI）|
| **失敗條件** | Victoria 仍主動 scan codebase（prompt 改不夠乾淨 — 需重寫不是 partial 修）/ 仍直接 call Workers（Tool Set 限制不足 — 需 prompt 加強紀律）|

---

## 驗證項 2：Petra 自主調度行為（Magentic Orchestration wire）

| 欄位 | 內容 |
|---|---|
| **驗證題** | Petra 是否能用 LLM 自主決策動態跳過會議 / 動態組合 Workers，不照固定 7-stage pipeline 跑；Magentic Orchestration class wire 是否支援 |
| **驗證方法** | Mock 場景跑 Petra 收 3 種規模任務 → 觀察 Petra session log 是否有「跳過 Kickoff」/「直接派 Cody」/「動態切換 Group Chat ↔ Agent-as-Tool」決策軌跡 |
| **deliverable** | Petra 動態決策軌跡 log（含 trigger 條件實際命中 / Workers 動態組合 list / Group Chat vs Agent-as-Tool 切換點）+ Magentic Orchestration class wire 真實程式片段（Stage 63 PoC 落 grep 驗證後紀錄）|
| **wire 點** | `src/AiTeam.Bot/Resources/CLAUDE_Petra.md` 221 行**全砍重寫**（Forge spike 自決 Aria 通過 — 從「品質審核閘門」變「全程動態 orchestrator」是定位質變不能 partial 修，避免既有審核口吻偏見 — Trial_v8 揭 Petra「假裝是 Aria」根因之一是 prompt 累積層偏見）+ 新建 `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs`（class signature 候選 + 對應 MS Agent Framework Magentic Orchestration class wire — **Charter spike 階段只寫 signature 候選 + 標「Stage 63 PoC 落真實 nuget API grep 驗證」**，不憑印象寫 class 名 / interface 名 — 對齊 workflow_aria.md 第 7 條延伸範圍段紀律）|
| **Mock 場景** | (a) 小需求（< 50 行 fix） → Petra 跳過 Kickoff + Design 直接派 Cody → Vera → Sage（1-on-1 trigger 命中）(b) 中需求（跨 3-5 元件） → 開 Design 但跳 Kickoff（Design trigger 命中）(c) 大需求（架構決策） → Kickoff + Design 都開（Kickoff trigger 命中）|
| **預期數據** | LLM call ≤ Trial_v6 baseline 17 call ×0.5（會議跳過省 ~30-50% cost — 對齊 FF 三十六既有預測）+ 三 trigger 各自至少 fire 1 次 |
| **失敗條件** | Petra 仍照固定 pipeline 跑（prompt 不夠強 — 需強化）/ Magentic Orchestration nuget 1.3.0 API 不支援動態決策（Phase 3 揭露同類 prerelease 風險 — fallback 強制走 Workflow） |

---

## 驗證項 3：per-task session 跨階段記憶

| 欄位 | 內容 |
|---|---|
| **驗證題** | Petra 跨階段（Kickoff → Dev → Vera → Sage）是否能記住 task context 不依賴每次傳完整 history；session 持久化 schema 候選是否可行 |
| **驗證方法** | Mock 場景跑完整任務 → 觀察 Petra session 在 Vera 階段時是否記得 Christ 原始需求 + Cody 實作摘要（不傳完整 history）；觀察 DB `petra_sessions` + `petra_session_messages` 表填入順暢 |
| **deliverable** | Petra session 跨階段記憶 log（Vera 階段時 context window 含 task 原始 input + Cody 摘要 + 不含完整 Workers history）+ DB schema 候選 SQL（Stage 63 PoC 寫 EF Migration `Stage63PetraSessionTables`）|
| **wire 點** | DB schema 候選（Forge spike 自決 Aria 通過 — 多 row table）：<br>① `petra_sessions`：`Id` PK / `TaskGroupId` FK / `Status` enum (running/escalated/done) / `CreatedAt` / `UpdatedAt`<br>② `petra_session_messages`：`Id` PK / `SessionId` FK / `Role` enum (system/user/tool/assistant) / `Content` text / `ToolCallId` nullable / `CreatedAt` index<br>對齊既有 EF Core PostgreSQL pattern + Stage 27 DB-as-Queue 多 row pattern reference + 對齊 `chat_messages` schema（既有 reference — Stage 63 PoC 寫 Migration 時 grep 驗證 schema 命名 PostgreSQL PascalCase quote 紀律）。**Charter spike 階段只寫 schema candidate 不寫 Migration**（紀律對齊 Charter 文件 only — Stage 63 PoC 才寫 EF Migration）|
| **Mock 場景** | Petra 收任務（10 字描述 + 1 PR link）→ 跑 Kickoff 產 TaskPlan → 派 Cody 產 dev_plan + impl → 派 Vera review → 派 Sage doc — 觀察 Vera 階段 Petra context 含原始 10 字 + Cody 摘要 ≤ 200 字（不傳完整 Cody output 5K+ 字）|
| **預期數據** | context tokens 比 Trial_v6 全傳 history -30-50% / Petra session 跨階段不丟失 task 原始 input 字段（100% 命中率） |
| **失敗條件** | Petra session 持久化序列化 / 反序列化卡（schema 設計 vs Magentic Orchestration session 對接點不順 — 需切候選 a JSON column 或 c 獨立 schema fallback） |

---

## 驗證項 4：Crash Recovery 重啟重跑

| 欄位 | 內容 |
|---|---|
| **驗證題** | Bot 重啟後 Petra 任務狀態恢復（重新跑 vs Checkpointing — Christ 拍板「重啟重跑」）；已 responded BossInteraction 算 task input 紀律是否避免雙重 ask |
| **驗證方法** | Mock 場景跑 Petra 任務跑到一半（已 fire 1 BossInteraction Christ 已 respond）→ Bot 重啟 → 觀察 Petra 是否從 task input + 已 responded BossInteraction 重新跑（不從 checkpoint resume）+ 不雙重 ask Christ |
| **deliverable** | 重啟前後 log 對照（重啟後 Petra session 重建路徑 + BossInteraction 已 responded 紀錄被讀進 task input）+ v4 既有 4 CheckpointStore 廢棄路徑紀錄 |
| **wire 點** | v4 既有 4 CheckpointStore 廢棄（Charter spike 階段只標「v5 不再用」— Stage 63 PoC 才 deprecate）：<br>① `src/AiTeam.Bot/Workflows/Kickoff/KickoffCheckpointStore.cs` 31 行<br>② `src/AiTeam.Bot/Workflows/Design/DesignCheckpointStore.cs` 31 行<br>③ `src/AiTeam.Bot/Workflows/Pipeline/PipelineCheckpointStore.cs` 33 行<br>④ `src/AiTeam.Bot/Workflows/Appeal/AppealCheckpointStore.cs` 37 行<br>⑤ `src/AiTeam.Bot/Workflows/Common/FrameworkCheckpointStoreBase.cs` 215 行<br>+ BossInteraction 已 responded 算 task input 紀律寫進 PetraOrchestratorService 重啟 logic（重啟時讀 `BossInteraction.Status='responded'` rows by `TaskGroupId` 灌進 Petra session）|
| **Mock 場景** | Petra 任務跑到 Cody 階段時 fire `agent_api_failure_intervention` Christ Discord 「retry」回應 → Bot 重啟 → 觀察 Petra 重新跑時 task input 含 Christ 已 responded 「retry」決策（不再 fire 第二次 BossInteraction）|
| **預期數據** | 重啟恢復時間 ≤ 重新跑一遍 Petra 一輪 LLM call cost（~$0.05-0.1） / 重啟後 Christ 雙重 ask 次數 = 0 |
| **失敗條件** | 重啟重跑導致 Christ 雙重 ask（已 responded BossInteraction 紀律沒生效 — 需強化 task input 注入點）/ Petra session 重建跑出 inconsistent 結果（task input 不全 — 需 schema 補欄位） |

---

## 驗證項 5：Mock Mode 用 Gemini Flash 跑 Petra

| 欄位 | 內容 |
|---|---|
| **驗證題** | Mock 模式下 Petra 用 Gemini Flash 跑（觀察真實調度行為）+ Workers 用 hardcoded mock（cost 控制）；Gemini Flash 是否支援 Magentic Orchestration tool calling |
| **驗證方法** | Dashboard Agent 設定頁切換 Petra Provider→Gemini + Workers 走既有 MockClaudeCodeService → 跑 Mock 任務觀察 Petra 真實 LLM 決策軌跡 + 觀察 Workers Mock response 對齊既有 hardcoded fixture |
| **deliverable** | Mock 模式 cost 統計（Petra Gemini Flash + Workers 0 cost）+ Petra Tool Set 呼叫 log（Gemini Flash tool calling 是否正常）|
| **wire 點** | `src/AiTeam.Bot/Agents/GeminiProvider.cs` 164 行（既有 — 0 改動）+ `src/AiTeam.Bot/Agents/LlmProviderFactory.cs` 85 行（既有 — 0 改動）+ `AgentConfig` DB 表（Dashboard Agent 設定頁切換 Petra Provider→Gemini — Stage 38 既有功能延續）+ Workers 對齊既有 `src/AiTeam.Bot/Agents/MockClaudeCodeService.cs` 545 行 hardcoded mock pattern（v5 Workers Mock 不變繼承既有）|
| **Mock 場景** | Mock Mode + Petra=Gemini Flash + Workers=hardcoded → Petra 跑「修 Dashboard 錯誤處理」任務 → 觀察 Petra Tool Set 動態 dispatch 5 Workers + 全 Workers Mock fixture 命中 |
| **預期數據** | Mock 模式單任務 cost ≤ $0.05（Gemini Flash 便宜 + Workers 0 cost — 對齊 FF 三十六挑戰 4 拍板「個別 Worker hardcoded mock + Petra Gemini Flash」）|
| **失敗條件** | Gemini Flash 不支援 Magentic Orchestration tool calling pattern（API gap — 需 fallback Anthropic Claude 跑 Petra Mock，cost 上升至 ~$0.5-1）|

---

## 驗證項 6：遷移成本量化

| 欄位 | 內容 |
|---|---|
| **驗證題** | v4 hierarchical static code 多少保留 / 多少重寫 / 多少吸收（具體量化 LoC + service / DI 結構統計）|
| **驗證方法** | partial read + `wc -l` + Glob + Grep 工具組合（Forge spike 自決 Aria 通過）— 不 full read 怪物大檔 |
| **deliverable** | [03_v4_Code_Audit.md](./03_v4_Code_Audit.md) 直接 deliver — 本驗證項引用 audit 結論 |
| **wire 點** | N/A — Charter spike 階段純量化盤點 |
| **Mock 場景** | N/A — 不跑 Mock |
| **預期數據** | 詳見 [03_v4_Code_Audit.md](./03_v4_Code_Audit.md) — 吸收 ~13,148 LoC / 重寫 ~3,991 LoC + 925 prompt 行 / 全保留 ~10,000+ LoC |
| **失敗條件** | 保留比例 < 40%（重寫成本超預期 — 需 escalate Christ 重評估「換引擎不換車身」是否仍成立） |

---

## 驗證項 7：Hybrid 會議 trigger 條件

| 欄位 | 內容 |
|---|---|
| **驗證題** | Petra 真實判斷 Kickoff/Design/1-on-1 trigger（觀察 trigger 條件落地行為）；CLAUDE_Petra.md 寫死 trigger 條件初版是否生效 |
| **驗證方法** | Mock 場景跑 3 種規模任務（小 / 中 / 大）→ 觀察 Petra session log 是否三 trigger 條件各自 fire |
| **deliverable** | 三 trigger 命中軌跡 log（含 Petra reasoning「trigger X 觸發因條件 Y」+ 對應決策） |
| **wire 點** | CLAUDE_Petra.md 重寫包含 trigger 條件初版（FF 三十六既有 — Future_Feature.md 218-220 行）：<br>**Kickoff 觸發**（任一滿足）：跨 ≥ 3 元件 / 工期 ≥ 3 天 / 架構決策 / 跨多領域<br>**Design 觸發**（任一滿足）：Kickoff 已開 / Issue ≥ 5 / 跨 Phase<br>**1-on-1 觸發**：純技術改動 < 50 行 / bug 補丁 / 文件配置 |
| **Mock 場景** | 3 任務跑：(a) 小 = 「修 typo 1 行」→ 1-on-1 trigger / (b) 中 = 「Dashboard 錯誤處理打磨跨 5 元件」→ Design trigger / (c) 大 = 「Token 守門架構級重構」→ Kickoff + Design trigger |
| **預期數據** | 三 trigger 各自至少 fire 1 次 / Petra reasoning 軌跡含明確 trigger 引用（命中率 100%）|
| **失敗條件** | Petra 全跑 Kickoff（trigger 條件沒生效 — 需強化 prompt 紀律）/ Petra 全跳 Kickoff（trigger 條件太寬 — 需收緊範圍）|

---

## Spike Plan 預測項（Stage 51 既有模板）

7 驗證項預期通過：

| 信心 | 驗證項 | 理由 |
|---|---|---|
| **強信心** 5 項 | #1 Victoria Router / #4 Crash Recovery / #5 Mock Gemini Flash / #6 遷移成本量化 / #7 Hybrid 會議 trigger | 既有 v4 投資直接複用（GeminiProvider / MockClaudeCodeService / BossInteraction 已 responded 紀律 / wc -l 量化方法）+ prompt 重寫 ROI 對齊 Trial_v7 已揭露 +224% Victoria scan 數據 |
| **中信心** 1 項 | #3 per-task session schema 對接 Magentic Orchestration session | DB schema 候選成熟（多 row table）但 Magentic Orchestration session API 對接點未實證（Stage 63 PoC 落 nuget 1.3.0 API grep 驗證才知）|
| **未知** 1 項 | #2 Petra 自主調度 + Magentic Orchestration class wire | Magentic Orchestration nuget 1.3.0 是否支援動態決策 + Gemini Flash tool calling pattern — Phase 3 揭露同類 prerelease 風險（Anthropic provider 1.3.0-preview 對齊）|

---

## Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 63 PoC + 回報 Christ 評估路線：

- **3 個以上驗證項無法 deliver**（spike 失敗整體判斷標準）
- 驗證項 #2 Magentic Orchestration nuget API 不支援動態決策（架構基底失效 — 需評估其他 framework 或 fallback Workflow）
- 驗證項 #6 v4 保留比例 < 40%（重寫成本超預期 — 需重評估「換引擎不換車身」前提）

---

## 環境細節 source of truth 紀律對齊

對齊 [workflow_aria.md 第 7 條延伸範圍段](../../../../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria.md)：

- **MS Agent Framework nuget 1.3.0 API class 名 / interface 名** — Stage 63 PoC 落 grep 驗證不憑印象寫
- **EF Core PostgreSQL multi-row table pattern** — 對齊既有 `chat_messages` schema（Stage 63 PoC `dotnet ef migrations` 落實時 grep 驗證 PascalCase quote 紀律）
- **Bot Internal API port `5052` / X-Api-Key** — Charter spike 不會用但對齊紀律
- **Stage 27 DB-as-Queue pattern reference** — schema 候選對齊既有 `agent_queue` 多 row 設計

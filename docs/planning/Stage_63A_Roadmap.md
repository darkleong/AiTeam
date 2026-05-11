# Stage 63A Roadmap — FF 三十六 Phase B 動態決策 API spike（最小驗證）

> 目標版本：**v3.52.0**（minor — spike deliverable，純 throwaway prototype + 文件，無 production code 改動）
> 狀態：規劃中
> 範圍：FF 三十六 Phase B PoC 拆出的「最小 API 驗證 spike」 — 單獨驗證 Magentic Orchestration nuget 1.3.0 是否支援動態決策
> 規模：S

---

## 戰略脈絡

Stage 62 Charter spike ✅ 通過後，原 Stage 63 PoC 規模 L（600-1000K）含 Petra Orchestrator 新建 + 8 個 prompt 重寫 + 9 Workers IAgentTool 改介面 + EF Migration + Trial_v9 5 向對照。但 Charter `01_Spike_Plan.md` 預測項揭露**驗證項 #2 是唯一 unknown**（Magentic Orchestration nuget 1.3.0 是否支援動態決策 + Gemini Flash tool calling），其餘 5 項強信心 + 1 項中信心。

**核心風險**：若 #2 失敗 → fallback 走 Workflow → v4 hierarchical pattern 復辟 → 整個 PoC 主賣點不存在。8 prompt 重寫 + 9 Worker 改介面的大投資全白做。

**Stage 63A = 廉價驗證 #2 + 中信心 #3**（per-task session 對接點）— 過了才 commit Stage 63B PoC 全量實作（Stage 62 → 63 拆 Stage 模式延伸到 63A → 63B）。

**對齊 Christ 2026-05-10 拍板路線 D** — 廉價 spike 驗證再 commit 大投資是「換引擎不換車身」精神的延伸（先確認新引擎能轉動，再投入車身改裝）。

---

## 子項清單

### 1. Magentic Orchestration nuget 1.3.0 真實 API grep + 紀錄

從 `nuget` package 內容 / xml doc / Microsoft Learn 文件三方 grep：

- Magentic Orchestration class 真實名稱（Charter 候選想像為 `MagenticOrchestrator<TState>` — 待 Forge 驗證）
- IAgent / IAgentTool / Tool calling pattern 真實 interface 名稱
- 動態決策 API（runtime 動態決定下一個 agent / 動態跳 step / 動態組 agent group）真實支援程度
- Anthropic 1.3.0-preview provider 動態決策對接點（既有 Stage 49 spike 揭 Anthropic prerelease feature flag 預設 false 紀錄）
- Gemini provider tool calling pattern（既有 GeminiProvider.cs 164 行對齊點）

**Deliverable**：Stage 63A spike notes 文件 `docs/architecture/v5_charter/05_Stage_63A_Spike_Notes.md` — 含真實 API class / interface 名 + 動態決策能力評估 + 失敗條件命中與否。

### 2. 最小 Petra prototype 實作（throwaway code）

最小可跑 prototype 證明 Magentic Orchestration 動態決策可行：

- **規模上限 100 行**（純 wire 不含註解）— 不寫 production-grade，throwaway 為主
- **位置**：`src/AiTeam.Bot/Orchestration/Petra/spike/PetraSpikePrototype.cs`（spike 子資料夾標記 throwaway，不算 Worker / DI 註冊）
- **內容**：Magentic Orchestration class wire + Petra prompt（hardcoded 5-10 行 trigger 條件）+ 接 2-3 個 mock tool（Cody / Vera 假 ExecuteAsync 回固定 fixture 字串）
- **Provider**：Gemini Flash（呼叫 LlmProviderFactory → GeminiProvider 既有 wire）
- **Session 持久化**：本 spike **不寫 EF Migration**，用 in-memory `List<Message>` 模擬（Stage 63B 才寫多 row table）

### 3. Mock 場景驗證動態決策三條路徑

跑 3 個小場景觀察 Petra 真實判斷（對齊 Charter `01_Spike_Plan.md` 驗證項 #2 三 trigger）：

- 場景 1：「修 typo 1 行」→ 預期 Petra 走 1-on-1 trigger，跳 Kickoff + Design 直接 call mock Cody
- 場景 2：「跨 5 元件 fix」→ 預期 Petra 走 Design trigger，跳 Kickoff 但開 Design
- 場景 3：「架構級重構」→ 預期 Petra 走 Kickoff + Design trigger 全開

**觀察重點**：
- Magentic Orchestration loop 是否真的 LLM 動態決策（vs 固定 step 跑完）
- Tool calling 是否走 Gemini Flash 正常 dispatch 到 mock Cody / Vera
- 三 trigger 至少各 fire 1 次（命中率 100%）

### 4. EF Migration 跨 branch 策略拍板（給 Stage 63B 用）

Stage 63B 寫 EF Migration `Stage63PetraSessionTables` 兩張新表，但 PoC 在 `feature/v5-poc` branch 不 merge — Christ 日常 main Bot 重啟時看到 feature branch 寫的 Migration 會出問題。

**三條候選路線拍板**（Stage 63A spike notes 紀錄結論，Stage 63B 落實）：

- **路線 (a)**：本機 spike 用獨立 PostgreSQL schema / database name（main Bot 跑 `aiteam` DB / spike 跑 `aiteam_spike` DB）
- **路線 (b)**：feature flag 控制 Migration apply（`AppSettings.EnablePetraSession=false` 時 skip 兩張表的 query 路徑，但 Migration 已 apply 到 DB）
- **路線 (c)**：Stage 63B 不寫 Migration，全程 in-memory session（spike 期間 Bot 重啟 = session 清空 — 跟「重啟重跑」紀律對齊）

**Aria 推薦 (c)**（最小耦合 + 對齊既有 Stage 62 Charter「重啟重跑不做 Checkpointing」紀律 + Stage 63B production-ready 才寫 Migration）。但 Christ 拍板。

### 5. Charter 8 條拍板 + 5 自決點對齊清單

Stage 63A 開頭 Forge 掃一遍 Charter `01-04` 抽取：
- 8 條 Christ 拍板對齊清單（從 Stage 62 v2.0 紀錄 + 4 deliverable scattered references 整理）
- 5 個 Forge spike 自決點清單（對齊 Stage 62 結案紀錄）
- Stage 63A prototype 行為對照清單命中與否

**Deliverable**：spike notes 內含對齊表格（Forge prototype 命中 / 漂移 / 待 Stage 63B 實作）。

### 6. Stage 63B 範圍校準回填

Stage 63A spike 結束後依驗證結果回填：
- 通過 → 維持 Stage 62 Charter `04_Stage_63_PoC_Roadmap_Draft.md` 既有 6 子項範圍 + 升 Stage 63B v1.0 正式 Roadmap
- 部分通過（強信心降為中信心 / 中信心降為未知）→ Stage 63B 範圍縮減 + 標 escalate Christ 拍板路線
- 失敗 → escalate Christ + Aria 評估路線（A 繼續 v4 修補 / B 大砍複雜度 / C Claude Code 模式）

### 7. Version bump v3.52.0

`src/Directory.Build.props` `<Version>` 標籤（spike deliverable 仍 minor bump 對齊 Stage 完成精神）。

---

## 設計決策

### 1. spike branch 策略

→ **拍板 main branch 寫 prototype**（Stage 62 Charter spike 同模式 — 純 throwaway code + 文件 deliverable，不開 feature branch 避免 Stage 63A spike 又要管 branch overhead）。Prototype 放 `src/AiTeam.Bot/Orchestration/Petra/spike/` 子資料夾標 throwaway，Stage 63B 開 `feature/v5-poc` 時刪掉重寫。

### 2. prototype 不接既有 production wire

→ **拍板 prototype 完全不接 production DI / 既有 Worker / 既有 BossInteraction**（避免污染 production code）— 自己寫 mock Cody / Vera 假 ExecuteAsync 回固定 fixture 字串。驗證 Magentic Orchestration class wire 行為即可。

### 3. Mock 場景數：3 個 trigger 全驗 vs 1 個最小驗證

→ **拍板 3 個 trigger 全驗**（Charter 驗證項 #2 預期數據「三 trigger 各自至少 fire 1 次」對齊）— 增加 cost 不大但量化準度高。

### 4. Stage 63A 自驗紀律

→ **Aria 自驗對齊 Stage 62 Charter spike 自驗模式**（純 throwaway prototype + 文件 0 production code 動 → dotnet build 0 error / 既有 11 Mock 場景 + dotnet test 131 passed 0 regression 自動對齊）+ Forge log 觀察 3 場景動態決策 trigger 命中。

### 5. Petra prompt hardcoded 5-10 行 vs 對齊 Charter `02_Architecture_Wire.md` 全範圍

→ **拍板 hardcoded 5-10 行**（Stage 63A 是 API 驗證不是 prompt 重寫 — 驗證 Magentic Orchestration class wire 動態決策即可，prompt 重寫留 Stage 63B）。

### 6. EF Migration 三路線 Aria 推薦 (c) in-memory session

→ **Aria 推薦 (c)**（最小耦合 + 對齊「重啟重跑不做 Checkpointing」紀律 + Stage 63B production-ready 才寫 Migration）— 但 Christ 拍板。

### 7. 失敗條件處理

→ **拍板 escalate Christ + Aria 評估路線**（A/B/C — D 已被 Stage 63A 推翻不列）— 不繼續 Stage 63B PoC（避免 fallback Workflow 等於 v4 復辟 + 8 prompt 重寫 + 9 Worker 改介面大投資白做）。

### 8. v3.52.0 minor bump

→ **拍板 minor bump**（spike deliverable 仍對齊 Stage 完成精神）。

---

## 驗收情境

### 場景 A：Magentic Orchestration nuget 真實 API grep 完成

**觸發**：Forge commit `05_Stage_63A_Spike_Notes.md` 含 nuget 真實 API 段落

**驗證**：
- Magentic Orchestration class 真實名稱有寫（不是 Charter 候選想像）
- 動態決策 API 支援程度有明確結論（支援 / 部分支援 / 不支援）
- Anthropic 1.3.0-preview + Gemini provider tool calling 對接點有 grep 結論
- 失敗條件命中與否有明確紀錄

### 場景 B：Petra prototype 跑 3 場景動態決策驗證

**觸發**：`dotnet run` Bot 起來後 / 自驗 Mock 模式跑 PetraSpikePrototype 3 場景

**驗證**：
- 場景 1（修 typo）→ Petra log 含「跳過 Kickoff + Design」+「直接 call mock Cody」軌跡
- 場景 2（跨 5 元件）→ Petra log 含「跳過 Kickoff」+「開 Design」+「dispatch mock Cody → mock Vera」軌跡
- 場景 3（架構級）→ Petra log 含「Kickoff 開 + Design 開」+「動態組合 mock Workers」軌跡
- 三 trigger 各自至少 fire 1 次
- Tool calling 走 Gemini Flash 正常 dispatch（不卡 API gap）

### 場景 C：EF Migration 跨 branch 策略拍板

**觸發**：Christ 對 Aria 推薦 (c) in-memory session 拍板（接受 / 改 (a) 獨立 DB / 改 (b) feature flag）

**驗證**：
- Stage 63A spike notes 紀錄拍板結論
- Stage 63B Roadmap 草稿（Stage 62 `04_Stage_63_PoC_Roadmap_Draft.md`）對應段落更新

### 場景 D：Stage 63B 範圍校準回填

**觸發**：Stage 63A 結案前 Aria 對照三條路徑（通過 / 部分通過 / 失敗）回填 Stage 63B 範圍

**驗證**：
- 通過路徑：Stage 63B v1.0 Roadmap 升正式版（沿用既有 6 子項）
- 部分通過 / 失敗路徑：Stage 63A spike notes 含 escalate Christ + Aria 推薦路線

### 0 regression 確認

- Stage 60+61 既有 11 Mock 場景仍綠（Stage 63A throwaway prototype 不接 production wire）
- dotnet build 0 error / dotnet test 131 passed
- 既有 v4 hierarchical static production 仍服務 Christ 日常

### Christ 視覺驗收（必驗）

- Spike notes 4 段（nuget API + prototype log + EF Migration 拍板 + 範圍校準）完整性
- Christ 拍板「Stage 63A 驗證結論」→ Stage 63B 啟動條件達成 / 改路線 / 停損

---

## 不在範圍（留 Stage 63B）

明確排除避免 scope creep：

- Petra Orchestrator production-grade service（Stage 63B 寫 PetraOrchestratorService.cs 含 DI 註冊 + per-task session 持久化）
- Victoria Router prompt 重寫 + Tool Set RouteToPetra 接 wire
- 8 個 CLAUDE_*.md prompt 重寫（Petra 全砍 + 7 partial）
- 9 個 Worker Service IAgentTool 介面實作 + Capability attribute + DI multi-registration
- EF Migration `Stage63PetraSessionTables` 寫 + apply（Stage 63A 用 in-memory 或 Christ 拍板的替代路線）
- Trial_v9 5 向對照真實任務跑（Dashboard 錯誤處理打磨）

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7+8 條紀律（不寫 code 範例 / 大檔 reference 標精準 line + method 簽名 / 環境細節 reference 標 source of truth / Pipeline 接管 decision 配對檢查）
- **環境細節 source of truth**：
  - Bot Internal API port `5052` 見 `docker-compose.prod.yml`
  - X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
  - MS Agent Framework nuget 真實版本見 `src/AiTeam.Bot/AiTeam.Bot.csproj`（Microsoft.Agents.AI 1.3.0 stable + Microsoft.Agents.AI.Workflows 1.3.0 stable + Microsoft.Agents.AI.Anthropic 1.3.0-preview.260423.1）
  - Gemini provider 對接點 `src/AiTeam.Bot/Agents/GeminiProvider.cs`（既有 164 行）
  - LlmProviderFactory 對接點 `src/AiTeam.Bot/Agents/LlmProviderFactory.cs`（既有 85 行）
- 對齊 Stage 51 + Stage 62 既有 spike Charter 模式（spike Plan + 驗證項 + 預期數據 + 失敗條件）
- **Stage 63A throwaway prototype**（不動 production DI / 不動 EF Migration / 不動 既有 Worker Service / 不動 既有 BossInteraction）
- 對齊 Charter `01_Spike_Plan.md` 驗證項 #2 + #3 + #5 三項（unknown + 中信心 + 強信心交叉驗證）

---

## 規模 / Model 預估

- **規模**：**S**（小 spike + throwaway prototype + 文件 deliverable）
- **Aria session model**：Sonnet 200K + medium effort（純驗證類工作 + 小範圍 prototype + 不寫 production code）
- **Forge context 預估**：~200-300K（含 Charter 4 deliverable read + nuget package contents grep + prototype 100 行 + 3 場景 Mock log 觀察）
- **PoC 任務 LLM cost 預估**：~$2-5（Gemini Flash 3 場景 + 文件規劃輕量）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-11 | 初版計劃書建立（Aria）— Stage 63A = FF 三十六 Phase B PoC 拆出的「最小 API 驗證 spike」。**戰略脈絡**：Stage 62 Charter spike 通過後原 Stage 63 規模 L 含核心 unknown 驗證項 #2 — 廉價先驗 #2 過了才 commit Stage 63B 大投資（Stage 62→63 拆 Stage 模式延伸到 63A→63B）。**範圍 7 子項**：① Magentic Orchestration nuget 真實 API grep ② 最小 Petra prototype 100 行 throwaway ③ 3 Mock 場景驗證動態決策三條 trigger ④ EF Migration 跨 branch 策略拍板 ⑤ Charter 8 條拍板 + 5 自決點對齊清單 ⑥ Stage 63B 範圍校準回填 ⑦ v3.52.0 bump。**設計決策 8 條拍板**（含 Aria 推薦 EF Migration in-memory 路線 (c) 待 Christ 拍板）。**規模 S** / Sonnet 200K + medium / Forge context 預估 ~200-300K / PoC LLM cost ~$2-5。**Stage 63A throwaway prototype**（main branch 寫 spike 子資料夾 / 不動 production wire / 0 EF Migration）— Stage 60+61 既有 11 Mock 場景 + dotnet test 131 passed 0 regression 自動對齊。 |
| v2.0 | 2026-05-11 | **Forge 結案 + 自驗第二輪硬通過 + Aria 第二段補強**（Aria）— v3.52.0 deployed + Spike ✅ **硬通過**（Christ AI Studio Gemini 2.5 Flash key 真打 3 場景 trigger 命中率 100% / 真實 cost $0 免費 tier + 1 次 503 retry success）。**3 場景真打結果**：① 修 typo → `Cody`（1 agent）→ 1-on-1 trigger ② 跨 5 元件 → `Cody → Vera`（2 agents）→ Design trigger ③ 架構級重構 → `Cody → Vera → Cody → Vera`（4 agents 多輪）→ Kickoff trigger（對齊 Charter `01_Spike_Plan.md` 驗證項 #2 預期數據 100%）。**核心 finding**：Charter 候選 `MagenticOrchestrator<TState>` **不存在於 nuget 1.3.0** — 動態決策真實 hook = `GroupChatManager.SelectNextAgentAsync` override + `AgentWorkflowBuilder.CreateGroupChatBuilderWith` + `HandoffWorkflowBuilder` 替代 pattern + `AIAgentExtensions.AsAIFunction` Worker-as-Tool 真實 API（命名落差 ≠ 驗證項 #2 失敗 — Charter `02_Architecture_Wire.md` 用 errata 補釘）。**⚠️ 2 framework limitation 揭露**（Phase 2 真打過程中新發現 — 對 Stage 63B 規劃戰略級價值）：① **Limitation (a)**：base `GroupChatManager` subclass **不啟動 manager loop**（`AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` → `GroupChatHost` executor 收 input 立刻完成 1 superstep / manager 3 hook 全 0 invoke — 推測 nuget 1.3.0 stable 對 base subclass 未啟動 manager loop / 需 internal seam）→ **Stage 63B 走候選 (B) 自寫 PetraOrchestratorService + BuildSequential**（spike 已驗 path）② **Limitation (b)**：base `AIAgent` subclass **不被 framework workflow dispatch**（`MockWorkerAgent.RunCoreAsync` / `RunCoreStreamingAsync` 兩 override 都 0 invoke — 推測 framework executor wrapper depends on ChatClientAgent-specific path / IChatClient 注入）→ **Stage 63B 必走 `ChatClientAgent(IChatClient, ...)` ctor + 新建 `ClaudeCodeChatClientAdapter : IChatClient`**（包既有 ClaudeCodeService — 從「可選」升「必走」）。**範圍變更接受 +13%**：prototype 113 行（超 Roadmap 100 行上限 13%）— 揭 framework limitation 後改 `DecideAsync` + `BuildSequential` path（動態決策從 GroupChatManager override 改 Petra 一次性 LLM DecideAsync + BuildSequential 對齊 Limitation (a) workaround）+ Mock 場景 assertion 從「Worker call count」改「PetraDecisions 動態分流序列」（Limitation (b) 揭露後聚焦核心命題）— 接受 +13% 因為揭 framework limitation value 巨大（給 Stage 63B 早期 derisk）。**5 deliverable**：① `PetraSpikePrototype.cs` **113 行 throwaway**（163→113 移除 PetraSpikeGroupChatManager 死路徑 reference 留 spike notes）② `PetraSpikePrototypeTests.cs` 改 assertion 聚焦 `PetraDecisions` 動態分流序列 ③ `05_Stage_63A_Spike_Notes.md` 第 2 段補完整 3 場景**實測 log** + 2 framework limitation 揭露 + Petra prompt 設計 + cost 紀錄 + 範圍變更紀錄 + 結論升「軟通過」→「**硬通過**」④ `04_Stage_63_PoC_Roadmap_Draft.md` errata 子項 1 候選 (B) 自寫 orchestrator + 子項 4 IChatClient adapter 升「必走」⑤ `Directory.Build.props` v3.52.0。**Christ 拍板補充**：EF Migration 路線 **(c) in-memory session**。**Aria gate0 揭露 3 點 Forge 全收**（test path 修正 / FF 五十九 hand-off / Gemini env key 對齊）。**Forge spike 新增第 6 自決點**（跨 assembly `protected` not `protected internal` override CS0507 修正）。**dotnet test baseline 漂移 131 → 134**（3 spike test silently pass 無 GEMINI key — 真打時 3 個 PASS — 未來新 Stage 看到 134 應對齊本 v2.0 紀錄理解）。**Aria gate1 commit 檢查通過**（591b36f 對照計劃書 7 子項全做 + Forge 自驗第二輪 b56ffd0 升硬通過）。**Aria 校準錨 待 Christ 補 Forge context 數字**（spike 規模 S + throwaway prototype + 文件 deliverable 混合型 — 預估 ~200-300K，對齊 Stage 62 ×0.71 純文件 vs Stage 49 ×0.78 spike 混合型）。**戰略主軸**：Stage 63A spike ✅ **硬通過** → **Stage 63B PoC spike 啟動條件達成（含 2 framework limitation 戰略級早期 derisk）**。Stage 63B 必含：feature/v5-poc branch + **候選 (B) 自寫 PetraOrchestratorService + BuildSequential**（不走 GroupChatManager loop）+ **ClaudeCodeChatClientAdapter : IChatClient**（必走 ChatClientAgent ctor）+ 8 CLAUDE_*.md prompt 重寫 + 9 Worker 走 ChatClientAgent + Capability attribute + EF Migration `Stage63PetraSessionTables` + Trial_v9 5 向對照 / 規模 L / cost ~600-1000K + LLM cost ~$5-15（建議儲值 ≥ $25 buffer，餘額 $17.22）。commits：`a065082`(規劃) + `591b36f`(主實作 + 軟通過結案) + `b56ffd0`(自驗第二輪真打 + 升硬通過 + 揭 2 framework limitation)。 |

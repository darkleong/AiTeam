# Stage 68 Roadmap — v5.5 Phase 1 完整收口前 production-ready 補強

> 目標版本：**v3.58.0**（minor — Phase 1 完整收口拍板閘門前最後一塊 production-ready 補強）
> 狀態：✅ 已完成（2026-05-16）
> 文件版本：v2.0
> 範圍：FF 二 v5 PoC simplification 補強 2 點 + Stage 67 follow-up know-how 升級 conventions/
> 規模：M
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 1 完整收口前最後一塊

---

## 戰略脈絡

**Trial_v13 結案後 v5.5 Phase 1 完整收口前最後一個工程 Stage**：

- Stage 67 ✅ Talent-Skill separation 重構基底落地 / Trial_v13 揭 3 議題（reload-cache scope 紀律 / workspace cleanup 紀律 / Cody push to main 紀律 — 第 3 條已修 commit `0226c60`）
- Christ 2026-05-16 拍板「Stage 68 + Trial_v14 合併跑」攤平 Trial cost / 時間 — Stage 68 範圍收緊在「Trial_v14 順便驗 + Phase 1 完整收口拍板閘門前準備」
- 對齊「production-ready 漸進 path 優先」+「對冗餘不容忍」+「持續開發迭代」紀律

**範圍邊界刻意收緊**（對齊 Phase 1 完整收口前最後一塊精神）：
- ✅ 做：FF 二 v5 PoC 補強清單實作 2 點（規模 S 等級）+ Stage 67 follow-up know-how 升級 docs/conventions/ef-core.md
- ❌ 不做：Phase 2 Step 3 DB 持久記憶 schema（規模 M-L 留 Stage 69 獨立 Stage）/ v5 PoC 其他 6+ 點補強清單（留 Phase 1 完整收口後評估）

**Trial_v14 啟動條件**：Stage 68 Mock 全綠 + Aria gate1 通過 → 沿用 Trial_v6-v13 同 prompt 真實任務驗 = 同時驗 Stage 68 補強 + Stage 67 紀律修法 `0226c60` 生效（Cody 不自己 commit / Petra FinalizeGitAsync 真實開 PR）。

---

## 子項清單

### 1. PetraSessionRepository.AppendMessage 改 async（FF 二補強清單）

**現狀**：[`PetraSessionRepository.cs:26`](src/AiTeam.Data/Repositories/PetraSessionRepository.cs#L26) `AppendMessage(Guid sessionId, string role, string content, string? toolCallId = null)` 是 sync method — 對比既有 `BossInteractionRepository` 全 async pattern 不對齊（Stage 63B 揭 v5 PoC 簡化議題 #4）。

**修法**：
- AppendMessage → AppendMessageAsync 加 CancellationToken 參數 + Task return
- 對應 caller（PetraOrchestratorService 自管 chain dispatch tool role 寫入 + DecideAsync raw 寫入）統一加 await
- 對齊既有 async pattern

**Forge spike 必驗**：grep AppendMessage 所有 caller 完整列出（避免漏轉 async）

### 2. v5 PoC post-confirm flow 收尾（Trial_v12 揭 🟡 工程議題）

**現狀**：Trial_v12 揭真實 — Christ 點「確認派工」（BossInteraction ResponseAction=confirm_yes）後 1 秒內系統自動 fire 一張 `exec_confirm` 類型 stale 卡（AgentName 空白 / Petra 已跑完沒下一個 worker）— v4 既有 default 行為 fall back 進 v5 path。Christ 需手動 cancel stale 卡（UI 雜訊）。

**修法**：v5 path Petra orchestrator 收尾段加「ceo_confirm responded 後乾淨關閉 BossInteraction」邏輯 + 不 fire 額外 exec_confirm 卡。

**Forge spike 必驗**：
- grep 真實 fire stale 卡 wire 點（CeoAgentService.ProcessWithClaudeCodeAsync forward 進 Petra path 後 v4 default 行為是否帶 next-step `exec_confirm` 預設）
- 修法落點：CeoAgentService forward 條件分支 vs PetraOrchestratorService.StartAsync 結尾 — Forge plan 階段拍

### 3. docs/conventions/ef-core.md 補強 PostgreSQL NULL unique + DbSeeder race pattern（Stage 67 follow-up know-how 升級）

**Stage 67 follow-up commit `6fd9472` 揭三條 know-how**（[Stage 67 Roadmap 踩坑紀錄](Stage_67_Roadmap.md)）：

1. **PostgreSQL `NULL ≠ NULL` unique 語義** — nullable 欄位含 NULL 群組 unique 必走 partial unique index `WHERE col IS NULL`
2. **Bot + Dashboard 並行 SeedAsync race** — per-row SaveChanges + catch DbUpdateException + Entity detach pattern
3. **DI register 在 `app.Build` 前 / DB migrate 在後 矛盾** — Singleton factory + IServiceScopeFactory pattern 解

**修法**：[`docs/conventions/ef-core.md`](docs/conventions/ef-core.md) 加新段「PostgreSQL nullable unique + race-safe DbSeeder pattern」涵蓋 3 條 know-how + 對齊既有 ef-core.md 風格（Migration 流程 / Repository 模式段參考）+ Stage 67 reference link。

---

## 設計決策

1. **範圍守緊 — 不擴 FF 二其他 6+ 點補強清單**（對齊 Christ 「production-ready 漸進 path 優先」+ Phase 1 完整收口閘門精神）— 其他補強留 Phase 1 完整收口後 Stage 69+ 評估
2. **子項 2 v5 PoC post-confirm 修法落點 Forge spike 拍**（CeoAgentService 條件分支 vs PetraOrchestratorService 結尾兩候選）
3. **conventions/ef-core.md 加段不重寫整檔**（對齊「對冗餘不容忍」紀律 — 只 append 新 know-how 段不重組）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：AppendMessage 改 async 後既有 v5/v5.5 dispatch chain 0 regression（保護驗）

**觸發**：dotnet test 跑既有 PetraOrchestratorServiceTests 17 case（Test 12+13 chain pass-through + Test 14-17 v5.5）

**驗證**：178 PASS 維持 / 0 新 warning / 0 v4 regression（grep AppendMessage 所有 caller 都對齊 async）

### 場景 B：v5 PoC post-confirm flow 收尾乾淨（Trial_v12 議題驗）

**觸發**：Mock /mock framework_pipeline 或真實 Trial_v14 任務 → Christ 點「確認派工」（BossInteraction ResponseAction=confirm_yes）後 5 秒內

**驗證**：
- SQL `SELECT "Type", "AgentName", "Status" FROM boss_interactions WHERE "CreatedAt" >= '<觸發時間>'`
- 期望：0 新增 `exec_confirm` 類型 stale 卡（vs Trial_v12 真實 1 張 stale 卡 / Christ 需手動 cancel）
- Bot log 0 含「fire exec_confirm AgentName 空白」warn

### 場景 C：PostgreSQL nullable unique pattern conventions/ 紀律落地（純文件驗）

**觸發**：read docs/conventions/ef-core.md 末段 + grep 是否含 `partial unique index`

**驗證**：
- 含新增段「PostgreSQL nullable unique + race-safe DbSeeder pattern」
- 段內 cover 3 條 know-how（NULL ≠ NULL 語義 / partial unique index 修法 / DbSeeder race-safe pattern）+ Stage 67 reference link

### 場景 D：v4 既有 production path 0 regression（守護驗）

**觸發**：
- DB flag `Workflow:UseTalentSkillSeparation=false` + `Workflow:UsePetraOrchestratorV5=false`（v4 path）
- /mock framework_kickoff_happy 跑

**驗證**：v4 path 完整跑通 0 regression / dotnet test 178 PASS

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Stage 68 在 main branch 跑（含 Stage 67 全 commits + 紀律修法 `0226c60` + revert `8b24192`）
- 預估規模 M（子項 1 S + 子項 2 S-M + 子項 3 純文件 S = 合計 M）
- 預估 Forge context mid 250-350K（對齊 production-ready 補強區間 ×0.78-0.99 / Stage 65/66 baseline）

---

---

## 實作紀錄（v2.0 — 2026-05-16）

### 實作完成項目

**子項 1 — PetraSessionRepository.AppendMessage 改 async** ✅
- [`PetraSessionRepository.cs:26`](../../src/AiTeam.Data/Repositories/PetraSessionRepository.cs#L26) AppendMessage → AppendMessageAsync + CancellationToken + Task return
- 同步轉 9 處 caller：PetraOrchestratorService 6 處（user / assistant / tool role 寫入 — line 60/196/265/314/375/457）+ PetraOrchestratorServiceTests 3 處（line 75-77）
- LogWorkflowEvent framework callback 用 fire-and-forget enqueue 維持非 async signature（純 EF Add 無 I/O — 安全）

**子項 2 — v5 PoC post-confirm flow 收尾** ✅
- 新建 [`CeoResponseActions.cs`](../../src/AiTeam.Shared/Constants/CeoResponseActions.cs)（採納 Christ #1 nice-to-have — magic string 抽常數對齊 AgentNames pattern）
- [`CeoAgentService.cs:108`](../../src/AiTeam.Bot/Agents/CeoAgentService.cs#L108) 寫入點改用 `CeoResponseActions.PetraV5Dispatched`
- [`ProposalConfirmationService.cs:50`](../../src/AiTeam.Bot/Orchestration/Proposal/ProposalConfirmationService.cs#L50) Dashboard 路徑加 v5/v5.5 path 偵測 + skip TaskItem + skip exec_confirm fire
- [`ButtonCallbackRouter.cs:394`](../../src/AiTeam.Bot/Discord/Routing/ButtonCallbackRouter.cs#L394) Discord button 路徑同邏輯 + Followup「✅ Petra 已動態調度完成」訊息

**子項 3 — docs/conventions/ef-core.md 補強** ✅
- 加新段「PostgreSQL nullable unique + race-safe DbSeeder pattern」插在「PostgreSQL 例外處理」段後（語義延伸）
- 涵蓋 3 條 know-how：① NULL ≠ NULL unique 語義 + partial unique index 修法 ② 並行 SeedAsync race + per-row SaveChanges + DbUpdateException + Entity detach ③ DI lifecycle Singleton factory + IServiceScopeFactory
- Stage 67 Roadmap reference link

**子項 4 — Directory.Build.props v3.57.0 → v3.58.0** ✅

### 關鍵設計決策

1. **子項 2 修法落點 Forge spike 推翻 Aria 兩候選方案**：
   - ❌ Aria 候選「PetraOrchestratorService 結尾關閉 BossInteraction」**不可行** — Petra 跑完時 ceo_confirm BossInteraction 還沒被 fire（fire 在上層 CeoResponse 回來後才建）
   - ❌ Aria 候選「CeoAgentService 條件分支」可行但太上層 — marker 已在 `Action = PetraV5Dispatched`，沒必要再翻譯
   - ✅ Forge 拍：修在 confirm 接力 service（ProcessCeoConfirmAsync + HandleConfirmYesAsync 兩處對等）— 真實 stale fire 點 = 修源頭最直接
2. **`LogWorkflowEvent` 內 AppendMessageAsync 用 fire-and-forget**：framework callback signature 非 async（`void LogWorkflowEvent(...)`）— 純 EF Add 無 I/O 等待 → `_ = sessionRepo.AppendMessageAsync(...)` 安全
3. **`AppendMessageAsync` 當前回 `Task.CompletedTask`**：純 EF Add 無 I/O — 但 CT 參數 + Task return 對齊 BossInteractionRepository pattern + 為將來 SaveChanges-inline 進化保留介面
4. **magic string 抽 constant**（採納 Christ #1）：對齊 [`AgentNames`](../../src/AiTeam.Shared/Constants/AgentNames.cs) pattern — 跨 3 檔 hardcode 收一處新 [`CeoResponseActions`](../../src/AiTeam.Shared/Constants/CeoResponseActions.cs)

### Mock 覆蓋情況

| 子項 | Mock 覆蓋 | 備註 |
|---|---|---|
| 1 AppendMessage async | ✅ 完整（dotnet test Test 4 直接打）| 178 PASS regression baseline |
| 2 v5 PoC post-confirm 跳過 | ⚠ **Mock 物理限制** — 所有 `framework_*` scenarios 都是 pipeline-stage mock，**無一驅動 CEO confirm 流程**（ProcessCeoConfirmAsync / HandleConfirmYesAsync）| Code path 100% wired（CeoResponseActions 1 producer + 2 reader / 0 magic string 殘留 / Bot 啟動 0 error 證實 IL 載入 OK）→ 留 Trial_v14 真實任務驗（對齊 commit message pre-acknowledge / 對齊 Stage 64/65 既有紀律精神）|
| 3 ef-core.md | ✅ grep 驗證內容齊全 | 純文件 |

### 踩坑紀錄

無。Forge spike Phase 1 已揭真實 wire 點 + 推翻兩候選方案落點 — 實作期 0 follow-up bug / 0 self-diag fix / 1 commit 完成。

### Production state 確認（自驗時取）

```
Workflow:UsePetraOrchestratorV5    = true   ← v5 path active production
Workflow:UseTalentSkillSeparation  = false  ← v5.5 待 Trial_v14 拍板切
MockMode                            = false  ← production
```

→ Stage 68 sub-item 2 修法 **immediately effective** on next real CEO task（v5 path 已上線）— Trial_v14 開跑時即驗。

### 本機驗證

- `dotnet build AiTeam.slnx`：0 error / 102 warning（全既有 / 0 新引入）
- `dotnet test`：178 PASS（51 AiTeam.Bot.Tests + 127 AiTeam.Tests.Generated / 0 fail / 0 skip）= 對齊 Roadmap 場景 A 期望
- CI/CD run [25935590154](https://github.com/darkleong/AiTeam/actions/runs/25935590154) **success**
- 容器 fresh deploy + 0 startup error / 0 exception

### 對應 commit

- `0b7e3c7` feat(stage68): v3.58.0 — v5.5 Phase 1 完整收口前 production-ready 補強（3 子項）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-16 | **實作完成（Forge）** — 3 子項 + 1 nice-to-have 採納（magic string 抽常數）/ 1 commit `0b7e3c7` / 0 follow-up / 178 PASS regression baseline / Mock 物理限制 acknowledge（sub-item 2 留 Trial_v14 真實驗）/ Forge spike 推翻 Aria 兩候選方案落點（PetraOrchestratorService 結尾不可行 + CeoAgentService 太上層 → 修在 confirm 接力 service 兩處對等）。 |
| v1.0 | 2026-05-16 | 規劃書建立（Aria）— Stage 68 = Trial_v13 結案後 v5.5 Phase 1 完整收口前最後一個 production-ready 補強 Stage。**3 子項**：① PetraSessionRepository.AppendMessage 改 async ② v5 PoC post-confirm flow 收尾 ③ docs/conventions/ef-core.md 補強 PostgreSQL NULL unique + DbSeeder race pattern。**4 驗收場景**：A AppendMessage async 0 regression / B v5 PoC post-confirm 乾淨收尾 / C conventions/ 紀律落地 / D v4 既有 production path 0 regression。**範圍邊界刻意收緊**：不擴 FF 二其他補強 / 不開 Phase 2 Step 3 DB 持久記憶 schema（規模 M-L 留 Stage 69）。**Trial_v14 啟動條件**：Stage 68 Mock 全綠 + Aria gate1 通過 → 沿用 Trial_v6-v13 同 prompt 真實任務驗 = 同時驗 Stage 68 補強 + Stage 67 紀律修法 `0226c60` 生效 → 通過 → Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = **v5.5 Phase 1 完整收口** + 進 Phase 2 Step 3 DB 持久記憶 schema 設計。 |

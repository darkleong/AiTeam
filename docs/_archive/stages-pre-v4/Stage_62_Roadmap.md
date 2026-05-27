# Stage 62 Roadmap — FF 三十六 Phase B Charter spike（v5 動態架構規劃文件 deliverable）

> 目標版本：**v3.51.0**（minor — Charter spike 文件 deliverable，無 production code 改動）
> 狀態：規劃中
> 範圍：FF 三十六 Phase B Charter spike（補完 spike Plan + 架構 wire 規劃 + v4 code audit + PoC Roadmap 草稿）
> 規模：M

---

## 戰略脈絡

**Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退**（11/12 → 部分 → 0 → 0 卡死更前置）= **infinite loop pattern 真實實證 = v4 hierarchical static 補強 ROI 為負**（Stage 60+61 才修完就揭新類型 🔴）。

**Christ 2026-05-10 拍板路線 D — FF 三十六 Phase B 動態架構 spike**（對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項 + 漸進遷移過半 + Phase A ✅ 啟動條件達成）。

**Stage 62 = Phase B Charter spike**（文件 deliverable）+ **Stage 63 = Phase B PoC spike**（小範圍真實任務驗證）兩階段拆分，對齊 Stage 51 既有 spike Charter 模式 + Stage 53A 拆 Stage 模式 — Charter 通過才 commit PoC 投資（範圍可控）。

**既有 80% 設計拍板**（FF 三十六 2026-05-01 brainstorm 已成熟）：
- 4 層 Hierarchy 架構（Christ → Victoria → Petra Orchestrator → Workers）
- 5 個關鍵挑戰拍板（Victoria 角色 / Petra 決策邊界 / 老闆介入機制 / Mock 模式 / Crash Recovery）
- Hybrid 會議模式（會議默認保留小需求動態跳過 + Petra 動態切換 Group Chat / Agent-as-Tool）
- Tool Set + Capability-based Multi-Agent + MS Agent Framework Magentic Orchestration

**Stage 62 補完剩 20%**（Charter spike 範圍）：
- 7 驗證項細節從「概念」變「具體 spike Plan」
- 架構 wire 規劃落到具體檔案 / class / DI 結構
- v4 code audit 盤點（保留 / 重寫 / 吸收分類）
- PoC spike Roadmap 草稿（Stage 63 範圍）

---

## 子項清單

### 1. 補完 7 驗證項細節（FF 三十六既有 spike 任務具體化）

從「概念」變「具體 spike Plan」— 每項含「驗證 deliverable + 對應 wire 點 + Mock 場景 + 預期數據」：

- **Victoria Router 模式**：純 facade 設計 wire 點 + Tool Set（接 Petra-only 派工 + Discord/Dashboard 雙通道）+ 既有 VictoriaService code reuse 量化
- **Petra 自主調度行為**：CLAUDE_Petra.md 重寫範圍（從「品質審核閘門」變「全程動態 orchestrator」）+ Magentic Orchestration wire 點 + 自主決策邊界 prompt 設計初版
- **per-task session 跨階段記憶**：Petra session 持久化設計（DB schema 候選）+ Worker session 對應策略 + 記憶傳遞 vs Tool Set 取捨
- **Crash Recovery 重啟重跑**：既有 Pipeline framework Crash Recovery（Stage 54 4 CheckpointStore）廢棄路徑 + 重啟後狀態恢復策略 + BossInteraction 已 responded 算 task input 紀律
- **Mock Mode 用 Gemini Flash 跑 Petra**：既有 GeminiProvider + AgentConfig DB 動態切換 wire 點 + Petra 用 Gemini 跑 + Workers hardcoded mock 對齊既有 MockClaudeCodeService 模式
- **遷移成本量化**：既有 v4 hierarchical static code 多少保留 / 多少重寫 / 多少吸收（具體量化 LoC + service / DI 結構統計）
- **Hybrid 會議 trigger 條件**：CLAUDE_Petra.md 寫死 trigger 條件初版（既有 FF 三十六拍板 Kickoff/Design/1-on-1 三 trigger 細化）+ Mock Petra 真實判斷觀察點

### 2. 架構 wire 規劃（4 層 Hierarchy 落具體 service / DI 結構）

- **Layer 1 Christ 入口**：既有 Discord/Dashboard 雙通道保留 + Operation Center 既有保留 — 0 改動
- **Layer 2 Victoria（Discord 秘書 / Router）**：VictoriaService 既有保留 + prompt 重寫 + Tool Set 改純 Petra 派工（不直接 invoke Workers）
- **Layer 3 Petra Orchestrator**（新增）：
  - PetraOrchestratorService 新建 / 對應 MS Agent Framework Magentic Orchestration class wire
  - per-task session 持久化設計（Petra session DB schema 候選 + Migration 候選 — Charter spike 階段不寫 Migration 只寫 schema 設計）
  - Tool Set 接 Workers + Capability-based 標籤
- **Layer 4 Workers**：既有 10 Agent service 保留（DevAgentService / VeraService / QaAgentService / DocAgentService / RequirementsAgentService / DesignerAgentService / RenaService / MayaService 等）+ CLAUDE_*.md prompt 全 10 個重寫對齊 Petra 動態調度

### 3. v4 code audit 盤點（保留 / 重寫 / 吸收三類分類）

| 類別 | 範圍 | v5 處理 |
|---|---|---|
| **吸收**（v5 動態架構不需）| WorkflowEngine + 7 routing types HITL pattern + 4-7 Stage Executor + 4 CheckpointStore Crash Recovery + Pipeline framework Workflow + framework Kickoff/Design Router | 不再用 / 廢棄路徑紀錄 |
| **重寫**（v5 仍需但 prompt 改）| Worker Service（DevAgentService / VeraService / QaAgentService / DocAgentService / RequirementsAgentService / DesignerAgentService 等 6+ 個）+ VictoriaService prompt + 全 10 個 CLAUDE_*.md | prompt 重寫 / Tool Set 接 wire |
| **全保留**（與 framework 無關基礎設施）| Discord/Dashboard 雙通道 + DB + EF Core + Token 守門 + Mock Mode + Token logs + IsEstimated + AgentConfig + Stage 27 DB-as-Queue + Stage 28 BossInteraction 樂觀鎖 | 0 改動 |

**量化 deliverable**（Charter spike 階段）：
- 「吸收」LoC 統計（v4 hierarchical static 投資多少被廢棄）
- 「重寫」LoC 統計 + prompt 重寫範圍（10 個 CLAUDE_*.md）
- 「保留」LoC 統計（基礎設施投資保留比例）

### 4. PoC spike Roadmap 草稿（Stage 63 範圍）

Stage 63 PoC spike 規劃草稿（Charter spike 階段先寫初版，Charter 通過後升 Stage 63 v1.0）：
- **branch 策略**：feature/v5-poc 獨立 branch（main 保留 v4 不動）
- **PoC 範圍**：Petra Orchestrator + Victoria Router + 4-10 Worker prompt 重寫 + Tool Set wire + Mock 模式（Petra Gemini Flash + Workers hardcoded mock）+ per-task session 持久化（DB Migration 候選）
- **驗證任務**：跑 Trial_v6/v7/v8 同任務（Dashboard 錯誤處理打磨 — 五向對照 Trial_v5/v6/v7/v8/v8-dynamic）
- **驗收標準**：Phase 1 完整 deliver / cost 對齊 Trial_v5 baseline ±50% / 0 🔴 新類型議題
- **規模 / cost 預估**：L / Opus 1M + high / 預估 ~600-1000K

### 5. 戰略大重評估後 backlog 大整理（Christ 拍板對齊）

| FF | 處理 |
|---|---|
| FF 五十九（Trial 框架認知錯位）| **close 不做** — v5 Petra orchestrator prompt 重寫吸收（不再含「Stage 61 follow-up」字樣困惑）|
| FF 六十（Stage 60 retry path silent）| **close 不做** — v4 修法 ROI 負 |
| FF 五十七（Petra prompt 5 位置 SoT）| **close 不做** — v5 prompt 重寫（CLAUDE_Petra.md + 4 prompt builder 全砍重寫對齊 Petra orchestrator）|
| FF 五十八（其他 3 申訴 path supersede）| **close 不做** — v5 動態架構吸收 |
| FF 二十五/四十六/四十八（Cody prompt 對齊群組）| **保留** — 跟 framework 無關 / v5 Worker prompt 仍適用（Cody Worker 仍存在）|
| FF 五十四子項 2/3（怪物大檔拆解）| **保留評估** — 視 v5 重寫後哪些大檔仍存在（部分被「吸收」會自然消失）|
| FF 三十六 | **進入 Phase B Charter spike**（本 Stage）|

### 6. Charter spike 揭露議題追蹤（Forge spike 階段補強）

對齊 Stage 51/53A 既有 spike 模式 — Charter spike 階段 Forge 揭露的新議題：
- 純技術 / 內部設計議題 → Forge 自決（對齊 Stage 60+61 healthy 模式）
- 對 Christ 看到行為 / 戰略級設計衝突 → escalate Christ + Aria 拍板（escalate 用純文字 markdown 列議題 + 路線 + Aria 推薦對齊 Stage 60+61 healthy 模式）

### 7. Version bump v3.51.0

`src/Directory.Build.props` `<Version>` 標籤（純文件 deliverable 仍 minor bump 對齊 Stage 完成精神）。

---

## 設計決策

### 1. Charter spike 範圍：文件 only vs 含小 PoC 驗證

→ **拍板文件 only**（PoC 留 Stage 63）— 範圍可控 + Charter 通過才 commit PoC 投資（對齊「拆兩階段」精神）。

### 2. branch 策略：Charter main / PoC branch

→ **拍板 Charter spike 在 main branch 寫 docs**（純文件 deliverable 不動 production code）+ PoC spike 開 feature/v5-poc branch（main 保留 v4 不動 — Christ 拍板既有對齊）。

### 3. 既有 v4 code 處理

→ **保留 main 不動 + Charter spike audit 文件化**（盤點保留 / 重寫 / 吸收三類 + LoC 量化）+ PoC spike 開 branch 跑（不刪 v4 — PoC 萬一失敗回滾 + production 仍服務 Christ 日常）。

### 4. Worker pool：保留 10 vs 收斂

→ **拍板保留 10 Agent role**（CLAUDE_*.md prompt 重寫對齊 Petra 動態調度）— spike PoC 觀察真實使用後哪些 Agent 少用再收斂（YAGNI 精神對齊 Christ 既有「老闆對團隊」模型偏好）。

### 5. PoC 跑什麼任務驗證

→ **拍板沿用 Trial_v6/v7/v8 同 prompt**（Dashboard 錯誤處理打磨 — 對照組精準度高 + Trial 框架認知錯位 FF 五十九在 v5 動態架構吸收）。對照數據組升級為 5 向（v5/v6/v7/v8/v8-dynamic）。

### 6. Charter spike 揭露重大議題的處理

→ **對齊 Stage 60+61 healthy 模式**：純技術細節 Forge 自決 + 戰略級設計衝突 escalate Christ + Aria 拍板（純文字 markdown 列議題 + 路線 + Aria 推薦 + 給定見不攤議題）。

### 7. Spike Plan 模板

→ **對齊 Stage 51 既有 spike Charter 模式**（spike Plan + 7 驗證項 + 預測項 + 對應 wire 點 + Mock 場景）。

### 8. v3.51.0 minor bump

→ **拍板 minor bump**（純文件 deliverable 仍對齊 Stage 完成精神 — 對齊既有 Stage 結案 minor bump 慣例）。

---

## 驗收情境

### Charter spike 文件 deliverable 驗收（4 場景）

#### 場景 A：Charter Plan 文件完整性檢查（spike Plan + 7 驗證項細節）

**觸發**：Charter spike Plan 文件 commit 後 Aria 對照 7 驗證項清單檢查

**驗證**：
- 7 驗證項全部從「概念」變「具體 spike Plan」（每項含 deliverable + 對應 wire 點 + Mock 場景 + 預期數據）
- Victoria Router / Petra 自主調度 / per-task session / Crash Recovery / Mock Gemini Flash / 遷移成本量化 / Hybrid 會議 trigger 七項全 cover
- 每項指向具體 src/ 檔案 / class / DI 結構（不是只寫「概念」）

#### 場景 B：架構 wire 規劃文件完整性檢查（4 層 Hierarchy 落具體 service / DI）

**觸發**：架構 wire 規劃文件 commit 後 Aria 對照 4 層 Hierarchy 檢查

**驗證**：
- Layer 1 Christ 入口（0 改動）+ Layer 2 Victoria（保留 + prompt 重寫）+ Layer 3 Petra Orchestrator（新建 service / DI / per-task session schema）+ Layer 4 Workers（10 Agent service 保留 + prompt 重寫）四層全 cover
- Tool Set 細節含 Victoria 接 Petra 工具 + Petra 接 Workers 工具 + Capability-based 標籤 wire
- per-task session 持久化 DB schema 候選文件化（Stage 63 PoC 寫 Migration 用）

#### 場景 C：v4 code audit 文件完整性檢查（保留 / 重寫 / 吸收三類分類）

**觸發**：v4 code audit 文件 commit 後 Aria 對照三類分類檢查

**驗證**：
- 三類分類完整（吸收 / 重寫 / 全保留）+ 每類列具體 service / class / file
- 量化統計：吸收 LoC / 重寫 LoC / 保留 LoC（v4 投資保留比例量化）
- 風險評估：吸收路徑廢棄風險 + 重寫路徑遷移成本 + 全保留路徑 0 風險

#### 場景 D：PoC spike Roadmap 草稿完整性（Stage 63 範圍）

**觸發**：PoC spike Roadmap 草稿 commit 後 Aria 對照 Stage 63 範圍檢查

**驗證**：
- branch 策略 + PoC 範圍 + 驗證任務 + 驗收標準 + 規模 / cost 預估全 cover
- Stage 63 範圍可獨立評估（規模 L / 預估 ~600-1000K / Opus 1M + high）
- Christ 視覺驗收 Charter Plan 完整性 + 拍板 Charter 通過 → 進 Stage 63 PoC spike

### Christ 視覺驗收（必驗）

- Charter Plan 文件 4 個 deliverable（spike Plan + 架構 wire + v4 audit + PoC Roadmap）完整性
- Christ 拍板 Charter 通過 → Stage 63 PoC spike 啟動條件達成

### 0 regression 確認

- Stage 60+61 既有 11 Mock 場景仍綠（Charter spike 不動 production code）
- dotnet build 0 error / dotnet test 131 passed（純文件改動）
- 既有 v4 hierarchical static production 仍跑著服務 Christ 日常（Charter spike 不影響）

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7+8 條紀律（不寫 code 範例 / 大檔 reference 標精準 line + method 簽名 / 環境細節 reference 標 source of truth / Pipeline 接管 decision 配對檢查）
- **環境細節 source of truth**：Bot Internal API port `5052` 見 `docker-compose.prod.yml` / X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey` / Internal endpoint 真實 path 見 `src/AiTeam.Bot/Api/InternalController.cs`
- 對齊 Stage 51 既有 spike Charter 模式 + Stage 53A 拆 Stage 模式
- **Charter spike 純文件 deliverable**（不動 production code / 不動 EF Migration / 不動 DI 結構）
- 對齊既有 80% 設計拍板（4 層 Hierarchy + 5 挑戰 + Hybrid 會議 + Tool Set + Capability-based）— 不重新評估 80% 已成熟設計
- 對齊 FF 三十六 既有 spike 任務 7 驗證項清單（不新增不刪減）
- **環境細節 reference 紀律**：含「prompt builder 真實檔名 + method 數量 + config default value」（Stage 61 揭露同類根因第三次累積修根因 — workflow_aria.md 第 7 條延伸範圍段）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Stage 62 = FF 三十六 Phase B Charter spike（v5 動態架構規劃文件 deliverable）。**戰略脈絡**：Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop pattern 真實實證 = v4 hierarchical static 補強 ROI 為負 → Christ 2026-05-10 拍板路線 D（FF 三十六 Phase B 動態架構 spike — 對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項）。**Stage 62 = Charter spike + Stage 63 = PoC spike 兩階段拆分**（對齊 Stage 51 spike Charter 模式 + Stage 53A 拆 Stage 模式）— Charter 通過才 commit PoC 投資。**範圍 7 子項**：① 補完 7 驗證項細節 ② 架構 wire 規劃 ③ v4 code audit 盤點 ④ PoC spike Roadmap 草稿 ⑤ 戰略大重評估後 backlog 大整理 ⑥ Charter spike 揭露議題追蹤 ⑦ v3.51.0 bump。**設計決策 8 條 Christ 全拍板**。**規模 M** / Opus 1M + medium / 預估 ~300-400K。**Charter spike 純文件 deliverable** — 不動 production code / 0 EF Migration / Stage 60+61 既有 11 Mock 場景 + dotnet test 131 passed 0 regression 自動對齊。 |
| v2.0 | 2026-05-11 | **Forge 結案 + Aria 第二段補強**（Aria）— v3.51.0 deployed + 4 deliverable 完整 + Aria gate1 一次數字校對修正後通過。**4 deliverable**（`docs/architecture/v5_charter/`）：① 01_Spike_Plan.md 147 行（7 驗證項六欄結構 + 雙 baseline 對照採納 Aria 補強建議 + 預測項強信心 5/中信心 1/未知 1）② 02_Architecture_Wire.md 273 行（4 層 Hierarchy 落具體 service / DI + per-task session 多 row table schema 候選 + Tool Set Capability attribute+interface hybrid + 9 Worker capability mapping + Lore vs Service mapping）③ 03_v4_Code_Audit.md 280 行（三類 LoC 量化 — 吸收 ~16,061 LoC ~26% / 重寫 ~3,991 LoC + 925 prompt 行 ~7% / 全保留 ~38,700+ LoC ~67% — v4 投資保留 + 重寫 = 73% 對齊「換引擎不換車身」精神）④ 04_Stage_63_PoC_Roadmap_Draft.md 182 行（PoC 6 子項 + 5 向對照 + 規模 L / cost ~600-1000K / 驗收標準）+ README 47 行。**5 Forge spike 自決點 Aria gate1 全通過**（per-task session 多 row table / Petra prompt 全砍 / Tool Set hybrid / wc -l Glob 量化 / docs/architecture/v5_charter/ 新資料夾）。**Aria gate1 揭露 1 點數字 inconsistency**：03 v4 Code Audit 三處吸收 LoC 數字（line 22 ~14,500 / line 98 ~15,061 / 表格 ~16,061）section header 沒同步最終版 → Forge fix commit `9f6091e` 統一兩處到 16,061 ✓ Aria 二檢通過。**範圍變更隱性兩處 Aria 規劃漏掃揭露**（healthy — Forge 補強對齊 Aria gate2）：① Forge 計劃書任務 6+8 含 Future_Feature 大整理 + CHANGELOG entry（對齊 workflow_aria.md 第五節常規 Stage 結案兩段式分工是 Aria 第二段範圍 — Aria plan review 漏揭範圍變更，但對 Christ 看到行為 0 影響接受合一作法）② Forge 計劃書任務沒列 Roadmap v2.0 實作紀錄章節 → Aria 第二段補（本 v2.0 段）。**Aria 規劃預估校準揭露**：Aria 預估吸收 ~6K vs 實際 ~16K（+167% 超預估）— 主因 Workflows 全資料夾 7864 LoC + Meeting legacy 2331 LoC + Pm/* 1415 LoC + Appeal Orchestration 1375 LoC 累積遠超預期 → **同類根因第四次累積**（Stage 56→59 port + Stage 57+58 5051 + Stage 61 prompt 檔名/method 數量/config default + Stage 62 LoC 預估）→ Aria 結案第二段升級 workflow_aria.md 第 7 條延伸範圍段加「LoC 預估必 wc -l 驗證不憑印象」+ memory 自省點 #31。**dotnet build 0 error / dotnet test 131 passed**（純文件 deliverable 0 regression 自動對齊）。**Aria 校準錨待 Christ 補 Forge context 數字**（純文件 deliverable Forge context 預估 300-370K，待 Christ 紀錄補 calibration_anchors 完整錨段）。**Trial_v8 結案戰略結論延續**：Stage 62 Charter spike ✅ → Stage 63 PoC spike feature/v5-poc branch + 5 向對照 + 7 驗證項實證 = v5 動態架構 ROI 拍板實證。commits：`2502741`(規劃) + `c02bd8b`(主實作) + `9f6091e`(數字 inconsistency fix) + Aria 第二段 commit。 |

# Future Feature — 未來功能候選清單

> 版本：v7.99
> 建立日期：2026-04-01
> 最後更新：2026-05-16
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。


---

## 外部檔索引

- **`Future_Feature_v5.5.md`** ⭐ — v5.5 升級規劃 reference（進行中戰略主軸）

---


## 當前優先級 Top 5（2026-05-16 v7.99 — Trial_v13 🟡 部分成功結案後 = v5.5 path 工程實證完整 + 揭關鍵紀律缺口已修 → 待 Stage 68 + Trial_v14 一起跑 → Phase 1 完整收口 → Phase 2 啟動）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **Stage 68 + Trial_v14（合併跑）** ⭐⭐⭐ | Trial_v13 揭紀律修法 `0226c60` 真實生效驗 + Stage 68（FF 六十一補強清單 8+ 點之一 / Phase 2 Step 3 DB 持久記憶 schema 候選）— Christ 2026-05-16 拍板「先記錄繼續實作 Stage 再一起試驗」攤平 Trial cost / 時間 | 🟡 Stage 68 範圍待定 | Trial_v13 揭 3 議題（reload-cache scope 紀律 / workspace cleanup 紀律 / Cody push to main 紀律 — 第 3 條已修法 `0226c60`）+ v5.5 path 工程實證完整（DecideTalentsAsync + 自管 chain talent=/skill= format + Vera inputMsgs=2 真做事 + Cody 6/6 cover + 順修 bug）+ blind spot 0% 維持。Trial_v14 沿用同 prompt + 預期 Cody 不自己 commit / Petra FinalizeGitAsync 真實開 PR / cost $1-5 |
| 2 | **v5.5 Phase 1 完整收口拍板** ⭐⭐⭐ | Trial_v14 ✅ 後 Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = v5.5 Phase 1 完成 | 🟡 待 Trial_v14 結案 | Phase 1 完整收口後正式進入 v5.5 Talent-Skill separation 架構（baseline 6 Talent + 6 Skill 落地 / DB-driven Talent registry / Petra dispatch Talent pool / round-robin schema 預備 horizontal scaling）|
| 3 | **v5.5 Phase 2 Step 3 DB 持久記憶 schema** | Per-task per-agent 持久記憶 hybrid（共用層 + 私有層）+ token budget 60-70% 主動 compact 紀律 | 🟡 待 Phase 1 完整收口 | v5.5 規劃 Phase 2 第一步 — 對齊業界 best practice 避 context drift 65% / 35 分鐘退化雷區 / 跟 Stage 67 Talent-Skill schema 整合 per-Talent 私有層 / 規模 M-L |
| 4 | **v5.5 Phase 2 Step 4-5** | Petra 拆解指令精準度 spike + Prompt DB 化 + Talent identity 整合 | 🟡 待 Step 3 結案 | v5.5 規劃 Phase 2 後續 — 業界沒明確 best practice 要 spike / Talent prompt DB 化解 prompt 改不用 redeploy + 跨 Talent 變體（Cody-1 嚴謹 vs Cody-2 創意）|
| 5 | **v5.5 Phase 3 Step 6-8** | WebUI Talent CRUD + 戰略決策層 3 agent debate + v4 既有 module 砍 vs 留 | 🟡 待 Phase 2 完整收口 | v5.5 規劃 Phase 3 — Phase 1+2 完整收口才開動態 CRUD（避業界 6% Copilot pilot 撐到 production 雷區）+ 戰略決策層多 agent debate 對齊兩層架構 + v4 既有 module 漸進評估砍 / 留 / 重寫 |

> ⚠️ **戰略主軸**：**Stage 67 結案 ✅ — v5.5 升級首發 Phase 1 Step 2 Talent-Skill separation 重構基底完成 / Trial_v13 是 v5.5 Phase 1 完整收口前最後一道閘門**。Stage 67 architectural 升級成果：Skill registry 6 Final Skill code-defined（砍 requirements_extraction 合進 Petra）+ Talent registry DB-driven baseline 6 預設 Talent（Cody 兼 3 skill / Vera/Quinn/Sage 主 1 skill / Victoria/Petra orchestrator）+ ITalentFactory pattern 解時序雷 + Phase 3 dynamic CRUD 副作用 + Petra dispatch 改 Talent pool round-robin schema 預備 horizontal scaling + 7 worker class 不 archive 守 v5 既有 fallback path + Feature flag UseTalentSkillSeparation default false 守 fallback。**Aria 紀律累積成熟度進化曲線**：計劃前 WebSearch 紀律連續 4 次（Stage 64-67）+ Gate1 Tier 升級紀律（架構級重構 Tier 0+1+2+Tier 3 #11 首次實踐）+ 自診 source of truth 紀律根因 14 次累積（Stage 67 一次揭 5 條 + gate1 揭 1 條延伸）+ Forge healthy 偏離 plan 第 N 次驗證（ITalentFactory + FinalizeGitAsync 簽名 + BuildPetraSystemPrompt 加段四段式）+ Aria 校準錨架構級重構新區間 ×0.58 連兩次穩定（Stage 66+67）。**對 v5.5 Phase 2 規劃的指引**：production-ready 補強區間 ×0.78-0.99（6 資料點）+ 架構級重構新區間 ×0.58（2 資料點）兩層分層成立 — Phase 2 Step 3 DB 持久記憶 schema 屬於架構級規模可預期 ×0.58 區間。**戰略終局**：Trial_v13 通過 → Christ 拍板切 default flag → v5.5 Phase 1 完成 → 進 Phase 2 Step 3 DB 持久記憶 schema 設計（Agent 像人類處理事件 — 接到任務後累積思考 / 中間別事打斷不會忘記 / 重啟接著做還記得來龍去脈精神實作）。詳見 [Stage_67_Roadmap.md](Stage_67_Roadmap.md) v2.0 + [CHANGELOG v3.57.0](../../CHANGELOG.md#3570) + [Future_Feature_v5.5.md](Future_Feature_v5.5.md)。

### 2026-05-11 close FF 補充紀錄（Stage 62 Charter spike v5 吸收）

| FF | 原議題 | close 不做原因 |
|---|---|---|
| **五十七** | Petra prompt 5 位置 SoT 維護紀律 | v5 prompt 重寫 — CLAUDE_Petra.md 全砍重寫 + 4 prompt builder method 全砍重寫對齊 Petra orchestrator（v5 動態架構勝過 SoT helper 抽 — 不再有 5 位置漂移風險）|
| **五十八** | 其他 3 申訴 path supersede 評估 | v5 動態架構 Petra Tool Set 動態 review/appeal — 不需固定 supersede helper（Pm/* 4 services 1415 LoC 吸收）|
| **五十九** | Trial 試驗框架 AI Team 認知錯位升級紀律 | v5 Petra prompt 重寫吸收 — CLAUDE_Petra.md 全砍重寫不再含「Stage 61 follow-up」字樣困惑（Trial_v8 揭露根因 = Petra prompt 累積層偏見根除）|
| **六十** | Stage 60 第 7 routing api_failure_retry/abort path silent 卡死 | v4 修法 ROI 負 — v5 動態架構 Petra orchestrator 捕捉 LLM API 失敗動態決策（不依賴固定 routing path）|

---

## Active 主清單

> 以下 5 個 FF 為「進行中 / 仍需做」狀態（v4 戰略主軸 FF 三十六 + 4 個觀察類）。

## 七、客戶專案交付流程與驗收閘門

### 背景

AiTeam 的定位不只是開發自身系統，未來也會替客戶開發專案。目前的流程（merge 後自動部署）對 AiTeam 自身足夠，但對客戶專案的風險層級完全不同：

| | AiTeam 自身開發 | 客戶專案開發 |
|---|---|---|
| **壞掉的代價** | 自己的工具壞了 | 客戶的系統壞了 |
| **git revert** | 可以接受 | 不可接受 |
| **merge 後再測** | OK | ❌ 太晚了 |
| **驗收責任** | 自己 | 對客戶負責 |

目前的流程走完產出一個 GitHub PR，但 merge 之前沒有 Preview 環境可以人工驗收，直接 merge 等於直接上客戶 Production。

### 期望行為

```
需求 → ... → PR 開出
                ↓
         Preview 環境自動部署（Staging）
                ↓
         Victoria 通知 Christ：「PR #42 已部署至 staging，請驗收」
                ↓
         Christ（或客戶）在 Staging 實際操作驗收
                ↓
         驗收通過 → Christ 回覆 OK → Merge → Production 自動部署
         驗收失敗 → Christ 回覆問題描述 → 流程重新進入修正循環
```

### 需要的兩個東西

1. **每個客戶專案都有一個 Staging 環境**（不一定在本機）
2. **AiTeam 流程加入正式的人工驗收閘門**（Victoria 等待 approve 才算 Done）

### 待釐清的子問題

1. **客戶專案的 Staging 環境由誰負責？** — 客戶自己有 staging server？還是 AiTeam 在本機幫每個專案起 container？
2. **AiTeam 是否應該管理「部署到客戶環境」？** — 目前 Maya（Ops）只針對 AiTeam 自身
3. **驗收失敗的循環怎麼設計？** — Christ 的修改意見要怎麼餵回 CEO → 再分派給對應 Agent？

### 初步討論結論（2026-04-12）

**部署到 IIS Web Deploy 的能力：**
- 建議走 **GitHub Actions + Web Deploy** 模式 — 客戶 repo 掛 workflow，push 時自動 `msdeploy` 部署到 IIS
- Agent 不需要直接操作部署，只負責 push code，CI/CD 負責部署（與 AiTeam 自身模式一致）
- 每個客戶專案設定一次 workflow 即可

**Git Flow 多環境部署：**
- 完全可行，需調整 WorkflowEngine 的分支策略
- Cody 的 PR 目標從 `main` 改為 `develop`
- GitHub Actions 依 branch 觸發不同部署目標：feature→開發環境 / develop→測試環境 / master→Production
- Victoria 需理解 Git Flow 各階段，知道「merge 到 develop ≠ 上線」
- 人工驗收閘門：feature 部署到開發環境後，等 approve 才 merge 到 develop

### 與現有 Future Feature 的關係

- **FF 三十八（跨專案能力研究）**：子議題 A（跨 Project repo 支援）是本 FF 前置條件
- **v4 路線**：與 FF 四十九 / 三十六 無關，獨立業務級需求

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

---

## 十二、Sage 全系統文件健康檢查

### 背景

Stage 23 流程重構中，Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」，pipeline 中的工作改為輕量的文件整理 + CHANGELOG 更新。

但長期而言，BugFix / TechImprovement 跳過 Doc 階段，加上程式碼持續演進，系統文件會逐漸與實際程式碼脫節。需要一個機制定期檢查並修補差異。

### 期望行為

Sage 作為獨立定期任務（非 pipeline 內），掃描整個專案：
- 比對程式碼與現有文件的差異（API 變更、新增模組、移除功能）
- 識別過時或缺漏的文件
- 自動補寫或更新，產出健康檢查報告

### 觸發方式

- 定期排程（例如每週 / 每月）
- 或手動指令觸發（Discord / Dashboard）

### 優先級

🔵 低優先級 — Phase 1 先觀察流程文件是否足夠，有實際過時問題再啟動

### v4 兼容性

與 framework / 架構無關，v4 落地後仍可獨立做。

---

## 三十六、AiTeam v5 動態流程架構 — Phase B（FF 四十九 後續）⭐ 進行中

> 狀態：🟡 **進行中 — Phase B Charter spike ✅ Stage 62 完成（v3.51.0，2026-05-11）/ Phase B PoC spike 啟動條件達成待 Christ 拍板 → Stage 63**
> 提出日期：2026-04-28（Trial_v4 結案戰略討論）；2026-05-01 拆分為 Phase B（架構評估獨立於工具評估）；2026-05-02 Phase A 通過解鎖但延後評估；2026-05-10 Christ 拍板路線 D（Trial_v8 結案後 — Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop 真實實證 = v4 hierarchical static 補強 ROI 為負）；2026-05-11 Stage 62 Charter spike 完成 — 4 deliverable + 8 條 Christ 拍板 + 5 Forge spike 自決點 Aria gate1 通過。

### Charter spike deliverable（Stage 62 ✅ 完成 — 詳見 [v5_charter/](../architecture/v5_charter/)）

- **`01_Spike_Plan.md`** — 7 驗證項細節（Victoria Router / Petra 自主調度 / per-task session / Crash Recovery / Mock Gemini Flash / 遷移成本量化 / Hybrid 會議 trigger）+ 預測項（強信心 5 / 中信心 1 / 未知 1）
- **`02_Architecture_Wire.md`** — 4 層 Hierarchy 落具體 service / DI（含 per-task session 多 row table schema 候選 + Tool Set Capability attribute+interface hybrid 候選 + 9 Worker capability mapping）
- **`03_v4_Code_Audit.md`** — 三類分類 + LoC 量化（**吸收 ~16,061 LoC ~26%** / 重寫 ~3,991 LoC + 925 prompt 行 ~7% / 全保留 ~38,700+ LoC ~67%）
- **`04_Stage_63_PoC_Roadmap_Draft.md`** — PoC 6 子項 + 5 向對照 + 規模 L / cost ~600-1000K / 驗收標準

### 拆分說明（2026-05-01）

原 FF 三十六 範圍包含「行業先例研究 + 工具選型 + 動態調度 + per-task session」雙支柱。2026-05-01 Christ + Aria research 後拆為兩個獨立 FF：

- **FF 四十九（Phase A 工具評估）**：是否替換手刻 framework → Microsoft Agent Framework（保留架構）
- **本 FF（Phase B 架構評估）**：是否從固定 pipeline 升級到動態流程（Magentic Orchestration / per-task session）

→ **必須先做 FF 四十九**。Phase A 通過 → Phase B 才有 framework 基礎可動態調度；Phase A 不通過 → Phase B 自動失效。

### 設計拍板（2026-05-01 Christ + Aria brainstorm 後）

Christ 提出 **Capability-based Multi-Agent Architecture** 構想（受 MCP 啟發）：每個 Agent 是「打包好的角色/權責」，PM 動態調度。Aria 對應行業術語：**Anthropic Orchestrator-Workers Pattern + MS Agent Framework Magentic Orchestration + Agent-as-Tool**（業界 v4 共識方向）。

→ Spike Phase B 從原本「**探索動態流程要不要做**」演進為「**驗證可行性 + 細節打磨**」（架構雛形已 80% 成熟）。

#### 4 層 Hierarchy 架構

```
┌──────────────────────────────┐
│ Layer 1: Christ（老闆）       │
└─────────────┬────────────────┘
              ↓ Discord 對話        ↓ Dashboard 操作
┌──────────────────────┐    ┌──────────────────┐
│ Layer 2: Victoria     │    │ Operation Center │
│ (Discord 秘書/Router) │    │ (BossInteraction)│
└─────────┬────────────┘    └────────┬─────────┘
          ↓ 派工                       ↑ Petra escalate
          ↓                            ↑
┌─────────────────────────────────────┴──┐
│ Layer 3: Petra (Orchestrator)           │
│ - 全程動態調度（不照固定 pipeline）       │
│ - per-task session 持久記憶              │
│ - LLM 自主決策（CLAUDE_Petra.md 控制）    │
└──┬──────┬──────┬──────┬──────┬─────────┘
   ↓      ↓      ↓      ↓      ↓
┌────┐ ┌────┐ ┌─────┐ ┌────┐ ┌─────────┐
│Cody│ │Vera│ │Quinn│ │Sage│ │Rosa/Demi│  ← Layer 4: Workers
└────┘ └────┘ └─────┘ └────┘ └─────────┘  (Petra 動態組合，不互相直接呼叫)
```

#### Victoria 角色重新定位（Discord 秘書 / Anthropic Router Pattern）

**新定位**：純 Discord facade + Router + reasoning，**不做業務邏輯**。

| 行為 | Victoria 做嗎？ |
|---|---|
| 跟 Christ 對話、識別問題類型 → 派給對的人 | ✅ |
| Format 答案回 Christ | ✅ |
| 推 Discord 通知（Petra escalate 時）| ✅ |
| 過濾過期 / 誤判 BossInteraction（reasoning）| ✅ |
| codebase 探索 | ❌（Petra 派 Rosa 做）|
| 需求理解 / 提煉 | ❌（Petra 自己做）|
| 業務邏輯判斷 | ❌（Petra orchestrate）|

**Victoria Tool Set**（範例 code 見原始版本，已有完整實作藍圖）

⭐ Victoria **只接觸 Petra**（不直接呼叫 Cody/Vera/Quinn/Sage/Rosa/Demi），Workers 是 Petra 的 pool。

#### Trial_v7 揭露補強：Victoria scan lazy 化具體 ROI 量化（2026-05-10）

Trial_v7 觀察 Victoria CEO 階段 cost **+224% vs Trial_v6 baseline**（$0.0567 → $0.1838 / output tokens 1,400 → 2,807 / 同 1 次成功 + 同 prompt + 同模型），根因是 Victoria proposal 內含「已掃描 codebase 確認受影響範圍 5 個主要元件 + MudBlazor ISnackbar 基礎設施已到位但 Error 路徑尚未接入 toast」這層 codebase scan 結果。

**當前 hierarchical static 架構下 Victoria scan 的三個系統性問題**：
1. **eager scan 浪費 cost** — Victoria 不知道下游真正要哪個面向就先全掃
2. **scan 結果重複** — Kickoff 5 人會議 4 Agent + Petra 又各自 clone repo 重 scan
3. **scan 範圍隨 codebase 線性擴大** — Stage 49-58 codebase 變大後 Victoria cost 持續漲（cost +224% 訊號）

**動態架構（本 FF）天生解這三個問題**：
| 問題 | 動態架構解法 |
|---|---|
| eager scan 浪費 | Lazy scan — 第一個被 Petra orchestrator 派工的 Agent 才 scan |
| scan 重複 | 單一 scan 結果由 orchestrator broadcast |
| 線性擴大 | scan 範圍隨任務需要 lazy 擴大，不被 codebase 大小拖累 |

**ROI 量化維度**（Phase B spike 啟動時 evaluation criteria 之一）：
- **單任務省 cost 估算**：~$0.1-0.3 / 任務（Victoria scan 移除）
- **隨 codebase 持續變大線性擴大** — 長期 ROI 隨 codebase 規模放大
- 對齊「挑戰 1 Victoria 角色 = Discord 秘書 / Router 純 facade」拍板既有方向，但補強具體 ROI 數字而非僅 design 方向

#### 5 個關鍵挑戰拍板（2026-05-01）

| 挑戰 | Christ 拍板 |
|---|---|
| **挑戰 1** Victoria 角色 | **Discord 秘書 / Router**（純 facade，不做業務邏輯）|
| **挑戰 2** 開會頻率 / Petra 決策邊界 | **初期 Petra 自主決策**，觀察後用 prompt iteration 修正（沿用 AiTeam 既有 CLAUDE_*.md 補強流程）|
| **挑戰 3** 老闆介入機制 | **Dashboard 為主介面** + **Victoria 同步推 Discord 通知**（dual-channel 對齊 Stage 28a/28b）|
| **挑戰 4** Mock 模式 | **個別 Worker hardcoded mock + Petra 用 Gemini Flash**（既有 GeminiProvider + AgentConfig DB 動態切換）|
| **挑戰 5** Crash Recovery | **重啟重跑**（不做 Checkpointing）+ 已 responded BossInteraction 算 task input 避免雙重 ask 老闆 |

#### Kickoff / Design 會議模式拍板（Hybrid，2026-05-01）

**Christ 拍板 Hybrid**（會議默認保留，小需求動態跳過）。核心理由：**省去二次返工**（前期投資高品質會議 cost vs 後期重做整段 pipeline cost）—— 對應 Anthropic「85% of quality improvement occurs in first 2 iterations」+ Trial_v4 vs Trial_v5 直接驗證。

**實作對應 MS Agent Framework**：
- 會議 = **Group Chat orchestration**（內建，Petra 為 chair）
- 1-on-1 = **Agent-as-Tool 直接呼叫**（純 MCP-style）
- Petra 看需求動態切換

**Trigger 條件初版**（寫進未來的 CLAUDE_Petra.md）：
- **Kickoff 觸發**（任一滿足）：跨 ≥ 3 元件 / 工期 ≥ 3 天 / 架構決策 / 跨多領域
- **Design 觸發**（任一滿足）：Kickoff 已開 / Issue ≥ 5 / 跨 Phase
- **1-on-1 觸發**：純技術改動 < 50 行 / bug 補丁 / 文件配置

#### 對 Trial_v5 揭露議題的解構

| 議題 | 動態架構下的處理 |
|---|---|
| **A**（MarkGroupDoneOrIntervention 誤判）| ✅ **消失** — 重啟重跑模式不依賴 task status 聚合判斷 |
| **B**（ImplementationNote 寫入路徑斷裂）| ✅ **消失** — Petra orchestrate 時直接看 PR Body / Sage 結果，不依賴特定 DB 欄位 |
| **C/D**（Token / CI/CD ops 議題）| ❌ 與架構無關（FF 四十七 補丁解）|
| **E**（Cody Dev_plan maxTurns）| ⚠️ Petra 看到 maxTurns 失敗 → 動態調整（拆小段重派 / 換 model）|
| **F**（Stage 42 補強單向性）| ⚠️ 部分緩解（CLAUDE_*.md 仍要對齊，但 Petra orchestrate 時可動態檢查）|

#### Phase B Spike 任務（2026-05-01 更新）

**從原本「探索動態流程要不要做」變為「驗證可行性 + 細節打磨」**。7 個驗證項：Victoria Router 模式 / Petra 自主調度行為 / per-task session 跨階段記憶 / Crash Recovery 重跑 / Mock Mode 用 Gemini Flash 跑 Petra / 遷移成本 / Hybrid 會議 trigger 條件（預期省 30-50% cost vs 全會議模式）。

### 啟動觸發條件（Christ 拍板）

✅ **2026-05-10 Christ 拍板路線 D 啟動**（Trial_v8 結案後 — Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop 真實實證 = v4 hierarchical static 補強 ROI 為負 → 對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項）。**兩階段拆分**：Stage 62 Charter spike（純文件 deliverable）→ Stage 63 PoC spike（feature/v5-poc branch + 5 向對照）— Charter 通過才 commit PoC 投資。

### 規模 / 風險

**規模**：**XL**（架構級躍進）— Stage 62 Charter spike M / Stage 63 PoC spike L / Stage 64+ 全量遷移 XL 累積
**風險**：**中-高**（架構級改動風險，但 Charter spike + 80% 既有設計拍板降低 spike 風險）

### 優先級

🟡 **進行中 #1（Top 5）** — Stage 62 Charter spike ✅ 完成 / Stage 63 PoC spike 啟動條件達成待 Christ 拍板。

---

## 五十、Dashboard token 統計頁 IsEstimated 視覺區分（Stage 56 範圍變更 follow-up） ✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— TokenAgentSummaryDto HasEstimated + BOOL_OR + razor MudIcon Warning + Tooltip + cost「~」前綴
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：🔵 低 — Trial_v6 觀察期間 SQL `WHERE "IsEstimated" = true` 可查不阻擋對照，UI 標記延後不影響 cost 對照可信度
> 提出日期：2026-05-05（Stage 56 子項 2 詳化第 7 步範圍變更跳過）

### 背景

Stage 56 FF 四十三 修法新加 `token_logs.IsEstimated` 欄位（區分 `TokenCostEstimator` fallback 估算值 vs CLI/API 真實值）。原計劃書子項 2 詳化第 7 步「Dashboard token 統計頁 cost 加 `~$0.123` estimated 標記 + tooltip」評估後實際範圍超出 1 處 grid cell 上限：

- `TokenMonitoring.razor` **4 處顯示點**（line 64 per-agent / line 101 總計 / line 152 SortLabel / line 159 表格 cell）
- `TokenAgentSummaryDto` 加 `IsEstimated` 聚合欄位
- backend SQL/LINQ GROUP BY `IsEstimated` 改動

對齊計劃書原文「若 razor 改動超出 1 處 grid cell 則留 follow-up FF」拍板跳過。

### 修法方向

- `TokenMonitoring.razor` 4 處顯示點加 estimated 標記（`~$0.123` 前綴 + tooltip「fallback 估算值」）
- `TokenAgentSummaryDto` 加 `IsEstimated` bool 聚合
- backend SQL/LINQ GROUP BY 加 IsEstimated 維度

### 規模 / 風險

**規模**：S（單頁 razor + DTO + SQL，3 layer 改動但範圍可控）
**風險**：極低（純 UI 視覺 + 聚合 layer，不影響 cost 寫入邏輯）

### 優先級

🔵 低 — Trial_v6 觀察期間 SQL 可手查 `WHERE IsEstimated = true`，UI 標記延後不阻擋 cost 對照可信度。觀察期累積真實 estimated row 後再評估視覺呈現需求。

### v4 兼容性

純 UI / DTO 改動，與 framework / 架構無關。

### 注（跨欄位來源差異 — Stage 56 揭露）

`TokenMonitoring` 既有 `EstimatedCostUsd` 欄位是用 `app_settings` 兩 key（`TokenPricing:InputPer1kUsd` / `OutputPer1kUsd`）client-side 估算的，與 `token_logs.TotalCostUsd` 欄位**不同來源**（後者是 Stage 56 修法寫入的，含 CLI 真實值 + Anthropic API path 用 `TokenCostEstimator` per-model 4 欄位估算）。本 FF 修法是「新增 IsEstimated 視覺辨識」非「對齊既有 EstimatedCostUsd 顯示」。

---

## 五十四、Stage 36 後怪物大檔復發 — TaskGroupService / ButtonCallbackRouter / DevAgentService 拆解 ⭐

> 狀態：🟡 子項 1 ✅（Stage 59，2026-05-10）/ 子項 2/3 待 Trial_v7 後評估動工
> 提出日期：2026-05-10（Christ 觀察 Forge Plan context 漸漲根因之一）

### 背景

Stage 34-36 FF 二十系列完成「四怪物大檔拆解」（MeetingService / PmAgentService / TaskGroupService / CommandHandler 全清零，2026-04-22）後，v4 漸進遷移路線（Stage 49-58）一路加 routing / NotifyBoss / TryRoute / dispatch case → 三檔復發超過 `docs/conventions/refactor-sop.md` 警戒線 800-1200 行：

| 檔案 | 行數 | 狀態 |
|---|---|---|
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | **1591 行**（Stage 57 後）| ⚠️ 超 Stage 36 拆解前 baseline 1300+ |
| `src/AiTeam.Bot/Discord/ButtonCallbackRouter.cs` | **1091 行**（Stage 36 後新觀察）| ⚠️ 超警戒線 |
| `src/AiTeam.Bot/Agents/DevAgentService.cs` | **958 行**（Stage 36 後新觀察）| ⚠️ 接近警戒上界 |

### 對 Forge Plan context 影響（Christ 2026-05-10 觀察根因之一）

- 怪物大檔 full read 一次 ~20-32K token（每行 ~20 token）
- partial read 多段 ~10-20K（仍偏高）
- Stage 56→57→58 Forge Plan context 漸漲（140K → 227K → ~270K）核心原因之一

### 修法方向（治本）

對齊 Stage 34-36 FF 二十系列既有拆解 SOP（[refactor-sop.md](../conventions/refactor-sop.md)）：
- TaskGroupService 拆 NotifyBoss helpers / TryRoutePipeline helpers / ProcessBossResponseAsync dispatch / HandleEpicPartialPaused 等職責分檔
- ButtonCallbackRouter 拆 dispatch table / handler 分組
- DevAgentService 拆 Dev / Dev_fix / Dev_plan 等職責分檔

### 配套治標（已立刻生效）

`workflow_aria.md` 第三節 A 計劃書格式硬規則 v1.1（2026-05-10 加）：
- **第 5 條**：計劃書內不寫整段 code 範例（>10 行 code block 一律砍）
- **第 6 條**：大檔 reference 標精準 line + method 簽名（Forge 用 Read offset+limit partial read 而非 full read）

Stage 59+ 立刻生效，預期 Forge Plan context 下降 ~30-40%。

### 規模 / 風險

**規模**：L（三檔拆解 + 對齊 refactor-sop.md SOP + Mock regression 確認既有行為不變）
**風險**：低（拆解類重構 + Stage 34-36 既有 SOP 範本可複用）

### 優先級

🟡 中 — Trial_v7 重跑後評估動工（累積到痛點才拆比預先拆 ROI 高，對齊 Stage 36 拆解節奏）。**Stage 57/58 完成後痛點主要落在 TaskGroupService（1591 行繼續漲，Stage 58 加 NotifyBossAgentApiFailure + TryRoutePipelineAgentApiFailure + dispatch case 預估再 +50-70 行）— TaskGroupService 優先拆**。

### v4 兼容性

純 refactor，不動業務邏輯，不影響 v4 framework 行為。

---

## 五十五、v4 framework 邊角 user actions legacy 遷移 + silent failure → fail-fast 統一 ⭐ ✅

> 狀態：✅ **完成**（Stage 60，v3.49.0，2026-05-10）— Trial_v7 揭露 1 🔴 戰略級新類型議題收口
> 提出日期：2026-05-10（Trial_v7 結案揭露 — 推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設）
> 收口：[Stage 60 Roadmap v2.0](Stage_60_Roadmap.md) + [CHANGELOG v3.49.0](../../CHANGELOG.md#3490) + Aria 校準錨 ×0.80

### 背景（Trial_v7 揭露）

Trial_v6 結案宣稱 v4 framework 9/9 達成 + Stage 57/58 收口 3 🔴 = production-ready。但 Trial_v7 在 Kickoff「需要修改」action 觸發時揭露：

- Bot log: `KickoffMeetingService.ModifyTaskPlan` ← **legacy path**（不是 Stage 50/51 framework）
- Petra subprocess 失敗 → `MeetingCommons：Petra session 執行失敗`
- 但 KickoffMeetingService 沒 fail-fast → `ModifyTaskPlan Petra 回應完成` silent skip → DB UPDATE TaskPlan = "" 空字串（從 6,279 字 → 5 字）
- Stage 58 第 7 routing `agent_api_failure_intervention` **沒 catch** — 因為走 legacy path 不接 framework routing

雙通道對照（Discord vs Dashboard）兩源都走相同 legacy path 同失敗 — source-agnostic root cause = ModifyTaskPlan path 本身。

### 戰略意義

**v4 framework 漸進遷移完整路線「主路徑」9/9 達成 ≠「邊角 user actions」全遷移**：
- 主路徑：proposal_approved → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → done（已遷 framework）
- **邊角 user actions（modify / rerun / pause 等）：仍在 legacy path** ← 未盤點
- silent failure 沒 fail-fast：對照 Trial_v6 議題 #15 Sage silent skip 同類根因 — Stage 58 第 7 routing 設計範圍只 cover Agent API failure 不 cover meeting subprocess failure

### 修法方向

**Stage 60A**：盤點 + 遷移 v4 邊角 user actions legacy paths 到 framework
- grep 找出所有 `ResponseAction` 對應 handler 走 legacy path 的（KickoffMeetingService.ModifyTaskPlan / 其他 modify 路徑）
- 對齊 Stage 50/51 framework Kickoff 既有 pattern 遷移到 framework path

**Stage 60B**：subprocess failure → Stage 58 第 7 routing 統一接管
- MeetingCommons + 各 Service 的 subprocess failure catch 點補 fail-fast
- 對齊 Stage 58 路線 A marker pattern：build `[SUBPROCESS_FAILURE]` summary 前綴 result → call HandleAgentCompletedAsync 走正常 callback flow → Stage Executor marker check → fire `agent_subprocess_failure_intervention` BossInteraction（或重用 `agent_api_failure_intervention` 第 7 routing）

### 規模 / 風險

**規模**：M-L（盤點 + 兩 Stage 拆解）
**風險**：中（Stage 50/51 framework Kickoff 既有 pattern 範本可複用，但邊角 actions 範圍未明確需先 spike 盤點）

### 優先級

🔴 戰略級必修 — Stage 60+ 候選（Stage 59 結案後第一順位）。Trial_v7 揭露 v4 production-ready 邊界仍有缺口，影響面廣（任何走 legacy modify path 的 user action 都同類踩坑）。

### v4 兼容性

本 FF 是 v4 漸進遷移的補強段，對齊 Stage 49-58 路線繼續推進。

---

## 五十六、Petra prompt「議題層次篩選紀律」+「給定見不攤議題」推廣到 AI Agent prompt 層 ✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— CLAUDE_Petra.md +「議題層次紀律 + 給定見紀律 + 工時禁字紀律」段 + 4 prompt builder 共用 AppendPetraDisciplineSection helper
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：🟡 中 — Stage 60+ 候選 / 與 FF 二十五 / 四十八 / 四十六 系統性 prompt 對齊群組合併修
> 提出日期：2026-05-10（Trial_v7 結案揭露 — Christ 親自點破 Petra「給 Christ 決策包」行為）

### 背景（Trial_v7 揭露）

Trial_v7 Kickoff Petra 產出 TaskPlan 內含 5 待拍板議題 + 給 A/B/C 三選讓 Christ 拍。Christ 親自點破：「Petra 她現在就有點像前不久的 Aria，請我拍板很多細節」。

對照 user_christ.md「議題層次篩選紀律」+ workflow_aria.md「給定見不攤議題」精神 — 這兩條是 Aria 之前被 Christ 校正過的工作風格紀律，Petra prompt 同類根因應該同步學。

### 修法方向

對齊既有 Aria 紀律精神，更新 Petra prompt（含 Kickoff Petra / Design Petra / Pipeline Petra）：
- 純技術 / 內部設計議題 → Petra 自決（不丟給 Christ）
- 對 Christ 看到行為 / 業務邏輯 / spec 有影響的議題 → 才丟拍板
- 拍板議題給定見（推薦 A 方案 + 理由），不只列三選

### 規模 / 風險

**規模**：S-M（純 prompt 修改，可與 FF 二十五 / 四十八 / 四十六 系統性 prompt 對齊群組合併修）
**風險**：低（prompt 修改 + Mock 場景驗證即可）

### 優先級

🟡 中 — Stage 60+ 候選。建議與 FF 二十五（Cody 繞道傾向）+ FF 四十八 + FF 四十六 合併成「AI Agent prompt 對齊紀律統一」一個 Stage 處理。

### v4 兼容性

純 prompt 修改不動 framework 路徑，全 v4 兼容。

---

## 五十七、Petra prompt 5 位置 SoT 維護紀律 / 是否抽 prompt template helper（Stage 61 揭露）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：CLAUDE_Petra.md 全砍重寫 + 4 prompt builder method 全砍重寫對齊 Petra orchestrator（v5 動態架構勝過 SoT helper 抽 — 不再有 5 位置漂移風險）
> 提出日期：2026-05-10（Stage 61 結案揭露 — Petra prompt 三處同步實際是五處同步首次踩到）

### 背景

Stage 61 實作 FF 五十六（Petra prompt 議題層次篩選紀律延伸）時，Forge spike 揭露 Petra prompt 真實位置不是 Aria 規劃寫的「三處同步」（CLAUDE_Petra.md + KickoffPrompts.cs + DesignState.cs）而是「**五處同步**」：
- CLAUDE_Petra.md（1 處 markdown）
- KickoffPrompts.BuildPetraRoundPrompt + BuildPetraPlanPrompt（2 method）
- DesignPrompts.BuildDesignPetraRoundPrompt + BuildDesignPetraPlanPrompt（2 method）

Forge 自決路線：直接 inline 紀律段文字到 5 位置 + 兩 prompts class 各自有同名 AppendPetraDisciplineSection helper（純 string 內聚 SoT）+ commit message 標 SoT 維護筆記。

### 5 位置漂移風險

未來其他 Stage 改 Petra 紀律時，必須記得 5 位置同步（CLAUDE_Petra.md + 4 個 AppendPetraDisciplineSection helper）— 修一處漏其他 4 處就 prompt 漂移。

### 修法方向（候選）

**選項 A**：繼續沿用 5 位置 inline + commit message 紀律提醒（當前 Stage 61 實作）
**選項 B**：抽 cross-class helper（PromptDisciplinePartials class 集中管理紀律段文字）
**選項 C**：改 markdown-driven prompt（CLAUDE_Petra.md 為 SoT，prompt builder 從 markdown 載入紀律段）

### 規模 / 風險

**規模**：S-M（選項 B）/ M（選項 C）/ **風險**：低

### 優先級

⚪ candidate standby — 累積到必要時拆 Stage（未來改 Petra 紀律時若漂移踩坑就動工，目前 commit message + Future_Feature SoT 維護筆記 + AppendPetraDisciplineSection helper 內聚 SoT 已覆蓋）

### v4 兼容性

純 prompt 結構，與 framework 無關。

---

## 五十九、Trial 試驗框架 AI Team 認知錯位升級紀律（Trial_v8 揭 🔴）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：CLAUDE_Petra.md 全砍重寫不再含「Stage 61 follow-up」字樣困惑（Trial_v8 揭露根因 = Petra prompt 累積層偏見根除 — Stage 63 PoC 用 5 向對照數據組驗證）
> 提出日期：2026-05-10（Trial_v8 結案揭露 — Trial_v6 議題 #3 升級 🟢 → 🔴）

### 背景

Trial_v8 v3.50.0 跑同任務 prompt（沿用 Trial_v6/v7 對照組精準度最高）— Petra Round 1 直接 escalate「需求定位不清——無法確認這是新 Stage 還是 Stage 61 follow-up」。真實 root cause = **Petra 看到 codebase 已含 Stage 60+61 痕跡**（commits / Roadmap / Petra prompt 紀律段「Stage 61」字樣）→ 認知錯位「這個任務感覺被處理過 / 是 follow-up?」。

對齊 Trial_v6 議題 #3「Trial 框架 AI team 認知錯位」🟢（無實質影響）→ Trial_v8 升級 🔴（直接 escalate 卡流程）。**Trial 試驗框架的設計缺口而非 v4 framework 缺口**。

### 修法方向（候選）

**選項 A**：Trial 任務 prompt 加「Trial 試驗模式」明確標記（如「請忽略 codebase 中既有 Stage 60+61 等試驗痕跡，當作新需求處理」）— 簡單但污染 prompt 對照精準度
**選項 B**：每次 Trial 用獨立 worktree / branch 跑（codebase 不含試驗痕跡）— 對照組真實但運維成本高
**選項 C**：改試驗任務 prompt（每次 Trial 用不同任務避免認知錯位）— 失去四向對照基線
**選項 D**：等戰略大重評估拍板後再評估（如路線 B/C 大砍複雜度則 Trial 模式本身可能改變）

### 規模 / 風險

**規模**：S-M（純 Trial 流程設計 / 不動 production code）/ **風險**：低

### 優先級

⚪ candidate standby — 戰略大重評估拍板路線後決定（如路線 A 才動工）

### v4 兼容性

純 Trial 試驗框架設計，與 v4 framework 無關（不修 production code）。

---

## 六十、Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死收口（Trial_v8 揭 🔴）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v4 修法 ROI 負：v5 動態架構 Petra orchestrator 動態決策 LLM API 失敗（不依賴固定 routing path 的 retry/abort case body — Petra Tool Set 收到 Worker exception 動態評估 retry 拆段 / 換 model / abort）
> 提出日期：2026-05-10（Trial_v8 結案揭露 — Stage 60 第 7 routing 真實首次觸發後 retry path silent 卡死）

### 背景

Trial_v8 揭露 Stage 60 第 7 routing `agent_api_failure_intervention` 三選 actions 中：
- ✅ `api_failure_continue` 已 Mock 驗證（Stage 60 結案紀錄場景 C MockMode auto-approve）
- ❌ `api_failure_retry` Trial_v8 真實首次觸發 = **silent 卡死**（Bot 標 ProcessedByBot=true 但無任何處理 log + token_logs 0 row + task state 不推進）
- ❓ `api_failure_abort` 真實未觸發但對齊推測同類根因 silent skip

對齊 Trial_v6 議題 #15 + Trial_v7 議題 #1 同類「silent failure」根因 — Stage 60 修了 fire interaction + 三選 UI 但 retry/abort path 真實 routing 處理沒做（或 silent skip）。對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證。

### 修法方向（候選）

`PipelineRoutingService.TryRoutePipelineAgentApiFailureAsync` retry/abort case 真實 wire 完整：
- **retry**：重 invoke 原 stage entry（如 KickoffStageBridge re-entry 觸發 Petra resume session 重跑 modify path）
- **abort**：SetIntervention end + 標 task cancelled + fire generic intervention
- 對齊既有 continue case wire pattern（KickoffAgentApiFailureResponse → KickoffStageExecutor.HandleAgentApiFailureResponseAsync continue → state.KickoffDone=true + DesignStageBridge）

**Mock 場景補強**：Stage 60 場景 C 擴 retry / abort 兩 path（auto-approve 切換 — 對齊 Forge 自驗物理限制範疇修根因）。

### 規模 / 風險

**規模**：S（Routing service + Stage Executor case 機械化擴）/ **風險**：低

### 優先級

⚪ candidate standby — 戰略大重評估拍板路線後決定（如路線 A 才動工）

### v4 兼容性

對齊 Stage 60 既有 marker pattern + per-stage Port pattern 延續，全 v4 兼容。

---

## 六十一、v5 PoC → production-ready simplification 補強清單（Stage 63B Aria spot check 揭露 — Stage 64+ 處理）

> 狀態：⚪ candidate standby — Stage 64+ 全量遷移時處理（v5 動態架構 production-ready 補完整 / Trial_v9 真實任務驗證後若採用路線 D 才動工）
> 提出日期：2026-05-12（Stage 63B 結案 Aria spot check 揭露 — Christ question「Context 偏低是否該做沒做」拍板開 FF 追蹤）

### 背景

Stage 63B PoC ✅ Mock 全綠 + 校準錨 ×0.49（vs Aria 預估 mid 750K 偏低 -51%）— Christ question 拍板 spot check 後揭 **30% 隱性 production simplification 我 Aria gate1 沒揭露**。所有都是 Mock 階段足夠 / production 階段需要 — Stage 64+ 全量遷移時補完整。對齊 Stage 60 揭露「Mock 全綠 ≠ production 全綠」自省點 #25 第 N 次精神延伸到「組件單元測試全綠 ≠ 端對端整合測試全綠」新類型。

### 補強清單（4 點）

**1. 🔴 PetraOrchestratorService.StartAsync 端對端 xUnit 漏測**

xUnit 7 test 真實覆蓋 7 個 critical 組件單元（DecideAsync parse / PetraSessionRepository 持久化 / WorkflowSettings default / Worker capability attribute reflection / ClaudeCodeChatClientAdapter dispatch 7 case）— 但**沒測完整 chain**：

> `PetraOrchestratorService.StartAsync → DecideAsync → BuildSequential → InProcessExecution.RunStreamingAsync → Worker.CreateAgent → ChatClientAgent → ClaudeCodeChatClientAdapter → IChatClient → IClaudeCodeService`

= 路線 A 三層 wrapper **端對端跑通** xUnit 漏做 — 「Mock 全綠 153 passed」實際是「組件單元測試全綠 ≠ 端對端整合測試全綠」。**Trial_v9 真實任務跑時才會驗端對端鏈**。

**2. 🟡 ResumeAsync「PoC 簡化」紀錄補強**

`PetraOrchestratorService.ResumeAsync` 註解寫「PoC 簡化：mark 既有 session done + 開新 session」— 不是真實「同 session 繼續」。production 階段需要切回**同 session 繼續**（rebuild context 不開新 session）對齊 5 挑戰拍板 #5「重啟重跑紀律」+ 不破壞 session_messages 連續性。

**3. 🟡 BuildSessionContext fallback hardcode `Agents:Dev:Model`**

`BuildSessionContext` 取 Worker model fallback hardcode 順序：`Agents:Dev:Model` → `Anthropic:DefaultModel` → `"claude-opus-4-6"` — **所有 7 Worker 共用 Dev model**。沒走 per-Worker AgentConfig DB Stage 38 既有 pattern（Cody=Opus / Vera=Sonnet / Quinn=Sonnet / 等）。production 階段需要 per-Worker model from AgentConfig DB。

**4. 🟡 PetraSessionRepository.AppendMessage 同步 method**

既有 BossInteraction Repository 是 async pattern — PetraSessionRepository.AppendMessage / Start 暴露同步 method 而不是 async。production 真實任務跑時可能踩 EF Core tracking 議題（跨 scope DbContext + 並行寫）— Mock 階段 InMemory DB 不踩。

### 修法方向

- 點 1 端對端 xUnit：加 PetraOrchestratorServiceTests.Test8 — stub ILlmProvider 回 capability 序列 + 跑 PetraOrchestratorService.StartAsync + 驗 BuildSequential events 真實 fire + 真實 worker 被 dispatch（透過 stub IClaudeCodeService LastInvokedMethod 多個）
- 點 2 ResumeAsync 改同 session 繼續 — 重 rebuild context 但保留 sessionId / 不寫新 PetraSession row
- 點 3 BuildSessionContext 走 AgentConfig DB query — 對齊 Stage 38 既有 dynamic model resolver pattern
- 點 4 PetraSessionRepository.AppendMessage / Start 改 async — 對齊既有 BossInteractionRepository pattern

### 規模 / 風險

**規模**：M（架構級 production-ready 補強 / 4 點獨立改 + 1 點端對端 xUnit）/ **風險**：低（feature/v5-poc branch + feature flag default=false 雙保險 + Stage 64+ 全量遷移精神對齊）

### 優先級

⚪ candidate standby — **Stage 64+ 全量遷移時處理**（路線 D 採用拍板後才動工 — Trial_v9 結案是關鍵實證）。若 Trial_v9 揭露其他 production simplification 議題 → 本 FF 擴 cover。

### v4 兼容性

完全在 feature/v5-poc branch + v5 動態架構範疇 — 與 v4 hierarchical static 無關。

### Aria gate1 補強紀律候選（候選自省點不立檔）

Stage 63B 結案揭：Aria gate1 commit 檢查只對照 Plan 子項 LoC + dotnet test pass + commit message — 沒 spot check production simplification flag points。**下次同類型 Stage 結案前考慮 gate1 補強紀律「commit 抽 1-2 個 critical service 看實作真實對齊 production-ready vs Mock 階段簡化」**— 但第一次資料點不立自省點，等 Stage 64+ 同類型再驗（healthy 累積成 baseline）。

---

## 五十八、其他 3 申訴 path supersede 評估（Review Appeal / QA Fix / Sage escalate）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：Petra Tool Set 動態 review/appeal（Pm/* 4 services 1415 LoC + Appeal Orchestration 1375 LoC 全納吸收 — 不需固定 supersede helper / SupersedePriorFailedTasks pattern 在 v5 動態架構自然消失）
> 提出日期：2026-05-10（Stage 61 結案揭露 — SupersedePriorFailedTasks 只 cover Dev_plan path 兩處）

### 背景

Stage 61 實作 FF 四十五（Christ action supersede）時 Forge spike 揭露範圍縮小 YAGNI：SupersedePriorFailedTasks helper 只 cover Dev_plan path 兩處（escalate_devplan_skip / abort）— 對齊 Trial_v6 議題 #9 揭露的 Dev_plan path 具體場景。

**其他 3 申訴 path 沒實證同類 cross-Agent supersede 誤判**：
- Review Appeal escalate（Stage 23 Cody-Vera-Petra Appeal loop）
- QA Fix loop escalate（Stage 24/55B QA fix loop limit）
- Sage escalate（Stage 23 Sage 歸檔）

### 修法方向（候選）

待 Trial_v8 真實使用揭露其他 3 申訴 path 是否踩同類問題：
- 觸發：Trial_v8 真實 Christ 點其他申訴 button → 觀察 MarkGroupDoneOrIntervention 是否誤觸 needs_intervention（理應已修根因）
- 若實證踩 → SupersedePriorFailedTasks helper 擴 cover 其他 3 path
- 若實證不踩 → 範圍縮小確認，FF 五十八 close

### 規模 / 風險

**規模**：S（對齊 SupersedePriorFailedTasks helper pattern 機械化擴）/ **風險**：低

### 優先級

⚪ candidate standby — Trial_v8 真實使用揭露才動工（YAGNI 精神）

### v4 兼容性

純 button handler + status 邏輯，與 framework 無關。

---

## 十、Dashboard UI 細節打磨（第四批）

> 狀態：低優先級 — UI 組織與使用便利性優化，待 Christ 確認完整清單後排入 Stage
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static 沒重設計 UI，仍需做）

### 修法方向

待 Christ 確認完整清單（Dashboard 系統設定 / 任務列表 / 流程追蹤 / Agent 設定 / 規則管理等頁面 UX 整體打磨清單）。

### 規模 / 風險

**規模**：M-L（依清單範圍）/ **風險**：低

### 優先級

低 — 業務上不阻塞，UX 優化性質

### v4 兼容性

純 Dashboard UI 改動，與 framework 無關。

---

## 二十二、Agent 命名一致性（守門 + 名稱映射）

> 狀態：中 — Agent 名稱混雜（Cody/Dev/Vera/Reviewer 等），需建統一守門 + 名稱映射
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static Worker pool 命名規則沒改變）

### 修法方向

建立 Agent 名稱映射常數表（`AgentNames.cs`）+ 跨層 (Discord channel / DB AssignedAgent / Workflow executor key) 守門檢查。

### 規模 / 風險

**規模**：S-M（建 const + 跨層守門）/ **風險**：低

### 優先級

中 — Christ 觀察 Dashboard 顯示混淆時優先處理

### v4 兼容性

純 const + 守門，與 framework 無關。

---

## 二十五、Self-implement 試驗 prompt 設計守則（Cody 繞道傾向）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— CLAUDE_Cody.md「Dev_plan 結構規範（強制）」新段（Step 1/2/3 + 改哪些檔案 + Issue 對照表 + 禁止「現況確認」表格）+ ImplementationNote 強制標題
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 進一步揭露 Cody/Petra prompt 對齊系統性議題（×3 確認）
> 重新分類：2026-05-09（從 v4_eval 升 active — Trial_v6 揭露議題加重）

### 背景

Trial_v6 Phase 1/2/3 三個 phase Cody Dev_plan 都 escalate（×3 系統性確認）— Cody 寫「現況確認」表格 vs Petra 期待「實作步驟說明」結構衝突。對齊 Trial_v5 Checkpoint 4 同議題重演。

### 修法方向

- Cody Dev_plan prompt 補強「實作步驟說明」結構規範（Step 1 / Step 2 / 改哪些檔案 / 加哪些 class / DI 註冊等）
- 對齊 Petra prompt 期待結構（CLAUDE_Cody.md vs CLAUDE_Petra.md prompt 對齊 audit）

### 規模 / 風險

**規模**：S-M（純 prompt 改寫 + Trial_v7 重跑驗證）/ **風險**：低

### 優先級

中 — Stage 57+ 候選（與 FF 五十一/五十二/五十三 一起處理 Cody/Petra prompt 對齊群組）

### v4 兼容性

純 prompt 改動，與 framework 無關。

---

## 四十、Stage 46 Dashboard razor UI 接線（epic 折疊 + 進度條 + 暫停按鈕）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— PipelineList row IsEpic 視覺標 + Sub-task icon + EpicPaused chip + PipelineView Epic section sub-task 列表 + ⏸️ 暫停 / ▶️ 恢復 epic 按鈕。場景 7 follow-up fix Stage 46 後端 SubTasks 漏填 自抓自修
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中-高 — Stage 46 FF 三十五自動拆任務的 Dashboard UI 接線未完成
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static UI 沒重設計，仍需做）

### 背景

Stage 46 自動拆任務（FF 三十五）已完成 backend 邏輯（EpicChain + sub-task chain），但 Dashboard razor UI 接線未完成：epic 折疊面板 / sub-task 進度條 / 暫停 epic 按鈕等 UX 缺失。Trial_v6 揭露 parent group 流程追蹤不顯示 sub-task 內部 stage（議題 #5）對應這個 FF。

### 修法方向

- Dashboard 流程追蹤頁加 epic 折疊面板（parent group 顯示 N sub-task 子層）
- sub-task 進度條（顯示 sub-task 內部 stage：Dev_plan/Dev/Reviewer/QA/Doc）
- 暫停整個 epic 按鈕（對應 EpicPaused 邏輯）

### 規模 / 風險

**規模**：M（razor + DTO + SignalR push）/ **風險**：低

### 優先級

中-高 — Trial_v6 觀察期間 Christ 體驗痛點（議題 #5），Stage 57+ 候選

### v4 兼容性

純 Dashboard UI 接線，與 framework 無關。

---

## 四十五、Dashboard 重試/跳過後舊 failed task 沒清理（MarkGroupDoneOrIntervention 誤判）✅（範圍縮小）

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— ButtonCallbackRouter SupersedePriorFailedTasks helper 兩處呼叫 + TaskGroupService.MarkGroupDoneOrInterventionAsync InterventionReason 動態列出真實 escalate source。**範圍縮小 YAGNI**：只 cover Dev_plan path（escalate_devplan_skip / abort）— 其他 3 申訴 path 立 FF 五十八 Trial_v8 後評估 candidate
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 議題 #9 直接踩（generic intervention 訊息「Vera 0 次修復後仍發現問題」誤導，實際是 Sage escalate）
> 重新分類：2026-05-09（從 archived_v4 升 active — v4 hierarchical static 仍依賴 task status 聚合判斷）

### 背景

Trial_v5 + Trial_v6 都觀察：Christ 按「跳過審核」/「重啟 Dev」action 後前置 failed task 沒被自動清除 → `MarkGroupDoneOrIntervention` helper 看到歷史 failed task 仍掛在 group → 標 needs_intervention + 建 generic intervention BossInteraction（訊息誤導實際根因）。

### 修法方向

- Christ action 標記後（跳過審核 / 重啟 Dev）→ 對應 task 標 cancelled / superseded
- `MarkGroupDoneOrIntervention` 邏輯加「忽略已被 superseded 的 failed task」
- generic intervention 訊息模板按真實 escalate source 動態化（對齊 FF 五十三第三 Agent fail-fast 統一精神）

### 規模 / 風險

**規模**：S-M（純 status 邏輯 + 訊息模板）/ **風險**：中（涉及 group 判定邏輯，需 Trial 對照組重驗）

### 優先級

中 — Stage 57+ 候選（跟 FF 五十一/五十二/五十三 一起處理）

### v4 兼容性

純 status 聚合 + 訊息模板，與 framework 無關。

---

## 四十六、ImplementationNote 寫入路徑與 PR Body 對齊（Sage 過嚴 escalate + Cody 實作範本補強）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— Cody Dev/Dev_fix prompt 強制寫 ImplementationNote + DocAgentService prompt 引導 Sage 走 PR Body / git log fallback + CLAUDE_Sage.md 品質下限改 fallback path（兩備援皆失敗才 escalate）
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 議題 #8 直接踩（Cody 跳過 Dev_plan 後沒寫 ImplementationNote → Sage 歸檔失敗）
> 重新分類：2026-05-09（從 archived_v4 升 active — v4 hierarchical static Sage 仍看 ImplementationNote）

### 背景

Trial_v5 PR #170：Cody PR Body 寫了完整實作說明，但 DB `task_groups.ImplementationNote = 0 字` → Sage escalate 誤判 Cody 沒寫實作。Trial_v6 Phase 1 / 3 同議題重演（Cody 跳過 Dev_plan 後沒寫 ImplementationNote → Sage 歸檔失敗）。

### 修法方向

- Cody Dev / Dev_fix prompt 強制寫 ImplementationNote DB 欄位（不論是否走 Dev_plan）
- Sage 歸檔流程加備援 source（PR Body / commit log）— 不只看 ImplementationNote
- 兩條路雙保險

### 規模 / 風險

**規模**：S-M（Cody prompt + Sage 邏輯）/ **風險**：低

### 優先級

中 — Stage 57+ 候選

### v4 兼容性

純 Agent prompt + 歸檔邏輯，與 framework 無關。

---

## 四十八、Cody Dev_plan 階段 maxTurns 配置不足（複雜任務踩 100%）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— Cody Dev_plan maxTurns 從 default **10**（不是 Aria 規劃寫的 40 — 真實值揭露）提升至 80。IClaudeCodeService.RunReadOnlyAsync 加 `int? maxTurns = null` 4 處同步擴 + DevAgentService caller 傳 `maxTurns: 80` + 3 處既有 caller（Designer/Requirements/PmReview）保持 default 10 不影響
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中-高 — Trial_v6 Phase 1/2/3 三個 phase Cody Dev_plan 全 escalate
> 重新分類：2026-05-09（從 v4_eval 升 active — Trial_v6 真實任務驗證觸發）

### 背景

Trial_v5 / Trial_v6 觀察 Cody Dev_plan 在複雜任務踩 maxTurns 100% → escalate dev_plan_unable HITL routing。對應 FF 二十五（Cody/Petra prompt 對齊問題）的子議題 — maxTurns 不夠是 prompt 對齊問題的物理表現。

### 修法方向

- Cody Dev_plan maxTurns 從預設值（推測 40）提升至 80-100
- 或動態化（Dashboard 可調，對齊 FF 十九 Agent maxTurns 動態化精神）
- 跟 FF 二十五一起處理 prompt 對齊根因

### 規模 / 風險

**規模**：S（純 config 改動）/ **風險**：低

### 優先級

中-高 — Trial_v6 系統性議題確認（×3 phase 都踩），Stage 57+ 候選

### v4 兼容性

純 config 動態化，與 framework 無關。



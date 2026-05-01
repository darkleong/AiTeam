# Future Feature — 未來功能候選清單

> 版本：v7.64
> 建立日期：2026-04-01
> 最後更新：2026-05-02
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。
> **2026-05-01 大整理**：以 v4 路線（FF 四十九 工具評估 + FF 三十六 架構評估）為主軸，重新評估 30 個待處理 FF，拆分 5 子檔讓主檔聚焦在 active 主清單。
> **2026-05-02 v7.64**：Stage 47 結案 — FF 四十七 ✅ + FF 十一 ✅（路線 b DB AppSettings 動態化順帶大半解 FF 十一）。

---

## 外部檔索引（2026-05-01 拆檔）

| 子檔 | 內容 | 何時讀 |
|---|---|---|
| `Future_Feature_v4_eval.md` | 9 個 FF 待 v4 spike 後重評估 | spike Phase A 完成後 |
| `Future_Feature_frozen.md` | 3 個冷凍 FF（觸發條件不滿足）| 評估解凍時 |
| `Future_Feature_archived_v4.md` | 11 個已歸檔 FF（v4 吸收 / framework 內建 / Trial 完成）| 查歷史脈絡 |
| `Future_Feature_completed.md` | 已完成項目摘要（FF + Stage 對照）| 查 Stage 完成歷史 |
| `Future_Feature_changelog.md` | 變更紀錄 v1.0 - v7.64 | 追蹤版本演進 |

---

## 當前優先級 Top 5（2026-05-02）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **四十九** | Microsoft Agent Framework 工具評估 | 🟠 中-高 | **戰略主軸** — Stage 48 spike，決定 v4 路線 |
| 2 | **三十六** | v4 動態流程架構 — Phase B | ⚪ 依賴 FF 四十九 | **戰略主軸** — 架構級躍進，Phase B 設計拍板已 80% |
| 3 | **四十三** | token_logs.TotalCostUsd 99.7% NULL | 🟡 中 | Trial_v5 follow-up，影響 Trial 對照組成本評估 |
| 4 | **七** | 客戶專案交付流程與驗收閘門 | 🟡 中 | 業務級需求，與 v4 路線無關 |
| 5 | **四十二** | TryParseDesignIssues 邊界判斷重構 | 🔵 低 | Stage 25b 既有 bug，已 workaround，可搭車或留 v4 後 |

---

## Active 主清單

> 以下 6 個 FF 為「進行中 / 仍需做」狀態，與 v4 路線無關或為 v4 戰略主軸。

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

## 三十六、AiTeam v4 動態流程架構 — Phase B（FF 四十九 後續）

> 狀態：⚪ 待觀察 — 依賴 **FF 四十九（工具評估 Phase A）** 通過 + 結果支持動態流程
> 提出日期：2026-04-28（Trial_v4 結案戰略討論）；2026-05-01 拆分為 Phase B（架構評估獨立於工具評估）

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

**從原本「探索動態流程要不要做」變為「驗證可行性 + 細節打磨」**：

1. 驗證 **Victoria Router 模式**對 Christ 互動的回應品質
2. 驗證 **Petra 自主調度**的行為（會不會亂跳？開會頻率合理？）
3. 驗證 **per-task session** 跨階段記憶力
4. 驗證 **Crash Recovery 重跑機制**
5. 驗證 **Mock Mode 用 Gemini Flash** 跑 Petra 的對齊度
6. 評估**遷移成本**
7. **驗證 Hybrid 會議 trigger 條件**：小需求 / 大需求 / 邊界 case + 頻率分析 + 品質 vs cost 對比，預期省 30-50% cost vs 全會議模式

### 啟動觸發條件（2026-05-01 重寫）

```
階段順序：
1. Stage 49（FF 四十五 + 四十六 補丁，1-2 週）→ ⚠️ 但 FF 四十五/四十六 已歸檔到 archived_v4（v4 吸收）
2. FF 四十九 Phase A spike（MS Agent Framework 工具評估，2-3 週）
3. Phase A 結論：
   - 正向 + 動態流程仍有獨立價值 → 啟動本 FF Phase B spike
   - 正向 + Phase A 已解大部分議題 → 本 FF 永久 ⚪ 待觀察
   - 負向 → 本 FF 自動失效（沒框架基礎做動態）
```

### 規模 / 風險

**規模**：**XL**（架構級躍進）  
**風險**：**高**（架構級改動風險最大，但 Phase B 設計拍板已 80% 成熟降低 spike 風險）

### 優先級

⚪ 待觀察 — **依賴 FF 四十九 Phase A 結論**。

---

## 四十二、TryParseDesignIssues 邊界判斷重構（Stage 25b 既有 bug，FF 三十五 揭露）

> 狀態：🔵 低 — 純 robustness 提升，已有 workaround（Mock prefix 改 `MOCK:` 不含 `[`）
> 提出日期：2026-04-29（Stage 46 驗收期揭露 Stage 25b 起既有 bug）

### 背景

從 Stage 25b 起 `TryParseDesignIssues` 用 `IndexOf('[')` + `LastIndexOf(']')` 抓 array 邊界 — 對任何含 `[` 的前綴文字（如 `[MOCK]`）都會解析失敗。

**為何從未被驗到**：Stage 25b ~ Stage 45 期間既有 1 Issue Mock 也踩這個 bug（解析失敗 → 沒建 GitHub Issue），但因為 1 < 8 規則層本來就不觸發、且**沒下游邏輯依賴 issuesJson** → bug silently 存在。

**直到 Stage 46 拆 task 機制首次依賴 issuesJson 解析正確才揭露**。

### 修法方向

`TryParseDesignIssues` 改用更嚴謹的 JSON balance 邏輯：
- 找對 array `[` 的第一個 token start（不是任何 `[`）
- 逐字 parse 直到匹配 `]` 結尾（counter-based balance）
- 對 `[` 出現在 string literal 內時忽略（quote-aware）

### 規模 / 風險

**規模**：S（單一 method 重構）  
**風險**：低（純 robustness，既有 Mock fix 已 workaround）

### 優先級

🔵 低 — 觀察類 backlog，可搭車或留 v4 路線後

### v4 兼容性

純解析邏輯，與 framework / 架構無關，v4 落地後仍可獨立做。

---

## 四十三、token_logs.TotalCostUsd 欄位寫入覆蓋率不全（FF 三十三 漏 cost 路徑）

> 狀態：🟡 中 — Token 計費觀察缺真實成本資料（已有 effective tokens 涵蓋大部分需求，但成本對照失真）
> 提出日期:：2026-04-29（Trial_v5 開跑前 Token 監控盤點時揭露）

### 背景

Stage 44 FF 三十三完成「Token 計費機制 CLI Agent 涵蓋」並升級 token_logs schema 5 個 nullable 欄位（含 `TotalCostUsd HasPrecision(18,6)`），驗收期 Christ 對 Victoria 一句話對話實證 CEO 那 1 row 寫入完整含 `TotalCostUsd $0.179531`。

但 Trial_v5 開跑前 Token 監控盤點查 token_logs 揭露：

```
SELECT COUNT("TotalCostUsd") AS filled, COUNT(*) AS total
  FROM token_logs WHERE "CreatedAt" >= date_trunc('month', now())
→ filled = 1 / total = 331 → 99.7% NULL
```

### 假設根因（待 Forge 探索確認）

1. **API layer Provider（Anthropic/Gemini）未回傳 cost 細節** — Stage 44 只在 ClaudeCodeService（CLI 路徑）解 `total_cost_usd`，API 層 LlmResponse 可能沒這欄位
2. **CLI 路徑 helper 套用範圍未覆蓋全 16 caller** — 可能某些路徑沒走 TokenLogService 共用 helper

### 影響

- token_logs 99.7% row 缺 cost → Dashboard `/tokens` 頁顯示成本不準（但 token 量準）
- Trial 對照組（Trial_v4 $4.99 vs Trial_v5 ?）需手算估算
- 不影響 token 守門邏輯（守門讀 input + output，非 cost）

### 修法方向

需 Forge 探索後確認：
- 若是 API layer 漏：檢查 Anthropic / Gemini Provider 是否 return cost 欄位
- 若是 CLI 路徑覆蓋率不全：grep 全 ClaudeCodeService caller 確認 ParseJsonOutput 3-tuple 第 3 元素 Usage 是否被丟棄

### 規模 / 風險

**規模**：S-M（探索後可能單檔 fix 或多 caller 串接補洞）  
**風險**：低（純資料完整性，不影響功能）

### 優先級

🟡 中 — Trial_v5 結束後盤點，可獨立 Stage 或搭車 FF 十一

### v4 兼容性

與 framework 無關，v4 落地後仍可獨立做。

---

## 四十九、Microsoft Agent Framework 工具評估（替換手刻 Orchestration framework）

> 狀態：🟠 **中-高** — Trial_v5 6 議題大部分為「手刻 framework 的痛點」，2026-04-03 GA 的 MS Agent Framework 1.0 是時機完美的標準化選項
> 提出日期：2026-05-01（Trial_v5 結束戰略討論 + WebSearch research）

### 背景

Trial_v5 結束後 Christ 提出戰略級議題：「AiTeam 固定流程式架構是否該停損 + 換架構？」Aria research（WebSearch + WebFetch 官方文件）後揭露兩件事：

1. **AiTeam 多年累積的 Orchestration 層是「手刻 framework」** — WorkflowEngine + Crash Recovery + BossInteraction + RunMeetingSession + ClaudeCodeService 流程編排，全部 C# 從零實作
2. **Microsoft Agent Framework 1.0 GA（2026-04-03）剛發布** — Microsoft 把 Semantic Kernel + AutoGen 合併成 single SDK，**.NET first-class support**，把 AiTeam 想做的事情全部標準化

### 工具決定 vs 架構決定（關鍵解構）

兩個獨立決定：
- **(1) 工具選型**：手刻 framework vs MS Agent Framework
- **(2) 架構選型**：固定 pipeline vs 動態流程（FF 三十六 範圍）

→ **本 FF 只處理 (1) 工具選型**，保留 AiTeam 現有架構（固定 pipeline + 部分 conditional），只換底層 framework。**(2) 架構選型留給 FF 三十六**（先做本 FF 再評估）。

比喻：**換引擎，車身保留**。AiTeam 的「車身」（Agent / DB / Discord / Dashboard）不變，只換底層引擎（手刻 framework → MS Agent Framework）。

### 行業先例 research（2026-05-01 WebSearch 結論）

| 框架 | 狀態 | .NET 支援 | 推薦度 |
|---|---|---|---|
| **Microsoft Agent Framework 1.0** | **2026-04-03 GA** | ✅ first-class | ⭐⭐⭐ 首選 |
| LangGraph | 已成熟（Klarna/Replit/Elastic 用）| Python only | ⭐ 次選 backup |
| CrewAI | 持續活躍 | Python only | 🟡 PoC 工具 |
| AutoGen | **maintenance mode**（被 MS Agent Framework 取代）| .NET 有 | ❌ 戰略排除 |
| Anthropic Multi-Agent Patterns | 設計指南 | — | 設計參考（非 framework）|

**Anthropic 研究 hard data**（驗證 AiTeam 設計選擇）：
- Supervisor pattern with explicit routing **outperform implicit by 31%**
- **85% of quality improvement in first 2 iterations**
- 3 iterations 後 lateral changes 不再 improvement

### Microsoft Agent Framework 對應 AiTeam 議題

| Trial_v5 議題 | MS Agent Framework 內建解 |
|---|---|
| **議題 A**（MarkGroupDoneOrIntervention）| ✅ Conditional Edge + Type-safe State 自動處理 |
| **議題 B**（ImplementationNote 路徑斷裂）| ✅ State schema 強制 |
| 議題 C/D（Token / CI/CD）| ❌ 與架構無關（FF 四十七 補丁解）|
| 議題 E（Cody maxTurns）| ❌ 與 framework 無關 |
| 議題 F（Stage 42 補強單向性）| ⚠️ 部分緩解 |

### 內建功能對應 AiTeam 已做 / 規劃中

| MS Agent Framework | 對應 AiTeam |
|---|---|
| Workflow API（Workflow Builder + Executors + Edges）| 取代 WorkflowEngine.cs hardcoded pipeline |
| Checkpointing（superstep-boundary）| 取代手刻 Crash Recovery |
| Pause/Resume | 取代 FF 三十四 |
| Human-in-the-loop（RequestInfoExecutor）| 取代 BossInteraction 機制 |
| Group Chat orchestration | 取代 RunMeetingSessionAsync 多 Agent 會議 |
| Handoff Orchestration | 取代 Petra 路由判斷 |
| Magentic Orchestration | **對應 FF 三十六 動態調度**（內建 pattern）|
| Sub-Workflow | 取代 FF 三十五自動拆任務（內建）|
| Loop with max iteration safety | 取代 ReviewAppeal/QaFix loop |
| Writer-Critic Workflow（內建 sample）| **== AiTeam Cody-Vera-Petra Appeal loop** |

### Hybrid 整合策略（保留 Claude Code CLI 能力）

**問題**：Anthropic provider 在 .NET 上沒內建 file system / shell / web search tools。  
**解法**：用 Custom Agent Executor 包 Claude Code subprocess。

| AiTeam Agent | 整合方式 | 理由 |
|---|---|---|
| Cody / Vera / Quinn / Sage（CLI 路徑）| **Custom Agent Executor + 既有 ClaudeCodeService** | 保留 Claude Code 全套能力 + 既有 C# 投資 |
| Victoria / Petra / Rosa / Demi（API 路徑）| **原生 MS Agent Framework Agent + Anthropic/Gemini provider** | 不需檔案探索，享受 Agent Framework telemetry / middleware |
| Kickoff / Design 多 Agent 會議 | **Group Chat orchestration**（內建）| 取代手刻 RunMeetingSessionAsync |
| Workflow 編排層 | **MS Agent Framework Workflow Builder** | 取代 WorkflowEngine.cs hardcoded pipeline |

### 替換 vs 保留清單

**換掉（底層 framework）**：
- WorkflowEngine.cs（hardcoded if-else pipeline）
- 手刻 Crash Recovery（ActiveOrchestration 欄位 + RecoverStuckMeetings 機制）
- 手刻 BossInteraction 處理流程（InteractionProcessor / InteractionService）
- 手刻 RunMeetingSessionAsync（多 Agent meeting）
- 手刻 ClaudeCodeService 流程編排部分

**保留（功能 / 資產）**：
- Cody/Vera/Quinn/Sage 全部 CLAUDE_*.md prompt
- DB schema（task_groups / tasks / boss_interactions / token_logs）
- Discord 整合（victoria-ceo channel / 各 Agent channel）
- Dashboard UI（流程追蹤 / 操作中心 / Token 監控）
- ClaudeCodeService 本身（subprocess 包裝層）
- Token 計費邏輯（cost 計算 + 記錄）
- Migration 機制 + Bot internal API
- 既有 Stage 1-46 的 production behavior

### Spike Phase A 範圍（本 FF）

**POC**：用 MS Agent Framework 重寫 1 個 AiTeam workflow（建議：CEO 分類 → Kickoff），對照組 vs 現有 C# 實作。

**評估維度**：
1. **開發速度**：手刻 vs framework API 的 LoC 對比
2. **議題自動解**：6 議題裡哪些直接被 framework 解掉（預期 A/B）
3. **學習曲線**：團隊熟悉度（.NET 友好 + Microsoft 文件完整）
4. **整合複雜度**：Custom Agent Executor 包 ClaudeCodeService 的可行性
5. **production 穩定性**：跑 Mock 場景看 Workflow 行為對齊現有 pipeline
6. **遷移成本**：每個 Agent 改寫工時 + 整體 Stage 數估算

**關鍵 sample 參考（POC 階段優先讀）**：
- `dotnet/samples/03-workflows/_StartHere/07_WriterCriticWorkflow`（== AiTeam Appeal loop）
- `dotnet/samples/03-workflows/Agents/GroupChatToolApproval`（== Kickoff/Design 會議）
- `dotnet/samples/03-workflows/HumanInTheLoop`（== BossInteraction）
- `dotnet/samples/03-workflows/Checkpoint`（== Crash Recovery）
- `dotnet/samples/02-agents/AgentWithAnthropic`（Anthropic provider 整合）
- `dotnet/samples/02-agents/ModelContextProtocol`（Claude Code as MCP server 路線）

**預期產出**：
1. spike 報告（推薦：採用 / 不採用 + 理由 + 風險）
2. POC code（1 個小 workflow + Custom Agent Executor 範例）
3. 漸進遷移計劃（若採用，估 Stage 數 + 順序）

### 規模 / 風險 / 時間

**規模**：M（spike Phase A POC）；後續遷移 L-XL（漸進 2-4 月）  
**風險**：中（4/3 GA 但 Microsoft 大力推 + 行業驗證 + 漸進可退）  
**時間**：spike Phase A **2-3 週**

### 啟動條件 / 優先級

🟠 **中-高** — 建議排在 FF 四十七 ops 補丁完成後

啟動順序：
1. FF 四十七 ops 補丁（1-2 週）
2. **Stage 50: spike Phase A**（本 FF，2-3 週）
3. spike 結論驅動：
   - **正向** → Stage 51+ 漸進遷移
   - **負向** → 維持手刻 + 補丁路線
4. spike 正向 → 評估是否啟動 FF 三十六（Phase B 動態流程）

### 與 FF 三十六 的關係

**本 FF 是 FF 三十六 的前置條件**：
- FF 四十九（本 FF）= 工具評估（換 framework，保留架構）
- FF 三十六 = 架構評估（動態流程 + per-task session）

→ **必須先做 FF 四十九**：
- Phase A 通過 → Phase B（FF 三十六）才有 framework 基礎可動態
- Phase A 不通過 → Phase B 自動失效

兩個 FF 串成完整 v4 升級路線圖。

### 參考來源

- [Microsoft Agent Framework Overview | Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [Microsoft Agent Framework Workflows | Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/workflows/)
- [Tools Overview | Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/agents/tools/)
- [GitHub - microsoft/agent-framework](https://github.com/microsoft/agent-framework)
- [Building Effective AI Agents | Anthropic](https://resources.anthropic.com/building-effective-ai-agents)

---

## v4 後重評估 FF 簡表

> 詳細內容見 [`Future_Feature_v4_eval.md`](Future_Feature_v4_eval.md)

| FF | 標題 | 狀態 | v4 重評估點 |
|---|---|---|---|
| 十 | Dashboard UI 細節打磨（第四批）| 🔵 低 | 動態架構下 UI 重新設計 |
| 十四 | Agent I/O 完整記錄 | ⚪ 待討論 | MS Agent Framework 內建 telemetry 涵蓋 |
| 十九 | Agent maxTurns 動態化 | ⚪ 待觀察 | v4 framework maxTurns 機制改變 |
| 二十二 | Agent 命名一致性 | 🔵 低 | 動態架構下 Worker pool 命名規則改變 |
| 二十五 | Self-implement 試驗 prompt 設計守則 | 🟢 經驗紀錄 | Trial_v5 證實 self-implement 適用範圍擴展 |
| 三十八 | 跨專案能力研究 | ⚪ 待深度討論 | v4 影響跨 project 設計 |
| 四十 | Stage 46 Dashboard razor UI 接線 | 🟠 中-高 | 動態架構下 UI 重新設計 |
| 四十八 | Cody Dev_plan 階段 maxTurns 配置不足 | 🟠 中-高 | v4 framework maxTurns 機制改變 |

---

## 冷凍 FF 簡表

> 詳細內容見 [`Future_Feature_frozen.md`](Future_Feature_frozen.md)

| FF | 標題 | 狀態 | 解凍觸發條件 |
|---|---|---|---|
| 二 | Agent 個性與造型 | 🔵 低 | Dashboard 視覺整體穩定（v4 後）|
| 三 | AiTeam 安裝精靈 | 🔵 低 | 系統架構穩定 + 真實「在第二台機器部署」需求 |
| 十三 | UAT 驗收階段 | ⚪ 待觀察 | Trial_v6+ 觀察期間頻繁出現「做出來不是我要的」case（≥ 3 次）|

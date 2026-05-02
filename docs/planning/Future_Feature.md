# Future Feature — 未來功能候選清單

> 版本：v7.68
> 建立日期：2026-04-01
> 最後更新：2026-05-02
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。
> **2026-05-01 大整理**：以 v4 路線（FF 四十九 工具評估 + FF 三十六 架構評估）為主軸，重新評估 30 個待處理 FF，拆分 5 子檔讓主檔聚焦在 active 主清單。
> **2026-05-02 v7.64**：Stage 47 結案 — FF 四十七 ✅ + FF 十一 ✅（路線 b DB AppSettings 動態化順帶大半解 FF 十一）。
> **2026-05-02 v7.65**：Stage 48 spike 採用結論 — FF 四十九 ✅（4 強正向 + 2 中性 + 0 負向，啟動 Stage 49+ 漸進遷移路線「換引擎不換車身」，FF 三十六 Phase B 進入「等遷移過半再評估」）。
> **2026-05-02 v7.66**：Stage 49 ⭐ v4 漸進遷移首發完成（v3.35.0）— Cody-Vera-Petra Appeal loop 切 framework + feature flag 並行雙系統 + 0 follow-up + production fallback 防呆真實生效。6 Stage 遷移 1/6 達成。
> **2026-05-02 v7.67**：Stage 50 v4 漸進遷移第二步完成（v3.36.0）— Kickoff Meeting 切 framework Group Chat（A2 fan-out/fan-in 路線）+ feature flag + 3 follow-up 揭露 framework 1.3.0 fan-out 拓撲首次 production 整合的 streaming dispatch + type validation 兩層額外要求。6 Stage 遷移 **2/6** 達成。
> **2026-05-02 v7.68**：Stage 51 ⭐ v4 漸進遷移第三步完成（v3.37.0）— framework HITL pattern 試點（Kickoff Workflow 中途介入）+ feature flag。**6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過**（含跨 process restart requestId stable 證據鏈，requestId `0daeccaa72714604812add3427ba4d9d` 在 yield emit + Bridge resume + Recovery 跨重啟全程 stable）。6 Stage 遷移 **3/6** 達成。

---

## 外部檔索引（2026-05-01 拆檔）

| 子檔 | 內容 | 何時讀 |
|---|---|---|
| `Future_Feature_v4_eval.md` | 9 個 FF 待 v4 spike 後重評估 | spike Phase A 完成後 |
| `Future_Feature_frozen.md` | 3 個冷凍 FF（觸發條件不滿足）| 評估解凍時 |
| `Future_Feature_archived_v4.md` | 11 個已歸檔 FF（v4 吸收 / framework 內建 / Trial 完成）| 查歷史脈絡 |
| `Future_Feature_completed.md` | 已完成項目摘要（FF + Stage 對照）| 查 Stage 完成歷史 |
| `Future_Feature_changelog.md` | 變更紀錄 v1.0 - v7.66 | 追蹤版本演進 |

---

## 當前優先級 Top 5（2026-05-02 v7.65 — Stage 48 spike 採用後）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **三十六** | v4 動態流程架構 — Phase B | ⚪ Phase A 已通過解鎖，**等 Stage 49+ 漸進遷移過半（Stage 52 後）再評估** | 戰略主軸（pending）— FF 三十六 拍板路線：先做 6 Stage 漸進遷移，遷移過半再評估動態流程是否有獨立價值 |
| 2 | **四十三** | token_logs.TotalCostUsd 99.7% NULL | 🟡 中 | Trial_v5 follow-up，影響 Trial 對照組成本評估 |
| 3 | **七** | 客戶專案交付流程與驗收閘門 | 🟡 中 | 業務級需求，與 v4 路線無關 |
| 4 | **四十二** | TryParseDesignIssues 邊界判斷重構 | 🔵 低 | Stage 25b 既有 bug，已 workaround，可搭車或留 v4 後 |
| 5 | **十二** | Sage 全系統文件健康檢查 | 🔵 低 | Phase 1 觀察期，定期排程 |

> ⚠️ **戰略主軸已轉向**：v4 工具選型完成，剩下是 Stage 49-54 漸進遷移（不開新 FF，按 Stage 走）。`Stage 49 = Cody-Vera-Petra Appeal loop` 為下個工作主軸（不在 Top 5 內，因為 Stage 工作不算 FF backlog）。

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

## 三十六、AiTeam v4 動態流程架構 — Phase B（FF 四十九 後續）

> 狀態：⚪ 待觀察 — **FF 四十九 Phase A 已通過（採用結論，Stage 48 完成 2026-05-02）**，Phase B 啟動條件解除但 Christ 拍板「Stage 49+ 漸進遷移過半（Stage 52 後）再評估」是否有獨立價值
> 提出日期：2026-04-28（Trial_v4 結案戰略討論）；2026-05-01 拆分為 Phase B（架構評估獨立於工具評估）；2026-05-02 Phase A 通過解鎖但延後評估

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

**從原本「探索動態流程要不要做」變為「驗證可行性 + 細節打磨」**。7 個驗證項：Victoria Router 模式 / Petra 自主調度行為 / per-task session 跨階段記憶 / Crash Recovery 重跑 / Mock Mode 用 Gemini Flash 跑 Petra / 遷移成本 / Hybrid 會議 trigger 條件（預期省 30-50% cost vs 全會議模式）。

### 啟動觸發條件（Christ 拍板）

⏰ **Stage 52 完成後**（v4 漸進遷移過半）評估：framework Magentic Orchestration 是否已解大部分動態調度需求？若已解 → 永久 ⚪ 待觀察；若仍有獨立價值（per-task session 持久記憶 / Capability-based 調度）→ 啟動 Phase B spike。**不在 Stage 49-51 期間啟動**（避免架構級變動疊加風險）。Stage 49-54 路線詳見 CHANGELOG / Spike v1 報告節 7。

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

# 服務層大檔案拆解守則（Refactor SOP）

> 本守則由 Stage 34-36（FF 二十）三次實踐累積而成，適用於 Service / Handler 類檔案超過 1000 行、或讀一次吃 > 20K tokens 時的拆解工作。

---

## 何時觸發拆解

任一條件成立就考慮：

- **單檔超過 1000 行**（Read 一次 ≈ 20K tokens）
- **單一類別職責 ≥ 5 種**（混合 Kickoff、Appeal、QA、Proposal 等多領域）
- **未來 Stage 需要頻繁動到**（每次動都要 Read 大檔，累積成本高）
- **實作 Session 抱怨「又要讀整個大檔」連續 2-3 次**

---

## 六項拆解 SOP

### SOP 1：Record / Type 組織

- **Public record / class**（API 合約、caller 會直接用）→ 搬**獨立檔**（例：`MeetingResults.cs`、`PmAgentResults.cs`）
  - 理由：避免引入不必要的大型 namespace 依賴、修改 record 時不用翻找 service 大檔
- **Internal DTO**（JSON parse、實作細節）→ **留各自 service 檔案**
  - 理由：防止跨 service 型別洩漏污染

### SOP 2：Migration 策略

**依 caller 數量決定**：

| Caller 數量 | 策略 |
|---|---|
| **< 15 處**（含 DI 註冊）| **直接切換，不做 thin wrapper** |
| **15-30 處** | 考慮 thin wrapper 過渡，分批遷移 |
| **> 30 處** | 必做 thin wrapper，避免一次改動過大 |

**直接切換的理由**：
- Wrapper 讓兩套名稱並存，未來維護混淆
- Git history 本身是最好的追蹤，直接刪除舊檔無妨
- 減少過渡期的認知負擔

**搬家後注意**：如果有多個子 service 都承接邏輯，**檢查是否有新的超大檔誕生**（例如 Stage 36 CommandHandler 拆後，ButtonCallbackRouter 變 1091 行新怪物）。搬家不等於拆解。

### SOP 3：Commons service 範圍界定

**只放**符合以下所有條件的：
- 多個新 service 都會呼叫
- 呼叫方式完全相同（不是「邏輯相似各自實作」）
- 放進 Commons 不會新增額外依賴（或新增的依賴是所有 caller 本來就需要的）

**不要放進 Commons**：
- 「邏輯相似但各自 private」的 helper（例：`GetApiKey` / `GetModel` 這類 3-5 行的讀 config helper，各自保留即可）
- Prompt builders（各 service 各自 prompt 風格，不共用）
- Internal DTOs（實作細節，跨 service 共用沒意義）

**推廣原則**：
> **Commons 推進的新依賴 vs 保留 3-5 行重複碼** — 保留重複碼比較便宜。

### SOP 4：DI 註冊順序

- **Commons / Store 先註冊**（即使 DI 是 lazy resolved，順序代表依賴方向，讓讀 Program.cs 的人一眼看出基礎層）
- **全部 Singleton**（除非有明確 per-request state 才用 Scoped，對齊既有模式）
- **循環依賴單向檢查**：子 Service → Commons（單向），不可反向

### SOP 5：Session state 管理

- **無 singleton-level state**（只有 local 變數）→ 各方法自管，Commons 不需要 state dictionary
- **有共享 state**（如 `_pendingConfirmations` dictionary）→ 抽成獨立 Singleton **Store**，跨 service 依賴此 Store 而非互相 reference
- **升級 thread-safety**：抽成 Store 後跨 thread 呼叫機會增加（不再侷限於 Discord event loop 單執行緒）→ **用 `ConcurrentDictionary`**（Stage 36 踩過這個坑，原本 `Dictionary` 在抽 Store 後需要升級）

### SOP 6：檔案夾組織（超過 10 檔就建子資料夾）

**觸發條件**：單一資料夾（如 `Orchestration/` 或 `Agents/`）檔案數 ≥ 10，或 3+ 個同主題 service

**歸屬判斷原則**（Stage 35 實踐結論）：

> **決策主體（誰說話）= Agent 角色時放 `Agents/`；協調多個 Agent 的流程控制放 `Orchestration/`**

**範例**：
- `Agents/Pm/PmReviewService.cs` — Petra 角色在審，歸 Agents
- `Orchestration/Meeting/MeetingOrchestrationService.cs` — 協調多個 Agent 開會，歸 Orchestration

**搬家成本**：
- namespace 從 `AiTeam.Bot.Orchestration` → `AiTeam.Bot.Orchestration.Meeting`
- caller 的 `using` 要補加 `using AiTeam.Bot.Orchestration.Meeting;`（兩個 namespace 可並存，原 using 不用刪）

---

## Stage 34-36 實戰數據（參考）

| Stage | 拆解目標 | 原行數 | 拆解後 | 縮減 | Model | Context 消耗 |
|---|---|---|---|---|---|---|
| 34 | MeetingService | 1415 | 4 檔共 997 | —（拆完合計更多是正常）| Sonnet 200K + high | 160K / 200K = 80% |
| 35 | PmAgentService + Agents/Pm/ 子資料夾首次實踐 | 1389 | 6 檔共 1444 | — | Opus 1M + high | 261K / 1M = 26% |
| 36 | TaskGroupService + CommandHandler 合併 | 4795 合計 | TGS 716（-73%）+ CH 556（-74%）+ 11 新檔 | 主檔大幅瘦身 | Opus 1M + high | 360K / 1M = 36% |

**觀察**：
- **「SOP 累積後同類工作越做越省」** — Stage 36 規模最大但 context 倍率（×1.49）反而比 Stage 34（×1.60）低
- **Opus 1M 是大型拆解的舒適區**，Sonnet 200K 邊界緊
- **拆完行數總合可能變多**（Stage 34 997 vs 原 1415 只減 30%，是因為新 service 各自有 namespace / using / class declaration 的 boilerplate）——這是正常的，目的是**降低單檔 Read 成本**不是減總碼量

---

## 結案必做清單

拆解 Stage 結案時，實作 Session 在 Roadmap 實作紀錄要寫下：

1. **實際產出檔案 + 行數**（vs 規劃預估）
2. **SOP 套用對照**（本次每一項 SOP 怎麼實踐）
3. **踩坑記錄**（特別記錄 SOP 中沒涵蓋的新發現，供未來擴充此份文件）
4. **驗收情境清單 + 零 follow-up commits 狀態**
5. **Context 消耗實測**（供 Aria 校準公式）

---

## 不適合套用本 SOP 的情況

- **UI 元件層級拆解**（Razor 元件各自生命週期、state 管理不同）—— 有獨立規則
- **Entity / DTO 層級重構**（通常是補欄位、改命名，不是職責拆分）
- **跨 Bot / Dashboard 的跨層 refactor**（需要 Spike，本 SOP 不適用）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-04-22 | 初版，由 Stage 34-36（FF 二十合集）累積而成 |

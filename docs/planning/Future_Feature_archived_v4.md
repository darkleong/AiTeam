# Future Feature — v4 動態架構吸收 / Framework 內建 / Trial 完成

> 從 `Future_Feature.md` 拆出（2026-05-01）
> 作用：保留歷史脈絡 — 受 v4 路線影響不再需要獨立做的 FF，避免 git diff 噪音

**歸檔分類**：
- **A. v4 動態架構吸收**：MS Agent Framework + 動態調度 + per-task session + 重啟重跑模式下，這些議題自動消失（7 個）
- **C. Framework 內建**：FF 四十九 內建涵蓋（2 個）
- **F. Trial 歷程完成**：Trial_v2/v3/v4/v5 已完成驗證任務（2 個）

---

# A. v4 動態架構吸收（7 個）

## 五、CEO 長期記憶升級（向量搜索版）

> 歸檔理由：v4 動態架構下 Victoria 變 Discord 秘書 / Router，不負責業務邏輯，沒「長期記憶」需求；per-task session 屬 Petra 而非 Victoria

### 背景

Stage 15 的長期記憶採用簡易版（DB 表 + 全量載入 prompt），在記憶量少時（< 100 筆）足夠使用。但隨著使用時間增長，記憶量會超過 prompt 容量限制。

### 簡易版 vs 向量搜索版

| | 簡易版（Stage 15） | 向量搜索版（本項目） |
|---|---|---|
| **存** | Victoria 提示詞驅動，自行判斷 | 每次對話結束自動摘要 + 存 |
| **找** | 全部載入 prompt，LLM 自己看 | 用 Embedding 語意搜索，只撈相關的 5~10 筆 |
| **Prompt 大小** | 隨記憶量線性增長 | 穩定（只載入相關記憶） |
| **基礎設施** | PostgreSQL 純文字表 | pgvector 擴充 + Embedding API（Anthropic / OpenAI） |
| **上限** | ~100 筆（10,000 tokens） | 數千筆 |

### 實作方向（如果未來解凍）

1. PostgreSQL 啟用 `pgvector` 擴充
2. `CeoMemory` Entity 新增 `Embedding` 欄位（`vector(1536)` 或對應維度）
3. 記憶寫入時，呼叫 Embedding API 產生向量並存入

### v4 動態架構下的處置

- **Victoria** 變 Discord facade，沒「會議記憶」需求 — 改用即時 query system tools 取代
- **Petra** 才需要 per-task session（FF 三十六 Phase B 實作）— 但用 CLI session resume 不是向量搜索

→ 本 FF 在 v4 架構下無對應角色，**歸檔不再規劃**。如未來真有需求，重新立 FF。

---

## 二十三、Orchestration 異常退出的復原機制（Stage 31/37 Crash Recovery 盲點）

> 歸檔理由：v4 動態架構採「重啟重跑」模式（FF 三十六 Phase B 拍板），不依賴 ActiveOrchestration flag；MS Agent Framework Checkpointing 內建涵蓋

### 背景

Stage 31 實作會議 Crash Recovery（v3.18.0）、Stage 37-2 升級為全編排流程 Crash Recovery（五種 `ActiveOrchestration` 值）。**設計前提**：crash = 進程被 kill → finally 沒機會跑 → flag 留在非 null → 下次啟動掃到。

### 盲點

Stage 37-2 驗收當日踩到：`DesignMeetingService.RunDesignAdjustmentAsync` 因 Petra JSON 漏填 `AdjustmentTargets` 拋 `ArgumentNullException`：
- Exception 沿 call stack 往上傳
- Design 的 try-finally **正常執行 finally** → `ActiveOrchestration` 被清回 `null` ✅
- 但 exception 也讓整場會議失敗、group 卡在 `Status=running / DesignPlan=null`
- **Crash Recovery 掃不到**（flag 已清），group 永遠卡住

即「crash recovery 只防 crash，不防邏輯 exception」。

### v4 動態架構下的處置

FF 三十六 Phase B 拍板「**重啟重跑**」（不做 Checkpointing）+ 已 responded BossInteraction 算 task input 避免雙重 ask：

```csharp
// Petra 重跑時的 prompt 自動帶入：
"""
任務需求：[原始 prompt]

已有老闆回應紀錄：
- 2026-05-01 14:30 [需要老闆決策 X] → Christ approve
"""
```

→ 重跑模式繞過 ActiveOrchestration flag 整類問題，本 FF 自動消失。

### 原方向（保留供 spike 階段 reference）

三個方向：A. 異常不清 flag / B. Dashboard 手動「重啟會議」按鈕 / C. 失敗狀態欄位 + 自動重啟計數

---

## 三十、tech_improvement 工作流的 ghost Dev task

> 歸檔理由：v4 動態架構下沒有固定 pipeline，沒有 ghost Dev task 概念

### 背景

Trial_v3 觀察：tech_improvement 流程的任務列表會出現一筆 **ghost Dev task** 永遠 stuck 在「等待中」：

| 時間 | 事件 | TaskItem |
|------|------|---------|
| t+0 | Christ 按 Agent 執行確認的「執行」 | 建 **Dev (等待中, Dashboard 觸發)** ← orphan |
| t+2min | Orchestrator 啟動 tech_improvement 流程 | 另起 **Dev_plan (執行中, Orchestrator)** |

### 根因（推測）

CEO 確認 → `ShowDirectAgentConfirm` 建立初始 Dev TaskItem，但 tech_improvement workflow 的 Orchestrator **沒使用這筆 task**，另起爐灶建 Dev_plan + 後來的 Dev。

### v4 動態架構下的處置

動態架構下 Petra orchestrate 不照固定 pipeline，沒有「先建 task 再 orchestrator 另起爐灶」這種設計矛盾 → 本 FF 自動消失。

---

## 四十一、Stage 46 Sequential 鏈精修（race condition + Status sync）

> 歸檔理由：v4 動態架構下沒有「Sequential 鏈」概念（Petra Magentic Orchestration 動態調度）

### 背景

Stage 46 驗收期觀察 2 個機制細節：

#### 子項 A：Race condition — pause-epic 與 sub-task done 觸發 TriggerNextPhase 時序競爭
- pause-epic 在 sub-task 即將 done 時呼叫 → `TriggerNextPhaseIfSubTaskAsync` 已先讀 EpicPaused（仍 false） → fire 下個 Phase

#### 子項 B：sub-task TaskGroup.Status 與內部 TaskItems 進度脫鉤
- `BuildEpicSubTasksAsync` 建 sub-task 時 Status="pending"；`FireStepsAsync` 後內部 TaskItems 開始跑但 sub-task 自身 group.Status 仍 pending

### v4 動態架構下的處置

動態架構下 Petra 不用「Sequential 鏈」固定模式（用 MS Agent Framework Sub-Workflow 內建機制），race condition 整類問題消失 → 本 FF 歸檔。

---

## 四十四、TokenTrackingProvider 守門 estimatedTokens 設計缺陷（input-only vs 累計含 output）

> 歸檔理由：v4 動態架構下守門邏輯重設計（FF 四十七 Token SoT 統一 + MS Agent Framework 自帶 telemetry）

### 背景

`TokenTrackingProvider.cs:37` 的守門邏輯：

```csharp
long estimatedTokens = (systemPrompt.Length + userMessage.Length) / 4;  // 只算 input
if (monthlyUsed + estimatedTokens > monthlyLimit) → throw
```

但 `monthlyUsed` 累計值含 input + output。導致 Reviewer 月限 300K 配置下實際累計到 411K（137% 超標）才被擋。

### v4 動態架構下的處置

- FF 四十七 Token SoT 統一會重設計守門邏輯（appsettings vs docker-compose env 對齊 + DB 動態化）
- MS Agent Framework 自帶 telemetry / 計費機制可能取代手刻 TokenTrackingProvider
- 本 FF 在 v4 路線下自動失效

---

## 四十五、Dashboard 重試/跳過後舊 failed task 沒清理 — MarkGroupDoneOrIntervention 誤判

> 歸檔理由：FF 四十九 已寫明「議題 A 在動態架構下消失」（重啟重跑模式不依賴 task status 聚合判斷）

### 背景

Trial_v5 觀察：Christ 按「跳過審核」/「重啟 Dev」action 後，前置 failed task 沒被自動清除：
- 21:30:29 PM (Petra Dev_plan escalate) → failed → Christ 21:52 跳過審核 → row 仍 failed
- 21:52:34 Dev (Token 守門擋) → failed → Christ 22:11 重啟 Dev → row 仍 failed
- 22:11:03 Dev (重啟後) → done ✅

22:33 流程末端 `MarkGroupDoneOrIntervention` helper 看到歷史 failed task 仍掛在 group → 標 needs_intervention + 建 intervention BossInteraction「Vera 在 0 次修復後仍發現問題」（**訊息嚴重誤導：實際 Vera approve**）

### v4 動態架構下的處置

FF 三十六 Phase B 拍板「重啟重跑」模式：
- 不依賴 task status 聚合判斷
- BossInteraction 紀錄算 Petra 重跑時的 input
- Petra 看到「已 approve 路徑」直接走，不會誤觸發 intervention

→ 本 FF 整類 bug 自動消失。

---

## 四十六、ImplementationNote 寫入路徑與 PR Body 對齊（Sage 過嚴 escalate 修法 + Cody 實作範本補強）

> 歸檔理由：FF 四十九 已寫明「議題 B 在動態架構下消失」（Petra orchestrate 時直接看 PR Body / Sage 結果，不依賴特定 DB 欄位）

### 背景

Trial_v5 PR #170：
- Cody **PR Body 寫了完整實作說明**（變更摘要 + Closes #159-#169 列表 + 詳盡 commit messages）
- 但 **DB `task_groups.ImplementationNote` = 0 字**
- Sage 看 ImplementationNote 為空 → escalate（FF 三十二子項 F 觸發） → **誤判 Cody 沒寫實作**

### v4 動態架構下的處置

Petra orchestrate 時動態決定 Sage 看什麼資料源（PR Body / DB / commit log），不依賴特定 DB 欄位 → 本 FF 路徑斷裂問題自動消失。

子項 B（Stage 42 補強單向性）部分緩解（CLAUDE_*.md 對齊問題仍要 prompt iteration），但動態架構下 Petra 可動態檢查避免下游放大。

---

# C. Framework 內建（2 個）

## 四、多 LLM 供應商支援（Gemini / OpenAI + Per-Agent 獨立設定）

> 歸檔理由：MS Agent Framework 支援多 provider（Anthropic / Gemini / OpenAI / Foundry / Ollama），FF 四十九 涵蓋全部 provider 抽象

### 已完成成果（2026-04-25 為止，Stage 37/38）

- ✅ **第一階段：API 層 GeminiProvider**（Stage 37-1，v3.24.0）— 詳見 [Stage_37_Roadmap.md](Stage_37_Roadmap.md)
- ✅ **第二階段 2-A：Dashboard Provider/Model 動態化**（Stage 38，v3.25.0）— DB 為唯一 SoT + `AgentConfigCache` Singleton（TTL 5min）+ `LlmModels.cs` 常數白名單 + Internal API `scope=agent-config` cache invalidate；建立「PR #107 三條禁止路線」作為未來 self-implement 紅線

### 原計劃 2-B（CLI 層多家共存）

`GeminiCliService : IClaudeCodeService` — 評估 Gemini CLI 的 session 延續、工具調用、輸出格式是否能對齊。

### v4 動態架構下的處置

MS Agent Framework Custom Agent Executor 模式直接支援包 Claude Code subprocess（FF 四十九 Hybrid 整合策略涵蓋），CLI 層多家共存的 GeminiCliService / Codex CLI 整合可在 framework 上做，**不需要本 FF 獨立 spike**。

→ FF 四十九 Phase A spike 完成後，2-B 自動被涵蓋，本 FF 歸檔。

### CLI 三家能力研究（保留供未來 reference）

| 能力 | Claude Code | Gemini CLI | Codex CLI |
|---|---|---|---|
| **Session resume** | `--session-id UUID` + `--resume` | `--resume <UUID>` | `codex exec resume <SESSION_ID>` |
| **預先指定 UUID** | ✅ | ❌ FR #20847 open | 不確定 |
| **Stream JSON** | ✅ stream-json | ✅ NDJSON | ✅ JSONL |
| **Structured Output Schema** | prompt 約束 | prompt 約束 | ✅ `--output-schema` native |
| **官方 SDK** | — | — | ✅ TypeScript SDK |
| **記憶檔** | `CLAUDE.md` | `GEMINI.md` | `AGENTS.md` |

---

## 二十六、Model 清單 DB 化 + Dashboard 管理頁

> 歸檔理由：MS Agent Framework provider 抽象化內建（NuGet 加 package 即支援新 provider），不需要自建 LlmModel entity + Dashboard 管理頁

### 背景

Stage 38（v3.25.0）做完 Agent 的 `Provider` / `Model` 動態化後，Dashboard Model 下拉清單的資料源是 `src/AiTeam.Shared/Constants/LlmModels.cs`（hard-coded 常數檔）。維護流程：Aria WebFetch 確認 → 改 LlmModels.cs commit → CI/CD 自動 build + restart（5-10 分鐘生效）。

### 升級觸發條件（已記錄）

- ⏰ **2026-06-17 Gemini 2.5 deprecating** — 第一個已知實際 trigger
- Christ 在 1-2 個月內超過 5 次「想立刻試新 Model」抱怨
- 出現「多個 Model A/B 對比實驗」場景
- 新增 Provider 後 model 清單膨脹超過 50 項

### v4 動態架構下的處置

MS Agent Framework：
- Provider 抽象化內建（`IChatClient` 介面 + 各 provider 實作）
- Model 切換是 framework API call（不需要自建 entity）
- 新 Model 上線時 Aria 透過設定（appsettings / DB AppSettings）切換

→ FF 四十九 Phase A 涵蓋 provider/model 抽象化，本 FF 自動歸檔。

---

# F. Trial 歷程完成（2 個）

## 十六、Dashboard 錯誤處理與提示 UX 打磨

> 歸檔理由：Trial_v4（PR #122 OPEN 不合併）+ Trial_v5（PR #170 OPEN 不合併）已用試驗替代；本 FF 任務需求被 Trial 試驗系列消化

### 背景

Stage 29-5 實作快速下達指令卡時遇到兩個 UX 觀察點：

#### A. MudBlazor 元件內部例外的接住機制
**現況**：`MudFileUpload.MaximumFileCount` 超量時會從元件內部拋例外，Blazor circuit 只能印到 log。

#### B. 錯誤訊息同時顯示 MudAlert + Snackbar
**現況**：快速下達指令卡的違規提示只顯示在送出區上方的 `MudAlert`，使用者若視線在其他地方會錯過。

### Trial 試驗系列消化過程

- **Trial_v4**（2026-04-27）：Cody 跑 FF 十六任務，PR #122 OPEN 未合併，揭露 13 bug
- **Trial_v5**（2026-04-30）：Cody 重跑同 prompt，PR #170 OPEN 未合併，11/12 Issue 完成 + 4 FF 補強驗證

兩次 Trial PR 都不合併（Trial 性質），但**任務需求本身已透過試驗驗證**。

### v4 動態架構下的處置

任務需求已完成試驗（多次 Cody 跑過），實際 production 是否要實作 Dashboard toast 雙通道機制由 Christ 拍板（不需獨立 FF tracking）。

→ 詳見 [docs/experiments/Trial_v4_DashboardErrorUx.md](../experiments/Trial_v4_DashboardErrorUx.md) + [docs/experiments/Trial_v5_DashboardErrorUx_Retest.md](../experiments/Trial_v5_DashboardErrorUx_Retest.md)

---

## 二十七、Self-implement 試驗 v2（FF 二十四 fix 後重新評估品質）

> 歸檔理由：Trial_v2/v3/v4/v5 全執行完成，本 FF 規劃任務全部完成

### Trial 系列執行完成

| Trial | 日期 | 任務 | 紀錄 |
|---|---|---|---|
| **v2** | 2026-04-25 | 規則管理頁面 UI 微調（PR #108）| [docs/experiments/Trial_v2_RuleManagementUI.md](../experiments/Trial_v2_RuleManagementUI.md) |
| **v3** | 2026-04-25 | 流程追蹤頁面 PR 欄位優化（PR #109）| [docs/experiments/Trial_v3_PipelinePrColumn.md](../experiments/Trial_v3_PipelinePrColumn.md) |
| **v4** | 2026-04-27 | Dashboard 錯誤處理 UX 打磨（PR #122 OPEN 未合併）| [docs/experiments/Trial_v4_DashboardErrorUx.md](../experiments/Trial_v4_DashboardErrorUx.md) |
| **v5** | 2026-04-30 | Dashboard 錯誤處理 UX 補齊（PR #170 OPEN 不合併，Trial_v4 對照組）| [docs/experiments/Trial_v5_DashboardErrorUx_Retest.md](../experiments/Trial_v5_DashboardErrorUx_Retest.md) |

### 戰略結論

- **Top 1（FF 二十四 CLAUDE_*.md COPY 漏）**：✅ 修復後 Trial_v3 / v4 / v5 部分驗證
- **Top 2（Vera 偏好放行 + Petra Warning 寬鬆）**：✅ Stage 39 + Stage 40 + Stage 42 連續補強，Trial_v5 證實判斷品質達 senior reviewer 水準
- **Top 3（Quinn 測試品質）**：✅ Stage 41 + Stage 39 補強，Trial_v5 證實 30 xUnit + 6 visual passed + Reflection + 邊界測試
- **新發現觸發新 FF**：FF 二十八（已完成）/ 二十九（已完成）/ 三十 / 三十二（已完成）/ 三十三（已完成）/ 四十五-四十八

### v4 動態架構下的處置

Trial 試驗系列任務已完整覆蓋 self-implement 品質評估。**Trial_v6+ 留作 v4 動態架構驗證用**（Petra Magentic Orchestration / per-task session 行為驗證）。

→ 本 FF 任務範疇歸檔，後續 Trial 用獨立紀錄追蹤。

---

> 此檔僅含 v4 吸收 / Framework 內建 / Trial 完成的 FF。其他類型 FF 拆分如下：
> - **進行中 active 主清單** → [`Future_Feature.md`](Future_Feature.md)
> - **已完成項目摘要** → [`Future_Feature_completed.md`](Future_Feature_completed.md)
> - **v4 後重評估** → [`Future_Feature_v4_eval.md`](Future_Feature_v4_eval.md)
> - **冷凍 FF** → [`Future_Feature_frozen.md`](Future_Feature_frozen.md)
> - **變更紀錄** → [`Future_Feature_changelog.md`](Future_Feature_changelog.md)

# Future Feature — v4 動態架構吸收 / Framework 內建 / Trial 完成

> 從 `Future_Feature.md` 拆出（2026-05-01）
> 作用：保留歷史脈絡 — 受 v4 路線影響不再需要獨立做的 FF，避免 git diff 噪音

**歸檔分類**（2026-05-09 Trial_v6 後重新分類）：
- **重新分類紀錄（2026-05-09）**：FF 五/二十三/三十 → 移到 frozen / FF 四十五/四十六 → 升回 active / FF 四十一/四十四 → 已吸收 FF 五十一/五十三
- **A. v4 動態架構吸收**：MS Agent Framework + 動態調度 + per-task session + 重啟重跑模式下，這些議題自動消失（**2 個 — Trial_v6 後 5 個重新分類；剩餘 2 個已吸收進 FF 五十一/五十三**）
- **C. Framework 內建**：FF 四十九 內建涵蓋（2 個）
- **F. Trial 歷程完成**：Trial_v2/v3/v4/v5 已完成驗證任務（2 個）

---

# A. v4 動態架構吸收（7 個）

## 四十一、Stage 46 Sequential 鏈精修（已吸收 FF 五十一）

> 狀態：✅ **已吸收進 [FF 五十一](Future_Feature.md#五十一pipeline-framework-race-condition)**（Pipeline framework race condition）— Trial_v6 揭露 race + Status sync 同根因，子議題（race / Status sync）併入 FF 五十一處理。
> 提出日期：2026-04-29 / 重新分類：2026-05-09（Trial_v6 結案後）

## 四十四、TokenTrackingProvider 守門 estimatedTokens 缺陷（已吸收 FF 五十三）

> 狀態：✅ **已吸收進 [FF 五十三](Future_Feature.md#五十三api-餘額用盡時容錯性缺口)**（API 餘額用盡時容錯性缺口）— Token 守門設計重疊（FF 五十三補 USD billing 守門 + 三 Agent fail-fast 統一涵蓋本 FF 議題）。
> 提出日期：2026-04-30 / 重新分類：2026-05-09（Trial_v6 結案後）

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

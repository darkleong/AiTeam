# Stage 48：FF 四十九 spike Phase A — MS Agent Framework Writer-Critic POC

> 對應 Future Feature：FF 四十九（Microsoft Agent Framework 工具評估）
> 對應版本：**v3.34.0 不變**（spike 性質，不產生 production 變動）
> 建立日期：2026-05-02
> 狀態：✅ **已完成**（spike 結論 = 採用）
> 文件版本：v2.0
> **特殊性質聲明**：本 Stage 是 **spike**（探索 > 規格化），文件結構為「Spike Charter」（成功門檻 + 探索路徑）而非傳統 production Stage 子項拆分。

---

## 概述

**戰略背景**：Trial_v5 結束後 Aria research 揭露兩個關鍵發現 ① AiTeam 多年累積的 Orchestration 層是「手刻 framework」（WorkflowEngine + Crash Recovery + BossInteraction + RunMeetingSession + 流程編排）② **Microsoft Agent Framework 1.0 GA（2026-04-03）剛發布**，.NET first-class support，把 AiTeam 想做的事情全部標準化。

**Christ 戰略議題**：「AiTeam 固定流程式架構是否該停損 + 換架構？」→ Aria research 後拆解為兩個獨立決定 ① 工具選型（手刻 vs MS Agent Framework）② 架構選型（固定 pipeline vs 動態流程，FF 三十六 範圍）。**本 Stage 只處理 (1) 工具選型**，(2) 架構選型由 spike 結論驅動 FF 三十六 Phase B。

**比喻**：「**換引擎，車身保留**」— 車身（CLAUDE_*.md prompt / DB schema / Discord / Dashboard / ClaudeCodeService 包裝層）不變，只換底層引擎（手刻 framework → MS Agent Framework）。

**Spike 不是 production Stage**：本 Stage 不產生 v3.35.0 版本 bump、不寫 docker-compose / appsettings、不動 main 分支。產出是 ① POC code（spike branch）② spike 報告（採用 / 不採用 + 理由）③ 漸進遷移計劃（若採用）。

---

## Spike Charter

### 目標（spike 要回答的問題）

**主問題**：MS Agent Framework 1.0 是否值得替換 AiTeam 的手刻 Orchestration 層？

**子問題（6 個評估維度）**：
1. 開發速度比手刻快多少？
2. Trial_v5 揭露的議題 A/B 是否被 framework 內建解掉？
3. 學習曲線陡峭程度？Forge 是否能 1 週上手？
4. Custom Agent Executor 包 ClaudeCodeService 整合複雜度？
5. POC 跑 Mock 場景的穩定性？
6. 預估遷移成本是否低於 6 個月維護成本？

### 範圍（spike 內做什麼）

**POC scope**：用 MS Agent Framework 重寫 **Cody-Vera-Petra Appeal loop**（Writer-Critic Workflow 模式）。

理由：
- 直接對應 AiTeam 真實核心機制（既有 `ReviewAppealService` / `DevPlanAppealService` / `AppealOrchestrationService`）
- framework 內建 sample（`dotnet/samples/03-workflows/_StartHere/07_WriterCriticWorkflow`）可抄，學習曲線可控
- Custom Agent Executor 包 ClaudeCodeService 兩條路徑都覆蓋（Cody = CLI / Petra = API）
- 1.5 週工時可控，不會讓 spike 變 production

### 不在範圍（spike 不碰什麼）

- ❌ **DB / Migration**：純 in-memory state，不連 AiTeam DB
- ❌ **Discord 整合**：log 輸出 console + 報告檔，不發 Discord
- ❌ **Dashboard 整合**：spike 不需 UI
- ❌ **整條 Pipeline**：只實作 Cody-Vera-Petra Appeal loop，不重寫 Kickoff / Design / QA / Doc
- ❌ **完整對齊 production behavior**：POC 證明 framework 能跑通 Writer-Critic 模式即可，不需替代既有 AppealOrchestrationService

### 6 維度成功 / 失敗門檻（Christ 2026-05-02 拍板）

| 維度 | 強正向 | 中性 | 負向 |
|---|---|---|---|
| **1. 開發速度（LoC 對比）** | POC LoC 比手刻同等 workflow ≥ **30% 少** | -30% ~ +30% 之間 | POC LoC 比手刻多 ≥ 30% |
| **2. 議題自動解** | Trial_v5 議題 **A**（MarkGroupDoneOrIntervention）+ **B**（ImplementationNote 路徑斷裂）至少 **1 個** framework 內建直接解 | A/B 都需手寫 wrapper 才解 | A/B 都解不了 |
| **3. 學習曲線** | Forge **1 週內**跑通 sample + 寫出 POC | 1-2 週 | 卡 ≥ 3 天在文件 / API 不熟 → 提早結束 spike |
| **4. 整合複雜度** | Custom Agent Executor 包 `ClaudeCodeService` 跑通，**不需大改 ClaudeCodeService** | 需小改 ClaudeCodeService（單一 method 簽名變更）| 需重寫 ClaudeCodeService 或 Anthropic provider 不支援 |
| **5. POC 穩定性** | POC 跑 10 次 Mock 場景，成功率 **≥ 8/10** | 6-7/10 | < 6/10 |
| **6. 遷移成本** | 估算總工時 **≤ 6 個月手刻維護成本** | 6-12 個月 | > 12 個月 |

**結論模式**：
- **多數正向（≥ 4/6 強正向）** → 採用，啟動 Stage 49+ 漸進遷移
- **多數負向（≥ 4/6 負向）** → 不採用，維持手刻 + 補丁路線
- **中性混合（任一維度負向但其他正向）** → 細化評估，可能分模組漸進遷移（例：Workflow Builder 採用，Group Chat 不採用）

### 風險 / 不確定性

| 風險 | 機率 | 影響 | 緩解 |
|---|---|---|---|
| **Anthropic provider 在 .NET 上不支援 file system / shell tools**（FF 49 line 461 已標）| 高 | 整合複雜度 +++ | 探索第 2 步驗：跑 sample `02-agents/AgentWithAnthropic` 確認；不支援 → 用 Custom Agent Executor 包 ClaudeCodeService 代替 |
| **MS Agent Framework 1.0 GA 4/3 剛發布，文件 / sample 不齊**| 中 | 學習曲線 + | 對應維度 3 門檻，卡 ≥ 3 天提早結束 spike |
| **Custom Agent Executor 跟 framework 整合方式 undocumented** | 中 | 整合複雜度 ++ | 探索第 2 步先驗 `ModelContextProtocol` sample（Claude Code as MCP server 路線）|
| **POC 範圍蔓延**（Forge 想做 production-ready 整合）| 中 | 工時超 1.5 週 | Charter 明寫「不在範圍」清單，Forge 卡到 ≥ 1.5 週主動結束 spike |
| **遷移成本估算主觀**（維度 6 沒實際遷移過怎麼估？）| 中 | 結論偏差 | Forge 報告寫「以 POC 為樣本，按比例 extrapolate Stage 1-46 遷移工時」+ 列出主要假設 |

---

## 探索路徑（給 Forge 參考的 4 階段，非強制順序）

### 階段 1：Setup MS Agent Framework + sample 跑通（~2-3 天）

- 切 branch `spike/ms-agent-framework`
- 開新 .NET solution `spike/AiTeam.Spike.MsAgentFramework.sln`（隔離既有 `AiTeam.slnx`）
- NuGet 安裝 MS Agent Framework SDK（依 framework 1.0 GA 文件）
- 跑通 framework 內建 sample 至少 3 個：
  - `dotnet/samples/03-workflows/_StartHere/07_WriterCriticWorkflow`（核心對照組）
  - `dotnet/samples/02-agents/AgentWithAnthropic`（驗證 Anthropic provider 在 .NET 上的能力）
  - `dotnet/samples/02-agents/ModelContextProtocol`（Custom Agent Executor 整合路線參考）
- 產出：能跑通 sample 的最小可行 setup（`spike/ms-agent-framework` branch + Forge 探索筆記）

### 階段 2：Custom Agent Executor 包 ClaudeCodeService PoC（~2-3 天）

- 在 spike solution 內引用既有 `src/AiTeam.Bot/Agents/ClaudeCodeService.cs`（純讀，不改）
- 寫 `ClaudeCodeAgentExecutor`：實作 framework Custom Agent Executor 介面，內部呼叫 `ClaudeCodeService.RunAsync` / 對應 method
- 驗證單一 Agent（例：Cody）能透過 Custom Agent Executor 在 framework 內被 invoke
- 產出：Custom Agent Executor 範例 + 是否需要動 ClaudeCodeService 的記錄（對應維度 4 門檻）

### 階段 3：Writer-Critic POC 實作（~3-4 天）

- 用 framework Workflow Builder + Loop with max iteration safety 重寫 Cody-Vera-Petra Appeal loop
- Cody / Vera 透過 Custom Agent Executor + ClaudeCodeService（CLI 路徑）
- Petra 透過原生 framework Agent + Anthropic provider（API 路徑）
- 純 in-memory state（不連 DB）
- Mock 場景：硬編碼 task description + 預期 review 反饋，跑 10 次觀察穩定性
- 產出：可執行的 POC code + log 輸出 + 10 次跑的成功率紀錄（對應維度 5）

### 階段 4：6 維度評估報告（~1-2 天）

- 寫 `docs/experiments/Spike_v1_MsAgentFramework.md`
- 6 維度逐項對照門檻打分（強正向 / 中性 / 負向）
- 結論：採用 / 不採用 / 中性混合
- 若採用：附漸進遷移計劃（Stage 49+ 估算）
- 若不採用：附「維持手刻 + 補丁路線」的 backlog 識別

---

## 設計決策（Christ 2026-05-02 拍板）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **branch 策略** | **`spike/ms-agent-framework` branch** — 不開 PR、不汙染 main、最後採用時走 PR 整合 | 獨立 POC repo（失去 ClaudeCodeService 引用）/ main `experiments/` 子目錄（CI/CD 可能誤跑）|
| **6 維度成功門檻** | **採用 Aria 草案 6 個具體門檻** — 寫進本檔，spike 報告對照打分 | 不設門檻交 Forge 自評（風險：主觀） |
| **POC 範圍** | **Writer-Critic（Cody-Vera-Petra Appeal loop）** | CEO 分類（驗證不到核心）/ Group Chat（整合密度低）/ 整條 Pipeline（範圍太大）|
| **POC 範圍邊界** | **純 in-memory POC** — 不連 DB / Discord / Dashboard，log 輸出 console + 報告檔 | 連既有 DB / Discord（POC 變半 production，工時翻倍）|
| **文件結構** | **「Spike Charter」格式** — 成功門檻 + 探索路徑為主，非傳統 Stage 子項拆分 | 仍寫子項 1-N（強行套用 production Stage 模板會錯置）|
| **報告產出位置** | **`docs/experiments/Spike_v1_MsAgentFramework.md`** | 寫進本 Roadmap「實作紀錄」段（不利後續 Trial 系列查找）|
| **POC code 結局** | **保留在 spike branch**（未來遷移時參考），採用時改 PR 整合進 main，不採用時 branch 留著作 trace | 廢棄（失去探索資產）/ 強制整合進 main（汙染 production）|

---

## 驗收情境

> Spike 性質特殊：驗收的不是「功能跑通」，而是「**6 維度評估完成 + spike 報告產出**」。以下 5 個場景全部需在 Forge 結案前確認。

### 場景 A：Setup 階段完成

**怎麼觸發**：
1. `git checkout spike/ms-agent-framework`
2. `cd spike/`，執行 `dotnet build` 跑 spike solution
3. 跑 framework 內建 sample 三個：WriterCriticWorkflow / AgentWithAnthropic / ModelContextProtocol

**怎麼驗證**：
- ✅ Spike branch 存在於 GitHub
- ✅ `spike/AiTeam.Spike.MsAgentFramework.sln` build 0 errors
- ✅ 三個 sample 各跑通至少一次（log 輸出 + Forge 探索筆記紀錄行為）
- ✅ Anthropic provider 在 .NET 上的能力探索結論已寫入報告（對應風險「Anthropic provider 不支援」）

### 場景 B：Custom Agent Executor 整合驗證

**怎麼觸發**：
1. 跑 spike POC 的 Cody Custom Agent Executor 入口
2. 觀察是否成功透過 framework 呼叫 `ClaudeCodeService` 並取得結果

**怎麼驗證**：
- ✅ `ClaudeCodeAgentExecutor` 範例 code 存在 spike branch
- ✅ POC 不需修改既有 `src/AiTeam.Bot/Agents/ClaudeCodeService.cs`（對應維度 4 門檻：強正向）
- ✅ 若需修改：報告明列修改點 + 評估其廣泛性（影響其他 Agent 嗎？）

### 場景 C：Writer-Critic POC 跑通 + 10 次穩定性

**怎麼觸發**：
1. 跑 POC 的 Cody-Vera-Petra Appeal loop entry
2. Mock 場景：硬編碼 task description + 預期 review 反饋
3. 跑 **10 次**，記錄每次成功 / 失敗

**怎麼驗證**：
- ✅ POC 能跑通至少 1 次完整 Appeal loop（Cody 出 plan → Vera review → Petra 仲裁 → 終止 / 重跑）
- ✅ 10 次跑的成功率紀錄寫入報告（對應維度 5 門檻）
- ✅ Loop with max iteration safety 機制確認生效（測 Petra 永不仲裁的情境，max iteration 後正確中止）

### 場景 D：6 維度評估報告產出

**怎麼觸發**：
1. 開 `docs/experiments/Spike_v1_MsAgentFramework.md`

**怎麼驗證**：
- ✅ 報告結構含本 Charter 列出的 **6 個維度**逐項打分
- ✅ 每個維度對照本檔成功門檻表，明標 **強正向 / 中性 / 負向**
- ✅ 結論明寫 **採用 / 不採用 / 中性混合**
- ✅ 若採用：附漸進遷移計劃（Stage 49+ 估算 + 主要遷移風險）
- ✅ 若不採用：附「維持手刻 + 補丁路線」的 backlog 識別（哪些 FF 變得更急迫）
- ✅ 含 LoC 對比實際數據（POC vs 既有 `AppealOrchestrationService` + `ReviewAppealService` + `DevPlanAppealService` 三檔合計 LoC）

### 場景 E：Forge 結案路徑與後續驅動

**怎麼觸發**：
1. Forge 結案時告訴 Christ「Stage 48 spike 完成，報告在 `docs/experiments/Spike_v1_MsAgentFramework.md`，結論 = X」

**怎麼驗證**：
- ✅ Roadmap 文件版本標 v2.0 + 狀態 ✅ 已完成
- ✅ Roadmap「實作紀錄」段補：spike branch hash + 報告檔路徑 + 結論摘要（採用 / 不採用 / 混合）
- ✅ 結論驅動的下一個 Stage 候選明寫於本檔末尾（Stage 49 = 漸進遷移 / Stage 49 = FF 四十二 觀察類補丁 / 等等）

---

## 風險點 / 注意事項

### 1. 工時超範圍提早結束 spike

Spike 不是 production Stage，**目的是驗證可行性，不是把 POC 寫到 production-ready**。Forge 卡到以下任一條件 → 提早結束 spike + 寫「不採用 / 部分採用」報告：
- 階段 1 setup 卡 ≥ 3 天（對應維度 3 門檻負向）
- 階段 2 Custom Agent Executor 卡 ≥ 3 天且看不到出路
- 階段 3 POC 跑 10 次成功率 < 6/10（對應維度 5 門檻負向）

### 2. POC 範圍蔓延防護

Forge 探索期可能想加 DB / Discord / Dashboard 整合「順便驗一下」——**Charter「不在範圍」清單明寫禁止**，這些等 Stage 49+ 漸進遷移時才做。

### 3. Anthropic provider 在 .NET 上的能力是高風險

FF 49 已標 .NET Anthropic provider 沒內建 file system / shell / web search tools。**階段 2 必須優先驗這個**——若 provider 沒能力，Custom Agent Executor 包 ClaudeCodeService 是唯一路徑，整合複雜度 +++（影響維度 4 評分）。

### 4. spike 不踩 production code / Agent prompt（自省點 #21）

spike branch 可動 `spike/` 子目錄內所有檔案 + `docs/experiments/Spike_v1_*.md`。**不動**：
- ❌ `src/` 既有 production code（含 ClaudeCodeService — 純讀引用，禁修）
- ❌ `Resources/CLAUDE_*.md` 任何 Agent prompt
- ❌ docker-compose / appsettings / .github/workflows
- ❌ main branch

### 5. 報告主觀性緩解

維度 1（LoC）+ 維度 5（10 次成功率）有客觀數據。維度 2/3/4/6 偏主觀，Forge 報告必須附「依據」（程式碼引用 / framework 文件連結 / 實際嘗試的具體 commit）讓 Aria 結案複檢可重現判斷。

### 6. Stage 47 校準錨教訓套用

Stage 47 教訓：估 ~260K 卻推 Sonnet 200K 自我矛盾，導致中段 compact。**本 Stage 預估 ~200-300K + spike 探索高不確定性 → 直接 Opus 1M + high**，不嘗試 Sonnet 200K。

---

## 工時估 / Model 建議

### 工時估

| 階段 | 工時（理想）|
|---|---|
| 1. Setup + sample 跑通 | 2-3 天 |
| 2. Custom Agent Executor PoC | 2-3 天 |
| 3. Writer-Critic POC 實作 | 3-4 天 |
| 4. 6 維度評估報告 | 1-2 天 |
| **總計** | **8-12 天**（1.5-2.5 週），最壞 3 週 |

**提早結束情境**：階段 1 / 2 卡 ≥ 3 天 → 跳到階段 4 寫「不採用 / 部分採用」報告。

### Model / Effort 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 的「四維度評估」：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 高 — spike 探索高不確定性 + 學習新 framework + 整合 ClaudeCodeService |
| **改動範圍** | M（spike branch 內 ~5-10 檔，但有不確定性 buffer）|
| **歷史包袱** | 低（spike branch 隔離，不踩 main 既有機制）|
| **判斷品質要求** | 高 — 6 維度評估直接驅動 v4 戰略路線 |

**建議**：**Opus 1M + high**

理由：
1. **Spike 規則**（`workflow_aria_model_effort.md`「輔助判斷規則」段）：「Spike 項目（需要實驗驗證）：不管規模，用 Opus 1M」
2. **Stage 47 校準錨教訓**：「Aria 預估 context > 180K → 直接推 Opus 1M」
3. **判斷品質要求高**：6 維度評估報告需要深度推理 + 跨文件整合

### Context 預估

依 7 項公式 + spike 高不確定性：
- 開場 ~32K
- 工作 raw（spike 5-10 檔 + framework sample 探索）~80-120K
- Grep / Bash 輸出 ~20-30K（NuGet install / dotnet build 多次）
- 對話 turn 成本 ~50-80K（探索期密集討論）
- Edit 反覆對齊 ~20-30K
- Mock 驗收（10 次跑）~30-50K
- follow-up 修正 ~20-50K（spike 結束就結束，不像 production 有驗收期）
- 結案文件寫作 ~15-25K（spike 報告 + Roadmap 紀錄）
- **總計約 ~270-420K**（Opus 1M 內 ~30-45% 負擔，舒適區）

**拆 session 建議**：
- 若階段 1-2 探索期累積 > 150K → 主動拆 Session B 進階段 3
- 階段 4 報告寫作建議獨立 Session（context 乾淨，避免被探索期 noise 污染）

---

## 與 v4 路線的關係

**Stage 48 是整個 v4 路線的關鍵拍板**：

```
Stage 47 ✅ ops 補丁（FF 四十七，已完成）
   ↓
Stage 48 spike Phase A（本 FF 四十九，**結論驅動下一步**）
   ↓
   ├── 結論 = 採用 → Stage 49+ 漸進遷移（換引擎不換車身）
   ├── 結論 = 不採用 → 維持手刻 + 補丁（FF 四十二 / 四十三 / 觀察類 backlog 接續）
   └── 結論 = 採用 + 動態流程仍有獨立價值 → 啟動 FF 三十六 Phase B（架構評估 spike）
```

**FF 三十六 Phase B 是 Stage 48 的依賴**：Phase A 不通過 → Phase B 自動失效（沒框架基礎可動態調度）。

---

## 實作紀錄

### Spike 結案（2026-05-02，Forge）

**結論 = 採用（Adopt）**，啟動 Stage 49+ 漸進遷移。

**6 維度打分**：4 強正向 + 2 中性 + 0 負向 + 0 N/A

| 維度 | 評分 |
|---|---|
| 1. 開發速度 LoC | ✅ 強正向（POC 687 vs baseline 1454，減少 ~53%）|
| 2. 議題 A/B 自動解 | ✅ 強正向（兩個都被 framework 內建解掉）|
| 3. 學習曲線 | 🟡 中性（doc gap：Anthropic provider 仍 prerelease + source generator 套件分離 doc 沒提）|
| 4. 整合複雜度 | ✅ 強正向（ClaudeCodeService 0 修改 + ProjectReference + 1 層 Custom Executor）|
| 5. POC 穩定性 | ✅ 強正向（10/10 mock 場景全成功）|
| 6. 遷移成本 | 🟡 中性（估 4-6 個月 senior dev focused effort，落在強正向門檻邊界，保守判中性）|

**Spike branch 最終 commit**：`916b860`（spike/ms-agent-framework）

```
916b860  spike(stage48-phase3): Cody-Vera-Petra Writer-Critic Workflow + 10/10 mock runs
1b9742b  spike(stage48-phase2): Custom Agent Executor wrapping ClaudeCodeService
161e694  spike(stage48-phase1): MS Agent Framework setup + compile validation
```

**報告檔**：[`docs/experiments/Spike_v1_MsAgentFramework.md`](../experiments/Spike_v1_MsAgentFramework.md)

**Live runtime smoke 補測（2026-05-02 Christ 要求補做）**：

兩個 demo 用 AiTeam Bot 容器內的 production `ANTHROPIC_API_KEY` 跑通（key 從 `docker exec aiteam-aiteam-bot-1` 取出，僅注入 dotnet subprocess env，不寫入任何檔案 / log）：

| Smoke | 測試項 | 結果 |
|---|---|---|
| **1. phase1-smoke** | framework 原生 Anthropic provider 直接 API 呼叫（`AnthropicClient → AsAIAgent → RunAsync`），對應 Petra 路徑 | ✅ exit 0，Claude haiku-4-5 回 "Spike phase 1 smoke OK" |
| **2. single-agent** | 整合鏈：`framework Workflow → ClaudeCodeAgentExecutor → ClaudeCodeService.RunReadOnlyAsync → claude CLI subprocess → 真 Anthropic API → 結果回 framework`，對應 Cody/Vera 路徑 | ✅ exit 0，`WorkflowOutputEvent` 正確捕獲 Claude 回應 |

→ 兩條路徑（API 直連 + Custom Executor 包 ClaudeCodeService）live runtime 證實可用，**維度 4 整合複雜度的「強正向」評分由 compile-time 提升為 runtime-validated 強正向**，不影響採用結論但提高信心。

**Smoke 2 過程小障礙**：Windows .NET `Process.Start("claude") + UseShellExecute=false` 不會 honor PATHEXT 找 `claude.cmd`，需建一個 `claude.exe` shim 把呼叫導向 `cmd.exe /c claude.cmd`。**這是 Windows 開發機獨有的環境問題**，不影響 production Linux Docker 容器內 `claude` 是 node-installed 無副檔名 binary 的部署。Stage 49 起若在 Windows 開發機 local 跑遷移後的 framework workflow，需重做此 shim 或 ClaudeCodeService 內判 OS 改 invoke 方式（FF 候選備註）。

shim + sandbox 全部建在 `%TEMP%` 子目錄並在 smoke 結束後 cleanup，未污染 spike/ 或任何 git-tracked 路徑。


**結論一句話**：MS Agent Framework 1.0 GA + Workflows.Generators source generator 套件可作為 AiTeam 手刻 Orchestration framework 的替代引擎；POC 級驗證表明整合成本低（ClaudeCodeService 不動）+ Trial_v5 議題 A/B 直接被內建解掉，可啟動 Stage 49+ 漸進遷移（Hybrid 整合策略：CLI 路徑 Custom Agent Executor + API 路徑原生 Anthropic provider）。

**下個 Stage 候選**（依本報告節 7 漸進遷移計劃）：

- **Stage 49**：第一個 Workflow 遷移 — Cody-Vera-Petra Appeal loop（POC 已有藍本，加 DB / Discord / Crash Recovery 整合，2-3 週）
- **Stage 50**：RunMeetingSession 重寫為 Group Chat orchestration（2-3 週）
- **Stage 51**：BossInteraction 重寫為 Human-in-the-Loop RequestInfoExecutor（1-2 週）
- **Stage 52**：WorkflowEngine 整體 hardcoded pipeline → Workflow Builder（4-6 週，最大遷移點）
- **Stage 53**：Crash Recovery 切換到 framework Checkpointing（2 週）
- **Stage 54**：收尾 + token middleware + production 切換 + 老 code 刪除（2-3 週）
- **Stage 55+**：FF 三十六 Phase B 動態流程架構評估（基於本 spike 採用結論的 framework 基礎啟動）

> 順序由 Christ 拍板。Stage 49 起跑前須處理三個前置：① integration test snapshot 鎖 production behavior ② lock framework 版本機制 ③ Anthropic provider prerelease 風險評估（必要時準備 Custom Executor 包 Anthropic.SDK 備援）。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版 Spike Charter 建立（Aria）—— 本 Stage 為 spike，文件結構為 Charter（成功門檻 + 探索路徑）非傳統 production Stage 子項拆分 |
| v2.0 | 2026-05-02 | Spike 結案（Forge）—— 4 強正向 + 2 中性 + 0 負向，結論 = 採用，啟動 Stage 49+ 漸進遷移；spike branch 最終 commit `916b860`，報告檔 `docs/experiments/Spike_v1_MsAgentFramework.md`；本檔狀態更新為 ✅ 已完成 |
| v2.1 | 2026-05-02 | Live runtime smoke 補測（Christ 要求）—— phase1-smoke + single-agent 兩 demo 用 production ANTHROPIC_API_KEY 跑通，整合鏈 runtime-validated；發現 Windows .NET Process.Start vs PATHEXT `.cmd` 問題（環境性，不影響 Linux 容器部署）|

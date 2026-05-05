# Trial_v6 試驗計劃書 — v4 framework 路線端到端真實任務驗證

> 對應版本：**v3.45.0**（Stage 56 結案後 = v4 framework 全切換架構第一次真實任務試驗）
> 建立日期：2026-05-05
> 狀態：📋 計劃中（待 Christ 提供任務 + 開跑）
> 文件版本：v1.0

---

## 一、背景與定位

### 戰略脈絡

- **v4 漸進遷移完整路線 9/9 達成**（Stage 49-55B，2026-05-02 ~ 2026-05-05）— Custom workflow engine 全面切換 MS Agent Framework hierarchical static ARCH
- **Stage 56 Trial_v6 前置條件統包完成**（v3.45.0，2026-05-05）— Dashboard 33 framework_* 場景補 + FF 四十二/四十三 修 + conventions 補 — Trial_v6 開跑前工具完備
- **Trial_v5（v3.33.0 + 4 FF 補強）= 既有架構最終真實任務試驗**（2026-05-01，PR #115，4 FF 補強讓 Cody 1/12→11/12 + Quinn 失敗→30 xUnit + Vera 保守誤判→精準抓 9 處裸 catch + Sage 13:30 異常→14 秒 escalate；揭露 6 個流程設計議題）

### Trial_v6 定位

**v4 framework 全切換架構（v3.45.0）的第一次真實任務試驗** — 性質對齊 Trial_v5「鎖定前置條件全綠的對照組驗證」，但對照基線從「v3.33.0 + 4 FF 補強」升級為「v3.45.0 v4 framework 全切換」。

**注**：FF 三十六 Phase B 動態流程架構（Petra Magentic Orchestration / per-task session）**不在本次範圍** — v4 路線是「換引擎不換車身」（framework hierarchical static），動態架構評估留 Trial_v7+ / FF 三十六 Phase B spike 後。

---

## 二、試驗目的（4 條）

1. **v4 framework 路線端到端在真實任務跑通驗證** — Mock 場景已綠（53B 6 + 55B 5 + 55A 4 + 54 2 + 52 6 + 51 4 + 50 5 + 49 5 = 37 場景全綠），真實任務完整 pipeline（CEO → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → done）走 framework path 是否端到端跑通
2. **對照 Trial_v5 vs Trial_v6 差異**（cost / 完成度 / 流程順暢度 / 揭露 bug）— 量化 v4 路線投資 ROI
3. **自然驗 Stage 56 修法**（FF 四十三 token_logs.TotalCostUsd 95%+ 寫入率達標 + IsEstimated flag 分布 + FF 四十二 TryParseDesignIssues robust 不依賴 [MOCK] workaround）
4. **揭露 v4 framework 路線在真實任務下的議題** — Mock 場景未涵蓋的 edge case、production 整合 noise、Crash Recovery 真實觸發行為等

---

## 三、任務需求

> ⚠️ **待 Christ 提供** — 可選：① 沿用 Trial_v5 同任務（最佳對照組，cost / 完成度可逐項對比）/ ② 新功能（更真實但失去對照基線）

**建議 ①**：沿用 Trial_v5 任務（FF 十六重做 + 後續流程改善），對照組價值最高。

---

## 四、預期觀察清單（Trial_v6 vs Trial_v5 對照維度）

| # | 維度 | Trial_v5 baseline（v3.33.0 + 4 FF）| Trial_v6 預期（v3.45.0 v4 全切換）| Aria 自驗 SQL/工具 |
|---|---|---|---|---|
| 1 | **Pipeline framework path 走通率** | N/A（無 framework path）| **100%**（PipelineFrameworkStateJson NOT NULL）| `SELECT COUNT(*) FROM task_groups WHERE PipelineFrameworkStateJson IS NOT NULL AND CreatedAt >= '<試驗開跑時間>'` |
| 2 | **總 cost** | $8.78 USD | 預期 ±50% 範圍（framework overhead vs 流程順暢度 trade-off）| `SELECT SUM(TotalCostUsd) FROM token_logs WHERE CreatedAt >= '<試驗開跑時間>'` |
| 3 | **token_logs.TotalCostUsd 寫入率** | 0.3%（FF 四十三 揭露）| **≥ 95%**（Stage 56 修法自然驗）| `SELECT COUNT(*) FILTER (WHERE TotalCostUsd IS NOT NULL) * 100.0 / COUNT(*)` |
| 4 | **IsEstimated flag 分布** | N/A（欄位不存在）| Path A CLI single-shot fallback estimation rate vs Path B Anthropic API direct estimation rate | `SELECT IsEstimated, COUNT(*) FROM token_logs GROUP BY IsEstimated` |
| 5 | **Cody 完成度**（Issue 數）| 11/12 Issue（916 行）| 預期同等或更佳 | git log + PR #N files changed |
| 6 | **Quinn 測試**（xUnit + visual）| 30 xUnit + 6 visual all passed | 預期同等 | `dotnet test` + Playwright result |
| 7 | **Vera 審查精準度** | 9 處裸 catch 精準抓 | 預期同等 | Reviewer comment 內容 grep |
| 8 | **Sage 異常 escalate 速度** | 13:30 異常 → 14 秒 escalate | 預期同等或更快 | Bot log timestamp diff |
| 9 | **Petra 仲裁次數 + type 分布** | N/A（不顯性紀錄）| Pipeline framework 5 routing HITL type 觸發次數 | `SELECT Type, COUNT(*) FROM boss_interactions WHERE GroupId IN (<Trial 期間 group ids>) GROUP BY Type` |
| 10 | **Crash Recovery 觸發行為** | N/A | 真實 docker restart 觸發 framework Recovery 路徑（4 router）vs Mock 場景已驗 | Bot startup log scan：`[CrashRecoveryFramework*]` log entry |
| 11 | **HITL routing 真實觸發** | N/A | 5 type-specific HITL（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）真實任務觸發 | BossInteraction Type 分布 + InteractionProcessor 路由表 hit rate |
| 12 | **揭露 bug 數量** | 13 bug（5 🔴）| 預期下降（v4 path 已 Mock 全驗 + Stage 56 修兩 FF），但會有真實任務新揭露 | 試驗期間 Forge / Aria 紀錄 |

---

## 五、Aria 自驗 SOP（Christ 強調 — Aria 來驗，不是 Forge）

### 5.1 分工矩陣

| 工作項 | Aria 做 | Christ 做 |
|---|---|---|
| **DB metrics 查詢**（SQL 12 維度） | ✅ 自動 | — |
| **Bot docker log scan**（Recovery / Crash / Pipeline transition / Petra 仲裁）| ✅ 自動 | — |
| **commit / PR 結構分析**（Cody 產出規模 / 修改檔案範圍 vs DesignPlan）| ✅ 自動 | — |
| **對照組數字報告**（Trial_v5 vs Trial_v6 12 維度） | ✅ 自動 | — |
| **異常 pattern detection**（Cody 跑 N 輪才成功 / Vera 連續 reject / 卡死的 BossInteraction）| ✅ 自動，濃縮報告給 Christ | 看報告判 OK/NOK |
| **Trial 健檢報告**（每 milestone / 異常發生時）| ✅ 寫初稿 | review + 補主觀感受 |
| **UAT「做出來是不是我要的」**（FF 十三性質）| ❌ 不能取代 | ✅ 必驗 |
| **業務正確性 / 原 spec 對照** | ❌ 看不到 spec 細節 | ✅ 必驗 |
| **AI Agent 主觀品質判斷**（Vera 審查到位嗎 / Petra 決議合理嗎 / Demi UI 規格符合需求嗎）| ⚠️ 可做技術品質檢查（grep / a11y / 命名）但不知業務正確性 | ✅ 主觀部分必驗 |

### 5.2 觸發點

Aria 自驗 = 主動觸發（不等 Christ 觸發），三個觸發點：

1. **Stage milestone 觸發**：每個 stage（Kickoff/Design/Dev_plan/Dev/Reviewer/QA/Doc）完成時，Aria 跑健檢 SQL + log scan + 對照 baseline 報告
2. **異常觸發**：BossInteraction 開啟（除 ack only type）/ Petra 仲裁 / Crash Recovery / Token 守門攔截 → Aria 立刻濃縮報告 + 分類（Trial_v5 已踩 vs 新議題）
3. **試驗結案觸發**：Trial 完成（done / cancelled / intervention 結案）→ Aria 寫 Trial_v6 結案報告草稿（對齊 Trial_v5 結構）

### 5.3 自驗工具清單

| 工具 | 用途 | 範例 |
|---|---|---|
| `Bash` SQL via `docker exec aiteam-postgres psql -U aiteam -d aiteam -c "..."` | DB metrics 查詢 | 寫入率 / Petra 仲裁次數 / Pipeline path 走通率 |
| `Bash` `docker logs aiteam-bot --since <time> 2>&1 \| grep -E "<pattern>"` | Bot log scan | Recovery / Crash / Pipeline transition |
| `Bash` `git log --oneline / git show / gh pr view` | commit / PR 結構分析 | Cody 產出規模 / files changed |
| `mcp__Claude_in_Chrome__*` | Dashboard 視覺驗證（如需）| 任務狀態快照 / Pipeline View / token 統計頁 |
| `Read` / `Grep` | 規格 / Trial 報告對照 | Trial_v5 對照組 |

> **不在 Aria 範圍**：直接 Discord 觸發 / 真實 LLM call / production 改 code（Aria 是諮詢 + 驗證角色，不執行 production 操作；ops 紀律對齊 user_christ.md「Aria ops 充分授權」段，僅限 read-only 查證 + 文件修改）

### 5.4 Trial 健檢報告格式

每次 milestone / 異常觸發後產出，格式：

```markdown
## Trial_v6 健檢報告 — <Checkpoint N> @ YYYY-MM-DD HH:MM

### 當前狀態
- TaskGroup ID：<guid>
- 已完成 stage：Kickoff ✅ / Design ✅ / Dev_plan ✅ / Dev 🔄 ...
- Pipeline framework path：✅ 走通 / ⚠️ fallback to legacy / ❌ 卡死

### 12 維度 SQL 數字（與 Trial_v5 baseline 對照）
| 維度 | Trial_v5 | Trial_v6 (now) | 差異 | 解讀 |
| ... |

### 異常揭露
- [若有] 異常 1：<描述> → 分類：<Trial_v5 已踩 / 新議題> → Aria 推薦：<繼續觀察 / escalate Christ>

### Christ 拍板項
- 議題 1：<具體問題 + 三選項 A/B/C + Aria 推薦>

### 建議下一步
- <Aria 推薦>
```

---

## 六、Christ 必驗範圍（UAT + 主觀判斷）

不可由 Aria 取代的驗收項：

1. **UAT「做出來是不是我要的」** — 任務最終產出 vs Christ 心中成功標準對照
2. **業務正確性** — code 真的解需求嗎？UI 規格符合期待嗎？
3. **AI Agent 主觀品質判斷** — Vera 審查的判斷品質 / Petra 決議的合理性 / Demi UI 規格的符合度
4. **Aria 異常報告的 OK/NOK 拍板** — Aria 報「Cody 跑 3 輪才成功」，Christ 判「v4 動態優勢 vs 異常」
5. **議題分類** — Aria 報的「新議題」是否真的是新議題（Christ 業務知識補充）

---

## 七、試驗開跑前置條件

開跑前 Aria 自動 check 清單：

- [ ] **production deployment v3.45.0** — `git log --oneline -1` 確認 commit hash + Bot 啟動 log 顯示 `Version 3.45.0`
- [ ] **feature flag 全 ON**：
  - [ ] `UseFrameworkAppealLoop` = true
  - [ ] `UseFrameworkKickoff` = true
  - [ ] `UseFrameworkHITL`（Kickoff 中途介入試點）= true
  - [ ] `UseFrameworkDesign` = true
  - [ ] `UseFrameworkPipeline` = true
- [ ] **MockMode = OFF**（真實任務驗，非 Mock）
- [ ] **Migration `Stage56TokenLogsIsEstimated` applied** — `SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_name='token_logs' AND column_name='IsEstimated')`
- [ ] **既有 stuck mock NewFeature group 清掉**（Stage 55B Session A pre-check 揭露 1 stuck，避免干擾 Trial_v6 metrics）
- [ ] **Token 守門設定確認**（全域月限 / per-agent 日限不會在 Trial 中段觸發）

---

## 八、結案標準（Aria 判定 + Christ 確認）

Trial_v6 結案分**成功 / 部分成功 / 失敗**三級：

| 結案類型 | 條件 |
|---|---|
| **✅ 成功** | Pipeline framework path 端到端跑通 + cost 在 Trial_v5 ±50% 範圍 + Christ UAT 通過 + 揭露議題收斂為 follow-up FF 立檔（無 🔴 阻塞 production）|
| **⚠️ 部分成功** | Pipeline framework path 部分跑通（fallback to legacy ≥ 1 處）+ Christ UAT 部分通過 + 揭露 1-2 個 🔴 議題需即時 hotfix |
| **❌ 失敗** | Pipeline framework path 卡死 / fallback to legacy ≥ 3 處 / Christ UAT 完全不通過 / 揭露 ≥ 3 個 🔴 議題需 stage 級修正 |

---

## 九、結案後動作

### Aria 結案第二段範圍（Trial 結案 ≠ Stage 結案，內容不同）

1. **本檔升 v2.0** — 加結案紀錄段（Checkpoints 觀察填實 + 對照組數字 + 揭露議題清單 + Christ UAT 結論 + 結案類型判定）
2. **新立 FF**（揭露議題逐項立 FF + 優先級）
3. **CHANGELOG 不需 entry**（Trial 是試驗不是 Stage，不 bump 版本）
4. **Future_Feature_changelog 加 v7.77 entry**（Trial_v6 結案 + 新立 FF + 戰略結論）
5. **calibration_anchors 加「Trial_v6 校準錨」段**（如有）— 紀錄 Aria 自驗工作量 / Trial cost / 對照組差異

### 戰略結論候選

- v4 framework 路線投資 ROI 量化（cost / 完成度 / 流程順暢度 vs Trial_v5）
- FF 三十六 Phase B 動態架構評估啟動條件（Trial_v6 結論驅動）
- Trial_v6 → Trial_v7+ 路線拍板（繼續 hierarchical static 優化 vs 進入動態架構 spike）

---

## 十、技術約束

- 不引入新 code 改動（Trial 是觀察試驗，production code 不動 — 揭露的議題立 FF 排 Stage 修）
- Aria 自驗工具僅限 read-only（SQL SELECT / docker logs / git log / gh pr view），不可動 production data
- Trial 期間 production code 凍結（除非揭露 🔴 阻塞議題需 hotfix）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-05 | 初版計劃書建立（Aria）— Trial_v6 = v4 framework 全切換架構（v3.45.0）第一次真實任務試驗，對照 Trial_v5（v3.33.0 + 4 FF）baseline；Aria 自驗 SOP 12 維度 + 5 觸發點 + 4 工具 + 健檢報告格式 + 分工矩陣（Aria 機制/數字/異常 pattern + Christ UAT/業務正確性/主觀品質）；FF 三十六 Phase B 動態架構評估**不在本次範圍**留 Trial_v7+。**規劃前期已 grep**：Trial_v5 結構（13 Checkpoints + 結果矩陣 + 結論）+ docs/experiments/ 5 既有檔（Spike_v1 / Trial_v2/v3/v4/v5）+ Stage 56 工具完備清單（Dashboard 33 framework_* 場景 + token_logs.IsEstimated + TryParseDesignIssues line-iteration）+ feature flag 全清單（UseFramework{AppealLoop / Kickoff / HITL / Design / Pipeline}）。

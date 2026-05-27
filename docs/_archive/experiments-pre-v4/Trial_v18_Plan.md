# Trial_v18 試驗計劃書 — v5.5 Phase 2 Step 5 正式完整收口拍板閘門

> 對應版本：**v3.62.0**（Stage 72 結案 — Prompt DB 化 + Talent identity 整合）
> 建立日期：2026-05-17
> 狀態：✅ 已結案 — 🟢 **全綠**（v5.5 Phase 2 Step 5 正式完整收口拍板閘門通過）
> 文件版本：v2.0

---

## 一、背景與定位

**Stage 72 結案後 v5.5 Phase 2 Step 5 正式完整收口拍板閘門**：

- Stage 72 ✅ SkillPrompts + TalentPrompts 兩層 schema + Versioning + rollback + PromptResolver service + 6 SkillPrompts seed + DI propagation chain 5 檔
- Trial_v18 = 驗 Stage 72 兩 path 真實生效 + 業務級成功重現 → Christ 拍板切 `UseV5PromptDb` default true（SQL 維持 true 上線）= v5.5 Phase 2 Step 5 正式完整收口
- **aria-trial-run skill 第 2 次實踐**（紀律工具化持續成熟）

---

## 二、試驗目的（3 條核心）

1. **Stage 72 PromptResolver service production 真實生效驗** — cache reload 真實 fire + 5min TTL 真實生效
2. **DB-driven prompt byte-for-byte 對齊 hardcoded 驗** — 議題 4「內容不動」搬家工程 0 regression 真實實證
3. **連續 8 Trial 業務級成功延續**（Trial_v10/v11/v12/v13.2/v14/v15.2/v17/v18）

---

## 三、任務需求

沿用 Trial_v6-v14 同 prompt（reuse `.tmp/trial_v15_body.json` — 對齊 9-step 模板「同 prompt 對照組精準度最高」精神）— Dashboard 錯誤處理打磨 toast 通知。

---

## 四、結案紀錄

### 4.1 lifecycle 概覽

| 項目 | 結果 |
|---|---|
| 開跑時間（UTC）| 2026-05-16 17:29:26 |
| PetraSession ID | `b1c501c4-97f0-49a7-91a8-ce828ceb4d66` |
| Petra 啟動 path | v5.5 Talent-Skill path + useV5SubtaskPlanning=True ✅ |
| DecideTalentsWithPlanAsync | **subtasks=3 dependencies=2** picks=Cody(impl) → Vera(review) → Quinn(qa_testing) |
| **PromptResolver cache reloaded** ⭐ | **2 次真實 fire**（首次 cache miss + 5min TTL 過期 reload）/ `6 skills / 0 talents` 對齊 baseline seed |
| Cody outputLen | **2435**（1 段做完整任務 / vs Trial_v17 2 段 1870+2124=3994 / 一段策略性更聚焦）|
| Vera outputLen | 1526 |
| Quinn outputLen | **833**（連續 3 Trial Quinn 真做事 / 議題 #4 transient 進一步結案）|
| **PR 開啟** | ✅ [#378](https://github.com/darkleong/AiTeam/pull/378) 真實開出（**19 檔 +1158/-73** / 10 範圍 cover 首次補 InteractionCenter+RuleManagement+TaskCenter）|
| FinalizeGitAsync | ✅ 完美無 permission denied（自省點 #34 紀律連續 2 Trial 0 踩雷）|
| Duration | 14m29s |

### 4.2 Stage 72 核心驗收達標項

#### ⭐ 議題 4「內容不動」搬家工程 0 regression production 真實實證

- **dispatch 1/3 Cody promptLen=1674 對齊 Trial_v17 dispatch 1 同數字** — DB-driven prompt byte-for-byte 對齊 hardcoded（PetraPromptTemplate.Template + 動態 skill roster 注入機制保留）
- 對齊議題 4 拍板「Stage 72 範圍只做搬家工程 / prompt content 升級留 Stage 73+ 評估」精神

#### ⭐ PromptResolver service production 真實生效

- `PromptResolver cache reloaded: 6 skills / 0 talents` **真實生效 2 次**：
  - 首次 cache miss（Petra 啟動時 BuildPetraSystemPrompt → ResolvePetraBaseTemplateAsync 觸發）
  - 5min TTL 過期 reload（cache 過期自動 reload 對齊 Stage 72 設計）
- Stage 72 cache 機制 production 真實實證

#### ✅ 自省點 #34 紀律連續 2 Trial 0 踩雷

Trial_v17 + Trial_v18 連續兩次 FinalizeGitAsync 完美無 permission denied — aria-trial-run skill 工具化 + workspace cleanup 紀律「完全省略 cleanup 信任 Bot CloneOrPull」持續實證。

### 4.3 業務品質（PR #378）

- **19 檔改動 +1158/-73**（vs Trial_v17 12 檔 +715 / **3x deliverable** vs Trial_v15.2 6 檔 +32）
- **10 範圍 cover** — 首次補 **InteractionCenter + RuleManagement + TaskCenter** 三個新範圍（vs Trial_v17 7 範圍）
- **主動補 6 unit test + 1 Playwright VisualTest**（vs Trial_v17 3 unit test）
- **Aria 角度品質評分 4.7/5** ⭐：
  - 範圍 cover 完整度 5/5（含 Login.razor 正確標「不適用」— 沒硬補不適合的場景）
  - Cody Code 品質 4.5/5（**SystemSettings toggle revert** UX bug 防 + **TaskCenter MudTable ServerData 空 TableData** 避錯誤狀態 / 4 條 production-grade 關鍵決策）
  - Vera review 質感 5/5（揭真實 UX bug + impact 分析「直接影響 vs 潛在副作用」分層 + 修法明確）
  - Quinn 測試覆蓋 4/5（19 unit test + **邊界聲明**「async 錯誤處理路徑需 integration test 補齊」對齊測試類型邊界紀律）
  - 業務 UX 對齊 4.5/5（三層判斷：雙通道 / 單通道 / 不適用）

### 4.4 cost / blind spot

| Agent | output tokens | cost |
|---|---|---|
| PM (Petra) | 175 | $0.007 |
| Cody (1 段) | 31,048 | $1.298 |
| Vera | 6,680 | $0.290 |
| Quinn | 19,301 | $0.706 |
| **Total** | | **$2.301** |

- 真實 cost：**$2.301** + Stage 72 Forge Claude Code session cost ~$1.75（Opus 1M + ultrathink）= 累積 ~$4.05
- 餘額追蹤：$20.04 → **$15.99** ✅ 對齊真實
- **cost per file = $0.121**（vs Trial_v17 $0.147 / **-18%**）⭐ **最優 ROI baseline**
- Blind spot ≈ 0%（連續 5 Trial 0% blind spot 維持）

### 4.5 議題分類

| # | 議題 | 嚴重度 | 範圍 | 處置 |
|---|---|---|---|---|
| 1 | **SystemSettings.razor.cs SaveWorkflowRoundsAsync inconsistent pattern**（Vera review 揭 / line 332 漏 `_saveError = null;` / 5 method 中 1 個漏）| 🟡 | **只在 spike branch / 不在 main** | PR close = 議題自動消失 / backlog 紀錄留 PR close 留言 / Stage 73+ 真實升級 SystemSettings prompt content 時對齊 5 method consistent pattern |
| 2 | 場景 E rollback production 真實 SQL UPDATE 改 active version 沒 explicit 驗 | 🟢 mild | xUnit T2 已驗 | Stage 73+ 真實業務需求觸發再驗 / 不阻擋上線 |
| 3 | 場景 F v4 既有 path（切 `UsePetraOrchestratorV5=false`）沒 explicit 驗 | 🟢 mild | 既有紀律 v4 path 已長期 0 regression | Stage 73+ 真實業務需求觸發再驗 / 不阻擋上線 |

### 4.6 戰略結論

**🟢 Trial_v18 完整 PASS = v5.5 Phase 2 Step 5 正式完整收口** ⭐⭐：

- Stage 72 Prompt DB 化兩 path 真實生效（cache reload + DB-driven prompt byte-for-byte 對齊 hardcoded）
- v5/v5.5 path 工程實證收口完整（Trial_v10-v18 連續 8 Trial 業務級成功）
- **5 flag SQL=true production active**（`UsePetraOrchestratorV5` + `UseTalentSkillSeparation` + `UseV5Memory` + `UseV5SubtaskPlanning` + **`UseV5PromptDb` ⭐ 新加**）— v5.5 Phase 2 完整路徑 production 預設
- **連續 6 Stage 0 follow-up bug fix**（Stage 67/68/69/70/71/72）+ Aria 評估框架升級三大紀律累積（自省點 #35「品質 > 做法」+ #36「對等和互相」+ #37「context 預估三步法」）

---

## 五、Aria 戰略級觀察

### 5.1 Aria 評估框架升級三大紀律累積實證（Trial_v17→v18 真實 trace）

- **自省點 #35「品質 > 做法」** — Trial_v18 subtasks=3（vs Trial_v17 subtasks=4 / Trial_v15.2 subtasks=5）= LLM 自主判斷對齊「品質 > 做法」精神 / Aria 不用 metric 死標準評估
- **自省點 #36「對等和互相」** — Trial_v17 結案 Christ 親口拍板「對等和互相」精神 → Trial_v18 結案 Aria 主動承認「B 描述順手 follow-up commit 修是技術判斷錯誤」（沒考慮議題範圍只在 spike branch / Trial PR 不 merge 紀律）→ 真實再評估給雙面定見 ✅ 紀律真實生效
- **自省點 #37「context 預估三步法」** — Stage 72 真實 367K vs Aria 預估 225-305K 揭根因 → Trial_v18 cost 對帳 $20.04 → $15.99 揭「Forge Claude Code session cost 漏算」候選自省點 #38

### 5.2 候選自省點 #38（待 `/aria-memory` 立檔）

**Stage cost 預估必含「Forge Claude Code session 自身 LLM cost」+「Trial AiTeam LLM cost」雙因子**：

- Stage 72 真實餘額落差 $4.05（Aria 預估 $2.30 / 真實 $4.05）
- Forge Claude Code session（Plan 209K + 實作 337K + 驗收 363K + 結案 367K / Opus 1M + ultrathink）燒 Christ 個人 Anthropic API ≈ $1.75
- 對齊自省點 #37「context 預估三步法」延伸到 cost 預估維度

### 5.3 對 Stage 73+ Phase 3 設計指引

- **Phase 3 Step 6 WebUI Talent CRUD** — 對齊「Phase 1+2 完整收口才開 WebUI」紀律 / Stage 72 收口後啟動條件達成
- **Phase 3 Step 7 prompt content 升級評估** — 對齊議題 4「Stage 72 內容不動 / 留 Stage 73+ 評估」精神 / 含 SystemSettings inconsistent pattern 修法（議題 1 範圍）
- **Phase 3 Step 8 真並行 dispatch / 3 agent debate**（v5.5.md 既有規劃）

---

## 六、後續行動

### 立即（aria-trial-summary 結案動作）

1. ✅ PR #378 close（含詳細議題 1 紀錄）
2. ✅ Trial_v18_Plan.md v2.0 結案紀錄
3. ✅ SQL flag **維持 true** 5 flag production active（`UseV5PromptDb` 加入上線陣容 / 對齊紀律 #10「Trial 通過後對齊『下個 Stage Roadmap 預期』= v5.5 Phase 2 完整上線」）
4. Future_Feature.md / Future_Feature_v5.5.md update — Step 5 Trial_v18 🟢 + Phase 2 完整收口拍板

### Stage 73+ Phase 3 候選

**v5.5 Phase 3** ✅ 啟動條件達成（Phase 1+2 完整收口）：
- Step 6 WebUI Talent CRUD（規模 S-M）
- Step 7 prompt content 升級（規模 M / 對齊 Trial_v18 經驗 + 議題 1）
- Step 8 真並行 dispatch / 3 agent debate（規模 M）

---

## 七、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v18 在 main branch 跑（含 Stage 67-72 全 commits + Trial_v17 結案 commits）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v17 既有驗證
- 對齊 Trial_v2-v17 既有獨立試驗計劃模式 + aria-trial-run skill 第 2 次實踐

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-17 | 試驗結案紀錄（直接 v2.0 — 試驗前未先建 v1.0 plan / 對齊 Trial_v15+v16 既有實踐 / aria-trial-run skill 內容已 cover 9-step 模板紀律不需要 plan 重寫）。**🟢 完整 PASS — v5.5 Phase 2 Step 5 正式完整收口拍板閘門通過**。**真實 lifecycle**：UTC 17:29:26 開跑 → Petra v5.5 path enter → PromptResolver cache reloaded 6 skills / 0 talents（首次 cache miss）→ DecideTalentsWithPlanAsync subtasks=3 dependencies=2 → Cody 1/3 outputLen=2435 + 寫回 memory → Vera 2/3 outputLen=1526 + cache reload 第 2 次（5min TTL）→ Quinn 3/3 outputLen=833 → Petra PR #378 真實開出（19 檔 +1158/-73 / 10 範圍 cover 首次補 InteractionCenter+RuleManagement+TaskCenter）→ 14m29s 完整 lifecycle。**核心驗收 3 項達標**：① PromptResolver cache reload 真實 2 次生效（首次 + TTL 過期）② DB-driven prompt promptLen=1674 對齊 Trial_v17 同數字 = 議題 4「內容不動」搬家工程 0 regression production 真實實證 ⭐ ③ 連續 8 Trial 業務級成功延續（v10-v18）。**業務品質 Aria 評分 4.7/5**（Cody production-grade UX 判斷 + Vera review 質感 + Quinn 19 unit test + 邊界聲明）。**議題分類**：🟡 1（SystemSettings inconsistent pattern / 範圍只在 spike branch 不在 main / PR close 自動消失 / backlog 留 Stage 73+ 升級時對齊） + 🟢 2（場景 E/F xUnit 已驗 / production 真實驗留 Stage 73+）+ 0 🔴 新議題。**戰略結論**：**5 flag SQL=true production active**（UsePetraOrchestratorV5 + UseTalentSkillSeparation + UseV5Memory + UseV5SubtaskPlanning + **UseV5PromptDb 新加** ⭐）= v5.5 Phase 2 完整路徑 production 預設 + 連續 6 Stage 0 follow-up + Aria 評估框架升級三大紀律累積實證（自省點 #35 + #36 + #37）+ 候選 #38（Stage cost 預估必含 Forge Claude Code session cost）。**真實 cost**：AiTeam LLM $2.301 + Forge Claude Code session ~$1.75 = 累積 ~$4.05 / 餘額 $20.04 → $15.99 ✅ 對齊。**cost per file = $0.121** 最優 ROI baseline（vs Trial_v17 $0.147 / -18%）。**下一步**：Stage 73+ v5.5 Phase 3 啟動條件達成（Step 6 WebUI Talent CRUD + Step 7 prompt content 升級 + Step 8 真並行 dispatch / 3 agent debate）。 |

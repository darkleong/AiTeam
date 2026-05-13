# Stage 64 Roadmap — v5 動態架構 production-ready 收口（Trial_v9 揭 7 議題 + Stage 63A errata）

> 目標版本：**v3.54.0**（minor — production-ready 工程收口 + 0 新架構引入）
> 狀態：規劃中
> 範圍：CLAUDE.md 注入儀式接通 v5 adapter path + Worker git/PR 整合 + Petra prompt 升級 + 環境細節修正 + Stage 63A spike notes errata
> 規模：M

---

## 戰略脈絡

**戰略大重評估拍板 — 路線 D 採用**（Christ 2026-05-13，Trial_v9 結案 + Aria WebSearch + code trace 雙確認後）：

Trial_v9 結案揭 12+ 議題後，原本估「Stage 64+ 全量遷移大投資」，經 Aria 跨輪 WebSearch 兩議題（Q1 `ChatClientAgent.instructions` 語意 / Q2 BuildSequential dispatch protocol）+ trace v4 `DevAgentService.cs:239-285` 既有 CLAUDE.md 注入儀式後，**工作量明顯下修**：

- **Q1 從「設計級不確定」變「工程級明確」** — 真實 root cause 是 v5 adapter 漏接 v4 既有的「備份 → 寫入 CLAUDE_<X>.md → run → finally 還原」儀式（v4 `DevAgentService` / `ReviewerAgentService` / `QaAgentService` 等 7 個 Worker 都有同 pattern）。修法 = adapter 層補同儀式，不是全量 prompt 重寫
- **Q2 從「需要 spike 驗 framework」變「PoC Petra prompt 太簡 + DB 驗證」** — 官方 sequential orchestration sample 證實 framework 自動 chain（3 agent fire / 1 個 `WorkflowOutputEvent` 終結）+ 我們 watch loop 結構與 sample 一致。嫌疑轉向 Petra Gemini Flash DecideAsync 太簡可能只回 1 capability
- **路線 D 採用 → Stage 64+ 預估從「大投資」下修為「M 級工程收口」**（本 Stage）+ Stage 65+ 後續（CLAUDE_*.md 從 partial → full 重寫 / production 切 default flag — 留 Trial_v10 驗證通過後另行）

**本 Stage 不含**：
- 8 CLAUDE_*.md 從 partial → full 重寫（FF 六十一其餘 8+ 議題留 Stage 65+）
- production 切 feature flag default true（留 Trial_v10 驗證通過後另行）
- Trial_v10 真實任務驗證（Stage + Trial 分開拍板紀律對齊 Stage 63B / Trial_v9 既有模式）

---

## 子項清單

### 1. CLAUDE.md 注入儀式接通 v5 adapter path（Q1 拍死級修法）

**修法位置**：`src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs` 的 `GetResponseAsync` 入口

**儀式步驟**（對齊 v4 `DevAgentService.cs:239-285` 既有實作 — 7 Worker AgentService 全部同 pattern）：
1. 備份 workspace 既有 `CLAUDE.md`（若存在）到記憶體
2. 從 `Resources/CLAUDE_<Worker>.md` 讀模板 → 寫入 `<workingDir>/CLAUDE.md`
3. 呼叫 `DispatchAsync(prompt, ct)`（既有 capability switch dispatch）
4. try-finally 還原原 `CLAUDE.md`（或刪除若原本不存在）

**capability → Resources/CLAUDE_<X>.md 對應表**（先 grep `src/AiTeam.Bot/Resources/` 確認檔名再寫 method）：
- `code_implementation` → `CLAUDE_Cody.md`
- `code_review` → `CLAUDE_Vera.md`
- `qa_testing` → `CLAUDE_Quinn.md`
- `documentation` → `CLAUDE_Sage.md`（grep 確認，Sage 是歸檔員可能不對 — 候選 CLAUDE_Doc.md 或其他）
- `requirements_extraction` → `CLAUDE_Rosa.md`
- `ui_design` → `CLAUDE_Demi.md`
- `release_publishing` → `CLAUDE_Rena.md`（grep 確認，Release Agent 角色名 / 模板檔名）

> 設計決策見「設計決策 1」段。

### 2. Worker 完成後 git commit/push/PR 接通（Q3 v4 既有 A 路）

**對齊範本**：v4 `DevAgentService.cs:149-152` 既有 pattern — `gitHubService.CommitAll(localPath, msg)` + `gitHubService.Push(localPath, branch)` + 後續 `OpenPullRequestAsync`

**修法位置**：`src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` 的 `StartAsync` 收尾段（`WorkflowOutputEvent` break 後）

**整合範圍**：
- 跑完整個 BuildSequential workflow 後，檢查 `WorkingDir` 是否有 git diff
- 有 diff → 呼叫既有 `GitHubService.CommitAll` + `Push`
- 全部 worker 完成後呼叫既有 `GitHubService.OpenPullRequestAsync` 建 PR

**branch name / commit message 來源**（設計決策 2）：
- branch：基於 `TaskGroupId` + 任務描述短摘自動產生（對齊 v4 既有 branch naming pattern — grep `DevAgentService` 確認）
- commit message：基於 Petra DecideAsync 完成後寫進 `PetraSession.Messages` 的 task summary 自動產生
- PR body：由 Petra 動態決策過程 + workers 完成 summary 組裝

**範圍邊界**：本 Stage 不重做 v4 既有 `DevAgentService.BuildPlanAsync` 那一整套 branch / plan / metadata 機制，只接通最小 commit + push + 開 PR。複雜 metadata（issue urls / fix loop branch resume / dev_plan）留 Stage 65+。

### 3. Petra DecideAsync prompt 升級（Q2 嫌疑收口）

**修法位置**：`src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` 的 `BuildPetraSystemPrompt` method（目前 inline 14 行太簡）

**升級內容**：
- 補三 trigger 條件具體判斷準則（範例任務 + 期望輸出）
- 對齊 `CLAUDE_Petra.md` 既有 trigger 描述段（不複製全文 221 行，提煉 capability dispatch 決策核心 ~30-50 行進 system prompt）
- 強化 Gemini Flash 解析穩定性：明示「只回 capability 序列、不要 markdown 包裹、不要解釋、用 `|` 分隔」紀律 + 反例

**先 grep 確認**：`CLAUDE_Petra.md` 真實 trigger 段範圍（filename + line range），避免憑印象抄錯段。

### 4. workspace volume mount 持久化（Q4 工程修法）

**修法位置**：`docker-compose.prod.yml` Bot service 段（既有 `GitHub__WorkspacePath: "/tmp/aiteam-workspace"` 見 docker-compose.prod.yml:45）

**修法**：加 volume mount 把 `/tmp/aiteam-workspace` 映射到 host 持久路徑（路徑 Forge 拍 — 對齊既有 postgres volume mount 命名 pattern）

**配套**：第一次 Bot 啟動時若 workspace 空 → 自動 git clone（位置：`ClaudeCodeService` caller 或 `PetraOrchestratorService.BuildSessionContext` 內 — 對齊 v4 既有 git clone 機制 grep 確認）

### 5. Gemini BaseUrl 預設值修為 v1beta（Q5 工程修法）

**修法位置**：`src/AiTeam.Bot/appsettings.json:40`

**修法**：`"BaseUrl": "https://generativelanguage.googleapis.com/v1"` → `"BaseUrl": "https://generativelanguage.googleapis.com/v1beta"`

**配套**：`docker-compose.prod.yml` 既有 `Gemini__BaseUrl: "${GEMINI_BASE_URL:-https://generativelanguage.googleapis.com/v1beta}"` env override 變多餘（保留作 escape hatch 或移除 Forge 拍）

### 6. Adapter token_logs 完整化 + transient retry（Q6 + Q7 並案）

**修法位置**：`src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs` 的 `GetResponseAsync`

**6a token_logs null-safe 完整化**：
- 既有 Trial_v9 commit `8dbe8f6` 已半修（`Usage` 不為 null 才寫）
- 補強：`Usage=null` 時仍寫一筆 cost=0 / token=0 dispatch 紀錄留 trace（觀測完整性）

**6b transient retry**：
- `DispatchAsync` 結果 `ClaudeCodeResult.Success=false` 且 `Output` 含 5xx pattern（`"Internal server error"` / `"503"` / `"502"` 等 — Forge 訂模式時 grep 真實 Anthropic 5xx 輸出格式）→ retry 最多 3 次 exponential backoff（1s / 2s / 4s）
- retry 機制以「adapter 層」實作，不下沉到 `ClaudeCodeService` 既有 subprocess wrapper（保持 v4 path 0 影響）

### 7. PetraOrchestratorService chain 驗證 + 必要時調整（Q2 真實原因收口）

**驗證項**（Forge 實作 1-6 完成後跑 Mock 驗證）：
- xUnit fixture 3 個（小 / 中 / 大需求 input），mock Gemini provider 回 capability 序列 → 驗 `DecideAsync` picks 解析 + workers chain 對齊
- xUnit smoke test：mock IClaudeCodeService 各 worker 回獨立 marker output → 跑通整個 BuildSequential workflow → 驗 `PetraSessionMessages` tool role 條數 = workers 數

**若 chain 真有問題**：深入 trace `LogWorkflowEvent` 真實 event sequence（official sample 證實 framework 自動 chain — 若仍只 fire 1 worker 反映在 Mock 驗證，Forge 自決 root cause + 補修，不需要 Aria 額外決策）

### 8. Stage 63A spike notes errata 改寫（文件 source of truth 修正）

**修法位置**：`docs/architecture/v5_charter/05_Stage_63A_Spike_Notes.md`「framework limitation (b)」段

**修法**：
- 揭真實 root cause（漏 `TurnToken` trigger + framework 自動 chain）
- 連帶 `docs/architecture/v5_charter/02_Architecture_Decision.md` / `04_Workflow_Wire.md` 對應段檢視（grep `limitation (b)` 字樣）若有 reference 此誤判結論一併更正

---

## 設計決策

### 1. CLAUDE.md 注入儀式實作位置 — adapter 層 per-call setup

**為什麼不在 `PetraOrchestratorService.StartAsync` 入口 setup**：v5 BuildSequential 一個 workflow 跑多個 capability 的 Worker，每個 Worker 用不同 CLAUDE_<X>.md。在 orchestrator 入口 setup 只能 cover 一份。

**為什麼 adapter 層安全**：BuildSequential 串行執行 Worker（official sequential orchestration sample 證實），不會兩個 Worker 同時操作同一 workspace 的 CLAUDE.md。

**try-finally 嚴格紀律**：對齊 v4 `DevAgentService.cs:281-285` finally 還原 pattern，避免 CLAUDE.md 洩漏被誤 commit 進 PR。

### 2. branch name / commit message 來源 — 自動產生 + Petra session 寫入

**branch name**：對齊 v4 既有 naming pattern（grep `DevAgentService` BuildPlanAsync 真實 branch 命名邏輯確認）— 候選 `feature/petra-<sessionId-short>` 或基於任務描述短摘

**commit message**：自動產生（template = `[Petra] <task summary>` + `<worker outputs>`）— 對齊 v4 既有 `plan.CommitMessage` 機制

**為什麼不讓 Petra LLM 動態決定**：Petra DecideAsync 已經吃 Gemini Flash cost 做 capability dispatch，再加一輪 commit message 決策對成本 / 穩定性都不利。Stage 64 走最小整合，動態決定 commit message 留 Stage 65+ 評估。

### 3. retry 設計 — adapter 層、5xx pattern matching、3 次 exponential backoff

**為什麼 adapter 層而非 ClaudeCodeService 層**：保持 v4 既有 ClaudeCodeService 0 修改（避免 v4 production path 受影響）。

**為什麼 pattern matching 而非 HTTP status code**：5xx 出現在 Claude Code CLI subprocess 內部 HTTP call（不是 C# 直接打 API），結果回到 `ClaudeCodeResult.Output` 是字串。Pattern matching 是唯一可行 detection。

**3 次 + exponential backoff**：對齊既有 Polly 慣例 + Trial_v9 揭實際 5xx 為 transient（單次重試即過），3 次足夠 cover transient burst。

### 4. Petra prompt 範圍 — 焦點段而非全文

**不用整份 `CLAUDE_Petra.md` 當 system prompt**：221 行太長 + 多數段針對 Petra 完整 orchestrator 角色，DecideAsync 只需 capability dispatch 決策核心 ~30-50 行段足夠。

**對 Gemini Flash 友善**：輕量模型 prompt 越聚焦準度越高。

### 5. feature flag 維持 false — 本 Stage 不切 production default

**對齊 Trial_v9 結案紀律**：v5 production 切換留 Trial_v10 驗證通過後另行（Stage / Trial 分開拍板）。

**Stage 64 結案 = feature flag 仍 default false / feature/v5-poc branch 仍未 merge**。

---

## 驗收情境

### 場景 1：CLAUDE.md 注入儀式 7 capability cover（xUnit Mock）

**觸發**：建 mock `IClaudeCodeService`，紀錄 `RunAsync` 被呼叫時 `<workingDir>/CLAUDE.md` 的存在性 + 內容 hash。
跑 7 個 fixture，每個對應一個 capability（`code_implementation` / `code_review` / ... / `release_publishing`），透過 `ClaudeCodeChatClientAdapter` dispatch。

**驗收**：
- 每個 fixture mock `IClaudeCodeService.RunAsync` 進入時 `CLAUDE.md` 存在 + 內容 = 對應 `Resources/CLAUDE_<X>.md`
- dispatch 完成後 `CLAUDE.md` 還原狀態 = 原始（不存在 → 刪除 / 存在 → 內容還原）
- finally restore 在 dispatch 拋 exception 時仍生效（test mock 拋一次 exception 驗證）

### 場景 2：Worker → git commit/push 路徑（xUnit Mock）

**觸發**：建 mock `GitHubService` 紀錄 `CommitAll` / `Push` / `OpenPullRequestAsync` 被呼叫次數 + 參數。跑 `PetraOrchestratorService.StartAsync` mock workflow（mock Petra 回 `code_implementation|code_review` 2 capability），mock `IClaudeCodeService` 跑後留下假 git diff 在 workingDir。

**驗收**：
- workflow 完成後 `CommitAll` 呼叫 1 次（合併兩 worker 改動單一 commit）+ `Push` 呼叫 1 次 + `OpenPullRequestAsync` 呼叫 1 次
- mock workingDir 無 diff 時 → 三 method 都 0 呼叫（無內容變更不誤建 PR）

### 場景 3：Petra DecideAsync 三 trigger fixture（xUnit）

**觸發**：mock `LlmProviderFactory.Create("PM")` 回固定 capability string fixture。

**fixture 1**（小需求）：mock 回 `"code_implementation"` → 驗 `picks` 1 個 = DevAgentService
**fixture 2**（中需求）：mock 回 `"code_implementation|code_review"` → 驗 `picks` 2 個 = [DevAgentService, ReviewerAgentService]
**fixture 3**（大需求 kickoff）：mock 回 `"code_implementation|code_review|code_implementation|code_review"` → 驗 `picks` 4 個

**驗收**：每個 fixture `picks.Count` + worker 順序 + 未知 capability skip warning log（fixture 4：mock 回 `"unknown_cap|code_review"` → picks 1 個 + warning log capture）

### 場景 4：Adapter transient retry（xUnit Mock）

**觸發**：mock `IClaudeCodeService.RunAsync` 第一次回 `ClaudeCodeResult(Success: false, Output: "Anthropic API: 503 Internal server error", ...)`，第二次回 `Success: true`。

**驗收**：
- adapter 自動 retry 一次 → 返回 success response
- 兩次 dispatch attempt 之間有 ≥ 1 秒 delay（exponential backoff 1s）
- token_logs 寫一筆紀錄（不寫兩筆 retry attempt 各一）

### 場景 5：Adapter null-safe token_logs（xUnit Mock）

**觸發**：mock `IClaudeCodeService.RunAsync` 回 `ClaudeCodeResult(Success: true, Output: "...", Usage: null)`。

**驗收**：`tokenLogService.LogCliUsageAsync` 仍呼叫一次（cost=0 / token=0 / dispatch trace 保留）。

### 場景 6：BuildSequential chain 多 worker dispatch 真實生效（xUnit Mock）

**觸發**：mock Petra DecideAsync 回 `"code_implementation|code_review"`。mock `IClaudeCodeService` 對 `RunAsync` / `RunReviewAsync` 各回獨立 marker output（`"[Cody marker]"` / `"[Vera marker]"`）。

**驗收**：
- `PetraSessionMessages` 表（in-memory DB）`tool` role 條數 = 2
- 兩條 content 分別含 `"[Cody marker]"` + `"[Vera marker]"`
- workflow 觀察到的 event 序列含 ≥ 2 個 `AgentResponseUpdateEvent`（每個 worker 至少 fire 一次 streaming update）

### 場景 7：Workspace volume mount 持久化（手動驗證）

**觸發**：
1. feature/v5-poc deploy 完成 + workspace 內有 git clone repo
2. `docker compose -f docker-compose.prod.yml up -d --force-recreate` 重啟 Bot
3. 重啟後 `docker exec aiteam-aiteam-bot-1 ls /tmp/aiteam-workspace`

**驗收**：workspace 內 git clone repo 內容保留（既有 `.git` / `CLAUDE.md` 還原狀態 / 源檔皆在）。

### 場景 8：Gemini BaseUrl 預設值修正（手動驗證）

**觸發**：
1. 暫時清空 `docker-compose.prod.yml` 的 `Gemini__BaseUrl` env override 段
2. Bot recreate
3. Dashboard 系統設定切 feature flag `UsePetraOrchestratorV5=true` + Petra Provider 切 Gemini Flash
4. Christ 從 Discord 送任務（最簡單「修個 typo」級需求）

**驗收**：
- Bot log 含 Petra DecideAsync 完成（capability 回傳正常解析）
- 0 `400 BadRequest "systemInstruction unsupported"` error log

### 場景 9：Stage 63A spike notes errata 改寫（文件審）

**觸發**：Aria 結案前審 `docs/architecture/v5_charter/05_Stage_63A_Spike_Notes.md`

**驗收**：
- limitation (b) 段含明確標記「結論修正：原判斷漏 TurnToken trigger 為 root cause」
- 連帶 02 / 04 文件 grep `limitation (b)` 字樣若有引用 errata 後結論 → 一併同步
- git log 含 commit 對應「docs(v5-charter): Stage 63A spike notes errata — limitation (b) root cause 修正」

### 場景 10：end-to-end smoke test（Forge 自驗 — Mock 全綠）

**觸發**：dotnet test 跑全套 xUnit fixture（含上述場景 1-6）

**驗收**：
- 全綠
- 0 build warning
- `dotnet build AiTeam.slnx` 0 error

---

## 技術約束

### 環境細節 source of truth（對齊 workflow_aria.md 第 7 條紀律）

- Bot Internal API port `5052` — 見 `docker-compose.prod.yml`
- X-Api-Key value — `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- container 名 — `aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
- Workspace config key — `GitHub:WorkspacePath`（既有，`docker-compose.prod.yml:45` 預設 `/tmp/aiteam-workspace`）
- Petra session 表 — `PetraSessions` + `PetraSessionMessages`（既有 Migration `Stage63PetraSessionTables` — feature/v5-poc 已 apply）
- PascalCase + quote 紀律（SQL 查詢欄位）

### Resources/CLAUDE_<X>.md 對應檔名（Forge 實作前先 grep `src/AiTeam.Bot/Resources/` 確認）

子項 1 capability → CLAUDE_<X>.md 對應表內標「grep 確認」的兩項（`documentation` / `release_publishing`），實作前先 `ls src/AiTeam.Bot/Resources/CLAUDE_*.md` + read Worker AgentService 既有 templatePath 紀錄真實檔名再寫 method。

### v4 既有 GitHubService API（子項 2 對齊範本）

- `GitHubService.CommitAll(string localPath, string message)` — 既有，line 236
- `GitHubService.Push(string localPath, string branchName)` — 既有，line 249
- `GitHubService.OpenPullRequestAsync(...)` — 既有，line 271

Stage 64 不新寫 git method，直接呼叫上述既有 API。

### branch / commit naming pattern（子項 2 對齊紀律）

Forge 實作前 grep `DevAgentService.cs` 既有 branch naming + `plan.CommitMessage` 機制，對齊既有 pattern；不自創新規範。

### feature/v5-poc branch 為 base

Stage 64 在 feature/v5-poc 之上 commit；main 0 動。Stage 64 結案 = feature/v5-poc 累積完整 production-ready commits 等 Trial_v10 驗證通過後另排 merge 計畫。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-13 | 規劃書建立（Aria）— Stage 64 = v5 動態架構 production-ready 收口（路線 D 採用拍板）。**戰略脈絡**：Trial_v9 揭 12+ 議題後 Aria 跨輪 WebSearch + code trace 雙確認 — Q1 從「設計級不確定」變「工程級明確」（v4 CLAUDE.md 注入儀式漏接）+ Q2 從「需要 spike framework」變「PoC Petra prompt 嫌疑 + DB 驗證」（official sequential orchestration sample 證實 framework 自動 chain）→ Stage 64+ 預估從「大投資」下修為「M 級工程收口」。**8 子項**：① CLAUDE.md 注入儀式接通 v5 adapter path（7 capability cover）② Worker 完成後 git commit/push/PR 接通（沿用 v4 GitHubService API）③ Petra DecideAsync prompt 升級（三 trigger 焦點段）④ workspace volume mount 持久化 ⑤ Gemini BaseUrl 預設 v1beta ⑥ Adapter token_logs null-safe + transient retry ⑦ PetraOrchestratorService chain 驗證 + 必要時調整 ⑧ Stage 63A spike notes errata 改寫。**5 設計決策**：① adapter 層 per-call CLAUDE.md setup（不在 orchestrator 入口）② branch / commit 自動產生（不讓 Petra 動態決定 — 留 Stage 65+）③ retry adapter 層 5xx pattern matching（不下沉 ClaudeCodeService）④ Petra prompt 焦點段而非全文（Gemini Flash 友善）⑤ feature flag 維持 false（production 切換留 Trial_v10）。**10 驗收場景**：7 xUnit Mock（CLAUDE.md 注入 / git 路徑 / Petra DecideAsync 三 trigger / Adapter retry / null-safe token_logs / BuildSequential chain 多 worker 真實 fire / smoke test）+ 2 手動（workspace 持久化 / Gemini endpoint）+ 1 文件審（spike notes errata）。**範圍邊界**：不含 8 CLAUDE_*.md partial → full 重寫（Stage 65+）/ 不含 production 切 default flag（Trial_v10 後另行）/ 不含 Trial_v10 真實任務驗證（Stage + Trial 分開拍板對齊 Stage 63B / Trial_v9 模式）。 |

# Stage 65 Roadmap — v5 動態架構 Trial_v10 揭 4 🟡 議題收口

> 目標版本：**v3.55.0**（minor — Trial_v10 4 🟡 議題收口 / production-ready 補強第二波）
> 狀態：規劃中
> 範圍：CLAUDE.md inject ritual 修根因（Claude Code CLI `--append-system-prompt`）+ Vera token_logs trace 修 + workspace volume permission init + Cody 廣範圍指令紀律
> 規模：M

---

## 戰略脈絡

**Trial_v10 ⭐⭐⭐ 業務級成功 + 路線 D 採用實證充分後第一個 production-ready 收口 Stage**：

- Trial_v10 揭 4 🟡 工程議題（0 🔴 戰略級新類型）— 全工程細節層次，補一輪就好
- Stage 64 計劃前 WebSearch 紀律 + Gate1 Tier 升級紀律首次實踐生效（×0.79 校準錨對齊 production-ready 補強 5 Stage 區間下緣）→ Stage 65 沿用同套紀律
- **議題 1 修法升級**：Stage 65 計劃前 WebSearch（Claude Code CLI headless mode + system prompt flags）揭真實 — `--append-system-prompt` / `--system-prompt` 是官方支援機制 → 從「修 Stage 64 inject ritual 時序漏洞補丁」升級為「直接拿掉 inject ritual 改用 CLI 參數修根因」（對齊「修根因 > 補丁」哲學）
- **範圍邊界刻意收緊**（對齊 infinite loop pattern 教訓）：
  - 只動 v5 adapter（ClaudeCodeChatClientAdapter）+ Stage 64 docker-compose
  - **不動 v4 既有 7 worker AgentService 的 CLAUDE.md inject ritual**（v4 production 跑得好好的，動了風險高 + 範圍擴 M → L）
  - v4 inject ritual 統一重構留 Stage 66+ 評估（feature/v5-poc merge to main 時順便處理）
  - 不含 8 CLAUDE_*.md partial → full 重寫（FF 六十一其餘 8+ 點 Stage 65+ 評估動工）

**對 Trial_v11 預期效果**：
- 議題 1 修 → 0 CLAUDE.md 污染 commit
- 議題 2 修 → token_logs cost tracking 100% 準確（Cody + Vera 都寫）
- 議題 3 修 → docker compose recreate 後 0 手動 chown
- 議題 4 修 → Cody 對廣範圍指令真實 cover 5/5 範圍

---

## 子項清單

### 1. CLAUDE.md inject ritual 修根因 — 改用 `--append-system-prompt`（議題 1）

**WebSearch 揭官方真相**（[Claude Code Docs - Run Claude Code programmatically](https://code.claude.com/docs/en/headless)）：
- `--system-prompt`：完全取代 default system prompt
- `--append-system-prompt`：在 default 後追加（適合本案 — 保留 Claude Code default tool/git 紀律 + 追加 Worker-specific 紀律）

**修法位置**：`src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs` `GetResponseAsync`

**修法**：
- 移除 CLAUDE.md inject ritual 整段（backup / write template / try-finally restore）
- 改：讀 `Resources/CLAUDE_<X>.md` 模板內容 → 透過新 systemPrompt 參數傳給 `IClaudeCodeService` → ClaudeCodeService 組 `--append-system-prompt` CLI 參數
- workspace `CLAUDE.md` 0 動 → 完全避開 commit 污染議題

**IClaudeCodeService API 修法路線**（Forge 自決 A vs B）：
- **路線 A**：`IClaudeCodeService` 既有 6 method 簽名加 `string? systemPrompt = null` 參數（default null 不影響 v4 caller）— v5 adapter 傳 systemPrompt / v4 caller 0 動。內部 `BuildArgs` 加 conditional `--append-system-prompt` flag。**Aria 推薦 — 統一 API + 對 v4 無影響**
- **路線 B**：`IClaudeCodeService` 加新 method `RunWithSystemPromptAsync(...)` — v5 adapter 用新 method / v4 用既有。隔離強但 method 數量增加

**CLI 參數帶 multi-line 中文系統 prompt 的細節**（Forge 自決）：
- 直接 `--append-system-prompt "<content>"` shell escape（中文 + 多行 + 特殊字元風險）
- 或 stdin pipe（對齊既有 RunCoreWithImagesAsync stream-json 模式）
- 或 temp file（write to /tmp/<guid>.md → `--append-system-prompt-file <path>` 如果有此 flag — Forge 實作前 grep CLI help 確認真實可用 flag）

### 2. Vera token_logs 寫入 trace 修（議題 2）

**Trial_v10 揭事實**：Cody token_logs $1.024 / Petra Gemini $0.002 / **Vera 0**。真實餘額消耗 $1.07 vs 系統 $1.026 = $0.044 blind spot（4.3%）。

**Aria 預掃可能 root cause**（Forge spike 自查 + 修）：
- Adapter `tokenLogService.LogCliUsageAsync` try-catch 內 silent caught exception（看 Bot log 找 warning）
- ClaudeCodeService `RunReviewAsync` readOnly path subprocess 真實沒 parse Usage（CLI 結果 `result.Usage = null`）
- 或 LogCliUsageAsync 接收 zero TokenUsage 後另有 early return path（[TokenLogService.cs:35](src/AiTeam.Bot/Services/TokenLogService.cs:35) 已知 `if (usage is null) return` — 但 adapter 已 fallback non-null zero TokenUsage 應跳過此早返）

**Forge 修法**：
- 先 trace Bot log Trial_v10 期間 Vera adapter 寫入路徑（找 warning / exception trace）
- 揭真實 root cause 後修 → 確保 Vera 走 RunReviewAsync 後 token_logs 必寫一筆（即便 zero / IsEstimated）
- 補 xUnit test cover Vera 寫入 path

### 3. Workspace named volume permission init（議題 3）

**Trial_v10 揭事實**：Stage 64 docker-compose `aiteam-workspace-data` named volume 預設 root-owned（0755）→ Bot 跑 `appuser` (uid 999) 寫不進去 → Aria Trial_v10 期間手動 `docker exec --user root chown -R appuser:appuser /tmp/aiteam-workspace` 才讓 Trial 跑下去。

**修法位置**（Forge 自決路線）：
- **路線 A**：`src/AiTeam.Bot/Dockerfile` 加 `RUN mkdir -p /tmp/aiteam-workspace && chown -R appuser:appuser /tmp/aiteam-workspace`（image build 時 init）— 但 named volume mount 會覆蓋 image dir owner
- **路線 B**：docker-compose.prod.yml 加 entrypoint script 包現有 ENTRYPOINT 前先 chown
- **路線 C**：用 init container 先 chown 再啟動 Bot
- **Aria 推薦路線 B**（最簡 + 對齊 docker-compose level config + 不動 Dockerfile build）

**驗證**：Trial_v11 前置條件「`docker compose -f docker-compose.prod.yml up -d --force-recreate`」後 0 手動 chown / Bot 啟動就能寫 workspace。

### 4. Cody 廣範圍指令紀律（議題 4 — 路線 A 由 Cody 自決，不加 Petra LLM 來回）

**Trial_v10 揭事實**：任務原文寫「整個 Dashboard 凡是有錯誤處理的地方（系統設定、操作中心、Agent 設定、規則管理之類）」5 範圍，Cody 改 4/5（漏 InteractionCenter 操作中心）。

**修法位置**：`src/AiTeam.Bot/Resources/CLAUDE_Cody.md`

**修法**（補一段「廣範圍指令處理紀律」）：
- 任務原文含「整個 X」/「所有 Y」/「凡是 Z」/「之類」等廣範圍措辭時：
  - 步驟 1：先 grep / list 真實範圍對應檔案 / 頁面（如 Dashboard 任務 → `ls src/AiTeam.Dashboard/Components/Pages/`）
  - 步驟 2：產出範圍對照表（Issue # / 任務點 / 對應檔案 / 已 cover ✓ vs 待 cover ⏳）
  - 步驟 3：實作完成後對照表 100% cover 才宣告完成
- 對齊既有「Dev_plan 結構規範」段（CLAUDE_Cody.md 既有 — Stage 61 立）+「對應 Issue 對照表」必含結構

**範圍邊界**：本紀律不擴 Petra prompt（議題 4 路線 A 拍板 — Cody 自決，少一層 LLM 來回 cost）。Petra DecideAsync 仍走「依任務規模 dispatch capability」現狀 default。

---

## 設計決策

### 1. 議題 1 修法選 `--append-system-prompt` 而非 `--system-prompt`

**為什麼 append 而非 replace**：保留 Claude Code default system prompt（含 git tool / Bash tool / Edit tool 通用紀律）+ 追加 Worker-specific 紀律。`--system-prompt` 完全取代會掉預設工具紀律。

### 2. 範圍邊界刻意不動 v4 既有 7 worker AgentService

**為什麼**：
- v4 production 7 worker 各自 inject CLAUDE.md ritual 跑得好好的（Trial_v6/v7/v8 揭的 issues 都不是 inject 路徑問題）
- 動 v4 7 worker = 範圍從 M → L + 需 regression test 完整跑（v4 production-ready Stage 風險高）
- 對齊「修根因 > 補丁」精神同時對齊「不要把太多事塞進一個 Stage」紀律

**Stage 66+ 評估時機**：feature/v5-poc merge to main 時順便重構 v4（一次性處理）。

### 3. IClaudeCodeService 路線 A 既有 method 加 default null 參數

**為什麼**：v4 caller 0 影響（C# default parameter 對既有 caller 透明）+ 統一 API（不必 v4/v5 兩套 method）+ 對齊 Stage 61 `RunReadOnlyAsync` 加 `int? maxTurns = null` 同 pattern。

### 4. workspace volume permission 路線 B（compose entrypoint）而非 Dockerfile

**為什麼**：named volume mount 覆蓋 image dir owner（Dockerfile chown 在 image build 時 init 但 mount 後失效）+ entrypoint chown 確保每次 container start 都 init permission（容錯性高）。

### 5. Cody 廣範圍指令紀律放 CLAUDE_Cody.md 而非 Petra prompt

**為什麼**：Cody 直接接觸任務原文 + 自己最懂 codebase 範圍 + 少一層 Petra LLM 來回 cost（Gemini Flash 雖免費但 latency 累積）。Petra 角色維持「dispatch capability」單一職能。

---

## 驗收情境

### 場景 1：CLAUDE.md inject ritual 完全移除 + `--append-system-prompt` 真實生效（xUnit + 手動）

**xUnit Mock**：
- mock `IClaudeCodeService.RunAsync` 紀錄收到的 systemPrompt 參數
- 跑 7 capability fixture，期望每個 capability dispatch 時 `systemPrompt` 含對應 `CLAUDE_<X>.md` 內容
- workspace `CLAUDE.md` 在 dispatch 前後 **完全 0 動**（不應該被 read / write / delete）

**手動驗證**：Trial_v11 跑完後 `git diff HEAD~1` PR 0 包含 `CLAUDE.md` 改動（vs Trial_v10 PR #372 含 -172/+113 CLAUDE.md 污染）

### 場景 2：Vera token_logs 真實寫入（xUnit Mock + Trial_v11 SQL）

**xUnit Mock**：
- mock `IClaudeCodeService.RunReviewAsync` 回 `Success: true / Usage: <real TokenUsage>`
- 跑 adapter dispatch capability=`code_review`
- 驗 mock `TokenLogService.LogCliUsageAsync` 被呼叫一次 + 參數 agentName="Vera" / model 對應 / usage 非 null

**Trial_v11 SQL**：
```sql
SELECT "AgentName", COUNT(*) FROM token_logs
 WHERE "CreatedAt" >= '<Trial_v11 開跑時間>'
 GROUP BY "AgentName";
```
期望結果含 `Vera` 一行（vs Trial_v10 只 PM + Cody）。對照真實餘額消耗 vs 系統累計 cost 差距 < 1%。

### 場景 3：Workspace volume permission 自動 init（手動驗證）

**觸發**：
1. `docker volume rm aiteam-workspace-data`（清掉現有 volume 模擬首次部署）
2. `docker compose -f docker-compose.prod.yml up -d --force-recreate`

**驗證**：
- `MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 ls -la /tmp/aiteam-workspace` → owner 顯示 `appuser:appuser`（vs Trial_v10 預設 root-owned）
- 觸發 PetraOrchestrator.StartAsync（任意小任務 / 或直接 Trial_v11 開跑）→ Bot log `Petra BuildSessionContext CloneOrPull 完成` 0 permission denied error

### 場景 4：Cody 廣範圍指令紀律真實生效（Trial_v11 真實任務）

**觸發**：Trial_v11 沿用 Trial_v6-v10 同 prompt（Dashboard 錯誤處理打磨任務 — 含「整個 Dashboard 凡是有錯誤處理的地方」廣範圍措辭）

**驗證**：
- Cody dispatch 後 PR 範圍 cover **5/5**（QuickCommandCard / SystemSettings / AgentSettings / RuleFormDialog / **InteractionCenter**）
- PR body 或 commit message 含「範圍對照表」段（5 個範圍逐項 ✓）
- vs Trial_v10 4/5 cover

### 場景 5：v4 既有 7 worker CLAUDE.md inject ritual 0 regression（手動驗證 + dotnet test）

**觸發**：
- `dotnet build AiTeam.slnx` + `dotnet test`（既有 v4 worker xUnit + Stage 64 v5 xUnit + 新 Stage 65 xUnit 全綠）
- v4 production path（feature flag `UsePetraOrchestratorV5=false`）跑既有 Mock 場景 → 應 0 regression

**驗證**：
- 既有 v4 worker `templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_<X>.md")` + `await File.WriteAllTextAsync(claudeMdPath, ...)` + try-finally restore 全段 0 動
- IClaudeCodeService 既有 6 method 簽名加 `string? systemPrompt = null` default null → v4 caller compile 0 error

---

## 技術約束

### 環境細節 source of truth（對齊 workflow_aria.md 第 7 條紀律）

- Bot Internal API port `5052` / X-Api-Key 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- container 名 `aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
- workspace path `/tmp/aiteam-workspace`（feature/v5-poc named volume `aiteam-workspace-data`）
- Petra session 表 `petra_sessions` + `petra_session_messages`（snake_case）+ 欄位 PascalCase quote 必加
- token_logs 表既有（`AgentName` / `Model` / `TotalCostUsd` / `IsEstimated` / `CreatedAt` PascalCase quote）

### Resources/CLAUDE_<X>.md 對應（對齊 Stage 64 子項 1 既有對應表）

`code_implementation` → `CLAUDE_Cody.md` / `code_review` → `CLAUDE_Vera.md` / `qa_testing` → `CLAUDE_Quinn.md` / `documentation` → `CLAUDE_Sage.md` / `requirements_extraction` → `CLAUDE_Rosa.md` / `ui_design` → `CLAUDE_Demi.md` / `release_publishing` → null（路線 A skip）

### 範圍邊界明寫不動

- v4 既有 7 worker AgentService（DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService / RequirementsAgentService / DesignerAgentService / Pm/PmReviewService）的 CLAUDE.md inject ritual 段 — 留 Stage 66+ merge 時統一重構
- 8 CLAUDE_*.md partial → full 重寫（FF 六十一其餘 8+ 點）— 留 Stage 66+ 評估
- production 切 `Workflow:UsePetraOrchestratorV5` default true — 留 Trial_v11 驗證通過後另行
- Trial_v11 真實任務驗證 — Stage 跟 Trial 分開拍板對齊 Stage 63B / Trial_v9 / Stage 64 / Trial_v10 既有模式

### feature/v5-poc branch 為 base

Stage 65 在 `feature/v5-poc` 之上 commit；main 0 動。Stage 65 結案 = feature/v5-poc 累積 production-ready commits 等 Trial_v11 驗證通過後另排 merge 計畫一次性 bump。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-14 | 規劃書建立（Aria）— Stage 65 = Trial_v10 揭 4 🟡 議題收口（v5 動態架構 production-ready 補強第二波）。**戰略脈絡**：Trial_v10 ⭐⭐⭐ 業務級成功 + 路線 D 採用實證充分後第一個 production-ready 收口 Stage / 4 🟡 工程議題收口 / 0 🔴 戰略級新類型。**Stage 65 計劃前 WebSearch 紀律真實生效**（workflow_aria.md 第三節 A 第 9 條第二次實踐）— 議題 1 修法從「補丁修 inject ritual 時序漏洞」升級為「修根因改用 Claude Code CLI `--append-system-prompt` 完全拿掉 inject ritual」（對齊「修根因 > 補丁」哲學 + WebSearch 揭官方真相）。**4 子項**：① CLAUDE.md inject ritual 修根因 — 改用 `--append-system-prompt`（IClaudeCodeService 既有 6 method 加 systemPrompt default null 參數路線 A 推薦）② Vera token_logs trace 修（Forge spike 揭真實 root cause + 補 xUnit test cover）③ Workspace named volume permission init（compose entrypoint chown 路線 B 推薦）④ Cody 廣範圍指令紀律（CLAUDE_Cody.md 補「廣範圍指令處理紀律」段 + 範圍對照表 — 路線 A Cody 自決 不加 Petra LLM 來回 cost）。**5 設計決策**：① append 而非 replace 保留 default 工具紀律 ② 範圍邊界刻意不動 v4 既有 7 worker AgentService inject ritual（留 Stage 66+ merge 時統一重構） ③ IClaudeCodeService 路線 A 既有 method 加 default null 參數對齊 Stage 61 maxTurns 同 pattern ④ workspace volume permission 路線 B compose entrypoint 而非 Dockerfile（named volume mount 覆蓋 image dir owner） ⑤ Cody 廣範圍紀律放 CLAUDE_Cody.md 而非 Petra prompt（少一層 LLM 來回 cost + Cody 直接接觸任務原文最懂 codebase 範圍）。**5 驗收場景**：1 CLAUDE.md inject ritual 完全移除 + `--append-system-prompt` 真實生效（xUnit + Trial_v11 PR diff 0 CLAUDE.md 改動）/ 2 Vera token_logs 真實寫入（xUnit Mock + Trial_v11 SQL）/ 3 Workspace volume permission 自動 init（手動 docker volume rm + recreate 後 owner 顯示 appuser）/ 4 Cody 廣範圍指令紀律真實生效（Trial_v11 PR cover 5/5 + 範圍對照表）/ 5 v4 既有 7 worker CLAUDE.md inject ritual 0 regression（dotnet test 全綠 + v4 production path 0 影響）。**範圍邊界**：不動 v4 既有 7 worker inject ritual / 不含 8 CLAUDE_*.md full 重寫 / 不切 production default flag / 不含 Trial_v11 真實任務驗證（Stage + Trial 分開拍板對齊既有模式）。**Trial_v11 預期效果**：0 CLAUDE.md 污染 commit + token_logs 100% cost tracking + 0 手動 chown + Cody 5/5 範圍 cover。 |

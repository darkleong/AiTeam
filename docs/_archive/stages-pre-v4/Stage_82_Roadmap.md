# Stage 82 Roadmap — Quinn outputLen 修根因 + Stage 81 子項 8 砍 + Trial_v25 三 🟡 議題收口

> 對應系統版本：v3.74.0（Stage 82 完成後）
> Stage 規模：**S**（修根因 + 砍錯方向 + 順手議題 / 預估 1-3 子項 / 0 燒 AiTeam 餘額 — Aria + Forge session 走 Claude Code subscription）
> 觸發來源：Trial_v25 v2.0 結案揭 🔴 戰略級 finding（Stage 81 子項 8 修法錯方向）+ 2 🟡 順手議題
> 戰略意義：**「修根因 > 補丁」紀律深度實踐** — Stage 81 假設 vs Trial_v25 真實根因不同方向，Stage 82 砍錯修法 + 修對地方，讓 Trial_v26 補驗 Stage 81 動態 replan + Stage 82 修法雙驗證

---

## 戰略脈絡

### Trial_v25 揭真實根因

Stage 81 子項 8 假設「Quinn CLI session 結束無 final text turn → `result` JSON 欄位空 → outputLen=0」 → 修法 `ClaudeCodeChatClientAdapter` qa_testing capability **user prompt prepend** `BuildQaSummaryEnforceSection` 提示。

Trial_v25 Round 2（Sonnet 5-subtask）真實揭：

| Worker | InputTokens | OutputTokens | TotalCostUsd | outputLen |
|---|---|---|---|---|
| Cody (1) | 62 | 10943 | $0.83 | 3020 ✅ |
| Vera (1) | 9 | 7130 | $0.25 | 1397 ✅ |
| Cody (2 fix) | 6 | 1357 | $0.13 | 917 ✅ |
| Vera (2 reverify) | 6 | 898 | $0.12 | 334 ✅ |
| **Quinn** | **22** | **27016** | **$1.04** | **0** ❌ |

Quinn 燒 27K output tokens（有大量輸出 / 不是「無 final text turn」單純假設）。對照 ClaudeCodeService 真實邏輯（`ClaudeCodeService.cs:530-565 ParseJsonOutput`）+ Claude Code CLI `--output-format json` 機制 + Trial 真實 token usage → **真實根因方向**：

1. `--output-format json` mode CLI 只 emit 最後一筆 `type="result"` row，`result` 欄位 = CLI session 結束時的 final assistant text turn
2. Quinn 跑 QA 大量 tool calls（Read tests + Bash dotnet test + Write 新 test）— 27K output 大部分是 **tool input/output**（檔案讀寫 + bash 命令）
3. CLI session 結束時 **直接 tool calls 跑完就結束 / 沒寫 final assistant text turn** → `result` 欄位 = empty string → outputLen=0
4. Stage 81 修法 user prompt prepend 提示「請輸出 final markdown 摘要」— Quinn **ignore prepend 提示繼續只跑 tool calls**（user prompt section 對 LLM follow 紀律不足 / 不是 system prompt 強制）

### 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

Claude Code CLI `--output-format json` vs `stream-json` 兩模式差異 + 「tool calls only no final text → result 空」edge case 業界 reference：

- `--output-format json`：**最終 JSON 一筆**（structured data + metadata），`type="result"` row `result` 欄位 = final assistant text。**Tool calls only no final text turn → result 欄位 empty**（WebSearch 確認「undocumented edge case」）
- `--output-format stream-json`：**NDJSON line-delimited**，每 message 一行 — 含 `assistant` (text with content blocks) / `tool_use` / `tool_result` / `result` (metadata 總結) 多 message type
- **修根因可行路線**（Forge spike 揭真實對應 AiTeam ParseJsonOutput 邏輯後拍）：
  - 🥇 **路線 A** — `--output-format stream-json` + 累積所有 `assistant` text content blocks 拼出 final output（cover tool calls 中間 turn 的 text）/ ParseJsonOutput 改 line-by-line accumulate / 對 v4+v5 path 0 regression（既有 `--output-format json` 退役）
  - 🥈 **路線 B** — 保留 `--output-format json` + ParseJsonOutput fallback：若 `result` empty 嘗試從 raw JSON 其他欄位（如 `subtype="error_during_execution"`/`session_id`/`tool_use_count`）撈 fallback 摘要（簡單 / 但仍缺 Quinn 真實 QA 工作內容）
  - 🥉 **路線 C** — 強化 system prompt enforce（移 prepend 從 user prompt → `--append-system-prompt` 段 / 或 CLAUDE_Quinn.md 本身紀律強化）/ 但 LLM follow 紀律不可控 / 跨 LLM provider 不穩定（已驗 Stage 81 user prompt prepend 對 Quinn 失效）

**Aria 傾向路線 A** — 對齊「修根因 > 補丁」精神 / cover Quinn + 未來其他 tool-heavy capability（如 documentation Sage / 未來 code_review 若 Vera tool calls 多）/ 不依賴 LLM follow 紀律 / stream-json 是 Claude Code CLI 業界 reference 模式。Forge spike Plan Mode 階段揭真實對應 AiTeam ParseJsonOutput 升級成本後再拍。

Sources（Forge 補查用）：
- [What is --output-format in Claude Code (ClaudeLog)](https://claudelog.com/faqs/what-is-output-format-in-claude-code/)
- [Claude Code stream-json: the output format that changes everything (Background Claude)](https://backgroundclaude.com/blog/stream-json)
- [claude-agent-sdk-go cli-protocol.md (Roasbeef)](https://github.com/Roasbeef/claude-agent-sdk-go/blob/main/docs/cli-protocol.md) — NDJSON message schema
- [Run Claude Code programmatically (Anthropic Docs)](https://code.claude.com/docs/en/headless)

### 規劃前置 grep 結論

| 觸點 | 真實檔案 + line | 真實狀態 |
|---|---|---|
| Quinn outputLen=0 收 result 邏輯 | `src/AiTeam.Bot/Agents/ClaudeCodeService.cs:530-565 ParseJsonOutput` | `--output-format json` + last `type="result"` row `result` 欄位 string |
| Stage 81 子項 8 prepend 落點 | `src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs:107-115` | qa_testing capability user prompt prepend `BuildQaSummaryEnforceSection` |
| BuildArgs `--output-format json` flag | `ClaudeCodeService.cs:497 + 519` | 4 worker method 都用 `--output-format json` |
| SubtaskPlanParser strip 邏輯 | `src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs:91-113 TryParse` | 只 `StripCodeFence`（markdown ```json ```）不 strip conversational preamble |
| Petra LLM token logging | `PetraOrchestratorService.cs` 5 worker token_logs 進 / Petra 自己 LLM call 0 token_logs row | LlmProviderFactory.Create("PM") path 沒掛 TokenLogService（要查 LlmProviderFactory + AnthropicProvider + GeminiProvider 三檔對齊 worker dispatch token logging pattern）|

---

## 子項清單

### 子項 1 ⭐ Quinn outputLen=0 修根因（核心 🔴）

**Forge spike Plan Mode 揭真實 + 拍路線 A vs B**：

1. Spike Claude Code CLI `--output-format stream-json` 真實 NDJSON 行為（**Forge 不必開新 spike 燒 cost** — 用本地 `claude --output-format stream-json -p "test"` subprocess 跑一次看真實 output / 對齊本地 dev 環境驗）
2. 評估 ParseJsonOutput 升級成本（line-by-line accumulate `assistant` text content blocks vs last `type="result"` row）
3. Plan Mode 拍路線 A 或 B（Christ 收 plan 再拍 / Aria gate1 endorse 哪條路線）
4. 實作對應修法
5. xUnit test cover：
   - tool calls only no final text turn → output 非空（accumulate tool result summary）
   - normal text-only turn → output 對齊既有
   - error path → result.Success=false 對齊

**結合 Stage 81 子項 8 砍**（同子項合併不分開）：
- 移除 `ClaudeCodeChatClientAdapter.cs:107-115` qa_testing prepend `BuildQaSummaryEnforceSection` 整段
- 移除 `BuildQaSummaryEnforceSection` method body（grep `BuildQaSummaryEnforceSection` 全 codebase 確認 0 caller 後刪）
- 對應 xUnit test 移除（Stage 81 加的）

### 子項 2 順手 🟡 Petra Anthropic provider token_logs 漏寫

**Forge spike grep `LlmProviderFactory.Create("PM")` 真實 caller + GeminiProvider/AnthropicProvider 真實 Usage 回傳機制**：

1. grep `PetraOrchestratorService.cs:227/407/474`（Trial_v22+v23+v24+v25 真實 caller 3 點）真實 LLM call 路徑
2. 對照 worker dispatch token_logs 寫入 pattern（`ClaudeCodeChatClientAdapter.cs:194-197 LogCliUsageAsync` + Stage 65 finally 紀律）
3. 修法落點：
   - 🥇 LlmProviderFactory 出口包 TokenTrackingProvider（既有 `TokenTrackingProvider.cs` 已有 pattern）— PM agent path 自動掛 token logging
   - 🥈 PetraOrchestratorService 三 call site 各自加 LogPetraUsage 包覆（重複代碼）
4. xUnit test cover：Petra Anthropic + Petra Gemini 兩 provider path token_logs 進 row + PetraSessionId 正確透傳

### 子項 3 順手 🟡 SubtaskPlanParser conversational preamble fallback 強化

**修法**：`SubtaskPlan.cs:91-113 TryParse` 升級 strip 邏輯：

1. 既有 `StripCodeFence` 不變（cover ```json ``` markdown fence pattern）
2. 新增 `StripPreambleAndPostamble`：strip 前 → 找 first `{` index + last `}` index → substring extract pure JSON object（cover Anthropic Haiku conversational preamble + 未來其他 LLM provider 健談行為）
3. Order：trim → StripCodeFence → StripPreambleAndPostamble → JsonSerializer.Deserialize
4. xUnit test cover：
   - 純 JSON 物件 → 對齊既有
   - markdown code fence 包 JSON → strip fence 對齊既有
   - 對話前綴文 + JSON → strip preamble pass
   - 對話前綴 + markdown fence + JSON → 雙 strip pass
   - 完全沒 JSON object → error="raw is empty" 或 "subtasks missing"

對齊 Petra 純 JSON 紀律（system prompt 要求純 JSON），但**底層解析器加防呆**對齊「修根因 > 補丁」紀律 — Petra LLM 不一定 follow 純 JSON 紀律時 parser 仍可 robust 處理。

---

## 設計決策

### D1 — 路線 A 動 ClaudeCodeService BuildArgs `--output-format json` → `stream-json` 全面切換 vs 只 qa_testing capability

**問題**：路線 A 升級 ParseJsonOutput cover stream-json，要不要對全 4 worker method（RunAsync / RunReviewAsync / RunQaAsync / RunReadOnlyAsync）都切 stream-json，還是只 qa_testing path 切？

**Aria 傾向：全 4 worker 一起切**（對齊一致性紀律 + Cody/Vera/Sage 未來也可能踩 tool-heavy edge case）。但 Forge spike 階段揭實作成本 + 既有 v4+v5 0 regression 風險後再拍。

### D2 — 子項 2 修法落點路線 A vs B

**Aria 傾向 🥇 LlmProviderFactory 出口包 TokenTrackingProvider** — 既有 pattern 重用 / 一次修對 PM + 未來其他 agent path 自動 cover / 重複代碼 -50%。

### D3 — 子項 3 對齊 Petra LLM JSON 紀律 vs 解析器 robust 防呆

**Aria 拍板：兩者並行**：
- Petra system prompt 維持「純 JSON only no preamble」紀律（既有不動）
- 解析器加 robust strip preamble 防呆（修根因 — 對齊未來 Provider 切換時 LLM 健談行為的兜底）

不互斥 / 雙保險。

### D4 — 子項 1 砍 Stage 81 子項 8 prepend 是否影響 Stage 81 既有 commit 結案

**不影響**：Stage 81 結案 commit `92eadb9` 已 push + Roadmap v2.0 實作紀錄已寫。Stage 82 砍 prepend 屬「修根因 > 補丁」延伸 — Stage 82 結案紀錄段明確標「砍 Stage 81 子項 8 假設錯方向」+ commit message 對 Stage 81 reference 清楚（不 amend Stage 81 既有 commit / Stage 82 新 commit 砍）。

---

## 驗收情境

### 場景 A：Quinn QA 真實 dispatch outputLen > 0（核心 🔴 驗收）

**怎麼觸發**：
1. Bot deploy Stage 82 commit 後
2. SQL `Workflow:UsePetraOrchestratorV5=true` + `UseTalentSkillSeparation=true`（既有 production active default）
3. curl `/internal/ceo/command` 派 Trial_v15_body.json prompt（Dashboard 錯誤處理打磨 / 沿用 Trial_v22-v25 baseline）
4. 等 chain 跑完（Cody → Vera → Quinn 或 Sonnet 拆的 5-subtask）

**怎麼驗證**：
- Bot log `PetraOrchestrator v5.5 自管 chain dispatch 完成 Level=N/N subtaskId=N talent=Quinn parallel=False outputLen=>0`（**非 0**）
- PR body `[Quinn|qa_testing|outputLen=N]` N > 0 + 內含 Quinn 真實 QA 工作摘要（Read tests + Bash dotnet test + Write 新 test 對應內容）
- SQL `token_logs WHERE AgentName='Quinn'` OutputTokens > 0 + outputLen 比例對齊（OutputTokens : outputLen ≈ 對齊既有 Cody/Vera 比例 ~3-4x 而非 0）

### 場景 B：v4 既有 path 0 regression（路線 A 全面切 stream-json 預警）

**怎麼觸發**：
1. SQL `Workflow:UsePetraOrchestratorV5=false`（暫切 v4 path）
2. curl `/internal/ceo/command` 派同 prompt
3. 等 v4 串行 chain 跑完

**怎麼驗證**：
- Bot log v4 path 4 worker（Cody/Vera/Quinn/Sage）outputLen 全非 0
- PR 真開業務內容對齊 Trial_v6 baseline
- SQL `token_logs` 4 worker row 完整

（這個場景 Forge spike Plan Mode 揭路線 A 對 v4 影響後 / Aria gate1 endorse 後才必跑）

### 場景 C：Petra Anthropic + Petra Gemini token_logs row 進

**怎麼觸發**：
1. Trial_v26 第一輪用 Anthropic Sonnet 4.6（current production active default）
2. 跑完 chain
3. Dashboard Agent 設定切回 Gemini Flash + reload-cache
4. Trial_v26 第二輪用 Gemini Flash
5. 跑完 chain

**怎麼驗證**：
- SQL `token_logs WHERE AgentName='PM'` 兩輪 PetraSession 都有 row + Model=claude-sonnet-4-6 / gemini-2.5-flash 對應
- TotalCostUsd 非 0
- PetraSessionId 正確透傳

### 場景 D：SubtaskPlanParser 對話前綴 + JSON robust 解析

**怎麼觸發**：xUnit unit test 直接 cover（不必 production 真實 fire — Anthropic Sonnet 4.6 純 JSON OK）

**怎麼驗證**：
- xUnit `SubtaskPlanParserTests` 5 case 全綠：
  - 純 JSON object → pass
  - markdown code fence JSON → pass
  - 對話前綴文 + JSON → pass（新加）
  - 對話前綴 + markdown fence + JSON → pass（新加）
  - 完全沒 JSON object → error=明確訊息

### 場景 E：Stage 81 子項 8 prepend 砍乾淨

**怎麼觸發**：grep + 看 code

**怎麼驗證**：
- `grep -rn "BuildQaSummaryEnforceSection" src/` 0 hit（method body + caller 都刪）
- `grep -rn "QA 報告紀律" src/AiTeam.Bot/Orchestration/Petra/` 0 hit
- `ClaudeCodeChatClientAdapter.cs:107-115` qa_testing prepend 整段移除
- 對應 xUnit test（Stage 81 加的 qa_testing prepend test）一併移除

---

## 技術約束

### 紀律對齊

- **修根因 > 補丁**：Stage 82 砍 Stage 81 子項 8 prepend 錯方向 + 修對地方（path 仍待 Forge spike 揭真實）/ 對齊「修根因連 skill / 自省點原文一併校正」紀律
- **不污染 CLAUDE_Quinn.md 跨專案守則**：Stage 81 紀律延續 — Quinn QA 報告紀律若要強化在 CLAUDE_Quinn.md 必先評估「未來客戶專案沿用同份」影響（Christ 2026-05-14 拍板紀律延續）
- **Forge spike Plan Mode 預設 0 主動 WebSearch**：本 Stage WebSearch 結論段 Forge 引用 / 不重複觸發
- **Aria 自審紀律自省點 #40 baseline**：本 Stage 規模 S 不必 ultrathink 自審（規模 L+ baseline / M+ 選擇性）

### 範圍刻意收緊

- **不動 CLAUDE_Quinn.md**（跨專案守則 / 不污染）
- **不動 Petra system prompt 純 JSON 紀律**（既有對 Sonnet 4.6 純 JSON OK / 子項 3 是底層 parser 防呆延伸）
- **不動 ClaudeCodeService 4 worker method 簽名**（IClaudeCodeService 介面穩定）
- **不動 v5 動態 replan 邏輯**（Stage 81 動態 replan core 留 Trial_v26 補驗）

### Gate1 Tier 建議

對齊 workflow_aria.md 第三節 D Gate1 分級紀律：
- 規模 S（修根因 + 砍錯方向 + 順手議題）→ **Tier 0 + Tier 1**（預設 — 一般功能擴展）
- 若 Forge spike Plan Mode 揭路線 A 全 4 worker 切 stream-json 規模升 M+ → **Tier 0 + Tier 1 + Tier 2 #3 build/test 跑**

---

## 實作紀錄（v2.0）

### Forge 結案總覽

- **commit**：[`a413aee`](https://github.com/darkleong/AiTeam/commit/a413aee) feat(stage82) — 10 檔變動 / +406 / -81 / 0 follow-up bug
- **Forge context 真實耗用**：規劃書 179K → gate1 修正 184K → 實作 253K → self-verify 270K → 結案 274K（Opus 1M + high effort）
- **本機驗證**：dotnet build 0 error / 105 baseline warning 對齊 / dotnet test 136 passed 0 failed / 6 sec
- **Aria gate1 通過**：Tier 0 + Tier 1 + Tier 2 全綠 — 對齊 plan v1.1 規模升 M+ tier 建議

### Forge spike 揭真實 vs Aria Roadmap 假設

| Spike 子項 | Aria Roadmap 假設 | 真實揭 | 修法調整 |
|---|---|---|---|
| 1 — claude CLI stream-json schema | 4 row type schema 推測 | 跑真實 `claude --output-format stream-json --verbose -p` 揭 4 row type（system init / assistant message / rate_limit_event / result）+ assistant.message.content[] 內 type=text / type=tool_use 對齊 | 路線 A spike 證實實作成本可控（ParseJsonOutput +30 行）|
| 1.5 — TryParseUsage schema verify（Aria gate1 補強）| 「stream-json result row usage nested 跟 json mode 一致性沒明確 verify」 | spike 跑真實 + 4 nested 欄位（input/output/cache_creation/cache_read）+ top-level total_cost_usd **完全對齊既有 TryParseUsage** | TryParseUsage **0 動** + xUnit T6 補對應 verify case |
| 2 — PM agent token_logs 真實狀態 | 「Petra LLM call 沒進 token_logs / 0 row」 | PostgreSQL grep 真實揭 PM 24 row 真實有 row（45464 input / 3847 output）但 PetraSessionId **0/24 全 NULL** | 修法落點調整 — 不是包 TokenTrackingProvider（已包），而是 TokenLog 寫入加 PetraSessionId AsyncLocal 透傳 |

### 設計決策真實拍板（vs Roadmap §設計決策 D1-D4）

- **D1 路線 A vs B**：🥇 A — spike 證實 ParseJsonOutput +30 行 + image path 0 regression + 修根因徹底
- **路線 A 全 4 worker 切 stream-json vs 只 qa_testing**：全切（一致性紀律 + 未來 Cody/Vera/Sage 也可能踩 tool-heavy edge case）
- **D2 子項 2 AsyncLocal vs ILlmProvider param**：🥇 AsyncLocal — 介面 0 動 / 既有 caller 透明 / 只 Petra 4 site 包 scope
- **D3 子項 3 strip 防呆 + Petra 純 JSON 紀律雙保險**：對齊 Roadmap 拍板維持
- **D4 不 amend Stage 81 既有 commit `92eadb9`**：Stage 82 新 commit 砍 prepend + 修對地方 — 對齊 plan 紀律

### 跨 Stage know-how 升級（Aria 結案第二段 step 0 順手做）

對齊 workflow_aria.md 第五節 step 0「跨 Stage know-how 升級評估」+「順手 grep 同檔同類根因」紀律：

1. **workflow_aria.md 第三節 A 第 7 條延伸範圍 #13** — 規劃含 token_logs / DB row 既有狀態評估時必 SQL grep 真實狀態（不憑印象 / 不憑 commit message 推測）— 同類根因第 13 次累積（既有 12 條延伸）
2. **workflow_aria_session_lessons.md #42 立** — Stage 修根因前先 spike / production 數據 verify 假設真實（Trial_v25 揭 Stage 81 子項 8 假設「無 final text turn」錯方向真實案例 — 修根因前驗證根因假設真實才動 / 對齊「修根因 > 補丁」紀律延伸）
3. **workflow_aria_session_lessons.md #40 補充段** — Aria gate1 階段主動補 micro-spike 紀律首次大規模實踐生效（Stage 82 plan v1.0 → v1.1 揭 TryParseUsage schema verify 議題 → Forge 補 micro-spike → 實作 0 動 0 follow-up bug 實證）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-21 | Stage 82 規劃書建立（Aria 撰寫 / Trial_v25 結案後即進 Stage 82 / 對齊「修根因 > 補丁」+「對冗餘不容忍」紀律）。**核心**：3 子項 + 5 場景 + 4 設計決策 + WebSearch 結論段 + 規劃前置 grep 5 觸點。**Stage 規模 S** / 0 燒 AiTeam 餘額。 |
| v1.1 | 2026-05-21 | **Aria gate1 plan review 揭 🟡 議題收口** — Forge 補 spike 子項 1.5 micro-spike 跑真實 claude CLI 取 result row 完整 schema verify TryParseUsage 4 nested 欄位 + top-level total_cost_usd 對齊 / xUnit T6 補對應 verify case / Vera blind spot 延伸風險解除。「Aria 自審紀律 + gate1 補強 micro-spike」延伸實踐生效。 |
| v2.0 | 2026-05-21 | **Forge 實作完成 + Aria gate1 通過 + 結案 in-place**。commit [`a413aee`](https://github.com/darkleong/AiTeam/commit/a413aee) / 10 檔 +406 -81 / 0 follow-up bug / 連續 18 Stage 0 follow-up + clean delivery 連續第十次 ⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-82）。Forge spike Plan Mode 揭真實 vs Roadmap 兩處假設修正（子項 1.5 schema 0 動 + 子項 2 修法落點調整 PetraSessionId 透傳 NULL 而非「0 row」）。Forge context 真實耗用 179K → 274K Opus 1M + high。**戰略意義**：「修根因 > 補丁」紀律深度實踐 — 砍 Stage 81 子項 8 prepend 錯方向 + 修對地方（stream-json accumulate）+ 收口 Trial_v25 三 🟡 議題（含 SubtaskPlanParser preamble 防呆 + PM PetraSessionId AsyncLocal 透傳）/ Trial_v26 補驗 Stage 81 動態 replan + Stage 82 修法雙驗證。**3 跨 Stage know-how 升級**（第三節 A 第 7 條延伸範圍 #13 + 自省點 #42 + #40 補充段）。 |

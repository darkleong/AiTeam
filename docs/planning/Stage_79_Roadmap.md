# Stage 79 — v5.5 image flow 補完（PetraInbox schema 擴 + 條件性 worker propagation）

> 對應系統版本：v3.71.0
> 規模：M+
> 狀態：規劃中
> 性質：新業務功能（schema + 資料流補完 + Petra 拆 plan 邏輯升級 + Claude Code CLI 圖片支援 spike）/ Migration 加 column nullable / 涉及 LLM call 真實業務影響
> Model + Effort 建議：**Opus 1M + Extra high**（連續 8 Stage 自選紀律延續 + 涉及 Petra prompt + ClaudeCodeChatClientAdapter 圖片 spike 風險中位）
> cost 預估：$3-5 per cycle

---

## 戰略脈絡

Christ 2026-05-19 戰略 question 揭 v5.5 path **Dashboard 附圖 Petra 看不到 gap** — Stage 75 切 PetraInbox 設計時遺漏 image flow：
- Dashboard UI 接受附圖 ✅ → CeoCommandController 接收 + parse ImageAttachment ✅ → ProcessWithClaudeCodeAsync 簽名收 images ✅
- **但 method body `petraInboxRepository.Enqueue(userInput, source)` 丟棄 images** ❌ → PetraInbox schema 0 image 欄位 ❌ → PetraDispatchWorker / PetraOrchestratorService / Petra LLM call / Worker dispatch 全鏈 0 image

Trial_v6-v22 連續 17 次純文字 prompt 沒踩到 / Stage 78a/78b/78c 砍 v4 路線也沒處理 / Stage 80 HITL plan confirmation 真實需要視覺 context（UI bug 截圖 / mockup）— **image flow 是 HITL 業務功能前置依賴**。

WebSearch 業界紀律（[Latenode LangGraph 2026 + Google Agent Bake-Off](https://developers.googleblog.com/build-better-ai-agents-5-developer-tips-from-the-agent-bake-off/)）：「**only give each agent the tools it actually needs / pass images only to the worker agents that need them**」— 不無腦 propagate / 由 supervisor（Petra）依 task 性質條件性決定。

**Stage 79 範圍**：補完 v5.5 image flow + Petra SubtaskPlan 條件性 worker propagation + Claude Code CLI 真實圖片支援機制對齊。

### Phase 4 路徑（Christ 2026-05-19 拍板）

```
Stage 78a ✅ → 78b ✅ → 78c ✅ → 79（本 Stage / v5.5 image flow 補完 / M+）
            → Trial_v23（驗 78a/b/c 砍 + image flow 4 Stage 一次）
            → 80（A HITL plan confirmation 閘門 / M）
            → Trial_v24（驗 HITL 業務體驗）
            → 81（B 動態 re-planning / L）
            → WebUI Stage（K WebUI Talent CRUD + Effort + E Token monitoring + v4 entity drop + Dashboard 重設計）
            → v5.5 完整收口
```

---

## 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

### 1. Claude Code CLI 圖片支援機制（最關鍵 spike 議題）

**真實機制**：[Claude Code CLI doc](https://code.claude.com/docs/en/headless) + [amanhimself blog](https://amanhimself.dev/blog/using-images-in-claude-code/) + [felloai guide](https://felloai.com/claude-code-images/)

- **無獨立 `--image` flag** — 真實機制：**prompt 內 reference 檔案路徑**（subprocess 模式唯一支援）
- 非互動 mode：`claude -p` + prompt 內含 file path
- 支援格式：JPEG / PNG / GIF / WebP / max 5 MB per image / max 100 images per request
- 範例：`claude -p "Analyze this screenshot and tell me what's wrong: /tmp/aiteam-workspace/.tmp/images/screenshot_001.png"`
- subprocess proxy pattern（spawn 新 claude CLI 分析 image / 保 main session clean）— [hook reference gist](https://gist.github.com/justi/8265b84e70e8204a8e01dc9f99b8f1d0)

**對 AiTeam ClaudeCodeChatClientAdapter 真實 fit**：Worker 已有 workspace dir（git clone repo dir）→ 加 `.tmp/images/` subdir 存 attachment 圖檔 → subprocess prompt 加段「附圖檔路徑：...」即可。**0 base64 / 0 --image flag / 純 workspace 檔案 reference**。

### 2. IChatClient (Microsoft.Extensions.AI) multimodal 支援

**真實機制**：[IChatClient doc 2026-03 update](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)

- IChatClient 設計支援 multimodal（text + images + audio）— `ChatMessage` 含 multi-modal content
- `GetResponseAsync(IEnumerable<ChatMessage> messages, ...)` 接 messages 含 image AIContent
- Stage 67 ITalentFactory + ClaudeCodeChatClientAdapter 設計已對齊 IChatClient interface

**對 AiTeam Worker dispatch 真實 fit**：ClaudeCodeChatClientAdapter.GetResponseAsync 收 ChatMessage 含 image AIContent → 內部寫 workspace 圖檔 + subprocess prompt 加 path reference → Claude Code CLI 真實處理圖片。

### 3. Multi-agent image propagation 業界 best practice

**真實機制**：[Latenode LangGraph 2026](https://latenode.com/blog/ai-frameworks-technical-infrastructure/langgraph-multi-agent-orchestration/langgraph-multi-agent-orchestration-complete-framework-guide-architecture-analysis-2025) + [Google Agent Bake-Off](https://developers.googleblog.com/build-better-ai-agents-5-developer-tips-from-the-agent-bake-off/)

- **只給需要的 worker / 不無腦 propagate**（token 浪費 + 干擾模型決策）
- supervisor（Petra）依 task 性質條件性決定
- multimodality 是 native feature 不是「先 LLM 轉文字描述再傳」反 pattern

**對 AiTeam 6 Talent 真實場景**：
- Petra（orchestrator）：必須看圖
- Cody（Dev）：條件性 — UI 修改要 / 純後端 / docs 不要
- Vera（Reviewer）：條件性 — UI 變動 review 要 / 一般 PR diff 不要
- Quinn（QA）：條件性 — UI E2E test 要 / 後端 logic test 不要
- Sage（收尾文件）：不需要

→ Petra 拆 SubtaskPlan 時加 `NeedsImageContext: bool` per subtask flag / Worker dispatch chain 依此條件性傳遞。

---

## 子項清單

### 1. PetraInbox schema 擴 Images + Migration

**範圍**：`src/AiTeam.Data/Entities.cs` PetraInbox class 加 Images column（nullable / 對齊 backwards-compatible 既有 row 0 image）

**兩路線**（Forge Plan Mode 拍板）：
- **🥇 路線 A**（推薦）：JSON 序列化 `List<ImageAttachment>` 進 PetraInbox 同表 `Images` column（jsonb / Postgres）
  - 優：1 Migration / 0 連動表 / Repository 簡單
  - 缺：DB row 可能 5-15 MB（最壞 case 100 image / AiTeam 真實 1-3 image 典型）
- **🥈 路線 B**：Separate `petra_inbox_images` 表（id / petra_inbox_id FK / base64_data / media_type / order_index）
  - 優：schema 乾淨 / DB row 不重 / 對齊業界 multi-image best practice
  - 缺：2 Migration / FK 連動寫入 / Repository 複雜

對齊 AiTeam 真實場景（Christ 附 1-3 截圖典型）— 路線 A baseline / Forge 拍板。

Migration：對齊 ef-core.md 紀律 / `--project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext` / nullable column ADD 0 不可逆風險。

### 2. PetraInboxRepository.Enqueue 簽名擴

**範圍**：`src/AiTeam.Data/Repositories/PetraInboxRepository.cs`

- 簽名擴：`PetraInbox Enqueue(string userInput, string source, IReadOnlyList<ImageAttachment>? images = null)`
- 對齊 backwards-compatible（既有 caller 不傳 images / default null）
- 路線 A：images != null → JSON 序列化寫 Images column / images == null → Images=null

### 3. CeoAgentService.ProcessWithClaudeCodeAsync 接通 images（Stage 75 漏接修根因）

**範圍**：`src/AiTeam.Bot/Agents/CeoAgentService.cs`

- ProcessWithClaudeCodeAsync 簽名既有 `IReadOnlyList<ImageAttachment>? images = null` 參數（Stage 78b 後保留）
- method body 修：`petraInboxRepository.Enqueue(userInput, source, images)` 接通 images（之前丟棄）
- logger 加 image count 紀錄：`images.Count`

### 4. PetraInboxProcessor → PetraDispatchWorker → PetraOrchestratorService.StartAsync chain 加 images

**範圍**：
- `src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs` pickup row → deserialize Images column → 傳 PetraDispatchWorker
- `src/AiTeam.Bot/Orchestration/Petra/PetraDispatchWorker.cs` chain 加 images 參數
- `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` `StartAsync` 簽名擴 + 內部傳 images 到 BuildSessionContext / Petra LLM call sites

對齊 backwards-compatible（既有 caller 不傳 / default null）。

### 5. Petra LLM call 3 call sites 含 images

**範圍**：`src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs`
- line 227（DecideTalentsAsync）
- line 407（DecideSkillAsync）
- line 474（DecideSubtaskPlanAsync）

`LlmProviderFactory.Create("PM").CompleteAsync(systemPrompt, userMessage, ct, images: images)` — Petra 用 LlmProviderFactory（Gemini default / 真實支援 multimodal）。

### 6. SubtaskPlan.Subtask record 擴 NeedsImageContext flag

**範圍**：`src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs`

- `internal sealed record Subtask(int Id, string SkillName, string Description, bool NeedsImageContext = false)`
- `SubtaskPlan.Linear` factory 對齊（default NeedsImageContext=false）
- `SubtaskPlan.Empty` factory 對齊

### 7. Petra prompt（TalentPrompt PM）教學如何判斷 NeedsImageContext

**範圍**：Stage 73 TalentPrompts schema 用 / DB-as-Prompt path

- Petra prompt 加段教學：「**判斷每個 subtask 是否需要附圖 context**：UI 修改 / 視覺 bug / mockup 對齊 → `needsImageContext: true` / 純後端 logic / 文件 / 測試 logic → `needsImageContext: false`」
- Petra LLM 回應 JSON format 加 `needsImageContext` per subtask field
- Parser 對齊解析

### 8. ClaudeCodeChatClientAdapter 條件性 image dispatch

**範圍**：`src/AiTeam.Bot/Agents/ClaudeCodeChatClientAdapter.cs`

- `GetResponseAsync` 收 messages 含 image AIContent（從 IChatClient ChatMessage 多 modal content 取）
- 內部 workspace 寫圖檔到 `.tmp/images/` subdir（filename pattern `{subtaskId}_{index}_{extension}`）
- subprocess prompt 加段「附圖檔路徑：...」
- 對齊 WebSearch 揭真實「Claude Code CLI prompt 內 reference 檔案路徑機制」

**條件性 dispatch 邏輯**：
- Subtask.NeedsImageContext=true → Worker dispatch 含 image
- Subtask.NeedsImageContext=false → Worker dispatch 純 text（0 image）
- 對齊「pass images only to worker agents that need them」業界紀律

### 9. workspace 圖檔清理 + 多 image 處理

- Worker 跑完 subtask 清理 `.tmp/images/` 對應檔案（避免 workspace 累積）
- 多 image：filename 順序對齊 ImageAttachment List 順序 / Petra prompt 對應「第一張圖 / 第二張圖」reference

### 10. xUnit test 補

- PetraInbox.Enqueue images null vs 有 images path（序列化 verify）
- PetraInboxRepository serialize/deserialize Images column 對齊
- SubtaskPlan.Subtask NeedsImageContext flag 預設 false + Parser 解析
- ClaudeCodeChatClientAdapter 條件性 image dispatch（Subtask.NeedsImageContext=true vs false 各驗）
- Petra prompt 對應 prompt segment 真實 fire（Stage 73 prompt versioning verify）

### 11. Directory.Build.props v3.70.0 → v3.71.0

---

## 設計決策

### 1. 路線 A image schema（PetraInbox 同表 jsonb column）對齊 AiTeam 真實場景

Christ 真實使用模式：1-3 截圖典型 / 不會 100 image / row 5-15 MB 最壞可接受 / DB query 偶爾大 row 0 production 影響（PetraInbox 是 task queue / pickup 後處理 / 不高頻 query）。

路線 B（separate 表）schema 更乾淨但 complexity+ ROI 低 — Forge plan 階段拍板 / Aria baseline 推 A。

### 2. NeedsImageContext flag per subtask — Petra decide 紀律

對齊業界 best practice「only give each agent the tools it actually needs」：
- Petra 拆 SubtaskPlan 時依 task 性質決定每個 subtask 是否傳 image 給 Worker
- 預設 false（保守 / 對齊 backwards-compatible）/ Petra prompt 教學主動 set true 場景
- Worker dispatch chain 依 flag 條件性傳遞 / 不無腦 propagate

### 3. Claude Code CLI 圖片支援 — workspace 檔案 reference 機制

對齊 WebSearch 揭真實「Claude Code CLI 真實機制是 prompt 內 reference 檔案路徑」：
- ClaudeCodeChatClientAdapter 內部寫圖檔到 workspace `.tmp/images/` subdir
- subprocess prompt 加段「附圖檔路徑：...」
- 0 base64 inline / 0 `--image` flag / 純 workspace 檔案 reference

對齊 Stage 67 教訓「framework 結論必驗 AiTeam 真實場景 fit」— WebSearch 確認 Claude Code CLI 真實機制 + AiTeam Worker 既有 workspace dir 設計 fit。

### 4. backwards-compatible 守護紀律延續

- PetraInbox Images column nullable / 既有 row 0 image 不擾
- PetraInboxRepository.Enqueue images 參數 default null / 既有 caller 不擾
- PetraOrchestratorService.StartAsync 簽名擴 nullable / 既有 caller 不擾
- SubtaskPlan.Subtask NeedsImageContext default false / 既有 Petra prompt 0 set field 自動 false
- ClaudeCodeChatClientAdapter 收 0 image AIContent path 0 行為改變（純 text dispatch 0 regression）

### 5. Migration 對齊 ef-core.md 紀律

- ADD column nullable / 0 不可逆風險（vs Stage 78c 議題 9 拍板「0 Migration drop」紀律 — Stage 79 是 ADD nullable / 0 不可逆）
- Down() 對齊 DROP column / 對稱紀律

### 6. ImageAttachment record namespace 評估

ImageAttachment 既有 `src/AiTeam.Bot/Agents/ILlmProvider.cs:27` Bot project 內 / Stage 79 Repository 用需要 reference Bot project type — 評估升 Shared project 對齊「跨 project type 紀律」（Forge plan 階段拍板）。

---

## 驗收情境

### 場景 A：xUnit baseline 0 regression + 新 test 全綠

**觸發**：`dotnet test AiTeam.slnx`
**驗證**：Bot.Tests 100 + Generated 127 baseline 全綠 + Stage 79 新 test（PetraInbox 序列化 / SubtaskPlan NeedsImageContext / ClaudeCodeChatClientAdapter 條件性 image dispatch / Petra prompt segment）全綠。

### 場景 B：Migration ADD column nullable 真實 executed

**觸發**：CI/CD 自動 `dotnet ef database update` apply Migration
**驗證**：
- `docker exec aiteam-aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d petra_inbox"` → 期望含 Images jsonb nullable column
- 既有 row Images=NULL 不擾
- Bot startup 0 exception（EF Core schema match）

### 場景 C：Dashboard 附圖 → PetraInbox 真實寫入 Images

**觸發**：Christ 開 Dashboard 派 task 附 1 張截圖（對齊 Trial_v22 任意 prompt + 加 image attachment）
**驗證**：
- API `/internal/ceo/command` POST request 含 Images 陣列 → 收
- CeoAgentService.ProcessWithClaudeCodeAsync 接收 images
- `petraInboxRepository.Enqueue(userInput, source, images)` 真實傳 images
- SQL `SELECT "Id", "UserInput", "Images" FROM petra_inbox ORDER BY "EnqueuedAt" DESC LIMIT 1` → 期望 Images column 真實含 JSON 序列化 ImageAttachment list（base64 + media_type）
- Bot log 出現「Victoria → 寫 PetraInbox row={Id} source={Source} images=1」

### 場景 D：Petra LLM call 真實含 image（DecideTalents / DecideSkill / DecideSubtaskPlan 3 call sites）

**觸發**：Dashboard 派 task 含圖（場景 C 觸發後 Petra 自動 pickup）
**驗證**：
- Bot log Petra 3 LLM call sites（DecideTalentsAsync / DecideSkillAsync / DecideSubtaskPlanAsync）真實傳 images 參數
- LlmProviderFactory.Create("PM").CompleteAsync 真實 fire 含 images
- token_logs SQL `SELECT "Model", "InputTokens" FROM token_logs WHERE "AgentName"='PM' ORDER BY "CreatedAt" DESC LIMIT 3` → 期望 InputTokens 較純文字偏高（含 image token cost）
- Petra LLM 回應 JSON 含 needsImageContext per subtask（example UI bug task → Cody subtask needsImageContext=true）

### 場景 E：SubtaskPlan NeedsImageContext flag 真實 fire

**觸發**：xUnit test 對 Subtask record 含 NeedsImageContext field
**驗證**：
- xUnit verify `new Subtask(1, "code_implementation", "修 UI bug", NeedsImageContext: true)` 真實接受 flag
- `SubtaskPlan.Linear(skills)` 對應 NeedsImageContext=false default 對齊 backwards-compatible
- Parser 對 Petra LLM JSON `{ "id": 1, "skill": "code_implementation", "description": "...", "needsImageContext": true }` 真實 parse

### 場景 F：Worker dispatch 條件性 image fire（NeedsImageContext=true 真實傳 / =false 不傳）

**觸發**：xUnit test 對 ClaudeCodeChatClientAdapter.GetResponseAsync
**驗證**：
- xUnit case A：messages 含 image AIContent + Subtask.NeedsImageContext=true → workspace `.tmp/images/` 寫圖檔 + subprocess prompt 含 path reference
- xUnit case B：messages 含 image AIContent + Subtask.NeedsImageContext=false → workspace 0 寫圖檔 + subprocess prompt 純 text（image 0 propagate）
- 對齊「pass images only to worker agents that need them」業界紀律

### 場景 G：v5.5 path 0 regression（純文字 prompt 場景）

**觸發**：Christ 開 Dashboard 派純文字 task（無附圖 / 對齊 Trial_v22 baseline）
**驗證**：
- API 接收 Images=null 或空陣列
- 整 chain 0 image propagation（PetraInbox Images=NULL / Petra LLM call images=null / Worker dispatch 純 text）
- 對齊 Trial_v6-v22 連續 14 Trial 業務級成功 baseline 0 行為改變

### 場景 H：Trial_v23 真實業務驗（Aria gate2 範圍 / 對齊 Phase 4 路徑）

**觸發**：Stage 79 結案後 Christ 觸發 Trial_v23
- 驗 Stage 78a/78b/78c 砍 + Stage 79 image flow 4 Stage 一次（對齊 Christ 拍板 Trial 頻率降低紀律）
- prompt 含附圖（UI bug 截圖場景）vs 純文字 baseline 對照

**驗證**：
- v5.5 path 0 regression（純文字 prompt 對齊 Trial_v22 baseline）
- 附圖 prompt：Petra 真實看圖 + 拆 plan 含 needsImageContext + Cody dispatch 真實看圖修 UI bug + PR 真開
- 對齊連續 15 Trial 業務級成功紀律延續

### 場景 I：Bot startup 0 exception（DI / Migration apply）

**觸發**：Bot 啟動 + Application.RunAsync 完整跑通
**驗證**：
- Migration apply log 出現 `Stage79PetraInboxImages` 真實 executed
- DI ServiceProvider validation 0 missing service（PetraInboxRepository.Enqueue 簽名變動 + PetraInboxProcessor + PetraDispatchWorker + PetraOrchestratorService chain）

---

## 技術約束

- Migration ADD column nullable / 0 不可逆風險（vs Stage 78c 議題 9 教訓）
- 對齊 ef-core.md 紀律（`--project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`）
- backwards-compatible 守護延續（既有 row + caller + Petra prompt 0 行為改變）
- Claude Code CLI 圖片支援機制對齊 WebSearch 揭真實（workspace 檔案 reference / 0 base64 / 0 `--image` flag）
- ImageAttachment 5 MB / 100 image max 對齊 Claude Code CLI 限制

---

## ⚠️ Aria 預警

### W1：PetraInbox image schema 路線 A vs B 拍板

Forge Plan Mode 階段拍板 image schema 存法：
- **路線 A**（推薦 / 對齊 AiTeam 真實場景）：jsonb column 同表
- **路線 B**：separate 表 FK 連動

若 Forge spike 揭真實 row size 風險 / DB query 性能風險 → escalate Christ 拍板。

### W2：Petra prompt NeedsImageContext 教學邏輯精準度

對齊業界紀律「pass images only to worker agents that need them」/ AiTeam 6 Talent 真實場景：
- UI 修改 / 視覺 bug / mockup → needsImageContext=true（Cody UI / Vera UI review / Quinn UI E2E）
- 純後端 logic / 文件 / 測試 logic → false（Cody backend / Sage docs / Quinn logic test）

Petra prompt 教學 example case 數 balanced（UI case + backend case + docs case 各 2-3 example）+ Stage 73 TalentPrompts versioning path。

### W3：ClaudeCodeChatClientAdapter workspace 寫圖檔 path 設計

- filename pattern：`.tmp/images/{subtaskId}_{index}_{extension}`（例：`.tmp/images/1_001_png` / `.tmp/images/1_002_jpg`）
- 順序對齊 ImageAttachment List index / Petra prompt 對應「第一張圖 / 第二張圖」reference
- 清理紀律：Worker 跑完 subtask 清理對應檔案（避免 workspace 累積 + 不擾 Cody git workspace 操作）
- 多 image 處理：subprocess prompt 加段含全 path list

### W4：Claude Code CLI 真實 prompt 內 file path reference 機制 verify

對齊 WebSearch 揭真實「Claude Code CLI 真實機制是 prompt 內 reference 檔案路徑」：
- Forge plan 階段對齊 ClaudeCodeChatClientAdapter 現有 subprocess CLI 用法（`claude -p <prompt>`）
- prompt 加 path reference 真實 work / 對齊 [amanhimself blog](https://amanhimself.dev/blog/using-images-in-claude-code/) 範例 pattern
- 若 spike 揭真實機制偏離（如 stdin pipe / 環境變數）→ escalate Christ 拍板修法

### W5：ImageAttachment record namespace 升 Shared 評估

ImageAttachment 既有 `AiTeam.Bot.Agents` namespace（Bot project 內）— Stage 79 Repository（AiTeam.Data project）用需要 cross-project reference / 評估升 `AiTeam.Shared` project：
- 升 Shared：對齊「跨 project type 紀律」/ 但既有 Bot 內 caller 全要 update import
- 留 Bot：Data project add reference Bot project / 但這違反 dependency 方向（Bot depends Data / 不是 Data depends Bot）

Forge plan 階段拍板（推升 Shared / 對齊 dependency direction 紀律）。

### W6：backwards-compatible — 既有 PetraInbox row 0 images

- Stage 79 加 Images nullable column / 既有 row Images=NULL 不擾
- PetraInboxRepository pickup 既有 row → deserialize Images=NULL → chain 0 image propagation（Worker 純 text）
- Dashboard 4 頁顯示既有 row 0 images 0 行為改變（Stage 78c 議題 1 路線 2 留邊界 / 對齊 WebUI Stage 重設計範圍）

### W7：Petra LLM call 3 call sites — Gemini multimodal 真實支援 verify

Petra 用 LlmProviderFactory.Create("PM")（Gemini default 真實使用 / Trial_v22 token_logs 驗）：
- Gemini API 真實支援 multimodal（image input）/ Forge plan 階段 grep verify GeminiProvider 真實實作（`src/AiTeam.Bot/Agents/GeminiProvider.cs` `CompleteAsync` 簽名 + image 處理 path）
- 若 Gemini Provider 真實未實作 multimodal → escalate Christ 拍板（兩路線：補 GeminiProvider multimodal 支援 / 切 Anthropic Provider）

### W8：SubtaskPlan record 加 field 對齊 backwards-compatible

- `Subtask` record 加 `NeedsImageContext` field 對齊 backwards-compatible（default false）
- `SubtaskPlan.Linear(skills)` factory 對齊（skill list 轉 Subtask 全 NeedsImageContext=false）
- `SubtaskPlan.Empty` factory 對齊
- Parser 對既有 Petra LLM JSON 0 `needsImageContext` field 自動 default false（不擾 Stage 73 既有 prompt 輸出）

### 校準錨預估（自省點 #37 第 9 次累積實證候選 + 反向校準紀律延伸）

對齊 Stage 78c 揭「ultrathink 預估上界偏高 / 真實落點往中段下緣」反向校準新發現：
- raw 預估：130-180K（M+ 規模 / 對齊 Stage 78b 純 refactor 80-130K 上調 +50K 含 Petra prompt 教學 + ClaudeCodeChatClientAdapter image dispatch spike + Migration ADD column + xUnit test 補）
- ratio 預估：×1.5-2.5（範圍清楚 + Stage 78a/b/c 累積經驗成熟 + 1 輪 spike 預期）
- 真實 context 預估：~200-450K（中位 ~300K / Stage 78b 394K + Stage 78c 521K baseline）
- Opus 1M + Extra high safety 20-45%（充裕）

**新校準錨紀律延伸**（Stage 78c 揭發現）：
- 「M+ 規模 + 新業務功能 + 1 輪 spike」場景 → ratio ×1.5-2.5 中段
- vs Stage 78a/78b/78c 「v4 砍 + 多輪 spike + healthy 偏離 propagation 多」場景 → ratio ×2.5-4.93 中上段
- Stage 79 性質偏向前者 / 落點預估 ~300-400K 真實

cost 預估：$3-5 per cycle

### Forge Plan Mode 起手紀律

1. 對齊 workflow_forge.md + forge-self-verify skill 紀律
2. **W1 image schema 路線 A vs B 拍板**（Forge Plan Mode）→ Aria 二檢 + Christ 確認
3. **W2 Petra prompt NeedsImageContext 教學 example case 設計**（balanced UI / backend / docs case）→ Aria 二檢
4. **W3 ClaudeCodeChatClientAdapter workspace 圖檔 path + 清理紀律設計**
5. **W4 Claude Code CLI subprocess image dispatch spike**（grep verify 現有 ClaudeCodeChatClientAdapter subprocess call 真實 prompt 形式 + 對齊 WebSearch 結論 file path reference）
6. **W5 ImageAttachment namespace 升 Shared 拍板**（推升 / 對齊 dependency direction）
7. **W7 GeminiProvider multimodal 支援 grep verify**（必前置 spike / 若未實作 escalate Christ）
8. v5.5 path 0 行為改變紀律守（場景 G 純文字 baseline 對齊）
9. 結案紀律延續（Forge 不進自驗 / 場景 H Trial_v23 留 Aria gate2 + Christ 觸發 / 對齊 forge-self-verify skill 觸發紀律）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-19 | 規劃書建立 — v3.71.0 / M+ 規模 / v5.5 Phase 4 image flow 補完（Stage 75 切 PetraInbox 設計遺漏修根因 + 條件性 worker propagation）。**戰略脈絡**：Christ 2026-05-19 戰略 question 揭 Dashboard 附圖 Petra 看不到 gap + WebSearch 業界紀律拍板「pass images only to worker agents that need them」+ Claude Code CLI 真實圖片支援機制 WebSearch 確認（workspace 檔案 reference / 0 base64 / 0 `--image` flag）。**11 子項**：① PetraInbox schema 擴 Images jsonb column + Migration ② PetraInboxRepository.Enqueue 簽名擴 ③ CeoAgentService.ProcessWithClaudeCodeAsync 接通 images 修 Stage 75 漏接根因 ④ PetraInboxProcessor → PetraDispatchWorker → PetraOrchestratorService.StartAsync chain 加 images ⑤ Petra LLM call 3 call sites 含 images（DecideTalents/Skill/SubtaskPlan）⑥ SubtaskPlan.Subtask record 擴 NeedsImageContext flag ⑦ Petra prompt（TalentPrompt PM）教學如何判斷 NeedsImageContext（UI 修改 → true / 純後端 / docs → false）⑧ ClaudeCodeChatClientAdapter 條件性 image dispatch（workspace 寫圖檔 + subprocess prompt path reference）⑨ workspace 圖檔清理 + 多 image 處理 ⑩ xUnit test 補 ⑪ Directory.Build.props v3.70.0 → v3.71.0。**計劃前 WebSearch 結論 3 段完整 incorporated**：① Claude Code CLI 圖片支援機制（無 `--image` flag / 真實機制 prompt 內 file path reference）② IChatClient multimodal 支援（ChatMessage 含 image AIContent）③ Multi-agent image propagation 業界 best practice（only give what agent needs / supervisor 條件性決定 / multimodality native feature）。**設計決策核心**：路線 A image schema baseline（PetraInbox jsonb column 同表 / AiTeam 真實場景 1-3 image 典型 / row 5-15 MB 可接受）+ NeedsImageContext per subtask Petra decide 紀律 + Claude Code CLI workspace 檔案 reference 機制 + backwards-compatible 守護紀律延續 + Migration ADD column nullable 0 不可逆風險。**驗收 9 場景**：A xUnit baseline + 新 test / B Migration ADD column / C Dashboard 附圖 → PetraInbox 寫入 Images / D Petra LLM call 真實含 image / E SubtaskPlan NeedsImageContext flag / F Worker dispatch 條件性 image fire / G v5.5 path 0 regression 純文字 baseline / H Trial_v23 真實業務驗 4 Stage 一次 / I Bot startup 0 exception。**Aria 預警 W1-W8**：image schema 路線拍板 / Petra prompt 教學精準度 / workspace 圖檔 path 設計 / Claude Code CLI subprocess image dispatch spike / ImageAttachment namespace 升 Shared 評估 / backwards-compatible 既有 row 0 images / GeminiProvider multimodal 支援 verify（必前置 spike）/ SubtaskPlan record 加 field 對齊。**校準錨預估**：對齊大規模架構級重構新區間 ×1.30-4.93 中段下緣 / raw 130-180K × ratio ×1.5-2.5 = 真實 ~200-450K / Opus 1M + Extra high safety 20-45% / cost $3-5。**Phase 4 路徑**：78a ✅ → 78b ✅ → 78c ✅ → **79**（本 / v5.5 image flow / M+）→ Trial_v23（驗 4 Stage）→ 80（HITL / M）→ Trial_v24（驗 HITL 業務體驗）→ 81（動態 replan / L）→ WebUI Stage → v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1 + Aria gate2 production 0 regression 驗 → 通過後 Trial_v23 開（4 Stage 一次 cover）。 |

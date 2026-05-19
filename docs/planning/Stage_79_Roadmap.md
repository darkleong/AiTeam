# Stage 79 — v5.5 image flow 補完（PetraInbox schema 擴 + 條件性 worker propagation + 半抽象 future-friendly 設計）

> 對應系統版本：v3.71.0
> 規模：M+
> 狀態：✅ 已完成（2026-05-19）
> 文件版本：v2.0
> commit：[`0f9176e`](https://github.com/darkleong/AiTeam/commit/0f9176e) — Forge 結案第一段 + 自驗 PASS（7 場景 Aria gate1 範圍 / Aria gate2 場景 C-H 留 Trial_v23 + Christ 視覺驗收）
> 性質：新業務功能（schema + 資料流補完 + Petra 拆 plan 邏輯升級 + Claude Code CLI 圖片支援 spike + 限制紀律 + 半抽象命名 future-friendly）/ Migration 加 column nullable / 涉及 LLM call 真實業務影響
> Model + Effort 建議：**Opus 1M + high**（範圍 M+ / 真實 context 預估 ~200-450K Opus 1M safety 20-45% / Christ 自決升 Extra high 兜底）
> Stage 期間餘額影響：**0 燒 AiTeam 餘額**（Aria + Forge session 走 Claude Code subscription / Stage 79 預期 0 API call spike — W4 Claude Code CLI subprocess + W7 GeminiProvider multimodal 都 grep verify level / 0 真實 LLM call）。Trial_v23 才燒 AiTeam Petra/Cody/Vera 等真實 LLM call 餘額（對齊既有 Trial baseline / 預估留 Trial_v23 計劃書）

---

## 戰略脈絡

Christ 2026-05-19 戰略 question 揭 v5.5 path **Dashboard 附圖 Petra 看不到 gap** — Stage 75 切 PetraInbox 設計時遺漏 image flow：
- Dashboard UI 接受附圖 ✅ → CeoCommandController 接收 + parse ImageAttachment ✅ → ProcessWithClaudeCodeAsync 簽名收 images ✅
- **但 method body `petraInboxRepository.Enqueue(userInput, source)` 丟棄 images** ❌ → PetraInbox schema 0 image 欄位 ❌ → PetraDispatchWorker / PetraOrchestratorService / Petra LLM call / Worker dispatch 全鏈 0 image

Trial_v6-v22 連續 17 次純文字 prompt 沒踩到 / Stage 78a/78b/78c 砍 v4 路線也沒處理 / Stage 80 HITL plan confirmation 真實需要視覺 context（UI bug 截圖 / mockup）— **image flow 是 HITL 業務功能前置依賴**。

WebSearch 業界紀律（[Latenode LangGraph 2026 + Google Agent Bake-Off](https://developers.googleblog.com/build-better-ai-agents-5-developer-tips-from-the-agent-bake-off/)）：「**only give each agent the tools it actually needs / pass images only to the worker agents that need them**」— 不無腦 propagate / 由 supervisor（Petra）依 task 性質條件性決定。

**Stage 79 範圍**：補完 v5.5 image flow + Petra SubtaskPlan 條件性 worker propagation + Claude Code CLI 真實圖片支援機制對齊 + 限制紀律（per task max 5 attachment / per file max 5 MB / AppSetting 可調）+ **半抽象 future-friendly schema 命名**（schema 用 `Attachments` jsonb + `AttachmentType` discriminator 預留 PDF/document 等未來擴展 / Stage 79 只實作 type="image"）。

**檔案類型範圍拍板**（Christ 2026-05-19 拍板路線 A）：Stage 79 **只做 Image**（AiTeam 真實 90%+ scenario UI 截圖 / Christ 親口 gap）— 其他檔案類型（PDF / document / audio / video）留 Future Feature「v5.5 multi-attachment flow 補完」候選 / 真實需求觸發才做。但 schema 命名半抽象（`Attachments` + `AttachmentType`）對齊「未來擴展 0 schema migration」紀律 / scope 不擴 Stage 79 規模。

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

### 1. PetraInbox schema 擴 Attachments + Migration（半抽象 future-friendly 命名）

**範圍**：`src/AiTeam.Data/Entities.cs` PetraInbox class 加 **`Attachments` jsonb column**（nullable / 對齊 backwards-compatible 既有 row 0 attachment）

**Schema 設計（半抽象）**：
- column 名 `Attachments`（不 `Images`）— 預留未來擴展 PDF / document / 等不需 schema migration
- JSON 內每個 attachment 含 `Type` discriminator field（"image" / 未來 "pdf" / "document"）+ `Base64Data` + `MediaType`
- Stage 79 只實作 `Type="image"` 真實寫入 / 其他 type 預留枠

**兩路線**（Forge Plan Mode 拍板）：
- **🥇 路線 A**（推薦）：JSON 序列化 `List<Attachment>` 進 PetraInbox 同表 `Attachments` jsonb column
  - 優：1 Migration / 0 連動表 / Repository 簡單 / Stage 79 範圍精準
  - 缺：DB row 可能 5-25 MB（max 5 attachment × 5 MB / 對齊限制紀律子項 12）
- **🥈 路線 B**：Separate `petra_inbox_attachments` 表（id / petra_inbox_id FK / type / base64_data / media_type / order_index）
  - 優：schema 乾淨 / DB row 不重
  - 缺：2 Migration / FK 連動寫入 / Repository 複雜

對齊 AiTeam 真實場景（Christ 附 1-3 截圖典型）+ 限制紀律 5 張上限 → 路線 A baseline / Forge 拍板。

Migration：對齊 ef-core.md 紀律 / `--project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext` / nullable column ADD 0 不可逆風險。

### 2. PetraInboxRepository.Enqueue 簽名擴

**範圍**：`src/AiTeam.Data/Repositories/PetraInboxRepository.cs`

- 簽名擴：`PetraInbox Enqueue(string userInput, string source, IReadOnlyList<ImageAttachment>? images = null)`
- 對齊 backwards-compatible（既有 caller 不傳 images / default null）
- 路線 A：images != null → JSON 序列化（含 Type="image" discriminator）寫 Attachments column / images == null → Attachments=null

**命名半抽象紀律**：Repository 簽名仍用 `ImageAttachment`（既有 record / Stage 79 範圍精準 image）/ schema column 用 `Attachments`（future-friendly）/ Repository 內部把 ImageAttachment 轉成含 Type="image" 的 JSON 結構寫入。未來擴展 PDF / document 時新加 `EnqueueWithPdf` 等 overload 或統一抽象（看真實需求）。

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

- PetraInbox.Enqueue images null vs 有 images path（序列化 verify / Type="image" discriminator）
- PetraInboxRepository serialize/deserialize Attachments column 對齊
- SubtaskPlan.Subtask NeedsImageContext flag 預設 false + Parser 解析
- ClaudeCodeChatClientAdapter 條件性 image dispatch（Subtask.NeedsImageContext=true vs false 各驗）
- Petra prompt 對應 prompt segment 真實 fire（Stage 73 prompt versioning verify）
- **限制違規處理**：MaxAttachmentsPerTask + MaxAttachmentSizeMB AppSetting 真實生效 / Enqueue 超限丟前 N 個 + log warning（子項 11）

### 11. Attachment 限制紀律 + AppSetting 可調

**設計拍板**（Aria 建議 / Forge 拍板確認）：

| 限制項 | 預設值 | AppSetting key |
|---|---|---|
| per attachment max size | **5 MB** | `Workflow:MaxAttachmentSizeMB` |
| per task max attachment count | **5 張** | `Workflow:MaxAttachmentsPerTask` |
| 推估 per task max total | 25 MB | 5 × 5 MB |

**理由**：
- 對齊 Claude Code CLI（5 MB per image）+ Claude API（5 MB per image）共同上限
- AiTeam 真實場景 1-3 截圖典型 / 5 張充裕緩衝
- 防止 PetraInbox row 過大（25 MB jsonb 是 Postgres 健康上限）+ Petra LLM call cost 可控

**檢查層**：
- **Dashboard 端**：MudFileUpload `MaxFiles={MaxAttachmentsPerTask}` + file size hint + 顯示「最多 5 張 / 每張 5 MB」提示
- **API 端**：CeoCommandController 收 > 5 張 / 單張 > 5 MB → 回 **400 Bad Request** 含明確錯誤訊息
- **後備層**：PetraInboxRepository.Enqueue 收 > 5 張 → log warning + 截前 5 張寫入（防 API 端漏 verify / 對齊「邊界守 + 後備保險」紀律）

**Migration**：AppSettings table 既有 InsertData seed 加 2 條（對齊 Stage 75 `Workflow:UsePetraOrchestratorV5` + Stage 77 `Workflow:MaxConcurrentPetra` 既有 pattern）。

### 12. Dashboard MudFileUpload + API verify 違規處理

**範圍**：
- `src/AiTeam.Dashboard/Components/Pages/Home/...` MudFileUpload 設定 `MaxFiles={MaxAttachmentsPerTask}` + `MaximumFileCount` 對齊
- `src/AiTeam.Bot/Api/CeoCommandController.cs` 收 request 後 verify `Images?.Count <= MaxAttachmentsPerTask` + 各張 `Base64Data.Length <= MaxAttachmentSizeMB × 1024 × 1024` → 超限回 400 + 訊息

對齊 Dashboard 主要 entry（Christ 真實使用通道）+ API 後備邊界守。

### 13. Directory.Build.props v3.70.0 → v3.71.0

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

### 7. 半抽象 future-friendly 設計（Christ 2026-05-19 拍板）

**Christ 拍板「Attachments 命名」+ 路線 A 只做 Image**：採半抽象設計：
- **schema 抽象**：column 名 `Attachments` jsonb + JSON 內含 `Type` discriminator field — 預留未來擴展 PDF / document 等 type 不需 schema migration
- **code 暫不抽象**：ImageAttachment record + SubtaskPlan.NeedsImageContext flag 留 image-specific 命名（Stage 79 唯一 type / 對齊 Petra prompt 教學精準度）
- **未來擴展 path**：
  - 加 PdfAttachment record（type="pdf"）/ DocumentAttachment record（type="document"）/ 各自獨立 record
  - SubtaskPlan 加 NeedsPdfContext / NeedsDocumentContext 新 flag（或統一 NeedsAttachmentContext: AttachmentTypes flag enum）— 看真實需求觸發時拍板
  - schema Attachments jsonb 0 migration 直接擴

**vs 拒絕全抽象**（record FileAttachment 統一 + SubtaskPlan.NeedsAttachmentContext 統一）：
- 全抽象 scope 擴 +20%（既有 ImageAttachment caller 全 update）
- Stage 79 範圍精準（image only / scope M+ 維持）/ 全抽象擴複雜度違反「最小可行紀律」

**對齊「最小抽象 future-friendly」紀律** — schema 預留擴展但 code 暫不重構 / 真實需求觸發再拍板抽象度。

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

### 場景 J：Attachment 限制違規處理（Dashboard + API + Repository 三層守）

**觸發**：
- Dashboard 端：嘗試上傳 6 張圖（超 MaxAttachmentsPerTask=5）
- API 端：直接 POST `/internal/ceo/command` 含 6 張 Images / 單張 10 MB（超 MaxAttachmentSizeMB=5）
- 後備層：xUnit test PetraInboxRepository.Enqueue 強制傳 7 張

**驗證**：
- Dashboard MudFileUpload `MaxFiles=5` 真實生效（用戶 UI 端阻止超第 6 張）
- API `/internal/ceo/command` 收 6 張 → 回 400 Bad Request 含「最多 5 張 attachment」訊息 / 收單張 10 MB → 回 400「單張最多 5 MB」
- xUnit verify PetraInboxRepository.Enqueue 收 7 張 → log warning「超 MaxAttachmentsPerTask=5 / 截前 5 張」+ PetraInbox row Attachments=5 張（真實寫入前 5 / 後 2 丟棄）
- AppSetting `Workflow:MaxAttachmentsPerTask` SQL UPDATE 為 3 → reload-cache → 新 task 強制限 3 張（AppSetting 真實動態生效）

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

### W9：半抽象 future-friendly 命名邊界 — schema vs record vs SubtaskPlan flag

對齊 Christ 拍板「Attachments 命名」+ 路線 A 只做 Image：

| 層 | 抽象度 | 理由 |
|---|---|---|
| **PetraInbox schema column** | **抽象** `Attachments` jsonb + `Type` discriminator | 未來擴展 0 schema migration |
| **ImageAttachment record** | **不抽象** image-specific | 既有 record + Stage 79 範圍精準 + 全抽象 scope 擴 +20% |
| **SubtaskPlan.NeedsImageContext flag** | **不抽象** image-specific | Petra prompt 教學精準對齊 image scenario / 未來擴展再加 NeedsPdfContext 等 |
| **Repository.Enqueue 簽名** | **不抽象** `IReadOnlyList<ImageAttachment>?` | 既有 caller 不擾 + Stage 79 image-only |

**Forge Plan Mode 確認**：若實作期揭真實全抽象 ROI 升高 / 半抽象設計擾範圍 → escalate Christ 拍板（不擾既有設計拍板 / 對齊「最小抽象紀律」精神）。

### W10：Attachment 限制紀律 AppSetting + Migration InsertData

對齊 Stage 75/77 既有 AppSettings InsertData seed pattern：
- Migration 加 2 條 InsertData：`Workflow:MaxAttachmentsPerTask=5` + `Workflow:MaxAttachmentSizeMB=5`
- WorkflowSettingsResolver 加對應 getter method（對齊 Stage 77 `MaxConcurrentPetra` resolver pattern）
- AppSettingsService cache TTL 對齊（5 min 既有 baseline）

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
- Opus 1M + high safety 20-45%（充裕 / Christ 自決升 Extra high 兜底）

**新校準錨紀律延伸**（Stage 78c 揭發現）：
- 「M+ 規模 + 新業務功能 + 1 輪 spike」場景 → ratio ×1.5-2.5 中段
- vs Stage 78a/78b/78c 「v4 砍 + 多輪 spike + healthy 偏離 propagation 多」場景 → ratio ×2.5-4.93 中上段
- Stage 79 性質偏向前者 / 落點預估 ~300-400K 真實

**Stage 期間 AiTeam 餘額影響：0**（對齊「Stage 期間 subscription / 0 API call spike」紀律 / Trial_v23 才燒 AiTeam Petra/Cody 真實 LLM call 餘額 / Trial cost 預估留 Trial_v23 計劃書）

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

## 實作紀錄（v2.0 / Forge 結案第一段 / 2026-05-19）

### 統計（commit [`0f9176e`](https://github.com/darkleong/AiTeam/commit/0f9176e)）

- **20 files changed** / **+1775 insertions / -53 deletions** / **net +1722 行**（Migration + Designer + Snapshot 含 ~600 行 EF auto-generated）
- 本機驗證：`dotnet build AiTeam.slnx` → **0 error / 54 warning**（baseline ~59 對齊 / -5 warning 健康下降）+ `dotnet test` → **Bot.Tests 102/102 + Generated 127/127 全綠**
- 新加 xUnit test 2 個（Test31 SubtaskPlanParser NeedsImageContext / Test32 SubtaskPlan.Linear Empty default false）
- 既有 xUnit Mock fixture 3 處 reflection invoke 簽名擴（DispatchWorkersAsync + 2× DispatchTalentsAsync + Stage77MultiConsumer StubPetraOrchestrator override）
- Forge 自驗 PASS 7 場景（Aria gate1 範圍）：場景 B Migration apply + 場景 B-seed InsertData + 場景 I Bot startup 0 exception + 場景 I-schema petra_inbox 含 Attachments jsonb + 場景 J-count API 400 超 5 張 + 場景 J-size API 400 單張超 5 MB + R5 既有 row backwards-compatible

### 完成項目（11 鏈 / 13 子項對齊 Roadmap v1.2 + Plan v1）

**鏈 A — PetraInbox schema 擴 + Migration**：
- [`src/AiTeam.Data/Entities.cs:493-497`](src/AiTeam.Data/Entities.cs) PetraInbox 加 `Attachments` nullable string column（半抽象 future-friendly schema / JSON 含 Type discriminator）
- [`src/AiTeam.Data/AppDbContext.cs:319`](src/AiTeam.Data/AppDbContext.cs) `e.Property(x => x.Attachments).HasColumnType("jsonb")` 配置
- [`Migrations/20260519150448_Stage79PetraInboxAttachments.cs`](src/AiTeam.Data/Migrations/20260519150448_Stage79PetraInboxAttachments.cs) Up()：AddColumn jsonb nullable + InsertData 2 AppSetting seed（對齊 Stage 77 既有 pattern）/ Down() 對稱（DropColumn + DeleteData）

**鏈 B — PetraInboxRepository.Enqueue 簽名擴**：
- [`PetraInboxRepository.Enqueue`](src/AiTeam.Data/Repositories/PetraInboxRepository.cs) 加 `string? attachmentsJson = null` param — Data layer 0 ImageAttachment type 依賴 / 對齊議題 P2 路線 A

**鏈 C — CeoAgentService 接通 images + 限制紀律守 + workflowResolver ctor dep**：
- [`CeoAgentService.cs`](src/AiTeam.Bot/Agents/CeoAgentService.cs) method body 修：① 限制紀律守（三層守第三層 / 截 maxCount + skip > maxSizeBytes）② JSON 序列化含 Type discriminator（camelCase 對齊 CeoCommandController.ImagesJsonOptions）③ Repository.Enqueue 接通 attachmentsJson — Stage 75 line 39 漏接根因修
- ctor 加 `WorkflowSettingsResolver workflowResolver`

**鏈 D — PetraInboxProcessor → PetraDispatchWorker → PetraOrchestratorService.StartAsync chain 加 images**：
- [`PetraDispatchWorker.cs:108-156`](src/AiTeam.Bot/Orchestration/Petra/PetraDispatchWorker.cs) DispatchOneAsync load row 段加 `row.Attachments` 反序列化 + StartAsync 傳 images
- 新加 `DeserializeImageAttachments` private static helper（容錯：JSON parse 失敗 log warning + return null / 未知 type 略過半抽象 future-friendly）
- [`PetraOrchestratorService.StartAsync`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) 簽名擴 `IReadOnlyList<ImageAttachment>? images = null`（virtual 保 Stage77 Mock fixture override path）

**鏈 E — Petra LLM call 3 call sites 含 images**：
- DecideAsync line 230：`provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct, images)`
- DecideTalentsAsync line 411 對齊
- DecideTalentsWithPlanAsync line 478 對齊
- 3 helper 簽名擴 `IReadOnlyList<ImageAttachment>? images = null` param

**鏈 F — SubtaskPlan.Subtask 擴 NeedsImageContext + Parser + Petra prompt 教學 + 條件性 dispatch**：
- [`SubtaskPlan.cs:31`](src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs) Subtask record 加 `bool NeedsImageContext = false` field（C# record positional default false / backwards-compatible）
- SubtaskPlanParser SubtaskDto 加 NeedsImageContext field + TryParse 對齊
- BuildPetraSystemPrompt useSubtaskPlanning=true path 加段「判斷每個 subtask 是否需要附圖 context」+ 3 balanced few-shot 範例（UI bug case true / 後端 case false / docs case false）+ output section 加 needsImageContext 正例
- BuildInputMessagesForSubtaskAsync 簽名擴 `Subtask currentSubtask` + `IReadOnlyList<ImageAttachment>? images` — 內部條件性 ChatMessage Contents 構造（NeedsImageContext=true AND images != null → first user message 替換為 multi-modal Contents 含 DataContent）
- DispatchTalentsAsync + DispatchWorkersAsync 簽名擴 images param + 2 caller（並行段 + sequential 段）propagate plan.Subtasks[i] + images
- 新加 BuildFirstWorkerInput helper（v5 path / IAgentTool / 0 SubtaskPlan 場景簡化：images != null AND 第一個 worker → 附 image / 後續純 text）

**鏈 G — ClaudeCodeChatClientAdapter workspace 圖檔 + subprocess prompt path reference**：
- [`ClaudeCodeChatClientAdapter.GetResponseAsync`](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs) 加 WriteImageContentsToWorkspaceAsync call → 從 ChatMessage Contents 取 DataContent → 寫 `.tmp/images/{guid8}_{index:D3}.{ext}` → prompt 加段「【附圖檔路徑】第 N 張圖：...」
- finally block 清理 workspace 圖檔（worker 跑完 subtask 清 / 0 擾 git workspace + Cody commit 紀律）
- 對齊 Claude Code CLI 真實機制（prompt 內 reference 檔案路徑 / 0 base64 inline / 0 `--image` flag — amanhimself blog/felloai guide）

**鏈 H — CeoCommandController API verify 後備層 + WorkflowSettingsResolver + Dashboard hardcoded → AppSetting**：
- [`WorkflowSettings.cs:118-122`](src/AiTeam.Bot/Configuration/WorkflowSettings.cs) 加 2 default（MaxAttachmentsPerTask=5 / MaxAttachmentSizeMB=5）
- [`WorkflowSettingsResolver.cs:107-112`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs) 加 2 method `GetMaxAttachmentsPerTaskAsync` + `GetMaxAttachmentSizeMBAsync`（範圍守 [1, 20] / 對齊 Stage 77 GetIntInRangeAsync pattern）
- [`CeoCommandController.cs:63-79`](src/AiTeam.Bot/Api/CeoCommandController.cs) API verify 後備層（超 maxCount → 400 / 單張 > maxSizeBytes → 400 含明確訊息）
- [`QuickCommandCard.razor.cs:32-50`](src/AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor.cs) hardcoded const `MaxFiles` / `MaxFileSize` 改為 field + OnInitializedAsync 從 DashboardAppSettingsService 動態載入（範圍守 [1, 20]）
- [`QuickCommandCard.razor:51`](src/AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor) UI 提示「最多 @_maxFiles 張，每張 ≤ @(_maxFileSize / 1024 / 1024)MB」對齊動態
- OnFilesValidated error message line 83 也改動態 `{_maxFileSize / 1024 / 1024}MB`

**鏈 I — GeminiProvider multimodal 補（議題 P1 路線 A）**：
- [`GeminiProvider.cs:35-50`](src/AiTeam.Bot/Agents/GeminiProvider.cs) 移除既有 line 33-34「忽略 images + log warning」段 → 改為 userParts 構造 `GeminiPart.InlineData = new GeminiInlineData { MimeType, Data }` 多 modal request
- 加 DTO `GeminiInlineData`（mimeType / data 對齊 [Gemini API multimodal doc](https://ai.google.dev/gemini-api/docs/vision)）
- GeminiPart.Text 改 nullable `string?`（image part 不傳 text）

**鏈 J — xUnit test 補**：
- 既有 4 Mock fixture 簽名擴（PetraOrchestratorServiceTests 3 處 reflection invoke + Stage77MultiConsumer StubPetraOrchestrator override）
- 新加 Test31 SubtaskPlanParser NeedsImageContext 4 case（missing field default false / 顯式 true / 顯式 false / mixed）
- 新加 Test32 SubtaskPlan.Linear / Empty NeedsImageContext default false 守護

**鏈 K — version bump**：
- [`Directory.Build.props`](src/Directory.Build.props) v3.70.0 → **v3.71.0**（3 處同步：Version / AssemblyVersion / FileVersion）

### 關鍵設計決策

**1. 議題 P1 路線 A — 補 GeminiProvider multimodal**（Christ 全採納 Forge 推薦）
Forge plan 階段 grep verify 揭真實 [`GeminiProvider.cs:33-34`](src/AiTeam.Bot/Agents/GeminiProvider.cs)「忽略 images + log warning」— Roadmap §W7 escalate trigger 成立。3 路線拍板路線 A：對齊 Stage 79 image flow 核心精神「Petra 必須看圖」+ Christ 偏好 Gemini 低 cost + AnthropicProvider 既有 pattern reference + scope 擴 1 file ~50 行 + 2 DTO new。拒絕路線 C（違反核心 + Trial_v23 業務驗收會踩）+ 路線 B（短期治標 cost up）。

**2. 議題 P2 路線 A — Repository 純字串簽名**（Aria 推 B → Forge spike 揭 A 更精準）
Forge grep verify ImageAttachment 22 caller / 0 in AiTeam.Data project — Aria Roadmap §W5 推升 Shared 前提是 Repository 簽名收 typed param，但實質可純字串。**選 A**：Bot project caller（CeoAgentService）負責 JSON 序列化 + Type discriminator / Data layer 0 type 依賴 / 0 namespace 升 14+ file using 改 / scope 最小 — 對齊 Christ「對冗餘不容忍」+「最小抽象紀律」。Aria 修正接受 Forge 推 A（自省點 #39 候選 — Aria 規劃 over-engineering 偏好往複雜走慣性）。

**3. 議題 P3 範圍精準 — Dashboard 既有 hardcoded → AppSetting 動態 + 後備層補強**
Forge grep verify 揭 Dashboard 端 [`QuickCommandCard.razor.cs:38-76`](src/AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor.cs) **已有完整 4 層 validate**（非圖片 + 過大 + 超量 + 友善訊息）但是 hardcoded const。子項 11 真實範圍縮為「source of truth 改 AppSetting」+ 後備層補強，不是「從 0 開始建限制」。

**4. 半抽象 future-friendly 設計**（Christ 2026-05-19 拍板紀律）
schema 抽象（column `Attachments` jsonb + JSON 內 `Type` discriminator）/ code 不抽象（ImageAttachment record + NeedsImageContext flag 留 image-specific）。未來擴展 PDF/document：加 PdfAttachment record + 對應 NeedsPdfContext flag + schema 0 migration（DeserializeImageAttachments 未知 type 已 skip 不擋既有 image dispatch path）。

**5. Petra prompt 教學硬編進 method body**（Plan v1 拍板）
NeedsImageContext few-shot 範例硬編進 [`BuildPetraSystemPrompt useSubtaskPlanning=true`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) outputSection。對齊 Stage 70 既定 path / 0 額外動 Stage 73 DB-as-Prompt 範圍。Trial_v23 + Christ 拍板切 DB-as-Prompt default 後（後續 Stage）統一搬遷 prompt 教學進 DB / 對齊「最小可行」紀律。

**6. ChatMessage Contents multi-modal 構造對齊 Microsoft.Extensions.AI**
真實 API：`new ChatMessage(ChatRole.User, IList<AIContent>)` + `new TextContent(string)` + `new DataContent(byte[] data, string mediaType)`。在 BuildInputMessagesForSubtaskAsync + BuildFirstWorkerInput + ClaudeCodeChatClientAdapter.WriteImageContentsToWorkspaceAsync 三處對齊。`DataContent.Data` 是 `ReadOnlyMemory<byte>` / 用 `.ToArray()` 寫檔。

**7. ClaudeCodeChatClientAdapter 條件性 dispatch — 自動由 messages 內 DataContent 觸發**
Adapter 不需額外 flag — caller PetraOrchestratorService 只在 NeedsImageContext=true 時 propagate image AIContent → adapter 內自動「messages 有 DataContent → 寫圖 + prompt path / 0 DataContent → 純 text」運作 / 0 額外控制 flag 紀律。

**8. v5 path（DispatchWorkersAsync）簡化紀律**
v5 path 0 SubtaskPlan / 0 NeedsImageContext flag — 簡化「images != null AND 第一個 worker → 附 image / 後續純 text」對齊 v5 baseline。實際 v5 path Stage 67 後 0 production caller（flag 切 true 全走 v5.5）但 fallback path 留著守 0 regression。

### 驗收後修正（Wave 2 build/test 修根因 / Forge 自抓自修）

**修正 1**：build error CS0115 — `Stage77MultiConsumerTests.StubPetraOrchestratorService.StartAsync` override 簽名沒對齊新 StartAsync（images param 加後）→ 修簽名 + 對齊 [`Stage77MultiConsumerTests.cs:65-69`](src/AiTeam.Bot.Tests/Orchestration/Stage77MultiConsumerTests.cs)

**修正 2**：3 處 reflection invoke fail — `PetraOrchestratorServiceTests.Test12/29/30` reflection invoke DispatchWorkersAsync + 2× DispatchTalentsAsync args array 數量沒對齊新簽名 → 加 `null` images param 對齊。對齊 Stage 78a-c **source of truth 紀律第 16 次累積**（每個 simple 簽名擴改後 grep verify caller propagation 完整 + Mock fixture 同步擴）。

**修正 3**：Dashboard razor markup 同步動態化 — line 51 提示「最多 5 張，每張 ≤ 5MB」+ OnFilesValidated line 83 error message 既寫死 → 改 `@_maxFiles` / `@(_maxFileSize / 1024 / 1024)MB` 動態 reference。對齊 Stage 75/77 既有 pattern + Christ「對冗餘不容忍」紀律。

### Mock 覆蓋情況

**新業務功能 + 純 refactor 混合**：
- Bot.Tests baseline 100 → 102（加 Test31/32 / 既有 100 全綠）
- Generated 127 全綠 baseline 對齊 / Stage 79 0 影響 Playwright UI test
- xUnit 4 Mock fixture 簽名擴 — 對齊新 StartAsync + DispatchTalentsAsync / DispatchWorkersAsync 簽名（reflection invoke args propagate）
- backwards-compatible 守護：既有 7 PetraInbox row Attachments=NULL / 0 image / 整 chain 0 行為改變 Trial_v22 baseline 對齊

**Aria gate2 範圍**（Trial_v23 + Christ 視覺驗收 / Forge 不主動觸發燒餘額）：
- 場景 C: Dashboard 附圖 → PetraInbox.Attachments JSON 真實寫入
- 場景 D: Petra LLM call 真實含 image / token_logs InputTokens 偏高
- 場景 F: Worker dispatch 條件性 image fire（NeedsImageContext=true vs false 對照）
- 場景 G: v5.5 path 0 regression 純文字 baseline 對齊 Trial_v22
- 場景 H: Trial_v23 真實業務驗 4 Stage 一次

### 踩坑紀錄

**坑 1 — W7 GeminiProvider 0 multimodal 真實揭**（Forge plan 階段 grep verify）
Roadmap §W7 預警「若 Gemini Provider 真實未實作 multimodal → escalate Christ」— grep verify [`GeminiProvider.cs:33-34`](src/AiTeam.Bot/Agents/GeminiProvider.cs) 真實明文「忽略 images」trigger 成立。**教訓**：Roadmap WebSearch 結論揭業界 framework 真實支援不等於 AiTeam 既有實作真實支援 / 必 grep verify code-level — 對齊 Stage 67 「framework 結論必驗 AiTeam 真實場景 fit」紀律延續。

**坑 2 — Mock fixture reflection invoke args propagation**（Wave 2 build/test 修根因）
PetraOrchestratorServiceTests 3 處 reflection invoke private DispatchXxxAsync 用 args array 數量 = 既有簽名 param 數 — Stage 79 加 images param 後既有 3 invoke 全 fail。**教訓**：每個 private method 簽名擴必對應 grep verify reflection invoke caller（不只 public method caller / 對齊 Stage 78a-c source of truth 紀律第 16 次累積）。

**坑 3 — Dashboard hardcoded const 不容易 spike 揭**（Plan 階段 §H.3 範圍精準化）
Plan v1 §H.3 起初規劃「子項 11 加限制紀律 + AppSetting 可調」— Forge spike 揭真實 Dashboard 端 4 層 validate 100% 既有 hardcoded → 子項 11 真實是「source of truth 改 AppSetting」+ API/Repository 後備層補強，不是「從 0 開始建」。**教訓**：Plan 階段對既有 UI / 既有 validate logic spike 揭真實 vs 從 zero 建 — 對齊「最小可行 + 對冗餘不容忍」紀律。

**坑 4 — camelCase JSON 序列化 inconsistency 風險**（Plan v1 §R9 預警 / 實作對齊）
CeoCommandController.ImagesJsonOptions 既有 camelCase pattern — Stage 79 Repository 寫 JSON / PetraDispatchWorker 反序列化必對齊（type/base64Data/mediaType 全 camelCase）。實作期 CeoAgentService 加 `AttachmentsJsonOptions` static `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` 對齊 CeoCommandController 既有 pattern。**教訓**：跨 layer JSON 序列化必對齊既有 naming policy（PascalCase vs camelCase 一致性 / 對齊既有 caller propagate）。

**坑 5 — Microsoft.Extensions.AI DataContent API 真實 spike**（Plan v1 §R6 預警 / 實作對齊）
真實 API：`new DataContent(byte[] data, string mediaType)` ctor + `DataContent.Data` 是 `ReadOnlyMemory<byte>` / 用 `.ToArray()` 寫檔 + `DataContent.MediaType` 是 `string?` nullable / 用 `dc.MediaType is { } mediaType` pattern match 守 null。對齊 [IChatClient doc 2026-03 update](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)。**教訓**：framework API spike 必對齊真實 ctor + property nullable + ReadOnlyMemory vs byte[] 差異（Plan 階段 WebSearch reference 不等於實作期 code-level fit / 對齊 Stage 67 結論延續）。

### 校準錨真實落點

對齊 Roadmap §校準錨預估 + 自省點 #37 第 9 次累積實證候選：

- **Plan v1 預估**：raw 130-180K × ratio ×1.5-2.5 = 真實 ~300-450K（中位 ~375K）
- **真實落點**：Plan 階段 + 實作 + Wave 2 修正 + 自驗 + 結案 — 預估在預估區間中段下緣（~280-350K Opus 1M safety 30-50%）— 對齊 Stage 78c 揭「ultrathink 預估上界偏高 / 真實落點往中段下緣」反向校準延伸 + Aria 預估 600-800K 偏高
- **Model**：Opus 1M + high 推薦對齊（Christ 自決升 Extra high 未觸發 / safety 充裕）
- **Stage 期間 AiTeam 餘額影響**：**0**（純 Forge subscription / 0 真實 API call spike — 對齊 R11 紀律）

**新校準錨類型實證**（Stage 79 新業務功能 + 條件性 propagation + 半抽象 future-friendly）：
- vs Stage 78a-c 大規模架構級重構 ×1.57-4.93 中段（500-700K）
- vs Stage 60-62 production-ready 補強 ×0.78-0.99 區間
- Stage 79 性質：M+ 新業務功能 + 1 輪 spike + 半抽象設計 → 真實落點預估中段下緣 / 較 production-ready 補強上限偏高 / 較大規模架構重構下限偏低

### Phase 4 路徑下一步

對齊 Roadmap §Phase 4 路徑：
```
Stage 78a ✅ → 78b ✅ → 78c ✅ → 79 ✅（本 Stage / v5.5 image flow / M+）
            → Trial_v23（Aria gate2 / 驗 78a/b/c 砍 + image flow 4 Stage 一次 / Christ 觸發）
            → 80（A HITL plan confirmation 閘門 / M）
            → ...
```

**等 Aria gate2 + Christ 觸發 Trial_v23 真實業務驗** — Trial_v23 預期 cover：
- 純文字 prompt baseline 對齊 Trial_v22 0 行為改變（場景 G）
- 附圖 prompt UI bug case → Petra 真實看圖 + 拆 plan needsImageContext=true → Cody dispatch 真實看圖修 UI bug + PR 真開（場景 C+D+F+H）
- 對齊連續 16 Trial 業務級成功紀律延續（Trial_v10-v22 連續 13 Trial 業務級成功）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v2.0 | 2026-05-19 | **Forge 結案第一段 ✅** — commit [`0f9176e`](https://github.com/darkleong/AiTeam/commit/0f9176e) + 本機驗證 + 自驗 PASS。**實作完成 11 鏈 / 13 子項**：① §A PetraInbox.Attachments jsonb nullable + AppDbContext 配置 + Migration AddColumn + InsertData 2 AppSetting seed ② §B Repository.Enqueue 純字串簽名（議題 P2 路線 A）③ §C CeoAgentService 接通 images + 限制紀律守 + workflowResolver ctor dep + camelCase JSON ④ §D PetraDispatchWorker 反序列化 + StartAsync 簽名擴 images ⑤ §E Petra 3 LLM call sites 含 images ⑥ §F SubtaskPlan.NeedsImageContext + Parser + Petra prompt few-shot + 條件性 dispatch ⑦ §G ClaudeCodeChatClientAdapter `.tmp/images/` 寫圖 + prompt path + finally 清理 ⑧ §H Resolver 加 2 method + CeoCommandController API verify 後備 + Dashboard hardcoded → AppSetting ⑨ §I GeminiProvider multimodal（議題 P1 路線 A）⑩ §J Mock fixture 4 處簽名擴 + Test31/32 ⑪ §K v3.71.0。**3 議題 Christ 全採納 Forge 推薦**：P1 路線 A 補 GeminiProvider multimodal / P2 路線 A 純字串簽名（Aria 推 B → Forge 推 A 更精準）/ P3 範圍精準（既有 hardcoded → AppSetting 動態 + 後備層補強）。**驗收後修正**：Wave 2 build/test 修根因 3 處 Mock fixture reflection invoke 簽名擴（Stage 78a-c source of truth 紀律第 16 次累積）+ Dashboard razor markup 動態化（line 51 提示 + line 83 error message）。**5 踩坑紀錄**：① W7 GeminiProvider 真實未實作 trigger 成立（Forge plan 階段 grep verify 揭）② Mock fixture reflection invoke args propagation（Wave 2 修根因）③ Dashboard hardcoded 範圍精準（從 zero 建 → source of truth 改 + 後備層補強）④ camelCase JSON 序列化 inconsistency 守 ⑤ Microsoft.Extensions.AI DataContent ctor + ReadOnlyMemory + nullable MediaType API spike。**Forge 自驗 PASS 7 場景**（Aria gate1 範圍）：場景 B Migration apply + B-seed InsertData + I Bot startup 0 exception + I-schema petra_inbox Attachments jsonb + J-count API 400 超 5 張 + J-size API 400 單張超 5 MB + R5 既有 row backwards-compatible。**Aria gate2 範圍**（Trial_v23 + Christ 視覺驗收 / Forge 不主動觸發燒餘額）：場景 C/D/F/G/H 留 Trial_v23 真實業務驗 4 Stage 一次。**校準錨**：Plan v1 raw 130-180K × ratio ×1.5-2.5 = 真實 ~300-450K（中位 ~375K）/ Opus 1M + high safety 30-50% 對齊預估 / Stage 期間 0 燒 AiTeam 餘額 / 自省點 #37 第 9 次累積實證候選新業務功能 + 半抽象 future-friendly 設計類型校準錨。 |
| v1.2 | 2026-05-19 | **Christ 戰略 question 揭真實 cost 結構 + Effort 反向校準修正**（Aria 慣性 propagate 自省點 #38 + Christ 連續紀律推 Extra high 兩條盲點）：① 砍 Roadmap header `cost 預估：$X-Y per cycle` 欄位 — Stage 期間 Aria + Forge session 走 Claude Code subscription / 0 燒 AiTeam 餘額（除非 Forge 自驗實際 API call spike 才燒 / Stage 79 預期 0 API call spike）/ Trial cost 預估留 Trial_v23 計劃書（對齊 Roadmap 是 Stage scope 文件不是 cost reference 紀律）② Model + Effort 推薦 Extra high → **high**（範圍 M+ / Opus 1M safety 20-45% / Christ 自決升 Extra high 兜底是 Christ 動作不是 Aria 義務 / 對齊 Stage 78c 揭「ultrathink 預估上界偏高」反向校準紀律延伸）③ header `Stage 期間餘額影響：0` 段新加（明寫真實 cost 結構分層）④ 校準錨段 cost 預估改「Stage 期間 AiTeam 餘額影響：0」。**Aria 自省點 #39 立檔候選**（結案第二段 / /aria-end 統一升級進 memory）：① Roadmap header 不該慣性 propagate cost 欄位（對齊「對冗餘不容忍」+ Christ 真實行動價值評估紀律）② Effort 推薦反向校準（基於 Stage 性質+規模 / 不對齊 Christ 連續紀律推高 / Christ 自決升一級兜底是 Christ 動作不是 Aria 義務）③ workflow_aria.md 第三節 A / workflow_aria_model_effort.md 對應紀律 update。**對齊「對等和互相」紀律延伸**（自省點 #36）— Christ 戰略 question 點破真實揭 Aria 預估精度倒退 / 健康反思。 |
| v1.1 | 2026-05-19 | **Christ 拍板路線 A + Aria 補強升級** — ① 半抽象 future-friendly 命名（PetraInbox schema column `Attachments` jsonb + JSON 內 `Type` discriminator field 預留未來擴展 PDF/document 等 0 schema migration / ImageAttachment record + SubtaskPlan.NeedsImageContext flag 留 image-specific 對齊 Stage 79 範圍精準 / 拒絕全抽象 scope 擴 +20%）② 加新子項 11 **Attachment 限制紀律 + AppSetting 可調**（per attachment max 5 MB / per task max 5 張 / 對應 AppSetting `Workflow:MaxAttachmentsPerTask` + `Workflow:MaxAttachmentSizeMB` + Migration InsertData seed 對齊 Stage 75/77 既有 pattern）③ 加新子項 12 **Dashboard MudFileUpload MaxFiles + API verify 違規處理**（三層守：Dashboard MaxFiles UI 阻止 + API 400 Bad Request + Repository 後備截前 N 個 log warning）④ 設計決策段 7 加「半抽象 future-friendly 設計」段 ⑤ 驗收場景 J 加「Attachment 限制違規處理三層守」⑥ Aria 預警 W9 「半抽象命名邊界」+ W10「AppSetting + Migration InsertData seed」⑦ header + 戰略脈絡 + 子項 1+2 命名更新對齊半抽象 ⑧ 子項編號 11→13（version bump 移至最後）。**檔案類型範圍拍板**：Stage 79 只做 Image（AiTeam 真實 90%+ scenario）/ PDF / document 等留 Future Feature「v5.5 multi-attachment flow 補完」候選真實需求觸發才做 / 半抽象 schema 預留擴展 0 schema migration。 |
| v1.0 | 2026-05-19 | 規劃書建立 — v3.71.0 / M+ 規模 / v5.5 Phase 4 image flow 補完（Stage 75 切 PetraInbox 設計遺漏修根因 + 條件性 worker propagation）。**戰略脈絡**：Christ 2026-05-19 戰略 question 揭 Dashboard 附圖 Petra 看不到 gap + WebSearch 業界紀律拍板「pass images only to worker agents that need them」+ Claude Code CLI 真實圖片支援機制 WebSearch 確認（workspace 檔案 reference / 0 base64 / 0 `--image` flag）。**11 子項**：① PetraInbox schema 擴 Images jsonb column + Migration ② PetraInboxRepository.Enqueue 簽名擴 ③ CeoAgentService.ProcessWithClaudeCodeAsync 接通 images 修 Stage 75 漏接根因 ④ PetraInboxProcessor → PetraDispatchWorker → PetraOrchestratorService.StartAsync chain 加 images ⑤ Petra LLM call 3 call sites 含 images（DecideTalents/Skill/SubtaskPlan）⑥ SubtaskPlan.Subtask record 擴 NeedsImageContext flag ⑦ Petra prompt（TalentPrompt PM）教學如何判斷 NeedsImageContext（UI 修改 → true / 純後端 / docs → false）⑧ ClaudeCodeChatClientAdapter 條件性 image dispatch（workspace 寫圖檔 + subprocess prompt path reference）⑨ workspace 圖檔清理 + 多 image 處理 ⑩ xUnit test 補 ⑪ Directory.Build.props v3.70.0 → v3.71.0。**計劃前 WebSearch 結論 3 段完整 incorporated**：① Claude Code CLI 圖片支援機制（無 `--image` flag / 真實機制 prompt 內 file path reference）② IChatClient multimodal 支援（ChatMessage 含 image AIContent）③ Multi-agent image propagation 業界 best practice（only give what agent needs / supervisor 條件性決定 / multimodality native feature）。**設計決策核心**：路線 A image schema baseline（PetraInbox jsonb column 同表 / AiTeam 真實場景 1-3 image 典型 / row 5-15 MB 可接受）+ NeedsImageContext per subtask Petra decide 紀律 + Claude Code CLI workspace 檔案 reference 機制 + backwards-compatible 守護紀律延續 + Migration ADD column nullable 0 不可逆風險。**驗收 9 場景**：A xUnit baseline + 新 test / B Migration ADD column / C Dashboard 附圖 → PetraInbox 寫入 Images / D Petra LLM call 真實含 image / E SubtaskPlan NeedsImageContext flag / F Worker dispatch 條件性 image fire / G v5.5 path 0 regression 純文字 baseline / H Trial_v23 真實業務驗 4 Stage 一次 / I Bot startup 0 exception。**Aria 預警 W1-W8**：image schema 路線拍板 / Petra prompt 教學精準度 / workspace 圖檔 path 設計 / Claude Code CLI subprocess image dispatch spike / ImageAttachment namespace 升 Shared 評估 / backwards-compatible 既有 row 0 images / GeminiProvider multimodal 支援 verify（必前置 spike）/ SubtaskPlan record 加 field 對齊。**校準錨預估**：對齊大規模架構級重構新區間 ×1.30-4.93 中段下緣 / raw 130-180K × ratio ×1.5-2.5 = 真實 ~200-450K / Opus 1M + Extra high safety 20-45% / cost $3-5。**Phase 4 路徑**：78a ✅ → 78b ✅ → 78c ✅ → **79**（本 / v5.5 image flow / M+）→ Trial_v23（驗 4 Stage）→ 80（HITL / M）→ Trial_v24（驗 HITL 業務體驗）→ 81（動態 replan / L）→ WebUI Stage → v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1 + Aria gate2 production 0 regression 驗 → 通過後 Trial_v23 開（4 Stage 一次 cover）。 |

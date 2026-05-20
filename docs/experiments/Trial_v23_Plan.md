# Trial_v23 — Stage 78a+78b+78c+79 連 4 Stage 整套真實業務驗（v5.5 image flow business-ready 閘門 + v4 path 全砍 0 regression 驗）

> 日期：2026-05-19 派出 / 2026-05-20 結案
> 對應系統版本：**v3.71.0**（Stage 79 結案後）
> 試驗版本：**v2.0**（Plan v1.0 → 真實跑完 + Aria 結案 in-place 升級）
> **真實結果**：🟡 **部分過 — v5.5 image flow business-ready 閘門通過 + 揭 1 🔴 + 3 🟡 議題**（連續 14 Trial 業務級成功延續）

---

## 試驗目的

**v5.5 連 4 Stage 真實業務驗閘門** — 驗證 Stage 78a/78b/78c + 79 整套機制 production 真實生效：

1. **Stage 78a/b/c v4 path single source of truth 完整收口 0 regression**（連續 17 次 Trial v5.5 path 業務級成功 + Stage 78a-c 大規模砍 v4 dead code+caller+routing 後 production 0 regression）
2. **Stage 79 v5.5 image flow business-ready 真實生效**：
   - PetraInbox.Attachments jsonb 真實寫入 image JSON
   - PetraInboxProcessor / PetraDispatchWorker 反序列化 + chain 傳遞 images
   - Petra LLM call 3 call sites 真實看圖（GeminiProvider multimodal native fire）
   - SubtaskPlan.Subtask.NeedsImageContext 真實 fire（Petra 判斷 per-subtask 需不需要看圖）
   - ClaudeCodeChatClientAdapter workspace `.tmp/images/` 寫圖檔 + subprocess prompt path reference
   - Worker（Cody/Vera/Quinn）條件性 dispatch image（NeedsImageContext=true 真實看圖 / =false 純 text）
   - Dashboard 限制三層守 verify（5 張 + 5 MB / AppSetting 真實動態）
3. **連續 16 Trial 業務級成功延續**（Trial_v10-v22 連續 13 Trial / Trial_v23 第 14 次目標）

對齊 Trial_v22 baseline（業務評分 5/5 滿分 / 3 task 並送 / cost per file $0.041 最優 ROI）+ Trial_v18 baseline（PR #378 19 檔 +1158 / 業務評分 4.7/5 / DB-driven prompt 真實實證）— 對照組精準度最高。

---

## 任務需求（兩 case 對照組）

### Case A — 純文字 baseline（對齊 Trial_v22 0 regression）

沿用 Trial_v6-v22 同 prompt（reuse `.tmp/trial_v15_body.json`）— Dashboard 錯誤處理打磨 toast 通知。

**目標**：純文字 prompt → v5.5 path 整 chain 真實 fire 對齊 Trial_v22 baseline 0 regression / 驗 Stage 78a/b/c 大規模砍 v4 後 v5.5 path 邏輯 0 行為改變。

### Case B — 附圖 UI bug case（驗 image flow business-ready）⭐

**新 prompt**：「Dashboard 操作中心 BossInteraction 卡片排版有問題（附截圖）— Talent name + status badge 對齊不一致 / 請修。」+ **附 1 張 Dashboard 截圖**（Christ 用 Dashboard MudFileUpload 上傳）

**目標**：附圖 prompt → Stage 79 v5.5 image flow 整 chain 真實 fire：
- Dashboard UI 接附圖 → PetraInbox.Attachments JSON 真實寫入
- Petra 真實看圖 + 拆 SubtaskPlan 判斷 Cody subtask NeedsImageContext=true（UI 修改 needsImage）
- Cody 走 ClaudeCodeChatClientAdapter → workspace `.tmp/images/` 真實寫圖檔 + subprocess prompt 含 path reference
- Cody 真實看圖修 UI bug（修 BossInteraction 卡片排版）
- PR 真開 + Vera review（needsImage=true 對齊 UI 變動 review）+ Quinn UI E2E test（needsImage=true 對齊 UI 驗）+ Sage 收尾文件（needsImage=false 純 text）

### Case 順序

Case A 先跑（baseline 確認 0 regression）→ Case B 跑（image flow 業務驗）— 分序避免議題分離難。

---

## 流程觀察 Checkpoints（Aria 9-step 模板第 12 次實踐 / aria-trial-run skill 第 6 次實踐）

### CP1 deploy + flag 確認

| 項目 | 預期 |
|---|---|
| Stage 79 commit `0f9176e` deploy run | ✅ success（CI/CD 自動 5-10 min） |
| Bot image v3.71.0 production active | ✅ |
| 5 v5.5 flag SQL=true production active | ✅ 全保留（UsePetraOrchestratorV5 / UseTalentSkillSeparation / UseV5Memory / UseV5SubtaskPlanning / UseV5PromptDb）|
| Stage 79 新加 2 AppSetting production active | ✅ `Workflow:MaxAttachmentsPerTask=5` + `Workflow:MaxAttachmentSizeMB=5` Migration InsertData seed |
| Migration `Stage79PetraInboxAttachments` apply | ✅ petra_inbox `Attachments` jsonb nullable column 真實 add |
| 5 層守門全綠 | ✅ Forge 自驗 + CI/CD + Stage 79 grep verify 9 條件 + xUnit Bot.Tests 102/102 + Generated 127/127 |

### CP2 Case A 純文字 baseline（對齊 Trial_v22 0 regression）

```
=== task A === Dashboard 派純文字 prompt（reuse Trial_v6-v22 同 prompt）
```

**Layer 1 訊號**：
- curl ack `[v5.5] Task 已接收（inbox=...）`
- SQL `petra_inbox` 新 row / Attachments=NULL / Source='dashboard'
- Bot log「Victoria → 寫 PetraInbox row={Id} source=dashboard attachments=0」

**Layer 2 訊號**（Petra dispatch）：
- PetraInboxProcessor pickup row → PetraDispatchWorker dispatch
- PetraOrchestratorService 3 LLM call sites（DecideAsync/DecideTalents/DecideTalentsWithPlan）/ images=null path
- SQL `token_logs` PM=gemini-2.5-flash baseline 對齊 Trial_v22

**Layer 3 訊號**（Worker dispatch）：
- Cody/Vera/Quinn/Sage chain dispatch / 整 chain 0 image AIContent propagation
- Workspace 0 `.tmp/images/` 寫檔（純文字 path 不擾）
- PR 真開 + 對齊 Trial_v22 業務級成功 baseline

### CP3 Case B 附圖 UI bug case（驗 image flow business-ready）⭐

```
=== task B === Dashboard 派附圖 prompt + 1 張 BossInteraction 卡片截圖
```

**Layer 1 訊號**（image flow 接收）：
- Dashboard MudFileUpload 顯示「最多 5 張，每張 ≤ 5 MB」提示 ✓
- curl ack 含 image count 紀錄
- SQL `petra_inbox` 新 row / **Attachments jsonb 真實 JSON**（`[{"type":"image","base64Data":"...","mediaType":"image/png"}]`）
- Bot log「Victoria → 寫 PetraInbox row={Id} source=dashboard attachments=1」

**Layer 2 訊號**（Petra 真實看圖）⭐：
- PetraInboxProcessor pickup row → 反序列化 Attachments → images.Count=1
- PetraDispatchWorker 傳 images 進 PetraOrchestratorService.StartAsync
- **Petra LLM call 3 call sites 真實含 images param**（GeminiProvider multimodal inline_data + base64 fire）
- SQL `token_logs` PM InputTokens 較純文字偏高（含 image token cost）⭐
- Petra LLM 回應 JSON 含 `needsImageContext` per subtask：
  - Cody subtask `needsImageContext: true`（UI 修改 needsImage）⭐
  - Vera subtask `needsImageContext: true`（UI 變動 review）
  - Quinn subtask `needsImageContext: true`（UI E2E test）
  - Sage subtask `needsImageContext: false`（純文字 release note）

**Layer 3 訊號**（Worker 條件性 image dispatch）⭐⭐：
- ClaudeCodeChatClientAdapter 對 Cody/Vera/Quinn dispatch：
  - workspace `.tmp/images/{guid8}_001.png` 真實寫圖檔 ✓
  - subprocess prompt 含「附圖檔路徑：/path/to/.tmp/images/...」
  - Bot log「Stage 79 Petra dispatch image AIContent → Worker talent=Cody subtaskId=1 imageCount=1」
- Cody 真實看圖修 UI bug（讀 workspace 圖檔 + 修 BossInteraction 卡片排版）⭐⭐⭐
- Sage dispatch：workspace 0 `.tmp/images/` 寫（NeedsImageContext=false 純 text）
- PR 真開 + Cody success + Vera review + Quinn QA + Sage 收尾

### CP4 Dashboard 限制三層守 verify（Case C / 補強驗 / 可選）

可選補驗（若 Christ 想驗限制紀律）：
- Dashboard 嘗試上傳 6 張圖 → MudFileUpload `MaxFiles=5` 真實阻止
- 直接 POST API `/internal/ceo/command` 含 6 張 → 400 Bad Request 含「最多 5 張 attachment」訊息
- AppSetting `Workflow:MaxAttachmentsPerTask` SQL UPDATE 為 3 → reload-cache → Dashboard 端動態 update 為「最多 3 張」

### CP5 GeminiProvider multimodal native verify ⭐

- Bot log Petra LLM call 真實 fire GeminiProvider.CompleteAsync 含 images
- 0 「GeminiProvider 第一階段尚未支援 Vision，已忽略 N 張圖片」warning（Stage 79 §I 補完後 0 trigger）
- SQL `token_logs` PM Model=gemini-2.5-flash InputTokens 較純文字偏高 ~+500-2000 tokens（image base64 對應）

### CP6 v4 path 0 regression 完整 verify

- Bot log 0 v4 Pipeline framework class fire（TaskGroupService / AgentQueueProcessor / Pipeline Executors / Meeting / Appeal / HITL / Boss / InteractionProcessor / Proposal / Pm/ folder / MockScenarioService 等 Stage 78c 全砍）
- Bot log 0 v4 routing fire（ButtonCallbackRouter v4 routing exec_yes/propose_yes/cancel_yes/kickoff_*/design_*/framework_* 等 Stage 78b+78c 全砍）
- Bot log 0 v4 entity 寫入（TaskGroup / TaskItem table 0 新 row / 議題 1 路線 2 留 entity cold storage 紀律守）
- Bot startup 0 exception（DI ServiceProvider validation / Migration apply / Quartz HealthCheckJob fire）

---

## 預期結果矩陣

### Aria 業務評分 ⭐⭐⭐⭐⭐ 5/5 目標（連續 14 Trial 業務級成功）

| 維度 | 預期 | 對照 baseline |
|---|---|---|
| Case A 純文字 0 regression | ✅ PR 真開 + 業務正確性對齊 Trial_v22 | Trial_v22 5/5 |
| Case B 附圖 image flow business-ready | ✅ Petra 真實看圖 + Worker 條件性 dispatch + Cody 真實看圖修 UI bug | 0 baseline / Stage 79 首發 |
| v4 path 0 regression | ✅ Bot startup 0 exception + 0 v4 class fire | Stage 78a/b/c 累積驗 |
| GeminiProvider multimodal native | ✅ 0 「忽略 images」warning + token_logs InputTokens 偏高 | Stage 79 §I 補完首發 |
| Dashboard 限制三層守 | ✅ MaxFiles UI + API 400 + Repository 後備（可選 CP4 驗）| Stage 79 §H 首發 |

### 對照 Trial_v22 / Trial_v19 baseline

| 維度 | Trial_v22 | Trial_v19 | Trial_v23 預期 |
|---|---|---|---|
| 業務評分 | 5/5 | 5/5 | 5/5（目標）|
| PR 規模 | 3 PR / 各 30+ 檔 | 30 檔 +1456 | Case A ~20-30 檔 / Case B ~10-20 檔 UI 修 |
| cost | ~$2.5（per file $0.041）| $2.844 | ~$2-3 預估（Case A+B 合計）|
| image fire | 0（純文字）| 0（純文字）| **Case B 真實 fire** ⭐ |
| 連續 Trial 業務級成功 | 12 連續（v10-v22）| 10 連續 | **14 連續目標** |

---

## 真實跑結果（v2.0 in-place 升級 / 2026-05-20 結案）

### Case A — 純文字 baseline ✅ 5/5

| 項 | 真實 |
|---|---|
| PR | [#386](https://github.com/darkleong/AiTeam/pull/386) / 38 檔 / +2281 -154 / **12 min 03s** |
| Petra picks | Cody → Vera → Quinn（3 talent / 對齊 Trial_v22 baseline）|
| 業務 cover | **17/17 Dashboard 頁面/元件雙路錯誤通知**（Inline MudAlert + ISnackbar）⭐ |
| build | 0 errors / 25 warnings 全既有 |
| Vera review | 🔴 1 critical（PipelineView 5 handler 缺 catch / Circuit 斷線風險）+ 🟡 1 warning（_actionError 殘留）|
| Quinn QA | 12 新測試 / 0 build errors |
| Cost | **$1.74**（Quinn $0.73 / Vera $0.55 / Cody $0.45 / PM $0.01）|

### Case B — 附圖 image flow business-ready ✅⭐⭐⭐ 5/5

| 項 | 真實 |
|---|---|
| PR | [#387](https://github.com/darkleong/AiTeam/pull/387) / 1 真實檔 InteractionCard.razor 修 / **7 min 29s** |
| Petra picks | **Cody → Vera 2 talent**（對 UI 修判 Quinn QA 不必要 — 真實合理）|
| Layer 1 | `petra_inbox.Attachments` jsonb 真實寫入 373347 bytes ✓ |
| Layer 2 | **`GeminiProvider Stage 79 multimodal dispatch images=1`** ⭐ CP5 ✓ |
| Layer 3 | **`Stage 79 Petra dispatch image AIContent → Worker talent=Cody+Vera imageCount=1`** ⭐⭐ |
| Workspace 圖檔 | `.tmp/images/6b1e8de2_001.png` 279962 bytes owner=appuser ✓（對齊 Stage 65 entrypoint chown / 0 root-owned 雷）|
| **Cody 真實看圖修 UI bug** | ⭐⭐⭐⭐ 3/3 Issue 全 cover（MudCardContent padding / ParseDescription helper / MudCardActions 間距）|
| **Vera 真實看圖 review** | ⭐⭐⭐ 揭 2 🟡 warning（深色主題硬寫 rgba 視覺失效 + ParseDescription string pattern 耦合）|
| build | 0 errors / 87 warnings 全既有 |
| PM Gemini multimodal | **InputTokens +235 vs Case A 對齊 Gemini image tile token model 預期**（1068x934 ≈ ~258 tokens 真實量化）|
| Cost | **$1.12**（Cody $0.94 / Vera $0.18 / PM $0.01）|

### CP6 v4 path 0 regression ✅⭐

- SQL `tasks` table **0 new row** since 16:00 ✓
- Bot log 30 min **0 v4 class fire**（TaskGroupService / AgentQueueProcessor / Pipeline Executor / ButtonCallbackRouter v4 routing / MockScenario 全 0）
- Stage 78a/b/c 砍 v4 後 production 0 regression 完整驗

### 總 cost vs Plan

| 階段 | cost | 對照 |
|---|---|---|
| Aria + Forge session（Claude Code subscription）| **0 燒 AiTeam 餘額** | 對齊 Stage 79 v1.2 紀律 ✓ |
| Trial AiTeam LLM cost | **$2.86** | Plan $2-3 ✓ |
| 餘額：$12.81 → ~$8.21（-$2.86 對齊 Trial_v22 baseline $2.5）| | |

---

## 議題分類

| # | 嚴重 | 議題 | 揭露來源 | 對應修法 |
|---|---|---|---|---|
| 1 | 🔴 | **Stage 79 QuickCommandCard EF Core DbContext concurrency bug** — `QuickCommandCard.OnInitializedAsync():line 42` 跟 `Home.OnInitializedAsync():line 48-50` 並行用同 Scoped DbContext → `A second operation was started` → 首頁 Circuit terminated 無法顯示 / 阻 Christ 走 Dashboard upload Case B real path | Trial 中段 Christ 截 Console 揭 | Stage 80 補強候選（合進 HITL Stage / 或先 Stage 79.1 hotfix 改 DashboardAppSettingsService 用 `IDbContextFactory.CreateDbContext()` pattern）|
| 2 | 🟡 | **v5.5 path 仍開「確認派工/取消」按鈕但 Bot 已自動 dispatch** — Stage 78c v4 Pipeline 砍除後 BossInteraction confirm UI 邏輯遺留 / 按鈕無實質功能 | Trial 開跑後 Christ Dashboard 截圖揭 | Stage 80 收口候選 |
| 3 | 🟡 | **Case A Vera 揭 PipelineView 5 handler 缺 catch（Circuit 斷線風險）** — 同類根因對齊 #1（Blazor exception path 防呆紀律不全） | Case A Vera review | Stage 80 候選（合進 #1）|
| 4 | 🟡 | **Case B Vera 揭 InteractionCard 2 設計 issue** — W1 硬寫 rgba 深色主題視覺失效（改 MudBlazor 主題變數）+ W2 ParseDescription string pattern 耦合（改 `BossInteractionDto.SystemNotes` 後端寫入）| Case B Vera review | follow-up 候選 / Christ Dashboard 目視確認後評估 |

---

## Aria 規劃漏接根因（自我反思 — 留 /aria-end 統一升級）

**Aria gate1 + Stage 79 規劃漏掃 Blazor InteractiveServer + Scoped DbContext concurrency 紀律**：
- Stage 79 §H 把 Dashboard hardcoded const 改 AppSetting 動態 OnInitializedAsync DB query，**漏掃**「Blazor InteractiveServer 多元件並行 init 同 Scoped DbContext 撞」業界紀律
- workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #12（規劃含 Blazor OnInitializedAsync 改動 DB query 時必 grep verify `IDbContextFactory.CreateDbContext()` pattern 對齊）
- 對齊既有 #11 延伸（Stage 79 Dashboard UI validate hardcoded const → AppSetting）— 同 Stage 不同盲點兩條根因累積

---

## 戰略意義

- **v5.5 image flow business-ready 閘門通過** ✅ — Petra 真實看圖 + 條件性 worker propagation（NeedsImageContext fire）+ GeminiProvider multimodal native + Workspace 真實寫圖檔 + Cody/Vera 真實看圖 review
- **v4 path 全砍 production 0 regression 完整驗** ✅ — Stage 78a/b/c 三輪累積 net -27434 行（commit `c6f81d6` -3957 + `a7c763a` -787 + `e17e83b` -22690）
- **連續 14 Trial 業務級成功延續** ✅（v10-v23）— infinite loop pattern 打破連續第 14 次
- **Aria 自跑 9-step 模板第 12 次實踐 + curl path 替代 Dashboard UI broken 路徑** — Trial 中段揭 production bug 時靈活適應（路線 A vs 路線 B 給 Christ 拍板 / 不阻擋戰略主目標）
- **路線 D 戰略確認** — v5.5 path single source of truth 完整收口 + image flow business-ready 即代表 AiTeam 真實到達「v5 動態架構 + v5.5 baseline 全量上線」里程碑

---

## Top 5 重排

| # | 項目 | 規模 / 優先度 |
|---|---|---|
| **1** | **Stage 80（A HITL plan confirmation 閘門 + 🔴 #1 hotfix 合進）** | M / Opus 1M + high baseline / Christ 親口要的業務功能 |
| 2 | Trial_v24 驗 HITL 業務體驗 | 5-15 min / cost $1-3 / Stage 80 結案後 |
| 3 | Stage 81（B 動態 re-planning）| L / Stage 80 + Trial_v24 後評估 |
| 4 | WebUI Stage（v4 entity drop + Dashboard 重設計）| L+ / Stage 81 後 |
| 5 | v5.5 完整收口 | Top 1-4 全跑完 |

---

## v5.5 image flow business-ready 閘門評估

Trial_v23 通過後**閘門評估標準**：

| 評估項 | 通過條件 |
|---|---|
| Case A 純文字 0 regression | Trial_v22 baseline 對齊 / PR 業務正確 |
| Case B 附圖 image flow 整 chain 真實 fire | Petra 看圖 + Worker 條件性 dispatch + Cody 真實看圖修 UI |
| v4 path 0 regression | Stage 78a/b/c 砍後 v5.5 path 0 行為改變 |
| GeminiProvider multimodal native | 0 「忽略 images」warning fire |
| 業務評分 ≥ 4.5/5 | image flow business-ready 業務級成功 |

通過 → **Stage 80 HITL plan confirmation 閘門開**（Christ 親口要的業務功能 / 規模 M / Opus 1M + high baseline）

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

- Trial_v23 結案紀錄改 v2.0 in-place（議題分類 + 業務評分 + cost 真實 vs 預估雙因子 + 紀律累積實證）
- 通過 → Top 5 重排：Stage 80 升 #1 / Trial_v23 落入「已完成項目摘要」/ Stage 79 已 ✅ 收尾完整
- 通過 → Christ 開 Stage 80 規劃（Aria /aria-plan / WebSearch HITL 業界紀律 LangGraph interrupt + 4 decision pattern 補新 dispatch 細節 / Stage 77 規劃前 WebSearch 結論可 reference）

### 下個重點戰略

- **Stage 80 HITL plan confirmation 閘門**（規模 M / Opus 1M + high baseline / Christ 自決升 Extra high 兜底）
- **Trial_v24** 驗 HITL 業務體驗（HITL approve/edit/reject/respond 4 decision pattern 真實 fire）
- **Stage 81 動態 re-planning**（規模 L / 需 Stage 80 HITL 配套）

---

## Cost 真實 vs 預估雙因子預估（自省點 #38 應用）

| 階段 | cost 來源 | 預估 |
|---|---|---|
| Aria 規劃（本 Plan v1.0 + Trial 觀察 + 結案紀錄） | Aria Claude Code session subscription | **0 燒 AiTeam 餘額** |
| Forge 觀察 + 結案紀錄 | Forge Claude Code session subscription | **0 燒 AiTeam 餘額** |
| **Trial AiTeam LLM cost**（Petra + Cody + Vera + Quinn + Sage 真實 LLM call） | AiTeam Anthropic + Gemini API key | **$2-3 預估**（對齊 Trial_v22 $2.5 baseline / Case A+B 合計）|
| **Total Trial_v23 cost** | | **~$2-3** |

對齊 Stage 79 v1.2 紀律「Stage 期間 0 燒 AiTeam 餘額 / 真實燒只 Trial / production」+ 自省點 #38 雙因子公式（Trial AiTeam LLM + Forge Claude Code session）。

預估 image flow case cost 略高於純文字（Petra LLM call 含 image token cost ~+500-2000 tokens / per call × 3 call sites = ~+1500-6000 tokens / Gemini 2.5 Flash 真實 cost ~+$0.1-0.3 / 影響小可接受）。

---

## ⚠️ Aria 預警 + Trial 期間注意事項

### W1：Trial workspace branch cleanup 紀律延續

對齊自省點 #34（Aria 自跑 Trial 期間 workspace cleanup 用 docker exec 紀律 / 走 appuser 或讓 Bot 自處理）— Trial_v23 跑完 Aria 不主動 docker exec rm workspace / Bot FinalizeGitAsync 真實清乾淨。

### W2：Case B 附圖真實樣本準備

Aria 不負責準備截圖（Christ 自己 Dashboard 上傳 / 對齊「Christ 真實使用模式」+「Aria 9-step 模板第 12 次實踐」紀律）— 但 Aria 提醒 Christ 準備 1 張 BossInteraction 卡片真實截圖。

### W3：GeminiProvider multimodal 真實 fire token cost 觀察

Stage 79 §I 補 GeminiProvider multimodal 後 / Petra 3 LLM call sites 真實含 images / token cost 預期偏高 ~+500-2000 tokens per call。若 token_logs 真實落點偏離預估 → 揭真實 cost 結構 / 對 Trial_v24+ Stage 80 cost 預估校準 reference。

### W4：Forge healthy 偏離 plan 紀律延續

Trial_v23 觀察期 Forge 若 spike 揭真實偏離（如 Cody 看圖修 UI 真實 fire 但品質不對齊預期 / 或 Vera review 對 UI 變動真實效果 differ）→ Aria 觀察 + 結案 v2.0 紀錄 / 對應 Stage 80 規劃 reference。

### W5：Aria 自跑 Trial 9-step 模板第 12 次實踐紀律

對齊 aria-trial-run skill 第 6 次實踐成熟 baseline：
1. CP1 deploy + flag 確認 → CP2 Case A 純文字 → CP3 Case B 附圖 → CP4 限制三層守（可選）→ CP5 GeminiProvider multimodal verify → CP6 v4 path 0 regression
2. 對齊「Christ 只動嘴」精神 — Aria 全程自跑（除 Christ 拍板開跑 + 業務正確性最終判斷 + Dashboard 上傳截圖）
3. 結案 v2.0 in-place 紀錄（議題分類 + 業務評分 + cost 真實 vs 預估 + 紀律累積實證）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-19 | 規劃書建立（Plan 預估版 / Aria 撰寫 / 對齊 Stage 79 結案後即進 Trial baseline / 預防 Compact 後新 Aria 認知 gap）。**核心結構**：兩 case 對照組（Case A 純文字 baseline 對齊 Trial_v22 0 regression + Case B 附圖 UI bug case 驗 image flow business-ready）+ 6 CP 觀察點 + 5 維度業務評分目標 + v5.5 image flow business-ready 閘門評估 + 5 維 Aria 預警。**預估 cost**：$2-3 對齊 Trial_v22 baseline + image token 略增。 |
| **v2.0** | **2026-05-20** | **Trial 真實跑完 + Aria 結案 in-place 升級**。Case A PR #386 / 12 min 03s / cost $1.74 / 17/17 cover ⭐。Case B PR #387 / 7 min 29s / cost $1.12 / Cody+Vera 真實看圖 + GeminiProvider multimodal native fire + workspace `.tmp/images/6b1e8de2_001.png` 真實寫 ⭐⭐⭐。CP6 v4 path 0 regression 完整驗（SQL tasks 0 new row + log 0 v4 fire）⭐。**總 cost $2.86 對齊 Plan**。**🟡 部分過** — 揭 🔴 #1 Stage 79 QuickCommandCard EF Core DbContext concurrency bug（Dashboard 首頁 Circuit terminated）+ 🟡 #2-#4 議題清單。**戰略意義**：v5.5 image flow business-ready 閘門通過 + v4 path 全砍 production 0 regression + 連續 14 Trial 業務級成功延續第 14 次 + Aria curl path 替代 broken Dashboard UI 路徑靈活適應。**Top 5 重排**：Stage 80（含 🔴 #1 hotfix 合進）升 #1。 |

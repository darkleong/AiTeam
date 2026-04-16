# Stage 28a：Dashboard 雙向操作中心 — 基礎架構與按鈕回覆

> 對應 Future Feature：九（Phase 1）
> 對應版本：v3.14.0
> 建立日期：2026-04-16
> 狀態：✅ 已完成（2026-04-17）
> 文件版本：v2.0

---

## 概述

目前所有需要 Christ 介入的互動（CEO 確認、執行確認、Kickoff/Design 確認、Dev_plan 升級、合併/介入通知）都只發生在 Discord。Dashboard 是純「唯讀監控」，不能操作。

Stage 28a 建立雙向操作中心的基礎架構：
- **統一互動模型**（BossInteraction Entity）— 所有老闆互動持久化至 DB
- **Dashboard 待處理清單**（新頁面 `/interactions`）— 即時顯示所有待回覆項目
- **按鈕回覆能力** — Christ 可在 Dashboard 直接點擊按鈕回覆
- **雙通道同步** — Discord 回覆後 Dashboard 即時更新、Dashboard 回覆後 Discord 同步顯示

**本階段只處理按鈕類互動**（確認/取消/繼續/停止/跳過），文字輸入類互動（修改意見）留給 28b。

---

## 現有確認機制分析

### 目前架構

所有確認狀態存在 CommandHandler 的**記憶體字典**中，Bot 重啟即遺失：

| 字典 | Key | 用途 |
|------|-----|------|
| `_pendingConfirmations` | Discord MessageId | CEO 確認 / 執行確認 / 提案確認 / Dev_plan 升級 |
| `_pendingAdjustments` | UserId | 提案修改（等待文字輸入） |
| `_pendingKickoffConfirmations` | Discord MessageId | Kickoff 會議確認 |
| `_pendingKickoffModify` | UserId | Kickoff 修改（等待文字輸入） |
| `_pendingDesignConfirmations` | Discord MessageId | 設計會議確認 |
| `_pendingDesignModify` | UserId | 設計修改（等待文字輸入） |
| `_pendingCancelSelections` | UserId | 任務取消（等待選擇） |

### 互動類型清單（按鈕 vs 文字輸入）

| 互動類型 | 按鈕動作 | 需要文字輸入？ | 28a 範圍 |
|---------|---------|:-------------:|:-------:|
| CEO 決策確認 | confirm_yes / confirm_no | — | ✅ |
| Agent 執行確認 | exec_yes / exec_no | — | ✅ |
| 提案確認 | propose_yes / propose_no | — | ✅ |
| 提案修改 | propose_adjust | ✅ 修改意見 | ❌ 28b |
| Kickoff 確認 | kickoff_continue / kickoff_stop / kickoff_restart | — | ✅ |
| Kickoff 修改 | kickoff_modify | ✅ 修改意見 | ❌ 28b |
| 設計確認 | design_continue / design_stop | — | ✅ |
| 設計修改 | design_modify | ✅ 修改意見 | ❌ 28b |
| Dev_plan 升級 | devplan_skip / devplan_abort | — | ✅ |
| 合併通知 | （純通知，無需動作） | — | ✅ |
| 介入通知 | （純通知，無需動作） | — | ✅ |
| 任務取消選擇 | — | ✅ 選擇任務 | ❌ 28b |

---

## 架構設計

### 資料流

```
Bot 需要老闆確認
    │
    ├──① 寫入 BossInteraction（status=pending）
    ├──② 發送 Discord 訊息（現有行為不變）
    └──③ POST /internal/agent-status/interaction → SignalR → Dashboard
    
               ╱                           ╲
     Christ 在 Discord 點按鈕        Christ 在 Dashboard 點按鈕
              │                              │
              ▼                              ▼
     CommandHandler 處理               Dashboard API 寫入 DB
     ＋更新 BossInteraction           （ResponseAction + ResponseSource）
     （status=responded,              ＋通知 Bot
      source=discord）                      │
              │                              ▼
              │                    InteractionProcessor 輪詢
              │                    取得 Dashboard 回覆
              │                    分派到對應 Handler
              │                              │
              ├──────────────┬───────────────┤
              ▼              ▼               ▼
     SignalR 更新        Discord 同步      流程繼續
     Dashboard 狀態      「已在 Dashboard    （FireSteps 等）
                          回覆」
```

### 先到先贏機制

- BossInteraction.Status 初始為 `pending`
- 第一個回覆（不論來源）將 Status 設為 `responded`
- 後到的通道檢查 Status：
  - Discord：CommandHandler 檢查 → 已回覆則回覆「✅ 已在 Dashboard 處理」
  - Dashboard：UI 收到 SignalR → 按鈕即時 disable + 顯示回覆來源

---

## 28a-1. BossInteraction Entity 與基礎設施

### Entity 設計

```
BossInteraction
├── Id                  : Guid（PK）
├── TaskGroupId         : Guid?（FK → TaskGroup，可 null）
├── TaskItemId          : Guid?（FK → TaskItem，可 null）
│
├── InteractionType     : string
│   （ceo_confirm / exec_confirm / proposal / kickoff / design / devplan_escalate / merge_notify / intervention）
├── Status              : string（pending / responded / expired）
│
├── Title               : string（摘要標題，Dashboard 列表顯示用）
├── Description         : string（詳細說明，Dashboard 展開顯示用）
├── Project             : string?（專案名稱）
├── AgentName           : string?（相關 Agent）
│
├── AvailableActionsJson: string（JSON 陣列：可用的按鈕動作定義）
│   範例：[{"id":"confirm_yes","label":"確認派工","color":"success"},
│          {"id":"confirm_no","label":"取消","color":"error"}]
│
├── ResponseAction      : string?（回覆的動作 ID）
├── ResponseSource      : string?（discord / dashboard）
├── RespondedAt         : DateTime?
│
├── DiscordMessageId    : decimal?（ulong 存為 numeric(20)，用於雙向同步）
├── ContextJson         : string?（互動類型特有的上下文，JSON）
│   範例：{ "targetAgent": "Dev", "prUrl": "...", "taskPlanExcerpt": "..." }
│
├── CreatedAt           : DateTime
└── ExpiresAt           : DateTime?（可選，自動過期時間）
```

### 需要做的事

1. **新增 Entity**：`AiTeam.Data/Entities/BossInteraction.cs`
2. **新增 EF Configuration**：`AiTeam.Data/Configurations/BossInteractionConfiguration.cs`
   - Index on `(Status, CreatedAt)` — 查詢 pending 項目
   - Index on `DiscordMessageId` — Discord 回覆時反查
3. **新增 Repository**：`AiTeam.Data/Repositories/BossInteractionRepository.cs`
   - `GetPendingAsync()` — Dashboard 列表
   - `GetByDiscordMessageIdAsync(decimal)` — Discord 回覆時查詢
   - `GetDashboardResponsesAsync()` — Bot 輪詢未處理的 Dashboard 回覆
   - `RespondAsync(id, action, source)` — 標記已回覆（Status 樂觀鎖防重複）
4. **新增 Migration**
5. **新增 DTO**：`AiTeam.Shared/Dtos/BossInteractionDto.cs`
   - `BossInteractionDto`：列表 + 詳情顯示用
   - `InteractionActionDto`：按鈕定義（Id, Label, Color）
   - `InteractionResponseRequest`：Dashboard 回覆 API 的 Request Body

### AvailableActionsJson 各類型定義

| InteractionType | 動作定義 |
|----------------|---------|
| `ceo_confirm` | `[{id:"confirm_yes", label:"確認派工", color:"success"}, {id:"confirm_no", label:"取消", color:"error"}]` |
| `exec_confirm` | `[{id:"exec_yes", label:"執行", color:"success"}, {id:"exec_no", label:"取消", color:"error"}]` |
| `proposal` | `[{id:"propose_yes", label:"核准提案", color:"success"}, {id:"propose_no", label:"駁回", color:"error"}]` |
| `kickoff` | `[{id:"kickoff_continue", label:"繼續", color:"success"}, {id:"kickoff_stop", label:"停止", color:"error"}, {id:"kickoff_restart", label:"重開會議", color:"warning"}]` |
| `design` | `[{id:"design_continue", label:"繼續", color:"success"}, {id:"design_stop", label:"停止", color:"error"}]` |
| `devplan_escalate` | `[{id:"devplan_skip", label:"跳過審閱，直接開發", color:"warning"}, {id:"devplan_abort", label:"放棄任務", color:"error"}]` |
| `merge_notify` | `[]`（純通知，無按鈕） |
| `intervention` | `[]`（純通知，無按鈕） |

---

## 28a-2. Bot 寫入確認事件

### 需要做的事

在 CommandHandler 中，每個產生確認的地方**同步新增** BossInteraction 寫入。現有 Discord 流程完全不動，純 additive。

#### 需要改動的方法

| 方法 | 寫入時機 | InteractionType |
|------|---------|----------------|
| `HandleCeoChannelMessageAsync` | 建立 CEO 確認 Embed 後 | `ceo_confirm` |
| `ShowDirectAgentConfirmAsync` | 建立 Agent 執行確認 Embed 後 | `exec_confirm` |
| `ShowProposalAsync` | 建立提案確認 Embed 後 | `proposal` |
| `TaskGroupService` → Discord 回調 `RegisterKickoffConfirmation` | Kickoff 確認訊息發送後 | `kickoff` |
| `TaskGroupService` → Discord 回調 `RegisterDesignConfirmation` | Design 確認訊息發送後 | `design` |
| `NotifyBossDevPlanEscalationAsync` | Dev_plan 升級訊息發送後 | `devplan_escalate` |
| `NotifyBossMergeAsync` | 合併通知發送後 | `merge_notify` |
| `NotifyBossInterventionAsync` | 介入通知發送後 | `intervention` |

#### 建立 BossInteraction 的公用方法

在 `CommandHandler`（或獨立的 `InteractionService`）新增：

```
CreateInteractionAsync(type, title, description, project, agentName, actions[], taskGroupId?, taskItemId?, contextJson?, discordMessageId?)
```

每個確認點呼叫此方法，傳入對應參數。`DiscordMessageId` 在發送 Discord 訊息後取得。

#### Discord 回覆時同步更新 BossInteraction

在 CommandHandler 處理按鈕點擊時（`HandleButtonAsync` 等），同步更新對應的 BossInteraction：

```
// 透過 DiscordMessageId 查到 BossInteraction
var interaction = await repo.GetByDiscordMessageIdAsync(messageId);
if (interaction is { Status: "pending" })
{
    await repo.RespondAsync(interaction.Id, actionId, "discord");
    _ = pushService.PushInteractionUpdateAsync(); // 通知 Dashboard
}
// 若 Status 已是 responded（Dashboard 先回覆了），顯示提示並跳過
```

#### SignalR 推送

新增 `DashboardPushService.PushInteractionUpdateAsync()`：
- `POST /internal/agent-status/interaction`
- Dashboard 收到後重新載入待處理清單

新增 `AgentStatusHub` 常數：`ReceiveInteractionUpdate`

---

## 28a-3. Dashboard 待處理清單頁面

### 新增頁面：`/interactions`

**位置：** `Components/Pages/Interactions/InteractionCenter.razor`
**側邊欄：** 插入在「首頁」下方第二位，Icon：`Notifications`，文字：「操作中心」

### UI 設計

```
┌─────────────────────────────────────────────┐
│  操作中心                                    │
│                                             │
│  ┌─ 待處理（3）──────────────────────────┐  │
│  │                                       │  │
│  │  ┌────────────────────────────────┐   │  │
│  │  │ 🟡 CEO 決策確認                │   │  │
│  │  │ 「新增使用者設定頁面」          │   │  │
│  │  │ 專案：Dashboard  Agent：Dev    │   │  │
│  │  │ 3 分鐘前                       │   │  │
│  │  │                                │   │  │
│  │  │ Victoria 建議將此任務交給 Dev   │   │  │
│  │  │ Agent（Cody）執行...           │   │  │
│  │  │                                │   │  │
│  │  │  [ 確認派工 ]  [ 取消 ]        │   │  │
│  │  └────────────────────────────────┘   │  │
│  │                                       │  │
│  │  ┌────────────────────────────────┐   │  │
│  │  │ 🟠 Dev_plan 升級               │   │  │
│  │  │ 「重構 Token 追蹤模組」         │   │  │
│  │  │ 專案：AiTeam  Petra 審核 ×2    │   │  │
│  │  │ 15 分鐘前                      │   │  │
│  │  │                                │   │  │
│  │  │ Petra 阻擋問題：...            │   │  │
│  │  │                                │   │  │
│  │  │  [ 跳過審閱 ]  [ 放棄任務 ]    │   │  │
│  │  └────────────────────────────────┘   │  │
│  │                                       │  │
│  └───────────────────────────────────────┘  │
│                                             │
│  ┌─ 已處理 ─────────────────── 顯示更多 ─┐  │
│  │                                       │  │
│  │  ✅ 提案已核准 — 「Stage 28 ...」     │  │
│  │     Dashboard 回覆 · 1 小時前         │  │
│  │                                       │  │
│  │  ✅ 全流程完成 — 「修正登入...」       │  │
│  │     PR: #142 · 2 小時前              │  │
│  │                                       │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### 元件規劃

| 元件 | 說明 |
|------|------|
| `InteractionCenter.razor` | 頁面主體，分「待處理」和「已處理」兩區 |
| `InteractionCard.razor` | 單一互動卡片（MudCard），含標題 / 描述 / 按鈕 |

### 需要做的事

1. **DashboardTaskService 新增方法**：
   - `GetPendingInteractionsAsync()` — 查詢 Status = "pending"
   - `GetRecentInteractionsAsync(int count)` — 查詢最近已處理的互動
   - `RespondToInteractionAsync(Guid id, string action)` — Dashboard 回覆

2. **InteractionCenter.razor**：
   - 載入待處理 + 最近已處理清單
   - 訂閱 SignalR `ReceiveInteractionUpdate`，收到時重新載入
   - 「已處理」區預設顯示最近 10 筆，可展開更多

3. **InteractionCard.razor**：
   - 依 InteractionType 顯示不同的圖示和標題顏色
   - Description 區塊顯示詳細說明（Markdown 或純文字）
   - ContextJson 中的特殊欄位顯示（如 PR 連結、計畫書摘要）
   - 動態渲染 AvailableActions 為 MudButton
   - 點擊按鈕 → 確認對話框 → 呼叫 `RespondToInteractionAsync`

4. **InteractionType 顯示對應**：

   | InteractionType | 圖示 | 標題前綴 | 卡片顏色 |
   |----------------|------|---------|---------|
   | `ceo_confirm` | `Assignment` | CEO 決策確認 | Default |
   | `exec_confirm` | `PlayArrow` | Agent 執行確認 | Default |
   | `proposal` | `Description` | 提案確認 | Info |
   | `kickoff` | `Groups` | Kickoff 會議確認 | Info |
   | `design` | `DesignServices` | 設計會議確認 | Info |
   | `devplan_escalate` | `Warning` | Dev_plan 升級 | Warning |
   | `merge_notify` | `CheckCircle` | 全流程完成 | Success |
   | `intervention` | `Error` | 需要介入 | Error |

5. **API 端點**（`AgentStatusController` 新增）：
   - `POST /internal/agent-status/interaction` — 接收 Bot 推送，廣播 SignalR
   - `POST /api/interactions/{id}/respond` — Dashboard 回覆 API
     - Request Body: `{ "action": "confirm_yes" }`
     - 寫入 DB（ResponseAction, ResponseSource="dashboard", RespondedAt）
     - 推送 SignalR 更新
     - 呼叫 Bot callback（見 28a-4）

6. **NavMenu** 新增操作中心連結（插入在首頁下方）

---

## 28a-4. Bot 消費 Dashboard 回覆

### Dashboard → Bot 通知鏈路

Dashboard 回覆後需要通知 Bot 處理。**採用 DB 輪詢模式**（與 AgentQueueProcessor 一致）。

### InteractionProcessor（新 BackgroundService）

```
InteractionProcessor : BackgroundService
├── 3 秒輪詢 BossInteraction 表
│   查詢條件：Status = "responded" AND ResponseSource = "dashboard" AND ProcessedByBot = false
│
├── 對每筆 Dashboard 回覆：
│   1. 根據 InteractionType + ResponseAction 分派到對應 Handler
│   2. 標記 ProcessedByBot = true
│   3. 發送 Discord 同步訊息（在對應頻道）
│
└── Handler 分派邏輯：
    ├── ceo_confirm + confirm_yes  → 建立 TaskItem，建立 exec_confirm 互動
    ├── ceo_confirm + confirm_no   → 取消
    ├── exec_confirm + exec_yes    → 建立 TaskGroup + FireSteps（同現有邏輯）
    ├── exec_confirm + exec_no     → 取消
    ├── proposal + propose_yes     → ExecuteProposalApprovedAsync
    ├── proposal + propose_no      → 取消 + 清理
    ├── kickoff + kickoff_continue → HandleKickoffConfirmedAsync("continue")
    ├── kickoff + kickoff_stop     → HandleKickoffConfirmedAsync("stop")
    ├── kickoff + kickoff_restart  → HandleKickoffConfirmedAsync("restart")
    ├── design + design_continue   → HandleDesignConfirmedAsync("continue")
    ├── design + design_stop       → HandleDesignConfirmedAsync("stop")
    ├── devplan_escalate + devplan_skip  → HandleKickoffConfirmedAsync("skip")
    └── devplan_escalate + devplan_abort → HandleKickoffConfirmedAsync("abort")
```

### BossInteraction Entity 補充欄位

為了 InteractionProcessor 能正確分派，`ContextJson` 需包含足夠的 resume 資訊：

| InteractionType | ContextJson 必要欄位 |
|----------------|---------------------|
| `ceo_confirm` | `{ ceoResponseJson, project, description }` |
| `exec_confirm` | `{ ceoResponseJson, project, description, taskId }` |
| `proposal` | `{ taskId, project, description, images? }` |
| `kickoff` | `{ groupId }` |
| `design` | `{ groupId, petraSessionId }` |
| `devplan_escalate` | `{ groupId }` |
| `merge_notify` | `{ groupId, prUrl }` |
| `intervention` | `{ groupId, prUrl, fixIteration }` |

### Discord 同步訊息

InteractionProcessor 處理完 Dashboard 回覆後，在對應的 Discord 頻道發送：

```
📋 Christ 已在 Dashboard 回覆：{actionLabel}
```

讓 Discord 的對話脈絡保持完整。

---

## 需要修改的檔案清單

### 新增檔案

| 檔案 | 說明 |
|------|------|
| `AiTeam.Data/Entities/BossInteraction.cs` | Entity |
| `AiTeam.Data/Configurations/BossInteractionConfiguration.cs` | EF Config + Index |
| `AiTeam.Data/Repositories/BossInteractionRepository.cs` | Repository |
| `AiTeam.Data/Migrations/{timestamp}_AddBossInteraction.cs` | EF Migration |
| `AiTeam.Shared/Dtos/BossInteractionDto.cs` | DTO + InteractionActionDto + ResponseRequest |
| `AiTeam.Bot/Orchestration/InteractionProcessor.cs` | BackgroundService，輪詢 Dashboard 回覆 |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor` | 操作中心頁面 |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs` | 頁面 Code-behind |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCard.razor` | 互動卡片元件 |

### 修改檔案

| 檔案 | 變更 |
|------|------|
| `AiTeam.Data/AiTeamDbContext.cs` | 新增 `DbSet<BossInteraction>` |
| `AiTeam.Bot/Discord/CommandHandler.cs` | 8 個確認點新增 BossInteraction 寫入 + Discord 回覆時同步更新 |
| `AiTeam.Bot/Orchestration/TaskGroupService.cs` | NotifyBoss* 方法新增 BossInteraction 寫入 |
| `AiTeam.Bot/Services/DashboardPushService.cs` | 新增 `PushInteractionUpdateAsync()` |
| `AiTeam.Data/Hubs/AgentStatusHub.cs` | 新增 `ReceiveInteractionUpdate` 常數 |
| `AiTeam.Dashboard/Controllers/AgentStatusController.cs` | 新增 interaction 推送端點 + Dashboard 回覆 API |
| `AiTeam.Dashboard/Services/DashboardTaskService.cs` | 新增互動查詢 + 回覆方法 |
| `AiTeam.Dashboard/Components/Layout/NavMenu.razor` | 新增操作中心連結 |
| `AiTeam.Dashboard/wwwroot/css/app.css` | 互動卡片樣式（若需要） |
| `AiTeam.Bot/Program.cs` | 註冊 InteractionProcessor + BossInteractionRepository |
| `AiTeam.Dashboard/Program.cs` | 註冊 BossInteractionRepository（若 Dashboard 直接查 DB） |
| `Directory.Build.props` | v3.13.0 → v3.14.0 |

---

## 建議實作順序

```
28a-1（Entity + Migration + Repository + DTO）
  ↓
28a-2（Bot 寫入：CommandHandler + TaskGroupService 8 個確認點）
  ↓
28a-3（Dashboard：頁面 + SignalR + 按鈕回覆 API）
  ↓
28a-4（InteractionProcessor：輪詢 Dashboard 回覆 + Discord 同步）
```

---

## 設計決策與注意事項

### 為什麼用 DB 輪詢而不是 HTTP 回調？

Bot 是 Discord Bot Worker Service，沒有自己的 HTTP listener。新增 HTTP 端點需要額外基礎設施（Kestrel host）。DB 輪詢與現有 `AgentQueueProcessor`（3 秒輪詢）模式一致，簡單可靠，延遲可接受。

### 為什麼 DiscordMessageId 用 decimal 而不是 ulong？

PostgreSQL 沒有原生 unsigned 64-bit integer 類型。Discord Snowflake ID 最大值 ~18.4×10¹⁸，`numeric(20,0)` 可完整儲存。EF Core mapping 用 `HasConversion<decimal>()`。

### 先到先贏的樂觀鎖

`RespondAsync` 使用 DB 層級的條件更新：

```sql
UPDATE boss_interactions 
SET status = 'responded', response_action = @action, response_source = @source, responded_at = @now
WHERE id = @id AND status = 'pending'
```

若 affected rows = 0，代表另一通道已先回覆，回傳 false 讓呼叫端顯示「已在另一通道回覆」。

### 現有 Discord 流程的兼容性

28a 是**純 additive**——所有現有的 Discord 按鈕流程完全不變。BossInteraction 寫入是在原有流程之上額外新增的。即使 BossInteraction 寫入失敗，Discord 流程照常運作（降級為現狀）。

### ProcessedByBot 欄位

InteractionProcessor 查詢 `Status = "responded" AND ResponseSource = "dashboard" AND ProcessedByBot = false`。處理完後標記 `ProcessedByBot = true`，防止重複處理。Bot 重啟後不會重複消費。

### 28b 預留

- BossInteraction Entity 已有 `ContextJson` 欄位，28b 可存入更多上下文
- InteractionType 和 AvailableActionsJson 設計為彈性 JSON，28b 可新增「文字輸入」類型的動作
- 操作中心頁面預留「已處理」區塊，28b 擴充為完整歷史紀錄（含篩選）

---

## 驗收清單

### 28a-1 Entity + 基礎設施
- [ ] BossInteraction Entity + Migration + Repository
- [ ] DTO（BossInteractionDto / InteractionActionDto / InteractionResponseRequest）
- [ ] `dotnet build` 零 error

### 28a-2 Bot 寫入
- [ ] CEO 決策確認 → 寫入 BossInteraction
- [ ] Agent 執行確認 → 寫入 BossInteraction
- [ ] 提案確認 → 寫入 BossInteraction
- [ ] Kickoff 確認 → 寫入 BossInteraction
- [ ] Design 確認 → 寫入 BossInteraction
- [ ] Dev_plan 升級 → 寫入 BossInteraction
- [ ] 合併通知 → 寫入 BossInteraction
- [ ] 介入通知 → 寫入 BossInteraction
- [ ] Discord 按鈕回覆時同步更新 BossInteraction

### 28a-3 Dashboard 操作中心
- [ ] `/interactions` 頁面顯示待處理清單
- [ ] 各類型互動卡片正確顯示（圖示、標題、描述、按鈕）
- [ ] 點擊按鈕 → 確認對話框 → 回覆成功
- [ ] SignalR 即時更新（新互動進來 / 狀態變更）
- [ ] 已處理區塊顯示最近回覆紀錄
- [x] NavMenu 新增「操作中心」連結

### 28a-4 雙通道同步
- [x] Dashboard 回覆 → InteractionProcessor 消費 → 流程繼續
- [x] Dashboard 回覆 → Discord 同步訊息
- [x] Discord 回覆 → Dashboard 即時更新（按鈕 disable + 回覆來源）
- [x] 先到先贏：兩邊幾乎同時回覆不會衝突

### 整體
- [x] `dotnet build` 零 error
- [x] `dotnet test` 通過
- [x] v3.14.0 版本號更新

---

## 實作紀錄

### 關鍵設計決策

**1. InteractionRespondService（Scoped）取代 HttpClient round-trip**

原計劃 Dashboard 回覆走 `POST /api/interactions/{id}/respond`（HTTP），但 Blazor Server 注入 HttpClient 需要 IHttpClientFactory 才安全。改為建立 `InteractionRespondService`（Scoped），直接注入 `AppDbContext` + `IHubContext<AgentStatusHub>`，省去一層 HTTP 轉折，也消除了自己打自己的 loopback 問題。

**2. TaskGroupService.ProcessBossResponseAsync 統一分派**

InteractionProcessor 不直接呼叫 CommandHandler 邏輯，而是新增 `TaskGroupService.ProcessBossResponseAsync` 作為統一入口。
- kickoff / design：直接呼叫已有的 `HandleKickoffConfirmedAsync` / `HandleDesignConfirmedAsync`（不複製）
- devplan_escalate：新增 `HandleDevPlanEscalationAsync`（skip = FireSteps("Dev") / abort = UpdateGroupStatus("failed")）
- ceo_confirm / exec_confirm / proposal：新增對應的 `ProcessCeoConfirmAsync` / `ProcessExecConfirmAsync` / `ProcessProposalApprovedAsync`，從 ContextJson 還原所需資料

**3. ContextJson 必要欄位 channelId**

所有 8 種 InteractionType 建立時都將當下的 Discord channelId（ulong → string）存入 ContextJson。InteractionProcessor 讀取後直接取得頻道，無需反查 TaskGroup（`ceo_confirm` / `exec_confirm` 時 TaskGroup 可能尚不存在）。

**4. Singleton + Scoped 陷阱**

`InteractionService` 是 Singleton（Bot 全局），但 `BossInteractionRepository` 是 Scoped（EF Core DbContext 生命週期）。解法：建構子注入 `IServiceProvider`，每次操作呼叫 `CreateAsyncScope()` 建立短暫的 scope，避免 DbContext 跨請求使用。`InteractionProcessor` 同理。

### 踩坑記錄

**1. CI/CD：Bot Dockerfile apt NodeSource 安裝極慢**

症狀：GitHub Actions `Build and push Bot image` 步驟卡 22 分鐘以上，前兩次強制取消。

根本原因：Dockerfile 原本的順序是先 `COPY --from=build /app/publish`，再 `RUN apt-get install nodejs`。每次程式碼改動都使 `COPY` 層的 hash 改變，導致後續的 npm install 層無法命中 GHA cache，必須從頭重跑。再加上當天 GitHub runner 連接 Ubuntu/NodeSource 套件伺服器速度極慢，造成超時。

解法：改用 **node:22-slim multi-stage**，從官方 Node image 直接 `COPY` binary 進 runtime stage，完全繞過 `apt-get install nodejs`，不再依賴 Ubuntu mirror 速度：

```dockerfile
FROM node:22-slim AS claude-installer
RUN npm install -g @anthropic-ai/claude-code

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
COPY --from=node:22-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-slim /usr/local/lib      /usr/local/lib
COPY --from=claude-installer /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=claude-installer /usr/local/bin/claude       /usr/local/bin/claude
```

### 驗收結果（2026-04-17）

| 項目 | 結果 |
|------|------|
| `/interactions` 頁面空狀態顯示 | ✅ |
| Mock kickoff_stop → Dashboard 歷史出現記錄 | ✅ 來源 `dashboard`、回覆 `kickoff_stop` |
| Dashboard 回覆 → Discord 同步訊息 | ✅「📋 Christ 已在 Dashboard 回覆：停止 Kickoff ⏹️」 |
| Discord 按鈕先到先贏（Dashboard 已回覆）| ✅ ephemeral「✅ 已在 Dashboard 回覆，流程繼續中。」 |
| v3.14.0 版本號 | ✅ 頁腳顯示正確 |

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-16 | v1.0 | Aria 撰寫初版規劃書 |
| 2026-04-17 | v2.0 | 實作完成結案；補充實作紀錄、踩坑、驗收結果 |

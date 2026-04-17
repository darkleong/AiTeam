# Stage 28b：Dashboard 雙向操作中心 — 文字輸入互動與歷史紀錄

> 對應 Future Feature：九（Phase 2）
> 對應版本：v3.15.0
> 建立日期：2026-04-17
> 狀態：✅ 已完成（2026-04-17）
> 文件版本：v2.0

---

## 概述

Stage 28a 完成了 Dashboard 雙向操作中心的基礎架構：BossInteraction Entity、按鈕回覆、雙通道先到先贏。但 28a 只處理了**按鈕類互動**（確認/取消/繼續/停止/跳過）。

以下四種互動需要**文字輸入**，被明確延後至 28b：

| 互動 | 動作 | 輸入類型 |
|------|------|---------|
| 提案修改 | `propose_adjust` | 文字：調整意見 |
| Kickoff 修改 | `kickoff_modify` | 文字：修改意見 |
| 設計修改 | `design_modify` | 文字：修改指引 |
| 任務取消選擇 | — | 選擇：從多個執行中任務選一 |

Stage 28b 的目標：

1. **文字輸入互動** — Dashboard InteractionCard 支援文字輸入，完成提案/Kickoff/設計三種修改流程
2. **Discord 按鈕同步** — Discord 點擊修改按鈕時同步更新 BossInteraction 狀態
3. **歷史紀錄擴充** — 已處理區新增篩選與分頁
4. **Dashboard 任務取消** — 流程追蹤頁面直接取消任務（不走 BossInteraction）

---

## 現有文字輸入流程分析

### Discord 修改流程（三種共用模式）

```
Christ 點擊修改按鈕（Discord）
    │
    ├── Bot 設定 _pending*Modify[userId] = context
    ├── Bot 回覆 ephemeral：「請說明修改方向」
    │
    ▼
Christ 在頻道輸入文字
    │
    ├── CommandHandler.HandleCeoChannelMessageAsync 攔截
    ├── 從 _pending* 取出 context
    │
    ▼
觸發修改處理
    ├── propose_adjust  → 增補描述，重新呼叫 ShowProposalAsync → 新提案
    ├── kickoff_modify  → TaskGroupService.HandleKickoffConfirmedAsync("modify", text)
    └── design_modify   → TaskGroupService.HandleDesignConfirmedAsync("modify", ..., text)
    │
    ▼
處理完成後產出新的確認訊息（Discord + BossInteraction）
```

### 關鍵發現

1. **AvailableActionsJson 缺少修改動作**：28a 的 `ProposalActionsJson`、`KickoffActionsJson`、`DesignActionsJson` 沒有包含 `propose_adjust`、`kickoff_modify`、`design_modify`，因此 Dashboard 目前看不到這些按鈕

2. **Discord 修改按鈕未同步 BossInteraction**：點擊 `propose_adjust`/`kickoff_modify`/`design_modify` 時，CommandHandler 只設定了 `_pending*` 字典，沒有更新 BossInteraction 的 Status。Dashboard 會繼續顯示這些互動為 "pending"

3. **修改處理後產生新互動**：三種修改流程處理完都會產生**新的** BossInteraction（新提案 / 新 Kickoff 確認 / 新設計確認），形成互動鏈

4. **Discord 回傳管道缺失**：`HandleKickoffConfirmedAsync("modify")` 和 `HandleDesignConfirmedAsync("modify")` 使用 `_kickoffDiscordCallback` / `_designDiscordCallback` 發送 Discord 訊息。Dashboard 路徑（InteractionProcessor）呼叫時，callback 可能為 null

---

## 架構設計

### 文字回覆資料流

```
Dashboard InteractionCard 顯示含「需要調整 ✏️」按鈕的互動
    │
    ▼
Christ 點擊「需要調整 ✏️」
    │
    ├── 彈出 MudDialog：標題 + MudTextField 多行文字輸入
    │
    ▼
Christ 輸入修改意見，點擊「送出」
    │
    ├── InteractionRespondService.RespondAsync(id, action, content)
    │   ├── 更新 BossInteraction：Status=responded, ResponseAction, ResponseContent, ResponseSource=dashboard
    │   └── 廣播 SignalR
    │
    ▼
InteractionProcessor 輪詢（3 秒）
    │
    ├── 讀取 ResponseAction + ResponseContent
    ├── 分派到 ProcessBossResponseAsync
    │
    ├── propose_adjust  → TaskGroupService.ProcessProposalAdjustAsync(contextJson, content)
    │                     ├── 更新 TaskItem.Description（增補調整意見）
    │                     ├── 發送 Discord 新提案 Embed
    │                     └── 建立新 BossInteraction（新提案）
    │
    ├── kickoff_modify  → HandleKickoffConfirmedAsync(groupId, "modify", content)
    │                     ├── MeetingService.ModifyTaskPlanAsync
    │                     ├── 發送 Discord 新確認 Embed
    │                     └── 建立新 BossInteraction（新 Kickoff 確認）
    │
    └── design_modify   → HandleDesignConfirmedAsync(groupId, "modify", petraSessionId, content)
                          ├── MeetingService.ModifyDesignPlanAsync
                          ├── 發送 Discord 新確認 Embed
                          └── 建立新 BossInteraction（新設計確認）
```

### Discord 修改按鈕同步

```
Christ 點擊 Discord 修改按鈕
    │
    ├──① 更新 BossInteraction：Status=responded, ResponseAction=propose_adjust, ResponseSource=discord
    ├──② 推送 SignalR → Dashboard 即時 disable 按鈕
    ├──③ 設定 _pending*[userId]（現有行為不變）
    │
    ▼
後續文字輸入走現有 Discord 流程（不變）
```

### Discord 回傳機制（channelId fallback）

修改處理完成後需要發送 Discord 訊息。現有方法依賴 callback delegate（Discord 路徑設定）。Dashboard 路徑的 callback 為 null，需要 fallback：

```
處理修改結果 → 需要發送 Discord 訊息
    │
    ├── _discordCallback != null？（Discord 路徑）
    │   └── 使用 callback 發送 → ✅
    │
    └── _discordCallback == null？（Dashboard 路徑）
        ├── 從 ContextJson 取得 channelId
        ├── DiscordSocketClient.GetChannel(channelId) as ITextChannel
        └── channel.SendMessageAsync(...) → ✅
```

在 `HandleKickoffConfirmedAsync` 和 `HandleDesignConfirmedAsync` 中新增 `channelId` 可選參數。InteractionProcessor 呼叫時傳入 contextJson 中的 channelId。方法內部若 callback 為 null，改用 `_discordClient.GetChannel(channelId)` 發送。

---

## 28b-1. Entity 擴充與 Migration

### BossInteraction 新增欄位

| 欄位 | 類型 | 說明 |
|------|------|------|
| `ResponseContent` | `string?` | 文字回覆內容（修改意見） |

### InteractionActionDto 新增屬性

```
InteractionActionDto
├── Id          : string     （現有）
├── Label       : string     （現有）
├── Color       : string     （現有）
└── RequiresInput : bool     （新增：此動作是否需要文字輸入）
```

### InteractionResponseRequest 擴充

```
InteractionResponseRequest
├── Action   : string    （現有）
└── Content  : string?   （新增：文字輸入內容）
```

### 需要做的事

1. **BossInteraction.cs**：新增 `ResponseContent` 屬性
2. **BossInteractionConfiguration.cs**：`ResponseContent` 設為 `text` 型別（不限長度）
3. **新增 EF Migration**：`AddBossInteractionResponseContent`
4. **BossInteractionDto.cs**：新增 `ResponseContent`
5. **InteractionActionDto**：新增 `RequiresInput` 屬性
6. **InteractionResponseRequest**：新增 `Content` 屬性

---

## 28b-2. AvailableActionsJson 加入修改動作

### InteractionService 常數更新

目前的 AvailableActionsJson 缺少修改動作。更新如下：

| 類型 | 現有動作 | 新增動作 |
|------|---------|---------|
| `ProposalActionsJson` | propose_yes, propose_no | **propose_adjust**（requiresInput: true） |
| `KickoffActionsJson` | kickoff_continue, kickoff_stop, kickoff_restart | **kickoff_modify**（requiresInput: true） |
| `DesignActionsJson` | design_continue, design_stop | **design_modify**（requiresInput: true） |

### JSON 格式範例

```json
[
  {"id":"propose_yes","label":"核准提案","color":"success","requiresInput":false},
  {"id":"propose_adjust","label":"需要調整 ✏️","color":"info","requiresInput":true},
  {"id":"propose_no","label":"駁回","color":"error","requiresInput":false}
]
```

### 需要做的事

1. **InteractionService.cs**：更新三個 ActionsJson 常數
2. **確認 JSON 序列化**：`RequiresInput` 的 camelCase 命名一致（`requiresInput`）

---

## 28b-3. Dashboard 文字輸入 UI

### InteractionCard 文字輸入互動

當使用者點擊 `requiresInput: true` 的按鈕時，彈出 MudDialog 讓使用者輸入文字：

```
┌────────────────────────────────────┐
│ ✏️ 提案調整                        │ ← Dialog 標題（依互動類型動態）
│                                    │
│ ┌────────────────────────────────┐ │
│ │ 請輸入修改意見：                │ │ ← MudTextField Label
│ │                                │ │
│ │ UI 規格的表格欄位要加日期範圍   │ │ ← Lines="4" 多行輸入
│ │ 篩選，其他沒問題               │ │
│ │                                │ │
│ └────────────────────────────────┘ │
│                                    │
│            [ 取消 ]  [ 送出 ]      │
└────────────────────────────────────┘
```

### Dialog 標題對應

| 動作 | Dialog 標題 | 輸入提示 |
|------|------------|---------|
| `propose_adjust` | 提案調整 | 請輸入您希望如何調整提案方向 |
| `kickoff_modify` | Kickoff 計劃修改 | 請輸入您對任務計劃書的修改意見 |
| `design_modify` | 設計規劃修改 | 請輸入您對設計規劃書的修改指引 |

### InteractionRespondService 擴充

```csharp
// 現有簽名
public async Task<bool> RespondAsync(Guid id, string action, CancellationToken ct = default)

// 新增 overload
public async Task<bool> RespondAsync(Guid id, string action, string? content, CancellationToken ct = default)
```

新 overload 多設定 `ResponseContent` 欄位：

```sql
UPDATE boss_interactions 
SET status = 'responded', 
    response_action = @action, 
    response_content = @content,    -- 新增
    response_source = 'dashboard', 
    responded_at = @now
WHERE id = @id AND status = 'pending'
```

### AgentStatusController API 擴充

`POST /api/interactions/{id}/respond` 的 Request Body 已包含 `Action`，現在多接受 `Content`：

```json
{
  "action": "propose_adjust",
  "content": "UI 規格的表格欄位要加日期範圍篩選，其他沒問題"
}
```

### 需要做的事

1. **InteractionCard.razor**：
   - 判斷按鈕的 `RequiresInput`，若為 true 則點擊後開 MudDialog
   - 新增 `TextInputDialog.razor`（共用元件）或直接在 Card 內用 `IDialogService`
   - Dialog 內含 MudTextField（Lines="4"）+ 取消/送出按鈕
   - 送出時驗證非空白
   - 呼叫 `HandleResponseAsync(interaction, action, content)`

2. **InteractionCenter.razor.cs**：
   - `HandleResponseAsync` 新增 `string? content` 參數
   - 傳遞 content 給 RespondService

3. **InteractionRespondService.cs**：新增含 `content` 的 overload

4. **AgentStatusController.cs**：讀取 `request.Content` 並傳遞給 RespondService

---

## 28b-4. Bot 消費文字回覆

### InteractionProcessor 擴充

InteractionProcessor 輪詢時，從 BossInteraction 取得 `ResponseContent`，傳給 `ProcessBossResponseAsync`。

#### ProcessBossResponseAsync 新增簽名

```csharp
// 現有
public async Task ProcessBossResponseAsync(
    string interactionType, string action, string? contextJson, CancellationToken ct)

// 擴充
public async Task ProcessBossResponseAsync(
    string interactionType, string action, string? contextJson, 
    string? responseContent,    // 新增
    CancellationToken ct)
```

#### 新增分派分支

| InteractionType | Action | 處理方式 |
|----------------|--------|---------|
| `proposal` | `propose_adjust` | `ProcessProposalAdjustAsync(contextJson, responseContent, ct)` — **新方法** |
| `kickoff` | `kickoff_modify` | `HandleKickoffConfirmedAsync(groupId, "modify", responseContent, ct)` — 新增 channelId fallback |
| `design` | `design_modify` | `HandleDesignConfirmedAsync(groupId, "modify", petraSessionId, responseContent, ct)` — 新增 channelId fallback |

### ProcessProposalAdjustAsync（新方法）

提案修改的 Dashboard 路徑，位於 `TaskGroupService`：

```
ProcessProposalAdjustAsync(contextJson, adjustmentText, ct)
│
├── 1. 從 contextJson 取得 taskId, project, description, channelId
├── 2. 從 DB 讀取 TaskItem（by taskId）
├── 3. 增補描述：$"{description}\n\n【老闆調整意見】{adjustmentText}"
├── 4. 更新 TaskItem.Description
├── 5. 建立 Discord Embed（標題 + 增補描述）
├── 6. 透過 channelId 發送 Discord 訊息（含 propose_yes / propose_adjust / propose_no 按鈕）
├── 7. 建立新 BossInteraction（type=proposal）
├── 8. 在 CommandHandler 註冊 _pendingConfirmations（Discord 按鈕用）
└── 9. 推送 SignalR → Dashboard 顯示新提案
```

#### 步驟 6 的 Discord 按鈕

提案 Embed 上的 Discord 按鈕（propose_yes / propose_adjust / propose_no）需要在 Bot 端建立。但 `BuildProposalConfirmButtons()` 在 CommandHandler 中。

**解法**：將 `BuildProposalEmbed` 和 `BuildProposalConfirmButtons` 提取為 static 方法（或搬到 TaskGroupService），讓兩邊都能使用。

#### 步驟 8 的 _pendingConfirmations

Dashboard 路徑產出的新提案 Embed 也帶 Discord 按鈕，需要在 `CommandHandler._pendingConfirmations` 註冊。

**解法**：CommandHandler 暴露 `RegisterProposalConfirmation(messageId, pending)` 方法，TaskGroupService 呼叫。（與現有的 `RegisterKickoffConfirmation` / `RegisterDesignConfirmation` 模式一致）

### HandleKickoffConfirmedAsync 擴充

新增可選參數 `ulong? channelId`：

```csharp
public async Task HandleKickoffConfirmedAsync(
    Guid groupId,
    string decision,
    string? modifyContent = null,
    CancellationToken ct = default,
    ulong? channelId = null)      // 新增：Dashboard 路徑提供
```

修改處理完成後，發送 Discord 訊息的邏輯改為：

```csharp
// 原本：只用 callback
var msg = await _kickoffDiscordCallback!(embed, components);

// 改為：callback 優先，channelId fallback
IUserMessage msg;
if (_kickoffDiscordCallback is not null)
{
    msg = await _kickoffDiscordCallback(embed, components);
}
else if (channelId.HasValue)
{
    var channel = _discordClient.GetChannel(channelId.Value) as ITextChannel;
    msg = await channel!.SendMessageAsync(embed: embed, components: components);
}
else
{
    logger.LogWarning("HandleKickoffConfirmedAsync: 無法發送 Discord 訊息（callback=null, channelId=null）");
    return;
}
```

### HandleDesignConfirmedAsync 同理

新增 `ulong? channelId` 參數，使用相同的 callback/channelId fallback 模式。

### InteractionProcessor 呼叫方式

```csharp
// 現有：不帶 content
await taskGroupService.ProcessBossResponseAsync(type, action, contextJson, ct);

// 改為：帶 content
await taskGroupService.ProcessBossResponseAsync(type, action, contextJson, 
    interaction.ResponseContent, ct);
```

### 需要做的事

1. **TaskGroupService.cs**：
   - `ProcessBossResponseAsync` 新增 `responseContent` 參數
   - 新增 `ProcessProposalAdjustAsync` 方法
   - `HandleKickoffConfirmedAsync` 新增 `channelId` 參數 + fallback 邏輯
   - `HandleDesignConfirmedAsync` 新增 `channelId` 參數 + fallback 邏輯
   - 從 contextJson 解析 channelId（`ulong.Parse(channelIdStr)`）

2. **CommandHandler.cs**：
   - 新增 `RegisterProposalConfirmation(messageId, pending)` 方法
   - 提取 `BuildProposalEmbed` / `BuildProposalConfirmButtons` 為可重用方法

3. **InteractionProcessor.cs**：
   - 輪詢時讀取 `interaction.ResponseContent`
   - 傳遞給 `ProcessBossResponseAsync`

4. **BossInteractionRepository.cs**：
   - `GetDashboardResponsesAsync()` 的投影補上 `ResponseContent`

---

## 28b-5. Discord 修改按鈕同步 BossInteraction

### 問題

28a 中，Discord 的 `propose_adjust` / `kickoff_modify` / `design_modify` 按鈕被點擊時，CommandHandler 只設定了 `_pending*` 字典，**沒有**更新 BossInteraction 的 Status。導致：

- Dashboard 上該互動仍顯示為 "pending"，按鈕未 disable
- 存在雙通道同時回覆的風險

### 修正方式

在 CommandHandler 的三個修改按鈕處理區塊中，新增 BossInteraction 狀態更新：

```csharp
// propose_adjust 按鈕被點擊
_pendingAdjustments[interaction.User.Id] = pending;

// 新增：同步更新 BossInteraction
var bossInteraction = await interactionRepo.GetByDiscordMessageIdAsync((decimal)interaction.Message.Id);
if (bossInteraction is { Status: "pending" })
{
    await interactionRepo.RespondAsync(bossInteraction.Id, "propose_adjust", "discord");
    _ = pushService.PushInteractionUpdateAsync();
}
```

同樣的模式套用到 `kickoff_modify` 和 `design_modify`。

### 需要做的事

1. **CommandHandler.cs**：
   - `propose_adjust` 按鈕處理區塊新增 BossInteraction 更新
   - `kickoff_modify` 按鈕處理區塊新增 BossInteraction 更新
   - `design_modify` 按鈕處理區塊新增 BossInteraction 更新

---

## 28b-6. 歷史紀錄擴充

### 現狀

InteractionCenter 的「已處理」區預設顯示最近 10 筆，無篩選功能。

### 擴充

| 功能 | 說明 |
|------|------|
| 分頁 | MudTable 分頁顯示（每頁 15 筆） |
| 類型篩選 | MudSelect 下拉，選擇 InteractionType |
| 日期範圍 | MudDateRangePicker |
| 回覆來源篩選 | MudChipSet：全部 / Discord / Dashboard |

### UI 設計

```
┌─ 已處理 ──────────────────────────────────────────────┐
│                                                       │
│  類型：[全部 ▾]  來源：[全部] [Discord] [Dashboard]   │
│  日期：[2026-04-10] ~ [2026-04-17]                    │
│                                                       │
│  ┌────────────────────────────────────────────────┐   │
│  │ ✅ 提案已核准 — 「新增使用者設定頁面」          │   │
│  │    Dashboard 回覆 · 2 小時前                   │   │
│  │    調整意見：表格欄位要加日期範圍篩選           │   │ ← 有 ResponseContent 時顯示
│  └────────────────────────────────────────────────┘   │
│                                                       │
│  ┌────────────────────────────────────────────────┐   │
│  │ ✅ Kickoff 已確認 — 「Stage 28b」              │   │
│  │    Discord 回覆 · 5 小時前                     │   │
│  └────────────────────────────────────────────────┘   │
│                                                       │
│              < 1  2  3  4  5 >                        │
└───────────────────────────────────────────────────────┘
```

### DashboardTaskService 擴充

新增查詢方法：

```csharp
GetInteractionHistoryAsync(
    int page, 
    int pageSize,
    string? typeFilter,
    string? sourceFilter,
    DateTime? from,
    DateTime? to)
```

回傳 `(List<BossInteractionDto> Items, int TotalCount)`。

### 需要做的事

1. **DashboardTaskService.cs**：新增 `GetInteractionHistoryAsync` 分頁查詢
2. **InteractionCenter.razor**：已處理區改為分頁表格 + 篩選列
3. **InteractionCard.razor**：已處理的卡片顯示 `ResponseContent`（若有）

---

## 28b-7. Dashboard 任務取消

### 背景

Discord 的 `/cancel` 指令需要使用者從多個執行中任務中選擇一個。這種「boss 主動發起」的操作，在 Dashboard 上以**直接操作按鈕**實現比走 BossInteraction 更直覺。

### 實作方式

在流程追蹤頁面（`/pipelines`）的每個執行中 TaskGroup 行上，新增「取消」按鈕：

```
┌──────────────────────────────────────────────────────────┐
│ 流程追蹤                                                  │
│                                                          │
│  狀態  標題                Agent    專案       操作       │
│  🟢   新增使用者設定頁面   Dev     Dashboard  [ ⛔ 取消 ] │
│  🟢   修正登入 Bug        Reviewer AiTeam     [ ⛔ 取消 ] │
│  ✅   Stage 28a           —        AiTeam     —          │
└──────────────────────────────────────────────────────────┘
```

### 取消流程

```
Christ 點擊「取消」按鈕
    │
    ├── MudDialog 確認：「確定要取消『{任務標題}』嗎？」
    │
    ▼
確認 → Dashboard API 呼叫
    │
    ├── POST /api/taskgroups/{id}/cancel
    ├── Bot internal API：POST /internal/agent-status/cancel-group
    │   ├── AgentQueueProcessor 取消該 TaskGroup 相關的排隊中任務
    │   ├── TaskGroup.Status = "cancelled"
    │   └── 中止正在執行的 Claude Code session（若有）
    ├── SignalR 推送 Dashboard 更新
    └── Discord 發送取消通知
```

### 需要做的事

1. **PipelineList.razor**：執行中的 TaskGroup 行顯示「取消」MudIconButton
2. **確認 Dialog**：MudDialog 確認取消
3. **AgentStatusController.cs**：新增 `POST /internal/agent-status/cancel-group` 端點
4. **Bot 端處理**：
   - 新增 `POST /internal/agent-status/cancel-group` 接收端點（DashboardPushService 的反向路徑）
   - 或由 Dashboard 直接更新 DB + 通知 Bot

> **注意**：此功能的完整實作取決於 AgentQueueProcessor 的取消機制是否完整。若現有取消機制不足以安全中止執行中任務，可降級為「僅取消排隊中任務」，執行中的任務需等完成或手動處理。

---

## 需要修改的檔案清單

### 新增檔案

| 檔案 | 說明 |
|------|------|
| `AiTeam.Data/Migrations/{timestamp}_AddBossInteractionResponseContent.cs` | EF Migration |
| `AiTeam.Dashboard/Components/Pages/Interactions/TextInputDialog.razor` | 文字輸入 MudDialog |

### 修改檔案

| 檔案 | 變更 |
|------|------|
| **Entity + DTO** | |
| `AiTeam.Data/Entities/BossInteraction.cs` | 新增 `ResponseContent` 屬性 |
| `AiTeam.Data/Configurations/BossInteractionConfiguration.cs` | `ResponseContent` 設為 text 型別 |
| `AiTeam.Shared/Dtos/BossInteractionDto.cs` | `ResponseContent` + `InteractionActionDto.RequiresInput` + `InteractionResponseRequest.Content` |
| **Bot 端** | |
| `AiTeam.Bot/Services/InteractionService.cs` | 三個 ActionsJson 常數加入 modify 動作 |
| `AiTeam.Bot/Discord/CommandHandler.cs` | 三個修改按鈕同步更新 BossInteraction + 暴露 `RegisterProposalConfirmation` + 提取 BuildProposal* 為可重用 |
| `AiTeam.Bot/Orchestration/TaskGroupService.cs` | `ProcessBossResponseAsync` 擴充 + `ProcessProposalAdjustAsync` + `HandleKickoff/DesignConfirmedAsync` 新增 channelId fallback |
| `AiTeam.Bot/Orchestration/InteractionProcessor.cs` | 讀取 ResponseContent 並傳遞 |
| `AiTeam.Data/Repositories/BossInteractionRepository.cs` | `GetDashboardResponsesAsync` 投影補 ResponseContent + `RespondAsync` 含 content overload |
| **Dashboard 端** | |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCard.razor` | requiresInput 按鈕 → 開 Dialog + 已處理卡片顯示 ResponseContent |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor` | 已處理區改分頁 + 篩選 |
| `AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs` | HandleResponseAsync 新增 content 參數 + 歷史查詢 |
| `AiTeam.Dashboard/Services/InteractionRespondService.cs` | 新增含 content 的 RespondAsync overload |
| `AiTeam.Dashboard/Services/DashboardTaskService.cs` | 新增 `GetInteractionHistoryAsync` |
| `AiTeam.Dashboard/Controllers/AgentStatusController.cs` | 讀取 request.Content 傳遞 |
| `AiTeam.Dashboard/Components/Pages/Pipelines/PipelineList.razor` | 執行中 TaskGroup 顯示「取消」按鈕 |
| **版本號** | |
| `Directory.Build.props` | v3.14.0 → v3.15.0 |

---

## 建議實作順序

```
28b-1（Entity 擴充 + Migration + DTO）
  ↓
28b-2（AvailableActionsJson 加入修改動作）
  ↓
28b-5（Discord 修改按鈕同步 BossInteraction — 修正 28a 遺漏）
  ↓
28b-3（Dashboard 文字輸入 UI — TextInputDialog + InteractionCard + RespondService）
  ↓
28b-4（Bot 消費文字回覆 — ProcessBossResponseAsync 擴充 + channelId fallback）
  ↓
28b-6（歷史紀錄擴充 — 篩選 + 分頁）
  ↓
28b-7（Dashboard 任務取消 — 可獨立，依時間評估是否納入）
```

28b-5 建議排在 28b-3 之前，因為它修正了 28a 的遺漏（Discord 修改按鈕未同步 BossInteraction），是正確性修正。

---

## 設計決策與注意事項

### 為什麼用 MudDialog 而不是 inline 展開？

InteractionCard 已有固定的卡片布局（標題 + 描述 + 按鈕）。在卡片內 inline 展開文字區會打破布局一致性，且多張卡片同時展開時會造成頁面跳動。MudDialog 讓使用者專注在一個修改操作上，關閉後卡片恢復原狀。

### 提案修改的「新提案」如何與 Discord 同步？

Dashboard 路徑的 `ProcessProposalAdjustAsync` 產出新的 Discord Embed + 新的 BossInteraction。Discord 使用者會在頻道看到新提案（與 Discord 路徑的行為一致）。新的 BossInteraction 透過 SignalR 推送到 Dashboard，形成互動鏈。

### channelId fallback 的安全性

`DiscordSocketClient.GetChannel(channelId)` 可能回傳 null（頻道已刪除、Bot 無權限等）。需要 null check + 錯誤 log。流程不應因 Discord 發送失敗而中斷——BossInteraction 已建立，Dashboard 仍可操作。

### 任務取消的「安全」程度

取消排隊中的任務（QueueStatus = "queued"）是安全的——只需更新 DB。取消執行中的任務（Claude Code CLI 正在跑）需要中止外部 process，已有 `CancellationToken` 機制但需驗證完整性。建議 28b 先實作「取消排隊中 + 標記執行中為 cancelling」，後續 Stage 再完善強制中止。

### ResponseContent 的長度考量

修改意見通常是一兩句話（< 500 字）。但為保險起見，Entity 使用 `text` 型別（PostgreSQL 不限長度），Dashboard 的 MudTextField 設 `MaxLength="2000"` 做前端限制。

### 28b-5 是否算 28a 的 bug？

嚴格來說，28a 的 scope 不包含修改按鈕（明確 deferred）。但 Discord 修改按鈕的存在（propose_adjust / kickoff_modify / design_modify 是 Discord 原有功能）加上 28a 新增的 BossInteraction 寫入，導致修改按鈕點擊後 BossInteraction 狀態不一致。這是 28a 的邊界效應，在 28b 中修正是合理的。

---

## 驗收清單

### 28b-1 Entity 擴充
- [ ] BossInteraction 新增 ResponseContent 欄位 + Migration
- [ ] DTO 更新（RequiresInput、Content）
- [ ] `dotnet build` 零 error

### 28b-2 AvailableActionsJson
- [ ] 新建立的提案互動包含 propose_adjust 按鈕
- [ ] 新建立的 Kickoff 互動包含 kickoff_modify 按鈕
- [ ] 新建立的設計互動包含 design_modify 按鈕
- [ ] Dashboard InteractionCard 正確渲染新按鈕

### 28b-3 Dashboard 文字輸入
- [ ] 點擊 requiresInput 按鈕 → MudDialog 彈出
- [ ] 輸入文字 + 送出 → BossInteraction 更新為 responded（含 ResponseContent）
- [ ] 空白文字無法送出（前端驗證）
- [ ] 送出後卡片即時更新為已處理狀態

### 28b-4 Bot 消費文字回覆
- [ ] Dashboard 提案調整 → InteractionProcessor → 新提案出現在 Discord + Dashboard
- [ ] Dashboard Kickoff 修改 → InteractionProcessor → Petra 處理 → 新確認出現
- [ ] Dashboard 設計修改 → InteractionProcessor → Petra 處理 → 新確認出現
- [ ] Discord 同步訊息包含「📋 Christ 已在 Dashboard 回覆：需要調整 ✏️」

### 28b-5 Discord 修改按鈕同步
- [ ] Discord 點擊 propose_adjust → BossInteraction 標記 responded + Dashboard 按鈕 disable
- [ ] Discord 點擊 kickoff_modify → BossInteraction 標記 responded + Dashboard 按鈕 disable
- [ ] Discord 點擊 design_modify → BossInteraction 標記 responded + Dashboard 按鈕 disable

### 28b-6 歷史紀錄
- [ ] 已處理區顯示分頁
- [ ] 類型篩選功能正常
- [ ] 來源篩選功能正常
- [ ] 日期範圍篩選功能正常
- [ ] 有 ResponseContent 的紀錄顯示調整意見

### 28b-7 任務取消（可選）
- [ ] 流程追蹤頁面顯示取消按鈕（僅執行中 TaskGroup）
- [ ] 確認 Dialog → 取消成功 → 狀態更新
- [ ] Discord 同步取消通知

### 整體
- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] v3.15.0 版本號更新

---

## 實作紀錄（2026-04-17）

### 實作完成項目

| 項目 | 狀態 | 說明 |
|------|------|------|
| 28b-1 Entity + Migration + DTO | ✅ | `BossInteraction.ResponseContent`、`InteractionActionDto.RequiresInput`、`InteractionResponseRequest.Content` |
| 28b-2 ActionsJson 更新 | ✅ | 三個 ActionsJson 加入修改動作（含 `requiresInput: true`）；新增 ProposeYes/ProposeAdjust/ProposeNo 常數 |
| 28b-3 Dashboard 文字輸入 UI | ✅ | `TextInputDialog.razor`、InteractionCard RequiresInput 分支、InteractionRespondService content overload |
| 28b-4 Bot 消費文字回覆 | ✅ | `ProcessBossResponseAsync` 含 responseContent；`ProcessProposalAdjustAsync` 新方法；InteractionProcessor 傳遞 ResponseContent |
| 28b-5 Discord 修改按鈕同步 | ✅ | 三個修改按鈕點擊後 `SyncDiscordResponseAsync`；`RegisterProposalConfirmation` 方法 |
| 28b-6 歷史紀錄擴充 | ✅ | `GetInteractionHistoryAsync` 含類型/來源/日期範圍篩選；InteractionCenter 篩選列 + MudTable 分頁 |
| 28b-7 任務取消 | ⏭️ | 延後至下一 Stage |

### 關鍵設計決策

1. **循環依賴**：沿用 `serviceProvider.GetRequiredService<CommandHandler>()` 模式（與 Kickoff/Design 相同），不新增 interface
2. **channelId fallback**：實際探索後發現 `HandleKickoffConfirmedAsync` / `HandleDesignConfirmedAsync` 使用 `FindChannel` 直接查找頻道，不需要 channelId 參數——比計劃書更簡單
3. **CeoResponse 最小化**：`PendingConfirmation` 的 CeoResponse 只有 propose_yes 路徑使用 TaskId/Project，實際上 CeoResponse 完全未被讀取，傳入 `new CeoResponse()` 即可
4. **歷史紀錄分頁**：前端用 `pageSize: 200` 一次撈，讓 MudTable 做客戶端分頁（互動量小，不需 server-side pagination）

### 踩坑記錄

1. **EF Migration 多 DbContext**：需加 `--context AppDbContext`，否則報 "More than one DbContext was found"
2. **Solution 檔案**：專案用 `AiTeam.slnx` 不是 `AiTeam.sln`
3. **MudChipSet SelectedValueChanged**：回傳 `string?` 而非 `string`，handler 簽名要用 `string?`

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-17 | v1.0 | Aria 撰寫初版規劃書 |
| 2026-04-17 | v2.0 | Stage 28b 實作完成，補充實作紀錄 |

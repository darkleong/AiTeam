# Stage 29：Dashboard 操作性收尾 + CEO 指令通道擴充

> 對應 Future Feature：零、零-A、十（部分）
> 對應版本：v3.16.0
> 建立日期：2026-04-18
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

Stage 28a/28b 完成了 Dashboard 雙向操作中心（8 個確認點 + 文字輸入互動 + 歷史紀錄篩選）。此階段之後，Christ 仍有少量「只能透過 Discord」或「必須繞路」的操作摩擦。

Stage 29 清掉這些小型但高頻的摩擦點，為之後「AiTeam 自己修自己」（dogfooding）做準備——讓 Dashboard 成為完整可操作的指令入口，不再需要在 Dashboard 和 Discord 之間切換才能完成一次完整流程。

### 本階段五大項目

| 項目 | 類型 | 規模 | 對應 Future Feature |
|------|------|------|---------------------|
| 29-1 | Dashboard 歸檔報告折疊面板 | S | 零 |
| 29-2 | TaskLog 顯示統一化 | S | 零-A |
| 29-3 | Dashboard Cache Reload 按鈕 | S | 十（部分） |
| 29-4 | 系統設定獨立頁 | M | 十（部分） |
| 29-5 | Dashboard 下達指令給 Victoria（含圖片附件） | M | 新增 |

---

## 29-1. Dashboard 歸檔報告折疊面板

### 背景

Pipeline View 的流程文件折疊面板（Stage 26 追加）目前顯示六份文件：提案書、任務計劃書、設計規劃書、實作計劃書、驗收報告、測試報告。

**歸檔報告**（Sage 產出的 `docs/archive/pr{N}-archive.md`）目前只寫入 Git 檔案，沒有存回 DB，Dashboard 因此看不到。

### 實作方式

1. **`TaskGroup` Entity 新增 `ArchiveContent string?` 欄位**
2. **EF Core Migration**：`AddTaskGroupArchiveContent`
3. **`DocAgentService` / `SageAgentService`**：產出歸檔報告後，除了寫入 Git 檔案，也將全文存入 `TaskGroup.ArchiveContent`
4. **`TaskGroupDto` 新增 `ArchiveContent` 欄位**
5. **`DashboardTaskService` 三個 LINQ 投影補上此欄位**（`GetAllAsync` / `GetByIdAsync` / `GetInProgressAsync` 等所有用到的地方）
6. **`PipelineView.razor` 新增 `📦 歸檔報告（Sage）` 折疊面板**（沿用現有 MudExpansionPanel 樣式）

### 需要做的事

| 檔案 | 變更 |
|------|------|
| `AiTeam.Data/Entities/TaskGroup.cs` | 新增 `ArchiveContent` 屬性 |
| `AiTeam.Data/Configurations/TaskGroupConfiguration.cs` | `ArchiveContent` 設為 text |
| `AiTeam.Data/Migrations/{timestamp}_AddTaskGroupArchiveContent.cs` | 新增 Migration |
| `AiTeam.Shared/Dtos/TaskGroupDto.cs` | 新增 `ArchiveContent` 欄位 |
| Sage/Doc Agent Service（視 Stage 23 架構） | 完成歸檔後寫入 `TaskGroup.ArchiveContent` |
| `AiTeam.Dashboard/Services/DashboardTaskService.cs` | LINQ 投影補 ArchiveContent |
| `AiTeam.Dashboard/Components/Pages/Pipelines/PipelineView.razor` | 新增歸檔報告折疊面板 |

---

## 29-2. TaskLog 顯示統一化

### 背景

在「任務列表」頁面點擊任務後，右側會展開顯示 TaskLog 記錄。目前各 Agent 的 Log 寫入風格不一致：

- **QA / Reviewer**：寫兩筆 Log（`running` → `done`），完成後點開仍看到第一筆「執行中」
- **Doc**：只寫一筆 `done` Log，第一筆即「完成」
- **PM（Petra） / Design**：未寫任何 TaskLog，顯示「尚無 Log 記錄」

對 Christ 而言，「點進任務看不到 Log」會誤以為系統沒在工作，降低 Dashboard 的可觀察性。

### 統一規格

所有 Agent 至少產出**兩筆 Log**：

| Log 時機 | Status | Message 範例 |
|----------|--------|-------------|
| Agent 開始執行 | `running` | 「Petra 開始審核任務計劃書」 |
| Agent 完成 / 失敗 | `done` / `error` | 「Petra 審核通過」/「Petra 要求重做」 |

多步驟 Agent（如 QA 多輪 fix、Petra 多審核點）可寫中間 Log，但至少頭尾兩筆必寫。

### 待確認項目

1. **逐一核對**每種 Agent 任務（Kickoff / Design / Dev / Reviewer / PM / QA / Doc）目前實際寫入的 Log
2. **補寫缺漏**：PM（Petra）各審核點、Design 兩段（Kickoff/Design）、其他發現缺漏者
3. **消除 QA/Reviewer 的過時 running Log 殘影**（完成後應顯示 done，不應留著 running）

### 需要做的事

| 檔案 | 變更 |
|------|------|
| `AiTeam.Bot/Agents/PetraAgentService.cs` | 各審核點進入/結束寫 TaskLog |
| `AiTeam.Bot/Meetings/MeetingService.cs` 或對應檔 | Kickoff/Design 兩段各寫 Log |
| `AiTeam.Bot/Agents/QaAgentService.cs` | 完成時 `running` Log 要被 `done` 取代（or 更新 status） |
| `AiTeam.Bot/Agents/ReviewerAgentService.cs` | 同上 |

> **提示**：這個項目是「清點 + 小修」，實作前先花 30 分鐘掃過所有 Agent，列出「實際寫了什麼 Log」清單，確認範圍後再動工。

---

## 29-3. Dashboard Cache Reload 按鈕

### 背景

目前在 Dashboard 修改規則或 Agent 設定後，需要到 Discord 執行 `/reload-rules` 才能讓 Bot 端 Cache 更新生效。操作流程不合理：
- 使用者在 A 介面修改，得到 B 介面生效
- 新手（若之後有其他使用者）完全看不出這條隱性依賴

### 需求

Dashboard 自帶 reload 按鈕：

- **規則管理頁面**：頂部新增「套用變更」按鈕
- **Agent 設定頁面**：頂部新增「套用變更」按鈕
- 按鈕呼叫 Bot internal API：`POST /internal/reload-cache?scope={rules|agents|all}`
- Bot 收到後清空對應 Cache，重新從 DB 載入
- 顯示 Snackbar 提示：「已套用（N 筆規則 / N 個 Agent）」

### 需要做的事

| 檔案 | 變更 |
|------|------|
| `AiTeam.Bot/Controllers/InternalReloadController.cs`（新增）或擴充 `AgentStatusController` | 新增 `POST /internal/reload-cache` 端點 |
| `AiTeam.Bot/Services/RulesService.cs` / `AgentRepository` / `IAppSettingsService` | 暴露 `ClearCacheAsync()` |
| `AiTeam.Dashboard/Services/DashboardReloadService.cs`（新增） | HTTP client 呼叫 Bot internal API |
| `AiTeam.Dashboard/Components/Pages/Rules/RulesManagement.razor` | 頂部「套用變更」按鈕 |
| `AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor` | 頂部「套用變更」按鈕 |

### 設計決策

- **為什麼 Discord `/reload-rules` 保留？** Discord 端操作者（未來可能不只 Christ）仍可能需要強制刷新，無害保留
- **為什麼不做「自動 reload」？** 修改中途的半成品狀態（開始改規則 A → 還沒改完 → 已觸發 reload）會讓 Agent 讀到不穩定的配置，明確的「套用」按鈕讓使用者控制 commit point

---

## 29-4. 系統設定獨立頁

### 背景

目前「Agent 設定」頁面下方塞有「系統設定」區塊（跳過 CEO 派工確認、Mock Mode）。隨著系統設定項目增加（Token 守門閾值、佇列自動恢復、指令預設頻道等），繼續共用同一頁面會越來越混亂。

### 需求

- 新增路由 `/system-settings`，對應新 Blazor 頁面 `SystemSettings.razor`
- 側邊欄 NavMenu 加入「系統設定」連結（icon 用 `Settings`）
- Agent 設定頁移除底部「系統設定」區塊，功能邏輯不變

### 第一批納入的系統設定項目

| 設定 | 說明 | 來源 |
|------|------|------|
| 跳過 CEO 派工確認 | 現有 | Agent 設定頁移來 |
| Mock Mode | 現有 | Agent 設定頁移來 |
| **CEO 指令預設頻道** | Dashboard 下達指令時 fallback 的 Discord 頻道 ID | 29-5 新增 |

（Token 守門、佇列自動恢復等後續 Stage 再加）

### 需要做的事

| 檔案 | 變更 |
|------|------|
| `AiTeam.Dashboard/Components/Pages/System/SystemSettings.razor`（新增） | 系統設定頁面本體 |
| `AiTeam.Dashboard/Components/Pages/System/SystemSettings.razor.cs`（新增） | 頁面 code-behind |
| `AiTeam.Dashboard/Components/Layout/NavMenu.razor` | 加入「系統設定」連結 |
| `AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor` | 移除底部系統設定區塊 |

### 設計決策

- **用現有 `AppSettingsService`**：Stage 27b 已建好 DB-backed + TTL cache 的 AppSettings 機制，直接沿用
- **權限**：Dashboard 目前走 localhost bypass，不需新增身分驗證層

---

## 29-5. Dashboard 下達指令給 Victoria（含圖片附件）

### 背景

目前**唯一**能下指令給 Victoria 的入口是 Discord `#victoria-ceo` 頻道。這導致：
- Dashboard 瀏覽中遇到問題，要切到 Discord 才能問
- 未來 dogfooding（「AiTeam 修 AiTeam 自己」）時，若 Dashboard 不能下指令，使用動線會卡住
- Christ 在手機/iPad 操作 Dashboard 時，同時切 Discord 手感差

**決策已確認（2026-04-18）：** Dashboard 新增指令入口，**支援文字 + 圖片**（與 Discord 對齊）。

### UX 設計

**位置**：首頁（`/`）新增「快速下達指令」卡片

```
┌─ 快速下達指令 ─────────────────────────────────┐
│                                                │
│ ┌────────────────────────────────────────────┐ │
│ │ 我想知道目前任務進度，並規劃下個 Stage...   │ │  ← MudTextField
│ │                                            │ │     Lines="4"
│ │                                            │ │
│ └────────────────────────────────────────────┘ │
│                                                │
│ 📎 附加圖片（選填，最多 5 張，每張 ≤ 5MB）       │
│ ┌──┐ ┌──┐ ┌──┐                                 │
│ │🖼│ │🖼│ │ + │   ← MudFileUpload 縮圖 + 刪除 X │
│ └──┘ └──┘ └──┘                                 │
│                                                │
│                        [ 取消 ]  [ 送出 🚀 ]   │
│                                                │
└────────────────────────────────────────────────┘
```

送出後：
1. 卡片顯示「已送達 Victoria，請至操作中心查看回應」
2. 3 秒後自動導向 `/interactions`（操作中心），Christ 能看到 Victoria 的回應以 BossInteraction 形式出現
3. 同時 Discord CEO 頻道會收到 Embed（文字 + 圖片），保持雙通道資訊對等

### 架構設計

```
Dashboard 首頁「快速下達指令」卡片
    │  [multipart/form-data]
    │  - text: string
    │  - images[]: IFormFile × N
    │
    ▼
Dashboard API：POST /api/ceo/command
    │  ├── 驗證 text 非空
    │  ├── 驗證 images 每張 ≤ 5MB、image/* only、最多 5 張
    │  ├── 將檔案讀入 byte[] + MediaType
    │  │
    │  ▼
    │  DashboardCeoCommandService.SendAsync(text, images)
    │      │
    │      │  [HTTP 呼叫 Bot internal API]
    │      │
    │      ▼
Bot：POST /internal/ceo/command（新端點）
    │  Body：{ text, images: [{ base64, mediaType }], sourceUserId? }
    │
    ├── 1. 讀取「CEO 指令預設頻道」（AppSettings，29-4）
    ├── 2. 將 images 轉為 List<ImageAttachment>（現成 record）
    ├── 3. 將指令文字 + 圖片縮圖 Embed 發到 CEO 頻道（保持雙通道對等）
    ├── 4. 將圖片 Base64 存入 BossCommandLog.Images（新 Entity）
    ├── 5. 呼叫 CeoAgentService.ProcessWithClaudeCodeAsync
    │      - triggerSource: "dashboard"
    │      - userId: AppSettings 的 Christ Discord ID
    │      - images: List<ImageAttachment>
    │
    ▼
Victoria 回應（CeoResponse）
    │
    ├── reply      → 發 Discord 訊息 + 建 BossInteraction（type=reply，純顯示用）
    ├── propose    → 走現有 ShowProposalAsync（產生提案 BossInteraction）
    └── delegate   → 走現有任務派發流程
    │
    ▼
Dashboard 操作中心（/interactions）
    │  SignalR 推送 InteractionUpdate
    │  Christ 看到 Victoria 的回應
```

### 關鍵設計決策

#### 1. 圖片存 DB（Christ 決策）

**新 Entity：`BossCommandLog`**（可追溯 Dashboard 下達過的指令）

| 欄位 | 類型 | 說明 |
|------|------|------|
| Id | Guid | PK |
| Text | string | 指令文字 |
| Images | jsonb | `[{base64, mediaType}]` 陣列 |
| Source | string | `"dashboard"` |
| CeoResponseRaw | string? | Victoria 回應原文（供追溯） |
| CreatedAt | DateTime | — |

> **為什麼另開一個 Entity 而不是塞進 BossInteraction？** BossInteraction 語意是「等待老闆回應」，這裡是「老闆主動發起」，方向相反。另開 Entity 語意清楚，未來 Discord 端若也想記錄「老闆在頻道講過什麼」可沿用。

#### 2. Discord 頻道同步（A，Christ 決策）

指令送出後，Dashboard 端 **也** 將文字 + 圖片縮圖 Embed 發到 CEO 頻道，讓：
- Discord 端觀察者能看到 Dashboard 發過來的指令
- Victoria 的回應天然落在 CEO 頻道（現有行為）
- 雙通道的對話紀錄一致，之後翻 Discord 歷史也是完整的

Embed 樣式：
```
┌─ 👤 Christ（來自 Dashboard）─────────┐
│                                      │
│  我想知道目前任務進度...              │
│                                      │
│  [🖼 縮圖 1] [🖼 縮圖 2]              │
└──────────────────────────────────────┘
```

#### 3. 圖片尺寸限制（跟 Anthropic 硬上限對齊，Christ 決策）

- **單張 ≤ 5MB**（Anthropic Vision API base64 上限）
- **最多 5 張**（實務上 Claude prompt 帶 5 張以上圖理解品質會下滑）
- **格式**：`image/png`, `image/jpeg`, `image/gif`, `image/webp`
- 前端驗證（`MudFileUpload` + `OnFilesChanged`）+ 後端驗證（Controller）雙保險

#### 4. channelId fallback

Victoria 深度依賴 `channelId` 做 Discord Embed 輸出。Dashboard 觸發沒有 channelId，用 **AppSettings 中的「CEO 指令預設頻道」** fallback（29-4 系統設定頁提供編輯 UI）。

若 AppSettings 未設定，回傳 400 + 友善錯誤：「請先到『系統設定』配置 CEO 指令預設頻道」。

#### 5. Session 延續性

Dashboard 觸發的對話要**延續 CEO 頻道的 Claude Code session**（Christ 切換通道但對話應連續）：

- Victoria 的 session key 目前以 `channel.Id` 為準（Stage 15）
- Dashboard 路徑呼叫時帶入「CEO 指令預設頻道 ID」作為 session key
- 結果：Dashboard 發的指令和 Discord 發的指令共用同一條對話歷史，Victoria 不會失憶

#### 6. TriggeredBy 標記

若此次指令最終演變成任務（Victoria 回 `delegate` / `propose`），建立的 `TaskItem.TriggeredBy = "Dashboard"`（欄位已存在，Stage 18 以後），之後在流程追蹤頁面可以看出任務來源。

### 需要做的事

| 檔案 | 變更 |
|------|------|
| **Entity + Migration** | |
| `AiTeam.Data/Entities/BossCommandLog.cs`（新增） | 新 Entity |
| `AiTeam.Data/Configurations/BossCommandLogConfiguration.cs`（新增） | EF 配置（Images 用 jsonb） |
| `AiTeam.Data/Migrations/{timestamp}_AddBossCommandLog.cs` | Migration |
| `AiTeam.Data/Repositories/BossCommandLogRepository.cs`（新增） | CRUD |
| **Bot 端** | |
| `AiTeam.Bot/Controllers/CeoCommandController.cs`（新增） | `POST /internal/ceo/command` 端點 |
| `AiTeam.Bot/Services/AppSettingsService.cs` | 新增 `GetCeoDefaultChannelIdAsync` / `SetCeoDefaultChannelIdAsync` |
| **Dashboard 端** | |
| `AiTeam.Dashboard/Services/DashboardCeoCommandService.cs`（新增） | 呼叫 Bot internal API |
| `AiTeam.Dashboard/Controllers/CeoCommandController.cs`（新增） | `POST /api/ceo/command` multipart 接收 |
| `AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor`（新增） | 首頁指令卡片 |
| `AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor.cs`（新增） | 卡片邏輯 |
| `AiTeam.Dashboard/Components/Pages/Home.razor` | 引入 QuickCommandCard |
| `AiTeam.Dashboard/Components/Pages/System/SystemSettings.razor` | 新增「CEO 指令預設頻道」設定（29-4 配合） |
| **版本號** | |
| `Directory.Build.props` | v3.15.0 → v3.16.0 |

---

## 29 總體：需要修改的檔案清單（彙整）

| 分類 | 檔案數量 | 說明 |
|------|---------|------|
| 新增 Entity / Migration | 2 | `TaskGroup.ArchiveContent`、`BossCommandLog` |
| Bot 端新增端點 | 2 | `/internal/reload-cache`、`/internal/ceo/command` |
| Dashboard 新頁面 | 2 | `SystemSettings.razor`、`QuickCommandCard.razor` |
| Dashboard 既有頁面修改 | 5 | RulesManagement、AgentSettings、Home、PipelineView、NavMenu |
| Agent Service Log 補寫 | 2-4 | Petra、Design 相關、QA/Reviewer 修正 |
| 版本號 | 1 | `Directory.Build.props` |

---

## 建議實作順序

```
29-1（歸檔報告折疊面板）               ← 最獨立，純加欄位
  ↓
29-2（TaskLog 統一化）                  ← 跨多個 Agent，但都是小修
  ↓
29-3（Cache Reload 按鈕）               ← 獨立，為 29-4 暖身
  ↓
29-4（系統設定獨立頁）                  ← 為 29-5 的「CEO 預設頻道」設定鋪路
  ↓
29-5（Dashboard 下達指令 + 圖片）       ← 最大項，依賴 29-4 的系統設定 UI
```

**為什麼 29-5 最後？**
1. 需要 29-4 的系統設定頁面做「CEO 預設頻道」UI
2. 規模最大，放最後讓前四項先驗收穩定
3. 是之後 dogfooding 的基礎設施，值得單獨一段時間仔細驗收

---

## 設計決策與注意事項

### 為什麼不一起做 FF 十一（Token 守門動態調整）？

FF 十一需要改 `TokenTrackingProvider` 守門邏輯、AppSettings 新增一整組 key、表格 UI 編輯各 Agent 日/月限。工程量單獨就是一個小 Stage 的 M 規模。本階段聚焦「指令通道」和「UI 摩擦」，Token 守門動態化留給後續 Stage。

### 29-5 是否需要 Rate Limit？

單使用者（Christ）場景暫不需要。未來若開放其他使用者走 Dashboard 下指令（不經過 localhost bypass），再加。目前 localhost bypass 已是事實上的 rate limit（只有本機可達）。

### 29-5 的圖片為什麼存 DB，不用 Discord CDN？

- Discord CDN 連結有效期未知（Discord 過去改過），存 DB 可確保追溯能力
- `BossCommandLog` 本來就是追溯用途，圖片一起存語意一致
- PostgreSQL jsonb 存 base64 string 效率雖不如 blob，但本場景每次查詢只會拉單一筆（顯示歷史時），成本可接受
- 預估容量：5 張 × 5MB × base64 膨脹 1.37 倍 ≈ 34MB / 指令。每天 5 條也才 170MB，半年一次資料庫清理即可

### 29-5 是否要在 Dashboard 顯示 Victoria 回應歷史？

本階段 **不做**。Victoria 回應會以 BossInteraction 形式自然出現在操作中心（28a/28b 已有），使用者能看到。若未來要做「Dashboard 端的 CEO 對話紀錄」獨立頁，再從 `BossCommandLog` + CEO session 歷史組合查詢。

### 29-2 的「統一規格」會不會過度工程？

**不會。** 本階段要求的只是「開始/結束各一筆 Log」，每個 Agent 加 2-4 行 `logRepo.InsertAsync(...)` 的程度。不引入新的 Log 框架、不改 schema，只是補齊既有呼叫。

### 29-5 圖片在 Victoria session 中的行為

Victoria 使用 Claude Code CLI（Stage 15），而 Claude Code CLI 支援傳圖片。需確認：
- Claude Code CLI 接受圖片的方式（檔案路徑 vs stdin base64 vs `--image` flag）
- 若 CLI 限制，fallback 為：將圖片轉檔存到 Victoria 的 workspace 目錄，用絕對路徑帶入 prompt

> **實作階段需驗證**：實作 29-5 時先寫一個 spike，確認 Claude Code CLI 圖片傳遞方式。若與預期不同，需調整此設計。

---

## 驗收清單

### 29-1 歸檔報告折疊面板
- [ ] `TaskGroup.ArchiveContent` 欄位 + Migration 建立
- [ ] Doc/Sage Agent 完成後 DB 有 ArchiveContent
- [ ] PipelineView 顯示歸檔報告折疊面板
- [ ] 無歸檔報告時折疊面板不顯示（不留空殼）

### 29-2 TaskLog 統一化
- [ ] 逐一確認 Kickoff/Design/Dev/Reviewer/PM/QA/Doc 七種 Agent 的 Log 輸出清單
- [ ] 所有 Agent 最少產出 `running` + `done`（或 `error`）兩筆 Log
- [ ] QA/Reviewer 完成後不再殘留 `running` 舊 Log
- [ ] Petra 各審核點有對應 Log

### 29-3 Cache Reload 按鈕
- [ ] 規則管理頁面「套用變更」按鈕
- [ ] Agent 設定頁面「套用變更」按鈕
- [ ] Bot `/internal/reload-cache` 端點實作
- [ ] 點擊後 Snackbar 提示
- [ ] DB 修改後點擊 → 下次 Agent 執行確實讀到新值

### 29-4 系統設定獨立頁
- [ ] `/system-settings` 路由可達
- [ ] 側邊欄 NavMenu 有「系統設定」連結
- [ ] Agent 設定頁底部「系統設定」區塊已移除
- [ ] 原有設定（跳過 CEO 派工確認、Mock Mode）在新頁正常運作
- [ ] 「CEO 指令預設頻道」設定可編輯 + 存 AppSettings

### 29-5 Dashboard 下達指令 + 圖片
- [ ] 首頁顯示「快速下達指令」卡片
- [ ] 文字送出 → Victoria 收到 → CEO 頻道出現 Embed
- [ ] 文字送出 → 操作中心出現 Victoria 回應（BossInteraction）
- [ ] 送出後自動導向 `/interactions`
- [ ] 圖片上傳 → 縮圖預覽 + 刪除 X 正常
- [ ] 超過 5MB 圖片被前端拒絕
- [ ] 超過 5 張被前端拒絕
- [ ] 非 image/* 檔被前端拒絕
- [ ] 圖片送達 Victoria + CEO 頻道 Embed 含縮圖
- [ ] `BossCommandLog` 資料庫有記錄（文字 + 圖片 base64）
- [ ] CEO 預設頻道未設定時回傳友善錯誤
- [ ] Victoria 的對話歷史（Claude Code session）在 Dashboard 和 Discord 兩邊連續
- [ ] Victoria 將指令發展為任務時，`TaskItem.TriggeredBy = "Dashboard"`

### 整體
- [ ] `dotnet build AiTeam.slnx` 零 error
- [ ] `dotnet test` 通過
- [ ] Playwright 截圖驗收 Dashboard UI 變更（首頁、系統設定頁、規則管理頁 reload 按鈕）
- [ ] v3.16.0 版本號更新
- [ ] Master Plan 和 Future_Feature 同步更新

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-18 | v1.0 | Aria 撰寫初版規劃書，5 項（29-1 ~ 29-5），v3.16.0 |

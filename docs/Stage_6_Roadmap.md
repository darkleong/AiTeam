# Stage 6：強化、驗收與技術債清償

> 版本：v2.0
> 建立日期：2026-03-30
> 完成日期：2026-04-01
> 狀態：✅ 已完成

---

## 完成項目總覽

| # | 項目 | 說明 |
|---|------|------|
| 1 | Discord Vision 支援 | `/task` 新增 `image` 附件參數，CEO 可直接讀取截圖 |
| 2 | Requirements Agent 三層確認 | `exec_yes` 後先展示 Issue 清單，老闆確認後才呼叫 GitHub API |
| 3 | MudBlazor 全面替換 Telerik | 移除商業授權依賴，全面改用 MudBlazor 8.15.0（MIT） |
| 4 | Blazor Circuit 隔離修正 | `MudProviders.razor` 解決 InteractiveServer / SSR 電路分離問題 |
| 5 | Sidebar 收合展開 | MutationObserver 防止 Blazor DOM patch 重設收合狀態 |
| 6 | CEO 委派強化 | Prompt 強化 + code-level `action=reply→delegate` 修正 |
| 7 | Discord Embed 安全截斷 | 1024 字元限制保護，`Truncate()` helper |
| 8 | QA Agent 強化 | PR head branch 讀取、測試檔過濾 |
| 9 | Doc Agent 強化 | 遞迴目錄列舉、trailing slash 修正 |
| 10 | SignalR 即時更新補全 | pending / running / done / failed 全路徑推送 |
| 11 | Dashboard 新增 Agent UI | Agent 設定頁直接從 DB 新增，不需改程式碼 |
| 12 | Stage 5 E2E 驗收 | 五項 Discord 端對端測試全部通過 |

---

## Stage 6 實作重點紀錄

### 1. Discord Vision 支援

- `/task` 指令新增選用 `image` 附件參數
- `ILlmProvider` 介面擴充 `CompleteAsync` 支援 `ImageSource`
- `AnthropicProvider` 下載圖片 → Base64 → 組裝 Vision content block
- `CeoAgentService` 將圖片傳入 LLM，CEO 可直接理解截圖內容

### 2. Requirements Agent 三層確認機制

原本流程：老闆確認 → 直接建立 GitHub Issues（繞過安全確認）

新流程：
```
老闆下指令
    ↓ CEO 分派（第一層確認）
    ↓ Requirements Agent 說明操作（第二層確認）
    ↓ LLM 分析需求，列出準備建立的 Issue 清單（第三層確認）
    ↓ 老闆按 exec_yes
    → 實際呼叫 GitHub Issues API
```

### 3. MudBlazor 全面替換 Telerik

**移除：**
- `Telerik.UI.for.Blazor` NuGet（商業授權）
- `AddTelerikBlazor()`、Telerik CSS/JS 引用
- 4 個 Telerik using（`Telerik.Blazor` 等）

**加入：**
- `MudBlazor` NuGet（MIT 授權）
- `AddMudServices()`、MudBlazor CSS/JS 引用
- `@using MudBlazor`

**元件對應：**

| Telerik | MudBlazor |
|---------|-----------|
| `TelerikRootComponent` | `MudThemeProvider` + `MudDialogProvider` + `MudSnackbarProvider` + `MudPopoverProvider` |
| `TelerikGrid` (ServerData) | `MudTable ServerData="..."` |
| `TelerikGrid` (靜態) | `MudTable Items="..."` |
| `GridReadEventArgs` | `TableState` + `TableData<T>` |
| `GridRowClickEventArgs` | `TableRowClickEventArgs<T>` |
| `TelerikSlider SmallStep` | `MudSlider Step` |

### 4. Blazor Circuit 隔離修正

**問題：** `MudPopoverProvider` 在 SSR 的 `MainLayout` 中無法與 InteractiveServer 頁面共享電路，導致任務中心頁面空白。

**解法：** 新增 `MudProviders.razor`，設定 `@rendermode InteractiveServer`，由 `MainLayout` 引用。MudBlazor 的 providers 因此與頁面共享同一個 InteractiveServer 電路。

### 5. Sidebar 收合展開

**問題：** Blazor Enhanced Navigation 觸發 DOM patch 時，會重設 JS 加入的 CSS class，導致收合狀態丟失。

**解法：** 在 `App.razor` 使用 `MutationObserver` 監聽 `layout-wrapper` 的 `class` 屬性變更，偵測到變更後斷開觀察者、重新套用 localStorage 狀態、再重新連接，避免無限觸發。

### 6. CEO 委派強化

**問題：** LLM 偶爾回傳 `action=reply`（但 `target_agent` 不為空），或 `require_confirmation=false`，導致不走確認流程。

**修法：**
1. System Prompt 強化：明確規定派任務必須 `action="delegate"`
2. Code-level 補丁：若 `target_agent` 非空但 `action=reply`，強制修正為 `delegate`
3. 移除 `require_confirmation` 的 condition 判斷，一律走確認流程

### 7. Discord Embed 安全截斷

Discord Embed field value 上限 1024 字元，LLM 產出的說明文字可能超過。

```csharp
private static string Truncate(string value, int max = 1024)
    => value.Length <= max ? value : value[..(max - 3)] + "…";
```

套用於所有 Embed field（任務標題、說明、Agent 清單等）。

### 8. QA Agent 強化

**問題一：** PR 的原始碼檔案在 main branch 尚未存在，讀取時 404。
**解法：** 呼叫 `GetPullRequestHeadRefAsync` 取得 PR 的 head branch，讀取檔案時指定 `gitRef`。

**問題二：** PR 包含測試檔（`*Tests.cs`）本身，嘗試為測試檔補測試導致 404 或無意義輸出。
**解法：** 過濾 `*Tests.cs`、`*Spec.cs`、`.Tests/`、`.Test/` 路徑的檔案。

### 9. Doc Agent 強化

**問題一：** `src/AiTeam.Bot/Agents/` 路徑帶尾巴斜線，GitHub Contents API 回傳 404。
**解法：** `path = path.TrimEnd('/')`

**問題二：** `ListFilesAsync` 只列出一層目錄，子目錄內的 `.cs` 檔案找不到。
**解法：** 改寫為遞迴實作 `CollectFilesAsync`，遇到 `Dir` 型別自動遞迴，遇到 `NotFoundException` 靜默略過。

### 10. SignalR 即時更新補全

原本 Dashboard 只在特定路徑有 SignalR 推送，任務中心需手動 F5 才更新。

補全的推送時機：
- `confirm_yes` 處理後（任務建立，`pending` 狀態）
- `ExecuteAgentTaskAsync` 開始時（`running`）
- `ExecuteAgentTaskAsync` 完成 / 失敗時
- `ExecuteRequirementsFromPreviewAsync` 執行中 / 完成 / 失敗時

### 11. Dashboard 新增 Agent UI

**新增 `DashboardAgentService.CreateAgentAsync`：**
- 取得第一個 Team ID
- 建立 `AgentConfig`（`IsActive = true`）
- 回傳 `AgentConfigDto`

**`AgentSettings.razor` 新增表單：**
- 名稱（必填，不可重複）、描述、信任等級 MudSlider
- 送出後即時加入清單，顯示「重啟 Bot 後生效」提示

### 12. Stage 5 E2E 驗收結果

| # | 測試項目 | 結果 |
|---|---------|------|
| 1 | Dev Agent 雙層確認、PR 建立 | ✅ |
| 2 | QA Agent xUnit 測試 PR 建立 | ✅ |
| 3 | Requirements Agent 三層確認、GitHub Issues | ✅ |
| 4 | Doc Agent Markdown 文件 PR 建立 | ✅ |
| 5 | Dashboard 新增 Agent → 重啟 Bot → 動態偵測 | ✅ |

---

## 未完成項目

以下項目已轉移至 [Future_Feature.md](./Future_Feature.md)（未來功能候選清單）：

- Dev Agent 使用 Claude Code（🟡 中優先級）
- API 費用優化
- MCP 整合
- Agent 個性與造型設定
- 顧問 Agent 設計
- Documentation Agent 品質控管
- Dashboard UI 微調清單
- 從 Dashboard 重啟 Bot

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-03-30 | 初版建立 |
| 2026-04-01 | 第六項（Discord 圖片輸入）✅ 完成 |
| 2026-04-01 | 第八項（Requirements 確認機制）✅ 完成 |
| 2026-04-01 | 第十一項（Telerik → MudBlazor）✅ 完成 |
| 2026-04-01 | 第十項（Stage 5 E2E 驗收）✅ 全部通過 |
| 2026-04-01 | v2.0 Stage 6 結案，重組為實作紀錄格式；未完成項目轉移至 Stage_7_Roadmap.md |

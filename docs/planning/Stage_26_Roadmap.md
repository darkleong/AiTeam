# Stage 26 — 驗收基礎設施 + 版本號集中管理

> Stage：26
> 對應版本：v3.11.0
> 建立日期：2026-04-14
> 狀態：✅ 實作完成（2026-04-14）
> 文件版本：v1.1

---

## 目標

為 Stage 23/24/25a/25b（流程重構四連發）的大驗收建立必要基礎設施：

1. **Dashboard 任務詳情頁顯示會議紀錄 + 計劃書**
2. **Pipeline View 支援 Kickoff / Design 步驟**
3. **MockMode 全流程驗證修正**
4. **版本號集中管理（Directory.Build.props）**

> 本 Stage 不包含新的流程邏輯，專注於「看得到」和「跑得通」。

---

## 背景說明

### 待驗收項目

| Stage | 版本    | 內容                                                      | 驗收重點                                       |
| ----- | ------- | --------------------------------------------------------- | ---------------------------------------------- |
| 23    | v3.7.0  | Review Appeal + 實作說明 + 阻礙報告 + Sage 轉型 + Git Tag | Vera-Cody 對話迴圈、Petra 仲裁、Sage CHANGELOG |
| 24    | v3.8.0  | QA Petra 介入 + Dev_plan Appeal + TestReport 結構化       | QA 四路由、Dev_plan 反駁、文件傳遞             |
| 25a   | v3.9.0  | Kick-off 會議機制                                         | 5 人會議 + Christ 按鈕確認 + 修改計劃書        |
| 25b   | v3.10.0 | 設計規劃階段                                              | 提案簡化 + 設計會議 + consensus/escalate 路由  |

### 驗收時的流程

```
Discord: /mock proposal → 提案（簡化版，無 Rosa/Demi）→ Christ 核准
→ Kickoff 會議（MockMode 30~60 秒 consensus）→ Christ 確認
→ Design 設計會議（MockMode consensus）→ 直接 Dev_plan
→ Dev → Reviewer → QA → Doc → Merge

Dashboard: 全程觀察 Pipeline View + 任務詳情
```

---

## 實作項目

### 26-1. Dashboard 任務詳情頁顯示會議紀錄 + 計劃書

**現狀**

`TaskGroupDto` 缺少 Stage 25a/25b 的新欄位。DB 有 `KickoffMeetingLog`、`TaskPlan`、`DesignMeetingLog`、`DesignPlan`，但 Dashboard 看不到。

任務詳情目前是 `PipelineList.razor` 中的 MudDrawer slide-out，顯示 `<PipelineView>` 元件。

**需要做的事**

1. **`TaskGroupDto` 新增欄位**：

    ```
    KickoffMeetingLog, TaskPlan, KickoffRound,
    DesignMeetingLog, DesignPlan, DesignRound
    ```

2. **Repository 查詢補齊**：`GetGroupByIdAsync` 或對應的 projection 需要包含新欄位

3. **PipelineView.razor 新增折疊區塊**：在步驟列表之上，用 `MudExpansionPanels` 顯示：

    | 面板              | 內容                          | 條件                     |
    | ----------------- | ----------------------------- | ------------------------ |
    | 任務計劃書        | TaskPlan（Markdown 純文字）   | TaskPlan 不為空          |
    | Kick-off 會議紀錄 | KickoffMeetingLog（Markdown） | KickoffMeetingLog 不為空 |
    | 設計規劃書        | DesignPlan（Markdown 純文字） | DesignPlan 不為空        |
    | 設計會議紀錄      | DesignMeetingLog（Markdown）  | DesignMeetingLog 不為空  |
    - 預設全部收合（不佔空間）
    - 展開後用 `<MudText>` 或 `<pre>` 顯示內容（不需要 Markdown renderer，純文字即可）
    - 排列順序：計劃書在上、會議紀錄在下（先看結論，需要細節再展開紀錄）

---

### 26-2. Pipeline View 支援 Kickoff / Design 步驟

**現狀**

`PipelineView.razor` 的步驟列表從 `TaskItem` 的 `AssignedAgent` 欄位取得步驟名稱。`GetStepTitle()` 有 Petra 的特殊處理（fix iteration 顯示），但沒有 Kickoff / Design 的對應。

Kickoff 和 Design 步驟在 `FireOneStepAsync` 中被攔截（不走一般 Agent 路徑），所以它們的 `TaskItem` 可能不會被正常建立。需要確認。

**需要做的事**

1. **確認 TaskItem 建立**：確認 Kickoff / Design 步驟執行時，是否有對應的 TaskItem 被建立。如果沒有，需要在 `RunKickoffMeetingAndWaitAsync` / `RunDesignPhaseAsync` 開始時建立 TaskItem（status: running），完成時更新（status: done）。

2. **GetStepTitle() 新增顯示名稱**：

    ```
    "Kickoff" → "Kick-off 會議"
    "Design"  → "設計規劃"
    ```

3. **步驟圖示（如有）**：如果 PipelineView 有步驟圖示，Kickoff / Design 可用 `MudBlazor.Icons.Material.Filled.Groups`（會議）和 `MudBlazor.Icons.Material.Filled.DesignServices`（設計）。

---

### 26-3. MockMode 全流程驗證修正

**現狀**

MockMode 有兩大類問題需要修正：

#### 問題 A：Design 會議 session ID 解析錯誤

`MockClaudeCodeService.RunMeetingSessionAsync` 用 `sessionId.Split('-').Last()` 判斷 Agent 角色。這對 Kickoff 可以運作（session ID 格式 `{groupId}` 由 `group.Id.ToString()` 產生，Petra 的 session ID 就是 group GUID）。

但 Design 階段的 session ID 全部是 `Guid.NewGuid().ToString()`（純 UUID），`Split('-').Last()` 拿到的是 UUID 最後一段（如 `a1b2c3d4e5f6`），不會匹配任何 Agent 名稱 → 所有 Agent 都走 default 分支 → Petra 不會回傳 consensus JSON → MeetingService 的 `TryParseDesignPetraDecision` 回傳 null → **靠 null fallback 走 consensus**。

這個 fallback 恰好能跑通，但邏輯不正確。

#### 問題 B：Reviewer / QA / Doc 在 MockMode 下 0 秒完成

這三個 Agent 的執行流程一開頭就檢查 GitHub PR 是否存在（解析 PR 編號、查詢 open PR 列表等）。MockMode 不會建立真正的 GitHub PR，所以驗證直接失敗 → Agent 在到達 Mock 延遲代碼之前就 early return → **0 秒完成**，Dashboard 上根本看不到狀態變化。

具體路徑：

- **Reviewer**：找不到 open PR 或 PR 無 .cs 檔 → 立即返回
- **QA**：從任務描述解析不到 PR 編號或無可測試檔案 → 立即返回
- **Doc**：找不到 PR 編號 → 立即返回（且回傳 success，略過歸檔）

#### 問題 C：Kickoff / Design 步驟的 Dashboard 顯示時序

`FireOneStepAsync` 用 `_ = Task.Run(...)` fire-and-forget 啟動會議，立刻 return。會議本身有延遲，但需確認 Dashboard 的 Pipeline View 步驟狀態更新不會因為 fire-and-forget 而顯示異常。

**需要做的事**

1. **修正 Design session ID 解析（問題 A）**：

    修改 `MockClaudeCodeService` 的判斷邏輯，讓它在無法從 sessionId 解析 Agent 角色時，根據 prompt 內容判斷（如果 prompt 包含「整理」「判斷」等關鍵字 → 回傳 Petra consensus JSON）。

    或者在 MeetingService 呼叫時，在 prompt 中帶入角色標記（如 `[ROLE:Petra]`），MockClaudeCodeService 從 prompt 中解析角色。

2. **修正 Reviewer / QA / Doc 的 MockMode 路徑（問題 B）**：

    在各 Agent Service 的執行流程最前面加入 MockMode 檢查：

    ```
    if (MockMode 啟用)
    {
        await Task.Delay(30~60 秒);
        return MockMode 模擬結果（success）;
    }
    ```

    這樣在 MockMode 下直接跳過 GitHub 驗證，走 Mock 路徑（含延遲），確保 Dashboard 有足夠時間顯示狀態變化。

    > 注意：Dev Agent 已有正確的 MockMode 延遲路徑（透過 MockClaudeCodeService），不需要修改。

3. **確認 Kickoff / Design 步驟的 Dashboard 狀態（問題 C）**：

    確認 `RunKickoffMeetingAndWaitAsync` / `RunDesignPhaseAsync` 在開始時有正確建立 TaskItem（status: running），且 fire-and-forget 不影響 Dashboard 即時更新。如果 26-2 已處理 TaskItem 建立，此項只需驗證。

4. **全流程延遲審計**：確保 MockMode 下每個步驟（從提案到 Merge）都至少有 30 秒可觀察時間。

5. **驗證完整 MockMode 流程**：
    - `/mock proposal` → 提案（無 Rosa/Demi）→ 核准
    - → Kickoff（MockMode consensus）→ Christ 按按鈕
    - → Design（MockMode consensus）→ 直接 Dev_plan
    - → Dev → Reviewer → QA → Doc → Merge
    - 全程無卡住、無報錯
    - **每個步驟在 Dashboard 上都能觀察到 running 狀態至少 30 秒**

---

### 26-4. 版本號集中管理（Directory.Build.props）

**對應 Future Feature：十五**

**現狀**

版本號分散在 `AiTeam.Bot.csproj` 和 `AiTeam.Dashboard.csproj`，其他專案（AppHost / Data / Shared / ServiceDefaults）沒有版本號。每次改版需修改兩個檔案。

**需要做的事**

1. **在解決方案根目錄建立 `Directory.Build.props`**：

    ```xml
    <Project>
      <PropertyGroup>
        <Version>3.11.0</Version>
      </PropertyGroup>
    </Project>
    ```

    放在 `AiTeam.sln` 同層目錄（`src/`）。

2. **移除各 `.csproj` 的 `<Version>` 和 `<AssemblyVersion>`**：
    - `src/AiTeam.Bot/AiTeam.Bot.csproj`
    - `src/AiTeam.Dashboard/AiTeam.Dashboard.csproj`

3. **驗證**：
    - `dotnet build` 確認所有專案自動繼承版本號
    - Dashboard 頁腳讀取 assembly version 仍正確顯示
    - GitHub Actions auto-tag（如有讀 .csproj version 的邏輯）仍正常

4. **更新 CLAUDE.md**：版本號管理章節改為指向 `Directory.Build.props`，移除「需要修改的地方」中的兩個 .csproj

---

## 實作順序建議

```
1. 26-4（Directory.Build.props）  ← 最簡單，先做
2. 26-1（Dashboard DTO + 詳情頁） ← 主要 UI 工作
3. 26-2（Pipeline View 步驟支援） ← 依賴 26-1 的 DTO
4. 26-3（MockMode 修正）          ← 最後跑一遍全流程驗證
5. 版本號設定為 3.11.0
```

---

## 驗收清單

### 26-1 Dashboard 詳情頁

- [ ] TaskGroupDto 包含 KickoffMeetingLog / TaskPlan / DesignMeetingLog / DesignPlan
- [ ] PipelineView 顯示折疊面板（任務計劃書、Kickoff 紀錄、設計規劃書、設計紀錄）
- [ ] 面板僅在有內容時顯示
- [ ] 面板預設收合，展開後可讀

### 26-2 Pipeline View

- [ ] Kickoff 步驟顯示為「Kick-off 會議」
- [ ] Design 步驟顯示為「設計規劃」
- [ ] 兩個步驟的 status（running / done / failed）正確顯示

### 26-3 MockMode

- [ ] Design 會議 session ID 解析正確（Petra 回傳 consensus JSON，非靠 null fallback）
- [ ] Reviewer 在 MockMode 下有 30~60 秒延遲，不會 0 秒完成
- [ ] QA 在 MockMode 下有 30~60 秒延遲，不會 0 秒完成
- [ ] Doc 在 MockMode 下有 30~60 秒延遲，不會 0 秒完成
- [ ] Kickoff / Design 步驟在 Dashboard 上顯示 running 狀態正確
- [ ] `/mock proposal` → 核准 → Kickoff → Design → Dev_plan → ... → Merge 全流程跑通（待 Christ 驗收）
- [ ] 每個步驟在 Dashboard 上都能觀察到 running 狀態至少 30 秒（待 Christ 驗收）
- [ ] MockMode 無卡住、無報錯（待 Christ 驗收）
- [ ] BugFix MockMode 跳過 Kickoff + Design（待 Christ 驗收）

### 26-4 版本號

- [x] `Directory.Build.props` 存在且版本為 3.11.0
- [x] 各 `.csproj` 無 `<Version>` 標籤
- [ ] Dashboard 頁腳顯示 v3.11.0（待部署後 Christ 確認）
- [x] `dotnet build` 零 error
- [x] CLAUDE.md 版本號章節已更新

### 整體

- [x] `dotnet build` 零 error
- [x] `dotnet test` 通過

---

## 不在 Stage 26 範圍

| 項目                       | 原因                                                                        |
| -------------------------- | --------------------------------------------------------------------------- |
| 會議紀錄 Markdown 渲染     | 純文字足夠驗收，Markdown renderer 是後續美化                                |
| Dashboard 會議紀錄獨立頁面 | 折疊面板足夠，獨立頁面是後續需求                                            |
| 實際（非 Mock）流程驗收    | Stage 26 確保 MockMode 能跑通，實際流程驗收在 Stage 26 完成後由 Christ 執行 |

---

## 變更紀錄

| 日期       | 版本 | 內容                                                                                                                                                                                                  |
| ---------- | ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-04-14 | v1.0 | Aria 撰寫初版規劃書                                                                                                                                                                                   |
| 2026-04-14 | v1.1 | 擴充 26-3：新增 MockMode 延遲修正（Reviewer/QA/Doc 0 秒問題）+ Dashboard 狀態時序確認                                                                                                                 |
| 2026-04-14 | v1.2 | 實作完成：26-4（Directory.Build.props）→ 26-1（DTO + PipelineView 折疊面板）→ 26-2（Kickoff/Design TaskItem）→ 26-3（MockMode session ID 解析 + 狀態時序）；dotnet build 零 error，驗收待 Christ 確認 |

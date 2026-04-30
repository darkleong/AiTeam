# Future Feature — v4 後重評估

> 從 `Future_Feature.md` 拆出（2026-05-01）
> 作用：受 v4 路線（FF 四十九 工具評估 + FF 三十六 架構評估）影響，需要 spike 結果出來後重新評估的 FF

**重評估觸發點**：
- FF 四十九 Phase A spike 完成（Stage 50）
- 視 spike 結論決定每個 FF 的命運（仍需做 / 被吸收 / 重新設計）

---

## 十、Dashboard UI 細節打磨（第四批）

> 狀態：🔵 低優先級 — UI 組織與使用便利性優化，待 Christ 確認完整清單後排入 Stage
> v4 評估：動態架構下 UI 重新設計，可能多項變過時 — 待 Stage 50 後重評估

### 背景

Stage 19 已完成三批 Dashboard UI 細節打磨。以下為第四批累積的改善需求。

### 改善清單

#### 系統設定獨立頁面

目前「Agent 設定」頁面下方塞有「系統設定」區塊（跳過 CEO 派工確認、Mock Mode）。隨著系統設定項目增加（Token 守門閾值等），繼續共用同一頁面會越來越混亂：
- 新增路由 `/system-settings`，對應新 Blazor 頁面 `SystemSettings.razor`
- 側邊欄 NavMenu 加入「系統設定」連結
- Agent 設定頁移除底部「系統設定」區塊，功能邏輯不變

#### 任務列表頁面

目前篩選只有「狀態」一種。新增以下三種篩選：
- **專案篩選** — 依專案名稱過濾
- **Agent 篩選** — 依執行 Agent 過濾
- **觸發來源篩選** — 依觸發方式過濾（Discord 指令 / GitHub Webhook / 排程 / 手動等）

#### 流程追蹤頁面

目前篩選只有「狀態」一種。新增以下兩種篩選：
- **專案篩選** — 依專案名稱過濾
- **流程類型篩選** — 依流程類型過濾（開發流程 / 文件 / Release / Ops 等）

PR 欄位顯示優化：
- 目前顯示完整 GitHub URL（冗長）
- 改為顯示 PR 編號超連結（例如：`#999`），點擊開新分頁到 GitHub PR 頁面

#### 專案管理頁面

「啟用」欄位目前顯示 Switch + 文字「啟用中」，文字冗餘：
- 移除文字「啟用中」，只保留 Switch 本身

#### 規則管理頁面

「狀態」欄位同上，Switch + 文字「啟用中」冗餘：
- 移除文字「啟用中」，只保留 Switch 本身

「操作」欄位目前顯示文字按鈕（編輯 / 刪除），視覺佔空間太大：
- 改為圖示按鈕（Icon Button），不顯示文字
- 建議圖示：編輯用 `Edit`，刪除用 `Delete`（MudIconButton）

#### TaskLog 獨立檢視頁面

目前 TaskLog 只能透過任務列表或 Pipeline View 點擊任務後，在右側 480px 抽屜中查看，不夠直觀。新增獨立頁面：
- 新增路由 `/logs`，對應新 Blazor 頁面
- 側邊欄 NavMenu 加入「執行記錄」連結
- 以時間軸方式展示所有 Agent 的 TaskLog，可依 Agent / 任務 / 狀態篩選
- 支援展開 Payload 內容（如 Designer 的 UI 規格 Markdown）

#### Dashboard Cache Reload 按鈕

目前在 Dashboard 修改規則或 Agent 設定後，需要到 Discord 執行 `/reload-rules` 才能生效，操作流程不合理。Dashboard 應自帶 reload 按鈕：
- 規則管理頁面 / Agent 設定頁面新增「套用設定」按鈕
- 按鈕呼叫 Bot internal API：`POST /internal/reload-cache?scope=rules|agents|all`
- Bot 收到後清空對應 Cache，重新從 DB 載入
- 與 DashboardPushService 同模式（Dashboard → Bot HTTP 呼叫）

#### Agent 角色設定 Dashboard 化

目前每個 Agent 的角色描述、行為準則都硬碼在 C# Agent Service 的 prompt 字串中，修改需要改程式碼重新部署。將可配置的部分抽出至 DB，透過 Dashboard 編輯：

**可配置（存 DB）：**
- 角色設定 — 「你是 Cody，Dev Agent，負責撰寫程式碼...」
- 行為準則 — 「寫完要 dotnet build 確認」「commit message 用繁中」

**不可配置（留在 C#）：**
- 流程邏輯模板 — 輸出格式要求、JSON 結構定義等（與 C# parse 邏輯綁定，改了 prompt 會導致程式端解析失敗）

**技術方向：**
- 復用現有規則系統的 DB + Cache + `/reload-rules` 機制
- Agent 設定頁面擴充：現有 Provider / Model 欄位旁，新增「角色設定」和「行為準則」文字編輯區
- prompt 組合順序：`DB 角色設定` + `DB 行為準則` + `C# 流程邏輯模板` + `任務動態內容`
- 與 Feature 二（Agent 個性）共用：個性描述就是角色設定的一部分

**分階段實作**：
- **Phase 1**：C# `SystemPrompt` 抽至 DB（API 層 Agent — Rosa / Demi / Sage / Release / Ops / Petra 部分）
- **Phase 2**：`CLAUDE_*.md` template 抽至 DB（CLI 層 Agent — Victoria / Cody / Vera / Quinn / Petra 部分）— **Stage 39 結案後 Christ 提出（2026-04-25）**：Stage 38 `AgentConfigCache` pattern 直接抄即可，工程上無新東西；但目前 `CLAUDE_*.md` 內容仍在演進（Stage 39 才剛擴充 Vera），且修改頻率低（1-2 月一次）+ 對系統行為影響大（JSON 輸出格式 / 工具範圍 / 角色定位），**code review 軌跡比 DB 安全**。**觸發條件**：等 AiTeam 核心功能穩定（Trial_v3 跑完、CLAUDE_Vera.md 內容定型 1-2 月後）+ 出現「想立刻試新 prompt」真實使用情境，再評估 Phase 2

### 優先級

🔵 低優先級 — 不影響功能，純 UI 組織與使用便利性優化；Phase 2（CLAUDE_*.md）需等核心穩定，目前不急

### v4 重評估點

動態架構下 Dashboard UI 整體可能重新設計（Petra 動態調度 view 不一樣），多個子項可能變過時或重新整合。

---

## 十一、Dashboard 可調整 Token 守門全域限額

> 狀態：🟡 中優先級 — Stage 37-2 驗收首次實際踩到全域月限（2026-04-24 升級）
> Stage 排序：**Trial_v5 之後評估**（2026-04-29 Christ 拍板），不預設 Stage 編號
> v4 評估：FF 四十七 部分涵蓋 ops 議題；本 FF 仍可能保留 Dashboard UI 部分

### 背景

Stage 22 實作了 Token 守門機制，包含：
- **全域月費上限**：`AgentSettings:MonthlyTokenLimitK`（預設 1000K）
- **各 Agent 日限**：`AgentSettings:Agents:{Name}:DailyTokenLimitK`
- **各 Agent 月限**：`AgentSettings:Agents:{Name}:MonthlyTokenLimitK`
- **單次請求上限**：`AgentSettings:SingleRequestTokenLimitK`（預設 50K）

目前這些值只能透過修改 `docker-compose.prod.yml` 環境變數並重新部署來調整。Token 監控頁面的警示訊息也只能說「請至 Bot 設定調整」，沒有直接入口。

### 實際踩坑紀錄（Stage 37-2 驗收，2026-04-24）

驗收「Agent 設定頁顯示 Provider/Model」任務（FF 四第二階段 self-implement 試驗）跑到 Dev 階段時，TokenTrackingProvider 的 Check 4（全域月限）擋下：

```
Dev Agent 執行失敗：Token 守門：全域本月用量 1,304,628 + 估算 5,289
超過全域月限 1,000,000。所有 LLM 呼叫已暫停。
```

老闆角度的體驗問題：
- Dashboard Token 監控頁警告訊息寫著「請至 Bot 設定調整 `AgentSettings:MonthlyTokenLimitK`」—— 相當於**教使用者 SSH 改 config + push + 等 CI/CD 重 build**
- 對「只動嘴的老闆」完全不友好，Stage 27b 以降的 `/pause` `/resume` 已經把佇列控制 Dashboard 化，Token 守門也該跟上

### 需求

在 Dashboard 「系統設定」頁面（配合十、系統設定獨立頁面規劃）加入 Token 守門設定區塊：

- **全域月費上限**（MonthlyTokenLimitK）— 數字輸入框，單位 K tokens
- **單次請求上限**（SingleRequestTokenLimitK）— 數字輸入框，單位 K tokens
- **各 Agent 日限 / 月限** — 表格形式，每個 Agent 一列，可直接修改數字

修改後儲存到 **動態 `AppSettings` 資料表**（Bot 端已有 TTL cache 機制），不需要重啟容器即可生效。Token 守門邏輯需改成優先讀取動態設定，appsettings.json 作為預設值 fallback。

### 技術方向

1. `AppSettings` 資料表新增 `AgentTokenLimits:*` 系列 key（或 JSON blob）
2. `TokenTrackingProvider` 守門邏輯改成透過 `AppSettingsService` 讀取動態值（有快取）
3. Dashboard 新增對應 UI，透過現有的 `DashboardAppSettingsService` 存取

### 優先級

🟡 中優先級 — **Stage 排序：Trial_v5 之後評估**（候選搭車：FF 十 Agent 設定頁 refactor / 順手吸收 Trial_v5 揭露的新議題如 FF 四十七 Token SoT）

### v4 重評估點

FF 四十七（Token SoT 統一）已涵蓋部分（ops 配置流程），本 FF 可能聚焦 Dashboard UI 部分。

---

## 十四、Agent I/O 完整記錄（待討論）

> 狀態：⚪ 待討論 — 等 TaskLog 獨立頁面（十）上線後，實際檢視現有 Log 內容再決定
> v4 評估：MS Agent Framework 內建 telemetry / observability 可能涵蓋

### 背景

目前 TaskLog 記錄的是流程層級的步驟狀態（「開始執行」「Git Clone 完成」等），不包含 Agent 實際與 LLM 交換的 prompt 和 response 全文。Christ 希望能看到每個 Agent 接收和產出的完整內容。

### 涵蓋範圍

系統中 Agent 與 LLM 的互動分兩條路徑，都需要埋 log：

| 路徑 | 涉及 Agent | 埋點位置 |
|------|-----------|---------|
| Claude Code CLI | Victoria、Cody、Vera、Quinn、Sage、Rosa、Demi、Petra（部分） | `ClaudeCodeService` 執行前後 |
| LLM API | Rena、Petra（部分）、申訴/仲裁流程 | `ILlmProvider.CompleteAsync()` 前後 |

Maya（Ops）為純程式邏輯，不呼叫 LLM，不在範圍內。

### 注意事項

- **資料量大**：Claude Code CLI 的 prompt 通常包含 CLAUDE.md、規則、上下文，一筆可能數千到上萬 token 的文字量
- **DB 儲存策略**：需考慮是否壓縮、TTL 自動清理、或只存摘要
- **Dashboard 載入效能**：完整 prompt/response 不適合列表直接顯示，應採用摺疊/按需載入

### 決策前提

先完成十的 TaskLog 獨立頁面，Christ 實際看過現有 Log 內容後，再決定：
1. 現有 TaskLog 的資訊是否足夠
2. 若不夠，需要補到什麼層級（流程摘要 vs 完整 I/O）
3. 儲存策略與效能取捨

### 優先級

⚪ 待討論 — 依賴十的 TaskLog 頁面上線後再評估

### v4 重評估點

MS Agent Framework 內建 telemetry / observability，可能完全涵蓋本 FF 需求 — Stage 50 後重評估。

---

## 十九、Agent maxTurns 動態化（Dashboard 可調）

> 狀態：⚪ 待觀察 — 等 AiTeam 架構穩定（例如 Codex CLI / Gemini CLI 整合後）再評估
> 提出日期：2026-04-20（Stage 32 規劃時釐清）
> v4 評估：FF 四十八 是 specific 子議題（Cody Dev_plan maxTurns）；本 FF 涵蓋更廣

### 背景

Stage 32 規劃「系統設定頁擴充」時考慮過把 Claude Code CLI 的 `maxTurns` 抽出成動態設定，但發現**不可行**：

- `maxTurns` 散落在 `MeetingService` 等多處，對應**不同角色**有不同值：
  - Rosa / Demi / 設計會議 Agent：25
  - 其他特定場景：12
  - Victoria 預設：40
- 不是單一「全域 maxTurns」設定，要放 UI 就得「每個 Agent 一個欄位」，Dashboard UI 會爆炸
- 強抽單一值會造成行為不一致（會議被截斷 / 探索不夠深 / 浪費 Token）

### 為什麼值得保留為候選

Christ 的長期願景是「Dashboard 能調 AiTeam 各項行為參數」。maxTurns 是其中一個——未來 Dashboard 上把每個 Agent 的 maxTurns / Model / 行為準則都整合成一個完整的「Agent 設定頁」時，maxTurns 應該放進去。

### 觸發條件

以下任一發生時，重新評估：

- 多 CLI 供應商整合完成（FF 四 第二階段），此時「每個 Agent 的 CLI 設定」自然擴充到需要包含 maxTurns
- Agent 角色設定 Dashboard 化（FF 十「Agent 角色設定 Dashboard 化」子項），此時順帶把 maxTurns 加入同一個設定區塊
- 老闆實際遇到「會議被截斷」或「探索不夠深」等事故，需要臨時調 maxTurns 又無法動設定檔

### 優先級

⚪ 待觀察 — 屬於 AiTeam 架構穩定後的「Dashboard 控制中心」願景拼圖之一，不急

### v4 重評估點

v4 framework 下 maxTurns 設定機制可能改變，本 FF 範圍待 Stage 50 spike 後重新規劃。

---

## 二十二、Agent 命名一致性（守門 + 名稱映射）

> 狀態：🔵 低 — 防呆類技術債，等搭車或獨立小 Stage
> 提出日期：2026-04-22（Stage 33 Roadmap v2.1 Dev_plan ghost 卡 debug 時浮現）
> v4 評估：動態架構下 Worker pool 命名規則改變

### 背景

AiTeam 系統中存在**兩種「Agent 名稱」的混淆**，導致 Stage 33 驗收時出現「Dev_plan ghost 卡」bug：

| 層面 | 什麼是 Agent 名稱 |
|------|-------------------|
| **DB `agent_configs` 表** | 真 Agent（Cody / Rosa / Demi / Vera / Quinn / Sage / Petra / Victoria / Maya / Rena）|
| **TaskItem.AssignedAgent** | 工作階段名，**可能含 workflow-only 的階段名**（Dev_plan / Kickoff / Design 等）|

**實際事故**：
- `AgentQueueProcessor.ExecuteTaskAsync` 推送 `AgentStatus` 時，`AgentName = task.AssignedAgent`
- 如果任務階段是「Dev_plan」，推送出去的 AgentName 就是 "Dev_plan"
- Dashboard `Home.UpdateAgentStatus` 對 SignalR 推進來的未知 AgentName **盲接 Add**
- → 首頁出現「Dev_plan」這個假 Agent 卡（實際沒有這個 Agent）
- Stage 33 臨時修法：`UpdateAgentStatus` 加白名單過濾，只接受初始 DB 撈的真 Agent

這個白名單是**症狀補丁**，根本的兩個議題留待本 FF 處理。

### 子項 A：AgentConfigService 命名守門

**現況**：建立 / 修改 `AgentConfig` 時，沒有任何檢查去擋「名稱是否為 workflow 保留字」。

**實作方向**：
- `AgentConfigService` 新增 `ReservedWorkflowNames` 常數：`{ "Dev_plan", "Kickoff", "Design" }`（未來有新 workflow 階段名再加）
- 建立 / 改名時檢查，撞到保留字 → 拋例外 + Dashboard 顯示錯誤
- 順便檢查 Agent 名稱是否與其他既有 Agent 重複

**規模**：S（約 1 個 validation method + 對應 UI 錯誤處理）

### 子項 B：Bot PushAgentStatus 名稱語意清理

**現況**：`AgentQueueProcessor.ExecuteTaskAsync` 推送 AgentStatus 時直接用 `task.AssignedAgent`，語意模糊——「工作階段名」被當成「真 Agent 名」推到客戶端。

**實作方向（兩種策略）**：

1. **映射法**（推薦）：Bot 端 push 前做 `AssignedAgent → 真 Agent` 映射表（如 `Dev_plan → Cody` / `Kickoff → Petra` / `Design → Petra`），客戶端收到的 AgentName 一律是真 Agent
2. **雙欄位法**：推送時明確分 `AgentName`（真 Agent）+ `WorkflowStage`（可選，階段名），客戶端依需要用

**規模**：M（影響 `AgentStatusViewModel` + `PushAgentStatusAsync` + 客戶端 `UpdateAgentStatus` + StatusBadge / Pipeline View 可能的 fallback 邏輯）

#### 具體案例（2026-04-25 Trial_v2 試驗發現）

執行 self-implement 試驗 v2 期間，Christ 觀察到首頁 Dev Agent 狀態卡顯示矛盾：

| 顯示位置 | 邏輯 | 看到什麼 |
|---------|------|---------|
| Agent chip 「閒置」 | 看 SemaphoreGroups 的 `Dev` 是否被佔用 | Dev_plan 不算佔 Dev semaphore → 閒置 |
| Expand 「🏃 規則管理頁表... 已跑 40s」 | 看 TaskItem.AssignedAgent | 包含 Dev_plan → 顯示 running |

**兩個資料來源邏輯各自正確，但並列顯示 → 使用者覺得矛盾。**

### 搭車時機

- **子項 A 可獨立做**：若未來做 Agent 設定頁 refactor（FF 十「Agent 角色設定 Dashboard 化」）時一起加守門最經濟
- **子項 B 較大，建議搭車**：等未來 refactor SignalR push 層、或做 FF 八 Phase 2 時（循環偵測會觸及 Agent 狀態推送）再一起整理

### 優先級

🔵 低 — 白名單補丁已防住實際影響，本 FF 是架構清理。

### v4 重評估點

動態架構下 Worker pool 命名規則可能改變（Magentic Orchestration 不需要 Workflow 階段名），本 FF 可能變過時或範圍縮小。

---

## 二十五、Self-implement 試驗 prompt 設計守則（Cody 繞道傾向）

> 狀態：🟢 經驗紀錄 — 不是技術 FF，是 prompt design 知識庫
> 提出日期：2026-04-24（Stage 37-2 PR #107 self-implement 試驗 close 後）
> v4 評估：Trial_v5 戰略結論「適用範圍大幅擴展」改變定位，本 FF 待重評估

### 背景

Stage 37-2 期間 Christ 順手做了一個「self-implement 試驗」：把 Stage 37-1 範圍刻意砍掉的子項（FF 四第二階段-A：Dashboard Provider/Model 顯示）當成普通需求丟給 Victoria，看系統能不能自己實作。

整條流程跑通，但生出的 PR #107 經 Aria review 後 close 不 merge，**因為架構方向不對**：Cody 選了「Dashboard 自己讀一份 appsettings」快路徑，繞過了「真正該做的 DB migration」。

### 觀察的傾向

**Cody 在 Petra 沒擋下時，會優化「執行成本」而非「架構正確性」。** 具體表現：

1. **遇到「需要動 entity + migration」的需求**：Cody 傾向找方案讓改動只發生在 service 層 / config 層，避免 schema 變更
2. **遇到「需要跨服務同步」的需求**：Cody 傾向各自為政（Bot 一份 config、Dashboard 一份 config）而非建立單一 source of truth

### 守則建議（給未來想做 self-implement 的需求 prompt）

下需求給 Victoria 時，如果涉及以下任一情境，**prompt 必須明寫禁止語句**：

- 「資料源統一」/ 「single source of truth」類需求
  → 「**禁止繞道：必須建立 DB schema 統一資料源，不接受純 config 同步方案**」
- 「跨服務一致性」需求
  → 「**禁止讓 Bot 和 Dashboard 各自維護獨立配置**」
- 「動 schema」類需求
  → 「**必須包含 EF Migration**，不接受純 service-layer 補丁」

### 對 Petra 的觀察

> ✅ **Stage 40（v3.27.0，2026-04-26）已完成此 Petra 子項**：CLAUDE_Petra.md 第 4 節新增「Warning→blocking 升級規則」清單五條（重複定義 / 硬編碼常數 / config 分散 / pattern match / `target="_blank"` 缺 `rel="noopener"`）。FF 二十五本體（self-implement 任務 prompt 設計守則）仍保留作為未來規劃 Trial 任務的 reference。

### 優先級

🟢 經驗紀錄 — 沒有對應的「實作項目」，這是 prompt design knowledge。

### v4 重評估點

Trial_v5 證實 self-implement 適用範圍大幅擴展（跨 11 元件任務不再縮水）。本 FF 的「禁止繞道」守則在 v4 動態架構下可能簡化（Petra orchestrate 時主動避免繞道），需 Stage 50 後重評估是否仍需要這套 prompt 守則。

---

## 三十八、跨專案能力研究（多 repo / scaffold / 環境建置 spike）

> 狀態：⚪ **待深度討論** — 議題已立案，但 scope / 拆分 / 觸發條件待 Christ + Aria 深度討論後才決定
> 提出日期：2026-04-29（Stage 44 進行中時 Christ 詢問「AiTeam 能否處理新增專案需求」引發）
> v4 評估：v4 動態架構影響跨 project 設計（子議題 B 跟 FF 三十六 耦合）

### 背景

Christ 2026-04-29 詢問 AiTeam 能否處理「新增專案」需求，提出兩個情境：

1. **完整自動化**：提需求 → AiTeam 從零建專案（建 repo / 環境 / 容器 / runner）
2. **半自動**：Christ 手動建 repo + 環境 → 在 Dashboard 加 Project → 請 AiTeam 幫忙建 PostgreSQL schema / Dockerfile / GitHub Actions yml 等程式碼層內容

### Aria grep 揭露的現況限制

**情境 1（完整自動化）→ 完全做不到**：
- 整個 AiTeam 架構假設「已存在 repo + 已存在環境」（team-on-existing-repo 模式）
- `GitHubSettings.Owner` / `DefaultRepo` hardcode（appsettings.json）
- Bot 端讀「TaskGroup.Project 字串」當 repo name，**`Project.RepoUrl` 欄位只在 Dashboard 顯示用，Bot 沒讀**（PmAgentCommons.cs:45/110 / CeoAgentService.cs:107/134/445 證實）
- Bot 容器內無法執行 `docker compose`（CLAUDE.md 已規範）
- GitHubService 沒包 CreateRepo API / 沒包 secret 設定 / 沒包 runner 設定

**情境 2（半自動）→ 部分可行**：
- ✅ 可寫：PostgreSQL EF Migration / Dockerfile / GitHub Actions yml / appsettings template（純檔案產出 Cody 強項）
- ❌ 不可：跨 owner repo 操作 / 真實啟動容器 / 設 Actions runner / 建 Discord channel / 設 Tailscale

### 兩個子議題候選

#### 子議題 A：跨 Project repo 支援（基礎建設類）

- **內容**：Bot 端改讀 `Project.RepoUrl` 而非 GitHubSettings hardcode；支援多 owner / 多 repo 混用
- **規模**：M-L（動 PmAgentCommons / CeoAgentService / GitHubService 多處）
- **戰略價值**：FF 七「客戶專案交付」前置條件 + 半自動情境的 1/3 解
- **相對成熟**：純改機制，不涉新工作流 — 可獨立做，與 v4 架構研究脫勾

#### 子議題 B：新專案 scaffold 流程（戰略級）

- **內容**：CEO / Petra 接收「建新專案」需求 → 觸發 scaffold 工作流（建 repo / dotnet new / 寫 docker-compose / 寫 Migration / 設 secrets）→ 部分自動 + 部分 Christ 手動操作
- **規模**：L+（新工作流類型 / GitHub API 擴充 / 跨環境協同）
- **戰略**：⭐ 對齊 AutoGPT / SWE-Agent 級能力，可能是 v4 架構一部分
- **跟 FF 三十六 耦合**：動態調度 + per-task session 是這類工作流的前提

### 待深度討論的問題

1. **拆 vs 合**：兩個子議題拆 2 個 FF 還是合 1 個？
2. **A 獨立性**：子議題 A 是否該獨立先做（與 v4 架構研究脫勾）？
3. **B 併入 FF 三十六**：子議題 B 是否該併入 FF 三十六 spike 範圍？
4. **FF 七 邊界**：FF 七（客戶專案交付）跟本 FF 的邊界？
5. **觸發條件優先**：客戶專案實際需求 / Trial_v5 結果 / FF 三十六 spike 結論 — 哪個應該觸發本 FF 深度討論？

### 優先級

⚪ **待深度討論** — 議題已立案但 scope / 拆分 / 觸發條件待 Christ + Aria 深度討論後才決定。

### v4 重評估點

子議題 A 可與 v4 脫勾獨立做；子議題 B 跟 FF 三十六 v4 架構耦合 — Stage 50 後評估邊界。

---

## 四十、Stage 46 Dashboard razor UI 接線（epic 折疊 + 進度條 + 暫停按鈕）

> 狀態：🟠 **中-高** — 後端全鏈路就緒，純前端 razor 拼接；影響 Trial_v5 觀察期 UX
> 提出日期：2026-04-29（Stage 46 驗收期 follow-up E）
> v4 評估：動態架構下 UI 重新設計

### 背景

Stage 46 FF 三十五 完成後端全鏈路（DTO + DashboardTaskService + Internal API + DashboardBotService client）但 razor UI 接線未做：
- PipelineList epic 主卡片 `📦 Epic - ` 標題 + sub-task 折疊
- PipelineView epic 進度條（MudTimeline N 個 Phase connector）
- 議題 5 epic 暫停 / 恢復按鈕（Stage 45 PipelineView paused alert + 按鈕風格延續）

### 為何 follow-up

- 純前端 razor 拼接無風險（後端就緒）
- Stage 46 戰略級規模 + 8 子項已飽和，UI 拼接留下個 Stage 統一處理時序最佳
- 但 **Trial_v5 觀察期 Christ 看不到 epic 折疊 UI**（DB / log 仍可驗）— UX 影響中-高

### 修法方向

對齊 Stage 45 PipelineView paused alert + 暫停 / 恢復按鈕風格，razor 拼接：
- PipelineList：MudExpansionPanels 折疊 sub-task / 標題前加 `📦 Epic - ` / 過濾掉 ParentGroupId is not null 的 row
- PipelineView：MudTimeline 顯示 N 個 Phase status connector / 點 Phase 跳 sub-task PipelineView
- epic 暫停 / 恢復 button：呼叫 DashboardBotService.PauseEpicAsync / ResumeEpicAsync

### 規模 / 風險

**規模**：S-M（純 razor，無後端動）  
**風險**：低（後端就緒 + 純拼接 + Stage 45 風格範本可參考）

### 優先級

🟠 **中-高** — Trial_v5 觀察期已過，現在評估：
- Stage 47 = FF 四十（修 UI 補完整性）
- 或合併立 Stage 47 = FF 四十 + 四十一 + 四十二

### v4 重評估點

動態架構下 epic / Sub-Workflow 顯示方式可能整體重新設計（MS Agent Framework Sub-Workflow 內建顯示方式 vs 自己做 UI），Stage 50 後重評估。

---

## 四十八、Cody Dev_plan 階段 maxTurns 配置不足（複雜任務踩 100%）

> 狀態：🟠 中-高 — Trial_v5 揭露對複雜任務的硬限制
> 提出日期：2026-04-30（Trial_v5 揭露議題 E）
> v4 評估：v4 framework 下 maxTurns 機制改變

### 背景

Trial_v5 Cody Dev_plan **第二輪重產時踩到 maxTurns=10**：

```
"subtype": "error_max_turns"
"num_turns": 11
"errors": ["Reached maximum number of turns (10)"]
"terminal_reason": "max_turns"
"duration_ms": 171895  (2 分 52 秒)
```

對「DesignPlan 10K 字 + 11 元件 + 30+ 操作點」這種複雜任務，10 turns 不夠 Cody 完整 grep + read + write 拆解。

Stage 16 紀錄寫過 `RunAsync maxTurns 40`（Petra 用），但 **Cody Dev_plan 用 10**。

### 影響

- 跨 N 元件（N ≥ 10）任務 Cody Dev_plan 100% 踩 maxTurns
- DevPlan 內容變失敗訊息覆蓋第一輪結果（FF 三十二子項 A 重產設計小缺陷）
- 雖然 FF 三十二子項 A 兜底機制 escalate 給 Christ，但 Cody 第一輪寫的 17K 字內容**永遠丟失**

### 修法方向

- **選項 A**：Cody Dev_plan maxTurns 從 10 提到 40（對齊 Petra）
- **選項 B**：Cody Dev_plan maxTurns 動態調整（依 DesignPlan 字數 / Issue 數調整）
- **選項 C**：FF 三十二子項 A 重產時保留前一版 DevPlan 到 DevPlanAppealLog（避免覆蓋丟失）

### 規模 / 風險

**規模**：S（單檔 ClaudeCodeService 配置改 / DevAgentService Dev_plan path）  
**風險**：低（提高上限不影響其他 Agent）

### 優先級

🟠 中-高 — 直接影響 Trial_v5+ 複雜任務的 Dev_plan 階段成功率，建議搭車 Stage 49

### v4 重評估點

v4 framework 下 maxTurns 設定機制可能改變（MS Agent Framework Workflow 用不同的 turn 概念），Stage 50 後重評估本 FF 是否仍適用。

---

> 此檔僅含 v4 後重評估的 FF。其他類型 FF 拆分如下：
> - **進行中 active 主清單** → [`Future_Feature.md`](Future_Feature.md)
> - **已完成項目摘要** → [`Future_Feature_completed.md`](Future_Feature_completed.md)
> - **冷凍 FF** → [`Future_Feature_frozen.md`](Future_Feature_frozen.md)
> - **v4 動態架構吸收 / framework 內建 / Trial 完成** → [`Future_Feature_archived_v4.md`](Future_Feature_archived_v4.md)
> - **變更紀錄** → [`Future_Feature_changelog.md`](Future_Feature_changelog.md)

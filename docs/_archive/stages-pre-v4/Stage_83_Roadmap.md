# Stage 83 Roadmap — WebUI 全砍重設計（3 大分區 + Home + Auth）

> **狀態：✅ 已完成（2026-05-21）**
> **文件版本：v2.0**（Forge 實作紀錄補完）
> 對應系統版本：v3.75.0（Stage 83 完成後）
> Stage 規模：**L+++**（Dashboard 11 頁全砍重做 / 預估 20-30 個新 .razor + razor.cs / DashboardService 重組 / **+1 Migration**（v5 補 Bug 4 ResultPrUrl）/ 0 燒 AiTeam 餘額 — 純 Blazor + EF Core / 0 LLM call）
> 觸發來源：v5.5 Phase 4 候選 — Trial_v26/v27 揭 AiTeam v5.5 + HITL + 業界 safety net 三層分工完整對齊業界主流 / Dashboard 仍 v4 結構未對齊
> 戰略意義：**「最後測驗」戰略節點** — 跑得順 + Dashboard 真的好用 → AiTeam v5.5 完整收口進 production 自然累積期；跑不順 / Dashboard 仍難用 / Forge 跑 L+++ 失控 → 真實評估「燒掉 vs 繼續」/ 不擺爛繼續

---

## 戰略脈絡

### 為什麼大砍重做

Dashboard 11 頁是 v3 / v4 hierarchical static 時代逐 Stage 累積的（Stage 18 Agent 狀態卡 / Stage 22 Token 守門 / Stage 28 互動中心 / Stage 33 老闆控制中心 / Stage 38 Provider Model 動態化 等），結構演進到 v5.5 dynamic orchestrator 上線後已不對齊：

- **Tasks 中心仍是 v4 TaskGroup/TaskItem/PipelineView 視角**（Stage 78c 雖砍 Pipeline framework 但 UI 留 legacy）— v5.5 真實核心是 PetraInbox → PetraSession → SubtaskPlan → BossInteraction HITL 卡片
- **Settings 散落 5 頁**（Agents / Rules / Projects / Settings / Tokens）— 22 Workflow Flag + Token + Agents + Talent + SkillPrompt + TalentPrompt 體系沒統一管理介面
- **Monitoring 散落**（Tokens / Home Agent 卡 / Deployments / 沒 System Health）— Token 統計沒視覺化（per-Talent / per-Skill / per-PetraSession 維度缺）
- **Office 頁 Stage 12 後一直 16 行 Coming Soon placeholder**

漸進改不如砍重做 — scope 全新範圍 / zero blast radius 既有功能（v4 entity 留 schema 不 drop / OpsAgent + Internal Deployment + Auth 留 active）。

### Christ「最後測驗」精神

對齊「不抱期待 + 真實跑一次看結果」戰略 — Christ 心理已半放棄。Aria 該務實協作不擴大情緒 / 跑完看結果再評估下一步。

### 對齊既有 v5.5 真實架構

- **接收層**：PetraInbox FIFO（Stage 75）+ retry/dead-letter（Stage 76）+ Attachments（Stage 79）
- **動態 orchestrator**：PetraSession + PetraSessionMessage（Stage 63B）+ replan（Stage 81）
- **Worker dispatch**：6 Talent / 6 Skill 兩層 Prompt（Stage 67/72/74）+ Claude Code CLI subprocess（Cody/Vera/Quinn/Sage）+ Petra LLM（Anthropic Sonnet 4.6 production active）
- **HITL**：BossInteraction（plan_confirm Stage 80 / replan_confirm Stage 81 / split_task_proposal / ceo_confirm / intervention）
- **跨 Session 記憶**：TaskMemory（per-PetraSession）+ TalentMemory（per-Talent）+ CeoMemory（Stage 69）
- **22 Workflow Flag**：WorkflowSettingsResolver 讀 app_settings DB / appsettings.json fallback

---

## 子項清單

### 子項 0：共用基礎建設 + Auth 留現狀（規模 S）

- `MainLayout.razor` 重設計：Drawer 改 3 分區 + Home + Auth 結構（NavMenu 砍 11 連結 → 4 連結 + Logout）
- MudBlazor theme：保留既有 wwwroot/css/app.css + JS theme switching
- `Login.razor` / `LogoutButton.razor` / ASP.NET Core Identity Auth 完全保留（不重做）
- `NavMenu.razor` 重寫 → 4 條 link：Home / Tasks / Settings / Monitoring

### 子項 1：Home 入口頁（規模 S）

- 速覽卡：Active PetraSession 數量 / Pending PetraInbox 數量 / Pending HITL 卡片數量 / 今日 Token 使用量
- 3 大分區跳轉按鈕
- 砍既有 AgentStatusCard + QuickCommandCard（或重做為 Home 速覽元件 — 看 Forge 評估）

### 子項 2：Tasks 分區（規模 L — 核心）

**頁面**：`Pages/Tasks/TaskHub.razor`（主頁 + 子 component 切換）

子 component：
- **HitlCardCenter**：BossInteraction Status='pending' 卡片清單（plan_confirm / replan_confirm / split_task_proposal / ceo_confirm / intervention 五類），點開 → 顯示 AvailableActionsJson + Approve/Reject/Modify 按鈕 → POST `/internal/...` 或直接 DB UPDATE（樂觀鎖）
- **ActiveSessions**：PetraSession Status='running'/'paused' 清單，點開 → 顯示 SubtaskPlan（從 PetraSessionMessages 用 `SubtaskPlanParser` 重建）+ chain progress + Vera/Quinn output + cost
- **PetraInboxQueue**：PetraInbox 全狀態（pending / running / completed / failed / dead）+ Attachments preview
- **History**：PetraSession Status='done'/'escalated'/'cancelled' 清單 + PR 連結 + cost + duration

### 子項 3：Settings 分區（規模 L — 散落 5 頁整合）

**頁面**：`Pages/Settings/SettingsHub.razor`（MudTabs 切 8 subtab）

8 subtab：
- **WorkflowFlags**：22 Workflow:* key 統一管理（toggle / 數值 / 描述 / 動態調 vs require restart 分類）
- **TokenGuard**：全域月限 / 全域單次請求上限 / per-agent 日限 / 月限
- **Agents**：AgentConfig CRUD（Provider/Model 動態調 — DB SoT）
- **Talents**：Talent CRUD + TalentSkill 多對多 assignment（動態加 Cody-2 / 對齊 Stage 67 baseline 6 instance）
- **SkillPrompts**：SkillPrompt 版本管理（IsActive flag 切 / 不刪舊版本 / per-Skill 6 record）
- **TalentPrompts**：TalentPrompt 版本管理（per-Talent / persona body / baseline 0 row Phase 3 才補）
- **RulesAndProjects**：Rules CRUD + Projects CRUD（既有 RuleManagement + ProjectManagement 邏輯整合）
- **MockMode**：MockMode 4 流程觸發（既有 /mock 指令 UI 版）

### 子項 4：Monitoring 分區（規模 M-L）

**頁面**：`Pages/Monitoring/MonitoringHub.razor`（MudTabs 切 4 subtab）

4 subtab：
- **TokenAnalytics**：Token 統計視覺化（MudChart）— 趨勢 / per-Talent / per-Skill / per-PetraSession / 警戒線（全域月限 80% / per-agent 日限 90%）
- **AgentStatus**：Agent 即時狀態卡（SignalR push — 既有 AgentStatusHub `/hubs/agent-status` `ReceiveAgentStatus` 接著用）
- **Deployments**：TaskItem WHERE AssignedAgent='Ops' 部署歷史（既有 DeploymentHistory.razor 邏輯遷移）
- **SystemHealth**：Bot 容器健康（既有 `/internal/health` 如有 / 沒則新建）+ DB 連線 + Discord 連線

`TokenLogDetail` drawer 元件：點 TokenAnalytics 任一 row → 開 drawer 顯示完整 TokenLog（Stage / Round / Model / Input/Output/Cache 細節）

### 子項 5：既有 11 頁砍 + redirect（規模 S）

- **完全砍**：`Pages/Office/TeamOffice.razor`（16 行 placeholder）
- **redirect 到新分區**：10 個既有頁 `@page` directive 改 `<Microsoft.AspNetCore.Components.NavigationManager>.NavigateTo("/新分區", forceLoad: false)`：
  - `/agents` → `/settings#agents`
  - `/projects` → `/settings#projects`
  - `/rules` → `/settings#rules`
  - `/settings` → `/settings`（舊頁砍 / 新 SettingsHub.razor 接 `@page "/settings"`）
  - `/tokens` → `/monitoring#tokens`
  - `/deployments` → `/monitoring#deployments`
  - `/tasks` → `/tasks`（舊頁砍 / 新 TaskHub.razor 接 `@page "/tasks"`）
  - `/interactions` → `/tasks#hitl`
  - `/` 既有 Home → 新 Home（重做）
- **保留**：`/login` + `/logout` + Auth 體系完全不動

### 子項 6：SignalR Hub wire 新分區（規模 S）

- `AgentStatusHub` 4 endpoint 留：ReceiveAgentStatus / ReceiveTaskUpdate / ReceiveTokenUpdate / ReceiveQueueUpdate
- 新分區重新 subscribe：Tasks subscribe ReceiveTaskUpdate + ReceiveQueueUpdate / Monitoring subscribe ReceiveAgentStatus + ReceiveTokenUpdate / Home subscribe 全部（速覽用）
- Bot 端 push 路徑不變（既有 InternalController + DashboardPushService）

### 子項 7：DashboardService / Repository 重組（規模 M）

- 既有 9 service 評估：
  - **保留 + rewire**：DashboardAppSettingsService / DashboardTokenService / InteractionRespondService（核心邏輯不變）
  - **重組**：DashboardTaskService（25KB 拆 — TaskHub 內 ActiveSessions / HitlCardCenter / PetraInboxQueue / History 各自分 service 或 Repository 直接讀）/ DashboardAgentService / DashboardProjectService / DashboardRuleService / DashboardBotService / DashboardCeoCommandService
  - **評估**：DashboardTaskService SemaphoreSlim 並發控制是否仍需要（既有為 v4 場景）
- Repository pattern 對齊既有 11 Repository（PetraSessionRepository / PetraInboxRepository / TokenRepository / BossInteractionRepository / TalentRepository / SkillPromptRepository 等）— 不新建 Repository / 直接用既有

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | v4 entity 留 schema 不 drop（TaskGroup/TaskItem/TaskLog 0 Migration drop）| OpsAgentService 4 method（MonitorDeploymentAsync / MonitorCiCdAsync / RunHealthCheckAsync / AlertAsync）+ Quartz HealthCheckJob + InternalController.RecordDeployment 還 active 寫 TaskItem + TaskLog / GitHub Actions self-hosted runner 部署紀錄走 RecordDeployment endpoint |
| 2 | Auth 完全保留不動（Login.razor + ASP.NET Core Identity + Localhost Bypass）| Auth 不在 3 分區內 / 是登入流程獨立 / 重做風險 0 收益 |
| 3 | Deployments 併入 Monitoring 分區（非 Settings）| 部署歷史是「看狀態」語義 / 對齊 Token 統計 + Agent 狀態 + System Health 一致 |
| 4 | Office 直接砍（16 行 placeholder）| Stage 12 後一直沒實作 / 砍最乾淨 |
| 5 | SubtaskPlan in-memory class → Dashboard 從 PetraSessionMessages 用 SubtaskPlanParser 重建 | SubtaskPlan 不是 DB entity（`src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs`）/ 持久層在 PetraSessionMessages content / SubtaskPlanParser 既有 parser 可重用 |
| 6 | HITL 卡片中心 = BossInteraction 五類統一 UI（plan_confirm / replan_confirm / split_task_proposal / ceo_confirm / intervention）| 對齊 Stage 28 樂觀鎖機制 + Stage 80 plan_confirm + Stage 81 replan_confirm 累積 |
| 7 | SkillPrompt + TalentPrompt 版本管理 = IsActive flag 切（不刪舊版本 / partial unique index 守同 SkillName 只一條 active）| 對齊 Stage 72 既有 versioning 設計 / audit trail 保留 |
| 8 | 22 Workflow Flag 動態調 vs require restart 分類 | 多數 flag 透過 WorkflowSettingsResolver 讀 app_settings DB / reload-cache 後 5 分鐘內生效 / 少數連線層 flag 需 restart（Discord / Postgres / GitHub 連線 docker-compose env / 不在 Workflow:* scope） |
| 9 | SignalR Hub 既有 4 endpoint 留 + 重 wire 新分區 subscribe 拓撲 | Bot 端 push 路徑零變動 / Dashboard 端只改 subscribe 點 / 風險最低 |
| 10 | Internal API 既有 X-Api-Key 機制保留 | Dashboard → Bot Internal API call pattern 不變 / `/internal/reload-cache` + `/internal/restart` + `/internal/deployment` + `/internal/tokens` 既有 endpoint 沿用 |
| 11 | 既有 9 DashboardService 部分保留部分重組 / 既有 11 Repository 全部沿用不新建 | 對齊「修根因 > 補丁」+「不重做能用的」紀律 / 重做 scope 只在「Service 對應分區語義」層 |
| 12 | DashboardTaskService 25KB SemaphoreSlim 並發控制砍（評估後）| v4 場景遺留（多 worker 並行更新）/ v5.5 dynamic orchestrator 場景不適用 / 大砍時順手砍 — Forge 自驗對齊 |

---

## 驗收情境

### Tasks 分區

1. **HITL 卡片中心**
   - 觸發：mock 一個 plan_confirm BossInteraction（Status='pending'）→ Dashboard 開 Tasks 分區
   - 驗證：HitlCardCenter 顯示該卡片 / 點開顯示 AvailableActionsJson 動作 / 點 Approve → SQL 看 BossInteraction.Status='responded' + ResponseAction='plan_approve'
   - 重複驗 replan_confirm（Stage 81）+ split_task_proposal + ceo_confirm + intervention 五類

2. **Active PetraSession 顯示**
   - 觸發：跑一個真實 task（或 mock）開 PetraSession Status='running'
   - 驗證：ActiveSessions 顯示該 session / 點開顯示 SubtaskPlan（SubtaskPlanParser 從 messages 重建 / 對齊 PetraOrchestratorServiceTests baseline）+ chain progress + 累積 cost

3. **PetraInbox 佇列顯示**
   - 觸發：送多 task 到 PetraInbox（不同 status：pending / running / completed / failed / dead）
   - 驗證：PetraInboxQueue 顯示全 status + Attachments preview（如有）

4. **History 列**
   - 觸發：完成的 PetraSession Status='done'/'escalated'/'cancelled'
   - 驗證：History 顯示 + PR 連結點開 GitHub + cost + duration

### Settings 分區

5. **WorkflowFlags toggle 生效**
   - 觸發：toggle `Workflow:UsePetraOrchestratorV5` flag → reload-cache
   - 驗證：5 分鐘內 / Bot log 顯示 cache 刷新 / 下次 task 走新 flag path（對齊 Stage 47 SoP）

6. **Token 守門編輯**
   - 觸發：編輯全域月限 / per-agent 日限 / 月限
   - 驗證：app_settings + agent_configs DB row 更新 / Bot Cache 5 分鐘內刷新

7. **Talent CRUD 動態加 Cody-2**
   - 觸發：Talents tab 新增 Talent Name='Cody-2' / TalentSkill 加 code_implementation
   - 驗證：Petra 下次 dispatch code_implementation 時 / DefaultTalentFactory 看到 Cody + Cody-2 兩個可選

8. **SkillPrompt 版本切換**
   - 觸發：SkillPrompt 編輯 code_implementation → 新版本（VersionNumber +1）→ 切 IsActive
   - 驗證：partial unique index 守同 SkillName 只一條 active / 下次 Cody dispatch 用新版 prompt / 舊版 row 保留

9. **TalentPrompt 編輯**
   - 觸發：TalentPrompts tab 新增 Cody persona body
   - 驗證：DB row 寫入 / Cody 下次 task 用新 persona

10. **Rules / Projects / Mock**
    - 對齊既有 RuleManagement / ProjectManagement / `/mock` 指令邏輯遷移

### Monitoring 分區

11. **Token 統計視覺化**
    - 觸發：開 TokenAnalytics tab
    - 驗證：顯示趨勢圖（MudChart）/ per-Talent / per-Skill / per-PetraSession 切換 / 警戒線（全域月限 80% / per-agent 日限 90%）

12. **Agent 即時狀態**
    - 觸發：Bot push agent status（既有 InternalController POST `/internal/agent-status`）
    - 驗證：AgentStatus tab 即時更新（SignalR `ReceiveAgentStatus` 接著用）

13. **Deployments 部署歷史**
    - 觸發：GitHub Actions self-hosted runner push 部署紀錄（既有 `/internal/deployment` endpoint）
    - 驗證：Deployments tab 顯示新 TaskItem（AssignedAgent='Ops'）

14. **System Health**
    - 觸發：開 SystemHealth tab
    - 驗證：Bot 容器健康 / DB 連線 / Discord 連線狀態（既有 /health endpoint 如有 / 沒則新建）

15. **TokenLog detail drawer**
    - 觸發：TokenAnalytics 點任一 row
    - 驗證：drawer 開啟 / 顯示 Stage / Round / Model / Input/Output/Cache tokens / cost

### Auth + Home + 既有頁砍

16. **Auth 登入流程不破**
    - 觸發：未登入訪問 → redirect /login / 輸入密碼 → 跳 Home / 點 Logout → 回 /login
    - 驗證：對齊 Stage 22 既有 Auth 行為

17. **Home 入口**
    - 觸發：登入後到 Home
    - 驗證：速覽卡顯示 Active PetraSession 數 / Pending PetraInbox 數 / Pending HITL 卡片數 / 今日 Token 使用量 / 3 分區跳轉按鈕

18. **既有頁 redirect**
    - 觸發：訪問 /agents / /projects / /rules / /tokens / /deployments / /interactions 等舊路徑
    - 驗證：redirect 到對應新分區（不破書籤 / 不 404）

19. **Office 砍**
    - 觸發：訪問 /office
    - 驗證：404 或 redirect 到 Home（看 Forge 評估）

---

## 技術約束

- MudBlazor 8.x（既有版本 / 不引入新 UI library / 對齊 `docs/conventions/mudblazor.md` 規範）
- InteractiveServer + SignalR（既有 / @rendermode InteractiveServer 不改）
- ASP.NET Core Identity（既有 / DashboardDbContext "identity" schema 不動）
- v4 entity（TaskGroup/TaskItem/TaskLog）schema 保留 / **0 Migration drop** / Forge 自驗對齊
- 0 燒 AiTeam 餘額（純 Blazor + EF Core / 0 LLM call / Aria + Forge session 走 Claude Code subscription）
- Internal API X-Api-Key 機制保留（既有 docker-compose env `InternalApiKey`）
- 既有 9 DashboardService 評估保留 / 11 Repository 全沿用不新建
- Forge context 估 ~500-700K Opus 1M + ultrathink（L+++ 規模 / 可能突破 model_effort 校準錨上界 ×4.93）

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| **L+++ 規模 Forge context 超 Stage 81** | Aria gate1 補 micro-spike 紀律生效 / Forge Plan Mode 階段分階段 commit / Forge healthy 偏離 plan 紀律延續 |
| **v4 entity 留 schema + Dashboard 不 reference 雙寫斷層** | Deployments 分區仍 reference TaskItem（部署紀錄）/ 其他 v4 entity Dashboard 端真實 0 read / OpsAgent + Internal Deployment Bot 端 write 不變 / 0 風險 |
| **SignalR Hub 重 wire** | 既有 4 endpoint 完全保留 / Bot 端 push path 不變 / Dashboard 端只改 subscribe 點 / 風險最低 |
| **DashboardTaskService 25KB 拆解風險** | 對齊 `docs/conventions/refactor-sop.md` 既有 SOP（Stage 34-36 FF 二十 4 怪物拆解累積） |
| **既有 9 service rewire 漏點** | Forge 自驗 V1-VN 多輪 + Aria gate1 Tier 2 build/test 驗 / 對齊 Stage 67 + 7 條延伸範圍 #5 紀律 |
| **HITL 卡片 5 類 UI 統一風險** | 對齊 Stage 28 BossInteraction 樂觀鎖既有實作 / Stage 80 plan_confirm + Stage 81 replan_confirm 已驗證 pattern 延伸 |
| **「最後測驗」失控** | Christ 明示「跑不順真實評估燒掉 vs 繼續 / 不擺爛繼續」/ Aria 不擴大情緒 / 結案後評估真實狀態 |

---

## 實作紀錄（v2.0 補 — 2026-05-21）

### 總覽

Stage 83 跨 **5 round 17 commit** 完成 — L+++ scope 真實高於 Aria 預估（Aria gate1 + 視覺驗共 4 輪揭 plan ↔ delivery gap + 15+ bug）。最終結果：4 大分區（Home / Tasks / Settings / Monitoring）+ Auth 保留 + Office 砍 + 7 redirect 不破書籤 + ResultPrUrl Migration 加（擴 plan 0 Migration 紀律）。

### 實作完成項目（依 7 子項 + Forge spike 議題）

| 子項 | 範圍 | 主要 commit |
|---|---|---|
| **0 共用基礎建設** | NavMenu 11 → 4 link（Home / 任務 / 設定 / 監控）+ MainLayout 0 動 + Auth 0 動 | `e8cd9bf` |
| **1 Home 入口** | 4 metric 速覽（Active session / Pending inbox / Pending HITL / 今日 Token）+ 3 分區跳轉 + QuickCommandCard reuse + SignalR 5 endpoint subscribe（v2 升） | `b05951b` / `e20c2b5` |
| **2 Tasks 分區** | TaskHub.razor MudTabs 4 tab（HITL / Active Session / PetraInbox / 歷史 + Session drawer + PR column v5 補）+ TaskCenter 砍 + PetraSessionRepository 4 新 method | `7fb360d` / `96dcd50` |
| **3 Settings 分區** | SettingsHub.razor MudTabs 8 subtab — WorkflowFlags 21 flag 議題 C1 完整（v1）+ 4 inline component reuse + 3 full CRUD（Talents / SkillPrompts / TalentPrompts）v2 補 | `455099e` / `4c57174` |
| **4 Monitoring 分區** | MonitoringHub.razor MudTabs 4 subtab — Token MudChart inline + per-PetraSession 切換 + 警戒線 + TokenLogDetail drawer + /internal/health endpoint v2 補 | `88d18be` / `4a9a954` |
| **5 既有 11 頁砍 + redirect** | Office + PipelineList + PipelineView 砍（v1）+ 7 redirect 全 page v2 補（既有 page 拿 @page directive 變純 component） | `204d52a` / `db57f66` |
| **6 SignalR Hub wire** | 5 endpoint 完整 wire（Home 5 / TaskHub 3 / MonitoringHub 2 / SettingsHub 0）— v1 併 1/2/4 commit / v2 獨立 commit 對齊 Aria 拍 | `e20c2b5` |
| **7 DashboardService 拆解** | DashboardTaskService 568 拆 DashboardInteractionQueryService + DashboardDeploymentService（議題 E1 + F3 IDbContextFactory pattern 對齊 Stage 80 修根因） | `79edfe5` |
| **Bug 4 補做（v5）** | PetraSession.ResultPrUrl Migration + PetraOrchestratorService FinalizeGitAsync prUrl → CompleteAsync 寫入 + TaskHub 歷史 tab PR link | `96dcd50` |

### 關鍵設計決策

**Christ Plan Mode 拍板 6 議題（A1/B1/C1/D/E1/F）**：
- **A1**：HitlCardCenter cover plan_confirm + replan_confirm 2 類完整 + 3 類 generic fallback（YAGNI / v3 揭 ceo_confirm AvailableActionsJson="[]" 補做 generic Approve/Reject 按鈕）
- **B1**：ActiveSessions SubtaskPlan 限 plan_confirm/replan_confirm pending 期間從 ContextJson parse（不擴 schema scope）
- **C1**：WorkflowFlags 21 flag toggle + Restart Bot 按鈕（修根因「真實全 require restart」+ 順手立 FF C2 候選 Stage 84+ AppSettingsService 5 min re-read）
- **D**：純事實校正（/system-settings route + /pipeline redirect 補）
- **E1**：DashboardTaskService 568 拆 2 service（DashboardSessionService 因 PetraSession query 已走 Repository inject 不需建 — Forge healthy 偏離 plan）
- **F3**：Forge spike grep 揭 SemaphoreSlim 是 Blazor circuit Scoped DbContext 並發限制（Stage 80 同類根因）→ 拆解後改 IDbContextFactory pattern（不是 v4 場景遺留 / Roadmap 假設錯）

**Bug 4 path A+ Forge spike 揭**（升級 Aria 推薦 path A）：`FinalizeGitAsync` line 194 既有真實 return `OpenPullRequestAsync` 的 prUrl 變數 — 直接傳 CompleteAsync 比 regex parse message **更乾淨** + 0 動 Cody worker + 0 動 Bot↔Petra 通訊 protocol。

### 驗收後修正（4 輪 + 15+ bug）

| 輪次 | 觸發 | 修正範圍 |
|---|---|---|
| **v2 補做（4 commit）** | Aria gate1 揭 plan ↔ delivery gap 大（implementation 階段 phased delivery 過度 trade-off） | 子項 3 Settings 8 subtab 完整 + 3 full CRUD inline / 子項 4 Monitoring 4 簡化補回 / 子項 5 7 頁 redirect 全做 / 子項 6 SignalR 5 endpoint 獨立 commit |
| **v3 視覺驗（5 commit 修 7 bug + 1 ops）** | Aria Chrome MCP 視覺驗揭 11 議題 | Bug 1 ceo_confirm 按鈕 / Bug 2 SkillPrompts template literal / Bug 3 Bot:InternalApiKey env / Bug 5 query talents 表 / Bug 6 DB DELETE dead talent_skills / Bug 7 GetEntryAssembly（修錯方向）/ Bug 8 column width |
| **v4 再揭（2 commit）** | Aria 揭 Bug 7 真實沒生效 + Bug 9 MudThemeProvider | Bug 7 真實 root cause Directory.Build.props（揭 commit `79edfe5` 騙人）/ Bug 9 MudProviders sync localStorage IsDarkMode |
| **v5 補 Bug 4（1 commit）** | Christ 拍板擴 plan 0 Migration 紀律補 PR 連結 | PetraSession.ResultPrUrl Migration + path A+ 寫入 + UI column |

### Mock 覆蓋情況

Stage 83 是純 WebUI 重設計 — **0 LLM call / 0 Mock scenario / 不適用 Phase 2 Mock 場景驗收 SOP**。Layer 1 自驗（容器健康 / endpoint / DB baseline / route HTTP）全綠 / Layer 2 Aria Chrome MCP 視覺驗收 + Christ 真實點擊 — 反饋 4 輪修正完整。

### 踩坑紀錄

1. **🔴 Path mangling bug 同類根因第 2 次累積**（commit↔diff 對不上）：
   - **NavMenu 子項 0**：Edit 寫到 `D:\Source Code\AI Team\src\...`（main repo path）而非 worktree path — 即時發現 + 自修
   - **Directory.Build.props 子項 7**：commit `79edfe5` 號稱 v3.74.0 → v3.75.0 + 3 處改動 / 真實 `git show 79edfe5 -- src/Directory.Build.props` **0 output** → file 沒改 → CI/CD build dll baked v3.74.0 → 視覺仍顯示 v3.74.0 → v3 Bug 7 GetEntryAssembly() 修錯方向 → v4 Bug 7 才揭真實
   - **Aria v4 立紀律**：commit 前必 `git diff --stat HEAD~1 HEAD` verify file 真實改動 vs commit message 描述對齊（v5 Bug 4 commit 嚴格守 ✓）

2. **🟡 規劃前 entity schema 漏 verify（同類根因第 N 次）**：
   - Bug 4 PetraSession 真實沒 ResultPrUrl 欄位（plan §子項 2 假設「History + PR 連結」/ Aria 規劃前未 grep entity schema）
   - Bug 5 agent_configs 表 v4 dead 10 Agent 還活著（Stage 78a 砍 v4 class 但 DB row 沒清 / silent regression）
   - Bug 6 talent_skills 含 dead skill（Stage 78a 修 DbSeeder 但 production row 沒清）
   - 議題 F SemaphoreSlim 真實是 Blazor circuit Scoped DbContext 並發限制（Roadmap §決策 #12 假設「v4 場景遺留」錯）
   - Bug 1 ceo_confirm AvailableActionsJson="[]" 空 array（v4 ceo_confirm 設計是 Discord embed button / Dashboard path 沒 actions JSON / 既有 schema 真實狀態 grep 才揭）

3. **🟡 既有 docker-compose env naming convention 漏 verify**：
   - Bug 3 我寫 `AgentSettings:InternalApiKey` 但 Dashboard env 真實是 `Bot__InternalApiKey`（Dashboard 視 Bot 為「外部 service」用 `Bot:` prefix）— 規劃前未 grep docker-compose Dashboard env

4. **🟡 implementation 階段過度 phased delivery trade-off**：
   - 子項 3 Settings 4 tab 用 link button（Aria 揭「link out 不是 inline 整合」）
   - 子項 4 Monitoring 4 簡化（MudChart inline / per-PetraSession / 警戒線 / /internal/health 全留 Stage 84+）
   - 子項 5 7 頁 redirect 0 做（只砍 Office + Pipeline）
   - 子項 6 SignalR 併 1/2/4 commit（不獨立 commit）
   - 真實主因：context budget 焦慮 + 把「Forge healthy 偏離 plan」紀律**用過頭** — Aria gate1 要求補做到 plan 100%

5. **🟡 既有設計問題 stage 內揭**：
   - MudThemeProvider 從未 sync localStorage（既有 `<MudThemeProvider />` 0 binding default light）— 既有 dark theme 只影響 wwwroot/css/app.css 變數 / 不影響 MudBlazor component / 既有 PipelineRedirect 也踩同 bug Christ 從未 notice
   - Stage 83 加大量 MudPaper/MudCard 後視覺對比變明顯 → Bug 9 揭 → v4 修根因

6. **🟢 Forge healthy 偏離 plan 紀律延伸**：
   - 議題 E1 拆 2 service 而非 Roadmap 拍板 3 service（DashboardSessionService 不建 — Repository 直接 inject 不需 service 層）
   - 議題 F3 IDbContextFactory pattern 而非 Roadmap 預設「砍 SemaphoreSlim」（修根因 Blazor circuit Scoped 並發限制）
   - Bug 4 path A+ 而非 Aria path A（FinalizeGitAsync 既有 return prUrl 不需 regex parse）

---

## 版本歷史

### v1.0 — 2026-05-21（Aria 建立）

- 觸發：Christ 拍板「Dashboard 全砍重做 / 一個大 Stage 跑不拆 / 最後測驗戰略節點」
- 範圍：11 頁全砍 → 3 大分區（Tasks / Settings / Monitoring）+ Home + Auth 獨立 + Office 砍
- 7 子項規劃 + 12 設計決策 + 19 驗收情境
- v4 entity 拍板留 schema 不 drop（OpsAgent + Internal Deployment 還 active）
- Aria 預估 Forge context ~500-700K Opus 1M + ultrathink / 0 燒 AiTeam 餘額（純 Blazor + EF Core）

### v2.0 — 2026-05-21（Forge 結案）

- 觸發：Stage 83 v1-v5 5 round 17 commit 全 push + Aria gate1 v4 通過 + Christ v5 拍板 Bug 4 補完
- 補「實作紀錄」章節（7 子項實作 + 6 關鍵設計決策 + 4 round 15+ bug 修正 + 6 踩坑紀錄）
- header 加狀態 ✅ 已完成 + 規模補正「+1 Migration」（v5 Bug 4 PetraSession.ResultPrUrl 擴 plan 0 Migration 紀律）
- **Stage 83 全 commit 鏈**（`e8cd9bf..96dcd50`）：
  - v1 主 plan：`e8cd9bf` / `b05951b` / `7fb360d` / `455099e` / `88d18be` / `204d52a` / `79edfe5`（7 commit / 子項 0-7）
  - v2 Aria 補做：`4c57174` / `4a9a954` / `db57f66` / `e20c2b5`（4 commit / 子項 3-6 完整實裝）
  - v3 視覺驗 11 議題：`3a50882` / `2384a65` / `eb3547a` / `02e0737` / `c734b7e`（5 commit / 修 7 bug + 1 ops）
  - v4 再揭：`1549877` / `b65cb3a`（2 commit / Bug 7 真實 root cause + Bug 9 修根因）
  - v5 補：`96dcd50`（1 commit / Bug 4 ResultPrUrl 完整實裝）
- **連續紀律生效**：commit↔diff 對齊紀律（Aria v4 新立）/ Forge healthy 偏離 plan 紀律 / 修根因 > 補丁 哲學貫穿 4 輪

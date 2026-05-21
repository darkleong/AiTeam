# Stage 83 Roadmap — WebUI 全砍重設計（3 大分區 + Home + Auth）

> 對應系統版本：v3.75.0（Stage 83 完成後）
> Stage 規模：**L+++**（Dashboard 11 頁全砍重做 / 預估 20-30 個新 .razor + razor.cs / DashboardService 重組 / 0 Migration / 0 燒 AiTeam 餘額 — 純 Blazor + EF Core / 0 LLM call）
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

## 版本歷史

### v1.0 — 2026-05-21（Aria 建立）

- 觸發：Christ 拍板「Dashboard 全砍重做 / 一個大 Stage 跑不拆 / 最後測驗戰略節點」
- 範圍：11 頁全砍 → 3 大分區（Tasks / Settings / Monitoring）+ Home + Auth 獨立 + Office 砍
- 7 子項規劃 + 12 設計決策 + 19 驗收情境
- v4 entity 拍板留 schema 不 drop（OpsAgent + Internal Deployment 還 active）
- Aria 預估 Forge context ~500-700K Opus 1M + ultrathink / 0 燒 AiTeam 餘額（純 Blazor + EF Core）

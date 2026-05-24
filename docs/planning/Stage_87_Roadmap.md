# Stage 87 Roadmap — Dashboard 改造收口 + v4 LLM 配置 SoT 統一

> **狀態：📋 規劃中**
> **文件版本：v1.0**
> 對應系統版本：v3.79.0（Stage 87 完成後 / minor bump）
> Stage 規模：**L+**（catch-all 三主軸合併 / Christ 偏好徹底版 / Stage 期間 0 燒 AiTeam 餘額）
> 觸發來源：Stage 85/86 Dashboard 改造大段落收尾 + FF v14.0 候選（nav 階層化 / Rules dead row 第二次處理）+ Christ 拍板 A 合併
> 戰略意義：v4 collapse 最後一塊殘留收乾（agent_configs 表 + AGENTS 分頁）/ Dashboard nav 從平層升階層 / Dashboard 改造三 Stage 大段落徹底收口

---

## 戰略脈絡

Stage 85 救火 + Stage 86 改造完成 11 條 Christ 痛點 cover。Stage 87 是同段落收口 Stage，三件事一起打包：

**v4 collapse 最後殘留**：Stage 78a/b/c 砍 v4 worker path 後，`agent_configs` 表保留下來變 Petra 的 LLM 配置 SoT（`LlmProviderFactory.Create("PM")` 走這個）。同時 `talents` 表是 v5.5 worker identity SoT。兩條配置路徑共存違反 v5.5 schema 單一性原則，本 Stage 把 Petra 配置遷到 `talents.Name="Petra"` row、砍掉整個 AgentConfig 表 + AGENTS 分頁。

**nav 階層化**：3 個 Hub 頁面（Monitoring / Tasks / Settings）目前共 15 個 sub-tab 平層擠在 `MudTabs` 內，無 URL 階層、無 SignalR 訂閱粒度。FF v12.1 候選的 nav 改造打包進來。

**Rules v4 row 收乾**：Stage 86 走 A 路線只動 UI fallback（「全域（v4 殘留）」label），實際 DB row 還留著。本 Stage 順手 DELETE。

---

## 子項清單

### A 軸：v4 LLM 配置 SoT 統一（AgentConfig → Talent）

#### A0：talents 表加 DailyTokenLimitK + MonthlyTokenLimitK 欄位（規模 XS）

新 Migration 加兩 nullable int 欄位到 `talents` 表，對齊 AgentConfig 既有欄位定義（`src/AiTeam.Data/Entities.cs:44-46`）。配合 Stage 74 既有三層 fallback chain（TalentSkill → Talent → appsettings）擴充到 TokenLimit。

#### A1：Petra LLM 配置 + Token Limit 資料遷移（規模 S）

Migration 寫資料遷移腳本：`UPDATE talents SET Provider=..., Model=..., DailyTokenLimitK=..., MonthlyTokenLimitK=... FROM agent_configs WHERE agent_configs.Name='PM' AND talents.Name='Petra'`。守 idempotency（多次跑同結果）。後置 DROP TABLE agent_configs 放同一 Migration 或下一個 Migration 拆都可（Forge 決定）。

#### A2：Bot 端 LLM lookup path 切讀 Talent（規模 M）

涉及檔案：
- `src/AiTeam.Bot/Services/AgentConfigCache.cs`（整檔重寫 → 改名 `TalentMetaCache` 或合併進 `TalentRepository`，cache key 從 "PM" 改成 "Petra"，return 維持四元組 tuple）
- `src/AiTeam.Bot/Agents/LlmProviderFactory.cs`（內部 lookup 改 Talent.Name）
- `src/AiTeam.Bot/Agents/TokenTrackingProvider.cs`（依賴 AgentConfigCache → 改新 cache）
- 既有 caller 點：`PetraTalentDispatchService.cs:90,144,609` 三處 `providerFactory.Create(PetraAgentName)`，常數 `PetraAgentName` 從 "PM" 改 "Petra"（grep 整檔確認唯一定義位置）

#### A3：Dashboard AGENTS 分頁砍 + TALENTS 分頁加 Token Limit UI（規模 S）

砍 `src/AiTeam.Dashboard/Components/Pages/Agents/`（整個資料夾 / AgentSettings.razor + .razor.cs）。SettingsHub.razor 內 AGENTS sub-tab 移除（剩 6 sub-tab，影響 B 軸 sub-page 拆分）。TALENTS 分頁加兩個 Token Limit input + Provider/Model 已有的編輯邏輯延伸到 Token Limit save。

#### A4：DTO / Internal API / DbSeeder / DbContext 連帶清理（規模 XS）

砍 `AgentConfigDto`、`AppDbContext.AgentConfigs DbSet`、`DbSeeder` 內 AgentConfig seed 邏輯、`Bot/Program.cs:188` 啟動讀 AgentConfigs 段、`InternalController` 相關 endpoint（cache invalidate）。TrustLevel / DiscordChannelId / IsActive / Description（v4 dead）一併消失，不需單獨遷移。

---

### B 軸：nav 階層化改造（FF v12.1）

#### B0：3 Hub 拆 14 sub-page + URL 階層 routing（規模 L）

15 sub-tab 扣掉 A3 砍掉的 AGENTS = 14 sub-page。每個 sub-page 獨立 `.razor` + `@page` 階層 URL：
- `/monitoring/{tokens|agents|deployments|health}`（4）
- `/tasks/{hitl|active|inbox|history}`（4）
- `/settings/{workflow|tokens|talents|skill-prompts|talent-prompts|rules}`（6）

3 個 Hub 頁面退化為 layout shell（剩 MudTabs navigation UI、實際內容由 child route 渲染）或整個砍掉改用 NavMenu 直接 deep link。Forge plan mode 選實作策略。

#### B1：SignalR 訂閱拓撲細粒度重對齊（規模 S）

現況：MonitoringHub 訂閱 `ReceiveAgentStatus` + `ReceiveTokenUpdate` 兩 event 觸發全頁 reload；TaskHub 訂閱 3 event。拆 sub-page 後改成 page-level 單 event 訂閱：
- `/monitoring/tokens` 只訂閱 `ReceiveTokenUpdate`
- `/monitoring/agents` 只訂閱 `ReceiveAgentStatus`
- `/tasks/hitl` 只訂閱 `ReceiveInteractionUpdate`
- 其他類推

對齊 `AgentStatusHub` 既有 6 event 常數（`src/AiTeam.Data/Hubs/AgentStatusHub.cs`）。

#### B2：NavMenu 階層化選單（規模 S）

`NavMenu.razor` 從 4 個平層 `MudNavLink` 改成 `MudNavGroup` 二層展開：3 Hub 為一級節點、14 sub-page 為二級節點。對齊 Stage 86 sidebar hover overlay + pin 機制（`IThemeService` pattern）。

---

### C 軸：Rules v4 殘留 row 收乾（規模 XS）

#### C0：DELETE v4 row + 砍 UI fallback 程式碼（規模 XS）

純資料 cleanup：`DELETE FROM rules WHERE AgentName IN ('Dev','Ops','Qa','Doc','Requirements','Reviewer','Release','Designer','PM')`（9 個 v4 角色，0 FK 反向引用）。Migration 可選（Forge 拍 / 對齊 Stage 85 「0 Migration 純 SQL cleanup」紀律繼承）。

UI 程式碼砍：
- `src/AiTeam.Dashboard/Components/Pages/Rules/RuleManagement.razor:49-57`（`isLegacy` 判斷整段 + 「全域（v4 殘留：xxx）」label 邏輯）
- `RuleManagement.razor.cs` 的 `GetAgentChipColor` fallback case（改回直接 `return Color.Default`）

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | AgentConfig 表 drop / Petra LLM 配置 SoT 改 Talent | v5.5 schema 單一性 / 砍 v4 最後殘留 |
| 2 | 不留兼容期 / 一次切換 | Petra 1 個 caller path（PetraTalentDispatchService 3 處）/ 切換成本低 |
| 3 | TrustLevel / DiscordChannelId / IsActive / Description 隨 AgentConfig 一起砍 | DashboardAgentService.cs L161,204 已標 v4 dead / Talent 也無此概念 |
| 4 | AgentConfigCache 改名 TalentMetaCache（或併 TalentRepository）/ 維持 sync API + TTL 5 分 | 對齊既有 cache pattern / 避免 LlmProviderFactory sync caller 改非同步 |
| 5 | 3 Hub 拆 14 sub-page + URL 階層 routing | 對齊 FF v12.1 / 拆 sub-page 後 SignalR 細粒度訂閱可行 |
| 6 | SignalR 訂閱粒度從「全頁多 event」改「sub-page 單 event」 | 減少冗餘重載 / 對齊 `AgentStatusHub` 6 event 設計 |
| 7 | NavMenu 用 MudNavGroup 二層展開 | MudBlazor 8.x 既有元件 / 對齊 mudblazor.md v1.6 既有紀律 |
| 8 | Rules v4 row 純 DELETE / Migration 選用 | 對齊 Stage 85「0 Migration 純 SQL cleanup」既有紀律 |
| 9 | AGENTS 分頁直接砍 / 不留 view-only mode | 表都砍了 / view-only mode 無意義 |
| 10 | TALENTS 分頁加 Token Limit UI（兩個 input + save） | Petra 既有月限機制（Stage 47）必須有 UI 維護點 |
| 11 | DiscordChannelId 欄位 grep 0 業務 caller → 砍除 0 風險 | entity 唯一 reference 是定義本身（`Entities.cs:38`）/ 不影響 Discord Bot 既有頻道路由（走 appsettings 不走 DB）|
| 12 | 對齊 Stage 85 IDbContextFactory pattern | 新增 sub-page DashboardService 走 IDbContextFactory 不 Scoped DbContext |

---

## 驗收情境

1. **AgentConfig 表 drop** — 觸發：本機 `dotnet ef database update` / 驗證：`\d agent_configs` 在 psql 回 `does not exist`
2. **Petra dispatch 成功讀 Talent** — 觸發：Dashboard 觸發 Petra Mock task / 驗證：Bot log 顯示 `LlmProviderFactory.Create("Petra")` + 對應 Provider/Model
3. **TokenGuard 抓 Talent 月限** — 觸發：DB 改 `talents.Name='Petra'.MonthlyTokenLimitK` 為小值 + 跑 Petra task / 驗證：TokenGuard 觸發 alert 含正確閾值
4. **AGENTS 分頁不存在** — 觸發：Dashboard 開 `/settings/agents` URL / 驗證：404 或 redirect 到 `/settings`
5. **TALENTS 分頁顯示 Token Limit 欄位** — 觸發：Dashboard 開 `/settings/talents` / 驗證：Petra row 顯示 DailyTokenLimitK + MonthlyTokenLimitK 兩 input + save 後 DB 落地
6. **NavMenu 二層階層展開** — 觸發：Dashboard sidebar hover / 驗證：3 Hub 一級節點展開後顯示對應 sub-page 條目
7. **`/monitoring/tokens` 直接 deep link** — 觸發：直接貼 URL / 驗證：Token 統計 sub-page 載入，無需先進 MonitoringHub 再切 tab
8. **`/tasks/hitl` 直接 deep link** — 觸發：同上 / 驗證：HITL 確認卡片 sub-page 載入
9. **SignalR 細粒度訂閱** — 觸發：Bot 推 `ReceiveTokenUpdate` event / 驗證：只 `/monitoring/tokens` 開啟的 circuit 收到更新，`/monitoring/agents` circuit 不受影響
10. **Rules v4 row 全 DELETE** — 觸發：Migration up 後 / 驗證：`SELECT COUNT(*) FROM rules WHERE AgentName IN ('Dev','Ops','Qa','Doc','Requirements','Reviewer','Release','Designer','PM')` 回 0
11. **Rules UI fallback 程式碼移除** — 觸發：Dashboard 開 `/settings/rules` / 驗證：`grep -r "v4 殘留" src/AiTeam.Dashboard/` 0 match
12. **`dotnet build AiTeam.slnx` 0 error 0 new warning** — 觸發：本機 build / 驗證：對齊 Stage 86 baseline warning count
13. **`dotnet test` 全綠** — 觸發：本機跑全 test / 驗證：既有 test 0 fail（Petra LLM 配置路徑改變需更新 test fixture）
14. **DbSeeder 啟動不報錯** — 觸發：新空 DB + 啟動 Bot / 驗證：Bot log 0 AgentConfig 相關 exception / talents 表 6 row seed 完整
15. **Petra 重跑 task 走新 cache path** — 觸發：Dashboard 觸發 Petra task 跑兩次 / 驗證：第二次走 TalentMetaCache 快取（log 顯示 cache hit）
16. **TALENTS 分頁編輯 Petra Provider/Model 即時生效** — 觸發：改 Provider/Model + save + 5 分鐘內觸發 Petra task / 驗證：新 Provider/Model 套用（cache TTL 5 分對齊既有設計）
17. **AGENTS 砍除後 SettingsHub 6 sub-tab 排版正常** — 觸發：Dashboard 開 `/settings` / 驗證：剩 6 sub-tab（B 軸後拆 sub-page 即無）/ 中途若 B 軸未完成需保 SettingsHub layout 正常

---

## 技術約束

- .NET 9 / MudBlazor 8.x / Aspire / EF Core
- 對齊 IDbContextFactory pattern（Stage 85 既有 / 5 DashboardServices 已切換）
- 新增 Migration 走標準流程：`dotnet ef migrations add Stage87XxxName --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`
- 對齊 mudblazor.md v1.6：MudNavGroup 用法、Theme 機制（IThemeService）
- 對齊 refactor-sop.md v1.6：UI 文字砍 grep 範圍含 `.razor.cs` / 結案必做清單 9 條
- 不引入新依賴（NavMenu / MudNavGroup / SignalR Hub method 皆既有）
- Cache TTL 5 分鐘對齊 AppSettingsService 既有 pattern

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| Petra dispatch 切 Talent lookup 漏改 caller / 跑 task 時抓不到配置 | grep `providerFactory.Create\(|AgentConfigCache\.Get\(|PetraAgentName` 全 src 列清單 + build error 守門 + 一個 fixture test cover Petra dispatch entry |
| 資料遷移 idempotency 違反 / 重跑 Migration 寫壞 | Migration 內 UPDATE 加 WHERE talents.Provider IS NULL OR DataMigration marker / 對齊既有 Stage 67 talents seed Migration pattern |
| DbSeeder 啟動順序：talents seed 必須在 Migration 跑完之後 | DbSeeder 已對齊既有 OnModelCreating + MigrateAsync 順序（`Bot/Program.cs:184`）/ 確認 talents.Name='Petra' row 在 DbSeeder 內已 seed 即可 |
| B 軸 sub-page 拆分牽動既有 component reuse / 漂移範圍 | Forge plan mode 先盤點 SettingsHub 7 sub-tab 內既有 component（subagent gate0 揭 3 個 inline component reuse）/ 拆分時保 component 不動 / 只動 page 結構 |
| SignalR 訂閱重構漏訂閱某 event / page 不更新 | 對齊 `AgentStatusHub` 6 event 常數逐 page 驗證 / 每個 sub-page Tier 1 fixture test cover「event in → UI 更新」 |
| NavMenu 二層展開跟 Stage 86 sidebar hover/pin 機制衝突 | gate0 verify `MainLayout.razor` 內 sidebar state binding + IThemeService event 拓撲不衝突 / Forge plan mode 必涵蓋此驗證 |

---

## 版本歷史

### v1.0 — 2026-05-24（Aria 建立）

- 觸發：Christ 拍板 Stage 87 = A 合併（三主軸 catch-all 徹底版）
- 範圍：v4 LLM 配置 SoT 收尾（AgentConfig → Talent 遷移 + 表 drop）+ nav 階層化（3 Hub 拆 14 sub-page + URL routing + SignalR 細粒度訂閱）+ Rules v4 row DELETE
- 6 維度 ultrathink 自審：
  - ① 架構 ✅ — v5.5 schema 單一性對齊 / Talent SoT 統一 / Stage 78a/b/c v4 collapse 段落收口
  - ② 邏輯 ✅ — Petra lookup path 改名 "PM"→"Petra" 對齊既有 Talent.Name baseline / TokenGuard 月限機制無斷裂
  - ③ 競態 ⚠️ — TalentMetaCache TTL 5 分內 Petra 配置改動有 5 分延遲（既有 AgentConfigCache 同行為 / 不算新風險）/ Internal API cache invalidate 路徑同步切過去
  - ④ 上下文 ✅ — Stage 78a/b/c v4 收口 + Stage 85 IDbContextFactory pattern + Stage 86 IThemeService pattern 全對齊
  - ⑤ 預留欄位 ✅ — talents 表加 TokenLimit 兩欄位採 nullable / 對齊 Stage 74 三層 fallback chain（TalentSkill → Talent → appsettings）後續可往 TalentSkill 層延伸
  - ⑥ 關鍵檔案 ✅ — 已列 `AgentConfigCache.cs` / `LlmProviderFactory.cs` / `TokenTrackingProvider.cs` / `PetraTalentDispatchService.cs:90,144,609` / `Bot/Program.cs:184,188` / `DbSeeder.cs:42-50` / `DashboardAgentService.cs` / `Components/Pages/Agents/` / `Components/Pages/Rules/RuleManagement.razor:49-57` / `Entities.cs:30-50,318-336` / `AppDbContext.cs` / `AgentStatusHub.cs` / `NavMenu.razor` / 3 Hub.razor

### v2.0 — YYYY-MM-DD（Forge 結案 / 結案後補）

實作紀錄段（v1.0 不寫 / Forge 結案時補）：總覽 + 實作項目 + 關鍵設計決策 + 驗收後修正輪次 + 踩坑紀錄 + Mock 覆蓋情況。

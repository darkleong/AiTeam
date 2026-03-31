# Stage 4 — Blazor Dashboard

> 所屬專案：AI 團隊實作總規劃
> 狀態：✅ 已完成（2026-03-31）
> 最後更新：2026-03-31

---

## 目標

建立視覺化管理介面，讓你可以即時監控所有 Agent 狀態、任務歷史、部署紀錄，並調整 Agent 信任等級。

---

## 交付項目

- [x] Blazor Server 應用程式（Blazor Web App + Interactive Server）
- [x] SignalR 即時推送 Agent 狀態
- [x] Telerik Grid 顯示任務歷史
- [x] Agent 設定介面（信任等級調整連結 Notion）
- [x] 專案總覽頁面
- [x] ASP.NET Core Identity 登入機制
- [x] Light / Dark 主題切換
- [ ] 多團隊 / 多專案切換檢視（留 Stage 5）
- [ ] 部署紀錄頁面（留 Stage 5）

---

## 頁面規劃

```
Dashboard
  ├── 首頁（總覽）
  │     ├── 所有 Agent 即時狀態卡片
  │     ├── 所有專案狀態摘要
  │     └── 最近任務與部署的快速摘要
  │
  ├── 任務中心
  │     ├── 任務歷史列表（Telerik Grid）
  │     └── 點擊任務 → 側邊展開詳細步驟 log
  │
  ├── 部署紀錄
  │     ├── 各專案的部署歷史
  │     └── 點擊部署 → 側邊展開詳細結果
  │
  ├── 專案管理
  │     ├── 所有專案列表
  │     └── 點擊專案 → 側邊展開 repo、技術棧、Agent 設定
  │
  └── Agent 設定
        ├── 各 Agent 信任等級調整
        └── 規則清單（連結到 Notion）
```

---

## 技術決策

| 項目 | 決定 |
|------|------|
| 框架 | Blazor Server |
| 即時更新 | SignalR（Agent 狀態變動立刻推送） |
| 資料表格 | Telerik Grid |
| 側邊詳細資訊 | Telerik Drawer / 自訂 Panel |
| 登入機制 | ASP.NET Core Identity |
| 資料來源 | PostgreSQL（任務、部署）+ Notion API（規則） |
| 主題色調 | 跟隨系統設定（Light / Dark）|
| 部署方式 | 獨立 Aspire 專案，與 Bot 分開部署 |

---

## 專案結構調整

Dashboard 獨立為一個 Aspire 專案，Solution 結構更新為：

```
AiTeam.sln
  ├── AiTeam.AppHost              ← Aspire 入口
  ├── AiTeam.ServiceDefaults      ← 共用設定
  ├── AiTeam.Bot                  ← Discord Bot 主程式
  ├── AiTeam.Dashboard            ← Blazor Server Dashboard（新增）
  ├── AiTeam.Agents               ← 各 Agent Prompt 與邏輯
  ├── AiTeam.Data                 ← EF Core，Bot 與 Dashboard 共用
  └── AiTeam.Shared               ← 共用 DTO、介面
```

`AiTeam.Data` 由 Bot 與 Dashboard 共用，不重複定義資料結構。

---

## 權限設計

| 角色 | 權限 |
|------|------|
| **Owner（你）** | 所有功能，包含 Agent 設定、信任等級調整 |
| **Viewer（未來）** | 只能看任務歷史、部署紀錄，不能調整設定 |

未來開放給同事或客戶查看進度，給 Viewer 帳號即可，不影響核心設定。

---

## 已確認機制

**信任等級同步：**
Dashboard 調整信任等級後，直接寫回 Notion API。Notion 是唯一來源，PostgreSQL 不存信任等級，避免兩邊資料不一致。

**SignalR Hub 設計（實作後修正）：**
Hub 定義在 `AiTeam.Data` 共用層，但 Bot 與 Dashboard 是不同 Process，無法共享 `IHubContext`。
實際架構改為：Bot → Dashboard HTTP API → `IHubContext` → 瀏覽器。

```
Bot
  └── DashboardPushService
        ├── POST /internal/agent-status        ← Agent 狀態變動
        └── POST /internal/agent-status/task   ← 任務狀態變動

Dashboard
  └── AgentStatusController（[Route("internal/agent-status")]）
        └── IHubContext<AgentStatusHub>.SendAsync(...)
              └── 瀏覽器 Home / TaskCenter 即時更新
```

---

## 辦公室頁面設計（Team Office）

### 互動方式
- 主畫面：3D 等角視角辦公室，每個 Agent 有獨立座位
- 點擊 Agent：右側滑出 Icon Based 風格的詳細資訊面板
- 面板內容：狀態、信任等級、成功率、Token 用量、近期任務

### 詳細資訊面板內容
- Agent 頭像、名稱、職責
- 目前狀態（忙碌/閒置/錯誤）與正在執行的任務
- 信任等級（Level 0~3）
- 今日 Token 用量進度條
- 完成任務數、成功率
- 近期任務列表

### 預留實作細節（實作前再細討）
- Agent 個性設定（名字、造型主題）
- 人物造型可替換（實作時提供選項）
- 人物依狀態有對應動畫（忙碌時打字、閒置時發呆、錯誤時冒汗）
- 辦公區之外加入休息區（Agent 閒置時移動到休息區）
- 其他互動元素（待實作前討論）

---

## 實作重點紀錄

### 專案架構

- `AiTeam.Dashboard` 為獨立 Blazor Web App（非純 Blazor Server），採 **Static SSR + Interactive Server 混用**模式
- `AiTeam.Data` 共用層負責 EF Core `AppDbContext`、Repository、Hub 定義；Dashboard 另有 `DashboardDbContext`（Identity 專用，`identity` schema）
- `AiTeam.Shared` 放跨專案共用 DTO 與 ViewModel（`AgentStatusViewModel`、`TaskUpdateViewModel` 等）

### Blazor Web App 關鍵陷阱

| 問題 | 原因 | 修法 |
|------|------|------|
| 登入 POST 回傳 HTTP 400 | `TelerikRootComponent` 強制所有子頁面進入 Interactive 模式，Static SSR form 無法運作 | Login 頁加 `@layout AuthLayout`（不含 Telerik）；登入 POST 改用 MVC `AccountController` |
| `blazor.server.js` 不支援 Static SSR form | Blazor Web App 需用 `blazor.web.js` | `App.razor` 改為 `blazor.web.js` |
| `UseAntiforgery()` 攔截 Controller POST | Blazor antiforgery middleware 與 MVC filter 衝突 | Login form 改為純 HTML form POST 到 `AccountController`，用 `[ValidateAntiForgeryToken]`（MVC filter） |
| `AddControllers()` 缺少 antiforgery filter | ViewFeatures 未載入 | 改用 `AddControllersWithViews()` |
| CS0104 `RouteAttribute` 衝突 | Blazor 與 MVC 各有 `RouteAttribute` | `using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;` |
| DbContext 併發錯誤 | `prerender: true`（預設）讓 `OnInitializedAsync` 與 Grid `OnRead` 同時執行 | 所有 Interactive 頁面加 `@rendermode @(new InteractiveServerRenderMode(prerender: false))` |

### ASP.NET Core Identity 陷阱

| 問題 | 原因 | 修法 |
|------|------|------|
| Identity migration 不存在 | Dashboard 是新專案，未執行 migration | 加 `Microsoft.EntityFrameworkCore.Design`，執行 `dotnet ef migrations add InitialIdentity` |
| User Secrets 未載入 | Aspire 預設以 `Production` 環境啟動，不載入 User Secrets | AppHost 加 `.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")` |
| 純數字密碼被拒絕 | `RequireLowercase=true`、`RequireDigit` 預設嚴格 | Identity options 放寬：`RequireLowercase=false`、`RequireUppercase=false`、`RequireNonAlphanumeric=false` |

### 主題切換陷阱

| 問題 | 原因 | 修法 |
|------|------|------|
| `@onclick` 無效 | `MainLayout` 在 Static SSR 模式，無 Blazor 互動能力 | 改用 inline `onclick` JS |
| CSS 未載入 | `app.UseStaticFiles()` 遺漏 | `Program.cs` 補上 |
| 換頁後主題重置 | Blazor Enhanced Navigation 替換 `<html>` 屬性 | `App.razor` 的 `<head>` 加 `MutationObserver`，監測 `data-theme` 被清除時從 `localStorage` 補回 |
| CSS 變數不生效 | `[data-theme="dark"]` 未加 `html` 前綴，無法從根節點 cascade | 改為 `html[data-theme="dark"]` |

### SignalR 即時推送陷阱

| 問題 | 原因 | 修法 |
|------|------|------|
| Bot 無法直接推送到 Hub | Bot 與 Dashboard 是不同 Process，無法共享 `IHubContext` | Bot 呼叫 Dashboard HTTP API，由 Dashboard controller 持有 `IHubContext` 並推送 |
| Bot 推送全部 404（靜默失敗） | Controller route `[Route("internal/[controller]")]` 解析為 `internal/AgentStatus`（無連字號），但 Bot 呼叫 `/internal/agent-status`（有連字號） | 改為明確路由 `[Route("internal/agent-status")]` |
| Aspire 跨服務 HTTP 解析失敗（靜默失敗） | Bot HttpClient 用 `http://aiteam-dashboard`，Aspire 找 `http` 端點；但 Dashboard 端點名稱是 `dashboard` | 改為 `http+dashboard://aiteam-dashboard`，明確指定 Aspire 端點名稱 |
| TaskCenter 不即時更新 | TaskCenter 只有 Grid `OnRead`，無 SignalR 訂閱 | 補上 `HubConnection`，`ReceiveTaskUpdate` 時呼叫 `_gridRef.Rebind()` |

### Aspire 設定

- Dashboard 端點：`.WithHttpEndpoint(port: 5051, name: "dashboard")`
- Bot 引用 Dashboard：`bot.WithReference(dashboard)`（Aspire 注入 `services__aiteam-dashboard__dashboard__0`）
- Bot HttpClient URL：`http+dashboard://aiteam-dashboard`（scheme+端點名稱格式）
- 兩個專案都需加 `.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")` 才能載入 User Secrets

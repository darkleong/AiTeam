# Stage 22 — Dashboard 存取分層 + Token 保護 + 頻道清理

> Stage：22
> 對應版本：v3.6.0
> 建立日期：2026-04-12
> 完成日期：2026-04-12
> 狀態：✅ 已完成
> 文件版本：v2.0

---

## 目標

1. **Dashboard 存取分層** — localhost 免登入（讓 Playwright / Claude Code 暢通），外部透過 Tailscale Funnel 強制登入
2. **Token 異常消耗保護** — 落實已存在但未 enforce 的 Token 限制，加入請求前估算與月費上限
3. **Discord #指令中心 頻道移除** — 清理 Stage 2 遺留的無用頻道

> 對應 Future Feature：九、七、四

---

## 背景說明

### Dashboard 存取分層

Playwright 測試每次卡在登入畫面，實作 Session 無法自行完成 UI 驗收。本機已有 Tailscale Funnel 提供公開 HTTPS 入口（`https://love-desktop.tailcd0255.ts.net`）。

**目前認證架構：**
- ASP.NET Core Identity + Cookie-based 認證
- Login 頁面：`Components/Pages/Auth/Login.razor` → POST `/account/login`
- `AccountController.cs`：`signInManager.PasswordSignInAsync()`
- `Routes.razor`：`CascadingAuthenticationState` + `AuthorizeRouteView` → 未登入重導到 `/login`
- Identity 資料存在 PostgreSQL（schema: `identity`）
- **目前沒有任何 IP / Origin 檢查的 middleware**

**目前 Docker port binding：**
```yaml
ports:
  - "5051:8080"  # 0.0.0.0 binding，同網段可直連
```

### Token 保護

曾發生 Agent 單次消耗 80 萬+ Token 的事故。目前 `appsettings.json` 已有 Token 限制設定但未實際 enforce：

```
全域：MonthlyTokenLimitK: 1000
Per-Agent：DailyTokenLimitK / MonthlyTokenLimitK（CEO 10K/200K、Dev 20K/400K 等）
```

`DashboardTokenService` 只負責查詢消耗量用於 Dashboard 顯示，Bot 端也未實際攔截超限呼叫。

### #指令中心 頻道

`#指令中心` 已被 `#victoria-ceo` 完全取代。目前程式碼中有以下引用：
- `DiscordSettings.cs:12` — `CommandCenter` 屬性
- `DiscordBotService.cs:64` — `EnsureChannelsAsync()` 確認頻道存在
- `WebhookController.cs:143` — Embed footer 硬編碼「請到 #指令中心 使用 /task 指令確認執行」

---

## 實作項目

### 項目一：Dashboard 存取分層（Future Feature 九）

#### 1-1. Localhost 免登入 Middleware

在 `Program.cs` 中加入自訂 middleware，置於 `UseAuthentication()` 之前。偵測到 localhost 請求時，自動以 admin 身份通過驗證，跳過登入流程：

```csharp
// 虛擬碼 — 實作 Session 參考
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress?.IsLoopback == true
        && !context.User.Identity?.IsAuthenticated == true)
    {
        // 自動以 admin ClaimsPrincipal 登入
        // 使用 signInManager 或直接設定 HttpContext.User
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
```

**注意事項：**
- 需考慮 Docker 容器內的 loopback 判斷 — 容器內收到的 `RemoteIpAddress` 可能是 Docker bridge IP，不是 `127.0.0.1`
- 建議同時檢查 `IPAddress.IsLoopback` 和 Docker bridge 網段（通常 `172.17.x.x`）
- 或者透過 `appsettings` 設定 `AllowAnonymousIPs` 清單，更靈活
- 此 middleware 只在免登入路徑生效，已登入的使用者不受影響

#### 1-2. Docker port binding 收緊

修改 `docker-compose.prod.yml`：

```yaml
# 改前
ports:
  - "5051:8080"

# 改後
ports:
  - "127.0.0.1:5051:8080"
```

外部流量只能透過 Tailscale Funnel（`https://love-desktop.tailcd0255.ts.net`）進入，Funnel 已設定 proxy 到 `http://127.0.0.1:5051`。

#### 1-3. 驗證安全架構

完成後的存取矩陣：

| 來源 | 路徑 | 需要登入 |
|------|------|---------|
| 本機 `localhost:5051` | 直連 | ❌ 自動通過 |
| Tailscale Funnel | `https://love-desktop...ts.net` → `127.0.0.1:5051` | ✅ 需要登入 |
| 同網段裝置 | 直連 `5051` | ❌ 連不進來（127.0.0.1 binding） |
| Playwright（本機 CI） | `localhost:5051` | ❌ 自動通過 |

> ⚠️ Tailscale Funnel 的請求經過 proxy 轉發，`RemoteIpAddress` 可能顯示為 Tailscale 的 IP 而非 loopback。這正好是我們要的效果 — Funnel 進來的請求不會被誤判為 localhost，仍需登入。但實作時需實際測試確認。

---

### 項目二：Token 異常消耗保護（Future Feature 七）

#### 2-1. 請求前 Token 估算攔截

在 `TokenTrackingProvider`（或 `ILlmProvider` 呼叫鏈）中，送出 API 請求前估算 prompt token 數：

```
估算公式：字元數 / 4 ≈ token 數（粗估，寧可高估）
```

超過單次閾值（建議預設 `50,000` tokens，可在 `appsettings.json` 設定）時：
- 拒絕送出請求
- 記錄 warning log（包含 Agent 名稱、估算 token 數、prompt 前 200 字元）
- 在 Discord 發出警報通知老闆

#### 2-2. Per-Agent 日限 / 月限 Enforce

目前 `DailyTokenLimitK` / `MonthlyTokenLimitK` 只存在 config，未實際檢查。需要在 LLM 呼叫前加入檢查：

```
呼叫前：查詢該 Agent 今日 / 本月累計 token 消耗
    ↓
超過 DailyTokenLimitK → 拒絕，log + Discord 通知
超過 MonthlyTokenLimitK → 拒絕，log + Discord 通知
    ↓
未超過 → 正常送出
```

查詢來源：現有的 `TokenUsage` 資料表（Dashboard 用於顯示的同一張表）。

#### 2-3. 全域月費上限

`AppSettings` 已有 `MonthlyTokenLimitK: 1000`。在 Token 監控服務中加入全域檢查：

- 當月所有 Agent 累計 token 超過全域上限 → 鎖定所有 LLM 呼叫
- Discord 通知老闆：「本月 Token 用量已達上限，所有 Agent 已暫停 LLM 呼叫」
- 老闆可透過 `/reload-rules` 或修改 config 提高上限後解鎖

#### 2-4. Dashboard 顯示

Token 監控頁面（已有）補充：
- 顯示各 Agent 的日限 / 月限 vs 目前消耗量（進度條或百分比）
- 全域月費上限 vs 目前消耗量
- 超限時顯示醒目警告

---

### 項目三：Discord #指令中心 頻道移除（Future Feature 四）

#### 3-1. 移除程式碼引用

| 檔案 | 行號 | 修改 |
|------|------|------|
| `DiscordSettings.cs` | :12 | 移除 `CommandCenter` 屬性 |
| `DiscordBotService.cs` | :64 | 移除 `EnsureChannelsAsync()` 中對 `CommandCenter` 的確認 |
| `WebhookController.cs` | :143 | 修改 Embed footer：「請到 #指令中心 使用 /task 指令確認執行」→ 移除或改為「請至 #victoria-ceo 確認」 |

#### 3-2. 確認無其他引用

搜尋 `指令中心` 和 `CommandCenter` 確認無遺漏。

#### 3-3. Discord 頻道刪除

> ⚠️ 此步驟需 Christ 手動在 Discord 執行（刪除 `#指令中心` 頻道）。Bot 沒有刪除頻道的權限，也不應該有。

---

## 實作順序建議

```
1. 項目三（#指令中心 移除）     ← 最簡單，先做完清場
2. 項目二（Token 保護）          ← 中等複雜度，獨立於 Dashboard 改動
3. 項目一（Dashboard 存取分層）  ← 最需要測試驗證的項目，放最後
```

---

## 驗收清單

### 項目一：Dashboard 存取分層

- [ ] `localhost:5051` 可直接進入 Dashboard，不出現登入頁面
- [ ] 透過 Tailscale Funnel（`https://love-desktop...ts.net`）存取時，需要登入
- [ ] `docker-compose.prod.yml` port binding 為 `127.0.0.1:5051:8080`
- [ ] 同網段其他裝置無法直連 `5051`
- [ ] Playwright 測試可完整執行不卡登入（自行執行 Playwright 驗證）

### 項目二：Token 保護

- [ ] 單次請求 token 估算超過閾值（50K）時被攔截，log 記錄 + Discord 通知
- [ ] Agent 日限超過時被攔截，log 記錄 + Discord 通知
- [ ] Agent 月限超過時被攔截，log 記錄 + Discord 通知
- [ ] 全域月限超過時所有 LLM 呼叫被鎖定，Discord 通知
- [ ] Dashboard Token 監控頁面顯示限額 vs 消耗量
- [ ] `dotnet build` 無新增 error

### 項目三：#指令中心 移除

- [ ] 搜尋全專案無 `指令中心` / `CommandCenter` 引用
- [ ] `dotnet build` 無新增 error
- [ ] Bot 啟動時不再檢查 `#指令中心` 頻道是否存在
- [ ] WebhookController Embed footer 已更新

### 整體

- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] git commit + push
- [ ] `.csproj` 版本更新為 `3.6.0`
- [ ] git tag `v3.6.0`

---

## 注意事項

1. **Docker 內 loopback 判斷**：容器內收到的請求 IP 不一定是 `127.0.0.1`，可能是 Docker bridge IP。實作時需在容器內實測 `RemoteIpAddress` 的實際值，再決定判斷邏輯。
2. **Tailscale Funnel 請求的 IP**：Funnel proxy 轉發的請求，其 `RemoteIpAddress` 應為 Tailscale 的 IP（非 loopback），但需實測確認。
3. **Token 估算精度**：字元數 / 4 只是粗估。中文字元的 token 比例較高（約 1 字 = 2-3 tokens），可後續微調。寧可高估（誤殺正常請求）也不要低估（漏掉異常請求）。
4. **#指令中心 刪除**：程式碼清理後需請 Christ 手動刪除 Discord 頻道。

---

## 實作紀錄

### 項目三：#指令中心 移除

完全按規劃執行，無意外：
- `DiscordSettings.cs`：移除 `CommandCenter` 屬性
- `DiscordBotService.cs`：移除 `EnsureChannelsAsync()` 中的頻道確認
- `WebhookController.cs`：footer 改為「請至 #victoria-ceo 頻道確認」
- `appsettings.json`：移除 `"CommandCenter": "指令中心"`
- `docker-compose.prod.yml`：移除 `Discord__Channels__CommandCenter` 環境變數

Discord 頻道實際刪除由 Christ 手動完成（Bot 無刪頻道權限）。

---

### 項目二：Token 異常消耗保護

#### 新增設定

`AgentSettings.cs` 加入 `SingleRequestTokenLimitK`（預設 50K），`appsettings.json` 與 `docker-compose.prod.yml` 同步更新。

#### 踩坑一：`AgentConfig` 命名空間衝突

`TokenTrackingProvider.cs` 和 `LlmProviderFactory.cs` 同時引用了：
- `AiTeam.Bot.Configuration.AgentConfig`（per-agent 設定 POCO）
- `AiTeam.Data.AgentConfig`（DB Entity）

導致 `CS0104 ambiguous` 編譯錯誤。解法：在兩個檔案頂部加入 C# type alias：

```csharp
using BotAgentSettings = AiTeam.Bot.Configuration.AgentSettings;
using BotAgentConfig = AiTeam.Bot.Configuration.AgentConfig;
```

#### 踩坑二：Dashboard 無法參照 Bot 的 AgentSettings

Dashboard 沒有也不應該參照 `AiTeam.Bot` 專案（會拉入 Discord.Net、Anthropic.SDK 等大量依賴）。解法：在 Dashboard 建立輕量 POCO 類別：

**`src/AiTeam.Dashboard/Configuration/AgentTokenLimits.cs`**
```csharp
public class AgentTokenLimits
{
    public int MonthlyTokenLimitK { get; set; } = 1000;
    public Dictionary<string, AgentLimit> Agents { get; set; } = new();
}

public class AgentLimit
{
    public int DailyTokenLimitK { get; set; }
    public int MonthlyTokenLimitK { get; set; }
}
```

`Program.cs` 加入：
```csharp
builder.Services.Configure<AgentTokenLimits>(builder.Configuration.GetSection("AgentSettings"));
```

Dashboard 的 `appsettings.json` 另行維護一份 `AgentSettings` 區段（與 Bot 同步）。兩邊獨立維護，限額改動頻率極低，成本可接受。

#### Token 守門架構

`TokenTrackingProvider.CompleteAsync()` 在呼叫 `inner.CompleteAsync` 之前執行 4 道關卡（各自獨立檢查）：

```
估算 token = (systemPrompt.Length + userMessage.Length) / 4

1. 單次請求上限：估算 > SingleRequestTokenLimitK × 1000
2. Agent 日限：今日已用 + 估算 > AgentConfig.DailyTokenLimitK × 1000
3. Agent 月限：本月已用 + 估算 > AgentConfig.MonthlyTokenLimitK × 1000
4. 全域月限：全部 Agent 本月已用 + 估算 > global MonthlyTokenLimitK × 1000
```

每道關卡攔截時：`logger.LogWarning/Error` + Discord 警報（`DiscordAlertService`）+ 拋出 `InvalidOperationException`。

`TokenRepository` 新增三個查詢方法：`GetAgentDailyTotalAsync`、`GetAgentMonthlyTotalAsync`、`GetGlobalMonthlyTotalAsync`（均使用 UTC 時間範圍計算）。

`DiscordAlertService` 新建為 Singleton，透過 `DiscordSocketClient` 找到 Alerts 頻道發送警報訊息。

#### Dashboard Token 頁面增強

- 各 Agent 卡片：當 period = today 顯示日限進度條，period = month 顯示月限進度條，用量 ≥ 90% 時進度條改為紅色
- 合計卡片：period = month 顯示全域月限進度條與百分比，≥ 90% 時出現 `MudAlert Severity.Error` 醒目警告

---

### 項目一：Dashboard 存取分層

#### 踩坑三：Docker bridge IP 無法區分 localhost vs Tailscale Funnel

**問題**：Docker 容器內收到的 `RemoteIpAddress` 是 Docker bridge gateway（`172.19.0.1`），而非原始客戶端 IP。問題在於：本機 `localhost:5051` 的請求和 Tailscale Funnel（`https://love-desktop.tailcd0255.ts.net` → proxy 到 `127.0.0.1:5051`）的請求，在容器端看到的 `RemoteIpAddress` 都是同一個 `172.19.0.1`。

曾嘗試的解法：
1. 檢查 `IPAddress.IsLoopback`（`127.0.0.1` / `::1`）→ 完全無效（容器看不到）
2. 加入 `LocalhostBypass:TrustedIPs` 設定 + 允許 `172.19.0.1` → localhost bypass 生效，但 Tailscale Funnel 也一起繞過登入（安全漏洞）
3. 允許 Docker CIDR（`172.16.0.0/12`）→ 同上問題

**最終解法：改用 Host header 區分**

Host header 由客戶端或 proxy 設定，不受 Docker NAT 影響：
- `localhost:5051` 請求：`Host: localhost:5051`（或 `localhost`）
- Tailscale Funnel 請求：`Host: love-desktop.tailcd0255.ts.net`

```csharp
var host = context.Request.Host.Host;
var isLocalhost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || host == "127.0.0.1"
               || host == "::1";
```

安全性：port binding 已收緊為 `127.0.0.1:5051:8080`，外部裝置無法直連 port 5051。能進到容器的唯一兩條路徑是：(1) 本機直連（Host: localhost），(2) Tailscale Funnel proxy（Host: love-desktop...）。Host header 可準確區分兩者。

#### 最終存取矩陣

| 來源 | Host Header | 結果 |
|------|-------------|------|
| 本機 `localhost:5051` | `localhost` | ✅ 自動通過（免登入）|
| Tailscale Funnel | `love-desktop.tailcd0255.ts.net` | ✅ 需要登入 |
| 同網段其他裝置 | — | ❌ port 不對外開放 |
| Playwright（本機） | `localhost` | ✅ 自動通過 |

Middleware 放置位置：`UseAuthentication()` 之後、`UseAuthorization()` 之前（Blazor AuthorizeRouteView 才能感知到已設定的 ClaimsPrincipal）。

---

### 版本更新

`AiTeam.Bot.csproj` 與 `AiTeam.Dashboard.csproj` 版本號均更新為 `3.6.0`。

---

### 驗收結果

- ✅ localhost:5051 直接進入 Dashboard，不出現登入頁面
- ✅ Tailscale Funnel 存取仍需登入（Christ 確認）
- ✅ Docker port binding 為 `127.0.0.1:5051:8080`
- ✅ Bot 啟動不再確認 `#指令中心` 頻道
- ✅ WebhookController footer 已更新
- ✅ Token 守門：超限時 log + Discord 警報 + 拒絕請求
- ✅ Dashboard Token 頁面顯示各 Agent 日/月限進度條與全域警告
- ✅ `dotnet build` 零 error
- ✅ `dotnet test` 通過

---

## 變更紀錄

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-12 | v1.0 | Aria 撰寫初版規劃書 |
| 2026-04-12 | v2.0 | 實作完成，補充完整實作紀錄與踩坑記錄 |

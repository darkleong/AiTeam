# Stage 22 — Dashboard 存取分層 + Token 保護 + 頻道清理

> Stage：22
> 對應版本：v3.6.0
> 建立日期：2026-04-12
> 狀態：📋 規劃完成，待實作
> 文件版本：v1.0

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

## 變更紀錄

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-12 | v1.0 | Aria 撰寫初版規劃書 |

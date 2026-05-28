# Aspire Service Discovery URL 格式

跨服務 HttpClient 呼叫時、URL scheme 必須對應 `WithHttpEndpoint` 的 `name` 參數。

## 規則

AppHost 給端點加 name 時：

```csharp
.WithHttpEndpoint(port: 5051, name: "dashboard")
```

則 Bot 的 HttpClient BaseAddress 必須用：

```csharp
new Uri("http+dashboard://aiteam-dashboard")
//          ↑ 端點名稱
```

而不是 `http://aiteam-dashboard`（那會找 `name: "http"` 的端點、找不到就 DNS 失敗）。

Aspire 注入的 env var 格式是：

```
services__aiteam-dashboard__dashboard__0=http://localhost:5051
```

## 預設端點

端點若無自訂 name（預設 `"http"`）、則 `http://servicename` 才正確。

## WHY（歷史脈絡）

曾經 Bot → Dashboard 的推送完全靜默失敗（DashboardPushService 只 log warning）、導致 SignalR 即時更新無效、花了多次排查才找到根本原因是 URL scheme 格式錯誤。

## 套用紀律

每次在 Aspire 中設定跨服務 HttpClient 時、確認 URL scheme 與 `WithHttpEndpoint` name 一致。

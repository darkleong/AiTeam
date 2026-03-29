# Stage 4 — Blazor Dashboard

> 所屬專案：AI 團隊實作總規劃  
> 狀態：🔄 規劃中  
> 最後更新：2026-03-29

---

## 目標

建立視覺化管理介面，讓你可以即時監控所有 Agent 狀態、任務歷史、部署紀錄，並調整 Agent 信任等級。

---

## 交付項目

- [ ] Blazor Server 應用程式
- [ ] SignalR 即時推送 Agent 狀態
- [ ] Telerik Grid 顯示任務歷史與部署紀錄
- [ ] Agent 信任等級調整介面
- [ ] 專案總覽頁面
- [ ] ASP.NET Core Identity 登入機制
- [ ] 多團隊 / 多專案切換檢視

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

**SignalR Hub 設計：**
Hub 定義在 `AiTeam.Data` 共用層，Bot 執行任務時推送狀態變動，Dashboard 訂閱接收並即時更新畫面，不需要跨服務 HTTP 呼叫。

```
AiTeam.Data
  └── Hubs/
        └── AgentStatusHub.cs  ← Bot 推送 / Dashboard 訂閱
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

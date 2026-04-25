# Personal Team 規劃

> 建立日期：2026-03-31  
> 說明：定義 Personal Team 的架構、Agent 職責與溝通方式。

---

## 願景

建立一個個人 AI 管家團隊，協助老闆（Christ）處理所有個人事務，讓生活更有效率、更有條理。

---

## 整體架構

### 第一階段：三個窗口（短期）

```
你（老闆）
    │
    ├── Software CEO   ← 工作相關（Software Team）
    │
    ├── Personal CEO   ← 個人事務總管
    │       ├── Secretary Agent  ← Notion / Email / 個人事件
    │       ├── Tracker Agent    ← 定時事件追蹤
    │       └── Research Agent   ← 深度研究報告
    │
    └── Home Agent     ← 家庭控制（快速指令，繞過 Personal CEO）
```

**三個你直接接觸的窗口：**

| 窗口 | 什麼時候用 |
|------|-----------|
| **Software CEO** | 工作相關：寫程式、部署、Bug 修復 |
| **Personal CEO** | 個人事務：Notion、Email、待辦、研究、追蹤 |
| **Home Agent** | 即時家庭控制：關燈、冷氣、場景模式 |

---

### 第二階段：總 CEO 統一窗口（長期）

```
你（老闆）
    ↓
總 CEO — Aria（策略顧問兼總指揮）
    │
    ├── Software CEO   ← Software Team
    │
    └── Personal CEO   ← Personal Team
            └── Home Agent（快速指令仍可直接）
```

**目標：** 你只需要跟一個窗口說話，Aria 負責判斷要轉給哪個 Team 處理。

> 參考：Future_Research.md 第七項（顧問 Agent 設計）

---

## Personal Team Agent 清單

| Agent | 名稱 | 職稱 | 狀態 |
|-------|------|------|------|
| **Personal CEO** | Nora | 個人事務總管 | ⏳ 待實作 |
| **Secretary** | Seki | 個人秘書 | ⏳ 待實作 |
| **Home** | Hana | 智能家居管家 | ⏳ 待實作 |
| **Tracker** | Tara | 事件追蹤員 | ⏳ 待實作 |
| **Research** | Rhea | 研究分析師 | ⏳ 待實作 |

---

## 各 Agent 職責速覽

### Personal CEO（Nora）
- 你的個人事務總窗口
- 接收指令、分派給對應 Agent
- 協調多個任務的優先順序

### Secretary Agent（Seki）
- **Notion 管理**：新增/修改/刪除紀錄、定時報告
- **Email 管理**：寄信/回覆/整理信箱（Gmail）
- **個人事件**：待辦事項/提醒/瑣碎事記錄/查詢

### Home Agent（Hana）
- 控制家中所有智能電器設備
- 透過 **Home Assistant** 統一管理各品牌設備
- 支援快速指令（繞過 Personal CEO）
- 支援場景模式與排程設定

### Tracker Agent（Tara）
- 設定追蹤主題
- 定時在網路上搜尋最新資訊
- 整理成摘要報告發送至 Discord

### Research Agent（Rhea）
- 設定研究主題或目標
- 深度蒐集與分析相關資料
- 產出完整的研究報告

---

## Home Agent 特殊設計

Home Agent 支援兩種使用方式：

**一般模式（透過 Personal CEO）：**
```
你：「幫我設定睡眠模式，並且明天早上 7 點自動開窗簾」
    ↓
Personal CEO 理解並分派給 Home Agent
    ↓
Home Agent 執行複合指令
```

**快速模式（直接指令）：**
```
/home 關燈
/home 冷氣 26
/home 睡眠模式
/home 全部關閉
```

---

## 智能家居整合架構

使用 **Home Assistant** 作為統一中介層，解決多平台設備分散的問題：

```
各種智能設備
  ├── Apple HomeKit 設備
  ├── Google Home 設備
  ├── 小米設備
  └── 其他品牌
      ↓
  Home Assistant（統一管理所有設備）
      ↓
  Home Agent（透過 Home Assistant API 控制）
```

優點：未來新增任何品牌設備，只需整合進 Home Assistant，Home Agent 不需要改動。

---

## Email 整合

- **服務**：Gmail
- **整合方式**：Gmail API（OAuth 2.0）
- **功能**：寄信、回覆、讀取、整理、標籤管理

---

## 待確認事項

- [ ] Personal Team 的溝通管道（Discord 獨立 Server / 現有 Server / 其他）
- [ ] Home Assistant 是否需要新架設
- [ ] Tracker Agent 的追蹤頻率設定方式
- [ ] Research Agent 的報告格式與儲存位置

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-03-31 | 初版建立 |

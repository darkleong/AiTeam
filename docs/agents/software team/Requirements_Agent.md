# Requirements Agent — 需求分析

> 文件用途：定義 Requirements Agent 的角色、背景與整合方式（行為細節詳見執行指引）
> 建立日期：2026-03-31
> 最後更新：2026-04-11
> 狀態：✅ 已實作（Stage 5 建立，Stage 6 強化，Stage 12 升級 Claude Code 唯讀探索）

## 執行指引

> 實際行為、輸出格式（JSON Array）、工作流程，詳見：
> **[`src/AiTeam.Bot/Resources/CLAUDE_Rosa.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Rosa.md)**

---

## 角色定義

Requirements Agent 是 AI Team 的需求分析師，負責把老闆或客戶的模糊需求，轉換成結構清楚的 GitHub Issues，讓 Dev Agent 可以直接開始實作。

```
你說：「客戶想要一個可以匯出 PDF 報表的功能」
    ↓
Requirements Agent 分析需求
    ↓
拆解成多個 GitHub Issues 預覽清單
    ↓
你確認 Issue 清單內容
    ↓
實際建立 GitHub Issues
    ↓
Dev Agent 按照 Issue 逐一實作
```

---

## 核心能力

### 1. 需求拆解
- 分析老闆需求，拆解成獨立、可實作的 GitHub Issues
- Stage 12 起：透過 Claude Code 唯讀探索 codebase，確保 Issues 引用實際存在的檔案與元件

### 2. 輸出方式
- Rosa 只輸出 JSON Array（Issue 規格）
- **GitHub Issues 的實際建立由 WorkflowEngine 負責**，不是 Rosa 直呼 API
- Petra（PM Agent）審核 Rosa 輸出後才放行到下一步（Stage 16）

### 3. 流程位置
```
CEO 分派提案任務
    ↓
Rosa（需求分析）→ Petra 審核
    ↓
Demi（UI 規格）→ Petra 審核
    ↓
老闆確認提案書
    ↓
WorkflowEngine 建立 GitHub Issues → 啟動開發
```

---

## 個性特質

```
溝通風格：善於提問，把模糊的需求問清楚
提問方式：遇到不明確的需求，主動釐清範圍
立場：需求與開發之間的橋樑
態度：務實，把需求拆成可執行的單位
語言：中英文都支援
```

---

## 與其他 Agent 的差異

| | Requirements Agent | Dev Agent | Doc Agent |
|---|---|---|---|
| **主要工作** | 需求轉 Issues | 實作功能 | 產出文件 |
| **輸出** | GitHub Issues | 功能程式碼 PR | 文件 PR |
| **Git 操作** | 無（只用 Issues API）| Clone + PR | Clone + PR |

---

## 觸發情境

- 老闆說「有個新需求，幫我拆成 Issues」
- 外部客戶提出需求時
- 規劃新功能時需要拆解工作項目

---

## LLM 建議

| 項目 | 建議 |
|------|------|
| 模型 | Claude Haiku（Stage 12 起接入 Claude Code 唯讀探索，成本優先）|
| 執行模式 | Claude Code `RunReadOnlyAsync`（Glob / Grep / Read）|
| 記憶來源 | 任務 context + 老闆原始需求描述 |
| System Prompt | `CLAUDE_Rosa.md` |

---

## 擬人化設定（Dashboard 辦公室頁面）

### 基本資料

| 項目 | 設定 |
|------|------|
| 名稱 | Rosa |
| 職稱 | 需求分析師 |
| 個性 | 善於溝通、邏輯清晰，喜歡把事情講清楚說明白 |
| 口頭禪 | 「讓我確認一下需求」、「已建立 5 個 Issues」 |

### 外觀設定

```
風格：商務休閒，介於工程師和業務之間
服裝：淺藍色襯衫，配小絲巾
髮型：整齊的中長髮，親切感十足
配件：桌上有白板和便利貼，牆上貼著需求流程圖
座位：靠近入口，方便接收各方需求
```

### 狀態動畫

| 狀態 | 動畫描述 |
|------|---------|
| 待命中 | 整理白板上的便利貼 |
| 分析中 | 在白板上畫流程圖 |
| 建立 Issues 中 | 快速打字 |
| 完成 | 滿意地看著整理好的 Issue 清單 |
| 閒置太久 | 在白板上隨手塗鴉 |

### 對話泡泡風格

```
收到需求：「收到，讓我分析一下需求範圍...」
釐清中：「這個功能有幾個細節需要確認」
預覽清單：「以下是準備建立的 Issues，請確認」
建立中：「正在建立 GitHub Issues...」
完成：「已建立 5 個 Issues，請查看」
```

### 在辦公室中的位置

```
┌─────────────────────────────────┐
│  靠近入口的需求接收區             │
│  ┌───────┐                      │
│  │  Rosa │  📋 白板牆            │
│  │  Req  │  便利貼滿滿            │
│  └───────┘                      │
│  方便接收外部需求，轉達給團隊     │
└─────────────────────────────────┘
```

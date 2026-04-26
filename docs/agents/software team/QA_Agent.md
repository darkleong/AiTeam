# QA Agent — 品質保證

> 文件用途：定義 QA Agent 的角色、背景與整合方式（行為細節詳見執行指引）
> 建立日期：2026-03-31
> 最後更新：2026-04-11
> 狀態：✅ 已實作（Stage 5 建立，Stage 6 強化，Stage 9 Playwright 加入，Stage 16 重構為 Claude Code session）

## 執行指引

> 實際行為、測試策略（xUnit / Playwright）、輸出路徑規則，詳見：
> **[`src/AiTeam.Bot/Resources/CLAUDE_Quinn.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Quinn.md)**

---

## 角色定義

QA Agent 是 AI Team 的品質保證工程師，負責為 Dev Agent 產出的程式碼撰寫自動化測試，確保功能正確、避免 regression。

```
Reviewer Agent（Vera）完成審查
    ↓
QA Agent（Quinn）分析 PR 變更
    ↓
產出測試檔案（直接寫入，不開新 PR）
    ↓
WorkflowEngine 繼續串行流程（→ Doc Agent）
```

---

## 核心能力

### 1. 雙軌測試策略（Stage 9 / Stage 16）
- **.cs 變更** → xUnit 單元測試（xUnit + NSubstitute + FluentAssertions）
- **.razor / .css 變更** → Playwright 視覺截圖測試（light / dark mode 各截一張）

### 2. 輸出方式（Stage 16 重構）
- 直接使用 Write 工具寫入測試檔案，**不另開測試 branch / PR**
- 輸出路徑：`tests/Generated/` 或 `src/AiTeam.Tests.Playwright/Generated/`
- 完成後執行 `dotnet build` 確認編譯，並回傳 JSON 摘要

---

## 個性特質

```
溝通風格：精確，說清楚測試了什麼、覆蓋了哪些情境
提問方式：不確定測試範圍時主動詢問
立場：品質守門員，不放過可能的問題
態度：嚴格但公平，找問題是為了讓產品更好
語言：中英文都支援，測試程式碼用英文
```

---

## 與其他 Agent 的差異

| | QA Agent | Dev Agent | Reviewer Agent |
|---|---|---|---|
| **主要工作** | 寫測試、驗證品質 | 寫功能程式碼 | 審查程式碼邏輯 |
| **觸發方式** | Dev 開 PR 後自動觸發 | CEO 分派 | CEO 分派 |
| **輸出** | 測試程式碼 PR | 功能程式碼 PR | Code Review 報告 |

---

## 觸發情境

- Dev Agent 開 PR 後自動觸發（未來）
- CEO 分派「幫 PR #xx 補測試」任務
- 老闆主動要求補測試

---

## LLM 建議

| 項目 | 建議 |
|------|------|
| 模型 | Claude Sonnet（需要理解程式邏輯）|
| 執行模式 | Claude Code session（Stage 16 重構）|
| 記憶來源 | 任務 context + PR changed files |
| System Prompt | `CLAUDE_Quinn.md` |

---

## 擬人化設定（Dashboard 辦公室頁面）

### 基本資料

| 項目 | 設定 |
|------|------|
| 名稱 | Quinn |
| 職稱 | 品質工程師 |
| 個性 | 龜毛、細心，對品質有強烈堅持 |
| 口頭禪 | 「這裡有個邊界情況沒測到」、「測試通過了」 |

### 外觀設定

```
風格：嚴謹、細心感
服裝：格紋上衣 + 卡其褲
髮型：綁包包頭，帶眼鏡
配件：桌上有測試報告、便利貼，螢幕顯示測試覆蓋率
座位：Dev Agent 旁邊，方便即時溝通
```

### 狀態動畫

| 狀態 | 動畫描述 |
|------|---------|
| 待命中 | 整理桌上的文件 |
| 分析中 | 仔細閱讀程式碼 |
| 撰寫測試中 | 快速打字 |
| 測試執行中 | 看著測試結果跑動 |
| 發現問題 | 皺眉，在便利貼上記錄 |
| 閒置太久 | 整理桌面，把便利貼排整齊 |

### 對話泡泡風格

```
收到任務：「收到，開始分析變更...」
撰寫中：「正在撰寫測試案例...」
完成：「測試 PR 已開啟，覆蓋率 87%」
發現問題：「發現 2 個邊界情況未覆蓋」
```

### 在辦公室中的位置

```
┌─────────────────────────────────┐
│  品質區                          │
│  ┌───────┐  ┌───────┐          │
│  │ Cody  │  │ Quinn │          │
│  │  Dev  │  │  QA   │          │
│  └───────┘  └───────┘          │
│  緊鄰 Dev，形成開發品質配對       │
└─────────────────────────────────┘
```

# Stage 11：Dev Agent 驅動 Claude Code

> 版本：v1.0
> 建立日期：2026-04-05
> 狀態：📋 規劃中

---

## 目標

讓 Cody（Dev Agent）從「Claude API 一次性產出」升級為「驅動 Claude Code 自主開發」。

這是整個 AiTeam 系統的地基——Agent 能不能寫出可用的程式碼，決定了後面所有流程的價值。Stage 10 驗收測試明確暴露了問題：相同的任務，Claude Code 能完成，Cody 卻產出大量錯誤（幻覺框架、build 不過、結構不對）。

**本 Stage 只做這一件事，把地基打穩。**

---

## 現況 vs 目標

### 目前（Claude API + LibGit2Sharp）

```
CEO → 把任務 + 有限 context 塞進 prompt → Claude API 一次性產出 → commit
```

| 能力 | 狀態 |
|------|------|
| 探索 codebase | ❌ 只能看 CEO 塞進 prompt 的有限 context |
| 驗證 build | ❌ 寫完就 commit，無法驗證 |
| 迭代修復 | ❌ 基本上一次性產出 |
| 工具 | ❌ 只有 LibGit2Sharp 寫檔 + commit |

### 目標（驅動 Claude Code）

```
CEO → 啟動 Claude Code session（帶入任務描述 + UI 規格 + Issues）
       → Claude Code 自己探索 repo、寫 code、build、test、修 error
       → 全部通過後 commit + 開 PR
```

| 能力 | 狀態 |
|------|------|
| 探索 codebase | ✅ Glob / Grep / Read 任意檔案 |
| 驗證 build | ✅ `dotnet build` → 看錯誤 → 修 → 再 build |
| 迭代修復 | ✅ 錯了就改，直到成功 |
| 工具 | ✅ 檔案系統、bash、搜尋、全套 CLI |

---

## 連帶效益

Cody 升級後，多項 Future Feature 問題自然減輕或消失：

| Future Feature | 影響 |
|---------------|------|
| 十二（Dev Agent 框架幻覺防護） | 可能不再需要 — Claude Code 能看到 csproj 依賴，不會亂引用 |
| 十四（Orchestrator 流程重構）fix loop | 大幅減輕 — Claude Code 自己迭代到 build 過才 commit |
| 十三（技術債）Dev fix loop PR number | 減輕 — Claude Code 自己管理 branch 和 PR |

---

## 待研究事項

以下問題需要在實作前先釐清：

### 1. Claude Code 的呼叫方式

Claude Code SDK 支援程式化呼叫。需確認：
- C# 如何呼叫（CLI subprocess vs SDK API）
- 輸入格式（任務描述、CLAUDE.md、repo 路徑）
- 輸出格式（commit hash、PR URL、執行結果）

### 2. Docker 容器環境

Bot 運行在 Docker 容器內，Claude Code 需要：
- Node.js runtime（Claude Code 本體）
- dotnet CLI（build / test 用）
- 檔案系統存取（repo 讀寫）
- 網路存取（GitHub API、Anthropic API）

需評估是否在 Bot 容器內安裝 Claude Code，或另起獨立容器。

### 3. Session 管理

- timeout 設定（開發任務可能跑數分鐘）
- 資源限制（記憶體、CPU）
- 異常處理（Claude Code crash、API 超時）
- 並行控制（多個 Dev 任務同時跑）

### 4. CLAUDE.md 客製化

需要為 Cody 的 Claude Code session 準備專用的 CLAUDE.md，內容包含：
- 專案結構與慣例
- 禁用框架清單
- commit 格式規範
- 測試要求（build 必須通過才能 commit）

---

## 實作範圍

### 要做的

- [ ] 研究 Claude Code SDK 呼叫方式，確認技術可行性
- [ ] 設計 `ClaudeCodeDevService`（取代目前的 `DevAgentService` 核心邏輯）
- [ ] Docker 環境配置（Claude Code 安裝與依賴）
- [ ] Cody 專用 CLAUDE.md 撰寫
- [ ] 與 Orchestrator 整合（TaskGroup 流程不變，只換 Dev 執行層）
- [ ] 驗收測試：用真實任務測試 Cody 的開發品質

### 不做的

- Orchestrator 流程重構（十四）→ Stage 11 之後再評估
- 其他 Agent 的 Claude Code 化 → 先穩定 Dev，再評估擴展
- CEO 分類補強（十五）→ 與 Stage 11 無關

---

## 驗收標準

1. Cody 能收到 CEO 派發的任務，自動啟動 Claude Code session
2. Claude Code 能探索 repo、寫 code、執行 `dotnet build`、自行修復錯誤
3. 最終產出 build 通過的 commit 並開 PR
4. 與現有 Orchestrator 流程（Vera 審查、QA 測試）正常銜接
5. 在 Docker 環境中穩定運行

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-05 | 初版建立，從 Future Feature 一 獨立為 Stage 11 規劃文件 |

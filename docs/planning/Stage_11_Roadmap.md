# Stage 11：Dev Agent 驅動 Claude Code

> 版本：v1.0
> 建立日期：2026-04-05
> 狀態：✅ 已完成（2026-04-05）

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

## 研究結論

### 1. Claude Code 的呼叫方式

採用 **CLI subprocess** 方式（Claude Code 無 C# SDK）：

```
claude -p "<prompt>" \
  --dangerously-skip-permissions \
  --output-format json \
  --max-turns 20 \
  --no-session-persistence \
  --model <model-from-config>
```

- `-p`：非互動模式，完成後退出
- `--output-format json`：stdout 為逐行 JSON，最後一行 `type="result"` 包含執行摘要
- `--dangerously-skip-permissions`：跳過所有確認提示（容器內隔離環境可接受）
- `--max-turns 20`：防止無限迭代
- `--no-session-persistence`：不寫入磁碟 session 紀錄
- `--model`：從 `appsettings.json` 讀取，不寫死

### 2. Docker 容器環境

決策：**在 Bot 容器內安裝 Claude Code**（不另起容器）。

- Runtime base image 從 `dotnet/aspnet:10.0` 改為 `dotnet/sdk:10.0`（Claude Code 需要 `dotnet build`）
- Node.js 22 透過 nodesource 安裝（`apt-get nodejs` 版本過舊，無法執行 Claude Code）
- 代價：image 約增加 400MB，為 Dev Agent 功能必要代價

### 3. Session 管理

- Timeout：30 分鐘（`CancellationTokenSource.CancelAfter`），超時拋出 `TimeoutException`
- API Key：透過 `ProcessStartInfo.Environment["ANTHROPIC_API_KEY"]` 注入，不暴露在 log
- 並行控制：目前依賴 Orchestrator 的 TaskGroup 機制，不另加鎖
- Git config：`ClaudeCodeService` 啟動前先執行 `git config user.name/email`（容器內可能缺少設定）

### 4. CLAUDE.md 客製化

已實作 `Resources/CLAUDE_CODY.md`，寫入 repo 根目錄作為 `CLAUDE.md`，包含：
- 身份定義、執行規則（不 commit/push）
- 技術棧：C# 14 / .NET 10、MudBlazor **8.x**
- 框架禁止清單（MudBlazor 9.x、Telerik、Radzen）
- 執行順序：先 `dotnet restore`，再 `dotnet build`
- 命名規範與專案結構說明

---

## 實作範圍

### 要做的

- [x] 研究 Claude Code SDK 呼叫方式，確認技術可行性
- [x] 設計 `ClaudeCodeService`（subprocess 封裝）+ 修改 `DevAgentService`（移除 `ApplyCodeChangesAsync`）
- [x] Docker 環境配置（Dockerfile 改 sdk:10.0 + Node.js 22 + claude CLI）
- [x] Cody 專用 CLAUDE.md 撰寫（`Resources/CLAUDE_CODY.md`）
- [x] 與 Orchestrator 整合（TaskGroup 流程不變，只換 Dev 執行層）
- [x] 驗收測試：用真實任務測試 Cody 的開發品質（PR #65 通過）

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

## 踩坑紀錄（驗收過程）

| 問題 | 原因 | 修復 |
|------|------|------|
| `--dangerously-skip-permissions cannot be used with root` | 容器預設以 root 執行，Claude Code 拒絕在 root 下使用此 flag | Dockerfile 加入 `appuser` 非 root 使用者，`USER appuser` 切換 |
| `CLAUDE.md` 出現在 PR diff | `DevAgentService` 寫入模板後未還原，`GitHubService` commit 時一併帶入 | `try/finally` 確保執行完後還原原始 `CLAUDE.md`（或刪除） |
| Claude Code 拒絕寫入檔案（`No changes; nothing to commit`） | `C:\AiTeam-Workspace` Windows 路徑在 Linux 容器內觸發 Claude Code 安全檢查 | `GitHub__WorkspacePath` 改為 `/tmp/aiteam-workspace` Linux 原生路徑 |

---

## 驗收結果

- **PR #65**：任務「在 Dashboard 中任務中心頁面移除重新整理按鈕」
- Claude Code 正確找到 `TaskCenter.razor`，刪除 1 行按鈕程式碼
- PR diff 乾淨：只有實際程式碼變更，無 `CLAUDE.md` 污染
- `dotnet build` 通過

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-05 | 初版建立，從 Future Feature 一 獨立為 Stage 11 規劃文件 |
| 2026-04-05 | 實作完成：ClaudeCodeService、DevAgentService 改寫、CLAUDE_CODY.md、Dockerfile、Program.cs 更新；待驗收測試 |
| 2026-04-05 | 驗收完成：修復三個踩坑（root 限制、CLAUDE.md 污染、Windows 路徑）；PR #65 通過，Stage 11 結案 |

# Stage 12：提案流程全面升級

> 版本：v1.0
> 建立日期：2026-04-05
> 狀態：📋 規劃中

---

## 目標

讓提案流程從「猜」變成「看」，從「各做各的」變成「有序協作」。

Stage 11 修好了流程的尾巴（Cody 能寫出好 code），Stage 12 修好源頭（Rosa / Demi 能產出好規格）。源頭的規格歪了，後面再強都沒用。

**本 Stage 包含三個項目，全部圍繞提案流程，改動集中在同一批檔案。**

---

## 現況問題總覽

以「老闆說：規劃管理頁面不好用，請重新設計」為例：

| 步驟 | 目前問題 |
|------|---------|
| 老闆附了截圖 | Victoria 看得到，但 Rosa / Demi 收不到圖片 |
| Rosa 拆 Issues | 只收到一句話，看不到 codebase，憑空拆解 |
| Demi 設計 UI | 只收到一句話，看不到現有頁面，憑空設計 |
| Rosa / Demi 並行 | 互相看不到對方產出，Issues 和 UI 規格可能矛盾 |
| Demi commit UI 規格 | 提案未核准就 commit，否決 / 調整時產生垃圾 commit |
| ✏️ 調整 | 沒帶第一版產出，每次從頭來過，不是疊代改良 |

---

## 一、Agent 唯讀探索能力（Future Feature 十八）

### 背景

Rosa / Demi / Vera / Sage 目前只收到文字輸入，完全看不到 codebase。「在讀完程式碼之前，不知道還需要讀什麼程式碼」是遞迴問題，靠事先塞 context 無法解決。

### 方案

複用 Stage 11 的 `ClaudeCodeService`，讓指定 Agent 以唯讀模式啟動 Claude Code：

```bash
claude -p "<prompt>" \
  --permission-mode dontAsk \
  --allowedTools "Glob,Grep,Read" \
  --output-format json \
  --max-turns 10
```

- 只開放 `Glob`、`Grep`、`Read` — 能看，不能改
- `--max-turns 10` — 唯讀探索輪數少，成本低
- 不需要 `dotnet build`，不需要 SDK image

### 需要實作的

- [ ] `ClaudeCodeService.RunAsync` 增加 `allowedTools` 參數（預設 null = 全部，傳入陣列 = 限制）
- [ ] `RequirementsAgentService` 改用 Claude Code 唯讀模式（探索 repo → 產出 Issues）
- [ ] `DesignerAgentService` 改用 Claude Code 唯讀模式（探索 repo → 產出 UI 規格）
- [ ] `ReviewerAgentService`（Vera）改用 Claude Code 唯讀模式（探索 repo → 理解影響範圍）
- [ ] `DocAgentService`（Sage）改用 Claude Code 唯讀模式（讀 PR changed files → 產出文件）
- [ ] 各 Agent 的 `CLAUDE_XXX.md` 模板撰寫（身份 + 職責 + 輸出格式）

### 連帶效果

- 十二（框架幻覺防護）不再需要 — Agent 能看到實際 csproj
- 十四問題二（Doc 猜路徑失敗）自動消失 — Sage 自己讀 PR changed files

---

## 二、提案流程重設計（Future Feature 十九）

### 背景

Rosa 和 Demi 並行呼叫，互看不到對方產出；圖片不傳遞；✏️ 調整不帶第一版結果。

### 需要實作的

**2-1. 圖片傳遞**
- [ ] `AnalyzeOnlyAsync` 增加 `images` 參數
- [ ] `GenerateDraftAsync` 增加 `images` 參數
- [ ] `ShowProposalAsync` 中把老闆附的圖片傳給 Rosa / Demi

**2-2. Rosa 先、Demi 後（並行改串行）**
- [ ] `ShowProposalAsync` 中改為：先 `await Rosa` → 再 `await Demi（帶入 Rosa 的 Issues）`
- [ ] Demi 的 prompt 中加入 Rosa 產出的 Issues 列表，確保 UI 規格涵蓋所有功能點

```
目前：Task.WhenAll(Rosa, Demi)  ← 並行
改為：var issues = await Rosa();
      var uiSpec = await Demi(issues);  ← 串行
```

**2-3. ✏️ 調整帶第一版產出**
- [ ] `PendingConfirmation` 新增欄位：`IssuesPreview`（string）、`UiSpecContent`（string）
- [ ] ✏️ 調整時，把第一版 Issues + UI 規格 + 老闆調整意見一併傳入 prompt
- [ ] Rosa / Demi 收到的是「修改」指令，不是「重做」指令

---

## 三、UI 規格改存 DB（Future Feature 十七）

### 背景

Demi 產出 UI 規格後立即 commit 到 GitHub，但提案尚未核准。否決 / 調整時產生垃圾 commit，Bot 重啟時孤立檔案無法清理。

### 需要實作的

- [ ] `TaskGroup` Entity 新增 `UiSpecContent`（text 欄位），取代 `UiSpecPath`
- [ ] EF Core Migration
- [ ] `DesignerAgentService` 不再呼叫 `GitHubService.CommitFileAsync()`，改為回傳 markdown 內容
- [ ] `CommandHandler` 提案 Embed 改用 Discord 附件（`new FileAttachment(stream, "ui-spec.md")`）
- [ ] `TaskGroupService.BuildTaskDescription()` 直接塞入 UI 規格全文，不再塞路徑
- [ ] Cody 開 PR 時，由 Claude Code 自行把 `docs/ui-specs/` 規格文件放進 PR

### 連帶效果

- 十（孤立檔案清理）整條不再需要
- 十四問題四（✏️ 調整刪舊規格）自動消失

---

## 不做的事

- Orchestrator 流程重構（十四）→ 提案後的流程，Stage 12 之後再評估
- CEO 分類補強（十五）→ 與提案流程改動無關
- CEO 文件記錄能力（十六）→ 好用但不影響核心流程
- Stage 10 技術債（十三）→ 重要但獨立，另外處理

---

## 驗收標準

1. 老闆附圖片說需求 → Rosa / Demi 都能解讀圖片內容
2. Rosa 的 Issues 引用了實際的檔案名稱、元件名稱（不是泛泛而談）
3. Demi 的 UI 規格基於現有頁面結構，且涵蓋 Rosa 所有 Issues
4. ✏️ 調整後，第二版是「基於第一版修改」而非「從頭來過」
5. 整個提案流程零 GitHub commit（UI 規格存 DB）
6. `dotnet build` 整個 solution 通過

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-05 | 初版建立，整合 Future Feature 十七、十八、十九 |

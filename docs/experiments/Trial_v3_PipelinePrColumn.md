# Self-Implement 試驗 v3 — 流程追蹤頁面 PR 欄位優化

> 日期：2026-04-25
> 觸發：[FF 二十七](../planning/Future_Feature.md)（Stage 39 FF 二十八 fix 後重做試驗）
> 任務：流程追蹤頁面 PR 欄位顯示優化（PR 編號 + Mock 不顯示超連結）
> WorkflowType：`tech_improvement`
> PR：[#109](https://github.com/darkleong/AiTeam/pull/109)
> 觀察者：Aria（Opus 4.7 1M）+ Christ
> 狀態：✅ 完成（**Top 1 部分驗證 / Top 2 預期行為 / Top 3 維持高品質**）

---

## 試驗目的（複習）

- 補 Trial_v2 沒驗到的 **Top 1 / Top 2**（Trial_v2 任務純 .razor 繞過 Vera）
- 用會動 .cs 的任務（razor + razor.cs）驗證 Stage 39（FF 二十八 + CLAUDE_Vera.md 擴充）的真實效果

---

## 任務需求（Christ 在 Discord 給 Victoria 的指令）

> Victoria，幫我針對 AiTeam 的 Dashboard 做一些修改：
> 在『流程追蹤』頁面中，PR 欄位的內容目前是顯示 PR，
> 請幫我改成直接顯示 PR 編號，例如：#108
> 還有，如果是 Mock 流程，就不需要顯示超連結了。

---

## 流程觀察 Checkpoints（精簡版，差異點為主）

### Checkpoint 1：Victoria CEO 決策確認 ⚠️

對照 Trial_v2：
- ✅ 找到正確檔案 + 行號 + 主動提 `ExtractPrNumber` helper + tech_improvement 分類
- ⚠️ **Mock 判斷用 `context.Title.Contains("[MOCK]")` 是 fragile**（pattern match）
- ⚠️ **Victoria 沒探索 codebase 看現有 Mock 標記方式、沒問 Christ** — Trial_v2 觀察點 2 重演

### Checkpoint 2：tech_improvement 流程的 ghost Dev task ⚠️ 新發現

按「Agent 執行確認」執行後，**任務列表多出一筆 `Dev (等待中, Dashboard 觸發)` 永遠 stuck**：

| 時間 | 事件 | TaskItem |
|------|------|---------|
| 13:24 | Christ 按確認執行 | 建立 **Dev (等待中, Dashboard 觸發)** ← orphan |
| 13:26 | Orchestrator 啟動 tech_improvement 流程 | 另起 **Dev_plan (執行中, Orchestrator)** |
| 13:29 | Dev_plan 完成 → Petra 審 → Dev 觸發 | 又建 **Dev (執行中, Orchestrator)** ← 真正在跑 |
| 13:24 那筆 | 永遠 stuck | **永遠不會被消化** |

**根因**：CEO 確認 → ShowDirectAgentConfirm 建初始 Dev TaskItem，但 tech_improvement workflow 的 Orchestrator 沒使用這筆 task，另起爐灶。可能是 Stage 25a/25b 設計遺漏。

### Checkpoint 3：Agent 執行確認訊息「即將由 Dev 執行」誤導 ⚠️

「Agent 執行確認」訊息寫「**即將由 Dev 執行**」，實際先跑的是 **Dev_plan**（Cody 在 Dev_plan 模式跑 2 分 35 秒才換 Dev）。FF 二十二 子項 B 額外案例。

### Checkpoint 4：Reviewer 啟動 ✅ FF 二十八完全成功

對照 Trial_v2 PR #108：
| | Trial_v2 | Trial_v3 |
|---|---|---|
| Vera 看到 | 無 .cs 略過 | **2 .cs / 2 .razor / 0 .css** |
| 結果 | ❌ 失敗 + skipped | **✅ 啟動 Claude Code Review session** |

**FF 二十八 修復 100% 生效**。

### Checkpoint 5：Vera review report ⭐ Top 1 / Top 2 試金石

實際輸出：
- 🟡 **1 個 Warning**：`ExtractPrNumber` 重複定義（PipelineList.razor.cs vs PipelineView.razor.cs L253）→ 建議抽 helper
- 🟢 **1 個 Info**：`Title.Contains("[MOCK]")` case-sensitive，建議 `OrdinalIgnoreCase`
- ❌ **0 個 Critical**

**亮點**：
- ⭐ **跨檔影響範圍分析**：注意到 `Home/Home.razor:60-62` 也有 `DevPrUrl` 顯示沒同步改（Vera 主動掃出來）
- ⭐ **fallback 行為描述準確**：「URL 末段非數字時靜默降級顯示 'PR'，不會崩潰，符合防禦性設計」
- ⭐ **慣例對齊觀察**：[MOCK] case-sensitive 與既有 PipelineView MOCK badge 邏輯一致 — 認可既有專案慣例

**未抓到的議題（嚴重，PR 帶進 production）**：
- ❌ **`<a target="_blank">` 缺 `rel="noopener"`**（tabnabbing 安全漏洞）
- ❌ **新 link 缺 `aria-label`**（a11y）
- ❌ **`Title.Contains("[MOCK]")` fragile pattern match**（只標 Info case-sensitive，沒升級為 fragile 設計議題）

### Checkpoint 6：Petra → 沒進 Appeal 迴圈 🟢 預期行為

1 Warning + 1 Info + 0 Critical → **Petra 直接放行進 QA**。**這是 FF 二十七 Top 2 預期行為**（「偏好放行」哲學維持）。

### Checkpoint 7：Quinn test ✅ Top 3 維持

- xUnit：`ExtractPrNumber` + `FormatDuration` / `IsCompleted` / `IsFailed` / `Theory + GetLogColor`（連既有 helper 也補測，**超出範圍 over-deliver**）
- Playwright：PR 欄位視覺截圖 + PipelineView Drawer
- `dotnet build` 0 Error

**Top 3 結論**：CLAUDE_QA.md 「測試品質優先於數量」原則持續被遵守。

---

## 試驗 v3 結果矩陣

| 維度 | Trial_v2 | Trial_v3 |
|------|---------|---------|
| Vera 審 razor | ❌ 略過 | ✅ 啟動 + 跨檔影響範圍分析 |
| **Top 1 a11y / 安全** | 沒驗到 | 🟡 **部分**（CLAUDE_Vera.md 漏寫 `<a>` 段）|
| **Top 2 Petra 寬鬆** | 沒驗到 | 🟢 **預期行為**（Warning 放行符合設計）|
| Top 3 Quinn 品質 | ✅ 高品質 | ✅ 維持 + over-deliver |
| 整體品質感受 | 沒被把關 | **比 Trial_v2 好但 CLAUDE_Vera.md 仍漏 a11y/安全** |

---

## 關鍵結論：CLAUDE_Vera.md 設計漏洞（Stage 39 沒涵蓋的）

Stage 39 我寫 CLAUDE_Vera.md a11y 段時，**列了** button / MudButton / MudSwitch / MudCheckBox / MudIconButton / img — **但沒列 `<a>` link**。Vera 是照 prompt 做事的，**沒寫到的議題就放行**。

Stage 39 也沒列：
- `target="_blank"` 安全慣例（`rel="noopener"` 必要）
- 業務邏輯用 string pattern match 應升 Warning（fragile）
- Victoria 上游 spec 設計選擇是否合理的 meta 判準

**這就是 Stage 37 self-implement 試驗品質低下感受的精確樣貌**——不是「Vera 失職」，是 **CLAUDE_Vera.md 的判準邊界沒涵蓋全部關鍵議題**。

---

## 後續行動清單

### 立即（試驗結案）

- [x] 寫 Trial_v3.md（本檔）
- [ ] 更新 FF 二十七：v3 結果 + 標相關觀察項
- [ ] **新增 FF 二十九**：CLAUDE_Vera.md 補強三處
  1. `<a target="_blank">` 缺 `rel="noopener"` → **Critical**（安全相關）
  2. `<a>` link 缺 `aria-label` → Warning
  3. 業務邏輯用 string pattern match → 升 Warning（fragile）
- [ ] FF 二十二 子項 B 補新案例（Agent 執行確認訊息誤導）
- [ ] **新增 FF 三十**：tech_improvement ghost Dev task（CEO 確認建的初始 Dev TaskItem 沒被 Orchestrator 使用）

### 下個 Stage 候選（Stage 40 推薦範圍）

**Stage 40：CLAUDE_Vera.md 補強 + PR #109 後續修補**
- 主菜：FF 二十九（補 a11y `<a>` 段 + 安全 + pattern match 判準）
- 搭車修：
  - PR #109 帶進的兩個遺漏（rel="noopener" + aria-label）
  - Vera 自己抓到但沒人修的：Home.razor:60-62 同步 + ExtractPrNumber helper 抽出
- 規模：S-M

### 試驗 v4 規劃

**Stage 40 完成後可考慮 Trial_v4**：用會涉及 a11y / 安全議題的任務（例如 form 表單、auth 相關），驗證 CLAUDE_Vera.md 補強是否生效。

---

## 對 self-implement 戰略的最終判斷

**系統能力分層**：

✅ **執行層健康**：
- Cody 寫 code 品質 OK（雖然 spec 太細沒空間自主，但仍 over-deliver 細節）
- Quinn 測試品質高（CLAUDE_QA.md 寫得到位）
- Petra 路由判斷符合「偏好放行」設計哲學

🟡 **審查層有結構性漏洞**：
- Vera 跨檔影響範圍分析能力強（CLAUDE_Vera.md 影響範圍段做得對）
- 但 CLAUDE_Vera.md 判準**邊界覆蓋不全**（漏 a11y `<a>` / `rel="noopener"` / pattern match）
- 「偏好放行」哲學讓設計議題天生不易被擋下

❌ **CEO 層的 Trial_v2 觀察點 2 持續存在**：
- Victoria 對歧義不主動追問
- 沒探索 codebase 看現有實作慣例
- 直接給 Cody 級精細 spec，壓縮探索空間

**戰略建議**：
1. **短期**（Stage 40）：補強 CLAUDE_Vera.md 判準邊界 — Trial_v4 補驗
2. **中期**：FF 二十五（self-implement prompt 守則）+ FF 二十二（Agent 命名一致性）一起做
3. **長期**：CLAUDE_*.md Dashboard 化（FF 十 Phase 2）等核心穩定後評估

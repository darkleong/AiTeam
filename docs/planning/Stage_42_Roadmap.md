# Stage 42：FF 三十二 prompt 補強類（C+D+F+G）— Self-implement 完整性閘門上半場

> 對應 Future Feature：FF 三十二（Self-implement 完整性閘門）— 子項 C / D / F / G
> 對應版本：v3.29.0
> 建立日期：2026-04-28
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：FF 三十二 七子項閘門中的「prompt 補強類」四子項（C / D / F / G）— 純動 `CLAUDE_*.md` + 重啟容器即生效的最輕量子項合一個 Stage。

四子項分別補強四個 Agent 的 prompt：

| 子項 | Agent | 補什麼 | 對應 Trial_v4 Bug |
|---|---|---|---|
| **C** | Petra | Warning→blocking 升級規則加「PR 範圍嚴重不符計劃書」 | Bug #9 🔴 |
| **D** | Vera | Server Circuit 斷線 = Critical 邊界更明確 | Bug #10 🟡 |
| **F** | Sage | 「無實作 / DevPlan 失敗」→ 不寫歸檔，回 escalate | Bug #12 🟡 |
| **G** | Cody | PR description 強制列 ✅/❌ Issue + `⚠️ ESCALATE_NEEDED` 標記 + Vera/Petra 配合判讀 | Trial_v4 第二維度盲點 🟡 |

**戰略意義**：Trial_v4 揭露三層品質迴圈（Vera + Petra + Quinn）擋得下「程式碼層面瑕疵」，但擋不下「self-implement 範圍縮水 + 流程斷裂」。本 Stage 補完「prompt 層」的閘門，與 Stage 43（Orchestrator 改動類，子項 A+B+E）合計七子項才能閉環。

**Trial_v5 前置條件之一**：FF 三十二 完整七子項（C+D+F+G+A+B+E）+ FF 三十三 + FF 三十四 全完成才能開 Trial_v5（重跑 FF 十六需求對照 Trial_v4）。

---

## 子項 C：CLAUDE_Petra.md 加「PR 範圍嚴重不符計劃書」升級規則

### 現況

[`src/AiTeam.Bot/Resources/CLAUDE_Petra.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Petra.md) 第 4 節「Vera 審查結果判斷」的 Warning→blocking 升級清單目前涵蓋程式碼層面議題：
- 重複邏輯 / 重複定義
- 硬編碼預設值
- 多份 config 分散維護
- 業務邏輯 string pattern match
- `target="_blank"` 缺 `rel="noopener"`

**Trial_v4 揭露的盲點**：Vera 標 Info「Phase 1 only / 9 元件未遷移」時 Petra 直接放行 — 升級清單沒涵蓋「PR 範圍嚴重不符計劃書」這類議題。

### 實作項目

**位置**：`CLAUDE_Petra.md` 第 4 節「Vera 審查結果判斷」的 Warning→blocking 升級清單。

**新增條目（暫定文案，由 Forge 對齊既有條列風格落筆）**：

> Vera 報告中明確指出「未完成計劃書多數 Issue」/「Phase X only」/「N 元件未遷移」/「PR 範圍 < 計劃書範圍」類議題（不論 Vera 自己標 Info / Warning / Critical）→ Petra 視為 blocking，要求 Cody 補齊或標記 `escalate` 給老闆確認是否分階段交付。

**配合子項 G 的判讀規則**（同節再補一條）：

> PR description 含 `⚠️ ESCALATE_NEEDED` 標記 → 直接 `escalate`（不走 fix loop，避免 Cody 重複自欺）。

### 不在範圍

- ❌ 改 Petra 機制設計（不升級為 Claude Code CLI 審 Vera report — Christ 2026-04-28 拍板採純 prompt 路線）
- ❌ 動 Petra 路由 Orchestrator 程式碼（純 prompt 改）

---

## 子項 D：CLAUDE_Vera.md 強化「Server Circuit 斷線必為 Critical」

### 現況

[`src/AiTeam.Bot/Resources/CLAUDE_Vera.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Vera.md) 第 74-78 行已有「Blazor 例外處理（唯二可能列為 Critical 的 razor 議題之一）」段：

> `@onclick` handler 內未處理可能拋例外的呼叫（如 `await DeleteAsync(...)` 沒 try-catch），且該例外會在 Server Circuit 內炸掉整個 connection → **Critical**

**Trial_v4 揭露的盲點**：Vera 在 PR #122 抓到 onUndo 沒包 try/catch「**透過 MudBlazor 元件事件鏈傳播至 Blazor Server Circuit，可能造成 circuit 斷線**」— 完全符合上述規則，但 Vera 自己保守標 Warning。**Vera 知道規則但實際應用時降級**。

根因推測：Vera 把「`@onclick` handler」狹義理解為直接寫在 element 上的 handler；MudBlazor 元件事件鏈（如 `OnClick="@HandleClick"` / `OnClose="@OnUndo"` 等間接 binding）她不確定是否屬於同一範疇 → 保守降級。

### 實作項目

**位置**：`CLAUDE_Vera.md` 第 74-78 行「Blazor 例外處理」段擴充。

**強化方向**（暫定，由 Forge 對齊既有風格落筆）：

1. **明確化「Server Circuit 斷線」判準的觸發條件**（不限於 `@onclick`）：
   - 任何 Blazor 事件 handler（`@onclick` / `@onchange` / MudBlazor 元件 `OnClick` / `OnClose` / `OnValueChanged` / 事件鏈中游 method 等）
   - 內部 `await` 可能拋例外的呼叫（DB / API / IO / 第三方 service）
   - 未包 try/catch 或無上層處理機制
   - **任一條符合 → Critical 不得降級為 Warning**

2. **加範例反面教材**：引用 PR #122 onUndo 案例 — 「即使透過 MudBlazor 元件事件鏈間接傳播，Server Circuit 行為相同 → Critical」。

3. **強化「不得降級」措辭**：第 78 行的「**關鍵**」段加一條 — 「Server Circuit 斷線可能性出現時，即使描述用詞保守（如『可能』『或許』），也必為 Critical」。

### 不在範圍

- ❌ 改 Vera 工具權限（不動 Bash / Read / Glob / Grep 設定）
- ❌ 改 Critical 三大類定義（會崩潰 / 安全 / 資源洩漏 — 維持原樣，只強化既有條目應用範圍）

---

## 子項 F：CLAUDE_Sage.md 加「無實作」escalate 規則

### 現況

[`src/AiTeam.Bot/Resources/CLAUDE_Sage.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Sage.md) 是純歸檔員 prompt — 「讀 prompt → 更新 CHANGELOG → 建立歸檔檔案」三步流程，**完全沒有 escalate 機制**。

**Trial_v4 揭露的盲點**：Doc 階段 Sage 看到 DevPlan = 「（計畫書產出失敗，請查看 log）」+ 實際 PR 只 41 行新 Service，但仍寫了歸檔（含錯的 PR URL `feature/110-...` + 「實作摘要：無實作說明」）。

根因：
1. Sage 沒任何「品質下限判準」— 收到什麼資料就寫什麼
2. 既有規則「若 implementation_note 為空，在歸檔中寫『無實作說明』」直接放行了不該寫的歸檔
3. PR URL 在當時用 hardcode 格式，沒從 `TaskGroup.DevPrUrl` 直接讀（**待 Forge 探索確認此議題是否已在後續 Stage 修過**；若已修則本子項只動 prompt 不動程式碼）

### 實作項目

**位置**：`CLAUDE_Sage.md`「重要原則」段或新增「品質下限判準」段。

**新增規則（暫定，由 Forge 對齊既有風格落筆）**：

1. **DevPlan 內容檢查**：若 `implementation_note` / `dev_plan` 為空、為「失敗訊息」（如包含「產出失敗」/「請查看 log」/「無實作說明」字樣）、或顯著少於合理歸檔所需內容 → **不寫歸檔，輸出 escalate JSON**：
   ```json
   {
     "decision": "escalate",
     "reason": "DevPlan / implementation_note 內容不足以歸檔（具體描述）",
     "evidence": "<引用收到的具體文字>"
   }
   ```

2. **PR URL 來源校正**：歸檔中 PR 連結必須從 prompt 中明確的 PR URL 欄位讀（如 `task_group.dev_pr_url`），**不得自行組裝 URL 格式**（避免 Trial_v4 的 `feature/110-...` 拼湊錯誤）。

3. **既有規則修訂**：原「若 implementation_note 為空，在歸檔中寫『無實作說明』」規則改寫 — 「若 implementation_note 為空 → escalate（不走歸檔路徑）」。

### 待 Forge 探索

- ✅ Sage prompt 收到的欄位實際 schema（從 SageAgentService.cs / DocAgentService.cs 對照確認）
- ✅ PR URL 在 prompt 是哪個欄位（`task_group.dev_pr_url` 還是其他）
- ✅ Sage 路由端是否已有 `escalate` 處理路徑（若無則本子項受限於子項 E 完成 — 但子項 E 在 Stage 43，本 Stage 先補 prompt，下游處理留待 Stage 43 自然涵蓋）

### 不在範圍

- ❌ 改 Sage 路由 Orchestrator（escalate 路由的下游處理留 Stage 43 / 既有 escalate 機制）
- ❌ 設計新 schema 欄位

---

## 子項 G：CLAUDE_Cody.md 加 PR 自我檢查 + Vera/Petra 配合判讀

### 現況

[`src/AiTeam.Bot/Resources/CLAUDE_Cody.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Cody.md) 已有「實作說明（Implementation Note）」段（第 76-99 行），但**沒有「PR 完成度自我檢查」機制**。

**Trial_v4 揭露的盲點**：Cody 自認 Dev 階段「完成」（Status=done + 產出 PR），但客觀上只做 12 個 Issue 中的 1 個。**沒有任何 Agent 把「Cody PR 範圍 vs DesignPlan 12 Issue」做客觀對照檢查**。

子項 C / D / F 都是「下游被動發現」，缺**「Cody 主動自我檢查」這條主動機制**。

### 實作項目

#### G-1：CLAUDE_Cody.md 補入「Dev 階段結束時的自我檢查（強制要求）」段

**位置**：`CLAUDE_Cody.md`，建議放在「實作說明（Implementation Note）」段之前或之後（由 Forge 評估更自然位置）。

**內容（FF 三十二 已寫定範本，由 Forge 落筆）**：

```markdown
## Dev 階段結束時的自我檢查（強制要求）

PR 提交前，**必須**在 PR description 列出：

### ✅ 已完成 Issue
- Issue #N1：<檔案 / commit hash>
- Issue #N2：<檔案 / commit hash>

### ❌ 未完成 Issue（含原因）
- Issue #N3：<理由 — 如：超出範圍 / 預期 Phase 2 處理 / 探索後發現不需要>

### 完成度判定
- 完成 X / Y Issue（X% 完成度）
- **若 < 80% 完成度 → 必須在 PR description 標記 `⚠️ ESCALATE_NEEDED`**

不允許「跳過自我檢查」/「列出但無理由」/「假裝全做」。
```

**門檻 80% 由 Christ 拍板**（範本即為定稿，不另議）。

#### G-2：CLAUDE_Vera.md 補一條 PR description 判讀規則

**位置**：`CLAUDE_Vera.md` 既有審查流程或重要原則段（由 Forge 評估自然位置）。

**內容**：

> 若 PR description 含 `⚠️ ESCALATE_NEEDED` 標記 → 報告中**必標 Critical 議題**（即使程式碼層面無瑕疵也要標一條 critical：「PR description 自承完成度 < 80%，要求老闆確認分階段交付」）。

#### G-3：CLAUDE_Petra.md 補一條（已含於子項 C 第二條）

第二條配合判讀規則已寫進子項 C：「PR description 含 `⚠️ ESCALATE_NEEDED` → 直接 `escalate`」 — 子項 G 不重複，但 Forge 實作時要確認三檔（Cody / Vera / Petra）措辭一致（同一個 marker `⚠️ ESCALATE_NEEDED` 不能寫成不同變體）。

### 不在範圍

- ❌ Cody 寫 PR description 的程式碼自動化（Cody 用 `gh pr edit` 自己寫 description，不需 Orchestrator 配合）
- ❌ 修改 Vera/Petra 的 escalate 路由（既有 escalate 機制涵蓋）

---

## 整體驗收原則

**本 Stage 純 prompt 補強，無 Bot Orchestrator / DB / UI 動作**。驗收三層：

### 第一層：靜態驗收（git diff + 重啟）

✅ 文字寫入正確、容器啟動 log 無 prompt parse 錯。

### 第二層：Mock 行為驗收

⚠️ **Mock Mode 不適合本 Stage**。理由：本子項全為 LLM 判斷品質類規則（Petra 看 Vera Info 升 blocking / Vera 看事件鏈拋例外標 Critical / Sage 看內容不足 escalate / Cody 寫 PR description 自我檢查）— Mock 跳過 LLM，**無法真實驗證 prompt 改動是否生效**（呼應 workflow_aria.md 第七節 #7 教訓）。

### 第三層：Trial_v5 真實流程驗收（留待 FF 三十二 全完成）

✅ Trial_v5 預期觀察清單第 3 / 4 / 6 / 7 項對照本 Stage 子項 C / D / F / G — **真實生效驗證留 Trial_v5**。

---

## 驗收情境

### A. 子項 C — CLAUDE_Petra.md 升級規則寫入

**驗收方式**：
1. `git diff src/AiTeam.Bot/Resources/CLAUDE_Petra.md` → 第 4 節 Warning→blocking 升級清單新增至少兩條：
   - 「PR 範圍嚴重不符計劃書」類議題（涵蓋「Phase X only」/「N 元件未遷移」用詞）
   - PR description `⚠️ ESCALATE_NEEDED` 標記 → escalate
2. `grep -n "ESCALATE_NEEDED" src/AiTeam.Bot/Resources/CLAUDE_Petra.md` → 命中
3. `grep -n "Phase\|未遷移\|範圍" src/AiTeam.Bot/Resources/CLAUDE_Petra.md` → 命中新條目
4. 重啟 Bot 容器 → 啟動 log 無 prompt template 錯誤

### B. 子項 D — CLAUDE_Vera.md Server Circuit Critical 邊界強化

**驗收方式**：
1. `git diff src/AiTeam.Bot/Resources/CLAUDE_Vera.md` → 「Blazor 例外處理」段（第 74-78 行附近）擴充
2. 新內容明確涵蓋「MudBlazor 元件事件鏈」/「OnClick / OnClose / OnValueChanged」等非 `@onclick` 直寫的觸發路徑
3. 含 PR #122 onUndo 反面教材引用（或同等具體案例）
4. `grep -n "Server Circuit\|事件鏈\|onUndo" src/AiTeam.Bot/Resources/CLAUDE_Vera.md` → 命中新增段
5. 「不得降級」措辭強化（描述保守用詞如「可能」也不豁免 Critical）
6. 重啟 Bot 容器 → 啟動 log 無 prompt template 錯誤

### C. 子項 F — CLAUDE_Sage.md escalate 規則寫入

**驗收方式**：
1. `git diff src/AiTeam.Bot/Resources/CLAUDE_Sage.md` → 新增「品質下限判準」段或「重要原則」段擴充
2. 規則明確涵蓋三項：① DevPlan 為空 / 失敗訊息 → escalate ② PR URL 從 `task_group.dev_pr_url` 直接讀（不 hardcode 拼湊）③ 既有「無實作說明」直接放行規則修訂為 escalate
3. escalate JSON schema 範本寫清楚（`decision` / `reason` / `evidence`）
4. `grep -n "escalate\|失敗訊息\|不寫歸檔" src/AiTeam.Bot/Resources/CLAUDE_Sage.md` → 命中新增段
5. 重啟 Bot 容器 → 啟動 log 無 prompt template 錯誤

### D. 子項 G — CLAUDE_Cody.md PR 自我檢查 + Vera 配合判讀

**驗收方式**：
1. `git diff src/AiTeam.Bot/Resources/CLAUDE_Cody.md` → 新增「Dev 階段結束時的自我檢查（強制要求）」段
2. 段內含 ✅/❌ Issue / 完成度判定 / `⚠️ ESCALATE_NEEDED` 標記 / 80% 門檻 / 三條禁止句
3. `git diff src/AiTeam.Bot/Resources/CLAUDE_Vera.md` → 額外新增 PR description `⚠️ ESCALATE_NEEDED` 判讀規則
4. **三檔 marker 一致性**：`grep -rn "ESCALATE_NEEDED" src/AiTeam.Bot/Resources/CLAUDE_*.md` → 在 Cody / Vera / Petra 三檔皆命中，**字面完全一致**（含全形 / 半形空白 / emoji）
5. 重啟 Bot 容器 → 啟動 log 無 prompt template 錯誤

### E. 整體一致性

**驗收方式**：
1. 四檔 `CLAUDE_*.md` 改動風格與既有 prompt 一致（不引入新章節結構，對齊現有條列風格）
2. 無新增不必要章節分隔線 / heading hierarchy 變動
3. 繁體中文措辭風格對齊（不混簡體 / 不混機翻語感）

### F. CI/CD 自動部署 + Christ 真實 PR 觀察（留待）

**驗收方式**：
1. push 後 GitHub Actions self-hosted runner 自動 rebuild Bot 容器
2. **真實生效驗證留 Trial_v5**（FF 三十二 全完成後重跑 FF 十六需求對照 Trial_v4）

---

## 技術約束 & 注意事項

1. **不動程式碼**：本 Stage 純 prompt 補強，**禁止動任何 `.cs` / `.razor` / `.csproj` / Migration**。如 Forge 探索發現 Sage PR URL hardcode 議題（子項 F-2）需動程式碼 → 標進實作紀錄但不在本 Stage 修，留 Stage 43 或獨立小修搭車。

2. **重啟容器即生效**：`CLAUDE_*.md` 是 prompt template，push → CI/CD rebuild Bot 容器後新 LLM call 自動讀新版本。

3. **Marker 字面一致性**：`⚠️ ESCALATE_NEEDED` 在三檔（Cody / Vera / Petra）出現時必須**字面完全一致**（含 emoji `⚠️` 與底線下劃線 `_NEEDED` 大小寫）。建議 Forge 用同一個變數定義或在 commit 前 `grep -rn "ESCALATE_NEEDED"` 比對。

4. **既有條列風格對齊**：每份 `CLAUDE_*.md` 有自己的條列 / heading hierarchy 風格（Petra 用 `### 1. / 2.` 子節 + `**xxx：**` / Vera 用 `### a11y（Warning）` / Sage 用 `## 標題`）— 不引入新風格，**對齊既有風格**。

5. **暫定文案 vs 由 Forge 落筆**：本計劃書給「方向 + 必含元素 + 反面教材」，**最終文案由 Forge 對齊既有 prompt 風格落筆**。Forge 落完後 Aria 在閘門一檢查時對照本計劃書確認無偏離。

6. **邊界覆蓋自查（呼應 workflow_aria 第七節 #8）**：四子項落筆時 Forge 須做「真實 PR 議題類型清單覆蓋率自查」— 80%+ 覆蓋率，剩 20% 給 Agent 自由探索。本計劃書已列關鍵議題類型清單（如子項 D 的事件鏈 / 子項 F 的 PR URL 拼湊），Forge 可額外補但不可漏。

7. **Mock Mode 不在驗收範圍**：本 Stage 純 LLM 判斷品質類補強，Mock 跳過 LLM 無法驗。真實生效驗證留 Trial_v5（呼應 workflow_aria 第七節 #7 教訓）。

---

## 版本

`v3.28.0 → v3.29.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Sonnet 200K + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **輕**（4 個 `CLAUDE_*.md` 共 ~480 行 ≈ 10K + 探索 PR #122 反面教材若有需要 5K + 開場 15K + grep 對照 5K ≈ 35K raw） |
| **邏輯複雜度** | **輕-中**（純 prompt 改寫但要對齊四檔風格 + marker 字面一致性 + 邊界覆蓋自查） |
| **風險代價** | **低**（無程式碼動作，最壞情況回滾 git revert） |
| **範本可用度** | **高**（FF 三十二 子項 G 已給範本；其他子項給「方向 + 反面教材」對齊既有風格落筆） |

**Context 粗估**：35K raw × 1.0 倍率（**Stage 41 校準後的「純 prompt 改寫類」分類**）= **~35-40K**。Sonnet 200K 充裕。

**選 Sonnet 200K + medium 理由**：
- 純 prompt 補強，無跨層架構推理
- 範本 / 反面教材 / 既有條列風格清楚
- 四子項性質一致，可一氣呵成
- 邊界覆蓋自查 + marker 一致性檢查屬於檢核工作，medium 足夠

**替代方案**：
- 若 Forge 探索後決定子項 F-2 順手修 PR URL hardcode（動程式碼）→ 拉 high effort 較保險（但本計劃書建議**不在本 Stage 修，留 Stage 43**）
- 若 Christ 偏好保守 → high effort 也可接受（成本差距小）

---

## 後續關聯

- **Stage 43 = FF 三十二 Orchestrator 改動類（A+B+E）**：本 Stage 完成後接續，動 DevPlan Appeal accept 重新產出 / Dev fix 失敗中止 fix loop / QA 失敗 TaskGroup 標 failed
- **Stage 44 = FF 三十三**（Token CLI 涵蓋）：可與 Stage 42-43 並行（性質不同無依賴）
- **Stage 45 = FF 三十四**（流程暫停）：Trial_v5 鎖死前置條件
- **Stage 46 = FF 三十五**（自動拆任務 ⭐ 戰略級）
- **Trial_v5**：Stage 42-46 全完成後執行（重跑 FF 十六需求對照 Trial_v4）
- **FF 三十六（v4 架構雙支柱研究 spike）**：⚪ 待觀察 — Trial_v5 結果若顯示 prompt 補強仍踩硬編碼根因 → 觸發

---

## 不在範圍

- ❌ FF 三十二 子項 A（DevPlan Appeal accept 後重新產出）→ Stage 43
- ❌ FF 三十二 子項 B（Dev fix 失敗中止 fix loop）→ Stage 43
- ❌ FF 三十二 子項 E（QA 失敗 TaskGroup 標 failed）→ Stage 43
- ❌ Sage PR URL hardcode 議題的程式碼修正（純 prompt 補規則，程式碼修正留 Stage 43 或搭車）
- ❌ Petra 升級為 Claude Code CLI 審 Vera report（Christ 2026-04-28 已拍板採純 prompt 路線，重評估留 FF 三十六候選）
- ❌ Trial_v5 試驗本身（Stage 42-46 全完成後執行）
- ❌ Mock Mode 行為驗證（純 LLM 判斷品質類，Mock 跳過 LLM 無法驗）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-28 | 計劃書建立（Aria）— FF 三十二 prompt 補強類四子項（C / D / F / G）合一 Stage |

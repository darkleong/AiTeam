# Self-Implement 試驗 v4 — Dashboard 錯誤處理 UX 打磨

> 日期：2026-04-27 ~ 2026-04-28
> 觸發：[FF 二十七](../planning/Future_Feature.md)（Stage 41 三層品質迴圈閉環後首次完整驗證）
> 任務：Dashboard 錯誤處理 UX 打磨（雙軌通知 MudAlert + Toast）
> WorkflowType：`new_feature`（Victoria 自動分類，**未走 tech_improvement**）
> PR：[#122](https://github.com/darkleong/AiTeam/pull/122)（**OPEN 未合併**）
> 觀察者：Aria（Opus 4.7 1M）+ Christ
> 送出路徑：**Dashboard 快速下達指令卡**（Trial 系列首次走 Dashboard 路徑，vs v2/v3 走 Discord）
> 狀態：⚠️ **TaskGroup 標 done 但實際嚴重失敗**（範圍縮水到 1/12 + 多項流程斷裂）

---

## 試驗目的（FF 二十七 框架）

對照 Trial_v3 結尾規劃：「Stage 40 / 41 完成後可考慮 Trial_v4：用會涉及 a11y / 安全議題的任務，驗證 CLAUDE_Vera.md / Petra.md 補強是否生效 + Stage 41 Quinn 測試補強是否生效。」

**首次完整三層品質迴圈閉環**驗證：
- ✅ Stage 39+40：Vera 審查層（a11y / 安全 / pattern match）
- ✅ Stage 40：Petra 閘門層（Warning→blocking 升級）
- ✅ Stage 41：Quinn 測試層（grep 證據 + unverifiable_targets）

**Top 1/2/3 預期驗證點**（沿用 FF 二十七 框架）：
- Top 1 — Vera 抓 a11y / 安全議題
- Top 2 — Petra 升級「架構議題 Warning → blocking」
- Top 3 — Quinn 測試品質 + 結構性 bug 防護

---

## 任務需求（Christ 在 Dashboard 快速下達指令卡輸入）

> Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。
>
> 最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。
>
> 舉個例子：首頁那個快速下達指令卡，如果我打的指令違規，提示就會在送出區上面冒出來，但有時候我已經滑下去看其他東西了，根本沒注意到指令沒送出去。
>
> 我希望能改成：表單裡的提示保留（緊貼違規來源），但同時要有個東西通知我「剛剛失敗了」，類似右下角彈出來的那種 toast 提示，讓我視線在哪裡都會看到。兩個地方訊息一致，toast 大概 3-5 秒自動消失就好。
>
> 不只快速下達指令卡，我覺得整個 Dashboard 凡是有「儲存失敗 / 操作失敗」的地方（系統設定、操作中心、Agent 設定、規則管理之類），都應該照同樣方式打磨一下。具體哪些頁面要動、要不要抽共用元件，你跟團隊判斷。

---

## 完整 Timeline（13 個 task，跨 ~9 小時 + 1 小時實際執行）

| 時間 (TW) | 階段 | 結果 |
|---|---|---|
| 23:06:35 | CEO Victoria 分類為 `new_feature` + 任務標題提煉 | ✅ 高品質提煉 |
| 23:06-23:14 | **Kickoff 五人 × 3 輪 = 8 分鐘收斂** | ✅ 6 個關鍵決策 + 6 個風險 + 完整實作建議 |
| 23:14-23:30 | Christ 等待按「繼續」（**8h16m，無主動通知**） | 🐛 Bug #3 |
| 23:30-23:38 | **Design 五人 × 1 輪** | ✅ Cody 主動 grep 揭露 5 個技術風險 + Quinn 揭露 2 個結構陷阱 |
| 23:38-23:41 | **Dev_plan 失敗** — DevPlan 欄位寫「（計畫書產出失敗，請查看 log）」17 字 | 🔴 Bug #5 |
| 23:41-23:42 | Petra 駁回 → Cody Appeal **accept**（自承「必須重新產出」）| 🔴 但流程沒重新產出 |
| 23:42-23:47 | **Dev 第一輪 5.5 分鐘**（無計畫書狀況下）→ 產出 PR #122 | ⚠️ 41 行 Service + 1 行 DI 註冊 |
| 23:47-23:49 | Reviewer 第一輪 1m52s | Vera 抓 1 Warning（onUndo）+ 1 Info（範圍縮水）|
| 23:49 | Petra→Vera 第一輪 → revision（**留著沒 close**）| 🐛 Bug #4 衍生 |
| 23:49 (3 sec) | **Dev 第二輪 fix 失敗（Token 守門擋下）** | ⚠️ Stage 22 Token 守門首次真實生效 |
| 23:49-23:51 | **Reviewer 第二輪 2m22s**（review 同一個未修的 PR）| 🔴 Bug #8 |
| 23:51-23:52 | Petra→Vera 第二輪 → done | ✅ 通過進 QA |
| 23:52-23:53 | **QA Quinn 失敗** | 🔴 但流程繼續 |
| 23:53-00:07 | **Doc 跑 13:30**（異常慢）| 🔴 Bug #12 (URL 寫錯 + 「無實作說明」)|
| 00:07 | **TaskGroup mark done** | 🔴 Bug #11（QA 失敗仍 done）|

**最終產出**：[`PR #122`](https://github.com/darkleong/AiTeam/pull/122) **OPEN 未合併**，包含：
- `src/AiTeam.Dashboard/Services/DashboardNotificationService.cs`（41 行新增）
- `src/AiTeam.Dashboard/Program.cs`（+1 行 DI 註冊）
- `docs/archive/pr122-archive.md`（21 行 Doc 歸檔）
- `CHANGELOG.md`（+13 行）

**僅完成 Design 階段拆解 12 個 Issue 中的 1 個（Issue 2）**。

---

## Top 1 / 2 / 3 試驗結果矩陣

| 維度 | Trial_v2 | Trial_v3 | **Trial_v4** |
|------|---------|---------|---------|
| 任務性質 | 純 .razor UI 微調 | razor + razor.cs PR 欄位 | **跨 5 頁面架構級重構** |
| 送出路徑 | Discord | Discord | **Dashboard 快速下達指令卡** |
| 任務分類 | tech_improvement | tech_improvement | **new_feature** |
| 走完整 pipeline | 否（無 Kickoff/Design）| 否 | **是（Kickoff + Design + Dev_plan + Petra + Dev + Vera + QA + Doc）**|
| **Top 1（Vera a11y 抓取）** | ❌ Vera 略過 | 🟡 部分（漏 a11y `<a>`）| 🟡 **無從驗證**（Cody 只做 41 行 Service，無 a11y 改動觸發點）|
| **Top 2（Petra 升 blocking）** | 沒驗 | 🟢 預期行為（Warning 放行）| 🔴 **規則對範圍縮水失效**（Stage 40 升級清單沒此議題）|
| **Top 3（Quinn 測試品質）** | ✅ 高品質 | ✅ over-deliver | 🔴 **QA failed**（測試沒跑）|
| 整體成本 | 不詳 | 不詳 | **$4.99 USD（Trial 系列首次有完整成本紀錄）**|
| PR 結局 | merged | merged | **OPEN 未合併** |

---

## 流程觀察 Checkpoints

### Checkpoint 1：Victoria 分類 + 任務標題提煉 ⭐

對照 Trial_v3 觀察點：
- ✅ **任務標題精煉到位**：「Dashboard 錯誤處理 UX 打磨：雙軌通知（MudAlert + Toast）」抽出核心關鍵字（雙軌 + 兩元件），優於 Trial_v3 的「流程追蹤頁面 PR 欄位優化」（後者只描述位置）
- ⚠️ **分類為 `new_feature`** 而非 `tech_improvement`——影響後續走完整 pipeline，包含完整 Kickoff/Design 階段

### Checkpoint 2：Kickoff 五人 × 3 輪 ⭐⭐

**8 分鐘收斂三輪會議，產出 11.3KB TaskPlan**：
- 6 個關鍵決策（方案 B / Scoped / MVP 範圍 / ErrorBoundary 分開 / a11y 最小化 / Snackbar 時間分級）
- 6 個風險識別（含 🔴 高風險 1 個：9 呼叫點靜默 regression）
- 完整實作建議含 C# 程式碼範例 + 規範文件更新清單

**亮點**：
- Cody 在 Round 2 **自我修正設計誤解**（Singleton → Scoped）— 對照 FF 二十五「Cody 走快路徑」傾向，本次 Cody 主動修正
- Quinn 主動提**特徵測試 +4-8h**（防 9 個呼叫點靜默 regression）— 業界正確做法（Working Effectively with Legacy Code）
- Rosa 對「漸進式完整」歧義**主動追問釐清**——對照 Trial_v3 觀察點 2「Victoria 對歧義不主動追問」**有改善**

### Checkpoint 3：Design 五人 × 1 輪 ⭐⭐⭐

**Cody 自掃 codebase 揭露 5 個技術風險**（不是空泛評論）：

| Cody 發現 | 嚴重度 |
|---|---|
| Issue 8 QuickCommandCard 性質誤為「遷移」實為「新增」 | 🔴 高 |
| MudAlert 雙重用途（操作回饋 vs 頁面常駐狀態）| 🔴 高 |
| `Action?` vs `Func<Task>?`（Blazor async event handler 型別不對）| 🟡 中 |
| Issue 7 InteractionCenter 409 是語義變更非純遷移 | 🟡 中 |
| Issue 3 與 Issue 2 時序倒置（特徵測試應在 Service 之前）| 🟡 中 |

**Quinn 揭露 2 個結構性測試陷阱**（Stage 41 補強已生效）：
- ⭐「特徵測試的現狀可能本來就是錯的」— 盲目記錄現狀=鎖死錯誤行為
- 「`Action?` 會吞掉例外，測試只驗按鈕存在會漏」

### Checkpoint 4：Dev_plan 階段失敗 🔴

**Cody 跑 3 分鐘後 DevPlan 欄位只寫**：
> `（計畫書產出失敗，請查看 log）` — 17 字

**Petra 駁回**：「無法審核計畫書——計畫書產出失敗，未提供 log」

**Cody Appeal Round 1 回應**（解碼後）：
> 「Petra 的意見完全合理且無從反駁。計畫書產出失敗是客觀事實...**我必須重新產出完整的實作計畫書，或提供產出失敗的詳細 log 供診斷。接受此意見並補齊產出物是正確的做法。**」`Position: accept`

**🔴 流程斷裂**：Cody 自承「必須重新產出」但**流程沒給她重新產出的機會**——直接進入 Dev 階段。後續 Cody 在**無計畫書情況下** self-implement，完全失去 Design 階段揭露的 5 項調整依據。

### Checkpoint 5：Dev 階段 PR 範圍嚴重縮水 🔴⭐⭐⭐

**5.5 分鐘 Cody Dev 階段 → PR #122**：

| 預期（Design 12 Issue）| 實際 |
|---|---|
| Issue 1：ErrorBoundary spike | ❌ 沒做 |
| **Issue 2：DashboardNotificationService 實作** | ✅ 41 行新 Service |
| Issue 3：特徵測試（9 呼叫點）| ❌ 沒做 |
| Issue 4：AgentSettings 遷移 | ❌ 沒做 |
| Issue 5：RuleManagement 遷移 | ❌ 沒做 |
| Issue 6：SystemSettings 遷移 | ❌ 沒做 |
| Issue 7：InteractionCenter 遷移 | ❌ 沒做 |
| Issue 8：QuickCommandCard 新增通知 | ❌ 沒做 |
| Issue 9：a11y 屬性補入 | ❌ 沒做 |
| Issue 10：規範文件更新（mudblazor.md / csharp.md）| ❌ 沒做 |
| Issue 11：Playwright E2E | ❌ 沒做 |
| Issue 12：Future_Feature.md 結案紀錄 | ❌ 沒做 |

**完成率 1/12 = 8.3%**。對照 Kickoff 預估「1-2 週 / 20+ 改動點」，**實際 5.5 分鐘 / 41 行**。

### Checkpoint 6：Vera review 看穿範圍縮水但保守標 Info ⚠️

Vera 第一輪 review 抓到（`LastReviewBody`）：

**🟡 1 Warning**：
- `options.OnClick = _ => onUndo()` 未包覆例外 → **可能造成 Blazor Server Circuit 斷線**

**🟢 1 Info（看穿了範圍縮水）**：
- 「Phase 1 僅完成服務定義與 DI 註冊，**現有 9 個元件仍直接注入 ISnackbar**，時間分級策略尚未生效」+ 列出 9 個未遷移元件名

**問題**：
- 🔴 onUndo 議題**對照 Stage 40 補強的 Critical 規則**：「`@onclick` handler 內未處理可能拋例外的呼叫，且該例外會在 Server Circuit 內炸掉整個 connection → **Critical**」—— Vera 的描述「透過 MudBlazor 元件事件鏈傳播至 Blazor Server Circuit，可能造成 circuit 斷線」**完全符合 Critical 條件**，但她**保守標 Warning** → CLAUDE_Vera.md 邊界 Vera 自己沒站穩
- 🔴 範圍縮水標 Info 而非 Critical → Vera 沒能升級「PR 範圍嚴重不符計劃書」這個議題

### Checkpoint 7：Dev fix 失敗 + Reviewer 仍跑 + Petra 通過 🔴

```
23:49:32 第二輪 Dev 啟動
23:49:35 第二輪 Dev 失敗（Token 守門 18,158 + 6,951 > 20,000 日限）
23:49:35 第二輪 Vera review 啟動（review 同一個未修的 PR）
23:51:57 Vera 第二輪 done
23:52:05 Petra→Vera 第二輪 done → 進 QA
```

**Stage 22 Token 守門 ✅ 真實生效**（Trial 系列首次）。
**但流程沒中止**：Cody 沒修 code → Vera 仍 review → Petra 仍通過 → 進 QA。

### Checkpoint 8：QA Quinn 失敗 + Doc 仍跑 + TaskGroup mark done 🔴

```
QaFixRound = 0  → QA 失敗沒進 fix loop
QA failed → 直接進 Doc → Doc done → TaskGroup mark done
```

**TaskGroup 完成判定邏輯有 bug**：「QA 失敗」不算 TaskGroup 失敗。

### Checkpoint 9：Doc Sage 13:30 異常 + 寫錯 PR URL 🐛

Doc 跑 13 分 30 秒（最長階段）但只產出 21 行歸檔，且：
- PR URL 寫錯：`https://github.com/feature/110-dashboard-notification-service`（feature/ 是 branch 名 + PR 編號 110 不是 122）
- 實作摘要：「**無實作說明**」（Sage 老實寫沒看到實作，因為 DevPlan = 17 字「產出失敗」）

→ Sage 對「無實作」**沒升 escalate**，直接寫「無實作說明」歸檔。

---

## 戰略結論：揭露三層迴圈的真實邊界

### ✅ 三層迴圈生效範圍（程式碼層面）

- **a11y / 安全議題**：Vera 在 Kickoff/Design 階段預警 + Stage 40 補強讓 Vera 抓得到
- **重複定義 / 硬編碼 / pattern match / `target="_blank"` 缺 rel**：Stage 40 Petra Warning→blocking 升級規則涵蓋
- **測試品質結構性陷阱**：Stage 41 Quinn 補強（特徵測試現狀可能錯 / Action? 吞例外）

### ❌ 三層迴圈擋不下（自然語言情境暴露的盲點）

- **「PR 範圍嚴重不符計劃書」**（Phase 1/12 = 8.3%）— Stage 40 升級規則沒此議題
- **「Cody Dev_plan 產出失敗 + Appeal accept 後沒補上」** — Appeal flow 設計缺口
- **「Cody fix 失敗仍進 Vera review」** — fix loop 完整性缺口
- **「QA 失敗仍進 Doc + TaskGroup mark done」** — TaskGroup 完成判定缺口
- **「Vera 自己保守標 Info 而非 Critical」** — Vera Critical 邊界自我 underuse
- **「Sage 對『無實作』不 escalate」** — Doc 階段缺實作存在性檢查

### Self-implement 適用邊界（首次精準定義）

| 任務性質 | Self-implement 適用嗎？ |
|---|---|
| **小範圍純技術改動**（Trial_v3 PR #109 流程追蹤頁面 PR 欄位）| ✅ 適合（成本低 + 三層迴圈擋得下）|
| **跨多檔案 / 多階段架構級重構**（Trial_v4 12 個 Issue）| 🔴 **不適合**（範圍縮水 + 成本高 + 三層迴圈擋不下）|
| **需要 Plan 階段多輪辯論的需求** | 🔴 **不適合**（Kickoff/Design 燒掉一半預算 + 後續 Cody 沒能力按計畫實作）|

**戰略決定**：未來這類「跨多檔案 / 架構級重構」需求，應**開獨立 Stage 由 Aria 規劃 + Forge 實作**，不交 Victoria self-implement。

---

## 完整 Bug 清單（Trial_v4 揭露 13 個）

| # | Bug | 嚴重度 | 對應行動 |
|---|---|---|---|
| 1 | CEO / Dev_plan 狀態卡顯示「閒置」（Dev queue 實際被佔）| 🟡 中 | 併入 FF 二十二 子項 B 同源議題 |
| 2 | Kickoff 卡截斷「請查看 Dashboard」（已在 Dashboard 內）| 🟡 中 | 併入 FF 十六（Dashboard UX 打磨）|
| 3 | BossInteraction 等待 8h 無主動通知 | 🟡 中 | 併入 FF 十六 |
| 4 | Design `needs_adjustment` fake 修改訊息 | 🔵 低 | 併入 FF 二十五（self-implement prompt 守則）|
| 5 | **DevPlan Appeal accept 後流程斷裂沒重新產出** | 🔴 高 | **新 FF 三十二 子項 A** |
| 6 | UI 時間 UTC vs 本地混用 | 🔵 低 | 併入 FF 十六 |
| 7 | Dashboard 不區分 fix iteration 輪次 | 🔵 低 | 併入 FF 十六 |
| 8 | **Cody Dev fix 失敗時 Reviewer 仍啟動** | 🔴 高 | **新 FF 三十二 子項 B** |
| 9 | **Petra 升級規則沒涵蓋「PR 範圍縮水」** | 🔴 高 | **新 FF 三十二 子項 C** |
| 10 | **Vera Critical 邊界 underuse**（onUndo Server Circuit 斷線僅標 Warning）| 🟡 中 | **新 FF 三十二 子項 D** |
| 11 | **QA 失敗仍進 Doc + TaskGroup mark done** | 🔴 高 | **新 FF 三十二 子項 E** |
| 12 | Sage 寫錯 PR URL + 「無實作」不 escalate | 🟡 中 | **新 FF 三十二 子項 F** |
| 13 | **token_logs 沒涵蓋 CLI Agent token**（Vera/Quinn/Sage/Kickoff/Design/Dev_plan）| 🔴 高 | **新 FF 三十三** |

---

## 成本紀錄（首次完整 Trial 成本 baseline）

```
起點 API 餘額：$9.31
終點 API 餘額：$4.32
Trial_v4 總消耗：$4.99 USD

每行有效 production code 成本：$0.12 / 行（41 行）
預期工作量達成率：8.3%（1/12 Issue）
```

**對 FF 一（API 費用優化）價值極高**：
- Trial 系列首次有成本基準
- 5 Agent 開會階段（Kickoff + Design）約佔 50% 成本
- Doc 階段 13:30 異常長 → 未來深入調查
- Bug #13（token_logs CLI 漏記）→ 解了才能對齊真實成本

---

## 後續行動清單

### 立即（Trial 結案）

- [x] 寫 Trial_v4.md（本檔）
- [ ] 更新 FF 二十七：v4 結果 + Top 1/2/3 標 🟡/🔴 + 戰略結論「Self-implement 適用邊界」
- [ ] **新增 FF 三十二**：Self-implement 完整性閘門（六子項 A-F）
- [ ] **新增 FF 三十三**：Token 計費機制 CLI Agent 涵蓋

### 處理 PR #122

待 Christ 決策：
- (a) **Close PR** + 開獨立 Stage（Aria 規劃 + Forge 實作完整 12 個 Issue）
- (b) **手動 merge Phase 1**（41 行 Service）+ 開後續 Stage 補完 Phase 2
- (c) **保留 OPEN 暫不處理** + 排入未來 Stage 帶完

Aria 推薦 (a)：Trial_v4 已揭露「跨多檔架構重構」不適合 self-implement，正規 Stage 才是合適路徑。

### 戰略建議

**未來 Trial_v5 候選**：
- 必須是「**小範圍純技術改動**」性質（驗證適用邊界）
- 例：FF 三十（tech_improvement ghost Dev task）—— 動 Orchestrator 但範圍可控
- **不要**選跨多檔案的需求（已知會踩 Bug #5/#9/#11）

**FF 三十二 完成前不開 Trial_v5**：先補上「self-implement 完整性閘門」六子項，再做下次試驗才有意義。

---

## 對 self-implement 戰略的最終判斷（Trial_v4 升級版）

**系統能力分層**（更精準）：

✅ **執行層（小範圍場景）健康**：
- Cody 寫小 PR 品質 OK（如 Trial_v3 PR #109）
- Quinn 在 Design / Kickoff 階段測試品質意識成熟（Stage 41 補強生效）
- Vera 程式碼層面議題抓取能力 OK

🟡 **執行層（大範圍場景）有缺口**：
- Cody 在「無 Dev_plan」情境下失去計劃指引 → PR 範圍縮水
- Cody 對 Appeal accept 後續行為設計不完整
- Vera 對「範圍縮水」議題 underuse Critical 等級

🔴 **流程層的多處斷裂**：
- DevPlan 失敗無容錯機制
- Dev fix 失敗不中止 fix loop
- QA 失敗不影響 TaskGroup 完成判定
- Doc 對「無實作」不 escalate

**戰略結論**：
1. **短期**（Stage 42-43）：補完 FF 三十二「self-implement 完整性閘門」六子項 + FF 三十三「token 計費 CLI 涵蓋」
2. **中期**：Self-implement 限縮到「小範圍純技術改動」場景，大範圍需求走正規 Stage
3. **長期**：Trial_v5+ 的選題標準明確化（範圍 / 性質 / 預期成本上限）

---

> 此紀錄為 self-implement 試驗系列首次完整三層迴圈閉環試驗、首次跨多階段 pipeline 試驗、首次有完整成本紀錄試驗、也是首次發現「self-implement 範圍縮水」這個結構性盲點。

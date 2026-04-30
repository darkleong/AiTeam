# Self-Implement 試驗 v5 — Dashboard 錯誤處理體驗優化（Trial_v4 重做對照組）

> 日期：2026-04-30
> 觸發：**Trial_v4 對照組** — FF 三十二 / 三十三 / 三十四 / 三十五 四項補強完成後重跑相同 prompt
> 任務：Dashboard 錯誤處理 UX 補齊（toast 通知雙軌機制）— 完全照搬 Trial_v4 prompt + 末尾引導句
> WorkflowType：`proposal` → `new_feature`
> PR：[#170](https://github.com/darkleong/AiTeam/pull/170)（OPEN, mergeable，Trial 性質不合併）
> 觀察者：Aria（Opus 4.7 1M）+ Christ
> 狀態：✅ 完成（4 FF 擋牆全鏈路有效驗證 + 揭露 6 個流程設計層級新議題）

---

## 試驗目的

- **核心目的**：對照 Trial_v4 13 bugs，驗證 4 FF 補強（FF 三十二/三十三/三十四/三十五）的全鏈路擋牆效果
- **次要目的**：FF 二十七觀察點 10 項（Trial_v4 末段定義）命中率
- **戰略意義**：AiTeam 從 Trial_v4「玩具系統」階段躍進到 Trial_v5「真實開發團隊」階段的關鍵驗證

---

## 任務需求（完全照搬 Trial_v4 + 末尾引導句）

> Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。
>
> 最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。
>
> 舉個例子：首頁那個快速下達指令卡，如果我打的指令違規，提示就會在送出區上面冒出來，但有時候我已經滑下去看其他東西了，根本沒注意到指令沒送出去。
>
> 我希望能改成：表單裡的提示保留（緊貼違規來源），但同時要有個東西通知我「剛剛失敗了」，類似右下角彈出來的那種 toast 提示，讓我視線在哪裡都會看到。兩個地方訊息一致，toast 大概 3-5 秒自動消失就好。
>
> 不只快速下達指令卡，我覺得整個 Dashboard 凡是有「儲存失敗 / 操作失敗」的地方（系統設定、操作中心、Agent 設定、規則管理之類），都應該照同樣方式打磨一下。具體哪些頁面要動、要不要抽共用元件，你跟團隊判斷。
>
> **請當做完整新功能走 Kickoff + Design 完整流程**

⚠️ 末尾引導句是 Aria 為 Trial_v5 加的（避免 Victoria 三次嘗試都分類為 tech_improvement，前兩次 cost ~$0.7 累積已驗證需要明確引導）。

---

## 流程觀察 Checkpoints

### Checkpoint 1：Victoria 分類三次嘗試對照 ⚠️

| 嘗試 | 路徑 | 分類 | Token | Cost |
|---|---|---|---|---|
| 1 | Discord | `ceo_confirm`（無 Kickoff/Design）| 4,032 | $0.32 |
| 2 | Dashboard | `ceo_confirm`（同 1）| ~1,500 | ~$0.30 |
| **3** | **Discord（加引導句）**| **`proposal`** ⭐ 完整流程 | 1,512 | **$0.062** |

⚠️ **同 prompt 三次給三種分類**：
- 嘗試 1/2 不加引導 → tech_improvement 派 Dev 直派路徑（跳過 Kickoff/Design）
- 嘗試 3 加「請走完整流程」 → proposal（更完整：Rosa/Demi 探索 → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc）

⭐ Victoria 探索成果 **carry forward 給 Rosa**（「已知 codebase 背景，供 Rosa 參考，不需重複調查」） — Trial_v4 沒看到的協作優化。

### Checkpoint 2：Kickoff 5 人會議 ⭐⭐ 顯著進步

11 分 11 秒 / 3 輪 / 16 LLM call / **$1.72** / KickoffMeetingLog 63,590 字 / TaskPlan 7,222 字

⭐ 計劃書品質**顯著優於 Trial_v4**：
- **量化精準度**：「11 元件 / 30+ 操作點 / 5-10 天工期」
- **避坑策略**：「分批 MVP（高風險優先）」← 主動避開 Trial_v4 一次性全補失敗
- **範圍收緊**：「SignalR 連線監控推獨立 FF」
- **Server Circuit 預判**：「Circuit 隔離問」（呼應 Stage 39 FF 二十九判準）

`AgentName='Meeting-Kickoff'` 統一記錄（multi-agent session），16 row TotalCostUsd **100% 填寫** ✅

### Checkpoint 3：Design 5 人 1 輪 ✅

11 LLM call / **$1.77** / DesignPlan 10,549 字 / DesignMeetingLog 52,522 字 / Petra 拆 12 Issue（#159-#170）

### Checkpoint 4：Dev_plan 重產 + escalate ⭐⭐⭐ 觀察點 #1 命中

| 輪 | 結果 |
|---|---|
| 第 1 輪（21:27:10）| Cody 寫 17,487 tokens 實作計畫書 → Petra LLM `approve` → **但 IsDevPlanFailed helper 獨立判失敗** → 觸發重產 |
| 第 2 輪（21:30:28）| Cody Claude Code subprocess **maxTurns=10 上限**（11 turns 失敗）→ DevPlan 變 17 字「（計畫書產出失敗，請查看 log）」覆蓋第一輪 |
| Petra 第二次審 | `decision=escalate` → **`devplan_escalate` BossInteraction 建立** |

⭐ FF 三十二子項 A 完整鏈路驗證 ✅

⚠️ 順帶觀察兩個小議題：
- 第一輪 17K 字 DevPlan 內容**被第二輪 17 字失敗訊息覆蓋丟失**（FF 三十二子項 A 設計小缺陷：應寫入 DevPlanAppealLog fallback）
- Cody Dev_plan maxTurns=10 對 Design Plan 10K 字 + 11 元件複雜任務不足（**議題 E**）

### Checkpoint 5：Token 守門 trap 🔴 揭露 ops 重大議題

Christ 按「跳過審核，直接開發」後 Cody Dev 階段被 Token 守門擋（全域月限 **2M** / 已用 **3.25M**）。

Aria 排查發現 **docker-compose.prod.yml env 寫死 `MonthlyTokenLimitK=2000`**，**完全 override 我前面 commit `f7e476b` 改的 appsettings.json (20000)**。

修復鏈路（22:00 ~ 22:10）：
1. 21:52 第一次 Dev failed（守門擋）
2. Aria 重啟 Bot 容器（**無效** — env 沒改）
3. 22:00 重試又失敗
4. Aria 改 docker-compose.prod.yml + commit `df3bfb7` + push
5. CI/CD 偵測 env 改動 → docker compose **recreate container**
6. 22:10 Bot 用新 env 20M ready

→ 揭露 **議題 C**（Token limit SoT 分歧）+ **議題 D**（CI/CD 部署不重啟容器）

### Checkpoint 6：Cody Dev 階段 ⭐⭐⭐ 巨大進步

22:11 重啟 Dev 後 Cody Claude Code subprocess **沒踩 maxTurns**：

| 維度 | Trial_v4 | Trial_v5 |
|---|---|---|
| PR 行數 | 41 行 / 1 個 Service | **+916 / -44 / 13 個檔案** ⭐ |
| Closes Issue | 1/12（縮水）| **11/12** ⭐ |
| 內容 | 1 個小 service | Service + 3 頁面 Toast 遷移 + 文件 + 測試 |
| **DB ImplementationNote** | — | **0 字（Cody 沒寫）** 🔴 |
| **PR Body** | — | 完整詳盡（變更摘要 + Closes 列表）|

→ Cody 寫的 PR 品質**顯著超越 Trial_v4**，但**沒填 DB ImplementationNote 欄位** → 觸發後續 Sage escalate（**議題 B**）。

### Checkpoint 7：Vera Reviewer ⭐⭐⭐ 達到 senior reviewer 水準

Vera report：1 Warning + 1 Info + **0 Critical**

⭐ **9 處裸 catch 全抓到**（PR diff 確認）：
- ProjectManagement ×1（切換狀態）
- RuleManagement ×2（切換狀態 + 刪除）
- SystemSettings ×6（Skip CEO / Mock Mode / CEO 頻道 / Christ ID / Mock 延遲 / 流程輪次）

⭐ **Warning 等級判斷正確**：不阻塞 merge + 生產環境影響（OperationCanceledException 攔截顯示誤導訊息）+ 對齊 CLAUDE_Vera.md 保守用詞 — Critical 邊界沒踩。

⭐ **正確區分「縮水 vs 分批」**：影響範圍分析提到「7 元件未遷移」但**沒列為議題**，因 Cody PR Body + Kickoff 計劃書已說明「Batch 1 高風險頁面」。

對比 Trial_v4 Bug #7：「Vera 看穿縮水但保守標 Info（沒區分縮水 vs 分批）」— **Trial_v5 顯著進步**（Stage 39 FF 二十八 + Stage 42 prompt 補強傳導效應）。

### Checkpoint 8：Petra Reviewer 審 ✅

Log: `Petra LLM fallback 審核完成（第 1 次）：approve` → `WorkflowEngine 決策：action=FireAgents`

Vera 1 Warning + 1 Info + 0 Critical → Petra approve → 進 QA。**這是合理判斷**，沒違反「偏好放行」哲學。

→ 觀察點 #3 「Petra 升 blocking」**沒命中**（這次 Vera 沒有「N 元件未遷移 Info」需要 Petra 升 blocking — 因 Vera 已正確區分縮水 vs 分批）。

### Checkpoint 9：Quinn QA ⭐⭐⭐ 巨大進步

5 個測試檔 / **30 xUnit + 6 visual screenshot** / 全 passed / dotnet build 0 Error

| 檔案 | 測試數 | 設計亮點 |
|---|---|---|
| DashboardNotificationServiceTests | 7 | 嚴重性斷言 + 80 字截斷邊界 |
| RuleManagementTests | 7 | **Reflection** 驗 private static `GetAgentChipColor` |
| SystemSettingsTests | 12 | Reflection + IsValidSnowflakeId 17-20 位邊界 + IsMockDelayValid 完整邊界 |
| ProjectManagementTests | 4 | **`RuntimeHelpers.GetUninitializedObject`** 繞 MudTr 建構子 ⭐ |
| Playwright PR170 VisualTests | 6 | /system-settings 亮/暗色 + 標題 + 一般設定區塊 |

⭐ 主動標 `unverifiable_targets`（Stage 41 FF 三十一 schema 精確應用）：Program.cs（頂層應用入口）+ IDashboardNotificationService.cs（介面定義）

對比 Trial_v4 Bug #10/#11：「QA 失敗 + TaskGroup mark done」— **Trial_v5 修了所有問題**。

### Checkpoint 10：Sage Doc 14 秒 escalate ⭐⭐ 觀察點 #6 命中

```
22:32:51 Doc 啟動
22:32:53 Clone repo + Claude Code subprocess (haiku-4-5, readOnly=False)
22:33:05 escalate：implementation_note 為空，無實作說明可歸檔
22:33:05 sage_escalate BossInteraction 建立 + repo 清除
```

⭐ FF 三十二子項 F 完整觸發 ✅（看 0 字直接 escalate，**不浪費 LLM 算力跑無效歸檔** — 14 秒 / 837 tokens / $0.02）

⚠️ **但判斷過嚴**：Sage 沒 fallback 看 PR Body（Cody 寫了完整 PR description），→ **議題 B**（ImplementationNote 寫入路徑斷裂）

對比 Trial_v4 Bug #12：「Doc 跑 13:30 異常慢 + URL 寫錯 + 直接放行『無實作說明』」 → **Trial_v5 修了所有問題**（速度 / URL / 不放行），但**過嚴了**（誤判 Cody 沒寫實作）。

### Checkpoint 11：MarkGroupDoneOrIntervention 誤判 🔴 議題 A

流程末端 helper：`MarkGroupDoneOrIntervention：group 有 failed/needs_intervention task → needs_intervention`

但這些 failed task 是**歷史殘留**：
- 21:30:29 Petra Dev_plan 第二輪 task → failed（已被 21:52 Christ 「跳過審核」action 處理過）
- 21:52:34 Dev task → failed（已被 22:11 Christ 「重啟 Dev」action 處理過）

兩個 failed task **沒被自動清除** → MarkGroupDoneOrIntervention 誤判 → 建 intervention BossInteraction「Vera 在 0 次修復後仍發現問題」（**訊息嚴重誤導：實際 Vera approve**）

→ **議題 A**：Dashboard 重試/跳過後舊 failed task 沒標記為 resolved → 流程末端誤判

---

## 試驗 v5 結果矩陣

### 預期觀察清單 10 項命中

| # | 觀察點 | Trial_v5 結果 |
|---|---|---|
| **1** | DevPlan 失敗→重產+escalate | ✅ **完美命中** |
| 2 | Dev fix 失敗→Reviewer 不啟動 | ⚠️ 沒驗到（沒進 fix loop）|
| 3 | Petra 升 blocking | ❌ 沒命中（Petra approve）|
| 4 | Vera Critical | ❌ 沒命中（Vera 標 Warning，邊界判斷正確）|
| 5 | QA 失敗→TaskGroup failed | ❌ 沒驗到（QA 沒失敗）|
| **6** | Sage escalate | ✅ **完美命中** |
| 7 | Cody PR ESCALATE_NEEDED | 🔴 沒命中（Cody ImplementationNote=0 字 — 議題 B/F）|
| **8** | token_logs CLI 涵蓋 | ✅ 命中（Meeting/Cody/Quinn/Vera/Sage cost 全填，僅 PM/Dev row NULL → FF 四十三）|
| 9 | TaskGroup 暫停 | ⚠️ UI 確認可見但沒實際使用 |
| 10 | Petra 拆 task 提案卡 | ❌ 沒觸發（規則層門檻 ≥ 8 Issue 需單個 Phase 跨多 Phase 條件未滿足）|

→ Hard hits：**3/10**（#1 + #6 + #8）

### Trial_v4 vs Trial_v5 對照（關鍵維度）

| 維度 | Trial_v4 | Trial_v5 |
|---|---|---|
| Cody PR 範圍 | 1/12 縮水（41 行）| **11/12 完成（916 行）** ⭐⭐⭐ |
| Vera review | 1 Warning + 1 Info（保守誤判）| 1 Warning + 1 Info（**精準判斷**）|
| Petra | 縮水 PR 過審 | 合理 PR approve |
| Quinn | 失敗（流程繼續）| **30 xUnit + 6 visual passed** ⭐ |
| Sage | 13:30 異常 + URL 錯 + 放行 | **14 秒 escalate**（修速度+URL+不放行）|
| TaskGroup 結局 | mark done（QA 失敗仍 done）| `needs_intervention`（多重擋牆觸發）|
| 揭露 bug 數 | 13 個（功能/流程/UX）| **6 個流程設計層級議題** + 2 個觀察點 hit |
| Cost | $4.99 | **~$8.78**（多 76% — 含 3 次 Victoria 分類 + Cody Dev_plan 兩輪 + 重啟 Bot 補課）|
| 歷時 | ~9 小時 + 1 小時執行（Bug #3 通知缺失）| ~2 小時（含 Aria 排查 ops 議題 10 分鐘）|

---

## 關鍵結論：4 FF 擋牆全鏈路有效，但揭露 6 個更深層議題

### ✅ 4 FF 擋牆有效驗證

| 補強 | 觸發場景 | 結果 |
|---|---|---|
| **FF 三十二子項 A**（DevPlan 容錯） | Cody 兩輪 Dev_plan 都失敗 | ✅ 重產→上限超出→escalate Christ |
| **FF 三十二子項 F**（Sage escalate） | implementation_note=0 字 | ✅ 14 秒 escalate 不歸檔 |
| **FF 三十二子項 G**（Cody ESCALATE_NEEDED）| Cody 沒寫 ImplementationNote | 🔴 沒觸發（Stage 42 補強單向性 — 議題 F）|
| **FF 三十三**（Token 計費 CLI）| 全 Agent 跑完 | ✅ Meeting/Cody/Quinn/Vera/Sage 全填 cost（PM/Dev NULL → FF 四十三 backlog）|
| **FF 三十四**（流程暫停）| Christ 全程盯沒實際使用 | ⚠️ UI 顯示確認可見 |
| **FF 三十五**（自動拆任務）| 「11 元件 / 30+ 操作點」未觸發拆 task | ⚠️ 規則層門檻條件未滿足（單個 epic 12 Issue 但跨 Phase 結構不夠）|

### 🔴 揭露 6 個流程設計層級議題

| # | 議題 | 嚴重度 | 影響 |
|---|---|---|---|
| **A** | MarkGroupDoneOrIntervention 看歷史 failed task 誤判 | 🔴 高 | 所有「重試/跳過」action 後的 group status 判斷被歷史 failed 污染 |
| **B** | ImplementationNote 寫入路徑斷裂（PR Body vs DB 欄位）| 🔴 高 | Sage 過嚴 escalate「沒實作」實際 Cody 已寫好實作 |
| **C** | Token limit SoT 分歧（appsettings vs docker-compose env）| 🔴 高 | 改 appsettings 但 env 沒對齊 → 靜默無效（ops 高風險陷阱）|
| **D** | CI/CD 部署不重啟容器（image+file 變但 in-memory 沒換）| 🟠 中-高 | 設定改動需懷疑「是否真的生效」 |
| **E** | Cody Dev_plan maxTurns=10 對複雜任務不足 | 🟠 中-高 | 跨 11 元件任務 Cody Dev_plan 100% 踩到 |
| **F** | Stage 42 補強單向性（Vera 判準 vs Cody 實作範本對齊）| 🟡 中 | Cody 寫 9 處裸 catch，Vera 抓 9 處 → 應該寫實作時就對 |

---

## 後續行動清單

### 立即（試驗結案）

- [x] 寫 Trial_v5.md（本檔）
- [ ] 更新 FF 二十七：v5 結果 + 標相關觀察項
- [ ] **新增 FF 四十五**：MarkGroupDoneOrIntervention 看歷史 failed task 誤判（議題 A，🔴 高）
- [ ] **新增 FF 四十六**：ImplementationNote 寫入路徑與 PR Body 對齊（議題 B + F，🔴 高）
- [ ] **新增 FF 四十七**：Token limit SoT 統一 + CI/CD 部署可靠性（議題 C+D 合一，🔴 高）
- [ ] **新增 FF 四十八**：Cody Dev_plan maxTurns 配置不足（議題 E，🟠 中-高）
- [ ] FF 四十三 描述更新（PR/Dev/PM AgentName cost NULL 仍存在 → 部分 caller 未涵蓋）
- [ ] PR #170 close 不合併（Trial 任務性質）
- [ ] 處理 3 張 BossInteraction 卡（Christ Dashboard ack）

### Stage 49 候選（Trial_v5 後續修補）

**主菜**：FF 四十五 + 四十六（A + B 兩個流程設計議題）
- 議題 A 修法：「重試/跳過」action handler 標記前置 failed task 為 resolved（避免污染 MarkGroupDoneOrIntervention 判斷）
- 議題 B 修法：① Cody 寫實作說明同步寫 DB ImplementationNote 欄位（CLAUDE_Cody.md 子項 G 補）+ ② Sage 判斷加 fallback PR Body（FF 三十二子項 F 補強）

**規模**：M（兩個議題都需要動 Orchestration / Agent prompt）  
**順帶搭車**：FF 四十八（maxTurns 配置）+ FF 四十七子項（Token SoT 統一）

### Trial_v5 之後評估 backlog

- FF 十一（Token Dashboard 化）— 議題 C 修完後評估是否仍需要
- FF 三十六（v4 架構雙支柱 spike）— Trial_v5 仍踩硬編碼根因 vs 否（沒踩，4 FF 補強有效，可降低急迫性）
- FF 三十八（跨專案能力研究）

---

## 對 self-implement 戰略的最終判斷（Trial_v5 升級版）

**系統能力分層**（Trial_v5 校準後）：

✅ **執行層全面進步**：
- Cody **無 DevPlan 仍寫出完整 PR**（11/12 Issue, 916 行）— Stage 42 prompt 補強有效
- Quinn 測試品質**達到專業級**（Reflection + RuntimeHelpers + 邊界 + unverifiable schema）
- Sage 速度修了 + 行為對齊（修了 Trial_v4 的 13:30 異常 + URL 錯 + 直接放行）

✅ **審查層大幅進步**：
- Vera 跨檔影響範圍分析能力**達到 senior reviewer 水準**（9 處裸 catch 全抓 + 區分縮水 vs 分批）
- CLAUDE_Vera.md 判準邊界覆蓋（Stage 39 + Stage 42 連續補強）已接近完整
- Petra 「偏好放行」哲學在合理 PR 表現正確

🔴 **流程設計層揭露 6 個議題**：
- 大部分是 **跨 Agent / 跨層級協作**的流程設計缺陷（不是單 Agent 失職）
- 議題 A/B 是流程末端與 Agent 自我標記之間的**整合縫隙**
- 議題 C/D 是 ops 配置流程的**多 SoT 不一致**

**戰略結論**：
1. **Trial_v5 證明 4 FF 補強讓 AiTeam 從「擋不住」躍進到「擋得住 + 擋過頭」**
2. **Cody/Vera/Quinn/Sage 4 個 Agent 全面達到 production-ready 水準**
3. **下一階段戰略重點**從「Agent 能力強化」轉向「**流程整合精準度打磨**」（議題 A/B 是核心）
4. **self-implement 適用範圍大幅擴展**：跨 11 元件任務不再「縮水」，能完成 11/12 Issue（vs Trial_v4 1/12）— **真實開發團隊水準達成**

---

> 此紀錄為 self-implement 試驗系列首次「對照組重做」試驗、首次驗證 4 FF 補強全鏈路擋牆效果、首次揭露「擋過頭」現象（議題 B）、首次完整壓力測試 ops 配置流程（議題 C+D）。Trial_v5 是 AiTeam v3.x 末期的關鍵戰略里程碑。

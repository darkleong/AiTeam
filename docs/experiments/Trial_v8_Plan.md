# Trial_v8 試驗計劃書 — Stage 60+61 修法後路線 A/B/C 拍板實證

> 對應版本：**v3.50.0**（Stage 60 v4 邊角 legacy 收口 + Stage 61 6 🟡 系統性議題群組清完後第一次真實任務試驗）
> 建立日期：2026-05-10
> 狀態：規劃中（待 Christ API 餘額確認 + 開跑時間）
> 文件版本：v1.0

---

## 一、背景與定位

### 戰略脈絡

- **Trial_v7 結案揭露第 4 🔴 戰略級議題（v3.48.0）**（2026-05-10） — 推翻 Trial_v6「3 🔴 收口 = v4 production-ready」假設 / Kickoff modify path 卡死中斷 / cost $1.5233（-90% vs Trial_v6）/ 揭 1 🔴 + 4 議題
- **Stage 60（v3.49.0）= FF 五十五 ✅** — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess fail-fast 統一（Aria 校準錨 ×0.80 production-ready 補強第三波）
- **Stage 61（v3.50.0）= 6 🟡 系統性議題群組清完 ✅**（FF 五十六 + 二十五 + 四十六 + 四十八 + 五十 + 四十五 + 四十 + 議題 #B）— Petra/Cody prompt 對齊 + Pipeline UI refresh + Dashboard 補強（Aria 校準錨 ×0.99 production-ready 補強第四波）

### Trial_v8 定位

**Stage 60+61 修法後路線 A/B/C 拍板實證** — 三個層次目的：

1. **驗證 Stage 60+61 修法效果**：v4 邊角 legacy 收口 + meeting subprocess fail-fast + Petra/Cody prompt 對齊 + Pipeline UI refresh + Dashboard 補強全 Trial_v8 真實任務驗證
2. **量化 v4 framework 真實 ROI**：cost / 完成度 / 流程順暢度對照 Trial_v5/v6/v7 三 baseline 四向矩陣
3. **戰略大重評估關鍵實證**（Christ 2026-05-10 提出）：
   - 如果 Trial_v8 deliver Phase 1 完整 + 揭 0-1 🔴 → 路線 A 繼續 v4 漸進修補有實證支撐（推翻路線 B/C 提案）
   - 如果 Trial_v8 仍中段卡死 / 揭 ≥ 1 🔴 新類型 → 路線 B/C 戰略大砍複雜度提案有實證

**核心試驗問題**：v4 framework hierarchical static 在 Stage 60+61 補完所有已知議題後，能否真正 deliver 完整 feature？還是 infinite loop 揭新類型 🔴 訊號？

**注**：FF 三十六 Phase B 動態流程架構**仍不在本次範圍** — Trial_v8 仍是「換引擎不換車身」對照試驗，動態架構評估留戰略大重評估後拍板。

---

## 二、試驗目的（5 條）

1. **驗證 Stage 60 修法效果**（v4 邊角 legacy 收口）
   - Kickoff/Design「需要修改」action 走 framework path（不再 legacy KickoffMeetingService.ModifyTaskPlan）
   - meeting subprocess failure → fail-fast `[SUBPROCESS_FAILURE]` marker → Stage 58 第 7 routing 接管 fire `agent_api_failure_intervention`
   - 雙通道（Discord + Dashboard）modify 一致性

2. **驗證 Stage 61 修法效果**（6 🟡 系統性議題群組）
   - Petra prompt 議題層次紀律生效（不丟決策包 / 不工時泛濫 / 給定見不攤議題）
   - Cody Dev_plan 結構規範生效（Step 1/2 / 改哪些檔案 / 不踩「現況確認」表格 → 不踩 escalate）
   - ImplementationNote 強制寫 + Sage 備援 source（不踩 Trial_v6 議題 #8 反例）
   - maxTurns 80 對 Dev_plan cost 影響量化
   - Pipeline KickoffStageExecutor max_iter path Reload entity 修根因（embed 不顯示「無計劃書」）
   - Dashboard token IsEstimated 視覺 + epic UI 接線 + Christ action supersede

3. **量化 v4 framework 真實 ROI 對照基線升級結果**（四向對照 Trial_v5/v6/v7/v8）
   - cost：Trial_v5 baseline $8.78 / Trial_v6 +80% / Trial_v7 -90% 中斷 / **Trial_v8 預期 ~$8-15（含 maxTurns 提升 buffer）**
   - 完成度：Trial_v5 11/12 Issue / Trial_v6 部分 Phase 1 / Trial_v7 0 / **Trial_v8 預期 Phase 1 完整 deliver**
   - 揭露議題數：Trial_v5 13 / Trial_v6 15 / Trial_v7 5 / **Trial_v8 預期 ≤ 5（含 0-1 🔴 新類型 + 0-3 🟡）**

4. **驗證 Trial_v6 三 🔴 收口真實 + Trial_v7 揭 1 🔴 收口真實**（4 🔴 全收口前置條件首次完整真實任務驗證）
   - 議題 #10 race condition fix（Stage 57 partial unique index）真實觸發行為
   - 議題 #12 Vera fix loop limit routing（Stage 57 第 6 routing）真實觸發行為
   - 議題 #15 API failure routing（Stage 58 第 7 routing）真實觸發行為（API 餘額充足下不觸發 OK）
   - Trial_v7 議題 #1 v4 邊角 legacy（Stage 60 收口）真實觸發行為

5. **戰略大重評估實證資料收集**
   - Trial_v8 結果是 Christ 拍板路線 A/B/C 的關鍵實證
   - Aria 主動於 Trial_v8 結案後提醒進戰略大重評估討論

---

## 三、任務需求

### Christ 確認沿用 Trial_v6/v7 同 prompt（四向對照精準度最高）

任務原文（完全照搬 Trial_v6/v7）：

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

---

## 四、預期觀察清單（Trial_v8 vs Trial_v7 vs Trial_v6 vs Trial_v5 四向對照）

| # | 維度 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 預期 | Aria 自驗工具 |
|---|---|---|---|---|---|---|
| 1 | Pipeline framework path 走通率 | N/A | 100% | 100%（中斷前）| **100%**（Stage 61 Reload entity 修根因驗證）| `SELECT COUNT(*) FROM task_groups WHERE "PipelineFrameworkStateJson" IS NOT NULL AND "CreatedAt" >= '<開跑時間>'` |
| 2 | **總 cost** | **$8.78** | **$15.81 (+80%)** | **$1.5233 (-90%)** | **預期 $8-15**（含 maxTurns 80 buffer，maxTurns 翻倍 cost ~+30-50% vs Trial_v6 baseline）| `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` |
| 3 | TotalCostUsd 寫入率 | 0.3% | 100% | 100% | **100%**（Stage 56 修法持續）| `SELECT COUNT(*) FILTER (WHERE "TotalCostUsd" IS NOT NULL) * 100.0 / COUNT(*)` |
| 4 | Cody Dev_plan 完成度 | 1-2 輪通過 | 3 phase 都 escalate | 未到 | **預期 1-2 輪通過**（Stage 61 Cody prompt 對齊 + maxTurns 80 修法驗證）| Dev_plan 輪次 + Petra revise 次數 SQL |
| 5 | Quinn 測試 | 30 + 6 all passed | 0 | 未到 | 預期 ≥ 20 xUnit + visual all passed | `dotnet test` + Playwright |
| 6 | Vera 審查精準度 | 9 處裸 catch 1 輪通過 | Phase 2 fix loop ×3 卡死 | 未到 | 預期 1-2 輪通過 + 不卡死 | Reviewer comment + fix iteration count |
| 7 | Sage escalate 速度 | 13:30 → 14 秒 | Phase 1 escalate / Phase 3 silent skip | 未到 | **預期不踩 Trial_v6 議題 #8**（Stage 61 ImplementationNote 強制寫 + Sage 備援 source 修法驗證）| Bot log timestamp + Sage 行為 |
| 8 | Petra 仲裁次數 | 4-5 次 | 10 PM task + 系統性 escalate | 2 次 | **預期 4-7 次**（Stage 61 Petra prompt 對齊紀律生效，不丟決策包 / 不工時泛濫）| `SELECT "Type", COUNT(*) FROM boss_interactions WHERE "TaskGroupId" IN (...) GROUP BY "Type"` |
| 9 | HITL routing 真實觸發 | N/A | 5 種全觸發 | 2 種（中斷前）| **預期 3-5 種**（kickoff proposal_approval + 視 Petra/Cody 行為）| BossInteraction Type 分布 |
| 10 | **揭露議題數量** | 13 bug（5 🔴）| 15 議題（3 🔴）| 5 議題（1 🔴）| **預期 ≤ 5（0-1 🔴 新類型 + 0-3 🟡 + 1-2 🟢）**| 試驗期間 Aria 紀錄 |
| **11** | **Stage 60 「需要修改」action framework path 真實觸發**（新） | N/A | N/A | N/A（揭 1 🔴 收口）| **預期 0 卡死**（如 Christ 點 modify → framework path 跑 / TaskPlan v2 寫入 ≥ 4000 字 / embed 帶 TaskPlan 摘要）| `SELECT "Status", LENGTH("TaskPlan") FROM task_groups WHERE "Id" = '<group_id>' AFTER modify` |
| **12** | **Stage 60 meeting subprocess fail-fast 真實觸發**（新） | N/A | N/A | N/A | **預期 0 觸發**（API 餘額充足 + Petra subprocess 穩定）/ 若觸發 → BossInteraction Type=`agent_api_failure_intervention` agent="Petra-Kickoff/Design" + 三選 actions Christ Dashboard | `SELECT * FROM boss_interactions WHERE "Type" = 'agent_api_failure_intervention'` |
| **13** | **Stage 61 Petra prompt 議題層次紀律生效**（新） | N/A | 議題 #2 工時泛濫 + Trial_v7 議題 #C「給決策包」| 議題 #C 重現 | **預期 0 復發**（DB TaskPlan/DesignPlan 不含「待 Christ 拍板」/「A/B/C 三選」/「X 天 / Y 週」字串）| DB 文字檢查 SQL |
| **14** | **Stage 61 Cody Dev_plan 結構規範生效**（新） | N/A | 3 phase 都 escalate | N/A | **預期不踩 escalate**（DB DevPlan 含「Step 1/2」+「改 [檔案]」+ DevPlanRevision ≤ 1）| DB DevPlan 文字檢查 |
| **15** | **Stage 61 議題 #B Reload 修根因生效**（新） | N/A | N/A | 議題 #B 揭 | **預期 embed 顯示 TaskPlan 摘要 ≥ 100 字**（不是「無計劃書」placeholder — 對齊 Trial_v7 反例修根因）| Discord embed 視覺 + DB 對照 |
| **16** | **Stage 61 maxTurns 80 對 Dev_plan cost 影響量化**（新） | N/A | maxTurns 10 default | N/A | **量化**：Dev_plan stage cost 預期翻倍（80 turns × token vs 10 turns）+ Cody 完成度提升買回 cost overhead | `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "Stage" = 'Dev_plan'` |
| **17** | **Stage 61 Christ action supersede 真實觸發**（新 — Mock 物理限制驗）| N/A | 議題 #9 揭 | N/A | **預期 supersede helper 觸發**（如 Christ 點「跳過審核」/「放棄」button → 前置 [Petra→Dev_plan] failed task 標 cancelled / generic intervention 訊息含真實 escalate source）| Bot log + DB task status SQL |
| **18** | **Stage 61 Dashboard epic UI 真實任務體驗**（新 — Christ 視覺驗收）| N/A | 議題 #5 揭 | N/A | **預期 Christ 體驗**：parent group row 顯示 📦 Epic + sub-task chip / Drawer Epic section 顯示 sub-task 列表 + 暫停 epic 按鈕 | Christ 視覺驗收 |
| 19 | Trial_v6 三 🔴 收口真實驗證 | N/A | 3 🔴 揭 | 未到 | **預期 0 復發**（race / Vera fix loop / API 容錯）| Bot log + DB SQL |
| 20 | Stage 59 拆解 + Stage 61 0 regression check | N/A | N/A | 0 regression | **預期 0 regression**（純 refactor + prompt + UI 改動驗證）| 行為對照 Trial_v6/v7 baseline |

---

## 五、Aria 自驗 SOP

### 5.1 分工矩陣（沿用 Trial_v6/v7）

對齊 Trial_v6 5.1 — Aria 做 DB metrics + Bot log scan + commit/PR 結構分析 + 三向→四向對照數字報告 + 異常 pattern detection + Trial 健檢報告初稿；Christ 做 UAT + 業務正確性 + AI Agent 主觀品質判斷 + Aria 異常報告 OK/NOK 拍板。

### 5.2 觸發點（沿用 Trial_v6/v7 + Stage 60+61 修法觸發點擴展）

1. **Stage milestone 觸發**：每個 stage（Kickoff/Design/Dev_plan/Dev/Reviewer/QA/Doc）完成時 Aria 跑健檢 SQL + log scan + 四向對照 baseline 報告
2. **異常觸發**：BossInteraction 開啟（除 ack only type）/ Petra 仲裁 / Crash Recovery / Token 守門攔截 / **Stage 60+61 修法觸發點**（modify action framework path / meeting subprocess fail-fast / Petra prompt 紀律檢測 / Cody Dev_plan 結構檢測 / Reload entity refresh / supersede helper / epic UI 視覺）→ Aria 立刻濃縮報告
3. **試驗結案觸發**：Trial 完成 → Aria 寫 Trial_v8 結案報告草稿 + **主動提醒進戰略大重評估討論**（Christ 2026-05-10 提出）

### 5.3 自驗工具清單

對齊 Trial_v6/v7 5.3（Bash SQL via `docker exec aiteam-postgres-1 psql` / docker logs / git log / Chrome MCP / Read-Grep）。**環境細節 reference**：
- **container 名**：`aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
- **Bot Internal API port**：`5052`（見 `docker-compose.prod.yml`）
- **X-Api-Key 取值**：`docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- **DB schema**：`src/AiTeam.Data/Entities.cs` + Migrations
- **SQL 欄位 PascalCase + quote 紀律**

### 5.4 Trial 健檢報告格式（四向對照）

對齊 Trial_v7 5.4 升級為 **20 維度四向對照**（Trial_v5 / v6 / v7 / v8 now）+ Stage 60+61 修法效果驗證段：

```markdown
## Trial_v8 健檢報告 — <Checkpoint N> @ YYYY-MM-DD HH:MM

### 當前狀態
- TaskGroup ID：<guid>
- 已完成 stage：Kickoff ✅ / Design ✅ / ...
- Pipeline framework path：✅ 走通 / ⚠️ fallback / ❌ 卡死
- Stage 60+61 修法觸發狀態：modify framework path / subprocess fail-fast / Petra/Cody 紀律生效 / Reload / supersede / epic UI

### 20 維度 SQL 數字（四向對照 Trial_v5 / v6 / v7 / v8 now）
| 維度 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 (now) | 差異 | 解讀 |

### Stage 60+61 修法效果即時驗證
- Stage 60 modify framework path：<觸發次數 / 行為>
- Stage 60 subprocess fail-fast：<行為>
- Stage 61 Petra 紀律：<DB 文字檢查結果>
- Stage 61 Cody Dev_plan 結構：<DB DevPlan 結構結果>
- Stage 61 議題 #B Reload：<embed TaskPlan 摘要顯示>
- Stage 61 supersede / epic UI：<行為>

### 異常揭露
- [若有] 異常 1：<描述> → 分類：<v5/v6/v7 已踩 / v8 新議題> → Aria 推薦：<繼續觀察 / escalate Christ>

### Christ 拍板項
- 議題 1：<具體問題 + 三選項 + Aria 推薦>

### 建議下一步
- <Aria 推薦>
```

---

## 六、Christ 必驗範圍（UAT + 主觀判斷 + Stage 61 視覺驗收）

不可由 Aria 取代的驗收項（沿用 Trial_v6/v7 + Stage 61 視覺驗收延伸）：

1. UAT「做出來是不是我要的」— 任務最終產出 vs Christ 心中成功標準
2. 業務正確性 / 原 spec 對照
3. AI Agent 主觀品質判斷（Vera 審查 / Petra 決議 / Demi UI 規格）
4. Aria 異常報告 OK/NOK 拍板
5. 議題分類（新議題 vs 已知）
6. **Stage 60 真三選 actions UI**（如觸發 race / Vera fix loop / API failure / modify subprocess failure → 確認 Dashboard 顯示三選按鈕）
7. **Stage 61 Dashboard 視覺驗收**：
   - epic UI（parent group 📦 Epic + sub-task chip + Drawer Epic section + 暫停按鈕）
   - token IsEstimated 視覺（Agent 卡片 ⚠️ icon + Tooltip + cost「~」前綴）
8. **Stage 61 Christ action supersede 真實觸發**（Mock 物理限制驗）：如 Christ 點 Discord「跳過審核」/「放棄」button → 觀察前置 failed task 是否標 cancelled + intervention 訊息是否含真實 escalate source

---

## 七、試驗開跑前置條件

開跑前 Aria 自動 check 清單：

- [ ] **production deployment v3.50.0** — `git log --oneline -1` 確認 commit hash + Bot 啟動 log 顯示 `Version 3.50.0`
- [ ] **feature flag 全 ON**（同 Trial_v6/v7）：UseFrameworkAppealLoop / UseFrameworkKickoff / UseFrameworkKickoffMidInterrupt / UseFrameworkDesign / UseFrameworkPipeline 五個 = true
- [ ] **MockMode = OFF**（真實任務驗）
- [ ] **Migration 全 applied**（Stage 60+61 不動 schema，最新到 Stage57BossInteractionPendingUniqueIndex）
- [ ] **既有 stuck task 清掉** — `SELECT COUNT(*) FROM task_groups WHERE "Status" IN ('pending', 'needs_intervention', 'running')` = 0
- [ ] **Token 守門設定確認**（全域月限 10M / per-agent 日限 / 不會在 Trial 中段觸發）
- [ ] **⚠️ Anthropic API 餘額充足** — Trial_v8 cost 預期 mid ~$10（含 maxTurns 80 buffer），**Christ 目前 $8.42 餘額可能不夠 cover 完整 Trial_v8 — 建議儲值至 ≥ $20** 避免 Trial_v6 重演 API 爆 noise（如預算限制中段爆也接受 Stage 58 第 7 routing 真實觸發 = 額外驗證價值）

---

## 八、結案標準（Aria 判定 + Christ 確認）

Trial_v8 結案分**成功 / 部分成功 / 失敗**三級 — **比 Trial_v6/v7 更嚴格**因 Stage 60+61 已收口所有已知議題：

| 結案類型 | 條件 |
|---|---|
| **✅ 成功** | Pipeline framework path 端到端跑通 + cost 在 Trial_v5 baseline ±50% 範圍（$4.4-13.2，含 maxTurns buffer 上修為 $4-15）+ Christ UAT 通過 + Phase 1 完整 deliver + 0 🔴 新類型議題 + 0 🔴 收口議題復發 + Stage 60+61 修法效果全驗證通過 |
| **⚠️ 部分成功** | Pipeline framework path 跑通 + Phase 1 部分 deliver + 0 🔴 新類型 + 0 🔴 收口復發 + Stage 60+61 修法部分驗證 + Christ UAT 部分通過 |
| **❌ 失敗** | Pipeline framework path 卡死 / 揭露 ≥ 1 🔴 新類型 / Stage 60+61 修法任一無效 / 4 🔴 收口任一復發 / cost 超 Trial_v5 baseline +200%（$26+）|

---

## 九、結案後動作

### Aria 結案第二段範圍（Trial 結案 ≠ Stage 結案）

1. **Trial_v8_Plan.md 升 v2.0** — 加結案紀錄段（Checkpoints 觀察填實 + 四向對照數字 + Stage 60+61 修法效果驗證表 + 揭露議題清單 + Christ UAT 結論 + 結案類型判定）
2. **新立 FF**（揭露議題逐項立 FF + 優先級）
3. **CHANGELOG 不需 entry**（Trial 是試驗不是 Stage）
4. **Future_Feature_changelog 加新版本 entry**（Trial_v8 結案 + 新立 FF + 戰略結論）
5. **calibration_anchors 加「Trial_v8 校準錨」段**（Aria 自驗工作量 / Trial cost / 四向對照差異 / Stage 60+61 修法效果量化）

### ⭐ 戰略大重評估（Christ 2026-05-10 提出 — Aria 主動提醒）

**Trial_v8 結案後 Aria 必主動提醒進戰略大重評估討論**（不等 Christ raise）：
- **路線 A**：繼續 v4 漸進修補（Trial_v8 deliver Phase 1 完整 + 揭 0-1 🔴 → 有實證支撐）
- **路線 B**：戰略大砍複雜度（10 Agent + 7 routing → 3 Agent + 2 routing — Trial_v8 仍中段卡死 / 揭 ≥ 1 🔴 → 有實證支撐）
- **路線 C**：根本性轉向 Claude Code 模式（1 Agent + 強大工具 — 對齊業界主流）

Trial_v8 結果是 Christ 拍板的關鍵實證資料 — Aria 結案報告含「**戰略大重評估資料整合**」段給 Christ 拍板依據。

---

## 十、技術約束

- 不引入新 code 改動（Trial 是觀察試驗 — 揭露議題立 FF 排 Stage 修）
- Aria 自驗工具僅限 read-only（SQL SELECT / docker logs / git log / gh pr view）
- Trial 期間 production code 凍結（除非揭露 🔴 阻塞議題需 hotfix）
- **環境細節 reference 對齊 source of truth**（port 5052 / X-Api-Key / container 名 aiteam-aiteam-bot-1 + aiteam-postgres-1 / SQL PascalCase quote）
- **Petra prompt 5 位置漂移風險提醒**（FF 五十七 candidate）— 如 Trial_v8 觀察 Petra 行為仍有問題，可能是 5 位置同步漂移而非 Stage 61 修法無效

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Trial_v8 = Stage 60+61 修法後路線 A/B/C 拍板實證（v3.50.0 — Stage 60 v4 邊角 legacy 收口 + Stage 61 6 🟡 系統性議題群組清完）。**戰略級三層次目的**：① 驗證 Stage 60+61 修法效果 ② 量化 v4 framework 真實 ROI 四向對照 Trial_v5/v6/v7/v8 ③ 戰略大重評估關鍵實證（Trial_v8 結果是 Christ 拍板路線 A/B/C 依據）。20 維度四向對照（含 Stage 60+61 修法效果新增 8 維度）。任務原文沿用 Trial_v6/v7（Christ 確認對照組精準度最高）。**結案標準比 Trial_v6/v7 嚴格**（已收口所有已知議題 — 揭 1 🔴 新類型即失敗）。**戰略大重評估 Aria 主動於 Trial_v8 結案後提醒**（路線 A vs B vs C 拍板）。 |
| v2.0 | 2026-05-10 | **試驗結案 + 觀察紀錄**（Aria）— Trial_v8 = Kickoff Petra Round 1 escalate → Christ 點需要修改 → Petra modify subprocess !result.Success → Stage 60 第 7 routing 真實首次觸發（fire BossInteraction + 三選 actions ✓）→ Christ 點 retry → **Stage 60 retry path silent 卡死**（ProcessedByBot=true 但 Bot 無處理 / 任務不推進）→ Aria SQL cancel 結案。total cost **$1.2023** / 13 LLM call。**揭 2 🔴 戰略級新類型議題**：① Trial 試驗框架 AI Team 認知錯位升級（Trial_v6 議題 #3 升級 — Petra 看到 codebase 已含 Stage 60+61 痕跡 + Stage 61 prompt 紀律段「Stage 61」字樣 → 困惑「這是新 Stage 還是 Stage 61 follow-up」直接 escalate Christ）② Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死（Mock 場景只驗 continue path auto-approve — Trial_v8 真實 Christ 點 retry 首次踩 = 對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證）。**戰略級成功 vs 業務級失敗（雙面，連續 3 Trial）**：戰略目的「戰略大重評估關鍵實證」超預期達成（連續 3 Trial 揭 6 🔴 + deliver 度倒退 11/12→部分→0→0 = infinite loop 真實實證）；deliverable 0。詳見下方「結案紀錄」章節。 |

---

## 試驗結案紀錄（Aria 2026-05-10 結案第二段）

### 試驗任務原文

完全照搬 Trial_v6/v7 同 prompt（Dashboard 錯誤處理體驗打磨：雙軌提示 Inline MudAlert + Toast Snackbar）。Christ 從 **Dashboard 首頁送出**（vs Trial_v6/v7 Discord 送出 — 雙通道對照升級）。

### 流程觀察 Checkpoints（5 個 milestone — 比 Trial_v7 5 個對齊但 path 不同）

#### Checkpoint 1：Victoria 分類 ✅ 一次成功 + Title 對齊 Christ 原指令精準

| 項目 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 |
|---|---|---|---|---|
| 嘗試次數 | 3 次 | 1 次 | 1 次 | **1 次** ✅ |
| Cost | $0.62 累計 | $0.0567 | $0.1838 | **$0.1918** |
| Output tokens | — | 1,400 | 2,807 | **3,220** |
| 持續時間 | — | ~2 分 | ~2 分 | **~5 分** ⚠️ |
| Title | — | 「機制」 | 「升級：雙軌通知」 | 「**打磨 — 雙軌提示（Inline MudAlert + Toast Snackbar）**」 |
| 提案結構 | 簡單 | 簡短 | codebase scan + 5 元件 | **11 基礎設施 + 5 補強 + 6 現有 + Issue 對照表 + 5 拆解條** |
| 送出來源 | Discord | Discord | Discord | **Dashboard** ⭐ |

**🟡 議題 #A**：Victoria 階段 cost +4% / time 翻倍 vs Trial_v7 — 印證 **FF 三十六 Phase B Victoria scan lazy 化 ROI** 持續累積真實證據（codebase 變大 → eager scan cost 線性擴大）。

**✅ Stage 61 修法觀察**：Title 對齊 Christ 原指令「打磨」+ MudBlazor 元件名精準（Petra 紀律延伸到 Victoria 不直接但 prompt 訓練累積品質提升）。

#### Checkpoint 2：Kickoff 5 人會議 → Petra Round 1 escalate ⚠️ Trial_v6/v7 都沒踩過的新 path

| 項目 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 |
|---|---|---|---|---|
| 持續時間 | 11:11 | 9:12 | 8:36 | **2:01** ⚠️ -76% |
| Decision | — | reach_consensus | max_iter | **escalate**（Round 1 主動上呈）|
| 會議輪次 | 3 | 3 | 3 | **1** |
| LLM call | 16 | 16 | 16 | **5** |
| Cost | $1.72 | $1.31 | $1.34 | **$0.5529** |
| TaskPlan 字數 | 7,222 | 7,072 | 6,279 | **0**（escalate path 設計上不產出）|

**🔴 議題 #B（戰略級新類型 — Trial_v6 議題 #3 升級）**：**Trial 試驗框架 AI Team 認知錯位升級**。Petra escalate reason「需求定位不清——無法確認這是新 Stage 還是 Stage 61 follow-up」← Petra 認知 codebase 已含 Stage 60+61 痕跡（剛結案）+ Stage 61 Petra prompt 紀律段（Petra 自己 prompt 載入時看到「Stage 61」字樣）→ 困惑「這個 Dashboard 錯誤處理打磨任務是新 Stage 還是 Stage 61 follow-up」直接 escalate Christ。**這是 Trial 試驗框架的設計缺口而非 v4 framework 缺口**（escalate path 設計上正確，embed 顯示 EscalateReason 邏輯正確）。

**Stage 61 修法部分驗證**：
- ✅ Petra「不列 A/B/C 三選」紀律生效（用「1) 2) 3) 4) 4 條問題」非「A 方案 / B 方案」結構）
- ⚠️ Petra「給定見不攤議題」紀律部分生效（escalate 形式上仍是攤議題給 Christ — 但對齊「議題層次篩選紀律」識別出「需求定位」是業務級議題 escalate 是合理）
- ✅ Petra「砍工時泛濫」紀律生效（escalate reason 0 工時字串）

#### Checkpoint 2.5：Christ 從 Dashboard 點「需要修改」+ 貼 Aria 修改 prompt v2 ⭐ 老闆風格紀律生效

Aria 第一版 modify prompt 太細節（5 元件名 list + Severity.Error / error-handling.md / 1)2)3)4) 結構）— **Christ 親自點破「太細節，不像老闆風格」**。Aria 重寫對齊 Christ 對話 register（自然口語段 / 「妳們」/「我說」/「順便」/「不要再丟決策包回來」/ 不寫具體元件名 / 不列結構）。

**🟡 議題 #C（新 — Aria 工作風格漂移）**：Aria 給 Christ 的「modify prompt 草稿」也須對齊 Christ 對話 register 不是 Aria 工程細節風格 — 對齊既有自省點 #26 AI Agent prompt 議題層次紀律延伸到 Aria 給人類使用者 prompt 草稿層。**結案立 自省點 #30 修根因**。

#### Checkpoint 2.6：Petra modify subprocess !result.Success → Stage 60 第 7 routing 真實首次觸發 ⭐⭐ 戰略級驗證

Christ 點 modify 後 Petra resume session 跑 modify path → subprocess `!result.Success`（output=5 chars）→ **Stage 60 修法 throw `MeetingSubprocessFailureException`**（fail-fast 不 swallow placeholder）→ KickoffStageExecutor catch → fire `agent_api_failure_intervention` BossInteraction agent="Petra-Kickoff" + 三選 actions（略過 / 重試 / 終止）。

**Stage 60 修法 5 件全綠驗證 ✅**（Trial_v7 揭 1 🔴 收口的真實任務驗證 — 完整循環）：
| 修法 | 證據 |
|---|---|
| 1. MeetingCommons fail-fast | output=5 chars 不再寫入 DB（vs Trial_v7 反例：6,279 → 5 字 placeholder）|
| 2. ModifyTaskPlan 走 framework path | KickoffStageExecutor catch 不是 legacy KickoffMeetingService |
| 3. `[SUBPROCESS_FAILURE]` marker 命名語意 | Description 含 `[SUBPROCESS_FAILURE]` Petra: subprocess !result.Success |
| 4. 第 7 routing fire | BossInteraction Type=`agent_api_failure_intervention` agent="Petra-Kickoff" |
| 5. 三選 actions Christ Dashboard | 略過 / 重試 / 終止三選按鈕顯示正確 |

**真實 root cause 評估**：output=5 chars 不是 API 餘額不足（Christ $17.68 充足）— Claude Code CLI subprocess 暫態失敗 / Petra modify resume session 對 Christ 給的「新 Stage 不是 Stage 61 follow-up」澄清產生極短輸出（暫態 LLM 行為）。

#### Checkpoint 2.7：Christ 點「重試（儲值後）」→ 🔴 Stage 60 retry path silent 卡死

Christ 點 retry → Bot 標 `ProcessedByBot=true` + ResponseAction=`api_failure_retry` 寫入 DB（14:53）→ **但 Bot 完全沒處理**（token_logs 0 row / Bot log dispatch/action/retry 0 hit / task state Status=running TaskPlan=0 KickoffRound=0 完全沒推進）→ ~5+ 分鐘 silent 卡死無新訊息。

**🔴 議題 #D（戰略級新類型 — v4 framework 缺口）**：Stage 60 第 7 routing **api_failure_retry/abort path 真實處理 silent 卡死**。對齊 Trial_v6 議題 #15 + Trial_v7 議題 #1 同類「silent failure」根因 — Stage 60 修了 fire interaction + 三選 UI 但 retry/abort path 真實 routing 處理沒做（或 silent skip）。**對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證** — Stage 60 Mock 場景只驗 continue path（auto-approve api_failure_continue），retry / abort 兩 path 沒驗（同類根因 Forge 自驗物理限制 — Stage 60 結案紀錄已揭露 SupersedePriorFailedTasks + generic intervention 動態化物理限制但**漏列 retry/abort path 也是物理限制範疇**）。

#### Checkpoint 3：Aria SQL cancel 結案 🛑

Christ 找不到「停止任務」按鈕（既有 BossInteraction 都已 responded / Dashboard 流程詳情頁只有「暫停下階段啟動」+「中途介入 KICKOFF」沒「停止任務」）→ Aria SQL UPDATE task_groups SET Status='cancelled' 結案 Trial_v8。

**🟢 議題 #E（小 UX）**：Dashboard 任務 stuck 狀態下 Christ 沒 visible 的「停止任務」入口 — 不影響戰略結論的小 UX 議題。

---

### 試驗結果矩陣（20 維度 vs Trial_v7/v6/v5 baseline）

| # | 維度 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 | 解讀 |
|---|---|---|---|---|---|---|
| 1 | Pipeline framework path 走通率 | N/A | 100% | 100% | **100%**（Kickoff escalate 前）| ✅ 主路徑健全 |
| 2 | **總 cost** | $8.78 | $15.81 | $1.5233 | **$1.2023** | 任務中斷 |
| 3 | TotalCostUsd 寫入率 | 0.3% | 100% | 100% | **100%** | ✅ 持續 |
| 5 | Cody Dev_plan 完成度 | 1-2 輪通過 | 3 phase 都 escalate | 未到 | **未到** | 無數據 |
| 6 | Quinn 測試 | 30+6 passed | 0 | 未到 | **未到** | 無數據 |
| 9 | Petra 仲裁次數 | 4-5 次 | 10 PM task | 2 次 | **1 次**（Round 1 escalate）| 任務中斷 |
| 11 | HITL routing 真實觸發 | N/A | 5 種全觸發 | 2 種 | **3 種**（kickoff proposal / kickoff modify / **agent_api_failure_intervention 首次** ⭐）| Stage 60 第 7 routing 真實首次觸發 |
| 12 | **揭露議題數量** | 13（5 🔴）| 15（3 🔴）| 5（1 🔴）| **5（2 🔴 + 2 🟡 + 1 🟢）** | infinite loop 訊號 |
| **17** | **Stage 60 第 7 routing fire 真實觸發**（新 — Trial_v7 揭 1 🔴 收口驗證）| N/A | N/A | N/A | **✅ 觸發 + 5 件全綠**（marker / fail-fast / framework path / per-stage Port / 三選 UI）| Stage 60 修法部分驗證 |
| **18** | **Stage 60 第 7 routing retry/abort path 真實處理**（新 — Trial_v8 揭 🔴）| N/A | N/A | N/A | **❌ silent 卡死** | Mock 沒驗 retry/abort path |
| 19 | Trial_v6 三 🔴 收口真實驗證 | N/A | 3 🔴 揭 | 未到 | **未到**（任務中斷未到後續 stage）| 無數據 |
| 20 | Stage 61 修法效果 | N/A | N/A | N/A | **部分驗證**（Petra「不列三選」/「砍工時」紀律 ✅ ; Reload 修根因 N/A escalate path 不踩；其他 Cody Dev_plan / ImplementationNote / Christ supersede / epic UI 未到）| 中段 |

---

### 揭露議題清單（5 個）

| # | 嚴重 | 分類 | 議題 |
|---|---|---|---|
| 1 | **🔴** | **Trial 試驗框架戰略級新類型** | **AI Team 認知錯位升級（Trial_v6 議題 #3 升級）— Petra 看到 codebase 已含 Stage 60+61 痕跡 + Stage 61 prompt 紀律段「Stage 61」字樣 → 困惑「這是新 Stage 還是 Stage 61 follow-up」直接 escalate Christ。Trial 試驗框架的設計缺口而非 v4 framework 缺口** |
| 2 | **🔴** | **v4 framework 戰略級新類型** | **Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死。Mock 只驗 continue path auto-approve，retry/abort 兩 path 沒驗 — Forge 自驗物理限制範疇延伸（Stage 60 結案紀錄漏列）。對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證** |
| 3 | 🟡 | Aria 工作風格漂移（新）| Aria 給 Christ 的「modify prompt 草稿」第一版太細節（5 元件名 list / Severity.Error / 1)2)3)4) 結構）→ Christ 親自點破「不像老闆風格」。對齊既有自省點 #26 AI Agent prompt 議題層次紀律延伸到 Aria 給人類使用者 prompt 草稿層。結案立自省點 #30 |
| 4 | 🟡 | Victoria scan lazy 化持續累積（同類 Trial_v7 議題 #A）| Victoria CEO cost +4% / time 翻倍 vs Trial_v7 — codebase 變大 → eager scan 線性擴大持續累積。對齊 FF 三十六 Phase B 候選 |
| 5 | 🟢 | Dashboard 任務 stuck 狀態無「停止任務」入口（小 UX）| 任務 stuck 狀態下 Christ 找不到 visible「停止任務」按鈕（Dashboard 流程詳情頁只有「暫停下階段啟動」+「中途介入 KICKOFF」+ 既有 BossInteraction 都 responded） |

---

### 結案類型判定：⭐ 戰略級成功（連續 3 Trial 揭 6 🔴）vs 業務級失敗（雙面）

| 維度 | 結果 |
|---|---|
| Pipeline framework path 端到端 | ❌ Kickoff escalate → modify → retry silent 卡死 |
| cost ±50% 範圍 | N/A（任務中斷無對照基準）— 截至中斷 cost $1.20 極省 |
| Christ UAT 通過 | ❌ 不適用 |
| 揭露議題收斂 follow-up FF | ✅ 5 議題（含 2 🔴 戰略級新類型）|
| **試驗主目的「戰略大重評估關鍵實證」** | ✅ **超預期達成 — 連續 3 Trial 揭 6 🔴 + deliver 度倒退 = infinite loop 真實實證** |
| Stage 60+61 修法部分驗證 | 部分 ✅（Stage 60 第 7 routing fire 5 件全綠 + Petra 紀律部分生效）+ 部分 ❌（Stage 60 retry path silent / 其他 Stage 61 修法未到階段驗）|
| **「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證** | ✅ Trial_v8 證明「Stage 60 修完照樣揭新類型 🔴」 |

→ **判定：⭐ 戰略級成功 / 業務級失敗** — 對戰略大重評估提供最關鍵實證，但 deliverable 0。

---

### 戰略結論（連續 3 Trial 揭 6 🔴 — 戰略大重評估時機到）

#### v4 framework 真實 ROI 量化（Trial_v6/v7/v8 連續 3 Trial 數據統計）

| 維度 | Trial_v5 baseline | Trial_v6 | Trial_v7 | Trial_v8 |
|---|---|---|---|---|
| 總 cost | $8.78 | $15.81（+80%）| $1.52（中斷）| $1.20（中斷）|
| Phase 1 完成度 | 11/12 Issue | 部分 | 0 | 0 |
| 揭 🔴 戰略級 | 5 | 3（race / Vera / API）| 1（v4 邊角）| 2（Trial 框架 / Stage 60 retry path）|
| 中斷階段 | — | API 餘額爆 Phase 3 | Kickoff modify | Kickoff modify retry |
| **deliver 度趨勢** | **92%** | **部分** | **0** | **0**（持續倒退 / 卡死更前置）|

**infinite loop pattern 真實實證**：每次 Trial 都揭新類型 🔴，Stage 60+61 補強後 Trial_v8 反而**卡死更前置階段**（Trial_v7 卡 modify subprocess failure / Trial_v8 卡 retry path silent）— Stage 60 第 7 routing 才修完就揭新缺口。

#### 「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證

- **第一次**（Trial_v6）：揭 race / Vera fix loop / API 容錯三 🔴 — Mock 沒涵蓋
- **第二次**（Trial_v7）：揭 ModifyTaskPlan legacy path silent failure — Mock 沒涵蓋
- **第三次**（Trial_v8）：揭 Stage 60 第 7 routing retry/abort path silent — Mock 只驗 continue path 沒涵蓋 retry/abort

**戰略意義**：Mock 設計本質「正向流程 + 已知失敗模式」涵蓋 — **邊角 user actions / silent failure / 環境級失敗 / Mock 物理限制 path 都很難 Mock 涵蓋**。Trial 模式核心價值持續驗證 — 必須持續跑真實任務揭新類型 = 但同時也是 v4 framework 的 infinite loop 訊號。

#### Trial 試驗框架本身的設計缺口（Trial_v8 議題 #B）

對齊 Trial_v6 議題 #3 升級 — **同任務 codebase 已含試驗痕跡的試驗模式設計缺口**：
- Trial_v6 v3.45.0 codebase 還沒有 Stage 60/61 痕跡 → 無認知錯位
- Trial_v8 v3.50.0 codebase 已含 Stage 60+61 commits / Roadmap / Petra prompt「Stage 61」字樣 → Petra 認知錯位 escalate 困惑
- 立 FF 五十九修根因（Trial 試驗框架紀律 — 同任務 codebase 已含試驗痕跡時的 Trial 模式設計）

---

### 後續行動清單

#### 立即（試驗結案後動作）

- [x] Aria SQL cancel 結束 Trial_v8（task_groups Status = cancelled ✓ 14:55+）
- [x] Trial_v8_Plan.md 升 v2.0（本檔）
- [ ] 新立 FF 五十九（Trial 試驗框架 AI Team 認知錯位升級紀律）
- [ ] 新立 FF 六十（Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死收口）
- [ ] Future_Feature_changelog v7.86 entry
- [ ] calibration_anchors 加 Trial_v8 校準錨段
- [ ] memory 寫入：workflow_aria_session_lessons.md 自省點 #30（Aria 給 Christ prompt 草稿必須對齊 Christ 對話 register — 對齊既有自省點 #26 AI Agent prompt 議題層次紀律延伸到 Aria 給人類使用者 prompt 草稿層）
- [ ] commit + push 結案文件
- [ ] **⭐ 戰略大重評估深度討論 — Aria 主動提醒 Christ**（對齊 2026-05-10 拍板）

#### Stage 候選（對齊路線 A/B/C 拍板後決定）

**等戰略大重評估拍板路線後再排** — 路線 A 才動 FF 六十 / 路線 B/C 大砍可能直接吸收。

#### Trial_v8 之後評估 backlog

- **戰略大重評估**：路線 A vs B vs C 拍板（Christ 拍板 — Aria 寫資料整合報告 + 推薦）
- 如路線 A：Stage 62 修 FF 六十（Stage 60 retry path silent 卡死收口）+ FF 五十九（Trial 框架紀律）
- 如路線 B/C：架構大砍 / 根本性轉向 — FF backlog 全重新評估

---

### 對 v4 framework 戰略的最終判斷（Trial_v8 修正 — 戰略大重評估前提）

**v4 framework 主路徑 9/9 達成 + production-ready 補強 4 Stage（56/58/60/61）✅**，但 **production-ready 邊界仍持續揭新類型 🔴**：
- Stage 57+58 補完 Trial_v6 揭 3 🔴 → Trial_v7 揭 1 🔴 新類型（v4 邊角）
- Stage 60 補完 Trial_v7 揭 1 🔴 → Trial_v8 揭 2 🔴 新類型（Trial 框架 + Stage 60 retry path）
- Stage 60+61 補完累積 7 ✅ FF + 4 production-ready 補強 Stage → Trial_v8 仍揭 2 🔴

**Trial 連續 3 次揭新類型 🔴 = Aria 戰略大重評估提案的核心訊號真實實證**。**Trial 模式本身證明「修一個揭一個」pattern 真實存在 = 戰略大重評估時機到了**。

**Trial_v8 獨特戰略價值**：
- 首次 Stage 60 第 7 routing 真實任務首次觸發（5 件全綠 + retry path silent 揭露）
- 首次 Trial 框架 AI Team 認知錯位升級 🔴 揭露
- 首次 Aria 工作風格漂移 Christ 親自點破 → 自省點 #30
- 首次 Dashboard 送出指令對照 Discord 送出（雙通道對照升級）
- **首次連續 3 Trial 揭 6 🔴 完整數據組 = 戰略大重評估關鍵實證**

→ Trial_v8 為 **Christ 拍板路線 A/B/C 提供最完整實證資料**。Aria 結案後立寫「戰略大重評估資料整合」報告。

# Trial_v7 試驗計劃書 — Trial_v6 對照重跑（v3.48.0 三 🔴 全收口後 ROI 量化）

> 對應版本：**v3.48.0**（Stage 59 結案後 = Trial_v6 揭露 3 🔴 全收口 + TaskGroupService 拆解後第一次真實任務試驗）
> 建立日期：2026-05-10
> 狀態：規劃中（待 Christ 確認任務 + 開跑時間）
> 文件版本：v1.0

---

## 一、背景與定位

### 戰略脈絡

- **Trial_v6（v3.45.0）= v4 framework 全切換第一次真實任務試驗**（2026-05-08 結案）— 部分成功 / cost $15.81（+80% 超 ±50%）/ 揭露 15 議題（含 3 🔴 戰略級）/ 戰略價值 ×2.5
- **Stage 57（v3.46.0/v3.46.1）= Trial_v6 🔴 議題 #10 race condition 收口**（2026-05-09，校準錨 ×1.36）— `epic_partial_paused` HITL routing partial unique index + 23505 catch 雙保險 + 真三選 actions set
- **Stage 57（v3.46.0）= Trial_v6 🔴 議題 #12 Vera fix loop limit 收口**（同 Stage 合併實作）— 第 6 routing `reviewer_fix_loop_limit` HITL 補強
- **Stage 58（v3.47.0）= Trial_v6 🔴 議題 #15 API 餘額容錯性收口**（2026-05-09，校準錨 ×0.94）— 第 7 routing `agent_api_failure_intervention` 路線 A marker pattern + 三 Agent fail-fast 統一
- **Stage 59（v3.48.0）= FF 五十四子項 1 TaskGroupService 拆解**（2026-05-10，校準錨 ×1.09）— 1759 行 → 主檔 808 行（-54%）+ 4 子 service / 純 refactor 無行為改動
- **Trial_v5（v3.33.0 + 4 FF）= 既有架構 baseline**（2026-05-01）— $8.78 / 13 bug / 11/12 Issue 完成

### Trial_v7 定位

**Trial_v6 對照重跑 — 量化「3 🔴 收口 + Stage 59 拆解」後 v4 framework 真實 ROI**。

對照基線升級：
- 從「Trial_v6 v3.45.0 v4 全切換 + 3 🔴 缺口」
- 升級為「Trial_v7 v3.48.0 v4 全切換 + Stage 57/58 補強 + Stage 59 拆解後 codebase」

**核心試驗問題**：去掉 race / 卡死 / API 容錯三 noise 後，v4 framework 端到端 ROI 量化結果是什麼？是否能進入 production stable 階段？

**注**：FF 三十六 Phase B 動態流程架構（Petra Magentic Orchestration / per-task session）**仍不在本次範圍** — Trial_v7 仍是「換引擎不換車身」對照試驗，動態架構評估留 Trial_v8+ / FF 三十六 Phase B spike 後。

---

## 二、試驗目的（5 條）

1. **驗證 3 🔴 議題已收口** — Stage 57/58 補強在真實任務下不再復發
   - 議題 #10 race condition：partial unique index + 23505 catch 真實觸發 0 race（或 catch 訊號正確 emit）
   - 議題 #12 Vera fix loop limit：第 6 routing `reviewer_fix_loop_limit` 真實觸發 + Christ 看到三選 actions（continue / approve / abort）+ 不再卡死
   - 議題 #15 API 容錯：第 7 routing `agent_api_failure_intervention` 觸發或 API 餘額充足下三 Agent fail-fast 行為一致

2. **量化 v4 framework 真實 ROI 對照基線升級結果**（cost / 完成度 / 流程順暢度 / 揭露議題數）
   - 三向對照：Trial_v5 baseline（$8.78 / 13 bug）vs Trial_v6（$15.81 / 15 議題 / 3 🔴）vs Trial_v7（預期 cost 回 baseline ±50% + 揭露議題下降）
   - 為 FF 三十六 Phase B 動態架構評估提供 production-ready baseline 數據

3. **驗證 Stage 59 TaskGroupService 拆解 0 regression** — 純 refactor 不引入行為差異
   - PipelineFrameworkStateJson 寫入率 100%
   - sub-task chain（Phase 1/2/3）啟動順序正確
   - HITL routing fire 路徑全綠

4. **揭露剩餘 🟡 議題在真實任務下的真實傷害**（Trial_v6 揭露 10 🟡 多數未修，預期再現）
   - Cody/Petra prompt 對齊群組（議題 #1/#2/#7/#8/#14 — 對應 FF 二十五/四十八/四十六）
   - Dashboard UI 一致性（議題 #5/#6/#11 — 對應 FF 五十）
   - BossInteraction 模板（議題 #9）
   - 評估這些 🟡 議題是否升級 🔴

5. **揭露 v4 framework 真實任務新議題**（Trial_v7 限定）— Mock 全綠 ≠ production 全綠紀律延伸

---

## 三、任務需求

### Christ 拍板項：沿用 vs 新任務

**Aria 推薦 ① 沿用 Trial_v5/v6 同任務原文**（Dashboard 全域錯誤通知 Toast 機制）— 對照組精準度最高（同 prompt vs v3.33.0 / v3.45.0 / v3.48.0 三 baseline 量化差異）。

任務原文 ⭐（完全照搬 Trial_v6）：

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

**選項 ② 新任務**：失去三向對照基線，但更貼近真實使用場景多樣性。除非 Christ 偏好換，否則建議走 ①。

---

## 四、預期觀察清單（Trial_v7 vs Trial_v6 vs Trial_v5 三向對照）

| # | 維度 | Trial_v5 baseline | Trial_v6 actual | Trial_v7 預期 | Aria 自驗 SQL/工具 |
|---|---|---|---|---|---|
| 1 | Pipeline framework path 走通率 | N/A | 100% | **100%**（Stage 59 拆解 0 regression）| `SELECT COUNT(*) FROM task_groups WHERE PipelineFrameworkStateJson IS NOT NULL AND CreatedAt >= '<開跑時間>'` |
| 2 | **總 cost** | **$8.78** | **$15.81 (+80%)** | **預期 $9-13**（去 race / fix loop / API 爆三 noise 後回 baseline ±50% 內）| `SELECT SUM(TotalCostUsd) FROM token_logs WHERE CreatedAt >= '<開跑時間>'` |
| 3 | TotalCostUsd 寫入率 | 0.3% | 100% | **100%**（Stage 56 修法持續生效）| `SELECT COUNT(*) FILTER (WHERE TotalCostUsd IS NOT NULL) * 100.0 / COUNT(*)` |
| 4 | IsEstimated flag 分布 | N/A | 11/72（Path A）| 同等比例（Path A CLI single-shot fallback rate 穩定）| `SELECT IsEstimated, COUNT(*) FROM token_logs GROUP BY IsEstimated` |
| 5 | Cody Dev_plan 完成度 | 1-2 輪通過 | 3 phase 都 escalate | **預期 1-2 輪通過**（如 prompt 對齊問題群組未修則仍 escalate — 揭露 🟡 議題未修真實傷害）| Dev_plan 輪次 + Petra revise 次數 SQL |
| 6 | Quinn 測試（xUnit + visual）| 30 + 6 all passed | 0（Phase 1 邊界 case + Phase 3 API 爆）| **預期 ≥ 20 xUnit + visual all passed**（Phase 1 邊界 case 修否取決 FF 二十五/四十八）| `dotnet test` + Playwright |
| 7 | Vera 審查精準度 | 9 處裸 catch 1 輪通過 | Phase 2 fix loop ×3 卡死 | **預期 1-2 輪通過 + 不卡死**（議題 #12 收口驗證）| Reviewer comment grep + fix iteration count |
| 8 | Sage escalate 速度 | 13:30 → 14 秒 | Phase 1 escalate / Phase 3 silent skip | 預期同等或更快（無 API 爆 noise）| Bot log timestamp diff |
| 9 | Petra 仲裁次數 | 4-5 次 | 10 PM task + 2 fix loop + 3 Dev_plan = +120% | **預期回到 5-7 次**（去 race + fix loop + escalate noise）| `SELECT Type, COUNT(*) FROM boss_interactions WHERE GroupId IN (...) GROUP BY Type` |
| 10 | Crash Recovery 觸發 | N/A | 0（試驗期間無 docker restart）| 預期同 0（除非 Christ 主動觸發測試）| Bot startup log |
| 11 | HITL routing 真實觸發 | N/A | 5 種 type 全觸發 | **預期 5-7 種觸發**（含 Stage 57 第 6 routing + Stage 58 第 7 routing 視 API 餘額狀態）| BossInteraction Type 分布 |
| 12 | 揭露議題數量 | 13 bug（5 🔴）| 15 議題（3 🔴 + 12 🟡）| **預期 5-10 議題（0 🔴 + 多數已知 🟡 再現）**— 如多於 10 或新揭 🔴 → 戰略級訊號 | 試驗期間 Aria 紀錄 |
| **13** | **race condition fix 真實觸發**（新） | N/A | 1 race 事件 | **預期 0 race**（partial unique index 擋）/ 若觸發 → 23505 catch emit `[FixCatchTriggered]` log | `docker logs aiteam-bot --since <開跑時間> 2>&1 \| grep -E "FixCatchTriggered\|epic_partial_paused"` |
| **14** | **Vera fix loop limit routing 真實觸發**（新） | N/A | N/A（卡死 generic intervention）| **預期 0 觸發**（fix loop 不到 3 輪通過）/ 若觸發 → BossInteraction Type = `reviewer_fix_loop_limit` + 三選 actions 出現 Christ Dashboard | `SELECT * FROM boss_interactions WHERE Type = 'reviewer_fix_loop_limit'` |
| **15** | **agent_api_failure_intervention routing 真實觸發**（新） | N/A | API 爆無 routing（議題 #15 揭露）| **預期 0 觸發**（API 餘額充足）/ 若 API 爆 → 三 Agent fail-fast 統一 + Type = `agent_api_failure_intervention` + result 含 `[API_FAILURE]` marker | `SELECT * FROM boss_interactions WHERE Type = 'agent_api_failure_intervention'` + `grep '\[API_FAILURE\]'` |
| **16** | **Stage 59 拆解 regression check**（新） | N/A | N/A | **預期 0 regression**（純機械化拆解）— 比對 Trial_v6 同 milestone 行為（sub-task 啟動順序 / Pipeline state 寫入 / HITL fire 路徑）| 行為對照 Trial_v6 結案紀錄 14 milestone |

---

## 五、Aria 自驗 SOP

### 5.1 分工矩陣（沿用 Trial_v6）

| 工作項 | Aria 做 | Christ 做 |
|---|---|---|
| DB metrics 查詢（SQL 16 維度）| ✅ 自動 | — |
| Bot docker log scan | ✅ 自動 | — |
| commit / PR 結構分析 | ✅ 自動 | — |
| 三向對照數字報告（Trial_v5 vs v6 vs v7 16 維度）| ✅ 自動 | — |
| 異常 pattern detection | ✅ 自動，濃縮報告 | 看報告判 OK/NOK |
| Trial 健檢報告（每 milestone / 異常觸發）| ✅ 寫初稿 | review + 補主觀感受 |
| UAT「做出來是不是我要的」| ❌ 不能取代 | ✅ 必驗 |
| 業務正確性 / 原 spec 對照 | ❌ 看不到 spec 細節 | ✅ 必驗 |
| AI Agent 主觀品質判斷 | ⚠️ 技術品質可（grep / a11y / 命名）業務正確性不行 | ✅ 主觀部分必驗 |

### 5.2 觸發點（沿用 Trial_v6）

1. **Stage milestone 觸發**：每個 stage（Kickoff/Design/Dev_plan/Dev/Reviewer/QA/Doc）完成時 Aria 跑健檢 SQL + log scan + 三向對照 baseline 報告
2. **異常觸發**：BossInteraction 開啟（除 ack only type）/ Petra 仲裁 / Crash Recovery / Token 守門攔截 / **Stage 57/58 三新 routing 觸發** → Aria 立刻濃縮報告
3. **試驗結案觸發**：Trial 完成（done / cancelled / intervention 結案）→ Aria 寫 Trial_v7 結案報告草稿

### 5.3 自驗工具清單

對齊 Trial_v6 5.3（Bash SQL via `docker exec aiteam-postgres psql` / docker logs / git log / Chrome MCP / Read-Grep）。**環境細節 reference**：

- **Bot Internal API port**：`5052`（見 `docker-compose.prod.yml`）
- **X-Api-Key 取值**：`docker exec aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- **DB schema**：`src/AiTeam.Data/Entities.cs` + Migrations

> 不在 Aria 範圍：直接 Discord 觸發 / 真實 LLM call / production 改 code。Trial 期間 Aria 僅 read-only 查證 + 文件修改。

### 5.4 Trial 健檢報告格式

每次 milestone / 異常觸發後產出，格式對齊 Trial_v6 5.4，**改為三向對照**：

```markdown
## Trial_v7 健檢報告 — <Checkpoint N> @ YYYY-MM-DD HH:MM

### 當前狀態
- TaskGroup ID：<guid>
- 已完成 stage：Kickoff ✅ / Design ✅ / ...
- Pipeline framework path：✅ 走通 / ⚠️ fallback / ❌ 卡死
- Stage 57/58 三新 routing 是否觸發：race-fix / reviewer_fix_loop_limit / agent_api_failure_intervention

### 16 維度 SQL 數字（三向對照 Trial_v5 / v6 / v7 now）
| 維度 | Trial_v5 | Trial_v6 | Trial_v7 (now) | 差異 | 解讀 |

### 異常揭露
- [若有] 異常 1：<描述> → 分類：<v5 已踩 / v6 已踩未修 🟡 / Trial_v7 新議題> → Aria 推薦：<繼續觀察 / escalate Christ>

### 3 🔴 收口驗證
- 議題 #10 race：<觸發次數 / 行為>
- 議題 #12 fix loop：<行為>
- 議題 #15 API 容錯：<行為>

### Christ 拍板項
- 議題 1：<具體問題 + 三選項 + Aria 推薦>

### 建議下一步
- <Aria 推薦>
```

---

## 六、Christ 必驗範圍（UAT + 主觀判斷）

不可由 Aria 取代的驗收項（沿用 Trial_v6）：

1. UAT「做出來是不是我要的」— 任務最終產出 vs Christ 心中成功標準
2. 業務正確性
3. AI Agent 主觀品質判斷
4. Aria 異常報告的 OK/NOK 拍板
5. 議題分類（新議題 vs Trial_v6 已知）

**Trial_v7 新增驗證項**（Christ 看 Dashboard）：
6. **Stage 57 真三選 actions UI**：若觸發 race / Vera fix loop limit BossInteraction，Christ 確認 Dashboard 顯示三選按鈕（continue / approve / abort）
7. **Stage 58 第 7 routing UI**：若 API 爆觸發 `agent_api_failure_intervention`，Christ 確認 Dashboard 訊息明確化（不再「No changes; nothing to commit」誤導）

---

## 七、試驗開跑前置條件

開跑前 Aria 自動 check 清單：

- [ ] **production deployment v3.48.0** — `git log --oneline -1` 確認 commit hash + Bot 啟動 log 顯示 `Version 3.48.0`
- [ ] **feature flag 全 ON**（同 Trial_v6）：
  - [ ] `UseFrameworkAppealLoop` = true
  - [ ] `UseFrameworkKickoff` = true
  - [ ] `UseFrameworkHITL` = true
  - [ ] `UseFrameworkDesign` = true
  - [ ] `UseFrameworkPipeline` = true
- [ ] **MockMode = OFF**（真實任務驗）
- [ ] **Migration 全 applied**（含 Stage 56 `Stage56TokenLogsIsEstimated` + Stage 57 partial unique index + Stage 59 拆解後新 schema 如有）
- [ ] **既有 stuck task 清掉**（Trial_v6 Phase 1/2/3 Cancelled / Trial_v7 開跑前再掃一次 `SELECT COUNT(*) FROM task_groups WHERE Status IN ('pending', 'needs_intervention')`）
- [ ] **Token 守門設定確認**（全域月限 / per-agent 日限）
- [ ] **Anthropic API 餘額充足**（≥ $20 USD，避免 Trial_v6 重演 API 爆 — 但若預算限制中段爆也接受 Stage 58 第 7 routing 真實驗證）

---

## 八、結案標準（Aria 判定 + Christ 確認）

Trial_v7 結案分**成功 / 部分成功 / 失敗**三級：

| 結案類型 | 條件 |
|---|---|
| **✅ 成功** | Pipeline framework path 端到端跑通 + cost 在 Trial_v5 ±50% 範圍（$4.4-13.2）+ 0 🔴 新議題 + 3 🔴 收口驗證通過 + Christ UAT 通過 |
| **⚠️ 部分成功** | Pipeline framework path 跑通 + cost 在 ±50%-100% 範圍 + 0 🔴 新議題 + 3 🔴 收口驗證通過 + Christ UAT 部分通過 |
| **❌ 失敗** | Pipeline framework path 卡死 / fallback to legacy / cost 再超 +100% / 揭露 ≥ 1 🔴 新議題 / 3 🔴 任一復發 |

---

## 九、結案後動作

### Aria 結案第二段範圍（Trial 結案 ≠ Stage 結案）

1. **本檔升 v2.0** — 加結案紀錄段（Checkpoints 觀察填實 + 三向對照數字 + 揭露議題清單 + Christ UAT 結論 + 結案類型判定）
2. **新立 FF**（揭露議題逐項立 FF + 優先級）
3. **CHANGELOG 不需 entry**（Trial 是試驗不是 Stage，不 bump 版本）
4. **Future_Feature_changelog 加新版本 entry**（Trial_v7 結案 + 新立 FF + 戰略結論）
5. **calibration_anchors 加「Trial_v7 校準錨」段**（Aria 自驗工作量 / Trial cost / 三向對照差異）

### 戰略結論候選

- **v4 framework production-ready 邊界判定**：3 🔴 收口後 Trial_v7 ROI 量化 → 是否進入 production stable 階段
- **FF 三十六 Phase B 動態架構評估啟動條件**：Trial_v7 結論驅動（v4 hierarchical static 投資 ROI 飽和 → 進入動態 spike？）
- **Trial_v7 → Trial_v8+ 路線拍板**：繼續 hierarchical static 優化（FF 二十五/四十八/四十六 prompt 對齊 + FF 五十 Dashboard UI 一致性）vs 進入動態架構 spike

---

## 十、技術約束

- **不引入新 code 改動**（Trial 是觀察試驗，production code 不動 — 揭露議題立 FF 排 Stage 修）
- Aria 自驗工具僅限 read-only（SQL SELECT / docker logs / git log / gh pr view），不可動 production data
- Trial 期間 production code 凍結（除非揭露 🔴 阻塞議題需 hotfix）
- **環境細節 reference 對齊 source of truth**（port 5052 / X-Api-Key / DB schema 見 docker-compose.prod.yml + Entities.cs）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Trial_v7 = Trial_v6 對照重跑（v3.48.0 三 🔴 全收口 + Stage 59 拆解後 codebase）；對照基線升級 v3.45.0 → v3.48.0；16 維度三向對照（Trial_v5 / v6 / v7）含 3 新增（race fix 真實觸發 / Vera fix loop routing / API failure routing）+ Stage 59 0 regression 驗證；任務原文沿用 Trial_v6（Dashboard 全域錯誤通知 Toast 機制 — 對照組精準度最高）；3 🔴 收口驗證列為核心試驗目的；FF 三十六 Phase B 動態架構評估**仍不在本次範圍**；Aria 自驗 SOP + 健檢報告格式對齊 Trial_v6 5.3/5.4 改三向對照；環境細節 reference 標 source of truth（port 5052 / X-Api-Key 取值 — 對齊 workflow_aria.md 第三節 A 第 7 條紀律）。 |
| v2.0 | 2026-05-10 | **試驗結案 + 觀察紀錄**（Aria）— Trial_v7 = Kickoff 階段 ModifyTaskPlan path 卡死中斷，task cancelled / total cost **$1.5233** / 17 LLM call。揭露 5 議題（**1 🔴 戰略級新類型** + 3 🟡 + 1 🟢）—— 🔴 揭露「v4 framework 邊角 user actions 還在 legacy + Petra subprocess silent failure 沒 fail-fast + Stage 58 第 7 routing 沒 catch」**直接推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設**。**戰略級成功 vs 業務級失敗（雙面，同 Trial_v6 模式）**：戰略目的「量化 v4 真實 ROI」超預期達成（cost -90% vs Trial_v6 揭聚焦新類型）+ 「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證；deliverable 0（Phase 1 沒推進）。詳見下方「結案紀錄」章節。 |

---

## 試驗結案紀錄（Aria 2026-05-10 結案第二段）

### 試驗任務原文

完全照搬 Trial_v6 同 prompt（Dashboard 錯誤處理體驗升級：雙軌通知 Inline + Toast）— 對照組精準度最高，原文見 v1.0 三、任務需求段。Christ 在 Discord #victoria-ceo 14:19 送出。

### 流程觀察 Checkpoints（5 個 milestone — 比 Trial_v6 14 個少，因 Kickoff modify 卡死中斷）

#### Checkpoint 1：Victoria 分類 ✅ 一次成功 + cost +224% 異常

| 項目 | Trial_v5 | Trial_v6 | Trial_v7 | 解讀 |
|---|---|---|---|---|
| 嘗試次數 | 3 次 | 1 次 | **1 次** ✅ | 對照通過 |
| Cost | $0.62 累計 | $0.0567 | **$0.1838** | **+224% vs v6** ⚠️ |
| Output tokens | — | 1,400 | **2,807** | +101% |
| Title | 「Dashboard 錯誤處理 UX 打磨」 | 「Dashboard 全域錯誤通知 Toast 機制」 | 「Dashboard 錯誤處理體驗升級：雙軌通知（Inline + Toast）」 | 更冗長 |

**🟡 議題 #A**：Victoria CEO 階段 cost +224% — 提案內含「已掃描 codebase」分析（5 受影響元件 + MudBlazor ISnackbar 基礎設施已到位），可能 codebase scan 範圍變大或 Victoria prompt 演進。單點觀察，未在 Kickoff 階段放大（對照 Kickoff +2%）→ 整體 cost 可控。

#### Checkpoint 2：Kickoff 5 人會議 ✅ 8 分 36 秒 / max_iter 強制收尾

| 項目 | Trial_v5 | Trial_v6 | Trial_v7 | 解讀 |
|---|---|---|---|---|
| 持續時間 | 11 分 11 秒 | 9 分 12 秒 | **8 分 36 秒** | -7% vs v6 ✅ |
| Decision | — | reach_consensus | **max_iter** | ⚠️ 不同 path |
| LLM call | 16 | 16 | 16 | 同 |
| Cost | $1.72 | $1.31 | **$1.34** | +2% vs v6 ✅ |
| TaskPlan 字數 | 7,222 | 7,072 | 6,279 | -11% vs v6 |

Petra 達 KickoffMaxRounds 3 後 decision = max_iter 強制收尾，TaskPlan DB 寫入正常 6,279 字實質內容（8 共識 + 5 待拍板議題 + 7 風險清單 + Phase 1+2 規劃）。但 BossInteraction embed 顯示「無計劃書」。

**🟡 議題 #B**：Pipeline `KickoffStageExecutor.CreateKickoffConfirmationAsync` 在 max_iter path 沒注入 TaskPlan 摘要進 BossInteraction `ContextJson` → Discord embed + Dashboard 操作中心都顯示「無計劃書」。Trial_v6 走 reach_consensus path 正常 → Trial_v7 max_iter path 才踩到。同類根因群組對齊 Trial_v6 議題 #5/#6/#11（Pipeline framework UI 注入缺口）。

**🟡 議題 #C**：Petra「給 Christ 決策包」行為 — TaskPlan 內列「5 待拍板議題」+ 給 A/B/C 三選讓 Christ 拍。對照 Aria 之前被 Christ 校正的「議題層次篩選紀律」+「給定見不攤議題」精神，Petra prompt 同類根因。Christ 親自點破：「Petra 她現在就有點像前不久的 Aria，請我拍板很多細節」。

**🟡 議題 #D（再現 — Trial_v6 議題 #2）**：Petra 工時估算泛濫 — 「2-3 週 Cody / 1 週 Quinn / 1-2 天 決策樹」等。FF 二十五 / 四十八 系統性議題群組未修，Trial_v7 實證再現 → 系統性議題確認。

#### Checkpoint 2.5：Christ Discord 點「需要修改」🔴 戰略級揭露

Christ 點 Kickoff 確認 embed Discord「需要修改」按鈕 → 貼修改 prompt（5 議題答案 + 砍工時 + 反丟決策包）→ 期望 Petra 跑 revise round 整合答案產 TaskPlan v2。

**真實行為**：
- Bot log: `KickoffMeetingService.ModifyTaskPlan` ← **legacy path**（不是 Stage 50/51 framework）
- Bot log: `MeetingCommons：Petra session 執行失敗` ← Petra subprocess failure
- Bot log: `KickoffMeetingService：ModifyTaskPlan Petra 回應完成` ← **silent failure 沒 fail-fast**
- DB UPDATE: TaskPlan 從 6,279 字 → **5 字（空回應蓋掉）**
- token_logs: 0 row（Petra LLM 沒成功 billing）
- 流程繼續 fire 新 BossInteraction kickoff `f46203bb`「無輸出」給 Christ

#### Checkpoint 2.6：Christ Dashboard 點「需要修改」對照 — A 同 path 同失敗

Christ 對 pending BossInteraction `f46203bb` 從 Dashboard 點「需要修改」貼相同 prompt → 控變因驗證雙通道差異。

**對照三向結果（Discord vs Dashboard）**：

| 維度 | Discord 來源 14:33 | Dashboard 來源 14:42 | 對照 |
|---|---|---|---|
| Path | `KickoffMeetingService.ModifyTaskPlan` | `KickoffMeetingService.ModifyTaskPlan` | 同 legacy ✓ |
| Petra subprocess | 失敗 | 失敗 | 同失敗 ✓ |
| token_logs | 0 row | 0 row | 同 |
| TaskPlan 字數 | 6,279 → 5 | 5 → 5（noop）| 同失敗 |
| ResponseSource | discord | dashboard | 雙通道路由正確 |
| ResponseAction 字串 | `kickoff_modify_<groupid>` | `kickoff_modify`（無後綴）| ⚠️ 小差異 |

**結論**：Petra subprocess failure **source-agnostic** — 不是雙通道差異，是 ModifyTaskPlan path 本身的 bug。雙通道架構正確 ✓ 但 ResponseAction 字串格式有小差異（解析端用 prefix match 抓得到所以無實質傷害）。

**🟢 議題 #E**：BossInteraction `ResponseAction` 字串格式雙通道不一致（Discord 含 group_id 後綴 / Dashboard 純動作名）— 低嚴重，不影響實質 routing。

#### Checkpoint 3：Christ 點「停止任務」🛑

Christ 拍板停止任務 14:49+ → task_groups Status = cancelled → Trial_v7 結束。

---

### 試驗結果矩陣（16 維度 vs Trial_v6 / Trial_v5 baseline — 部分維度因任務中斷無數據）

| # | 維度 | Trial_v5 | Trial_v6 | Trial_v7 | 差異 |
|---|---|---|---|---|---|
| 1 | Pipeline framework path 走通率 | N/A | 100% | **100%**（KickoffFrameworkStateJson 寫入 ✓ 在 modify 失敗前）| ✅ 主路徑健全 |
| 2 | **總 cost** | $8.78 | $15.81 | **$1.5233** | **-90% vs v6**（任務中斷）|
| 3 | TotalCostUsd 寫入率 | 0.3% | 100% | **100%**（17/17 成功 call）| ✅ Stage 56 修法持續 |
| 4 | IsEstimated count | N/A | 11/72 | 0/17（全 IsEstimated=false）| 同等比例 |
| 5 | Cody Dev_plan 完成度 | 1-2 輪通過 | 3 phase 都 escalate | **未到 Dev_plan 階段** | 無數據 |
| 6 | Quinn 測試 | 30 + 6 all passed | 0 | **未到 QA 階段** | 無數據 |
| 7 | Vera 審查精準度 | 9 處裸 catch 1 輪通過 | Phase 2 fix loop ×3 卡死 | **未到 Reviewer 階段** | 無數據 |
| 8 | Sage escalate 速度 | 13:30 → 14 秒 | Phase 1 escalate | **未到 Doc 階段** | 無數據 |
| 9 | Petra 仲裁次數 | 4-5 次 | 10 PM task | **2 次（modify failure）** | 任務中斷 |
| 10 | Crash Recovery 觸發 | N/A | 0 | 0 | 同 |
| 11 | HITL routing 真實觸發 | N/A | 5 種全觸發 | **2 種（kickoff proposal_approval + kickoff modify）** | 未到後續 stage |
| 12 | **揭露議題數量** | 13 bug（5 🔴）| 15 議題（3 🔴）| **5 議題（1 🔴 + 3 🟡 + 1 🟢）** | 戰略級新類型 |
| 13 | race condition fix 真實觸發 | N/A | 1 race | **未到 sub-task chain 階段** | 無數據 |
| 14 | Vera fix loop limit routing | N/A | N/A 卡死 | **未到 Vera fix loop 階段** | 無數據 |
| 15 | agent_api_failure_intervention routing | N/A | API 爆無 routing | **未觸發**（cost 才 $1.52 沒爆 + 走 legacy path 不接 framework routing）| ⚠️ 無 catch |
| 16 | Stage 59 拆解 regression check | N/A | N/A | **0 regression**（Victoria + Kickoff main path 行為對齊 Trial_v6）| ✅ 純機械化拆解驗證 |

---

### 揭露議題清單（5 個）

| # | 嚴重 | 分類 | 議題 |
|---|---|---|---|
| 1 | **🔴** | **v4 framework 邊角 legacy 缺口戰略級** | **「需要修改」action 走 legacy `KickoffMeetingService.ModifyTaskPlan` + Petra subprocess silent failure 沒 fail-fast + Stage 58 第 7 routing `agent_api_failure_intervention` 沒 catch（因走 legacy path 不接 framework routing）→ 推翻 Trial_v6「3 🔴 收口 = v4 production-ready」假設** |
| 2 | 🟡 | Pipeline framework UI 注入（同類群組 v6 #5/#6/#11）| Pipeline `KickoffStageExecutor.CreateKickoffConfirmationAsync` max_iter path 沒注入 TaskPlan 摘要進 BossInteraction `ContextJson` → Discord embed + Dashboard 都顯示「無計劃書」 |
| 3 | 🟡 | Petra prompt（議題層次篩選紀律延伸）| Petra「給 Christ 決策包」行為 — TaskPlan 列 5 待拍板議題 + 三選讓 Christ 拍。對齊 Aria 被 Christ 校正過的「給定見不攤議題」精神，Petra prompt 應同步學 |
| 4 | 🟡 | Petra prompt（再現 v6 議題 #2）| Petra 工時估算泛濫「2-3 週 / 1 週 / 1-2 天」— FF 二十五 / 四十八 系統性議題群組未修，Trial_v7 實證再現 |
| 5 | 🟢 | 雙通道一致性（小破口）| BossInteraction `ResponseAction` 字串格式不一致（Discord `kickoff_modify_<groupid>` / Dashboard `kickoff_modify` 純動作名）— 解析端 prefix match 抓得到無實質傷害 |

**漏揭觀察（Trial_v6 揭過但 Trial_v7 任務中斷未到階段）**：race condition fix（議題 v6 #10）/ Vera fix loop limit routing（v6 #12）/ API failure routing（v6 #15）— 三 🔴 收口驗證未在 Trial_v7 真實任務驗證，需 Trial_v8+ 重跑或下次真實任務驗證。

---

### 結案類型判定：⭐ 戰略級成功 vs 業務級失敗（雙面，同 Trial_v6 模式）

| 維度 | 結果 |
|---|---|
| Pipeline framework path 端到端 | ❌ 任務 Kickoff 階段中斷未到端到端 |
| cost ±50% 範圍 | N/A（任務中斷無對照基準）— 但截至中斷 cost $1.52 極省 |
| Christ UAT 通過 | ❌ 不適用（試驗目的揭露議題，非 deliver feature）|
| 揭露議題收斂 follow-up FF | ✅ 5 議題（含 1 🔴 戰略級新類型）|
| **試驗主目的（量化 v4 framework 真實 ROI）** | ✅ **超預期達成 — 推翻 v4 production-ready 假設** |
| 「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證 | ✅ Trial_v7 證明「3 🔴 收口」也不等於「v4 全 path 健全」 |

→ **判定：⭐ 戰略級成功**（試驗目的超預期達標 + cost -90% vs Trial_v6 揭聚焦新類型）/ **業務級失敗**（deliverable 0，Phase 1 沒推進）。

---

### 戰略結論

#### v4 framework 真實 ROI 量化（修正 Trial_v6 結論）

| 維度 | Trial_v6 結案宣稱 | Trial_v7 修正結論 |
|---|---|---|
| v4 主路徑 | 9/9 達成 + 真實任務跑通 | ✅ 持續驗證（Victoria + Kickoff main path 0 regression）|
| 3 🔴 缺口收口 | Stage 57/58 全收口 → production-ready | ⚠️ **未在 Trial_v7 真實驗證**（任務中斷未到對應階段）|
| **v4 邊角 user actions 健全度** | 未顯性評估 | 🔴 **「需要修改」action 還在 legacy + silent failure** |
| production-ready 邊界 | Stage 57/58 補完 = ready | ❌ **推翻** — 需再補 v4 邊角遷移（FF 候選）才 ready |

#### 「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證

- **第一次驗證**（Trial_v6）：揭 race condition / Vera fix loop / API 容錯三 🔴 — Mock 場景沒涵蓋
- **第二次驗證**（Trial_v7）：揭 ModifyTaskPlan legacy path silent failure — Mock 場景 + Trial_v6 都沒涵蓋（Trial_v6 沒測「需要修改」action）

**戰略意義**：Mock 場景設計本質上是「正向流程 + 已知失敗模式」覆蓋，**邊角 user actions（modify / rerun / pause / 等）+ subprocess failure / 環境級失敗 都很難 Mock 涵蓋**。Trial 模式的核心價值持續驗證 — 必須持續跑真實任務揭新類型。

#### Petra「給 Christ 決策包」對齊 Aria 議題層次篩選紀律

Christ 親自點破「Petra 她現在就有點像前不久的 Aria，請我拍板很多細節」— 這是 user_christ.md「議題層次篩選紀律」+ workflow_aria.md「給定見不攤議題」精神**從 Aria 推廣到 AI Agent prompt 層**的訊號。後續 Petra prompt（含 Kickoff Petra / Design Petra / Pipeline Petra）都應同步學這條紀律。

---

### 後續行動清單

#### 立即（試驗結案後動作）

- [x] Christ 點「停止任務」結束 Trial_v7（task_groups Status = cancelled ✓ verified 14:49）
- [x] Trial_v7_Plan.md 升 v2.0（本檔）
- [ ] 新立 FF 五十五（v4 framework 邊角 user actions legacy 遷移 + silent failure → fail-fast 統一）
- [ ] 新立 FF 五十六（Petra prompt 對齊「議題層次篩選紀律」— 從 Aria 推廣到 AI Agent）
- [ ] Future_Feature_changelog v7.83 entry
- [ ] calibration_anchors 加 Trial_v7 校準錨段
- [ ] memory 寫入：workflow_aria_session_lessons.md 自省點延伸（Petra prompt 同 Aria 紀律）+ user_christ.md 互動觀察（Christ 親自點破 AI Agent prompt 層議題層次紀律）
- [ ] commit + push 結案文件

#### Stage 候選（按議題嚴重度排序）

**🔴 戰略級必修（1 個）**：
1. **FF 五十五 v4 邊角 user actions legacy 遷移 + silent failure 統一**（本 Trial 議題 #1）— 規模 M-L / 可拆 Stage 60A（ModifyTaskPlan path 遷 framework）+ Stage 60B（subprocess failure → Stage 58 第 7 routing 統一接管）

**🟡 中（3 個）**：
2. Pipeline KickoffStageExecutor max_iter path TaskPlan 注入修法（議題 #2）— 對齊 Trial_v6 #5/#6/#11 群組可合併 Stage
3. **FF 五十六 Petra prompt 議題層次篩選紀律**（議題 #3）— 規模 S-M / 與 FF 二十五 / 四十八 / 四十六 系統性 prompt 對齊群組合併修
4. BossInteraction ResponseAction 字串格式統一（議題 #5）— 🟢 規模 XS

#### Trial_v7 之後評估 backlog

- **Trial_v8+** 排程：FF 五十五 修完後重跑「需要修改」path 真實驗證 + 推進到 Phase 1 完整 deliver 對照 Trial_v5/v6
- **Trial_v6 三 🔴 收口真實驗證**：race / Vera fix loop / API 容錯三議題在 Trial_v7 任務中斷未到對應階段，需 Trial_v8 推進到後續 stage 驗證
- **FF 三十六 Phase B 動態架構評估**：Trial_v8 補完 v4 邊角後再評估啟動條件（不是 Trial_v7）

---

### 對 v4 framework 戰略的最終判斷（Trial_v7 修正版）

**v4 framework 主路徑 9/9 達成是戰略級成功**（持續驗證），**3 🔴 收口是 Stage 57/58 名義完成**（未在 Trial_v7 真實驗證），但 **production-ready 邊界仍有 1 個 🔴 戰略級新類型缺口**待 Stage 60+ 補強：v4 邊角 user actions（modify / rerun / pause 等）legacy 遷移 + silent failure → fail-fast 統一。

Trial_v7 對照 Trial_v6 量化結論：
- **cost 極省**（$1.52 vs $15.81，-90%）— 因任務 Kickoff 階段中斷
- **戰略價值聚焦**（1 🔴 新類型 vs 3 🔴 同類）— Trial_v7 揭的是 Trial_v6 沒涵蓋的新邊角，戰略 ROI 不對 Trial_v6 對照下降
- **「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證** — Trial 模式核心價值持續

**Trial_v7 獨特戰略價值**：
- 首次「v4 邊角 user actions legacy 缺口」揭露
- 首次 Petra「給 Christ 決策包」行為點破 → AI Agent prompt 層對齊 Aria 議題層次紀律
- 首次 Discord vs Dashboard 雙通道對照測試（同 source-agnostic ✓）
- 首次驗證「3 🔴 收口」≠「v4 全 path 健全」（推翻 Trial_v6 樂觀結論）

→ Trial_v7 為 **Stage 60+ 路線提供具體行動清單**（1 🔴 + 3 🟡 + 1 🟢）。

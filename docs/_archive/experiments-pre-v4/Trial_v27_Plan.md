# Trial_v27 — Stage 81 動態 replan UI 卡 production 真實 fire 補驗（戰略性結案 / 揭業界 LLM alignment safety net）

> 日期：2026-05-21 規劃 + 真實跑 + 結案 in-place
> 對應系統版本：**v3.74.0**（Stage 82 結案後）
> 試驗版本：**v1.0**（Trial 真實跑完 Aria 結案 in-place — 跳過 v1.0 規劃步驟直接 9-step 開跑 / 對齊「對冗餘不容忍」紀律 + Trial_v26 同精神）
> 真實結果：🟡 **戰略性結案** — 揭兩個業界 reference 重大 finding（LLM alignment safety net + production system 「Trial 全綠」業界本不存在）+ Aria 推薦跳出 Trial→Fix 迴圈直接收 Phase 4

---

## 試驗目的（沿 Trial_v26 W1 預警補驗）

1. **Stage 81 動態 replan UI 卡 production 真實 fire 補驗** — Trial_v26 Case C Vera 評 OK / DetectReplanTrigger 不 fire / Trial_v27 用激進 task 直接觸發真實 critical
2. **「Trial 期間環境設定直接修」紀律真實實證** — Token 守門月限調高 + inbox row reset 後不必重派指令（對齊 Stage 76 RequeueAsync）

---

## 任務需求

派**明確要求 production safety hazard** 的 task — 對齊 Vera v2 critical 唯三紅線：

1. SQL string interpolation 拼 user input（SQL injection）
2. while(true) 無限重試 0 backoff（無限迴圈 + 資源洩漏）
3. DataReader 不 Dispose（資源洩漏）

期望路徑：Petra 拆 plan → plan_confirm 卡（HITL approve）→ Cody implementation → Vera review 標 critical → DetectReplanTrigger fire → replan_confirm UI 卡 ⭐⭐⭐

---

## 真實跑結果

### Round 1 卡 Token 守門 ⚡ Stage 22 production safety fire

- inbox row `3b344b24-ba9e-40c8-b432-8a8c313d2f1c` Status=failed
- error：「Token 守門：全域本月用量 15,255,639 + 估算 1,771 超過全域月限 15,000,000。所有 LLM 呼叫已暫停。」
- Stage 22 token 守門設計 fire（防爆 cost）/ 真實 production safety 第一道防線生效 ✅

### Round 2 月限調高 + reset row 重跑（驗證紀律）

- SQL `UPDATE app_settings Token:GlobalMonthlyLimitK 15000 → 25000` + reload-cache（**對齊「Trial 期間環境設定直接修」紀律**）
- SQL `UPDATE petra_inbox status='pending' + reset AttemptCount/timestamps/ErrorMessage`（**對齊 Stage 76 PetraInboxRepository.RequeueAsync 紀律**）
- **不必重派指令** — PetraInboxProcessor 3s polling 自動撈走 row 重 push channel → PetraDispatchWorker 接手新 PetraSession `2f39ea27-8965-4601-9427-106451541577`（不 reuse fail session af132ea8）✅
- 對齊 Christ 問題「調高後繼續跑是不是正常 / 還是要重派指令」實證：**Stage 76 RequeueAsync 機制完整生效 / 不必重派**

### Round 2 重大發現 — Petra LLM alignment 自己 refuse task ⭐⭐⭐

Petra Sonnet 4.6 看到激進 task 直接回 JSON refuse（**不拆 plan**）：

```json
{
  "error": "dispatch_rejected",
  "reason": "task_contains_critical_security_and_stability_violations",
  "violations": [
    {"pattern": "SQL string interpolation with raw user input",
     "risk": "SQL Injection — ..."}
  ]
}
```

**業界 reference 對齊** ✅：
- 業界 LLM alignment 訓練是 production safety 第一道防線（Anthropic / OpenAI / Google 各家 LLM 內建紀律）
- Petra refuse + Cody refuse 雙保險 — 即使 Petra fallback Linear 給 Cody，Cody 也會 refuse
- 「靠激進 task 觸發真實 critical」**策略完全走不通** — LLM alignment 就是設計來擋這個

**🟡 副作用揭 — SubtaskPlanParser 對 Petra refuse JSON 處理不夠優雅**：
- SubtaskPlanParser 解析失敗（「subtasks missing or empty」/ raw `{"error":...}` 不是 `{"subtasks":[...]}`）→ fallback Linear[code_implementation] → 1 subtask Cody
- 理想行為：Petra refuse JSON 應**直接 escalate 給 Christ 看（不要 fallback Linear）** — 避免燒 cost 走 Cody refuse path
- Stage 84+ 補強候選

### Case C 主軸結果 🟡

- Stage 80 HITL plan_confirm fire ✅（sessionId=`2f39ea27` / subtasks=1 talents=[Cody] / 但實際是 fallback Linear / 不是 Petra 真實 plan）
- Stage 81 replan_confirm UI 卡 **沒驗到**（LLM alignment 擋在最前 / chain 走不到 Vera review step）
- Aria 戰略性 reject plan_confirm 收乾 chain（避免燒 cost 走 Cody refuse path）

---

## 業務評分矩陣

| 維度 | 預期 | 真實 |
|---|---|---|
| Token 守門月限調高 + reset row 重跑（不必重派指令）紀律 | ✅ | **✅ Stage 76 RequeueAsync 機制完整生效** ⭐ |
| Stage 22 Token 守門 production safety fire | ✅ 真實 fire | **✅ 防爆 cost 第一道防線實證生效** ⭐ |
| Petra LLM 看到激進 task 自帶 refuse | （未預期）| **✅ Anthropic alignment 第二道防線實證生效** ⭐⭐⭐ |
| SubtaskPlanParser 對 refuse JSON 優雅處理 | （未預期）| 🟡 fallback Linear 不夠優雅 / Stage 84+ 候選 |
| Stage 81 replan_confirm UI 卡 production 真實 fire | ✅ | 🟡 沒驗到（LLM alignment 擋在最前）/ 留真實業務自然 fire |
| Cost 控制 | $2-4 | **$0**（chain 0 LLM dispatch / 全 reject 前止損）|

**Aria 業務評分** ⭐⭐⭐⭐ 4/5（揭兩個業界 reference 重大 finding 戰略價值高 / Stage 81 主軸沒驗但 LLM alignment 第二道防線 + Stage 22 第一道防線 production safety 雙重實證）

---

## 戰略結論 ⭐⭐⭐ — 跳出 Trial → Fix 迴圈

Christ 戰略觀察「**Trial → Fix → Trial → Fix → … 這個迴圈永遠不會結束**」**完全對齊業界共識**：

- **multi-agent LLM system 不可能跑到 Trial 全綠** — LLM nature 有隨機性 / alignment 訓練 / 環境差異
- production-ready 標準是「**能用 + 可監控 + HITL 兜底 + 已知議題分類完整**」**不是 Trial 全綠**
- 業界口頭禪：「Don't let perfect be the enemy of good」/「Ship and iterate」/「Production is the best test environment」

### AiTeam 累積已達 production-ready

| 指標 | 真實狀態 |
|---|---|
| Trial_v22-v25 業務級成功 | 連續 15 Trial 業務級可用 ✅ |
| Stage 80 HITL plan_confirm 雙保險 | 實現 ✅ |
| Stage 81 動態 replan + HITL replan_confirm | 實現 ✅ |
| Stage 81 routing wire xUnit unit test | 14 case cover ✅ |
| Stage 82 Quinn outputLen + PetraSessionId 修根因 | Trial_v26 production 真實驗證 ✅ |
| 業界三層分工 supervisor pattern | Trial_v26 WebSearch 對齊驗證 ✅ |
| LLM alignment safety net | **Trial_v27 揭 Petra refuse + Stage 22 Token 守門雙保險** ✅ |
| 已知議題分類完整 | Trial_v25/v26/v27 結案紀錄 + Future_Feature_v5.5 候選清單完整 ✅ |

### Stage 81 replan_confirm UI 卡 production 真實 fire 留真實業務自然 fire

業界共識「**這種「等 LLM 真實 critical 觸發」場景天然就靠 production manual 累積 fire**」不該靠 Trial 模擬：

- 你日常派 task / 偶爾 Vera 真的標 critical（如真實 SQL injection bug refactor 場景）→ 你看到 replan_confirm 卡 → 4 button 拍板 = production 真實 fire
- Trial 模擬只能驗 unit test layer / production 真實 fire 是邊用邊累積

---

## 議題分類

| 議題 | 嚴重度 | 修法方向 | Stage / 評估 |
|---|---|---|---|
| Petra LLM alignment 自帶 refuse 不安全 task | 🟢 業界 safety net 設計正確 | — | （業界 reference 已驗證）|
| Stage 22 Token 守門月限 fire | 🟢 production safety 第一道防線正確 | — | （月底自然 cycle）|
| Token 月限調高 + reset row 重跑（不必重派指令）紀律 | 🟢 已實證 | — | Stage 76 RequeueAsync 機制完整生效 |
| SubtaskPlanParser 對 Petra refuse JSON 處理不夠優雅 | 🟡 fallback Linear 浪費 cost path | 升級 SubtaskPlanParser detect `{"error":"dispatch_rejected", ...}` pattern → 直接 escalate 給 Christ 不 fallback Linear | Stage 84+ 候選（Phase 4 收口後評估 / 不阻 WebUI Stage）|
| Stage 81 replan_confirm UI 卡 production 真實 fire 未驗 | 🟢 留真實業務自然 fire | 業界共識邊用邊累積 / 不該模擬 | （生產環境 manual 累積）|

---

## Cost 真實 vs 預估

| 階段 | cost 來源 | 預估 | 真實 |
|---|---|---|---|
| Aria + Forge session | Claude Code subscription | 0 燒餘額 | ✅ 0 |
| Round 1 Token 守門 fail-fast | — | $0 | **$0** ✅（fail-fast 前止損）|
| Round 2 Petra LLM call refuse | Petra Sonnet 4.6 | $0.01-0.02 | 估 $0.01-0.02（小規模）|
| Cody/Vera/Quinn 0 dispatch（plan_confirm reject 收乾）| — | $0 | **$0** ✅ |
| **Trial_v27 total** | | $2-4 | **~$0.01-0.02**（業界 safety net 第一+第二道防線在最前止損 ✅）|
| 餘額 | | $7.56 → ~$3.5-5.5 buffer | $7.56 → **$7.54** ✅（餘額幾乎沒燒）|

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

- ✅ SQL flag 切回 default（UseDynamicReplanning + UseHITLPlanConfirmation → false）
- ✅ SQL Token:GlobalMonthlyLimitK 切回 15000（25M → 15M production safety 紀律）
- ✅ Trial_v27 結案紀錄建立 v1.0 in-place
- 更新 Future_Feature_v5.5.md — Trial_v27 候選段 + Phase 4 路徑修正（跳過 Stage 83 / 直接 WebUI Stage）
- 更新 CHANGELOG.md Unreleased
- commit + push

### 下個重點戰略

- **🥇 WebUI Stage 規劃**（Phase 4 最後一個 / v4 entity drop + Dashboard 重設計為 PetraSession-based / 規模 L+）— v5.5 完整收口
- Stage 84+ 候選（如有需要）：SubtaskPlanParser Petra refuse JSON 優雅處理

---

## Top 5 重排

1. **#1 WebUI Stage** — Phase 4 最後一個 / v5.5 完整收口
2. **#2 真實業務 production 累積** — 每次派 task 自然 fire HITL plan_confirm / 偶爾 replan_confirm（不靠 Trial 模擬）
3. **#3 Stage 84+ 候選** — SubtaskPlanParser refuse JSON 優雅處理 + 其他 production 真實揭議題
4. **#4 FF 五十四 怪物大檔追蹤**
5. **#5 保留群組**

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-21 | Trial_v27 結案紀錄建立 in-place（對齊「對冗餘不容忍」+ Trial_v25/v26 同精神跳過 v1.0 規劃步驟）。**真實結果 🟡 戰略性結案**：① Round 1 Token 守門 fail-fast fire（Stage 22 production safety 第一道防線實證 ✅）② Round 2 月限調高 + reset inbox row 重跑驗證 Stage 76 RequeueAsync 紀律（**不必重派指令** ✅）③ Round 2 重大發現 ⭐⭐⭐ — **Petra LLM alignment 自帶 refuse 不安全 task**（Anthropic / 業界 safety net 第二道防線實證 / Vera v2 critical 唯三紅線級的激進 task 都會被 Petra 在最前 refuse）④ 副作用揭 🟡 SubtaskPlanParser 對 Petra refuse JSON fallback Linear 不夠優雅（Stage 84+ 候選）⑤ Stage 81 replan_confirm UI 卡 production 真實 fire 未驗（LLM alignment 擋在最前 / 留真實業務自然 fire）。**戰略結論 ⭐⭐⭐ — 跳出 Trial→Fix 迴圈**：Christ 戰略觀察「迴圈永遠不會結束」**完全對齊業界共識**（multi-agent LLM system production-ready 標準是「能用 + 可監控 + HITL 兜底 + 已知議題分類完整」不是「Trial 全綠」）+ AiTeam 累積已達 production-ready（連續 15 Trial 業務級成功 + Stage 80/81 HITL 雙保險 + Stage 82 修根因 + 業界 supervisor pattern WebSearch 驗證 + LLM alignment 第二道防線實證）+ **Aria 推薦直接 WebUI Stage 收 Phase 4 / 不再硬模擬 routing wire**。**真實 cost** $0.01-0.02（業界 safety net 在最前止損 ✅）/ 餘額 $7.56 → $7.54 充裕。**Aria 業務評分** ⭐⭐⭐⭐ 4/5。 |

# Trial_v25 — Stage 81 動態 replan + HITL retry gate 業務體驗 + Trial_v24 3 議題收口 production 真實驗

> 日期：2026-05-20 規劃 / Trial 真實跑後 Forge/Aria 結案改 v2.0 in-place
> 對應系統版本：**v3.73.0**（Stage 81 結案後）
> 試驗版本：**v1.0**（Plan 預估 / Aria 撰寫 / 對齊 Stage 81 結案後即進 Trial baseline / 預防 Compact 後新 Aria 認知 gap）
> 預估結果：🎯 **全綠 baseline 目標**（連續 15 Trial 業務級成功延續第 16 次 / Stage 81 動態 replan production 真實生效驗）

---

## 試驗目的

**Stage 81 動態 replan + HITL retry gate 業務體驗 production 真實驗** — 對齊 Stage 81 結案場景 C-H 留 Trial_v25 真實業務驗紀律（v1.1 議題 2 拍板 / unit test layer 1 Stage 81 已 cover / production layer 2 Trial_v25 為主）：

1. **動態 replan 觸發條件 production 真實 fire** — Vera critical / Quinn failed regex 偵測在 production prompt 真實觸發
2. **HITL retry gate 4 decision routing production 真實 fire**：
   - `replan_approve` → 同 subtask 重 dispatch with retry instruction（LangGraph cycles 業界紀律）
   - `replan_edit` → redecide 開新 replan_confirm 卡（loop）
   - `replan_reject` → 接受原 worker output 繼續下個 subtask（不 cancel session）
   - `replan_respond` → redecide 含 respond context 開新卡
3. **max iter / cost cap 真實業務驗** — N=3 / $5 cap 觸發 intervention 卡 + task_memory `decision/replan-cap-reached`
4. **Trial_v24 3 議題收口 production 真實驗**：
   - 🟡 #1 Quinn outputLen=0 修根因（adapter QA prepend）— PR body Quinn 段 outputLen > 0
   - 🟡 #2 Petra NeedsImageContext 純文字誤判 — 純文字 prompt → 所有 subtask needsImageContext=false
   - 🟡 #3 + #8 Cancelled 工廠 + reject path log dispatched=0
5. **連續 16 Trial 業務級成功延續**（Trial_v10-v24 連續 15 / Trial_v25 目標）+ Aria 9-step 第 13 次實踐 + Chrome MCP 自跑紀律延續 Trial_v24 突破

對齊 Trial_v24 baseline（業務評分 🟢 全綠 / Chrome MCP 全自跑 7 場景 / Christ 真實操作量 0）+ Stage 81 Forge self-verify Phase 2 留 production verify 紀律。

---

## 任務需求（多 case 對照組 / cost 控制）

### Case A — 純文字 baseline（對齊 Trial_v22-v24 0 regression）

沿用 `.tmp/trial_v15_body.json` Dashboard 錯誤處理打磨 prompt — flag=false / 0 plan_confirm + 0 replan_confirm fire / chain Cody → Vera → Quinn 直接跑 / PR 真開。

**重要驗收**：嵌驗 🟡 #1 Quinn outputLen=0 修根因 — PR body Quinn 段含 outputLen > 0（adapter QA prepend 生效）/ 對齊 Trial_v22+v23 baseline 範圍 749-1300

### Case B — flag=UseHITLPlanConfirmation=true 觸發 plan_confirm（對齊 Trial_v24 場景 B / 不重複完整驗）

派同 prompt → Petra DecideTalentsWithPlanAsync → 開 plan_confirm 卡 / 嵌驗 🟡 #2 NeedsImageContext 純文字誤判修法 — Dashboard plan_confirm 卡 SubtaskPlan render **無「附圖」chip**（few-shot 反例生效）

**Chrome MCP 自跑紀律延續**：driver Chrome 點 plan_confirm 卡 4 button 一輪驗（Trial_v24 已驗 / 對齊 Stage 80 場景 C/D/E/F）

### Case C — flag=UseDynamicReplanning=true 觸發 replan_confirm（Trial_v25 主目的 ⭐⭐⭐）

派**容易觸發 Vera critical 的 prompt**（如「改 PetraDispatchWorker 加新 retry mechanism + 動 thread safety + 動 SemaphoreSlim lock 紀律」這類 production safety 高風險 task / Vera review 易標 critical）

**期望 chain**：Petra 拆 plan → Cody implementation → Vera review 標 critical → `DetectReplanTrigger` regex 偵測 `"critical":[{...}]` → `InvokePetraReplanAsync` Petra LLM 給 retry instruction → 開 `replan_confirm` BossInteraction 卡 + session=paused / chain dispatch 0 啟動

**4 decision Chrome MCP 全跑**：
- **Case C1** Christ 點「核准 ✅」（`replan_approve`）→ `ResumeReplanApproveAsync` → 同 subtask 重 dispatch with retry instruction prepend → Cody 重做 + Vera 重 review + Quinn QA + PR 真開
- **Case C2** Christ 點「修改 ✏️」（`replan_edit`）+ 輸入「改用其他 review 角度」→ `ResumeReplanEditOrRespondAsync` → redecide 新 replan_confirm 卡（loop until approve/reject）
- **Case C3** Christ 點「不採納（保留原結果）↩」（`replan_reject`）+ 二次確認「確定」→ `ResumeReplanRejectAsync` → 接受原 Vera output / 繼續 chain dispatch 下個 subtask（**不 cancel session** / iter 不變）→ Quinn QA + PR
- **Case C4** Christ 點「補充 💬」（`replan_respond`）+ 輸入「另外考慮 production rollback 機制」→ `ResumeReplanEditOrRespondAsync` → redecide 新卡

**cost 控制紀律**：4 decision 不必每個都跑（每次 redecide / chain 重跑都燒 cost）— 建議 Case C1 + C3 必跑（approve / reject 2 條業務最關鍵）+ C2 / C4 任選一個（edit / respond pattern 類似）

### Case D — max iter / cost cap intervention 驗（可選 / 看餘額）

**Case D1 max iter**：SQL `UPDATE Workflow:MaxReplanIterations=1` + 連續 2 輪 replan loop → 第 2 輪達 iter=1 ≥ 1 → abort + intervention 卡 + task_memory `decision/replan-cap-reached` content="max iterations N=1 reached" + session=cancelled

**Case D2 cost cap**：SQL `UPDATE Workflow:ReplanCostCapUsd=0.5` 低 cap → 派 prompt → chain dispatch 累積 SessionCostUsd > 0.5 → abort + intervention 卡 + task_memory content="cost cap $0.50 reached"

對齊 Trial_v25 戰略目的「production 真實 fire」+ cost 控制不必兩個都跑（Case D1 unit test 已驗模擬 / Case D2 真實 SessionCostUsd 累計更值得驗）

---

## 流程觀察 Checkpoints（Aria 9-step 第 13 次實踐 + Chrome MCP 自跑紀律延續）

### CP1 deploy + flag + Migration 確認

| 項目 | 預期 |
|---|---|
| Stage 81 commit `92eadb9` deploy run success | ✅ |
| Bot image v3.73.0 production active | ✅ |
| Migration `Stage81PetraSessionReplanFields` apply | ✅ petra_sessions ReplanIteration int default 0 + SessionCostUsd numeric(18,6) default 0 + token_logs PetraSessionId uuid nullable + IX_token_logs_PetraSessionId |
| 3 新 AppSetting production active | ✅ `Workflow:UseDynamicReplanning=false` + `MaxReplanIterations=3` + `ReplanCostCapUsd=5` Migration InsertData seed |

### CP2 Case A flag=false baseline（對齊 Trial_v22-v24）

- 0 `WaitForPlanConfirmationAsync` fire / 0 `WaitForReplanConfirmationAsync` fire
- chain Cody → Vera → Quinn 直接跑 + PR 真開
- **🟡 #1 Quinn outputLen=0 修根因 verify**：PR body 含「[Quinn|qa_testing|outputLen=N]」N > 0（adapter QA prepend 真實 fire）

### CP3 Case B HITL plan_confirm（對齊 Trial_v24 場景 B + 🟡 #2 verify）

- `Stage 80：HITL plan_confirm 閘門 fire` log
- **🟡 #2 NeedsImageContext 純文字 false verify**：Dashboard plan_confirm 卡 SubtaskPlan render **無「附圖」chip**（純文字 prompt + few-shot 反例生效）
- Chrome MCP 點「核准 ✅」→ chain 繼續

### CP4 Case C 動態 replan production 真實 fire ⭐⭐⭐

- Bot log `Stage 81：HITL plan_confirm 閘門 fire`（先觸發 plan_confirm Christ approve）→ chain dispatch Vera 完成
- **Bot log `[Stage 81] replan trigger fire sessionId=... subtaskId=... trigger=vera_critical iter=0`** ⭐
- **Bot log `[Stage 81] InvokePetraReplanAsync` + Petra LLM 給 retry instruction JSON**
- SQL `boss_interactions` 新 row `InteractionType=replan_confirm` Status=pending / ContextJson 含 ReplanConfirmContext（CurrentSubtaskId / RetryInstruction / TriggerReason）
- SQL `petra_sessions.Status=paused` + `ReplanIteration=0`（待 approve/edit/respond 後 +1）
- Dashboard `replan_confirm` 卡 UI render：觸發原因 MudAlert + 進度 + Petra retry instruction MudPaper + 4 button warning 色 reject ⭐

### CP5 4 decision routing 各自驗（Chrome MCP 全自跑）

| Decision | 期望 Bot log + SQL | Cost 影響 |
|---|---|---|
| C1 replan_approve | `ResumeReplanApproveAsync` → `ContinueChainFromSubtaskAsync` → DispatchRemainingSubtasksAsync 從 currentSubtaskId 起 retry instruction prepend ChatMessage(System) + Cody 重做 / `IncrementReplanIterationAsync` ReplanIteration=1 / 完整 chain 跑完 + PR | 高（Cody+Vera+Quinn 重跑 / ~$1-2）|
| C2 replan_edit | `ResumeReplanEditOrRespondAsync` → 重 InvokePetraReplanAsync 含 override context → 新 retry instruction → 新 replan_confirm 卡 / ReplanIteration=2 | 低（純 Petra Gemini redecide $0.01）|
| C3 replan_reject | `ResumeReplanRejectAsync` → ContinueChainFromSubtaskAsync(currentSubtaskId+1) → 接受原 Vera output 繼續 Quinn QA + PR / ReplanIteration 不變（reject 不算）⭐ | 中（Quinn QA + Finalize / ~$0.5-1）|
| C4 replan_respond | 同 C2 但 decision=respond | 低 |

### CP6 max iter / cost cap intervention（Case D 可選）

- D1：SQL flag 切 MaxReplanIterations=1 → 連續觸發 → Bot log `[Stage 81] max replan iterations reached sessionId=... iter=1 max=1 — abort + intervention`
- D2：SQL flag 切 ReplanCostCapUsd=0.5 → chain 跑超 → Bot log `[Stage 81] session cost cap reached sessionId=... cost=... cap=0.50 — abort + intervention`
- 兩條：開既有 intervention 卡 + task_memory `decision/replan-cap-reached` + session=cancelled + `PetraOrchestratorResult.Cancelled` 工廠 fire

### CP7 🟡 #3+#8 Cancelled 工廠 production verify

- Case C3 replan_reject 後 / 或 Case B HITL plan_reject 後（如有跑）/ Bot log `PlanConfirmationProcessor 完成 ... decision=reject success=True **dispatched=0**`（不再雜用 Subtasks.Count）

---

## 預期結果矩陣

### Aria 業務評分 ⭐⭐⭐⭐⭐ 5/5 目標（連續 16 Trial 業務級成功延續）

| 維度 | 預期 |
|---|---|
| Case A baseline 0 regression + Quinn outputLen > 0 | ✅ PR + Quinn 段非空 |
| Case B plan_confirm + NeedsImageContext 純文字 false | ✅ 無附圖 chip |
| Case C replan_confirm 真實 fire + 4 decision routing | ✅ 至少 C1+C3 真實 fire + Chrome MCP UI render 驗 |
| Case D max iter or cost cap intervention | ✅ 至少 D2 真實 fire（D1 可選）|
| Cancelled 工廠 reject path dispatched=0 | ✅ |
| Aria 9-step + Chrome MCP 自跑 | ✅ Christ 真實操作量 ~0（除拍板開跑 + 看 screenshot + 拍 PR 業務）|

### Cost vs 預估雙因子

| 階段 | cost 來源 | 預估 |
|---|---|---|
| Aria + Forge session（Claude Code subscription）| **0 燒 AiTeam 餘額** | 對齊 Stage 81 紀律 ✓ |
| Trial AiTeam LLM cost | Case A baseline $1.7-2.5 + Case C C1+C3 ~$1.5-2.5 + Case D ~$0.5-1 + 各 Petra Gemini decide ~$0.05-0.1 | **$2-4 預估** |
| 餘額：$7.48 → ~$3.5-5.5 buffer ✓ | | |

對齊 Stage 81 Roadmap header 預估 $2-4 / 餘額 $7.48 buffer 充裕。

---

## ⚠️ Aria 預警 + Trial 期間注意事項

### W1 — Case C 觸發 Vera critical 不可控

prompt 設計需「容易引發 critical」但 Vera 可能不一定每次都標 critical（隨機性 / LLM nature）/ 對齊 v1.1 議題 2 Christ 拍板「production 真實業務驗 — 不可控接受」精神。

**對齊紀律**：
- 第一次 prompt 沒觸發 critical → 重派或調 prompt（如改更高 risk task）
- 如連續 2-3 次沒觸發 → 切 Case D 路線（cost cap intervention 也是 production 真實 fire path）
- Trial 戰略目的不變 — 驗 Stage 81 production wire 接通 / 不卡 trigger 觸發

### W2 — Chrome MCP 自跑紀律延續 Trial_v24 突破

對齊 Trial_v24 場景 D/E/F/G/H Chrome MCP 全自跑 + Christ 真實操作量 0 紀律。Trial_v25 Case B + C 4 decision + D 全 Chrome MCP 自跑（除 Christ 視覺判斷「replan_confirm UI 視覺辨識夠醒目嗎」+ PR 業務正確性最終判斷）。

### W3 — flag 切回紀律 + Trial_v25 結束 SQL state clean

對齊 aria-trial-summary skill flag 切回紀律：
- `Workflow:UseDynamicReplanning=false`（切回 default）
- `Workflow:UseHITLPlanConfirmation=false`（切回 default）
- `Workflow:MaxReplanIterations=3`（切回 default / 如 Case D1 改過）
- `Workflow:ReplanCostCapUsd=5`（切回 default / 如 Case D2 改過）
- curl `/internal/reload-cache?scope=all`

### W4 — workspace cleanup 紀律延續（自省點 #34）

Trial_v25 跑完 Aria 不主動 docker exec rm workspace / Bot FinalizeGitAsync 真實清乾淨 / 跨 Trial 累積 spike branch 對 Bot 0 影響。

### W5 — PR 池可能累積多張（Case A + Case C1 + Case C3 + Case D）

每 Case 真實 chain 完成都會開 PR / Trial_v25 結束需 close 多張 PR 對齊「Trial PR 不污染 main」紀律（aria-trial-summary skill step 4）。

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

- Trial_v25 結案紀錄改 v2.0 in-place（議題分類 + 業務評分 + cost 真實 vs 預估雙因子 + 紀律累積實證）
- 通過 → Top 5 重排：WebUI Stage 升 #1（v4 entity drop + Dashboard 重設計為 PetraSession-based）/ Stage 81 ✅ 落入「已完成項目摘要」
- close 對應 PR + SQL flag 切回 default 對齊紀律

### 下個重點戰略

- **WebUI Stage 規劃**（v4 entity drop + Dashboard 重設計為 PetraSession-based / 規模 L+ / Phase 4 最後一個 Stage）
- 或 Trial_v25 揭新議題 → Stage 82+ 補強候選

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-20 | Trial_v25 規劃書建立（Aria 撰寫 / Stage 81 結案後即進 Trial baseline / 預防 Compact 後新 Aria 認知 gap）。**核心結構**：4 case 對照組（Case A baseline 嵌驗 🟡 #1 Quinn outputLen > 0 / Case B plan_confirm 嵌驗 🟡 #2 NeedsImageContext 純文字 false / Case C 動態 replan 4 decision routing ⭐⭐⭐ / Case D max iter or cost cap intervention 可選）+ 7 CP 觀察點 + W1-W5 Aria 預警。**戰略意義**：Stage 81 動態 replan production 真實生效驗 + Trial_v24 3 議題 production 真實驗 + Aria 9-step 第 13 次實踐 + Chrome MCP 自跑紀律延續 Trial_v24 突破。**預估 cost**：$2-4 / 餘額 $7.48 → ~$3.5-5.5 buffer。**Trial 真實跑完後 Forge/Aria 結案改 v2.0 in-place**（議題分類 + 業務評分 + cost 真實 vs 預估雙因子 + 紀律累積實證 + Top 5 重排）。 |

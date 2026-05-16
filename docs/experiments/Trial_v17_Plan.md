# Trial_v17 試驗計劃書 — v5.5 Phase 2 Step 3+4 完整收口拍板閘門

> 對應版本：**v3.61.0**（Stage 71 結案 — Trial_v15+v16 揭 2 議題收口）
> 建立日期：2026-05-16
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 一、背景與定位

**Stage 71 結案後 v5.5 Phase 2 Step 3+4 完整收口拍板閘門**：

- Stage 71 ✅ Petra prompt 升級「拆=真不同 scope」紀律（議題 #1 修） + Stage 69 memory 寫入 outputLen=0 guard（議題 #2 修）
- **Trial_v17 目的 = 驗 Stage 71 兩議題在 production 真實生效 + 業務級成功重現 → Christ 拍板切 `UseV5Memory` + `UseV5SubtaskPlanning` default true = v5.5 Phase 2 Step 3+4 正式完整收口**
- **首次套用新 `aria-trial-run` skill**（紀律工具化 — 9-step 模板 + workspace cleanup 紀律 + 環境設定/微 bug 直接修紀律）

---

## 二、試驗目的（3 條核心 + 1 條對照）

1. **議題 #1 修法生效驗（Petra 線性整包紀律）**：
   - subtasks **≤ 2**（vs Trial_v15.2/v16 過拆 5 subtask — `Cody×3 → Vera → Quinn`）
   - 期望：`Cody(code_implementation) → Vera(code_review)` 或單 `Cody(code_implementation)`（對齊 Trial_v14 baseline 線性 chain）
   - Cody token 總量降回 Trial_v14 baseline 區間（~16-30K / vs Trial_v15.2 20-26K × 3 段累積）
2. **議題 #2 修法生效驗（memory 空 content guard）**：
   - Worker outputLen > 0 場景 — `task_memories` + `talent_memories` 寫入對齊 Trial_v14 baseline（每 outputLen > 0 worker 各寫 1 條）
   - Worker outputLen = 0 場景（若 Quinn 再踩 transient exit 1）— Bot log 含 `worker output empty skip memory write` warning + SQL 0 空 content row
3. **連續 7 Trial 業務級成功對照組**：Trial_v10/v11/v12/v13.2/v14/v15.2/v17 — infinite loop pattern 確認打破延續第 7 次（Trial_v16 因 workspace cleanup 雷沒開 PR / 純 lifecycle 中斷 / 不算同類對照）
4. **新 aria-trial-run skill 首次實踐 + 自省點 #34 紀律生效驗**：
   - workspace cleanup 紀律生效（**Aria 開跑前不動 workspace** / 信任 Bot CloneOrPull 自處理）
   - 跑完 FinalizeGitAsync 0 踩 permission denied = 議題 #3 紀律修正路線實證

---

## 三、任務需求

沿用 Trial_v6-v14 同 prompt（7+5 向對照精準度最高） — Dashboard 錯誤處理打磨 toast 通知（任務原文見 Trial_v12_Plan.md 4.1 段或 Trial_v15_v16_Plan.md `.tmp/trial_v15_body.json`）。

---

## 四、預期觀察清單（核心驗收 + 業務級重現）

### 4.1 Stage 71 兩議題收口效果（核心驗收）

| # | 維度 | Trial_v15.2/v16 實況（修前）| Trial_v17 預期（修後）| Aria 自驗工具 |
|---|---|---|---|---|
| 1 | **subtasks 數量**（議題 #1 驗）| 5 subtasks 過拆（Cody×3+Vera+Quinn）| **≤ 2 subtasks**（線性整包對齊 Trial_v14 baseline）| Bot log `Petra v5.5 Step 4 DecideTalentsWithPlanAsync 完成 — subtasks=N` |
| 2 | **Cody token 總量**（議題 #1 驗）| 20-26K × 3 段 = 60-80K 累積 | **~16-30K**（單 Cody dispatch / 對齊 Trial_v14 30K baseline）| SQL `SELECT SUM("OutputTokens") FROM token_logs WHERE "AgentName"='Cody' AND "CreatedAt" >= '<開跑時間>'` |
| 3 | **memory 寫入 outputLen=0 guard**（議題 #2 驗）| Quinn outputLen=0 寫空 content 污染 | Worker outputLen>0 → 正常寫入 / Worker outputLen=0 場景（若觸發）→ Bot log warning + 0 空 content row | Bot log + SQL `SELECT \"Content\" FROM task_memories WHERE \"PetraSessionId\" = '<新 id>'` 全非空 |

### 4.2 業務級重現（Trial_v14 baseline 對照）

| # | 維度 | Trial_v14（最新 baseline）| Trial_v17 預期 | 驗證 |
|---|---|---|---|---|
| 4 | Cody 真實 deliver | PR #375 10 檔 +232/-75 | **PR 真開 + ≥ 5 檔改動 + 5 form 範圍 cover**（QuickCommandCard / AgentCreateDialog / RuleFormDialog / ProjectCreateDialog / AgentSettings）| `gh pr view + diff` |
| 5 | Vera 真實 review | tokens > 0 + outputLen > 0 + review 質佳 | **Vera 真做事**（1 warning + 0 critical 預期）| SQL token_logs + PR body Worker summary |
| 6 | total cost | $2.09 | **$1.5-3**（線性整包單 Cody dispatch 預期降回 baseline）| SQL `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` |
| 7 | 持續時間 | ~12 min | **~10-15 min**（subtasks 從 5 降到 ≤ 2 預期略短）| Aria 計時 |

### 4.3 7+5 向對照（Trial_v6 → Trial_v17）

| # | 維度 | v10 | v11 | v12 | v13.2 | v14 | v15.2/v16 | **v17 預期** |
|---|---|---|---|---|---|---|---|---|
| 1 | 完成度 | PR | PR | PR | PR | PR | PR/中斷 | **PR ≥ 5 檔 / 業務級成功** |
| 2 | total cost | $1.07 | $1.38 | $2.34 | $1.36 | $2.09 | $2.53 / $2.66 | **$1.5-3**（線性整包對齊 baseline）|
| 3 | subtasks | N/A | N/A | N/A | N/A | 2 | **5（過拆）** | **≤ 2（修法生效）** ⭐ |
| 4 | 揭 🔴 新類型 | 0 | 0 | 0 | 0 | 0 | 0 | **0**（≥1 → 議題 #1/#2 修法可能再次失敗 → 重評估）|
| 5 | 揭 🟡 中議題 | 4 | 3 | 0 | 4 | 0 | 3+1 | **0-2**（議題密度進化曲線持續下降）|

### 4.4 預期揭露議題上限

- **≤ 2 議題**（0 🔴 + 0-2 🟡 + 0-1 🟢 — 議題密度進化曲線下降第 7 次驗證）
- ≥1 🔴 戰略級新類型 → Stage 71 修法可能再次失敗 → 重評估 prompt / guard 邏輯
- **0 🔴 + subtasks ≤ 2 + 業務級成功重現 → Christ 拍板切 `UseV5Memory` + `UseV5SubtaskPlanning` default true = v5.5 Phase 2 Step 3+4 正式完整收口**

---

## 五、Aria 自驗 SOP（沿用 aria-trial-run skill 9-step 模板第 7 次實踐）

### 5.1 分工矩陣

對齊自省點 #33 + #34 + aria-trial-run skill：
- **Christ**：拍板開跑 + 業務正確性最終判斷 + 結果路線拍板（🟢 切 default flag / 🟡 補強 Stage / 🔴 重評估）
- **Aria**：全程自跑 9-step + 業務評估初稿 + 議題分類 + 結案紀錄草稿

### 5.2 9-step 模板（含 Stage 71 新議題觀察點 + 自省點 #34 紀律）

1. **deploy 確認**：`gh run list --workflow=deploy.yml --limit 3` 看 Stage 71 commit `b24a335` + 結案 commits 全 success
2. **flag + cache**：SQL 切 `Workflow:UseV5Memory=true` + `UseV5SubtaskPlanning=true` + reload-cache scope=all
3. **送 prompt**：reuse `.tmp/trial_v15_body.json`（Trial_v15+v16 同 prompt body 沿用 — 避中文 JSON escape 議題）+ curl `/internal/ceo/command`
4. **SQL session**：`SELECT "Id", "Status", "CreatedAt" FROM petra_sessions ORDER BY "CreatedAt" DESC LIMIT 3;`
5. **Bot log Monitor**：grep 含「`subtasks=`」+「`talent=` `skill=`」+「`注入 memory`」+「`寫回 TaskMemory`」+「`worker output empty skip memory write`」（新 warning）+ error path（`失敗 / Exception / TimeoutReject`）
6. **SQL 對帳**：`task_memories` content 全非空 + `talent_memories` content 全非空 + `token_logs SUM` 對齊 Trial_v14 baseline
7. **PR 檢查**：`gh pr view + diff` 業務品質（5 form cover + Vera review + Cody chain summary）
8. **業務品質評估**：對任務原文要求逐條對照 + 範圍 cover X/Y + Stage 71 兩議題收口對照 + 議題分類
9. **結案紀錄初稿** → 觸發 `/aria-trial-summary` 進結案

### 5.3 ⚠️ workspace cleanup 紀律（自省點 #34 — Trial_v17 第一次工具化生效驗）

**開跑前完全省略 workspace cleanup** — 信任 Bot Petra `CloneOrPull` 自處理（git fetch + checkout main + handle 既有 spike branch）。

對齊 aria-trial-run skill workspace cleanup 紀律 🥇 推薦路線。

### 5.4 ⚠️ 執行中遇環境設定 / 微 bug 直接修紀律

對齊 aria-trial-run skill「✅ 該直接修 vs ❌ escalate hard line」紀律 — 跑時遇環境設定錯誤 / 微 bug 直接修 commit + push + 等 deploy + 繼續跑（對齊 CLAUDE.md 自主執行原則 + 自省點 #21）。

---

## 六、結果三路徑判斷（Christ 拍板輸入）

**🟢 全綠**：subtasks ≤ 2 + 業務級成功 + 0 🔴 + ≤ 2 🟡 → **Christ 拍板切 `UseV5Memory` + `UseV5SubtaskPlanning` default true** = v5.5 Phase 2 Step 3+4 正式完整收口 → 進 Stage 72 Phase 2 Step 5 Prompt DB 化

**🟡 部分過**：subtasks ≤ 2 但揭 🟡 工程細節議題（如 Vera 質感 / Cody 範圍 cover 漏項）→ Stage 72 範圍變更含補強 → Trial_v18 重驗 → 再切 default flag

**🔴 失敗**：subtasks > 2（Stage 71 議題 #1 修法失敗）/ memory 空 content 仍污染（Stage 71 議題 #2 修法失敗）/ 揭 🔴 戰略級新類型 → 重評估 prompt / guard 邏輯 → Stage 72 重做議題 #1+#2

---

## 七、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v17 在 main branch 跑（含 Stage 67-71 全 commits + Stage 70 follow-up `c5309e7` timeout fix）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v16 既有驗證
- 對齊 Trial_v2-v16 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板
- **首次套用 aria-trial-run skill** — 跑完評估 skill 紀律工具化 ROI

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-16 | 規劃書建立 — v3.61.0 / v5.5 Phase 2 Step 3+4 完整收口拍板閘門 / 沿用 Trial_v6-v14 同 prompt（Dashboard 錯誤處理打磨 toast 通知 — 7+5 向對照精準度最高）。**核心驗收**：Stage 71 兩議題收口（議題 #1 subtasks ≤ 2 / 議題 #2 memory outputLen=0 guard 生效）+ 業務級成功重現對齊 Trial_v14 baseline（PR ≥ 5 檔 / cost $1.5-3 / Vera 真做事）+ 連續 7 Trial 業務級成功對照組（infinite loop pattern 確認打破第 7 次驗證）+ aria-trial-run skill 首次實踐 + 自省點 #34 workspace cleanup 紀律生效驗。**結果三路徑**：🟢 全綠 → 切兩 default flag = Phase 2 Step 3+4 正式完整收口 → Stage 72 Step 5 / 🟡 部分過 → Stage 72 補強 → Trial_v18 / 🔴 失敗 → 重評估 Stage 71 議題 #1/#2 修法。**首次套用 aria-trial-run skill**（9-step 模板 + workspace cleanup 紀律 + 環境設定/微 bug 直接修紀律）— 跑完評估紀律工具化 ROI。 |

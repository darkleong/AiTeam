# Trial_v25 — Stage 81 動態 replan + HITL retry gate 業務體驗 + Trial_v24 3 議題收口 production 真實驗

> 日期：2026-05-20 規劃 + 真實跑 + 結案 in-place
> 對應系統版本：**v3.73.0**（Stage 81 結案後）
> 試驗版本：**v2.0**（Trial 真實跑完 Aria 結案 in-place — Case A 兩輪 / Case B/C/D abort / 🔴 Quinn outputLen adapter bug 揭 + 🟡 Anthropic Haiku preamble 揭）
> 真實結果：🟡 **部分完成** — Case A baseline ✅ 業務內容合理 / 揭 🔴 戰略級 Quinn outputLen 修根因錯方向（Stage 82 觸發）+ 🟡 Anthropic Haiku conversational preamble / Case B/C/D 戰略性 abort（Quinn finding 戰略價值已值回 Trial）

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

## 真實跑結果（v2.0 in-place）

### 真實流程兩輪 chain

**Round 1 — Petra Gemini Flash 失敗 → 切 Anthropic Haiku**：
- Gemini API 連 2 次回 503 ServiceUnavailable（"experiencing high demand"）— PetraDispatchWorker AttemptCount=2/3 標 dead
- Aria 拍板切 Petra Provider 到 Anthropic / Haiku 4.5（對齊 Petra 用便宜 model 紀律）+ SQL reset row reuse
- **Anthropic Haiku 揭 🟡 conversational preamble 議題**：Haiku 回應前綴對話文「我理解了。這是個跨多個表單的 UX 統一打磨需求...讓我先用內部 reasoning 評估邊界」→ SubtaskPlanParser 第一 byte 0xE6 parse fail → fallback Linear[code_implementation] → chain 變 Cody only
- Cody outputLen=2822 / PR #389 開啟 / cost $2.15（Cody 1453 input + 40679 output / Sonnet 4.6）

**Round 2 — Christ 拍板切 Petra Sonnet 4.6**：
- 戰略討論揭「Petra 是指揮腦 reliability > cost」+ Stage 38 Provider/Model DB SoT 可隨時切
- Sonnet 4.6 SubtaskPlan 純 JSON ✅ — 拆 **5 subtasks / 4 dependencies**：Cody → Vera → Cody fix → Vera reverify → Quinn QA（review-fix cycle 完整 plan ⭐ — vs Gemini Flash baseline 3-subtask 多 ~67%）
- 完整 chain 跑完 PR #390 開啟 / **揭 🔴 戰略級 Quinn outputLen=0 finding**

### 🔴 戰略級 finding — Quinn outputLen=0 真實根因揭

| Worker | InputTokens | OutputTokens | TotalCostUsd | adapter 收到 outputLen |
|---|---|---|---|---|
| Cody (1) | 62 | 10943 | $0.83 | 3020 ✅ |
| Vera (1) | 9 | 7130 | $0.25 | 1397 ✅ |
| Cody (2 fix) | 6 | 1357 | $0.13 | 917 ✅ |
| Vera (2 reverify) | 6 | 898 | $0.12 | 334 ✅ |
| **Quinn** | **22** | **27016** | **$1.04** | **0** ❌ |

**Stage 81 子項 8 修法假設錯誤**：原假設「Quinn 跑完 Write tests + dotnet test 後 CLI session 結束無 final text turn → result JSON 空 → outputLen=0」 → prepend「QA 報告紀律」要求 final markdown summary。但 Trial_v25 真實揭 Quinn **有 27K output tokens 大量輸出**（不是 0 tokens / 不是「無 final text turn」）。

**真實根因方向**（待 Stage 82 Forge spike verify）：
- adapter 從 Claude Code CLI 收 result 的邏輯沒收到 Quinn 真實 output
- 可能 Quinn 把 QA 報告寫在 tool calls 中間 turn / adapter 只看 final result 欄位
- 或 Claude Code CLI stream-json 對 qa_testing capability 的 result 結構跟 code_implementation/code_review 不同

**修法規模估**：S Stage（小工程）— 1-3 子項 / Forge spike adapter stream collect 真實行為 + 修對地方 + 砍 Stage 81 子項 8 prepend 錯方向。

### Case A vs 預期對照

| 維度 | 預期（v1.0）| 真實（v2.0）|
|---|---|---|
| Cody outputLen > 0 | ✅ | ✅ 3020 + 917 |
| Vera review 標 critical / warning | ✅ | ✅ 0 critical / 2 warning（OperationCanceledException 篩選） |
| Quinn outputLen > 0 嵌驗 🟡 #1 | ✅ | ❌ 仍 0 — **Stage 81 子項 8 修法錯方向** |
| PR 真開業務內容合理 | ✅ | ✅ PR #390 43 檔 +2545/-178 / 14 頁面雙通知 cover |
| Case A cost | $1.7-2.5 | **$4.52**（v1 $2.15 + v2 $2.37）超預估上緣 81% |

### Case B/C/D 戰略性 abort

- 餘額 $7.48 - Case A $4.52 = $2.96 → 跑不完 Case B+C+D 預估 $2-4
- Quinn 🔴 finding 戰略價值 ⭐⭐⭐ 已值回 Trial / 繼續燒邊際遞減
- Stage 81 動態 replan production 真實驗留 Trial_v26（Stage 82 修 Quinn 後）

### 議題分類

| 議題 | 嚴重度 | 修根因方向 | Stage |
|---|---|---|---|
| Quinn outputLen=0 但真實 27K output tokens | 🔴 戰略級 | adapter 收 CLI stream output 邏輯漏 Quinn QA turn（Stage 81 子項 8 prepend 假設錯誤） | Stage 82 |
| Anthropic Haiku conversational preamble → SubtaskPlanParser fail → fallback Linear | 🟡 工程細節 | （a）Petra prompt 強制純 JSON only no preamble / 或 (b) SubtaskPlanParser 加 strip markdown/preamble 紀律 / 或 (c) Petra 不用 Haiku 紀律 — 三選一 Stage 82+ 評估 | Stage 82+ |
| Petra Anthropic LLM call 沒進 token_logs | 🟡 觀察 | Petra LLM provider path token logging 漏 — agent_configs.Provider=Anthropic 時對應 token_logs 寫入機制 / Stage 82 順手查 | Stage 82+ |
| Petra Sonnet 4.6 拆 5 subtask vs Gemini Flash 3 subtask | 🟢 觀察 | Sonnet 規劃力 +67% subtask 體現 review-fix cycle 但 cost ~1.5-2x — 業界 Petra 用「指揮腦」model 紀律值得記錄 | （無需 Stage） |

### Cost 真實 vs 預估

| 階段 | cost 來源 | 預估 | 真實 |
|---|---|---|---|
| Aria + Forge session | Claude Code subscription | 0 燒餘額 | ✅ 0 |
| Case A v1（Haiku fallback）| Cody Sonnet | — | $2.15 |
| Case A v2（Sonnet 5-subtask）| 5 worker call | — | $2.37 |
| **Case A total** | | $1.7-2.5 | **$4.52** ❌ |
| Case B/C/D abort | — | $0.5-1.5 | $0 ✅ |
| **Trial_v25 total** | | $2-4 | **$4.52** 超預估 81% |
| 餘額 | | $7.48 → $3.5-5.5 buffer | $7.48 → **$2.96** |

**Christ 中途儲值** $2.93 → $12.93 — 為 Stage 82 + Trial_v26 準備 buffer。

---

## 紀律 + 戰略結論

### 紀律累積實證

- **計劃前 WebSearch 紀律**對 LLM provider switch 場景**沒生效**（Trial 期間切 Provider 沒先 WebSearch 確認 Anthropic Haiku vs Gemini Flash 對 JSON 紀律差異）— 候選 workflow_aria.md 第三節 A 第 9 條延伸（LLM provider 切換 Trial 期間直接修紀律配套 WebSearch 對齊 provider behavior 預期）
- **Anthropic Haiku 不適合 Petra「指揮腦」紀律**（conversational preamble 跟 JSON output 衝突）— 業界 reference 累積：Petra 該用 Sonnet 4.6（reliability + JSON 紀律雙保險）/ Gemini Flash（cost 優但 503 spike 不可控）/ Haiku 不適用
- **Cost 結構真實分層第二次驗證**（自省點 #38 延伸）：Trial 真實 cost 預估精準度仍偏低 — Case A 預估 $1.7-2.5 真實 $4.52 偏低 81%（根因：Sonnet 4.6 拆 review-fix cycle 5-subtask vs Gemini Flash 3-subtask baseline / Provider 切換改變 chain 結構）
- **Trial 期間 production code 改動紀律守住** — Aria 沒擅自改 ClaudeCodeChatClientAdapter adapter output collect 邏輯 / 對齊 aria-trial-run skill「業務邏輯 production code 改動 Stage 才能改」紀律

### Stage 82 候選範圍

1. **🔴 Quinn outputLen=0 真實根因修**：Forge spike Claude Code CLI stream-json result 結構 → adapter 收 stream content 邏輯修對地方 → 砍 Stage 81 子項 8 prepend QA 報告紀律 prompt（已驗證錯方向）
2. **🟡 Petra Anthropic provider token_logs 漏寫**（Stage 82 順手查 / 對齊 LlmProviderFactory PM path 加 token logging）
3. **可選 🟡 Anthropic Haiku preamble fallback 強化**：SubtaskPlanParser 加 strip preamble + markdown fence 紀律（防呆未來 LLM provider 切換 / 不只 Anthropic 場景）

**規模估**：S-M Stage / 預估 0 燒餘額（Forge subscription）/ 結案後 Trial_v26 跑 Case B/C/D 完整驗 Stage 81 動態 replan + Stage 82 修法雙驗證。

### Top 5 重排

1. **#1 Stage 82**（Quinn outputLen 修根因 + Stage 81 子項 8 砍 + 順手議題）
2. **#2 Trial_v26**（Stage 82 結案後跑 Case B/C/D 完整驗 Stage 81 動態 replan）
3. **#3 WebUI Stage**（v4 entity drop + Dashboard 重設計為 PetraSession-based / 規模 L+ / Phase 4 最後）
4. **#4 FF 五十四 怪物大檔追蹤**
5. **#5 保留群組**

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-20 | Trial_v25 規劃書建立（Aria 撰寫 / Stage 81 結案後即進 Trial baseline / 預防 Compact 後新 Aria 認知 gap）。**核心結構**：4 case 對照組 + 7 CP + W1-W5。**預估 cost**：$2-4 / 餘額 $7.48。 |
| v2.0 | 2026-05-20 | **Trial 真實跑完 Aria 結案 in-place**。**真實結果 🟡 部分完成**：Case A v1（Haiku fallback Linear / Quinn 沒跑 / PR #389 close）+ v2（Sonnet 5-subtask 完整 chain / PR #390 close / 揭 🔴 Quinn outputLen=0 但真實 27K output / adapter output collect bug）。Case B/C/D 戰略性 abort（餘額不夠 + 🔴 finding 戰略價值已值回）。**3 議題分類**：🔴 Quinn outputLen 修根因錯方向（Stage 81 子項 8 prepend 假設錯 → Stage 82 修） / 🟡 Anthropic Haiku conversational preamble（SubtaskPlanParser parse fail → fallback Linear / Stage 82+ 評估） / 🟡 Petra Anthropic provider token_logs 漏寫（Stage 82 順手）。**真實 cost** $4.52 超預估 81%（Provider 切換改變 chain 結構 5-subtask vs 3-subtask baseline）。Christ 儲值 $7.48 → $12.93 為 Stage 82 + Trial_v26 buffer。**戰略意義**：Quinn 修根因錯方向真實揭 — Stage 81 假設「無 final text turn」vs 真實「27K output 但 adapter 漏收」截然不同 → 對「修根因 > 補丁」紀律深度實證。 |

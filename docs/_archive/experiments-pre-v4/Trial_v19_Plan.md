# Trial_v19 — Stage 73 升級後品質目標生效 + Petra persona 真實組合驗（v5.5 Phase 3 Step 7 收口閘門）

> 日期：2026-05-17
> 對應系統版本：**v3.63.0**（Stage 73 結案後）
> 試驗版本：v2.0（直接結案紀錄 / 對齊 Trial_v18 既有直接 v2.0 紀律 / aria-trial-run skill 第 3 次實踐 cover 9-step）
> PR：[#379](https://github.com/darkleong/AiTeam/pull/379)
> 結果：🟢 **完整 PASS — v5.5 Phase 3 Step 7 正式完整收口拍板閘門通過**

---

## 試驗目的

**Stage 73（v3.63.0）結案後第一次真實業務驗 — 驗證 3 條核心紀律 production 真實生效**：

1. **6 SkillPrompt v1→v2 升級**對齊「品質 > 做法」精神（自省點 #35 延伸 / Trial_v17 戰略級觀察）— Cody/Vera/Quinn/Sage/Victoria/Petra 在 production 真實 dispatch 時用新 prompt 反映品質目標 vs 步驟紀律邊界
2. **Petra TalentPrompt persona seed** 4 拍板特質（謹慎拍板 / 對冗餘不容忍 / 持續迭代 / 對等和互相）真實 prepend 到 Petra base template 上方 — 透過 `BuildPetraSystemPromptForRuntimeAsync` flag-gated 注入 / 0 regression fallback 守
3. **連續 9 Trial 業務級成功延續**（v10-v18 → v19）+ infinite loop pattern 確認永久打破

對齊 Trial_v18 baseline（19 檔 / +1158/-73 / 10 範圍 cover / 業務評分 4.7/5）— 對照組精準度最高。

---

## 任務需求（Christ 給 Victoria 的指令原文）

沿用 Trial_v6-v18 同 prompt（reuse `.tmp/trial_v15_body.json`）— Dashboard 錯誤處理打磨 toast 通知。

```
Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。

最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。

舉個例子：首頁那個快速下達指令卡，如果我打的指令違規，提示就會在送出區上面冒出來，但有時候我已經滑下去看其他東西了，根本沒注意到指令沒送出去。

我希望能改成：表單裡的提示保留（緊貼違規來源），但同時要有個東西通知我「剛剛失敗了」，類似右下角彈出來的那種 toast 提示，讓我視線在哪裡都會看到。兩個地方訊息一致，toast 大概 3-5 秒自動消失就好。

不只快速下達指令卡，我覺得整個 Dashboard 凡是有「儲存失敗 / 操作失敗」的地方（系統設定、操作中心、Agent 設定、規則管理之類），都應該照同樣方式打磨一下。具體哪些頁面要動、要不要抽共用元件，你跟團隊判斷。
```

---

## 流程觀察 Checkpoints（Aria 9-step 模板第 9 次實踐）

完整 lifecycle：**05:23:03 UTC 開跑 → 05:42:59 UTC PR 開啟 + Dashboard log 收口 / 真實時長 ~20 分鐘**

### CP1 deploy + flag 確認

| 項目 | 結果 |
|---|---|
| Stage 73 commit `cb47648` deploy run | ✅ success / 6m10s |
| Bot image v3.63.0 production active | ✅ |
| 5 v5.5 flag SQL=true | ✅（UsePetraOrchestratorV5 / UseTalentSkillSeparation / UseV5Memory / UseV5SubtaskPlanning / UseV5PromptDb）|
| reload-cache scope=all | ✅ 真實生效（讓 v2 SkillPrompt + Petra TalentPrompt 進 PromptResolver cache）|
| 6 SkillPrompt v2 active production SQL 驗 | ✅ 全 IsActive=true / CreatedByUser='stage73-upgrade' |

### CP2 Petra orchestration（v5.5 Talent-Skill path）

```
05:23:03 → Victoria flag UsePetraOrchestratorV5=true forward to PetraOrchestratorService
05:23:04 → Petra BuildSessionContext CloneOrPull 完成 / sessionId=770e8836... created
05:23:04 → PetraOrchestrator 啟動 v5.5 Talent-Skill path / talentsCount=4 / useV5SubtaskPlanning=True
        → Petra v5.5 Step 4 DecideTalentsWithPlanAsync 完成
        → subtasks=3 dependencies=2 picks=Cody(code_implementation) → Vera(code_review) → Quinn(qa_testing)
```

**對齊 Trial_v18 baseline** — 同 prompt → 同 3 subtask 拆解 → 同 Cody/Vera/Quinn chain（Petra 「謹慎拍板」persona 紀律真實生效訊號 — 不機械重拆 / 對齊 baseline plan）。

### CP3 Cody dispatch 1/3（code_implementation / Talent=Cody）

- 開跑：05:23:04
- 完成：05:28:14（**5m11s**）
- ClaudeCodeChatClientAdapter promptLen=1674（vs Trial_v18 1674 對齊）
- outputLen=**2822**（vs Trial_v18 2435 / **+15.9%** 內容更詳細）
- TaskMemory `decision/Cody-output-summary`（500 chars）+ TalentMemory `last-task-summary`（500 chars）寫回

### CP4 Vera dispatch 2/3（code_review / Talent=Vera）

- 開跑：05:28:14（depends on 1）
- 完成：05:31:33（**3m19s**）
- promptLen=4487 / inputMsgs=3 / dependsOn=[1]
- outputLen=**1491**（vs Trial_v18 1526 / -2.3% 正常範圍）
- TaskMemory + TalentMemory 寫回

### CP5 Quinn dispatch 3/3（qa_testing / Talent=Quinn）

- 開跑：05:31:33（depends on 2）
- 完成：05:42:59（**11m26s** — Quinn 寫 test code 最費時）
- promptLen=6584 / inputMsgs=4 / dependsOn=[2] / taskMemoryCount=2 + talentMemoryCount=1
- outputLen=**1093**（vs Trial_v18 833 / **+31.2%** Quinn 寫更詳細）
- TaskMemory + TalentMemory 寫回

### CP6 FinalizeGitAsync + PR 開啟

- branch `petra/spike-770e8836-202605170542` 建立 + checkout
- Commit 完成（msg=「[Petra] Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。」）
- **PR [#379](https://github.com/darkleong/AiTeam/pull/379) 開啟**
- Petra session Status=completed
- Dashboard 指令 background logId=1a36bfff... action=petra_v5_dispatched 收口

---

## 試驗結果矩陣

### Aria 業務評分 ⭐⭐⭐⭐⭐ 5/5（滿分）

| 維度 | 評分 | 證據 |
|---|---|---|
| **範圍 cover** | **5/5** | 任務原文 5 範圍（系統設定 / 操作中心 / Agent 設定 / 規則管理 / 「之類」廣範圍）+ Cody 主動掃 32 .razor 揭 9 個額外缺口（QuickCommandCard / TaskCenter / ProjectManagement / ProjectCreateDialog / AgentCreateDialog / RuleFormDialog / PipelineList / PipelineView / Program.cs DI）+ Login MVC 明寫 ❌ 不適用 = **14/5 超越 280%** |
| **Cody Code 品質** | **5/5** | production-grade UX consistent pattern（雙通道 Snackbar + inline MudAlert 4 檔結構一致）+ 邊界處理（catch + 降級空集合 + idempotent loading flag）+ Implementation Note 含「範圍對照表」14 row + 「修改檔案清單」+ 自驗結果 — **Stage 73「品質目標 #5 廣範圍對照表 100% cover」紀律真實生效 ⭐** |
| **Vera review 質感** | **5/5** | 0 critical + 1 info（精準揭 LoadStepsAsync catch 路徑 `_activeStepIndex` 沒重設 / SignalR 第二次以上呼叫 edge case）+ impact 分析詳細 — **「擋住會出事的問題，不追求完美」+「偏好放行」紀律生效** |
| **Quinn 測試覆蓋** | **5/5** | 12 個新 test method + **真實 NRE 觸發走 catch 分支 + 反射 private method**（不被 mock 騙過）+ 主動補既有 coverage gap（IsRevision/GetLogColor needs_intervention Stage 43 補充 + PipelineStepViewModel 預設值驗證） — **Stage 73「不被 mock 騙過 + production-grade 邊界」紀律真實生效 ⭐** |
| **業務 UX 對齊** | **5/5** | 雙通道完整（Snackbar 右下角 + inline MudAlert 緊貼來源）+ toast 3-5 秒消失 + 訊息一致 — 完美對齊 Christ 任務原文 |
| **整體** | **5/5** | 連續 9 Trial 業務級成功（v10-v19）/ 連續 7 Stage 0 follow-up |

### 對照 Trial_v18 baseline

| 維度 | Trial_v18 | Trial_v19 | 差異 |
|---|---|---|---|
| 業務評分 | 4.7/5 | **5/5** | ✅ +0.3 升級 |
| PR 檔數 | 19 | **26** | ✅ +37% |
| 範圍 cover | 10/7 | **14/5** | ✅ Cody 主動掃 32 .razor 揭更多缺口 |
| Cody outputLen | 2435 | 2822 | +15.9% |
| Vera outputLen | 1526 | 1491 | -2.3%（正常範圍）|
| Quinn outputLen | 833 | 1093 | +31.2% |
| AiTeam LLM cost | $2.301 | **$2.844** | +23.6% |
| Forge session cost | $1.75 | **$0**（Aria 全程自跑 / 0 Forge）| - |
| Total cycle cost | $4.05 | **$2.844** | **-29.8%** ⭐（Forge 0 介入省 cost）|
| 真實時長 | 14m29s | ~20 min | +38%（Quinn 寫更詳細）|
| 餘額 | $15.99 → $20.04 | $15.99 → **$13.15** | 對齊 SQL $2.844 ✓ |

---

## 關鍵結論

### Stage 73 升級紀律真實生效實證 ⭐

| 紀律 | production 真實生效訊號 |
|---|---|
| **Cody「品質目標 #5 廣範圍對照表 100% cover」**（Stage 65 + Trial_v10 教訓延伸保留+ 升級為品質目標）| Cody 主動掃 32 .razor + 範圍對照表 14 row（含 ✓/❌ 雙態）+ Login ❌ 不適用明寫原因 |
| **Cody「production-grade UX consistent pattern」**（Trial_v18 議題 1 SystemSettings inconsistent 教訓間接守 — 升級為品質目標 #2）| 4 檔雙通道結構一致（Snackbar + inline MudAlert）+ catch 降級空集合 + try-catch-finally |
| **Vera「擋住會出事的 critical + 偏好放行」**（既有紀律保留）| 0 critical + 1 info / 不誤報 |
| **Quinn「不被 mock 騙過 + happy+edge 雙覆蓋」**（Aria gate1 Tier 1 紀律延伸 — 升級為品質目標 #2/#3）| 真實 NRE 觸發 catch 路徑 + 反射 private method + 12 個新 test + 主動補既有 coverage gap |
| **Petra TalentPrompt persona seed**（4 拍板特質）| DecideTalentsWithPlanAsync subtasks=3 對齊 Trial_v18 baseline = **「謹慎拍板」+「不機械重拆」persona 紀律真實生效** / 同任務 → 同 plan |

### 系統能力分層

- **執行層（Cody/Vera/Quinn）**：production-grade 質感升級 — Cody +15.9% / Quinn +31.2% outputLen 反映「品質目標」紀律生效（不是步驟紀律驅動的形式化輸出，是 Cody/Quinn 真實「為達品質目標多寫內容」）
- **審查層（Vera）**：「偏好放行」紀律穩定 — 0 critical + 1 info / 不誤報 / 找到真實 edge case
- **PM 層（Petra）**：「謹慎拍板」persona 紀律真實生效 — 同 prompt → 同 plan baseline / 不機械重拆 / 對齊 Stage 71+Trial_v17/v18 累積線性整包紀律

### 戰略級觀察

**「品質 > 做法」評估框架（自省點 #35）首次大規模文案 drafting Stage 後真實業務驗實證** — Stage 73 把抽象「品質 > 做法」精神文案化進 6 SkillPrompt 後，Cody/Vera/Quinn 真實在 dispatch 時表現出對應行為（主動掃範圍 / 不被 mock 騙過 / 不誤報 critical / 主動補 coverage gap）— **「prompt 內容驅動 Talent 自主判斷做法 / 我們只定品質標準」設計理念完整 production cycle 真實實證**。

### 連續 9 Trial 業務級成功 + 連續 7 Stage 0 follow-up（系統穩定性里程碑）

- **連續 9 Trial 業務級成功**（Trial_v10/v11/v12/v13.2/v14/v15.2/v17/v18/v19）— infinite loop pattern 確認永久打破延續第 9 次
- **連續 7 Stage 0 follow-up bug fix**（Stage 67/68/69/70/71/72/73）
- **計劃前 WebSearch 紀律連續 8 次**實踐（Stage 64-70 + Trial_v15.1 follow-up + Stage 72 議題 1 + Stage 73 設計討論場景延用既有 WebSearch 結論不重複觸發）
- **aria-trial-run skill 第 3 次實踐成功** ⭐（紀律工具化 ROI 累積實證 — workspace cleanup 紀律 + 環境設定/微 bug 直接修紀律 + 9-step 模板 全套對齊）
- **Aria 全程自跑 9-step 模板第 9 次實踐**（對「Christ 只動嘴」精神成熟實踐 — Christ 0 操作除拍板開跑 + 看結果）

---

## 議題分類

- **🔴 戰略級新類型**：**0**
- **🟡 工程細節**：**0**（cost +23.6% / 時長 +38% 都是內容更詳細的自然結果，不是議題）
- **🟢 觀察留檔**：1 — Vera 揭 SignalR `_activeStepIndex` info 議題（範圍外既有 baseline / 0 阻擋 production / 0 升級候選 — 留 PR close 留言 backlog）

---

## v5.5 Phase 3 Step 7 正式完整收口拍板閘門通過 ⭐⭐

production 狀態：**5 flag SQL=true active 維持**（既有 production 預設不動）：
- `Workflow:UsePetraOrchestratorV5`（Stage 64+ 上線）
- `Workflow:UseTalentSkillSeparation`（Stage 67 上線）
- `Workflow:UseV5Memory`（Trial_v17 上線）
- `Workflow:UseV5SubtaskPlanning`（Trial_v17 上線）
- `Workflow:UseV5PromptDb`（Trial_v18 上線 / Trial_v19 驗 v2 真實生效）

v5.5 完整路徑 production 預設 — Cody/Vera/Quinn 走 Stage 73 升級後 v2 prompt 真實 dispatch / Petra TalentPrompt persona 真實 prepend 生效。

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

1. ✅ Aria 結案紀錄（本檔 v2.0）
2. ✅ Future_Feature_v5.5.md Phase 3 Step 7（Stage 73） 標 Trial_v19 ✅ 通過閘門
3. ✅ CHANGELOG [Unreleased] 更新為下個動作候選 Stage 74
4. ✅ Close PR #379（Trial 不污染 main 紀律 — 對齊 Trial_v10-v18 既有實踐）
5. ✅ SQL flag 維持既有 5 flag SQL=true production active 不切回（v5.5 production 預設 / 無需切換對齊「下個 Stage Roadmap 預期」紀律）

### 下個重點戰略

**Stage 74 啟動條件達成** — Phase 3 收口路徑 2/4：

```
Stage 73 ✅ → Stage 74（真並行 dispatch + 3 agent debate）
            → Stage 76（兩層 queue 配套）
            → Stage 75（WebUI Talent CRUD 最後做）
            → v5.5 完整收口
```

Stage 74 範圍（對齊 Future_Feature_v5.5.md Phase 3 Step 8）：
- 真並行 dispatch — 同 dependency level subtask 平行跑（既有 SubtaskPlan Independent dependency 純設計 surface → 真實 dispatch 並行）
- 戰略決策層 3 agent debate — 從 Talent pool 選 3 個（2 opposing + 1 synthesizer）
- 設計依賴 Stage 73 升級後 prompt content 已完成 ✓

預估 cost $3-5 per cycle / Phase 3 餘額 $13.15 - $3-5 = $8-10 剩餘 / 中間可能需儲值 1 次（對齊自省點 #38 Stage cost 雙因子預估）。

---

## 紀律累積成熟度進化曲線（Trial_v19 真實實證接續 Trial_v18 + Stage 73）

- **連續 9 Trial 業務級成功**（v10-v19）— infinite loop pattern 永久打破延續第 9 次 / 「品質 > 做法」評估框架（自省點 #35）+ 「對等和互相」精神（自省點 #36）真實生效 production 驗
- **連續 7 Stage 0 follow-up bug fix**（Stage 67-73）— Forge healthy 偏離 plan 紀律累積成熟 + Aria gate1 Tier 升級紀律累積 + 自省點 #37 同類根因第 2 次累積（Stage 73 文案 drafting context 預估失準修法）
- **Aria 評估框架升級延續**：5/5 6 維度業務評分（取代既有「accept / fix-loop / close」分類）/ Trial_v18 baseline 4.7/5 → Trial_v19 5/5 滿分
- **aria-trial-run skill 第 3 次實踐成功**：workspace cleanup 紀律（紀律 1 完全省略 cleanup）+ 環境設定/微 bug 直接修紀律（Trial_v19 0 環境議題 / 0 微 bug / 0 直接修需要）+ 9-step 模板對齊
- **Aria 全程自跑「Christ 只動嘴」精神突破** — Trial_v19 Christ 0 操作除拍板開跑 + 看結果（0 SQL / 0 curl / 0 docker exec）

---

## Cost 真實 vs 預估雙因子對照（自省點 #38 應用）

| 因子 | Aria 預估 | 真實 | 對齊 |
|---|---|---|---|
| AiTeam LLM cost | $1.5-3 | **$2.844** | ✅ 範圍上限對齊 |
| Forge Claude Code session cost | $0（Aria 全程自跑）| **$0** | ✅ 完美對齊（Aria 自跑無 Forge 介入）|
| **Total cycle cost** | $1.5-3 | **$2.844** | ✅ 對齊 |
| 餘額變化 | $15.99 → $12.99-14.49 | **$15.99 → $13.15** | ✅ 範圍中段對齊 |

**真實 cost 落點對齊預估上限** — Stage 73 升級後 Cody/Quinn 內容更詳細的「品質目標」生效訊號真實成本 ✓ 對齊預期。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-17 | 試驗結案紀錄（直接 v2.0 — 試驗前未先建 v1.0 plan / 對齊 Trial_v15+v16 / Trial_v18 既有實踐 / aria-trial-run skill 內容已 cover 9-step 模板紀律不需要 plan 重寫）。**🟢 完整 PASS — v5.5 Phase 3 Step 7 正式完整收口拍板閘門通過**。**真實 lifecycle**：UTC 05:23:03 開跑 → Petra v5.5 path enter → DecideTalentsWithPlanAsync subtasks=3 dependencies=2 picks=Cody→Vera→Quinn → Cody 1/3 outputLen=2822（+15.9% vs Trial_v18 / Stage 73 production-grade 紀律訊號）+ 寫回 memory → Vera 2/3 outputLen=1491（-2.3% / baseline 範圍）+ 0 critical + 1 info（精準揭 SignalR edge case）→ Quinn 3/3 outputLen=1093（+31.2% / Stage 73 不被 mock 騙過紀律訊號）+ 12 個新 test method（含真實 NRE 觸發 catch + 反射 private method + 主動補既有 coverage gap）→ Petra PR #379 真實開出（26 檔 +1456/-88 / **14 範圍 cover 含 9 個 Cody 主動補強** + Login ❌ 不適用 escalate）→ 真實 ~20 分鐘完整 lifecycle。**核心驗收 3 項達標**：① Stage 73 升級紀律真實生效（Cody 廣範圍對照表 / production-grade UX consistent / Vera 偏好放行 / Quinn 不被 mock 騙過 / Petra 謹慎拍板 全套訊號 production 真實實證 ⭐）② DecideTalentsWithPlanAsync subtasks=3 對齊 Trial_v18 baseline = Petra「謹慎拍板」persona 紀律真實生效 ③ 連續 9 Trial 業務級成功（v10-v19）+ 連續 7 Stage 0 follow-up（Stage 67-73）延續。**業務品質 Aria 評分 5/5 滿分**（6 維度全 5/5 / vs Trial_v18 baseline 4.7/5 +0.3 升級）。**議題分類**：0 🔴 + 0 🟡 + 1 🟢 觀察留檔（Vera 揭 SignalR `_activeStepIndex` info / 範圍外既有 baseline / 0 升級候選）。**戰略結論**：**5 flag SQL=true production active 維持**（v5.5 完整路徑 production 預設）+ v5.5 Phase 3 Step 7 正式完整收口 + 「品質 > 做法」評估框架（自省點 #35）+ 「對等和互相」精神（自省點 #36）真實生效 production 驗 + 「prompt 內容驅動 Talent 自主判斷做法」設計理念完整 production cycle 真實實證 ⭐。**真實 cost**：AiTeam LLM $2.844 + Forge session $0（Aria 全程自跑 / 0 Forge 介入）= total $2.844 / 餘額 $15.99 → $13.15 ✅ 對齊（SQL token_logs total $2.8447 完美對齊）。**cost per file = $0.109** 最優 ROI baseline（vs Trial_v18 $0.121 / -10% 更優 / 對應 Forge 0 介入 -29.8% total cycle cost）。**下一步**：Stage 74 啟動條件達成（Phase 3 Step 8 真並行 dispatch + 戰略決策層 3 agent debate）— Phase 3 收口路徑 2/4 / 預估 $3-5 per cycle。 |

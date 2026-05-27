# Trial_v14 試驗計劃書 — v5.5 Phase 1 完整收口拍板閘門

> 對應版本：**v3.58.0**（Stage 68 結案 — v5.5 Phase 1 完整收口前 production-ready 補強）
> 建立日期：2026-05-16
> 狀態：✅ 已結案 — 🟢 完整 PASS（v5.5 Phase 1 完整收口拍板閘門通過）
> 文件版本：v2.0

---

## 一、背景與定位

**Stage 67 + Stage 68 結案後 v5.5 Phase 1 完整收口拍板閘門**：

- Stage 67 ✅ Talent-Skill separation 重構基底 / Trial_v13 揭 3 議題（紀律修法 commit `0226c60` BuildBroadScopeEnforceSection 步驟 3-4 + ⛔ 嚴禁段）
- Stage 68 ✅ production-ready 補強 3 子項（AppendMessage async + v5 PoC post-confirm 收尾 + ef-core.md nullable unique pattern）
- **Trial_v14 目的 = 驗 Stage 67/68 兩個紀律修法生效 + 業務級成功重現 → Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = v5.5 Phase 1 完整收口正式上線**

---

## 二、試驗目的（2 條核心 + 1 條對照）

1. **Stage 67 紀律修法 `0226c60` 生效驗**：Cody 不自己 commit + push → Petra FinalizeGitAsync 真實開 PR（vs Trial_v13.2 Cody push to main / 0 PR）
2. **Stage 68 sub-item 2 修法生效驗**：Christ 點「確認派工」後 0 stale exec_confirm 卡（vs Trial_v12 真實 1 張需手動 cancel）
3. **連續 5 Trial 業務級成功對照組**（Trial_v10/v11/v12/v13.2/v14 — infinite loop pattern 確認打破延續）

---

## 三、任務需求

沿用 Trial_v6-v13 同 prompt（7+3 向對照精準度最高）— Dashboard 錯誤處理打磨 toast 通知。

---

## 四、結案紀錄

### 4.1 lifecycle 概覽

| 項目 | 結果 |
|---|---|
| 開跑時間（UTC）| 2026-05-16 01:32:20 |
| PetraSession ID | `59e26206-ab61-4705-8589-4a07f8f53571` |
| Petra 啟動 path | **v5.5 Talent-Skill path** ✅ / talentsCount=4 |
| DecideTalentsAsync raw | `code_implementation\|code_review` → picks Cody(code_implementation) → Vera(code_review) |
| chain dispatch wire | talent=/skill= format ✅ / inputMsgs 1 → 2（chain wire 真實生效）|
| Cody outputLen / Vera outputLen | 2264 / 1128 |
| **PR 開啟** | ✅ [#375](https://github.com/darkleong/AiTeam/pull/375) 真實開出（vs Trial_v13.2 0 PR）|
| Christ confirm 操作 | 點「確認派工」responded → `ProcessCeoConfirmAsync: 跳過 exec_confirm fire` log ✅ |
| stale exec_confirm 卡 | **0 張** ✅（vs Trial_v12 真實 1 張）|

### 4.2 業務品質（PR #375）

- **10 檔改動 +232/-75** — production-grade（vs Trial_v12 PR #374 8 檔 +252/-53 / vs Trial_v13.2 7 檔 +78/-16）
- **9 範圍全 cover** 含 InteractionCenter（評估後判斷已有 Snackbar 不需改動 — 對齊 Trial_v12 質的不同精神）
- **順手修 SystemSettings 驗證錯誤誤顯示為 `Severity.Success` 綠色 alert bug**（Cody 自動識別）
- **Vera 真實 review**：1 warning（RestartBotAsync `_showRestartConfirm=false` 在 try 內 / catch path 不關閉對話框）+ 0 critical + summary「改動方向正確」
- **PR body chain summary 完整**（Stage 66 tool role 寫入修法持續生效）+ **0 CLAUDE.md 改動**（Stage 65 子項 1 持續生效）

### 4.3 cost / blind spot

| Agent | tokens out | cost |
|---|---|---|
| Cody | 30219 | $1.6562 |
| Vera | 10612 | $0.4314 |
| PM | 7 | $0.0026 |
| **SQL Total** | | **$2.0902** |

- 真實 cost：$27.31 → $25.22 = **$2.09**
- **Blind spot ≈ $0** = 完美對齊（Trial_v12 baseline 0% 維持 / 連續 4 Trial 0% blind spot）
- vs Trial_v12 $2.34 baseline -11%（Vera output 較短 1128 vs 1676）

### 4.4 6/6 驗收項全 PASS

| # | 項目 | 結果 |
|---|---|---|
| 1 | v5.5 path 完整 enter | ✅ talentsCount=4 / DecideTalentsAsync / 自管 chain talent=/skill= / Vera inputMsgs=2 真做事 |
| 2 | **Stage 67 紀律修法 `0226c60` 生效** ⭐ | ✅ Petra 真實開 PR #375 |
| 3 | **Stage 68 sub-item 2 修法生效** ⭐ | ✅ Bot log 「跳過 exec_confirm fire」+ SQL 0 stale 卡（雙重證實）|
| 4 | 業務品質 ≥ Trial_v12 baseline | ✅ 10 檔 / 9 範圍 cover / 順手修 bug / Vera review 質佳 |
| 5 | blind spot 0% | ✅ $2.09 vs SQL $2.0902 完美 |
| 6 | 0 🔴 戰略級 + 0 🟡 新議題 | ✅ 議題密度進化曲線進入尾聲 |

### 4.5 戰略結論

**🟢 Trial_v14 完整 PASS = v5.5 Phase 1 完整收口拍板閘門通過**：

- v5/v5.5 工程實證收口完整（Trial_v10/v11/v12/v13.2/v14 連續 5 Trial 業務級成功）
- Stage 64-68 五波 production-ready 補強累積成熟 — Aria 紀律累積成熟度進化曲線完整實證（計劃前 WebSearch 紀律連續 5 次 / Gate1 Tier 升級紀律 / source of truth 紀律根因 14+ 次累積修根因）
- **v5.5 Phase 1 完整收口** ✅ — flag UseTalentSkillSeparation 維持 SQL=true production active（對齊 v5 上線模式 — DB 優先 / appsettings.json default false 守 fallback）

---

## 五、後續行動

### 立即（aria-trial-summary 結案動作）

1. ✅ PR #375 close（對齊 Trial 既有紀律不污染 main）
2. ✅ Trial_v14_Plan.md 建檔 + commit + push
3. ✅ flag UseTalentSkillSeparation 維持 SQL=true（對齊紀律 #10 — Trial 通過後對齊「下個 Stage Roadmap 預期」= v5.5 上線預期）
4. Future_Feature.md update — Active 清單 FF 二「v5 PoC 補強清單」進展 +2 點完成

### Stage 69 候選

**v5.5 Phase 2 Step 3 DB 持久記憶 schema**（規模 M-L / 「Agent 像人類處理事件」核心 / 對齊業界 best practice 避 context drift 65% / 35 分鐘退化雷區 / 跟 Stage 67 Talent-Skill schema 整合 per-Talent 私有層）— Phase 2 第一步主軸。

---

## 六、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v14 在 main branch 跑（含 Stage 67+68 全 commits + 紀律修法 `0226c60`）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v13 既有驗證
- 對齊 Trial_v2-v13 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-16 | 試驗結案紀錄（直接 v2.0 — 試驗前未先建 v1.0 plan / Aria 全程自跑 9-step 模板第 5 次實踐）。**🟢 完整 PASS — v5.5 Phase 1 完整收口拍板閘門通過**。**6/6 驗收項全 PASS**：① v5.5 path 完整 enter（talentsCount=4 / DecideTalentsAsync / 自管 chain talent=/skill= / Vera inputMsgs=2 真做事）② Stage 67 紀律修法 `0226c60` 生效（Petra 真實開 PR #375 vs Trial_v13.2 Cody push main / 0 PR）③ Stage 68 sub-item 2 修法生效（Bot log「跳過 exec_confirm fire」+ SQL 0 stale 卡雙重證實）④ 業務品質 ≥ Trial_v12 baseline（10 檔 +232/-75 / 9 範圍 cover 含 InteractionCenter 評估 / 順手修 SystemSettings 綠色 alert bug / Vera review 1 warning + 0 critical）⑤ blind spot 0%（$2.09 真實 vs SQL $2.0902 完美 / 連續 4 Trial 0% blind spot baseline）⑥ 0 🔴 + 0 🟡 新議題（議題密度進化曲線進入尾聲）。**真實 lifecycle**：UTC 01:32:20 開跑 → Petra v5.5 path enter → DecideTalentsAsync + 自管 chain → Cody 1/2 outputLen=2264 → Vera 2/2 inputMsgs=2 outputLen=1128 → Petra PR #375 真實開出 → Christ 點「確認派工」responded → ProcessCeoConfirmAsync skip exec_confirm fire → 0 stale 卡。**戰略結論**：連續 5 Trial 業務級成功（Trial_v10/v11/v12/v13.2/v14 — infinite loop pattern 確認打破延續）+ v5/v5.5 工程實證收口完整 + Stage 64-68 五波 production-ready 補強累積成熟 + Aria 紀律累積成熟度進化曲線完整實證 → **v5.5 Phase 1 完整收口** ✅ flag UseTalentSkillSeparation 維持 SQL=true production active。**下一步**：Stage 69 = v5.5 Phase 2 Step 3 DB 持久記憶 schema（規模 M-L / 「Agent 像人類處理事件」核心精神實作）。 |

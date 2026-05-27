# Trial_v13 試驗計劃書 — Stage 67 v5.5 升級首發 Talent-Skill separation 重構基底真實 task 驗

> 對應版本：**v3.57.0**（Stage 67 結案 — v5.5 升級首發 Phase 1 Step 2 Talent-Skill separation 重構基底，2026-05-15）
> 建立日期：2026-05-15
> 狀態：✅ 已結案 — 🟡 部分成功（v5.5 path 工程實證 + 揭關鍵紀律缺口 + 修法 0226c60）
> 文件版本：v2.0

---

## 一、背景與定位

**Stage 67 結案後 v5.5 升級首發完整實證 + v5.5 Phase 1 完整收口拍板前最後一道閘門**：

- v5 動態架構 2026-05-14 正式上線後第一個架構級升級（v5.5 Talent-Skill separation 重構基底落地）
- Stage 67 Forge 自驗 4 場景全 PASS + Aria gate1 Tier 0+1+2+Tier 3 #11 架構級重構 baseline 首次實踐 + 6 Talent + 8 TalentSkill 落地 + Migration `20260515135610` 結案
- **Trial_v13 目的 = 驗 Stage 67 v5.5 path 真實 task 業務級成功 → Christ 拍板切 `UseTalentSkillSeparation` default true = v5.5 Phase 1 完成 → 進 Phase 2 Step 3 DB 持久記憶 schema**

**Trial_v13 定位**：v5.5 Phase 1 完整收口拍板前最後一道閘門 — 通過後 Christ 拍板切 default flag。

---

## 二、試驗目的（3 條）

1. **驗 Stage 67 v5.5 path 真實 task 業務級成功**（核心）：
   - flag `Workflow:UseTalentSkillSeparation=true` 真實 enter v5.5 path（vs v5 既有 IAgentTool path）
   - `DecideTalentsAsync` 動態決策 Skill 序列 + Talent pool lookup（看 Skill 找 Talent / round-robin）
   - 自管 chain dispatch format = `talent=/skill=`（vs v5 既有 `worker=/capability=`）
   - Cody + Vera 真實做事 + PR 真開 + 對齊 Trial_v12 業務品質

2. **連續 4 Trial 業務級成功重現**（infinite loop pattern 確認打破延續）：
   - v5.5 path 跑通 + 0 戰略級新類型 + 0 deliverable 失敗
   - cost ≤ $5 對齊 Trial_v10/v11/v12 baseline

3. **切 default flag 拍板實證**（v5.5 正式上線）：
   - 跑成功 + 0 🔴 + Stage 67 baseline 落地 → Christ 拍板切 `UseTalentSkillSeparation` default true

---

## 三、任務需求

**沿用 Trial_v6-v12 同 prompt**（7+2 向對照精準度最高）：

> Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。
>
> 最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。
>
> （以下省略 — 同 Trial_v12 完整 prompt）

---

## 四、結案紀錄

### 4.1 兩輪 lifecycle 概覽

| | Trial_v13.1（v5 既有 path 意外觸發）| Trial_v13.2（v5.5 path 真實驗） |
|---|---|---|
| sessionId | `4f666c43-2f1b-4d45-91f4-65dfed2726d1` | `b36f03f0-f4f1-4f3b-b9d5-1b4b911958aa` |
| Petra 啟動 path | v5 既有 IAgentTool / toolsCount=7 | **v5.5 Talent-Skill / talentsCount=4** ✅ |
| DecideTalentsAsync raw | `code_implementation\|code_review` | `code_implementation\|code_review` |
| picks | Cody → Vera (worker=) | **Cody(code_implementation) → Vera(code_review) (talent= skill=)** ✅ |
| chain dispatch wire | inputMsgs=1 → 2 (chain OK) | inputMsgs=1 → 2 (chain OK) |
| Cody outputLen | 1696 | **2174**（+28%）|
| Vera outputLen | 1609 | 1676 |
| Cody 行為 | push Trial_v12 spike branch（殘留）| **push origin/main**（觸發 deploy） |
| Petra FinalizeGitAsync | skip 0 unstaged | skip 0 unstaged |
| PR 開啟 | 0 | 0 |
| 業務品質 | 業務級成功（無 PR）| 業務級成功 + 順修 bug + 6/6 cover（含 InteractionCenter） |

### 4.2 揭 3 議題

1. **🟡 Aria handoff briefing 紀律錯傳 — reload-cache scope=workflow 不存在**
   - Trial_v12 v2.0 + Aria handoff briefing 都寫「reload-cache scope=workflow」
   - 真實 [InternalController.cs:39](src/AiTeam.Bot/Api/InternalController.cs#L39) 只支援 `rules / agents / agent-config / all`
   - 用 `scope=workflow` API 回 success 但 InvalidateCache 0 呼叫 → Trial_v13.1 意外走 v5 既有 path
   - 修法：本 plan 揭露 + memory step 0 升級候選（forge-self-verify skill / Aria reload-cache 對應表）

2. **🟡 Trial 之間 workspace branch 沒 cleanup**
   - Trial_v12 結束後 spike branch `petra/spike-e8c26d5a-202605141409` 留在 `/tmp/aiteam-workspace/AiTeam`
   - Trial_v13.1 啟動 Petra 沿用同 workspace + 同 branch → Cody 在 Trial_v12 殘留 branch commit + push
   - 修法：Aria 自跑 Trial 9-step 模板加 step 0「workspace branch 確認回 main + reset --hard origin/main」紀律候選

3. **🚨 Cody push to main 紀律缺口 — 修根因 commit 0226c60**
   - 真實 root cause：[ClaudeCodeChatClientAdapter.cs:230-239](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs#L230) Stage 66 既有 prepend `BuildBroadScopeEnforceSection()` 步驟 3-4 措辭「commit message 必含...」「commit / PR 前必須 cover 完」自己預設 Cody 會 commit + 開 PR
   - 直接矛盾 [CLAUDE_Cody.md:26](src/AiTeam.Bot/Resources/CLAUDE_Cody.md#L26)「不要 commit 或 push」+ v5 path「Cody 改 code / Petra finalize」設計
   - Cody 模型 prioritize user prompt > system prompt → 跟著 user prompt 紀律 commit + push
   - **Trial_v12 沒揭真實**：Trial_v12 Cody 也 push spike branch + Petra FinalizeGitAsync 開 PR #374 wrap，假象「Petra 開 PR」實際 Cody 已 commit
   - 修法：commit `0226c60` 改 `BuildBroadScopeEnforceSection` 步驟 3-4 措辭 + 新增「⛔ 嚴禁自己 git commit / git push」段（純 user prompt prepend 修法 / 不污染 CLAUDE_Cody.md 跨專案守則）

### 4.3 戰略結論

**🟡 部分成功 — v5.5 path 工程實證完整 + 揭關鍵紀律缺口已修**

- ✅ **v5.5 path 完整 enter + 真實生效**（DecideTalentsAsync / 自管 chain dispatch talent=/skill= format / Vera 真做事 inputMsgs=2）
- ✅ **Stage 67 重構基底業務級實證**（Cody 6/6 cover + 順修 bug + Vera review JSON 0 critical / 1 warning / info）
- ✅ **Stage 67 對 v5 既有 path 0 regression**（Trial_v13.1 v5 既有 path 也跑通 — fallback 紀律有效）
- ✅ **連續 4 Trial 業務級成功重現**（Trial_v10/v11/v12/v13.2 — infinite loop pattern 打破持續）
- ⚠️ **PR 沒開**（Cody push to main / spike branch → Petra FinalizeGitAsync skip）— 不算 v5.5 設計問題，是 Stage 66 既有紀律措辭跨紀律矛盾揭 → 修法 `0226c60` 已 push

### 4.4 cost / 時程

- 真實 cost：**$3.18**（Christ 餘額 $30.49 → $27.31）
- SQL token_logs SUM：**$3.1869**（Cody $2.4324 / Vera $0.7492 / PM $0.0052）
- **Blind spot 0%** ✅（對齊 Trial_v12 baseline 維持）
- 持續時間：兩輪 lifecycle 合計 ~37 分鐘（13.1 ~6 分 / 13.2 ~7 分 + Aria 觀察 + 修根因 + revert）

### 4.5 修根因動作清單

| Hash | 動作 |
|---|---|
| `8b24192` | Revert `cd891fa` — 把 Trial_v13.2 Cody push 到 main 的功能變更整套 reverse（避免 Trial_v14 沒新 prompt 對標） |
| `0226c60` | fix(petra): 修 Cody push to main 紀律缺口 — `BuildBroadScopeEnforceSection` 步驟 3-4 措辭 + 新增「v5 Petra 接管」嚴格段 |

---

## 五、後續行動

### 立即（aria-trial-summary 結案動作）

1. ✅ SQL UPDATE `Workflow:UseTalentSkillSeparation=false` + reload-cache scope=all（對齊紀律 #10 Trial 結束切回 default）
2. ✅ Trial_v13_Plan.md 建檔 + commit + push
3. Future_Feature.md / Future_Feature_changelog.md / Future_Feature_v5.5.md 更新

### Trial_v14（v5.5 Phase 1 完整收口拍板閘門）

**啟動條件**：跟下個 Stage 完成後一起跑（攤平 Trial cost / 時間 — Christ 2026-05-16 拍板「先記錄繼續實作 Stage 再一起試驗」紀律）

**驗收點**：
- Stage 66 prepend 紀律修法（`0226c60`）生效 — Cody 不自己 commit / Petra FinalizeGitAsync 真實開 PR
- v5.5 path 完整業務級成功（PR 真開 + 對齊 Trial_v12 baseline）
- 通過後 Christ 拍板切 `UseTalentSkillSeparation` default true = **v5.5 Phase 1 完整收口 + v5.5 正式上線**

### Step 0 升級候選（兩條 + 一條新）

| # | 候選 | 升級對應檔 |
|---|---|---|
| 1 | reload-cache scope 對應表紀律（揭 briefing 錯傳 + Trial_v12 v2.0 紀錄錯誤）| `.claude/skills/forge-self-verify/SKILL.md` + workflow_aria.md 第三節 A 第 7 條延伸範圍段 #5 |
| 2 | step 3 reload-cache 後必驗證真實生效紀律（API 回 success 騙人）| 同上 |
| 3 | Trial 之間 workspace branch 確認回 main + reset --hard origin/main 紀律 | workflow_aria.md / aria-trial-summary skill SOP step 0 |
| 4 | PostgreSQL NULL ≠ NULL unique（Stage 67 follow-up 既有候選） | docs/conventions/ef-core.md |
| 5 | workflow_aria.md 第三節 D Gate1 Tier 對應表補架構級重構 Tier 0+1+2+Tier 3 #11 baseline（Stage 67 首次實踐既有候選） | workflow_aria.md |

留 Stage 68 規劃前 session 順手做。

---

## 六、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v13 在 main branch 跑（含 Stage 67 commits 全集 + Directory.Build.props v3.57.0）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v12 既有驗證
- 對齊 Trial_v2-v12 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-15/16 | 試驗結案紀錄（直接 v2.0 — 試驗開跑前未先建 v1.0 plan，照 Aria handoff briefing 9-step 模板直接跑）。**🟡 部分成功 — v5.5 path 工程實證完整 + 揭關鍵紀律缺口已修**。**兩輪 lifecycle**：Trial_v13.1（reload-cache scope=workflow 錯誤 → 意外走 v5 既有 path）→ 揭 root cause + cleanup workspace + 重跑 Trial_v13.2（v5.5 path 真實 enter / DecideTalentsAsync + 自管 chain dispatch talent=/skill= format / Vera inputMsgs=2 真做事 / Cody 7 檔 +78/-16 6/6 cover 含 InteractionCenter + 順修 SystemSettings 驗證錯誤綠色顯示 bug）。**揭 3 議題**：① 🟡 reload-cache scope=workflow 不存在（briefing + Trial_v12 v2.0 紀錄錯誤 — InternalController 真實只支援 rules/agents/agent-config/all）② 🟡 Trial 之間 workspace branch 沒 cleanup（Trial_v12 spike branch 殘留導致 Trial_v13.1 行為偏移）③ 🚨 Cody push to main 紀律缺口（Stage 66 BuildBroadScopeEnforceSection 步驟 3-4 措辭「commit message 必含...」自己預設 Cody commit + push 矛盾 CLAUDE_Cody.md:26 + v5 path 設計 — Cody 模型 prioritize user prompt > system prompt 跟著 commit）。**修根因動作**：commit `8b24192` revert `cd891fa`（Cody push main 變更整套 reverse）+ commit `0226c60` fix BuildBroadScopeEnforceSection 步驟 3-4 措辭 + 新增「v5 Petra 接管」⛔ 嚴禁 git commit/push 段（純 user prompt prepend 不污染 CLAUDE_Cody.md 跨專案守則）。**cost / blind spot**：真實 $3.18（餘額 30.49→27.31）vs SQL SUM $3.1869 = **blind spot 0% ✅**（Trial_v12 baseline 維持 / Cody $2.4324 / Vera $0.7492 / PM $0.0052）。**戰略結論**：連續 4 Trial 業務級成功重現（Trial_v10/v11/v12/v13.2 — infinite loop pattern 打破持續）+ v5.5 path 工程實證完整 + Stage 67 對 v5 既有 path 0 regression（Trial_v13.1 v5 既有 path 也跑通 = fallback 紀律有效）+ Stage 67 重構基底業務級成功（6/6 cover + 順修 bug）。**下一步**：Christ 2026-05-16 拍板「先記錄繼續實作 Stage 再一起試驗」— Trial_v14 跟下個 Stage 完成後一起跑（攤平 cost / 時間）/ 通過後切 `UseTalentSkillSeparation` default true = v5.5 Phase 1 完整收口拍板 + v5.5 正式上線 + 進 Phase 2 Step 3 DB 持久記憶 schema。**Step 0 升級候選 5 條**留 Stage 68 規劃前順手做（reload-cache scope 對應表 + step 3 reload 後驗證紀律 + Trial workspace branch cleanup 紀律 + PostgreSQL NULL unique + Gate1 Tier 架構級重構 baseline）。 |

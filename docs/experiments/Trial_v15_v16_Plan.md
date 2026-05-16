# Trial_v15+v16 試驗計劃書 — v5.5 Phase 2 Step 3+4 production 真實生效 + 跨 session memory inject 驗收

> 對應版本：**v3.60.0**（Stage 70 結案 + commit `c5309e7` Stage 70 follow-up timeout fix）
> 建立日期：2026-05-16
> 狀態：✅ 已結案 — 🟡 部分 PASS（Stage 69 跨 session memory inject 核心驗收達標 / 4 議題揭 → Stage 71 補強）
> 文件版本：v2.0

---

## 一、背景與定位

**Trial_v15+v16 合併跑 — Stage 69+70 工程實證後 production 真實生效驗收**：

- Stage 69 ✅ DB 持久記憶 schema（TaskMemory + TalentMemory + MemoryRepository + compact + 整合 v5.5 dispatch / feature flag `Workflow:UseV5Memory` default false）
- Stage 70 ✅ Petra hierarchical decomposition + dependency graph（SubtaskPlan + TopoSort + DispatchTalentsAsync 升級 / feature flag `Workflow:UseV5SubtaskPlanning` default false）
- **核心驗收**：第一次跑（空 memory baseline）+ 第二次跑（同 task 對照組）驗 talent_memories 跨 session inject 真實生效

---

## 二、試驗目的（3 條核心 + 1 條對照）

1. **Stage 70 hierarchical decomposition + dependency dispatch production 真實生效**：Petra 真實拆 N subtask + 依 dependency graph topological sort 派 multiple talent / chain memory pass-through 真實 fire
2. **Stage 69 兩層 memory schema production 真實生效**：TaskMemory（per-session 跨 talent 共享）+ TalentMemory（per-talent 跨 session 累積）兩層分層真實寫入 + inject
3. **Stage 69 跨 session inject 核心驗收**：第二次跑同 task — dispatch 1/N 注入 memory 時 `talentMemoryCount > 0`（上次 Trial 留的 talent_memories 真實 inject）
4. **連續 6 Trial 業務級成功對照組**：Trial_v10/v11/v12/v13.2/v14/v15.2 — infinite loop pattern 確認打破延續

---

## 三、任務需求

沿用 Trial_v6-v14 同 prompt（7+4 向對照精準度最高）— Dashboard 錯誤處理打磨 toast 通知。

---

## 四、結案紀錄

### 4.1 三 phase lifecycle 概覽

| Phase | sessionId | useV5SubtaskPlanning | 結果 |
|---|---|---|---|
| Trial_v15.1（08:06 UTC）| 8f843bb3 | true | ❌ DecideTalentsWithPlanAsync Polly TimeoutRejected 30s 全 abort — **揭 ServiceDefaults default timeout 議題** |
| **🔧 follow-up fix（commit `c5309e7`）** | — | — | ServiceDefaults global AddStandardResilienceHandler default 拉長 — AttemptTimeout 10s→60s / TotalRequestTimeout 30s→180s / SamplingDuration 120s。WebSearch 對齊 [MS doc](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.http.resilience.httpstandardresilienceoptions) default 值 + override API + LLM call typical timeout 範圍前置查清 — 對齊 workflow_aria.md 第三節 A 第 9 條紀律 |
| Trial_v15.2（08:28 UTC）| f66e8797 | true | ✅ 完整跑完 14m19s + PR #376 真開（6 檔 +32/-1）— 但揭 3 議題 |
| Trial_v16（12:32 UTC）| 50f601e7 | true | 🟡 完整跑完 16m11s 但 FinalizeGitAsync permission denied 沒開 PR — 核心驗收 talent_memories 跨 session inject 達標 |

### 4.2 Trial_v15.2 lifecycle 細節（Stage 70 production 真實生效）

| Step | 結果 |
|---|---|
| Petra v5.5 啟動 path | ✅ talentsCount=4 / useV5SubtaskPlanning=true / taskGroupId=null（對齊 Stage 69 v2.1 schema pivot） |
| DecideTalentsWithPlanAsync | ✅ subtasks=**5** dependencies=**4** picks=Cody(impl) → Cody(impl) → Cody(impl) → Vera(review) → Quinn(qa_testing) |
| dispatch 1/5 Cody | ✅ outputLen=1929 / 注入 memory `taskMemoryCount=0 talentMemoryCount=0`（baseline 空）|
| dispatch 2/5 Cody | ✅ outputLen=2569 / `taskMemoryCount=1 talentMemoryCount=1`（同 session 累積）/ dependsOn=[1] inputMsgs=3 |
| dispatch 3/5 Cody | ✅ outputLen=2455 / `taskMemoryCount=1 talentMemoryCount=1`（per-talent upsert 覆寫不累積）|
| dispatch 4/5 Vera | ✅ outputLen=801 / `taskMemoryCount=1 talentMemoryCount=0`（**跨 talent 共享 TaskMemory** / Vera 自己 talent 無 memory）|
| dispatch 5/5 Quinn | ⚠️ **outputLen=0** + Claude Code CLI exit 1 / 但 token_logs Quinn 真消耗 20K out — 議題 #2 |
| FinalizeGitAsync | ✅ Petra 開 PR **#376** 真實成功 / 6 檔 +32/-1 / 5 form 場景 cover（QuickCommandCard / AgentCreateDialog / RuleFormDialog / ProjectCreateDialog / AgentSettings / Program.cs 全域 Snackbar BottomRight 4s）|
| stale exec_confirm | ✅ 0 張（Stage 68 sub-item 2 修法持續生效）|

### 4.3 Trial_v16 lifecycle 細節（跨 session memory inject 核心驗收）

| Step | 結果 |
|---|---|
| Petra v5.5 啟動 path | ✅ talentsCount=4 / useV5SubtaskPlanning=true |
| DecideTalentsWithPlanAsync | ✅ subtasks=5 dependencies=4 picks 一致（vs Trial_v15.2 同 LLM 拆出相同 plan — 穩定）|
| **dispatch 1/5 Cody** | ✅ outputLen=2317 / 注入 memory `taskMemoryCount=0 talentMemoryCount=**1**` ⭐ **核心驗收達標** — Cody 上次 Trial_v15.2 留的 last-task-summary 真實 inject / inputMsgs=2 promptLen=1674（vs Trial_v15.2 第一次 1/5 inputMsgs=1 promptLen=1077）|
| dispatch 4/5 Vera | ✅ outputLen=1457 / `taskMemoryCount=1 talentMemoryCount=1` — 跨 talent 共享 + 跨 session 自己 talent 雙重 inject |
| dispatch 5/5 Quinn | ✅ **outputLen=803** — vs Trial_v15.2 outputLen=0 = **議題 #2 transient 結案**（Claude Code CLI 偶發 exit 1，非 systematic）/ `taskMemoryCount=2 talentMemoryCount=1`（議題 #3 持續：上次空 content talent_memory 仍 inject）|
| FinalizeGitAsync | ❌ **LibGit2Sharp permission denied 失敗** — `failed to rename lockfile to /tmp/aiteam-workspace/AiTeam/.git/objects/3e/...: Permission denied` — **議題 #4 揭** |
| PR 開啟 | ❌ 0 PR（業務 deliver 中斷在 FinalizeGitAsync）|
| session status | ✅ `done` 16m11s（FinalizeGitAsync 失敗不影響 session complete 設計）|

### 4.4 cost / blind spot

| | Trial_v15.1 | Trial_v15.2 | Trial_v16 | 合計 |
|---|---|---|---|---|
| 真實 cost | $0（timeout abort）| $2.5331 | $2.6632 | **$5.20** |
| Duration | 30s abort | 14m19s | 16m11s | — |
| Cody output tokens | — | 20,880（3 段）| 26,442（3 段）| — |
| Quinn output | — | 20,013（wasted）| 31,208（真做事）| — |
| **餘額追蹤** | $25.22 → $22.70 | | | $22.70 → ~$17.50 |

- **Blind spot ≈ 0%**（連續 5 Trial 0% blind spot 維持）

### 4.5 4 議題完整清單

| # | 議題 | 嚴重度 | Root cause |
|---|---|---|---|
| **1** | Petra 過度拆 code_implementation 為 3 段重複工作 | 🟡 | DecideTalentsWithPlanAsync prompt few-shot 沒給「拆=真不同 scope」清楚紀律 / LLM 對「打磨多 form」場景判斷成「分階段做」而非「線性整包」/ Cody token 累積 20-26K × 3 段重複 但 PR 規模 +32（vs Trial_v14 +232）|
| ~~**2**~~ | ~~Quinn outputLen=0~~ | ✅ 結案 transient | Claude Code CLI 偶發 exit 1（Trial_v16 同 Quinn outputLen=803 — 非 systematic）|
| **3** | Stage 69 memory 寫入 path 沒檢查 outputLen=0 跳過 | 🟡 | Quinn Trial_v15.2 空 content 寫進 task_memories + talent_memories / Trial_v16 inject 進 Quinn prompt 空 content memory block 污染 |
| **4** | **FinalizeGitAsync permission denied** | 🔴 業務 deliver 中斷 | LibGit2Sharp 對 root-owned .git/objects 操作失敗 / **Aria 自身操作疏漏 — docker exec root 動 workspace cleanup 違反 Stage 65 entrypoint chown + workspace permission init 紀律** |
| **5** | **Aria 自省點 #34** — workspace cleanup 紀律 | 🔴 自省 | Aria 全程自跑 Trial workspace cleanup 不能用 `docker exec sh -c "git ..."`（root） — 必走 `gosu appuser` 或讓 Bot Petra 自 cleanup（CloneOrPull 自動處理）/ 對齊 Stage 65 既有紀律延伸到 Aria 操作層 |

### 4.6 戰略結論

**🟡 Trial_v15+v16 部分 PASS — Stage 69+70 工程實證 production 真實生效 / 但揭 3 production-ready 補強議題（+ 1 Aria 自省）**：

#### ✅ 達標項

- **Stage 70 hierarchical decomposition + dependency dispatch production 真實生效**（subtasks=5 / dependencies=4 / picks 一致 / chain memory pass-through 真實 fire）
- **Stage 69 兩層 memory schema production 真實生效**（TaskMemory 跨 talent 共享 / TalentMemory per-talent / upsert 覆寫不累積對齊 compact 紀律）
- **Stage 69 跨 session inject 核心驗收達標 ⭐**（Trial_v16 dispatch 1/5 talentMemoryCount=1 / promptLen 多 ~600 char talent memory block）
- **Stage 70 timeout 修法 `c5309e7` 持續生效**（兩次跑 14m+16m 無 timeout）
- **連續 6 Trial 業務級成功延續**（Trial_v10/v11/v12/v13.2/v14/v15.2 — infinite loop pattern 確認打破第 6 次）

#### 🟡 未達標項

- 業務 deliver 中斷 1 次（Trial_v16 FinalizeGitAsync permission denied 沒開 PR）
- Petra 過度拆 subtask 導致 cost +21% vs Trial_v14 baseline（PR 規模反而較小 +32 vs +232）
- 空 content memory 寫入污染

#### 🎯 default flag 切換決策

**暫緩切兩個 default flag**（對齊紀律 SQL 已切回 false 守 fallback）：
- `UseV5Memory` default false（議題 #3 修後再切）
- `UseV5SubtaskPlanning` default false（議題 #1 修後再切）

理由：兩個 flag 都涉及 v5.5 path 行為層面（不 break path 但耗 cost / 污染 memory），先 Stage 71 補強 → Trial_v17 重驗 → 切 default true 才算 Phase 2 Step 3+4 正式完整收口。

---

## 五、後續行動

### 立即（aria-trial-summary 結案動作）

1. ✅ PR #376 close（對齊 Trial 既有紀律不污染 main）
2. ✅ Trial_v15_v16_Plan.md 建檔
3. ✅ flag 切回 default false（`UseV5Memory` + `UseV5SubtaskPlanning` — 對齊紀律 #10 + Stage 71 預期 production fallback 守）
4. Future_Feature_v5.5.md update — FF 二「v5 PoC 補強清單」加 4 議題進 Stage 71 候選

### Stage 71 候選 — v5.5 Phase 2 Step 3+4 production-ready 補強

**範圍**（4 議題 + 1 自省點對應紀律升級）：

1. **Petra prompt 升級紀律** — DecideTalentsWithPlanAsync few-shot 範例補強「拆 = 真不同 scope」清楚紀律 + 「打磨多 form」場景線性 vs 分階段判斷邊界
2. **Stage 69 memory 寫入 outputLen=0 guard** — `WriteMemoryAsync` path 加 `if (output.Length == 0) return;` 跳過空 content 寫入（對齊「不污染下次 prompt」精神）
3. **FinalizeGitAsync permission denied 修法** — LibGit2Sharp 操作前確保 workspace ownership = appuser（Bot CloneOrPull 路徑加 ownership 復原 step / 或 git operations 改透過 subprocess + gosu appuser）
4. **Aria 自省點 #34 立**（workflow_aria_session_lessons.md） — Trial workspace cleanup 紀律：不能用 `docker exec sh -c "git ..."` root 動 workspace，必走 `gosu appuser` 或讓 Bot Petra 自 cleanup（CloneOrPull 自動處理）

預估規模：M（4 議題 + 紀律升級 / 1 commit / Mock 場景補 + Trial_v17 驗）

### Trial_v17 候選

Stage 71 結案後 — 沿用同 prompt 驗 4 議題收口 + 切兩個 default flag = v5.5 Phase 2 Step 3+4 完整收口。

---

## 六、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v15+v16 在 main branch 跑（含 Stage 67-70 全 commits + `c5309e7` follow-up timeout fix）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v14 既有驗證
- 對齊 Trial_v2-v14 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-16 | 試驗結案紀錄（直接 v2.0 — 試驗前未先建 v1.0 plan / Aria 全程自跑 9-step 模板第 6 次實踐）。**🟡 部分 PASS — Stage 69+70 工程實證 production 真實生效 + Stage 69 跨 session memory inject 核心驗收達標 ⭐ / 揭 4 議題（含 1 Aria 自省點）→ Stage 71 補強**。**真實 lifecycle 三 phase**：① Trial_v15.1 30s timeout abort 揭 ServiceDefaults default timeout 議題 → follow-up commit `c5309e7` 修 ② Trial_v15.2 14m19s 完整跑完 PR #376 真開（6 檔 +32/-1 / 5 form cover）揭 3 議題 ③ Trial_v16 16m11s 完整跑完 / dispatch 1/5 talentMemoryCount=1 跨 session inject 達標 / FinalizeGitAsync permission denied 沒開 PR 揭議題 #4 + Aria 自省 #5。**達標**：Stage 70 hierarchical chain（subtasks=5 dependencies=4 / Cody×3 → Vera → Quinn）+ Stage 69 兩層 memory schema（TaskMemory 跨 talent 共享 / TalentMemory per-talent upsert）+ 跨 session inject ⭐ + Stage 70 timeout 修法持續生效 + 連續 6 Trial 業務級成功延續。**未達標**：業務 deliver 中斷 1 次（FinalizeGitAsync permission） + Petra 過度拆 cost +21% + 空 content memory 污染。**戰略結論**：暫緩切 default flag（已 SQL 切回 false 守 fallback）→ Stage 71 補強（4 議題 + 自省點 #34 立）→ Trial_v17 重驗 → 切 default true = v5.5 Phase 2 Step 3+4 正式完整收口。**真實 cost** $5.20 三 phase 累積（$0+$2.53+$2.66）/ 餘額 $25.22 → ~$17.50。 |

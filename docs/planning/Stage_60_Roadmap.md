# Stage 60 Roadmap — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess failure → fail-fast 統一（FF 五十五）

> 目標版本：**v3.49.0**（minor — v4 framework production-ready 補強第三波）
> 狀態：規劃中
> 範圍：FF 五十五（Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口）
> 規模：M-L

---

## 戰略脈絡

Trial_v7 結案揭露第 4 🔴 戰略級議題 — 直接推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設：

- **議題揭露**：Christ 點「需要修改」→ Bot 走 legacy `KickoffMeetingService.ModifyTaskPlanAsync` → Petra subprocess 失敗 → `MeetingCommons.RunAgentTurnAsync` 把失敗 swallow 成 placeholder string → caller silent skip 寫入 DB → TaskPlan 從 6,279 字 → 5 字（"（Petra 無回應）"）→ Stage 58 第 7 routing 沒 catch（因走 legacy path）
- **同類根因延伸**：`DesignMeetingService.ModifyDesignPlanAsync` 同 path 同設計缺口（FrameworkDesignRouter `:34` 註解明寫「議題 H1 — Stage 55+ 真切 framework HITL」延宕至今）

**對齊 Trial_v6 結案紀律**：「v4 framework 主路徑 9/9 達成」≠「v4 邊角 user actions 全遷移」+「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證（Trial_v7 揭 Mock 沒涵蓋的 modify path）。

---

## 子項清單

### 1. MeetingCommons silent failure → fail-fast 治本（核心子項）

`src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs` 內 `RunAgentTurnAsync(string agentDisplayName, string sessionId, ...)` method（line 34-74）三條 swallow 路徑修法：
- subprocess `!result.Success`（line 62-63）：目前只 LogWarning 不 throw → 改 throw 新 `MeetingSubprocessFailureException`
- 空 output 回傳 placeholder（line 65-67）：歸入同一 fail-fast 路徑
- catch 一般 Exception（line 69-73）：目前 swallow 成 placeholder → 改 re-throw（含 LlmApiFailureException 對齊 Stage 58 既有 marker pattern）

**影響面評估**（Forge spike 階段必跑）：grep 全 caller 確認所有 RunAgentTurnAsync 呼叫站（Kickoff Round 1-3 各 4 Agent + Petra / Design Round 各 Agent / Modify path 等）能容受 throw 而非 placeholder。**這是 regression 風險最大子項**。

### 2. KickoffMeetingService.ModifyTaskPlanAsync 遷移 framework Kickoff revise round（議題 C2 收口）

`src/AiTeam.Bot/Orchestration/Meeting/KickoffMeetingService.cs:212` `ModifyTaskPlanAsync` 改為走 framework path。對齊 Stage 50 framework Kickoff Group Chat orchestration + Stage 51 KickoffMidInterrupt HITL 試點 既有 pattern。

新 framework executor 接管 modify 場景：input = Christ 修改指引 + 現有 TaskPlan + 完整會議 context；output = TaskPlan v2 + 對應 BossInteraction `kickoff` 帶 TaskPlan 摘要進 ContextJson。

對齊 FrameworkKickoffRouter `:32` 註解既有 TODO 說明（「C2 拍板：Petra session_id = group.Id 仍可 resume」— 議題 C2 留的 framework 遷移 TODO 此 Stage 收口）。

### 3. DesignMeetingService.ModifyDesignPlanAsync 遷移 framework Design revise round（議題 H1 收口）

`src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs:359` `ModifyDesignPlanAsync` 改為走 framework path。對齊 Stage 52 framework Design Meeting Workflow（fan-out/fan-in + 條件式 Demi + needs_adjustment B2 子流程）既有 pattern。

對齊 FrameworkDesignRouter `:34` 註解既有 TODO 說明（「議題 H1，Stage 55+ 真切 framework HITL」— 此 Stage 收口）。

### 4. Stage 58 marker pattern 擴展接管 meeting subprocess failure

對齊 Stage 58 既有 `[API_FAILURE]` marker pattern（PipelineState `:281` HandleResponseAsync marker check + AgentQueueProcessor specific catch + 4 Stage Executor wire 第 7 routing），新增通用 `[SUBPROCESS_FAILURE]` marker：

- AgentQueueProcessor catch 新 `MeetingSubprocessFailureException` → build `[SUBPROCESS_FAILURE]` summary 前綴 result → call HandleAgentCompletedAsync 走正常 callback flow
- Pipeline Stage Executor `HandleResponseAsync` 內既有 `[API_FAILURE]` marker check 旁加 `[SUBPROCESS_FAILURE]` marker check → 統一接 `agent_api_failure_intervention` 第 7 routing（語意：兩類 failure 都是 Agent 不可恢復失敗，Christ 三選 continue/retry/abort 統一）

不新增 routing type — 重用 Stage 58 第 7 routing。

### 5. 盤點 + 處理剩餘 v4 邊角 user actions legacy paths（Forge spike 範圍）

Forge Plan Mode 階段 grep 找：
- 所有 ResponseAction 對應 handler 走 legacy path（不只 `kickoff_modify` / `design_modify`，還有 `kickoff_restart` / `kickoff_stop` / 其他 modify-style action）
- 對齊 InteractionProcessor `src/AiTeam.Bot/Orchestration/InteractionProcessor.cs:132` dispatch table 全盤點

預期 spike 揭露 2-4 個邊角 actions 待遷移。**範圍變更條件**：spike 揭露 > 4 個或設計衝突 → escalate Christ 拍板分 Stage 60A/60B（modify 兩 path 在 60A / 其他 actions 在 60B）。

### 6. Mock 場景補強

- `framework_modify_taskplan_happy`：Christ 點 modify → framework Kickoff revise round → Petra 跑 → TaskPlan v2 DB 寫入 ≥ 4000 字 + BossInteraction kickoff 帶 TaskPlan 摘要進 ContextJson
- `framework_modify_designplan_happy`：同上 Design path
- `meeting_subprocess_failure`：MeetingCommons subprocess 失敗 mock → fail-fast → AgentQueueProcessor build `[SUBPROCESS_FAILURE]` result → 第 7 routing fire BossInteraction `agent_api_failure_intervention` + context.agent="Petra" → MockMode auto-approve `api_failure_continue` → 流程推進
- `meeting_modify_during_subprocess_failure`：Christ 點 modify 同時 subprocess 失敗 → 走 routing 不 silent skip（驗證 fail-fast 對 modify path 同樣有效）

### 7. Version bump v3.49.0

`src/Directory.Build.props` `<Version>` 標籤 + Dashboard 頁腳自動讀。

---

## 設計決策

### 1. 路線 A 主路徑真正遷移 vs 路線 B 純 fail-fast 補強

| 路線 | 內容 | 評估 |
|---|---|---|
| **A 主路徑遷移**（推薦）| ModifyTaskPlan / ModifyDesignPlan 真切 framework path（子項 2+3） | 對齊 Stage 49-58 既有遷移紀律治本，徹底解決 v4 邊角 legacy 缺口 |
| B 純 fail-fast 補強 | 保留 legacy path 只補 MeetingCommons fail-fast（子項 1+4） | 最小修改但不解決根因，FF 五十五描述明寫「邊角 user actions legacy 遷移」必須做 |

→ **拍板路線 A** — Stage 60 範圍含 1+2+3+4+5+6+7 全子項。

### 2. Marker pattern 設計：擴用 [API_FAILURE] vs 新 [SUBPROCESS_FAILURE]

| 選項 | 評估 |
|---|---|
| 擴用 `[API_FAILURE]` | 簡單但語意混淆（不只 API 失敗，含 subprocess 異常） |
| 新增 `[SUBPROCESS_FAILURE]`（推薦）| 精準語意 + 對齊 Stage 58 marker pattern 累積；兩 marker 並存複雜度可控（第 7 routing wire 加一行 check） |

→ **拍板新 marker `[SUBPROCESS_FAILURE]`** — 語意清晰，未來其他 subprocess failure（不只 meeting）也可用同 marker 接 routing。

### 3. routing 設計：新增第 8 routing vs 重用第 7 routing

→ **拍板重用第 7 routing `agent_api_failure_intervention`** — 語意：兩類 failure 都是「Agent 不可恢復失敗」，Christ 三選 continue/retry/abort 統一。新增 routing 不 justify。

### 4. ModifyExecutor 設計層級：Workflow 主迴路 vs 獨立 mini Workflow

| 選項 | 評估 |
|---|---|
| 主迴路 Workflow 內加 ModifyExecutor | 對齊 Stage 51 KickoffMidInterrupt HITL 既有 pattern；framework state 統一 | 推薦 |
| 獨立 mini Workflow（modify 獨立啟動）| 邏輯獨立但 framework state 拆兩處 | 不推薦 |

→ **拍板主迴路 Workflow 內 ModifyExecutor**（Forge Plan Mode 細節設計）。

### 5. Forge spike 預期揭露議題

- 子項 1 MeetingCommons fail-fast 修法的 caller 影響面（Round 1-3 主流程 vs Modify path 是否能容受 throw）
- 子項 2+3 framework executor wire 點（對齊 Stage 50/52 既有 pattern 是否需擴）
- 子項 5 邊角 user actions 數量盤點（決定範圍變更需求）

---

## 驗收情境

### Mock 場景驗收（Forge 自驗）

#### 場景 A：framework_modify_taskplan_happy

**觸發**：`curl -X POST http://localhost:5052/internal/mock/scenario -H "X-Api-Key: $API_KEY" -d '{"scenario": "framework_modify_taskplan_happy"}'`（port 5052 + X-Api-Key 對齊 forge-self-verify skill）

**驗證**：
- Mock Kickoff Round 1-3 跑完 → BossInteraction `kickoff` fire → MockMode auto-approve `kickoff_modify` 帶修改指引
- Bot log 出現 `[Stage60] Framework Kickoff modify executor 啟動`（不是 legacy `KickoffMeetingService.ModifyTaskPlan`）
- DB query：`SELECT LENGTH("TaskPlan") FROM task_groups WHERE "Id" = '<group_id>'` ≥ 4000 字（不是 5 字 placeholder）
- DB query：`SELECT "ContextJson" FROM boss_interactions WHERE "InteractionType" = 'kickoff' AND "TaskGroupId" = '<group_id>' ORDER BY "CreatedAt" DESC LIMIT 1` 含 TaskPlan 摘要欄位

#### 場景 B：framework_modify_designplan_happy

同場景 A 結構，對應 Design Meeting + ModifyDesignPlan path。驗證 DesignPlan 欄位。

#### 場景 C：meeting_subprocess_failure

**觸發**：mock subprocess 失敗（MeetingCommons.RunAgentTurnAsync caller mock injection 模擬 subprocess 異常）

**驗證**：
- Bot log 出現 `[Stage60] MeetingCommons subprocess failure → throw MeetingSubprocessFailureException`（不是 silent skip placeholder）
- AgentQueueProcessor catch 新 exception → build `[SUBPROCESS_FAILURE]` result → log `[Stage60] AgentQueueProcessor catch MeetingSubprocessFailureException → build [SUBPROCESS_FAILURE] result`
- Pipeline Stage Executor marker check → fire BossInteraction `agent_api_failure_intervention` + context.agent="Petra"
- DB query：`SELECT "InteractionType", "Status" FROM boss_interactions WHERE "TaskGroupId" = '<group_id>' AND "InteractionType" = 'agent_api_failure_intervention'` 應有 1 row
- MockMode auto-approve `api_failure_continue` → 流程推進

#### 場景 D：meeting_modify_during_subprocess_failure

**觸發**：場景 A 流程中對 modify Petra subprocess 注入 mock 失敗

**驗證**：modify path 觸發 subprocess failure → 走第 7 routing 不 silent skip（不寫 placeholder TaskPlan）。對齊 Trial_v7 反例修根因。

### Christ 視覺驗收（必驗）

- Christ Discord 點 Kickoff modify embed → Discord 看到「修改進行中」狀態提示（framework 中途過程）+ 完成後 BossInteraction kickoff embed 顯示 TaskPlan v2 摘要（不是「無計劃書」）
- Christ Dashboard 點 Kickoff modify → 同上效果（雙通道一致）

### 0 regression 確認

- Trial_v6/v7 既有 Kickoff/Design Round 1-3 主流程行為不變（cost / 時長 / TaskPlan 字數對齊既有 baseline）
- 既有 33 framework_* Mock 場景仍綠（dotnet test + Mock 場景全跑）

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7 條紀律（不寫 code 範例 + 大檔 reference 標精準 line + 環境細節 reference 標 source of truth）
- **環境細節 source of truth**：Bot Internal API port `5052` 見 `docker-compose.prod.yml` / X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey` / Internal endpoint 真實 path 見 `src/AiTeam.Bot/Api/InternalController.cs`
- 對齊 Stage 50/51/52/58 framework Meeting + HITL + marker pattern 既有累積
- MeetingCommons.RunAgentTurnAsync 修法影響面廣（全 Meeting subprocess caller） — Forge spike 必跑 caller 影響面 grep + Mock regression 全跑
- 不新增 EF Migration（不動 schema — 對齊 Stage 58 marker pattern 不動 DB）
- 不引入新 routing type — 重用 Stage 58 第 7 `agent_api_failure_intervention`

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Stage 60 = FF 五十五（Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口）。範圍：① MeetingCommons.RunAgentTurnAsync silent failure → fail-fast 治本核心 ② KickoffMeetingService.ModifyTaskPlanAsync 遷 framework Kickoff revise round（議題 C2 收口）③ DesignMeetingService.ModifyDesignPlanAsync 遷 framework Design revise round（議題 H1 收口）④ Stage 58 marker pattern 擴展新 `[SUBPROCESS_FAILURE]` 接第 7 routing 統一 ⑤ Forge spike 盤點剩餘 v4 邊角 user actions legacy paths ⑥ Mock 場景補強 4 場景（含 modify happy 雙 path + subprocess failure + modify during subprocess failure）⑦ v3.49.0 bump。**設計決策 5 條**：路線 A 主路徑真正遷移 / 新 marker `[SUBPROCESS_FAILURE]` / 重用第 7 routing / ModifyExecutor 主迴路內 / Forge spike 預期揭露 caller 影響面。**規劃前期已 grep 驗證**：MeetingCommons line 34-74 silent failure 三條路徑（subprocess !result.Success / 空 output / catch Exception 全 swallow placeholder）+ ModifyTaskPlanAsync line 212-263 + ModifyDesignPlanAsync line 359 + FrameworkKickoffRouter :32 + FrameworkDesignRouter :34 既有 TODO 註解 + Stage 58 marker pattern PipelineState :281 + 4 Stage Executor HandleResponseAsync 對齊位置 + InteractionProcessor :132 dispatch table。**規模 M-L** / Opus 1M + medium-high / 預估 ~450-650K（對齊 Trial_v7 結案紀錄）。Mock 全綠 + Forge 自驗 4 場景 + Christ 視覺驗收 modify 雙通道一致。 |
| v1.1 | 2026-05-10 | **Forge 實作完成 + 4 Mock 場景全綠** — v3.49.0 deployed（commit 8d0637d / 32b2bc3 / 0962509）。**3 commit 一氣呵成**：① 8d0637d 主實作（925+/15- 變動 / 13 file modified / 2 new file）② 32b2bc3 MockMode auto-approve scenario-aware + ResponseContent 擴 RespondAsync（Forge 自驗第一輪揭露 modify path 沒被 auto-approve fire 補強）③ 0962509 framework_modify_designplan_happy 加 Petra escalate path（Forge 自驗第二輪揭露 design BossInteraction 沒開 — Pipeline ConsensusNoSplit path 不開 design 卡，需 escalate path 補強）。**Forge spike 揭露 3 議題 Aria 拍板全 Forge 自決**：① catch 點在 KickoffStageExecutor 而非 AgentQueueProcessor（Pipeline framework 實際 architecture）② `[SUBPROCESS_FAILURE]` 命名語意 wire catch 不做 marker check（Stage Executor response type 不同）③ 新加 2 Port + Petra-{Stage} 命名（per-stage Port pattern 對齊 Stage 58 紀律延續）。**4 Mock 場景全 PASS**：場景 A framework_modify_taskplan_happy → DB TaskPlan 10302 字（≥ 4000）✓ / 場景 B framework_modify_designplan_happy → DB DesignPlan 10302 字 ✓ / 場景 C meeting_subprocess_failure → throw → fire agent_api_failure_intervention agent="Petra-Kickoff" → MockMode auto-approve api_failure_continue → Pipeline 推進 ✓ / 場景 D meeting_modify_during_subprocess_failure → modify path 觸發 subprocess failure → 走第 7 routing 不 silent skip（Trial_v7 反例修根因驗證）✓。**dotnet build clean + dotnet test 131 tests pass**。**等 Christ 觸發 Aria gate2 + 視覺驗收**（Discord embed + Dashboard 操作中心 modify 雙通道一致）。 |

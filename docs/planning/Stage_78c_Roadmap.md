# Stage 78c — v4 Pipeline framework 整套砍除（接續 Stage 78b 後續 / v5.5 path single source of truth 完整收口）

> 對應系統版本：v3.70.0
> 規模：L+（一次過整套砍 / 路線 A 拍板）
> 狀態：✅ 已完成（2026-05-19）
> 文件版本：v2.0
> 實作 commit：[`e17e83b`](https://github.com/darkleong/AiTeam/commit/e17e83b)
> 性質：大規模 refactor / **0 Migration changes**（議題 9 揭真實 agent_queues 表不存在 / Plan v1 §G.4 Migration drop + R9 紀律廢除）/ 0 業務變化（v4 path 0 production fire 連續 17 次 Trial 累積）
> Model + Effort 建議：**Opus 1M + Extra high**（對齊 Stage 78a 488K / Stage 78b 394K baseline + 大規模架構級重構新區間 ×1.57-4.93 7 資料點累積）
> cost 預估：$5-8 per cycle

---

## 戰略脈絡

Stage 78a 砍 v4 path dead code（22+4=26 檔 / net -3957 行）+ Stage 78b 砍 v4 path dead caller（8 檔 / net -787 行）後，**v4 Pipeline framework 整套（~25-30 class）仍 active code**：

- v5.5 path 真實 0 依賴 v4 Pipeline（PetraOrchestratorService grep verify 0 method call to TaskGroupService / AgentQueueService — 只 2 comment reference）
- v4 Pipeline 真實 0 production fire（Trial_v6-v22 連續 17 次 v5.5 path 業務級成功 + Stage 78b 砍 v4 entry 後 0 caller）
- v4 Pipeline framework code 仍在 = 純 effective dead code

**Stage 78c 範圍**：v4 Pipeline framework 整套砍 — ~25-30 class + Migration drop agent_queues 表 + 連動 ButtonCallbackRouter v5.5 routing 評估（Stage 78b 留的）+ 其他 SlashCommand + WebhookController PR handler 評估 + IAgentExecutor interface + AgentExecutionResult / AgentResultType / AgentDescriptor 整套砍（Stage 78b W3 fallback 預備條件達成）。

**Christ 2026-05-19 拍板路線 A 一次過整套砍**（vs 拆 Stage 78c-1/78c-2/78c-3 三輪）：
- 接受 Migration drop table 不可逆風險（agent_queues 表 0 active row / 0 production 影響）
- 接受規模 L+ 真實落點預估 ~500-700K Opus 1M 50-70% safety
- 對齊 Stage 78a 488K + Stage 78b 394K baseline 已實證跑得起來

### Phase 4 路徑（Christ 2026-05-19 拍板）

```
Stage 78a ✅ → 78b ✅ → 78c（本 Stage / v4 Pipeline 整套砍 / L+）
            → 79（v5.5 image flow 補完 / M）
            → 80（A HITL plan confirmation 閘門 / M）
            → 81（B 動態 re-planning / L）
            → WebUI Stage（K WebUI Talent CRUD + Effort 擴展 + E Token monitoring 視覺化）
            → v5.5 完整收口
```

### 範圍邊界

**全砍**（v4 Pipeline framework 整套 + 配套）：v4 Pipeline 核心 / v4 Meeting / Appeal / HITL / Queue / Group / IAgentExecutor 整套 / 連動 routing / Migration drop agent_queues 表 / v4 entity（TaskGroup / TaskItem 等）

**保留**（v5.5 path active）：
- PetraInboxProcessor + PetraDispatchWorker + PetraOrchestratorService（v5.5 dispatch 整套）
- CeoAgentService.ProcessWithClaudeCodeAsync（v5.5 flag forward only）
- OpsAgentService + HealthCheckJob Quartz job（Stage 78b 留 / production active 監控）
- ButtonCallbackRouter v5.5 active routing（confirm_yes Stage 68 短路 / + 其他 v5.5 routing 精準分辨）
- DI / Database core / Logging / Configuration 等 framework infra

---

## 子項清單

### 鏈 A — v4 Pipeline 核心砍

**1. WorkflowEngine 砍** — v4 workflow engine 主入口
**2. PipelineState + PipelineHitlHelper 砍** — v4 pipeline state 管理
**3. 7 個 Stage Executors 砍** — DevStageExecutor / DevPlanStageExecutor / DevFixStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor / NotifyMergeStageExecutor（整 folder `src/AiTeam.Bot/Workflows/Pipeline/Executors/`）
**4. PipelineRoutingService 砍** — v4 pipeline routing dispatch

### 鏈 B — v4 Meeting / Appeal / HITL 砍

**5. MeetingOrchestrationService + FrameworkPipelineRouter + FrameworkDesignRouter 砍** — v4 Kickoff / Design meeting flow（v5.5 PetraOrchestratorService 取代）
**6. AppealOrchestrationService + FrameworkAppealRouter 砍** — v4 Vera fix loop / Dev_plan Appeal flow
**7. FrameworkHitlBridge 砍** — v4 HITL bridge（v5.5 HITL Stage 80 重新設計）
**8. EpicChainService 砍** — v4 epic chain
**9. QaCoordinationService 砍** — v4 QA coordination
**10. BossResponseHandlerService + BossNotificationService 砍**（grep verify v5.5 path 0 依賴 — PetraInboxProcessor 只 comment reference 不 method call）
**11. InteractionProcessor v4 對應段砍**（grep verify 哪些 segment v5.5 用 / 哪些 v4 only — InteractionProcessor 整 class 可能 mixed-use）

### 鏈 C — v4 Queue / Group 砍 + Migration drop

**12. TaskGroupService 砍**
**13. ProposalConfirmationService 砍**（v4 提案確認 + propose_yes routing handler）
**14. AgentQueueService + AgentQueueProcessor 砍**
**15. agent_queues 表 Migration drop**（不可逆 / 對齊 ef-core.md 紀律）
**16. v4 entity 砍評估** — `TaskGroup` + `TaskItem` + 對應 entity（grep verify 是否其他表有 FK / Dashboard UI 是否 reference / 砍前先 grep）
**17. IAgentExecutor interface + AgentExecutionResult / AgentResultType / AgentDescriptor 整套砍**（Stage 78b W3 fallback 預備條件達成 / 鏈 A+B+C 砍後 0 caller）

### 鏈 D — 連動 routing + propagation

**18. ButtonCallbackRouter v4 Pipeline 對應 routing 整套砍** — propose_yes / propose_no / propose_adjust / cancel_yes / cancel_no / kickoff_* / design_* / framework_* / ShowDirectAgentConfirmAsync + HandleDirectAgentChannelMessageAsync（Stage 78b 留的整套 / grep verify 哪些 v5.5 active 哪些 v4 only）
**19. CommandHandler v4 Pipeline call 砍** — agent 頻道直接訊息 path + SkipCeoConfirm path 整套（line 174-212 + 459-498 + 505-560 等 / grep verify v5.5 path 0 走這段）
**20. WebhookController PR 事件 handler 評估** — HandlePrOpenedAsync / HandlePrSynchronizedAsync / HandlePushAsync — grep verify v5.5 path 真實依賴後決定砍 vs 留
**21. 其他 SlashCommand 評估** — reload-rules / status / pause / resume / stop-all / resume-all / queue / new-session / mock — `/mock` Forge 自驗可能用 grep verify / 其他 0 使用直接砍

### 鏈 E — DB schema + Migration

**22. Migration**：
- `Drop table agent_queues`（agent_queues 表整套砍 / 0 FK / drop 0 影響其他 schema integrity）
- v4 entity 對應表 drop 評估（TaskGroup / TaskItem 等）— grep verify 後決定
- **Migration 前置紀律**：`docker exec aiteam-aiteam-postgres-1 pg_dump -U aiteam -d aiteam -t agent_queues > backup_agent_queues_pre_stage78c.sql` 備份留檔（Christ 拍板路線 A 接受 drop 不可逆 / pg_dump 10 秒成本保險）

**23. Entities.cs + AppDbContext DbSet 砍**（對應 v4 entity）

### 鏈 F — 連動配套

**24. WorkflowSettings 對應 v4 段砍評估**
**25. CeoResponseActions 對應 v4 action 字串砍評估**（propose / delegate / cancel 等）
**26. RoutingTypes 對應 v4 type 砍評估**
**27. archive prompt 評估**（v4 Pipeline 對應 docs）
**28. xUnit test 對應 v4 Pipeline 整套砍**（grep verify 哪些 test cover v4 only / 砍乾淨）
**29. Directory.Build.props v3.69.0 → v3.70.0**

---

## 設計決策

### 1. 路線 A 一次過整套砍（Christ 2026-05-19 拍板）

**vs 拒絕路線 B（拆三輪 78c-1/78c-2/78c-3）**：
- 規模 L+ 但 Stage 78a/78b baseline 已實證 Opus 1M safety 內跑得起來
- 範圍清楚（v4 Pipeline 整套 / Stage 78a/78b 多輪累積 narrow down 完）
- spike 多輪可能性低（vs Stage 78a v1.0→v1.2 升級）
- 戰略推 Stage 79+ 業務功能快（vs 3 輪 Stage 拉長 Phase 4）

**vs 拒絕路線 C（拆兩輪）**：
- 跟路線 B 同方向但中間態 / 沒明顯優勢
- 反而每 Stage 規模仍偏大（L+ 級）

### 2. Migration drop table 不可逆 — Christ 接受（v4 path 0 production 影響）

- agent_queues 真實 0 active row（Trial_v6-v22 連續 17 次 v5.5 path 業務級成功 + Stage 78b 砍 v4 entry 後 0 caller）
- 即使 drop 丟資料 → 真實業務影響 0
- pg_dump 備份留檔候選紀律（鏈 E §22 / 不强制 / 萬一未來 audit 用）

### 3. backwards-compatible 守護紀律 — v5.5 path 0 行為改變

對齊 Stage 78a/78b 連續第五次 clean delivery baseline：
- Dashboard → API `/internal/ceo/command` → CeoAgentService.ProcessWithClaudeCodeAsync → PetraInbox forward → PetraDispatchWorker → Petra → Cody chain 0 行為改變
- Discord @Christ mention → CommandHandler v5.5 path → 同 chain 0 行為改變
- ButtonCallbackRouter v5.5 routing（confirm_yes Stage 68 短路）0 行為改變
- HealthCheckJob Quartz scheduled job 0 行為改變

### 4. ctor unused dep cleanup 紀律延續（對齊 Stage 78b）

砍 v4 class 後對應 Program.cs DI 註冊砍 / ctor 對應 dep 砍 / unused using 砍 — 對齊 CS9113 warning cleanup。

### 5. Forge spike 揭真實 propagation 紀律延續（對齊 Stage 78a 教訓）

Forge plan 階段必 grep verify W1-W9 真實狀態（見下方 Aria 預警）— 範圍變動必 escalate Christ 拍板。

---

## 驗收情境

### 場景 A：xUnit baseline 0 regression

**觸發**：`dotnet test AiTeam.slnx`
**驗證**：Bot.Tests + Generated 全綠（砍 v4 Pipeline 對應 test 後新 baseline / 對齊 Stage 78b baseline 104+127 進一步下降）。

### 場景 B：Migration drop agent_queues 表真實 executed

**觸發**：CI/CD 自動 `dotnet ef database update` apply Migration
**驗證**：
- Migration `Drop table agent_queues` 真實 SQL fire
- `docker exec aiteam-aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d agent_queues"` → 期望 `Did not find any relation named "agent_queues"`
- Bot startup 0 exception（DI / EF Core 不再 reference agent_queues）

### 場景 C：v4 Pipeline framework 整套 0 caller（grep verify）

**觸發**：grep verify 9 條件
**驗證**：
- `grep -rnE "TaskGroupService|AgentQueueService|ProposalConfirmationService|WorkflowEngine|PipelineState\b|FrameworkPipelineRouter|FrameworkDesignRouter|FrameworkAppealRouter|FrameworkHitlBridge|MeetingOrchestrationService|AppealOrchestrationService|EpicChainService|QaCoordinationService" src/` → 期望 0 hit（除註解）
- `grep -rnE "IAgentExecutor|AgentExecutionResult|AgentResultType|AgentDescriptor" src/` → 期望 0 hit（除註解 / IAgentExecutor.cs 整檔砍）
- `grep -rnE "agent_queues|class TaskGroup\b|class TaskItem\b|class AgentQueue\b" src/AiTeam.Data/` → 期望 0 hit（v4 entity 砍）

### 場景 D：v5.5 path production 0 regression（Aria gate2 真實業務驗）

**觸發**：Christ 開 Dashboard 派一個簡單 task（對齊 Trial_v22 baseline prompt）
**驗證**：
- API `/internal/ceo/command` → CeoAgentService.ProcessWithClaudeCodeAsync → PetraInbox row 寫入 → return v5.5 ack
- PetraInboxProcessor pickup row → PetraDispatchWorker dispatch → PetraOrchestratorService → Petra orchestrator → Cody/Vera/etc chain
- PR 真開 + Cody success
- token_logs 真實寫入（PM=gemini / Cody=claude-code）
- Dashboard 操作中心顯示 task 進度
- 對齊連續 13 Trial 業務級成功 baseline

### 場景 E：Bot startup 0 exception（DI registration 完整性）

**觸發**：Bot 啟動 + Application.RunAsync 完整跑通
**驗證**：
- Bot startup log 0 exception（特別 DI ServiceProvider validation）
- 砍 v4 class DI 註冊後 0 missing service
- Quartz Scheduler started + HealthCheckJob schedule
- `No migrations were applied. The database is already up to date.`（Migration apply 完成後）

### 場景 F：HealthCheckJob production active 不受影響（Stage 78b 留 / Stage 78c 不擾）

**觸發**：Bot 啟動後 60 sec 內 Quartz HealthCheckJob fire
**驗證**：
- Bot log 出現 `HealthCheckJob.Execute` + `RunHealthCheckAsync` + `MonitorCiCdAsync` 真實 fire
- OpsAgentService 仍 Singleton register + production active 4 method 仍可呼叫

### 場景 G：ButtonCallbackRouter v4 Pipeline routing 砍 + v5.5 routing 0 行為改變

**觸發**：
- xUnit test：對 v5.5 routing case（confirm_yes Stage 68 短路）的單元測試（若有）
- production 自驗：Dashboard 派 task → v5.5 path 完整 chain 跑通

**驗證**：
- ButtonCallbackRouter 行數大幅下降（Stage 78b 後 713 行 → 預期 Stage 78c 後 ~200-300 行 / 對齊 refactor-sop.md 紀律 ≤500 行）
- `grep -nE '"propose_yes|"propose_no|"propose_adjust|"cancel_yes|"cancel_no|"kickoff_|"design_|"framework_' ButtonCallbackRouter.cs` → 期望 0 hit（v4 Pipeline routing 整套砍乾淨）
- v5.5 routing case 邏輯不變（confirm_yes Stage 68 短路 + defensive log+ack fallback）

### 場景 H：CeoAgentService.ProcessWithClaudeCodeAsync v5.5 path 0 行為改變

**觸發**：API `/internal/ceo/command` POST request
**驗證**：
- CeoAgentService.ProcessWithClaudeCodeAsync → petraInboxRepository.Enqueue → return CeoResponse with Action=PetraV5Dispatched
- 0 行為改變 vs Stage 78b baseline

### 場景 I：其他 SlashCommand 砍評估

**觸發**：Bot 啟動 + Discord slash command 註冊 sync
**驗證**：
- grep verify `/mock` 真實使用度（Forge 自驗 / Trial 自驗 / Christ 偶爾測試）— 若 active 則留 / 若 0 使用直接砍
- 其他 SlashCommand（reload-rules / status / pause / resume / queue / new-session）grep verify 真實使用度 — Christ 拍板「Discord slash command 完全沒在使用」→ 全砍
- Discord 端 slash command list 對應更新

### 場景 J：WebhookController PR 事件 handler 評估

**觸發**：grep verify v5.5 path 真實依賴
**驗證**：
- HandlePrOpenedAsync / HandlePrSynchronizedAsync / HandlePushAsync 真實 caller 是 v4 path or v5.5 path？
- v5.5 path 真實依賴 → 留
- v5.5 path 0 依賴 + Christ 拍板 GitHub PR/push webhook 0 使用 → 整套砍 + WebhookController 整檔砍 / `/webhook/github` route 廢除

---

## 技術約束

- **Migration drop table 不可逆**（agent_queues 表 / 對齊 ef-core.md 紀律 / Christ 拍板接受）
- **Migration 前置 pg_dump 備份留檔**（候選紀律 / 不强制 / 萬一未來 audit 用）
- backwards-compatible 守護延續（v5.5 path Success=true 0 行為改變）
- v5.5 path 涵蓋全 entry：Dashboard API + Discord @mention + GitHub PR webhook（若留）
- Forge plan 階段必走 spike grep verify 紀律（W1-W9 真實狀態 verify）

---

## ⚠️ Aria 預警（對齊 Stage 78a/78b 教訓 + 連續 7 Stage 自省點 #37 紀律延伸）

### W1：v4 Pipeline 整套砍前必先 grep verify 各 class 真實 v5.5 path 依賴度

PetraOrchestratorService 0 method call to TaskGroupService / AgentQueueService 已 grep verify ✓。但其他 mixed-use 風險：
- **InteractionProcessor** 整 class 可能 v4 + v5.5 mixed-use（Petra 系列 reference comment / 0 method call grep verify pass / 但 class 整套砍 vs 砍 v4 segment 留 class — Forge plan 階段揭真實後拍板）
- **BossResponseHandlerService + BossNotificationService** v5.5 path 0 依賴已 grep verify ✓ / 整套砍 OK
- 其他 v4 Pipeline class（WorkflowEngine / FrameworkPipelineRouter / 等）— Forge plan 階段 grep verify

### W2：Migration drop table 不可逆 — pg_dump 備份留檔候選

**接受不可逆**（Christ 拍板 / agent_queues 0 active row / 0 production 影響）。Migration 前置紀律候選：
```bash
docker exec aiteam-aiteam-postgres-1 pg_dump -U aiteam -d aiteam -t agent_queues > backup_agent_queues_pre_stage78c.sql
```
10 秒成本 / 萬一未來 audit 用。**Forge 拍板要不要做 / Aria 不强制**。

### W3：v4 entity（TaskGroup / TaskItem 等）砍評估 — Dashboard UI + 其他表 FK 風險

- agent_queues 表 0 FK 已 grep verify ✓
- TaskGroup / TaskItem 真實狀態：是否 Dashboard UI 仍 reference？（task center / pipeline view 等）
- 是否其他 entity 有 FK 到 TaskGroup / TaskItem？（Migration drop 前必 grep verify schema dependency）
- Forge plan 階段揭真實後決定砍 vs 留

### W4：其他 SlashCommand 砍評估 — `/mock` 風險

- `/mock` 是 Forge 自驗 / Trial 自驗常用 / Mock Mode 走這條
- Forge plan 階段必 grep verify `/mock` 真實使用度（forge-self-verify skill / Mock scenario test 等）— 若 active → 留 / 若 0 使用 → 砍
- 其他 SlashCommand（reload-rules / status / pause / resume / queue / new-session）— Christ 拍板 0 使用 → 全砍

### W5：WebhookController PR 事件 handler 評估 — v5.5 path 真實依賴

- HandlePrOpenedAsync / HandlePrSynchronizedAsync / HandlePushAsync 真實 v5.5 path 依賴待 grep verify
- v5.5 path 0 依賴 + Christ 拍板 GitHub webhook 0 使用 → 整套砍 + WebhookController 整檔砍
- 若 v5.5 path 有依賴 → 留對應 handler 砍 v4 only segment

### W6：ButtonCallbackRouter v4 Pipeline routing 完整砍乾淨

- v4 Pipeline routing：propose_yes / propose_no / propose_adjust / cancel_yes / cancel_no / kickoff_* / design_* / framework_* / ShowDirectAgentConfirmAsync + HandleDirectAgentChannelMessageAsync 整套
- Stage 78b 留的整套砍乾淨 / v5.5 active routing 0 行為改變
- ButtonCallbackRouter 行數預期下降 713 → ~200-300（對齊 refactor-sop.md ≤500 行紀律）

### W7：IAgentExecutor + AgentExecutionResult / AgentResultType / AgentDescriptor 整套砍前必 grep verify 全 0 caller

Stage 78b 留的 W3 fallback 預備條件 — Stage 78c 鏈 A+B+C 砍後預期 0 caller：
- AgentQueueProcessor.cs:190 `GetKeyedService<IAgentExecutor>(executorKey)` 隨鏈 C §14 砍
- AgentExecutionResult / AgentResultType / AgentDescriptor 12+ file 廣用（PipelineState / Executors / FrameworkAppealRouter / AppealOrchestrationService / QaCoordinationService / MeetingOrchestrationService / FrameworkPipelineRouter / TaskGroupService）— 鏈 A+B 砍後 0 caller
- Forge plan 階段最後 grep verify 全 0 caller → 整套砍 IAgentExecutor.cs 檔案

### W8：CommandHandler agent 頻道直接訊息 path + SkipCeoConfirm path 砍

- CommandHandler.cs:505-560 HandleDirectAgentChannelMessageAsync 整段砍（Stage 78b 留的 Stage 78c 範圍）
- CommandHandler.cs:174-212 + 459-498 SkipCeoConfirm path 整段砍（v4 path / v5.5 path 0 走這段）
- v5.5 path @Christ mention 主路徑 0 影響 / 對齊 backwards-compatible 守護

### W9：Forge spike 揭真實後 escalate Christ 拍板（範圍變動必 escalate）

對齊 Stage 78a 連續 3 輪 spike 教訓 — Stage 78c 範圍 narrow 但仍可能 spike 揭真實：
- InteractionProcessor mixed-use 處理（整套砍 vs 砍 v4 segment 留 class）
- v4 entity 砍評估（TaskGroup / TaskItem schema dependency）
- WebhookController PR handler 砍 vs 留
- `/mock` 砍 vs 留
- 其他 healthy 偏離 plan 候選

### 校準錨預估（自省點 #37 第 8 次累積實證候選）

- raw 預估：250-400K（大規模 ~25-30 class refactor + Migration drop table + 連動 routing）
- ratio 預估：×1.5-2.5（範圍清楚 / 預期 1 輪 spike / Stage 78a/78b 累積經驗成熟）
- 真實 context 預估：~500-700K（取中位）
- safety：Opus 1M = 50-70% 利用率（仍 < 75% 警戒）
- cost 預估：$5-8

### Forge Plan Mode 起手紀律

1. 對齊 workflow_forge.md + forge-self-verify skill 紀律
2. 先 grep verify W1-W9 真實狀態 → 拍板子項依賴鏈 + 範圍精準化
3. spike 揭真實後 escalate Christ 拍板（對齊 Stage 78a 教訓 / 範圍變動必 escalate）
4. v5.5 path 0 行為改變紀律守
5. Migration drop table 前 pg_dump 備份候選紀律拍板
6. 配套 propagation 範圍精準（不擴 Stage 79 image flow 範圍）

---

## 實作紀錄（v2.0 / 2026-05-19）

> Forge 結案第一段。對應 commit [`e17e83b`](https://github.com/darkleong/AiTeam/commit/e17e83b) — `feat(stage78c): v4 Pipeline framework 整套砍除 v3.70.0 — 路線 A 一次過 + 6 議題拍板採納`。

### 統計

- **108 files changed / 220 insertions / 22910 deletions = net -22690 行**
- ~60+ files 整檔砍 + ~10 files 內部段砍 + 1 file 重命名（IAgentExecutor.cs → AgentDescriptor.cs）
- 0 Migration（議題 9 揭真實 / 0 不可逆風險 / git revert 完全可還原）
- 本機驗證：**0 error / 0 C# compile warning**（Stage 78b 59 → 78c **0** 全乾淨突破）/ Bot.Tests 100 + Generated 127 全綠
- Production deploy verify：Bot startup 0 exception / Quartz Scheduler initialized / PetraInboxProcessor polling 正常 / `/webhook/github` HTTP 404 / `/internal/mock/scenario` HTTP 404

### 實作完成項目（6 鏈 + 議題 7+8+9 incorporated）

**鏈 A — v4 Pipeline 核心砍（17 files 整檔）**
- `Orchestration/WorkflowEngine.cs`
- `Workflows/Pipeline/` 整 folder（5 base + 11 executors = 16 files）
- `Orchestration/Routing/PipelineRoutingService.cs`

**鏈 B — v4 Meeting+Appeal+HITL+Boss+InteractionProcessor+Proposal+Pm+MockScenarioService 砍**
- `Workflows/Kickoff/` 整 folder（11 files）
- `Workflows/Design/` 整 folder（15 files）
- `Workflows/Appeal/` 整 folder（10 files）
- `Workflows/Common/FrameworkCheckpointStoreBase.cs`
- `Orchestration/Meeting/` 整 folder（9 files）
- `Orchestration/Appeal/` 整 folder（2 files）
- `Orchestration/Hitl/` 整 folder（2 files）
- `Orchestration/Epic/EpicChainService.cs`
- `Orchestration/Qa/QaCoordinationService.cs`
- `Orchestration/Boss/` 整 folder（2 files）
- `Orchestration/InteractionProcessor.cs`（議題 5）
- `Orchestration/Proposal/ProposalConfirmationService.cs`
- `Agents/Pm/` 整 folder 5 files（議題 8 / Forge 自決）
- `Services/MockScenarioService.cs`（議題 7 / Forge spike R8 揭真實）

**鏈 C — v4 Queue+Group+Migration drop（議題 9 揭真實）**
- `Orchestration/TaskGroupService.cs`（813 行）
- `Orchestration/AgentQueueService.cs`（205 行）
- `Orchestration/AgentQueueProcessor.cs`（510 行）
- `Services/AgentQueueControlService.cs`（議題 7 配套）
- **0 Migration**（議題 9：agent_queues 表真實不存在 / Stage 27a 加的是 TaskItem.QueueStatus + QueuedAt nullable fields 對齊議題 1 路線 2 留 entity 邊界）

**鏈 D — IAgentExecutor interface + type 系統部分砍**
- `Agents/IAgentExecutor.cs` → `Agents/AgentDescriptor.cs` 重命名 + 縮為 ~10 行
- 砍 `IAgentExecutor` interface + `AgentExecutionResult` record + `AgentResultType` enum
- **留** `AgentDescriptor` record（v5.5 active / R1 校正 / CeoAgentService + CommandHandler + CeoCommandController 用）

**鏈 E — Discord routing 重整**
- `Discord/Routing/ButtonCallbackRouter.cs` 713 → **120 行**（-83%）/ 留 RouteAsync 簡化 + HandleConfirmYesAsync Stage 68 短路 + BuildCeoDecisionEmbed + BuildConfirmButtons + Truncate
- `Discord/CommandHandler.cs` 614 → **315 行**（-49%）/ 留 v5.5 main flow + DetectImageMediaType 從 SlashCommandRouter 移入 + slashRouter/appSettings ctor dep 砍
- `Discord/Routing/SlashCommandRouter.cs` 整檔砍（議題 4）
- `Discord/Routing/PendingConfirmationStore.cs` v4 confirmation 整套砍（縮為純 v5.5 generic confirmation）
- `Discord/Routing/RoutingTypes.cs` `PendingConfirmation` record 縮為 3 base field

**鏈 F — WebhookController + Program.cs + Configuration + Constants 配套砍**
- `GitHub/WebhookController.cs` 整檔砍（議題 2 / `/webhook/github` route 廢除）
- `Program.cs` v4 DI 註冊段砍（~30+ 行）+ 註解 update Stage 78a/78b/78c 進度
- InternalController 砍 7 v4 endpoints（mock/scenario / queue/{agent}/pause+resume / queue/stop-all+resume-all / tasks/{taskId}/requeue / admin/replay-completion / taskgroup pause+resume+pause-epic+resume-epic / kickoff/trigger-mid-interrupt）/ 留 reload-cache + restart + deployment + tokens
- DashboardBotService 砍 9 v4 methods（議題 7+5+配套）/ 留 ReloadCacheAsync + RestartBotAsync
- DashboardCeoCommandService 砍 TriggerKickoffMidInterruptAsync（議題 5 配套）
- Dashboard 4 pages stub v4 action methods（AgentStatusCard pause/resume / TaskCenter requeue / PipelineView pause/resume/epic/mid-interrupt）→ snackbar 「v4 已砍 / WebUI Stage 重設計」（議題 1 路線 2 邊界守）
- Home.razor 砍 `<MockScenarioCard />` + `<GlobalQueueControlCard />` reference
- Dashboard `MockScenarioCard.razor` + `.razor.cs` 整檔砍（議題 7 配套）
- Dashboard `GlobalQueueControlCard.razor` + `.razor.cs` 整檔砍（議題 7 配套）
- `Bot.Tests/DesignPromptsTests.cs` 整檔砍（Workflows/Design folder 砍配套）

**鏈 G — Version bump**
- `src/Directory.Build.props` v3.69.0 → **v3.70.0**

### 關鍵設計決策

**1. 路線 A 一次過拍板 + 6 議題拍板採納（Christ 2026-05-19）**
4 議題（v4 entity Dashboard 連動範圍 / WebhookController / `/mock` SlashCommand / 其他 SlashCommand）全採納 Forge 推薦 + Aria 5 條補強紀律 incorporated。Forge 實作期再 spike 揭 3 新議題（議題 7 MockScenarioService / 議題 8 Pm/ folder / 議題 9 agent_queues 表真實不存在）全 Christ 拍板採納。

**2. 議題 9：agent_queues 表真實不存在（Forge spike 揭真實）**
Stage 27a Migration `Stage27aQueueFields.cs` 真實加的是 `TaskItem.QueueStatus` + `QueuedAt` nullable fields，**不是** agent_queues 獨立表。Plan v1 §G.4 Migration drop agent_queues + R9 Down() 對齊 Stage 27a baseline 紀律 + pg_dump 備份候選紀律全廢除。**0 Migration changes 本 Stage / 0 不可逆風險 / git revert 完全可還原 / scope 從 L+ 含 Migration → 純 code refactor L+**。對齊議題 1 路線 2「留 TaskItem entity + DB tables」邊界精神。

**3. R1：Roadmap §C 場景 C AgentDescriptor 校正（Forge spike 揭真實）**
Roadmap 寫 `AgentDescriptor 期望 0 hit` 實質 v5.5 active type（CommandHandler:420 + CeoCommandController:120 + CeoAgentService:31 用）。Plan v1 校正：砍 `IAgentExecutor` interface + `AgentExecutionResult` record + `AgentResultType` enum / **留** `AgentDescriptor` record / 檔案重命名 `IAgentExecutor.cs` → `AgentDescriptor.cs`。Verification 場景 C 對應調整：grep `AgentDescriptor` 期望 4 hit（定義 + 3 caller）。

**4. 議題 1 路線 2 邊界守：留 v4 entity + DB tables + Dashboard UI**
砍 v4 Pipeline **service** 整套（~25-30 class）/ **留** `TaskGroup` + `TaskItem` + `TaskLog` + `BossInteraction` entity + DB tables + 4 Dashboard Pages（PipelineView / PipelineList / TaskCenter / AgentStatusCard）+ DashboardTaskService + BossInteraction Repository + Stage 57 partial unique index / Dashboard pages stub v4 action methods 顯示 snackbar「Stage 78c 已砍 / 留待 WebUI Stage 重設計」/ **WebUI Stage 預備統一重設計範圍**（對齊 Phase 4 路徑 Stage 79 image flow → 80 HITL → 81 replan → WebUI Stage 統一處理）。

**5. 議題 3+7 配套：Mock 框架分層處理**
Aria 議題 3 wording 漏分層「留 Mock 框架其餘整套」實質正確路徑是「LLM 層 Mock 留 / Orchestration 層 Mock 砍」。Forge spike R8 揭真實後 Christ 拍板：
- **砍**：Orchestration 層 Mock（MockScenarioService + InternalController `/internal/mock/scenario` endpoint + Dashboard MockScenarioCard + DashboardBotService TriggerMockScenarioAsync）— 全 v4 Pipeline scenario 觸發器
- **留**：LLM 層 Mock（MockClaudeCodeService + MockLlmProvider）— 對 v5.5 path Cody/Vera/Quinn/Sage Mock 仍有效 / forge-self-verify skill 真實依賴

**6. ButtonCallbackRouter 縮為 v5.5 active routing only（713 → 120 行 -83%）**
砍 routing prefix：kickoff_ / design_ / framework_kickoff_mid_interrupt_ / propose_yes / propose_no / propose_adjust / cancel_yes / cancel_no 整套。留 confirm_yes / confirm_no 通用 confirmation routing（v5.5 PetraInbox flag forward only 後 confirm_yes Stage 68 短路 ack）+ BuildCeoDecisionEmbed / BuildConfirmButtons / Truncate static helper（CommandHandler caller）。ctor 5 dep 砍（client / settings / gitHubSettings / serviceProvider / taskGroupService）→ 留 3 dep（interactionService / store / logger）。

**7. CommandHandler 縮為 v5.5 path only（614 → 315 行 -49%）**
砍：HandleDirectAgentChannelMessageAsync + BuildChannelAgentMap + TruncateTitle + 4 Register* helper + SlashCommand 註冊 + 5 v4 path 段（Cancel selection / Kickoff modify / HITL mid interrupt / Design modify / 提案調整輸入） + Action="propose"/"cancel" 分支 + SkipCeoConfirm path。留：v5.5 main flow（ProcessWithClaudeCodeAsync → BuildCeoDecisionEmbed + ceo_confirm BossInteraction → confirm_yes Stage 68 短路）+ Action="reply" defensive branch + DetectImageMediaType 從 SlashCommandRouter 移入（line 405 唯一 caller）。ctor 2 dep 砍（slashRouter / appSettings）。

**8. WorkflowSettings + WorkflowSettingsResolver 保留 conservative 留紀律**
v4 framework flag（UseFrameworkPipeline 等 5 flag）+ v4 max rounds（ReviewAppealMaxRounds 等 5 setting）conservative 留（Dashboard SystemSettings.razor.cs UI 顯示 / 0 production caller after Wave 1 / 不擾 Dashboard UI）。WorkflowSettingsResolver getters 0 caller 但不砍（避免 propagate 到 Dashboard 視覺異常）。對齊 Aria 補強紀律 #4「conservative 留 default branch」。

### 驗收後修正（Wave 2 build verify 自診修）

Wave 2 build verify 揭 12 compile errors 跨 4 files — 全 Forge 自抓自修：
- `Bot.Tests/DesignPromptsTests.cs` 整檔砍（reference `AiTeam.Bot.Workflows.Design` namespace 已砍）
- `Dashboard/Pages/Home/AgentStatusCard.razor.cs` 砍 PauseAgentAsync/ResumeAgentAsync caller / stub snackbar 「v4 已砍」
- `Dashboard/Pages/Tasks/TaskCenter.razor.cs` 砍 RequeueTaskAsync caller / stub snackbar
- `Dashboard/Pages/Tasks/PipelineView.razor.cs` 砍 6 v4 action methods caller（HandlePauseClick / HandleResumeClick / HandlePauseEpicClick / HandleResumeEpicClick / HandleMidInterruptClick + HandleRequeueAsync） / stub snackbar + CanShowMidInterruptButton 永遠 false

修正後 final build：**0 error / 0 C# compile warning**（Stage 78b 59 → 78c 0 全乾淨突破）。

### Mock 覆蓋情況

**N/A 純 refactor / 0 業務邏輯改變**：
- 議題 7 拍板砍 MockScenarioService（v4 framework scenarios 整套 / 0 v5.5 對應）/ 0 Mock scenario 重寫 v5.5
- MockClaudeCodeService + MockLlmProvider 留（LLM 層 Mock / 對 v5.5 path Cody/Vera/Quinn/Sage Mock 仍有效）
- xUnit baseline 涵蓋 v5.5 path 邏輯（Bot.Tests 100 + Generated 127 全綠）
- 真實業務驗（場景 D + H + I）留 Aria gate2 範圍 / Christ 視覺驗收

### 踩坑紀錄（Forge spike 揭真實 + 自診修）

**1. 議題 7 揭真實（Plan v1 階段 / Forge spike R8 紀律）**
Plan v1 §B 鏈漏列 MockScenarioService — Aria 議題 3 拍板 wording「留 Mock 框架其餘整套」與 v4 Pipeline 砍 contradiction（MockScenarioService 整套依賴 TaskGroupService + KickoffMidInterruptTriggerStore / 100% v4 framework scenario 觸發器）。Forge plan 階段 escalate Christ + Aria 拍板路線 A 砍 Orchestration 層 + 留 LLM 層 — Mock 框架分層處理紀律立。

**2. 議題 8 揭真實**
Plan v1 §B 鏈漏列 `Agents/Pm/` folder 5 files — 100% v4 path（Appeal Executors + QaCoordinationService + FrameworkAppealRouter 用）/ Aria + Christ 拍板 Forge 自決納入鏈 B 砍。

**3. 議題 9 揭真實**
Plan v1 §G.4 + Roadmap §鏈 C 寫「Migration drop agent_queues」實質**表真實不存在**（Stage 27a Migration 加 TaskItem.QueueStatus + QueuedAt nullable fields 而非獨立表 / Aria Roadmap 認知錯誤）。grep verify Migrations folder 0 `agent_queues` hit confirmed → Plan v1 §G.4 Migration + pg_dump 備份 + R9 Down() 紀律全廢除 / **scope 從含 Migration 不可逆 → 純 code refactor / 0 不可逆風險**。

**4. Wave 2 build verify 12 errors 自診修**
大批 v4 service 砍後 Dashboard 4 pages（AgentStatusCard / TaskCenter / PipelineView）+ Bot.Tests/DesignPromptsTests 殘留 v4 method caller / namespace reference / Forge 自抓自修 12 errors（議題 1 路線 2 邊界守 stub action methods + DesignPromptsTests 整檔砍）→ final build 0 error 0 warning。對齊 Stage 54 自驗能力突破「驗收 follow-up bug 自抓自修」紀律。

**5. AgentDescriptor R1 校正（Forge plan 階段 grep verify 揭）**
Roadmap §C 場景 C 寫 `AgentDescriptor 期望 0 hit` 實質 v5.5 active type — Plan v1 校正為留 AgentDescriptor record + IAgentExecutor.cs → AgentDescriptor.cs 重命名。對齊「Forge plan 階段 spike 必走 grep verify 真實狀態 / 不憑 Roadmap 假設」紀律延續。

### 校準錨真實落點（自省點 #37 第 8 次累積實證候選）

- raw 預估：250-400K（Roadmap + Plan v1 baseline）
- 真實 context 落點：~預估 600-800K（Aria prep-session ultrathink）— Plan / 實作 / Wave 2 修正 / 自驗 / 結案 6 階段累積
- ratio 估算：×1.5-2.5 → 對齊大規模架構級重構新區間 ×1.57-4.93 中段
- Opus 1M + Extra high safety：60-80%（< 75% 警戒）
- 0 Migration 不可逆風險解除 → scope 性質從 L+（含 Migration）降到 L+（純 code refactor）

### Phase 4 v5.5 路徑下一步

```
Stage 78a ✅ → 78b ✅ → 78c ✅（本 Stage 完）→ 79（v5.5 image flow / M）→ 80（A HITL plan confirmation / M）
            → 81（B 動態 re-planning / L）→ WebUI Stage（K WebUI Talent CRUD + Effort 擴展 + E Token monitoring 視覺化
                                                  + v4 entity drop + Dashboard 重設計為 PetraSession-based）
            → v5.5 完整收口
```

下一步：等 Aria gate1 commit 檢查 + gate2 production 0 regression 驗（場景 D + H + I Christ 視覺驗收）通過後 → Stage 79 開（v5.5 image flow 補完）。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v2.0 | 2026-05-19 | **Forge 結案第一段** — 實作完成（commit `e17e83b` / 108 files / 220 insertions / 22910 deletions / net -22690 行）+ 自驗 PASS（8/8 self-verifiable 場景全綠 / 3 場景 D/H/I Aria gate2 範圍）+ 本機驗證 0 error / 0 C# compile warning（Stage 78b 59 → 78c 0 全乾淨突破）+ Bot.Tests 100 + Generated 127 全綠。**6 鏈完成 + 6 議題 incorporated**（議題 1-4 Plan 階段 + 議題 5-9 Forge spike 實作期揭真實）。**3 新議題 Forge spike 揭真實**：① 議題 7 MockScenarioService 整套依賴 v4 / Aria 議題 3 wording 漏分層 → Christ 拍板路線 A 砍 Orchestration 層 + 留 LLM 層 ② 議題 8 Pm/ folder 5 files 整套 v4 → Forge 自決納入鏈 B 砍 ③ 議題 9 agent_queues 表真實不存在（Stage 27a 加的是 TaskItem.QueueStatus/QueuedAt nullable fields）→ Plan v1 §G.4 Migration drop + R9 Down() 紀律全廢除 / **0 Migration changes / 0 不可逆風險**。**R1 AgentDescriptor 校正**：Roadmap 寫期望 0 hit 實質 v5.5 active → Plan v1 留 AgentDescriptor + 砍 IAgentExecutor/AgentExecutionResult/AgentResultType + 檔案重命名。**議題 1 路線 2 邊界守**：留 TaskGroup+TaskItem+TaskLog+BossInteraction entity + DB tables + 4 Dashboard Pages + DashboardTaskService + BossInteraction Repository + Stage 57 partial unique index / Dashboard 4 pages stub v4 action methods 顯示 snackbar「v4 已砍 / WebUI Stage 重設計」/ WebUI Stage 預備統一重設計範圍。**ButtonCallbackRouter 713 → 120 行 -83% + CommandHandler 614 → 315 行 -49%**（v5.5 active routing only）。**校準錨**：真實落點預估 ~600-800K / 對齊大規模架構級重構新區間 ×1.57-4.93 中段 / Opus 1M + Extra high safety 60-80% / 自省點 #37 第 8 次累積實證候選。**Wave 2 自診修**：build verify 揭 12 errors 4 files / Forge 自抓自修（Dashboard 3 pages stub + Bot.Tests DesignPromptsTests 砍）→ final build 0 error 0 warning。**Phase 4 路徑**：78a ✅ → 78b ✅ → **78c ✅**（本完）→ 79（image flow M）→ 80（HITL M）→ 81（replan L）→ WebUI Stage（entity drop + Dashboard 重設計） → v5.5 完整收口。**下一步**：Aria gate1 commit 檢查 + gate2 production 0 regression 驗（場景 D+H+I Christ 視覺驗收）通過 → Stage 79 開。 |
| v1.0 | 2026-05-19 | 規劃書建立 — v3.70.0 / L+ 規模 / v5.5 Phase 4 候選 C 最終收口（Stage 78a v4 dead code 整套砍完 → 78b v4 dead caller 整套砍完 → 78c v4 Pipeline framework 整套砍 ~25-30 class）。**戰略脈絡**：Christ 2026-05-19 拍板路線 A 一次過整套砍（vs 路線 B 拆三輪 78c-1/78c-2/78c-3 / 路線 C 拆兩輪 — 接受 Migration drop table 不可逆 + 規模 L+ 真實 ~500-700K Opus 1M 50-70% safety）。**6 鏈子項 ~29 條**：鏈 A v4 Pipeline 核心（WorkflowEngine + PipelineState + 7 Stage Executors + PipelineRoutingService）/ 鏈 B v4 Meeting + Appeal + HITL（MeetingOrchestrationService + Framework routers + AppealOrchestrationService + FrameworkHitlBridge + EpicChainService + QaCoordinationService + BossResponseHandlerService + BossNotificationService + InteractionProcessor 評估）/ 鏈 C v4 Queue + Group（TaskGroupService + ProposalConfirmationService + AgentQueueService + AgentQueueProcessor + Migration drop agent_queues + v4 entity 評估 + IAgentExecutor 整套砍）/ 鏈 D 連動 routing（ButtonCallbackRouter v4 Pipeline routing + CommandHandler v4 path + WebhookController PR handler 評估 + 其他 SlashCommand 評估）/ 鏈 E DB schema + Migration（drop agent_queues + v4 entity drop 評估 + Entities.cs + AppDbContext DbSet 砍）/ 鏈 F 連動配套（WorkflowSettings + CeoResponseActions + RoutingTypes + archive prompt + xUnit test + Directory.Build.props v3.69.0 → v3.70.0）。**設計決策核心**：路線 A 一次過 + Migration drop 不可逆接受（agent_queues 0 active row + pg_dump 備份候選紀律）+ backwards-compatible 守護 v5.5 path 0 行為改變 + ctor unused dep cleanup 紀律延續 + Forge spike 揭真實 propagation 紀律。**驗收 10 場景**：A xUnit baseline / B Migration drop 真實 executed / C v4 Pipeline 整套 0 caller grep verify / D v5.5 path production 0 regression / E Bot startup 0 exception / F HealthCheckJob production active 不受影響 / G ButtonCallbackRouter v4 routing 砍 + v5.5 0 行為改變 / H CeoAgentService ProcessWithClaudeCodeAsync 0 行為改變 / I 其他 SlashCommand 砍評估 / J WebhookController PR handler 評估。**Aria 預警 W1-W9**：mixed-use class 砍評估 / Migration drop 不可逆 pg_dump 候選 / v4 entity schema dependency / `/mock` 風險 / WebhookController PR handler / ButtonCallbackRouter routing 完整砍 / IAgentExecutor 整套砍前 0 caller verify / CommandHandler v4 path 砍 / Forge spike 揭真實 escalate 紀律。**校準錨預估**：對齊大規模架構級重構新區間 ×1.57-4.93 中段 / raw 250-400K × ratio ×1.5-2.5 = 真實 ~500-700K / Opus 1M + Extra high safety 50-70% / cost $5-8。**Phase 4 路徑**：78a ✅ → 78b ✅ → **78c**（本 / v4 Pipeline 整套砍 / L+）→ 79（image flow / M）→ 80（HITL / M）→ 81（動態 replan / L）→ WebUI Stage → v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Aria gate2 production 0 regression 驗 → 通過後 Stage 79 開（v5.5 image flow 補完）。 |

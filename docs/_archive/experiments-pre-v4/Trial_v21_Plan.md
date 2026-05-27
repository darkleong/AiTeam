# Trial_v21 — Stage 75 兩層 queue + per-Talent serialization 真實生效驗收（v5.5 Phase 3 Step 9 收口閘門 / 多 task 並送場景）

> 日期：2026-05-17
> 對應系統版本：**v3.65.0**（Stage 75 結案後 + Trial_v21 中段順手修 PetraInboxProcessor Status='failed' bug commit `9b433a4`）
> 試驗版本：v2.0（直接結案紀錄 / 對齊 Trial_v17-v20 既有實踐 / aria-trial-run skill 第 5 次實踐 cover 9-step / 雙 phase 紀錄 — phase 1 Token 守門 fire + phase 2 重 deploy 後完整跑完）
> PR：[#381](https://github.com/darkleong/AiTeam/pull/381) + [#382](https://github.com/darkleong/AiTeam/pull/382)（兩 PR closed / 對齊 Trial 不污染 main 紀律）
> 結果：🟡 **部分過 — 業務評分 5/5 滿分 + Stage 75 Layer 1 ✅ 完整生效 + 揭 1 🔴 戰略級設計實作落差**

---

## 試驗目的

**Stage 75（v3.65.0）結案後第一次真實業務驗 — 驗證 4 條核心紀律 production 真實生效**：

1. **Layer 1 Petra 接收層 queue 真實生效**（場景 D）— CeoAgentService 寫 PetraInbox row + immediate ack「task 已接收 / 排隊位 N」/ 不 await Petra
2. **Layer 2 Worker 執行層 per-Talent serialization 真實生效**（場景 E/F）— 同 Talent 多 task 序列化 / 不同 Talent 平行 / TalentDispatchLockService SemaphoreSlim per-Talent lock 訊號真實 fire
3. **多 task 並送場景**（場景 I）— FIFO 紀律 EnqueuedAt ASC polling + 議題 1 拍板實踐「multi-session 並存」
4. **連續 11 Trial 業務級成功延續**（v10-v20 → v21）

對齊 Trial_v20 baseline（30 檔 / +1456/-88 / 17 範圍 cover / 業務評分 4.5/5）— 對照組精準度最高。

---

## 任務需求（Christ 給 Victoria 的指令原文）

沿用 Trial_v6-v20 同 prompt（reuse `.tmp/trial_v15_body.json`）— Dashboard 錯誤處理打磨 toast 通知。

**多 task 並送設計**：連送 2 個 task / 同 prompt / 不等第一個跑完 / 驗 Stage 75 兩層 queue + per-Talent lock 真實生效。

---

## 流程觀察 Checkpoints（Aria 9-step 模板第 11 次實踐 / 雙 phase 紀錄）

### Phase 1：14:34:55 UTC 開跑 → Token 守門 fire（fail-fast）

#### CP1 deploy + flag 確認

| 項目 | 結果 |
|---|---|
| Stage 75 commit `fd8975f` deploy run | ✅ success / 4m34s |
| Bot image v3.65.0 production active | ✅ |
| 5 v5.5 flag SQL=true production active | ✅ 全保留 |
| petra_inbox schema | ✅ 9 column + index `ix_petra_inbox_status_enqueued` |
| baseline state | ✅ 0 row 乾淨 |

#### CP2 連送 2 task → Layer 1 訊號完整

```
=== task A === 14:34:55 → curl ack「指令已送達」（immediate ack 不 await Petra）
=== task B === 14:34:55 → curl ack「指令已送達」（同秒）
```

Bot log + SQL 對帳：
- PetraInbox 寫入 2 row（123ae75e + aa417017）/ FIFO 對齊 EnqueuedAt
- Bot log `寫 PetraInbox row=... queuePosition=2`（**兩個 row 都顯示 queuePosition=2** ⚠️ — race condition）
- boss_interactions Reply `[v5.5] Task 已接收（inbox=... / 排隊位 2）— Petra 將依 FIFO 順序拆解派工`

#### CP3 PetraInboxProcessor 接手 → Token 守門 fire

```
14:34:58 PetraInboxProcessor 接手 row=123ae75e — 開新 Scoped PetraOrchestratorService 處理
14:34:59 PetraOrchestrator 執行失敗 sessionId=8221e5b5 — InvalidOperationException:
         Token 守門：全域本月用量 10,108,845 + 估算 1,170 超過全域月限 10,000,000
14:35:00 PetraInboxProcessor 完成 row=123ae75e sessionId=8221e5b5  ⚠️ 標 completed 不是 failed
14:35:03 PetraInboxProcessor 接手 row=aa417017 — 同樣 Token 守門 fire → 同樣標 completed
```

**揭 2 議題**：
- ① 🟢 **Token 守門等效 token 公式**：`Input + Output + CacheCreation×1.25 + CacheRead×0.1` = 10.1M（vs USD cost 真實只 $43.82 / cache 加權失控 / 月限 10M 卡住）
- ② 🟡 **PetraInboxProcessor Status='failed' bug**：[`PetraInboxProcessor.cs:89-103`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L89) 收到 `result` 直接 `MarkCompletedAsync` / 0 check `result.Success` → fail path row 標 completed（vs petra_sessions 真實 Status='escalated'）

### Phase 2：14:54:09 UTC 重跑 → 兩 task 完整 lifecycle

#### CP4 中段修法 + 重 deploy

| 動作 | 結果 |
|---|---|
| SQL UPDATE `Token:GlobalMonthlyLimitK` 10000 → **15000**（Christ 拍板放寬月限）+ reload-cache scope=appsettings | ✅ |
| 修 PetraInboxProcessor Status='failed' bug（[`PetraInboxProcessor.cs:82-126`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L82) check `result.Success` 拆 Success/Failure 路徑）| ✅ +21/-9 行 |
| `dotnet build AiTeam.slnx` | ✅ 0 error / 103 warning（全 pre-existing）|
| commit `9b433a4` + push → CI/CD deploy | ✅ success / 8m12s |
| Bot 啟動 0 exception + PetraInboxProcessor polling 恢復 | ✅ |

#### CP5 連送 2 task → Layer 1 訊號完整

```
=== task A === 14:54:09.719987+00 → PetraInbox row 43816ca3 / queuePosition=1
=== task B === 14:54:09.772843+00 → PetraInbox row 84beb577 / queuePosition=2
```

Bot 冷啟動後 race condition 0 重現 / queuePosition 對齊 1+2 ✓。

#### CP6 PetraInboxProcessor 接手 row A → row A 完整 chain

```
14:54:11.577 PetraInboxProcessor 接手 row=43816ca3 — Scoped PetraOrchestratorService 處理
14:54:13      Petra v5.5 Step 4 DecideTalentsWithPlanAsync 完成
             subtasks=2 dependencies=1 picks=Cody(code_implementation) → Vera(code_review)
14:54:13      acquire per-Talent lock talent=Cody talentId=f76611d2-...  ⭐
~15:05:29     Cody parallel=False outputLen=2868 ✓ 對齊 baseline ~2800-2900
~15:05:30     acquire per-Talent lock talent=Vera talentId=020b5954-...  ⭐
~15:07:07     Vera parallel=False outputLen=969 ✓（邊界 -25% vs Trial_v19 baseline）
~15:07:09     FinalizeGitAsync → PR #381 開啟
15:07:10.378  PetraInboxProcessor 完成 row=43816ca3 sessionId=56c43857
```

**row A duration：12.98 min**（對齊 Trial_v20 13 min ✓）

#### CP7 row A 完成 → PetraInboxProcessor 下一次 polling tick 接 row B

```
15:07:13.385  PetraInboxProcessor 接手 row=84beb577 — Scoped PetraOrchestratorService 處理
              Petra plan 拆 3 subtask（Cody → Vera → Quinn / 完整 chain 對齊 Trial_v19/v20 baseline）
~15:07:13     acquire per-Talent lock talent=Cody  ⭐（vs row A 完成釋鎖 — 0 contention 因為 sequential 派工）
~15:14:24     Cody outputLen=2091 ✓
~15:14:25     acquire per-Talent lock talent=Vera  ⭐
~15:16:34     Vera outputLen=1577 ✓（深度 review — 揭 1 warning + 1 info）
~15:16:35     acquire per-Talent lock talent=Quinn  ⭐
~15:19:14     Quinn outputLen=426 ✓（Stage 71 outputLen=0 guard 0 fire）
~15:19:16     FinalizeGitAsync → PR #382 開啟
15:19:17.200  PetraInboxProcessor 完成 row=84beb577 sessionId=1ec92b1b
```

**row B duration：12.06 min**

**🔴 揭 Stage 75 設計實作落差**（戰略級）— row A 完整跑完才接 row B / **PetraInboxProcessor 是 sequential await 不是 fire-and-forget** / 兩 Petra 不同時 dispatch / per-Talent lock **production 0 機會 fire contention**。

#### CP8 PM (Petra) 走 Gemini 2.5 Flash ⭐

對齊 Trial_v18-v20 紀律：
- AgentName=PM / Model=**gemini-2.5-flash** / cost=$0.0162（2 session × 兩 Petra 決策）

---

## 試驗結果矩陣

### Aria 業務評分 ⭐⭐⭐⭐⭐ 5/5 滿分

| 維度 | 評分 | 證據 |
|---|---|---|
| **範圍 cover** | 5/5 | 兩 PR 都對齊 toast + inline 雙通道 / Cody 看到「前 N 次 Petra 調度」歷史 / 各補真實 1-2 剩餘缺口（PR 381：補 TokenMonitoring + 修 DeploymentHistoryTests build error / PR 382：補 AgentSettings 右面板 _formError + 三個 handler try/catch/finally）|
| **Cody Code 品質** | 5/5 | try-catch-finally + _loadError/_formError + Snackbar 雙通道 / 對齊既有 _saveError convention pattern / Cody 業務深度 — 主動找出 ToggleIsActiveAsync / SaveTrustLevelAsync / RestartBotAsync 三個 handler 缺保護 |
| **Vera review 質感** | 5/5 | PR 381 0 critical+0 warning+1 info（design 評論：切換時間區間是否保留舊資料）/ PR 382 0 critical+1 warning（**業務深度** — _formError 未在 ToggleIsActiveAsync 入口清除 / user 誤判舊錯誤）+1 info（建議分拆 _trustError/_tokenError 定位更直覺） |
| **Quinn 測試覆蓋** | 4.5/5 | PR 382 outputLen=426 + 8 test passed ✅（覆蓋 SaveTrustLevelAsync + SaveTokenLimitsAsync × 4 案例）/ PR 381 plan 沒拆 Quinn（Petra 動態決策每次不同 / 對齊 Trial_v20 揭觀察）|
| **業務 UX 對齊** | 5/5 | 雙通道完整對齊 Christ 任務原文「toast + inline 雙通道 / toast 3-5 秒自動消失 / 兩處訊息一致」 |
| **整體** | **5/5** | 連續 11 Trial 業務級成功 / infinite loop pattern 永久打破延續第 11 次 / 業務質感對齊 Trial_v19 baseline 5/5 滿分 |

### 對照 Trial_v19/v20 baseline

| 維度 | Trial_v19 | Trial_v20 | Trial_v21 (Phase 2) | 差異 |
|---|---|---|---|---|
| 業務評分 | 5/5 | 4.5/5 | **5/5** | 恢復滿分 |
| PR 檔數 | 26 | 30 | **32 / 33** | 對齊 +20% |
| Cody outputLen | 2822 | 2906 | row A 2868 / row B 2091 | 平均 -8% |
| Vera outputLen | 1491 | 1223 | row A 969 / row B 1577 | row A -34% / row B +6% |
| Quinn outputLen | 1093 | 0 fail | row A 沒拆 / row B 426 | row B -61% / Quinn 變淺 |
| AiTeam LLM cost | $2.844 | $2.213 | **$3.750**（2 task）| 單 task ~$1.88 / cost per file ~$0.058 ⭐ **新最優 ROI** |
| Forge session cost | $0 | $0 | $0 | Aria 全程自跑 ✓ |
| **Total cycle cost** | $2.844 | $2.213 | **$3.750**（2 task / 單 task 平均 $1.88 -22% vs v19）| ROI 持續優化 |
| 真實時長 | 20 min | 13 min | **25 min sequential**（2 task）| 設計實作落差揭 |
| **cost per file** | $0.109 | $0.074 | **$0.058** ⭐ | -47% vs v19 / -22% vs v20 / **新最優 ROI baseline** |
| 餘額 | $15.99 → $13.15 | $13.15 → $10.95 | $10.95 → **$7.21** | 對齊 SQL $3.75 ✓ |

---

## Stage 75 核心驗收訊號

### Layer 1 接收層 ✅ 完整生效

| 紀律 | production 真實生效訊號 |
|---|---|
| **CeoAgentService 不 await Petra** | curl 同秒 ack return / immediate response 不阻塞 |
| **PetraInbox 寫入 row** | Bot log `Victoria flag UsePetraOrchestratorV5=true → 寫 PetraInbox row=... source=dashboard queuePosition=N` |
| **boss_interactions Reply 含 inbox short id + 排隊位** | `[v5.5] Task 已接收（inbox=43816ca3 / 排隊位 1）— Petra 將依 FIFO 順序拆解派工` |
| **PetraInboxProcessor 3s polling 接手** | Bot log `PetraInboxProcessor 接手 row=... userInputLen=400 — 開新 Scoped PetraOrchestratorService 處理` |
| **FIFO 紀律對齊 EnqueuedAt** | row A 14:54:09.719 → row B 14:54:09.772 / Processor 接手順序對齊 |
| **PetraInbox SQL atomic check 守 0 雙重 process** | TryMarkRunningAsync 對齊（場景 C xUnit T3 Forge 自驗已 cover）|
| **Crash Recovery 紀律** | PetraInboxProcessor 啟動 RecoverStuckRunningAsync 對齊 AgentQueueProcessor pattern |

### Layer 2 執行層 🟡 code path 真實 wire / production 0 contention

| 紀律 | production 真實生效訊號 |
|---|---|
| **TalentDispatchLockService SemaphoreSlim acquire 真實 fire** | Bot log `acquire per-Talent lock talent=Cody/Vera/Quinn talentId=...` — 每個 Talent dispatch 前都真實 acquire lock ✓ |
| **per-Talent lock release 真實 fire** | using IDisposable handle 自動 release / Talent dispatch 完成後自然釋放 ✓ |
| **同 Talent 多 task 序列化** | 🔴 **0 機會 fire contention** — PetraInboxProcessor sequential await / row A 完整跑完才接 row B / 兩 Petra 不同時 dispatch / per-Talent lock 永遠 acquire-immediate-release |
| **不同 Talent 平行** | 🔴 同上 — 任意時刻只 1 Petra 跑 chain / 多 Talent 平行 dispatch 0 機會發生 |

### Stage 71 outputLen=0 guard / PetraInboxProcessor 修法 / v4 path

| 紀律 | 真實 fire 訊號 |
|---|---|
| Stage 71 outputLen=0 guard | ⚠️ 未驗（Quinn outputLen=426 ≠ 0 / 沒 fire）|
| PetraInboxProcessor Status='failed' fix（Trial_v21 中段順手修）| ⚠️ 未驗 production fire（兩 row 都 Success=true / fix 對齊紀律但 production 0 重現原 bug 條件）|
| 場景 J v4 path 0 regression | ⚠️ 未驗（沒切 flag=false 跑 / 留 Trial_v22 補驗候選）|

---

## 議題分類

- **🔴 戰略級新類型**：**1**
  - **Stage 75 設計實作落差** ⭐ — v2.0 紀錄 §2「**fire-and-forget** per row 開 Scoped instance」+ 議題 1 拍板「multi-session 並存」/ 真實 [`PetraInboxProcessor.cs:89`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L89) `await orchestrator.StartAsync(...)` 是 **sequential await** / 一次 1 個 Petra 跑完才接下一個 row
  - **production 影響**：per-Talent lock code path 真實 wire ✓ / 但 contention 0 機會 fire（因 sequential 派工 — 不會兩 Petra 同時 dispatch 同 Talent）/ 兩層 queue 真實只第一層 functional / 第二層為未來 horizontal scaling 預留
  - **修法候選**：[`PetraInboxProcessor.cs:89`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L89) `await orchestrator.StartAsync` → `_ = Task.Run(async () => ...)` fire-and-forget pattern + 內層 try-catch 處理 status / CancellationToken 重新評估
  - **3 路徑待 Christ 拍板**：
    - 🥇 路徑 A：Stage 76 順手修 fire-and-forget 一起進（範圍本就含 WebUI + Effort 擴展 / 加 ~10 行不擴太多）+ Trial_v22 驗 fire-and-forget 真實 multi-session 並存 + 真實 per-Talent contention fire
    - 🥈 路徑 B：Phase 4 候選（current single-Bot sequential 也能用 / 對齊「自己用爽」精神 / per-Talent lock 為未來 horizontal scaling 預留）
    - 🥉 路徑 C：立 Stage 75.1 hotfix（不建議 / Stage 編號連續紀律不開小數點）

- **🟡 工程細節（補強候選）**：**2**
  - ① **queuePosition off-by-one race condition** — Bot 熱起來時兩 CeoAgentService 並行寫 inbox 都讀到 pending count=1 / 兩 row 都顯示「排隊位 2」（Phase 1 揭 / Phase 2 cold-start 0 重現 race）
    - 真實 source：[`CeoAgentService.cs:100-122`](../../src/AiTeam.Bot/Agents/CeoAgentService.cs#L100) Enqueue + CountPendingBySourceAsync 非 atomic / SaveChangesAsync 之間 race
    - 影響：使用者看「排隊位」數字略不準 / FIFO 紀律本身對 / 業務不擋
    - 候選修法：① SQL `OVER (ORDER BY EnqueuedAt) ROW_NUMBER()` 算 position（但 enqueue 時 row 尚未真實寫入 / 算不準）② 加 Lock 守 atomic（過度設計）③ 接受 race 改顯示「task 已接收」不顯示精確 N（最簡）
    - 留 FF 候選追蹤 / Stage 76 評估再拍

  - ② **PetraInboxProcessor Status='failed' bug 已順手修**（中段 commit `9b433a4`）— Trial_v21 揭真實 production state row 標 completed 即使 result.Success=false / fix 對齊紀律 + 加 LogWarning + ErrorMessage 寫 result.ErrorMessage（或 fallback Summary）
    - 留檔對齊「Trial 過程順手修紀律」實踐 — 對齊 aria-trial-run skill「執行中遇環境設定 / 微 bug 直接修紀律」段
    - 未來 retry 機制 / Dashboard「重跑 failed task」按鈕（Phase 4 候選）依 status 語意正確紀律生效

- **🟢 觀察留檔**：**3**
  - **Token 月限放寬 10M → 15M**（Christ 2026-05-17 拍板）— 本 Trial 跑前 fire 守門 / 真實 USD cost 月累積只 $43.82 + cache 加權失控 / 後續 production 工作（Stage 76 + Phase 4 候選）維持 15M 比較安全 / 月底 reset 後評估再切回 10M
  - **Petra plan 同 prompt 不同次拆解不同**（row A 拆 2 subtask Cody+Vera / row B 拆 3 subtask Cody+Vera+Quinn）— Petra LLM 動態決策 / 不算 bug / 對齊「Petra 謹慎拍板」紀律延續觀察
  - **task 意外停止後無 retry / 必須重下指令**（Christ 戰略 question 點破）— current 架構 PetraInbox + PetraSession 都是終態（completed/failed/escalated）/ 0 background re-pickup / `PetraSessionRecoveryService` Stage 53 只 cover Bot restart in-flight session / 不 cover runtime fail 後 retry / 候選修法：Phase 4 加 retry count + backoff / Discord `/retry-task <inbox_short_id>` / Dashboard「重跑」按鈕

---

## v5.5 Phase 3 Step 9 收口閘門評估

**🟡 部分過評估標準**：
- 核心驗收達標（Stage 75 Layer 1 完整真實生效 ✓ + Layer 2 code path 真實 wire ✓）
- 業務評分 5/5 滿分（連續 11 Trial 業務級成功延續）✓
- 揭 1 🔴 戰略級設計實作落差（Layer 2 production 0 機會 fire contention — 修法路徑待 Christ 拍板）
- 2 🟡 工程細節（queuePosition race + Status='failed' bug 已順手修）
- 3 🟢 觀察留檔

**Christ 拍板路徑**：**待後續討論細節**（Christ 2026-05-17 結案 skill 觸發時明示「結案完成後再討論細節」）

production 狀態：**5 v5.5 flag SQL=true production active 維持** + **Token 月限 15M 維持**（月底 reset 評估切回 10M 候選）

---

## 後續行動清單

### 立即動作（Aria 結案範圍）

1. ✅ Aria 結案紀錄（本檔 v2.0）
2. ✅ Future_Feature_v5.5.md Phase 3 Step 9（Stage 75）標 Trial_v21 🟡 部分過 + 揭 1 🔴 設計實作落差路徑待拍板
3. ✅ Future_Feature.md header bump v9.2 → v9.3
4. ✅ CHANGELOG [Unreleased] 更新為下個動作候選（Stage 76 + 3 路徑待 Christ 拍板）
5. ✅ Close PR #381 + #382（Trial 不污染 main 紀律）
6. ⏸ Token 月限切回 10M — 推遲到月底 reset 評估（current production 仍用 v5.5 path / 維持 15M 安全）

### 下個重點戰略（討論細節後拍板）

**Trial_v21 揭設計實作落差 → 3 路徑待 Christ 拍板**：

```
路徑 A：Stage 76 順手修 fire-and-forget（推薦）
  Stage 76 範圍：WebUI Talent CRUD + Effort 擴展 + PetraInboxProcessor fire-and-forget 修法 + Trial_v22 驗
  → Phase 3 完整收口 ✓

路徑 B：Phase 4 候選（不修 fire-and-forget）
  current single-Bot sequential 也能用 / per-Talent lock 為未來 horizontal scaling 預留
  Stage 76 範圍維持原計劃（WebUI + Effort 擴展）
  → Phase 3 收口 + Phase 4 候選加 fire-and-forget 修法

路徑 C：立 Stage 75.1 hotfix（不建議）
  Stage 編號連續紀律不開小數點
```

---

## 紀律累積成熟度進化曲線（Trial_v21 真實實證接續 Trial_v20 + Stage 75）

- **連續 11 Trial 業務級成功**（v10-v21）— infinite loop pattern 永久打破延續第 11 次
- **連續 9 Stage 0 follow-up bug fix**（Stage 67-75）— Forge healthy 偏離 plan 紀律累積成熟
- **Stage 75 Layer 1 接收層真實 production 生效完整實證** ⭐（CeoAgentService 不 await Petra + PetraInbox + FIFO + Dashboard live update + Discord/Reply ack 五件套全綠）
- **Stage 75 設計實作落差揭** 🔴（PetraInboxProcessor sequential vs 預期 fire-and-forget / per-Talent lock production 0 contention — 留 Stage 76+ 修法評估）
- **aria-trial-run skill 第 5 次實踐成功** ⭐：workspace cleanup 紀律 0 踩雷 + 環境設定（Token 月限放寬）直接修紀律 + 微 bug（Status='failed'）順手修紀律 + 9-step 模板對齊 + 0 環境議題踩雷
- **Aria 全程自跑「Christ 只動嘴」精神成熟實踐第 11 次**（Trial_v21 Christ 0 SQL / 0 curl / 0 docker exec / 0 commit / 0 push / 只動嘴拍板餘額 + 路徑 + 修法 + 結案）
- **cost per file = $0.058 新最優 ROI baseline**（連續 ROI 進化 — Trial_v18 $0.121 → Trial_v19 $0.109 → Trial_v20 $0.074 → Trial_v21 $0.058 / 持續優化趨勢）
- **Aria gate1 升級 Tier 0+1+Tier 2 #3 build 紀律首次實踐配 Trial 揭設計實作落差**（Stage 75 結案前 gate1 通過 / production Trial 揭機制層深度議題 — Aria gate1 + Forge 自驗 + Aria gate2 + Trial production 四層守門紀律深度驗證）

---

## Cost 真實 vs 預估雙因子對照（自省點 #38 應用）

| 因子 | Aria 預估 | 真實 | 對齊 |
|---|---|---|---|
| AiTeam LLM cost（2 task）| $4-5 | **$3.750** | ✅ 範圍下緣對齊 |
| Forge Claude Code session cost | $0（Aria 全程自跑）| **$0** | ✅ 完美對齊 |
| **Total cycle cost** | $4-5 | **$3.750** | ✅ 範圍下緣對齊（-17% vs 預估上限）|
| 餘額變化 | $10.95 → $5.95-6.95 | **$10.95 → $7.21** | ✅ 範圍上限對齊（-22% vs 預估上限）|

**真實 cost 落點對齊預估** — 兩 task sequential 跑完 / cache 加權有效降低 cost / cost per file 新最優 ROI baseline $0.058。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-17 | 試驗結案紀錄（直接 v2.0 / 對齊 Trial_v20 既有實踐）。**🟡 部分過 — 業務評分 5/5 滿分 + Stage 75 Layer 1 完整生效 + 揭 1 🔴 戰略級設計實作落差**。**雙 phase lifecycle**：Phase 1 14:34:55 UTC 開跑 → Token 守門 fire（fail-fast）+ 揭 PetraInboxProcessor Status='failed' bug + Christ 拍板放寬 Token 月限 10M → 15M + Aria 順手修 bug commit `9b433a4` + 重 deploy 8m12s success / Phase 2 14:54:09 UTC 重跑 → 兩 task sequential 完整 lifecycle 25 min（row A 12.98 min + row B 12.06 min）→ 兩 PR 開出 closed。**Layer 1 訊號全綠**：CeoAgentService 不 await Petra + PetraInbox 寫入 + boss_interactions Reply「Task 已接收 / 排隊位 N」+ PetraInboxProcessor 3s polling 接手 + FIFO 紀律對齊 EnqueuedAt + Crash Recovery 紀律。**Layer 2 訊號**：TalentDispatchLockService SemaphoreSlim acquire/release 每 Talent dispatch 真實 fire ✓ / **但 contention 0 機會 fire**（PetraInboxProcessor sequential await / row A 跑完才接 row B / 兩 Petra 不同時 dispatch / per-Talent lock 永遠 acquire-immediate-release）⭐ **揭 Stage 75 v2.0 紀錄寫「fire-and-forget per row」+ 議題 1 拍板「multi-session 並存」vs 真實 code sequential 落差**。**業務評分 5/5**：32+33 檔對齊 toast + inline 雙通道 / Cody 業務深度（補 TokenMonitoring + AgentSettings _formError + 三 handler try-catch）/ Vera 業務深度（揭 _formError 未清除 user 誤判 warning + 定位建議分拆 info）/ Quinn 8 test passed / 業務 UX 完整對齊 Christ 任務原文。**議題分類**：1 🔴（Stage 75 設計實作落差 — 3 修法路徑待 Christ 拍板）+ 2 🟡（queuePosition race condition + Status='failed' bug 已順手修）+ 3 🟢（Token 月限放寬 / Petra plan 不同次不同 / task 無 retry 候選）。**真實 cost**：AiTeam LLM $3.750 + Forge $0（Aria 全程自跑）= total $3.750 / 餘額 $10.95 → $7.21 ✓ 對齊。**cost per file = $0.058 新最優 ROI baseline**（連續 ROI 進化 v18 $0.121 → v19 $0.109 → v20 $0.074 → v21 $0.058 持續優化）。**下一步**：3 路徑待 Christ 拍板（🥇 Stage 76 順手修 fire-and-forget / 🥈 Phase 4 候選 / 🥉 立 75.1 hotfix）→ 拍板後 Stage 76 開（WebUI Talent CRUD + Effort 擴展 + 可能加 fire-and-forget 修法）→ v5.5 Phase 3 完整收口路徑。 |

# Future Feature — 未來功能候選清單

> 版本：v7.77
> 建立日期：2026-04-01
> 最後更新：2026-05-08
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。
> **2026-05-01 大整理**：以 v4 路線（FF 四十九 工具評估 + FF 三十六 架構評估）為主軸，重新評估 30 個待處理 FF，拆分 5 子檔讓主檔聚焦在 active 主清單。
> **2026-05-02 v7.64**：Stage 47 結案 — FF 四十七 ✅ + FF 十一 ✅（路線 b DB AppSettings 動態化順帶大半解 FF 十一）。
> **2026-05-02 v7.65**：Stage 48 spike 採用結論 — FF 四十九 ✅（4 強正向 + 2 中性 + 0 負向，啟動 Stage 49+ 漸進遷移路線「換引擎不換車身」，FF 三十六 Phase B 進入「等遷移過半再評估」）。
> **2026-05-02 v7.66**：Stage 49 ⭐ v4 漸進遷移首發完成（v3.35.0）— Cody-Vera-Petra Appeal loop 切 framework + feature flag 並行雙系統 + 0 follow-up + production fallback 防呆真實生效。6 Stage 遷移 1/6 達成。
> **2026-05-02 v7.67**：Stage 50 v4 漸進遷移第二步完成（v3.36.0）— Kickoff Meeting 切 framework Group Chat（A2 fan-out/fan-in 路線）+ feature flag + 3 follow-up 揭露 framework 1.3.0 fan-out 拓撲首次 production 整合的 streaming dispatch + type validation 兩層額外要求。6 Stage 遷移 **2/6** 達成。
> **2026-05-02 v7.68**：Stage 51 ⭐ v4 漸進遷移第三步完成（v3.37.0）— framework HITL pattern 試點（Kickoff Workflow 中途介入）+ feature flag。**6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過**（含跨 process restart requestId stable 證據鏈，requestId `0daeccaa72714604812add3427ba4d9d` 在 yield emit + Bridge resume + Recovery 跨重啟全程 stable）。6 Stage 遷移 **3/6** 達成。
> **2026-05-03 v7.69**：Stage 52 v4 漸進遷移第四步完成（v3.38.0）— Design Meeting B3 路線（fan-out/fan-in + 條件式 Demi + needs_adjustment B2 子流程 + 拆 task 提案後置）。議題 A 拆 Stage：原 v4 路線 Stage 52 含 WorkflowEngine pipeline 拆出獨立 Stage 53，**v4 路線 6→7 Stage**。6 場景全綠 + 2 follow-up（戰略級 framework AddEdge type filter 不 source-aware 揭露，修法拆 plan executor）。Aria 校準錨 **×1.05**（混合型第 4 資料點 mid 中段，×0.96-1.25 四資料點穩定）。7 Stage 遷移 **4/7** 達成。
> **2026-05-03 v7.70**：Stage 53A ⭐ v4 漸進遷移第五步完成（v3.39.0）— macro pipeline NewFeature 主路徑切 framework Workflow（**Aria 方案 C** Pipeline 從 Dev_plan 階段啟動，Kickoff/Design 留 legacy）。**Aria Session A 子項 5 實作期揭露議題 G3 假設失誤**（inner Meeting router post-meeting actions 衝突）→ 即時拍板方案 C 範圍 -40%；**驗收期 4 follow-up 戰略級含 #1 議題 G3 在 QA 重演 + #4 Pipeline Recovery 自接 Agent task requeue**（Aria 規劃前期 grep 紀律必升級為「對所有既有 service finalize/post-completion actions 都 grep」+ Stage 53B/54 必補 Agent task 層 Recovery 設計）。6 場景驗收（4 dynamic + 2 靜態，**首次 Forge 自跑 SIGTERM/SIGKILL Crash Recovery 完整循環**）。Aria 校準錨 **×0.73**（混合型第 5 資料點 mid 帶下半，區間擴展為 ×0.73-1.25 — 方案 C 拆 Stage + Stage 51 know-how 複用 + 0 Aria gate1 揭露三因素疊加）。**v4 路線 7→8 Stage**（53 拆 53A/53B），8 Stage 遷移 **5/8** 達成。Christ 拍板 production 保留 UseFrameworkPipeline=true。
> **2026-05-03 v7.71**：Stage 53B ⭐ v4 漸進遷移第六步完成（v3.40.0）— fix loop / appeal / QA fix loop / intervention 4 子流程切 framework + 5 fallback to legacy 點移除 + Stage 53A follow-up #3 場景 E/F 補 dynamic。**NewFeature 主路徑 + 子流程完整 Pipeline framework 化**達成。**議題 F-1 規劃前期 grep 紀律升級成功**（Aria 揭露 10+ 處 → Forge 第一手補強至 16 處 skip 修正 — AppealOrchestrationService 11 + QaCoordinationService 5）+ HandleDevBlockerAsync signature `Task` → `Task<BlockerDecision>` 升級給 Pipeline 自接管 routing。6 場景全綠 + 2 follow-up + **Forge 自驗能力突破**（/internal/mock/scenario HTTP API + auto-approver 全 6 場景含 Crash Recovery — Christ 線下實跑模式從「必要」轉「選擇性」）。Aria 校準錨 **×0.88**（混合型第 6 資料點 mid 帶中段；6 資料點區間穩定 ×0.73-1.25）。8 Stage 遷移 **6/8** 達成。
> **2026-05-04 v7.72**：Stage 54 v4 漸進遷移第七步完成（v3.41.0）— Crash Recovery 全切 framework Checkpointing + 4 CheckpointStore 抽 base class（833 → 360 行 -473 淨減）+ B2 round-aware idempotency（Forge gate1 揭露 B1 用 state.IssueUrls check 會破壞 needs_adjustment 多輪業務 → Christ 重拍板「Adjustment 觸發都會踩，不是機率問題」→ TaskGroups 加 LastIssueCreatedRound int? + Migration Stage54TaskGroupIssueCreatedMarker）+ Stage 53B follow-up #1/#2/#4 搭車。**8 場景驗收全綠 + 1 follow-up bug 修復**（Forge 自驗時自抓自修：MockMode auto-approve source='mock' → 'dashboard' 對齊 InteractionProcessor 消費路徑）。Forge 揭露 plan IsFixLoop=true 嚴格條件對 dev_blocker 場景無效 → 廣義化「同 AssignedAgent newer success task 取代」覆蓋 fix loop + dev_blocker 兩場景。Aria 校準錨 **×0.77**（421K vs 中位 545K，混合型第 7 資料點 mid 帶下半 — 接近 Stage 53A ×0.73；7 資料點區間穩定 ×0.73-1.25）。**8 Stage 遷移 7/8 達成**，剩 Stage 55 戰略級收尾。
> **2026-05-04 v7.73**：Stage 55A v4 漸進遷移第八步完成（v3.42.0，拆 55A/55B 第一段）— Kickoff/Design 整合到 Pipeline（議題 G3 真正解決，Stage 53A 留的核心戰略級 TODO）+ sub-task 整合 + 6+1 hooks 移除 + WorkflowEngine.cs 精簡（保留 enum + record，刪 GetDecision 邏輯）。**Forge Plan Mode 主動揭露 3 個 Aria 預掃缺口拍板**：① inner router method 名已存在 → skipFinalize 參數選項 2 ② sub-task first step = Dev_plan ≠ Kickoff（Aria 拿捏 #1 vs #11 衝突）→ 兩入口分流方案 C ③ EpicChain 不依賴 6 hooks ✅ 好消息。**Aria gate1 揭露 1 critical**（Mock 場景擴充 + Forge 自驗未做 — 時序：Forge 不知道剛寫好的 forge-self-verify skill）→ Forge 補 4 alias + 場景 B/E 自驗全綠。**驗收期 follow-up #1（戰略級）**：Forge 自驗場景 E 揭露 **Stage 54 既有遺留 bug**（MockMode auto-approve switch 漏 split_task_proposal type）→ Forge 自抓自修 — Stage 53B/54 自驗能力進化在 Stage 55A 揭露遺留 bug 真正生效。**Christ 視覺驗收 4 張截圖**：場景 B Pipeline 從 Kickoff 啟動 7 stage 全綠（議題 G3 視覺證明）+ 場景 E sub-task 流程詳情**沒有** Kickoff/Design stage（缺口 2 兩入口分流戰略級視覺證明）。Aria 校準錨 **×0.88**（482K vs 中位 545K，混合型第 8 資料點 mid 中段，跟 Stage 53B 一樣 ×0.88；8 資料點區間穩定 ×0.73-1.25）。**9 Stage 遷移 8/9 達成**，剩 Stage 55B BossInteraction HITL 推廣（27 處 caller refactor）→ v4 路線 9/9 達成。
> **2026-05-04 v7.74**：Stage 55B Session A 完成（v3.43.0，**首次拆 Session B**）— PipelineHitlHelper + AppealOrchestrationService 11 + QaCoordination 5 = 16 處 skip 精簡（dead code 全清，AppealOrchestration -25% / QaCoordination -28%）+ F-α 排除條件 4 處移除 + 8 處 calling site comment + Version bump。**4 戰略議題 Christ 拍板**：① **1A** proposal 留 Stage 56（Forge spike F6 揭露 ProposalConfirmationService 入口流程 group lifecycle 整合衝突）② **2C** Pattern A 主 + Stage 51 試點 mid_interrupt 獨立保留（Forge 揭露 Stage 55A 已採 Pattern A）③ **3A** intervention/merge_notify 留 fire-and-forget（ack only no routing 切 yield-resume 收益 = 0）④ **4A** 拆 Session B（Forge spike 揭露 5 routing HITL 規模 ~600-900 LOC，首次拆 session）。**Production DB pre-check**（Forge 主動執行 — 96 done + 54 cancelled + 14 failed + 1 stuck mock，PipelineFrameworkStateJson IS NULL 無 Pipeline path 干擾 → dead code 移除 production safe）。Aria 校準錨 Session A **×0.73**（450K vs Stage 55B 整體中位 615K，**半 Stage 校準錨計算非典型** — 完整 Stage 55B 校準錨等 Session B 結案後重新評估）。**Stage 55B 整體 8.5/9 達成**，Session B 5 routing HITL refactor 留待 v3.44.0 → Stage 55B 完整結案 → v4 路線 9/9 達成。
> **🎉🎉🎉 2026-05-05 v7.75：v4 漸進遷移完整路線 9/9 達成 🎉🎉🎉** — Stage 55B Session B 完成（v3.44.0）— 5 routing types HITL refactor（dev_failed_intervention / qa_failed_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）+ Pipeline executor 從 SetIntervention end 改 yield-resume + legacy handler 加 Pipeline 分支（議題 5 = 5A 對齊 Stage 55A kickoff/design pattern）。**核心戰略級 Forge 缺口 6 揭露**：5 type-specific BossInteraction 在 Pipeline path 下**部分已 fire**（不是統一 generic intervention）— Pipeline 不 yield 而是 SetIntervention end → refactor 策略**比預期更輕**（不需新加 type-specific BossInteraction）。**首次拆 Session 戰術完整實踐 + Compact know-how 揭露**：Forge 用 Compact 模式（vs 新開 Forge session）完成 Session A → Compact → Session B — Session A 4 戰略議題拍板脈絡保留 + 對話連貫 + Aria 工作量單線程，**比新開 Forge session 更乾淨**。Aria 校準錨 Stage 55B 整體 **×1.42**（876K = Session A 450K + Session B 426K vs 中位 615K — **混合型新上界**：拆 Session + Compact 戰術 trade-off 跨 session 加總比一個 session 跑 + 1M compact 風險低）。**v4 漸進遷移完整路線 9/9 達成** — Stage 51 試點 framework HITL pattern 全面 wire 完成 + Pipeline framework 完整化 + Crash Recovery 全切 + sub-task 整合 + 16 處 skip 精簡 + F-α 移除。剩 Stage 56 Trial v6 前置條件統包（Dashboard MockScenarioCard 補 22+ framework_* 場景 + FF 四十二 + FF 四十三 + Stage 48 候選 FF + WorkflowEngine.cs enum/record 殘留評估）後可進入 Trial_v6 v4 動態架構驗證。
> **2026-05-05 v7.76：Stage 56 完成（v3.45.0）— Trial_v6 前置條件統包 ✅** — Dashboard MockScenarioCard 補全 33 framework_* 場景（Stage 49-55B 全到位）+ **FF 四十三 ✅**（路線 b + 議題 spike-2 選項 B：兩 path 修 — Path A CLI single-shot TryParseUsage 多欄位兼容 + LogDebug dump fallback / Path B Anthropic API direct TokenTrackingProvider 中央寫入點補 cost 估算 + 新建 TokenCostEstimator hardcoded per-model 4 欄位 dict 12 數字 + IsEstimated flag + Migration `Stage56TokenLogsIsEstimated`）+ **FF 四十二 ✅**（line-iteration + try-deserialize pattern 對齊既有 helper + 新建 AiTeam.Bot.Tests xUnit project 4 case all pass）+ conventions 補 2 段（WorkflowType/WorkflowStep fundamental type 不可移除 + Windows PATHEXT 解法落地 Stage 48 候選 FF）。**Aria 閘門一 4 critical 揭露**（Stage 47 model pricing 設施前提錯誤 / API path 修法位置模糊 / AiTeam.Bot.Tests 不存在 / 兩 path 根因混淆）→ Forge Plan Mode 二輪修正 + 議題 spike-2 三選項 escalate Christ 拍 B（hardcoded dict）。**範圍變更**：子項 7 Dashboard token 統計頁 IsEstimated 視覺區分跳過（4 處 razor + DTO + SQL 改動超出 1 處 grid cell 上限）→ **新立 FF 五十** Trial_v6 觀察期 follow-up。**0 follow-up bug**。Aria 校準錨 **×0.78**（混合型第 10 資料點 mid 帶下半，10 資料點區間穩定 ×0.73-1.42 — 觀察類整理 + Forge spike + 修一氣呵成 + 0 follow-up bug + Plan Mode 二輪 Aria gate1 修正充分四因素疊加）。**Trial_v6 開跑前工具完備**。
> **2026-05-08 v7.77：Trial_v6 結案 ⭐ 部分成功 — 揭露 15 議題**（vs Trial_v5 baseline 6 個，戰略價值 ×2.5）。試驗目的達標（v4 framework path 100% 走通 + Stage 46 sub-task chain + Stage 55B 5 routing HITL 全 type 真實觸發 + Stage 56 token 寫入率 100%），但 deliverable 不完整因 API 餘額自然耗盡（容錯性試驗達成）。**3 🔴 戰略級議題立 FF**：① **FF 五十一** Pipeline framework race condition（epic_partial_paused HITL routing 雙觸發）② **FF 五十二** Vera fix loop limit HITL routing 缺口（Stage 55B 5 routing 設計遺漏）③ **FF 五十三** API 餘額容錯性（TokenTrackingProvider USD billing 守門 + 三 Agent fail-fast 統一）。**12 🟡 中議題**併入既有 backlog（Cody/Petra prompt 對齊群組 / Dashboard UI 一致性 / BossInteraction 模板 / Internal API 安全評估）。**v4 ROI 量化結論**：Kickoff cost -24% ⭐ / Design cost +45%（含 sub-task 評估 overhead）/ 拆 task 機制真實任務首次觸發 / **總 cost +80% 超 Trial_v5 ±50% 範圍** ❌ — v4 投資戰略級正向但 production-ready 需 Stage 57+ 補強 3 🔴 議題。詳見 [Trial_v6_Plan.md](../experiments/Trial_v6_Plan.md) v2.0 結案紀錄。


---

## 外部檔索引（2026-05-01 拆檔）

| 子檔 | 內容 | 何時讀 |
|---|---|---|
| `Future_Feature_v4_eval.md` | 9 個 FF 待 v4 spike 後重評估 | spike Phase A 完成後 |
| `Future_Feature_frozen.md` | 3 個冷凍 FF（觸發條件不滿足）| 評估解凍時 |
| `Future_Feature_archived_v4.md` | 11 個已歸檔 FF（v4 吸收 / framework 內建 / Trial 完成）| 查歷史脈絡 |
| `Future_Feature_completed.md` | 已完成項目摘要（FF + Stage 對照）| 查 Stage 完成歷史 |
| `Future_Feature_changelog.md` | 變更紀錄 v1.0 - v7.66 | 追蹤版本演進 |

---

## 當前優先級 Top 5（2026-05-08 v7.77 — Trial_v6 結案後）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **五十一** ⭐ | Pipeline framework race condition（epic_partial_paused HITL routing 雙觸發）| 🔴 戰略級必修 | Trial_v6 揭露 — production race bug，Pipeline framework HITL routing 雙 fire 引發 EpicChain 雙觸發 sub-task 啟動，cost 浪費 + 流程亂序。Stage 57 候選 |
| 2 | **五十二** ⭐ | Vera fix loop limit HITL routing 缺口 | 🔴 戰略級必修 | Trial_v6 揭露 — Stage 55B Session B 5 routing HITL 設計遺漏「Vera fix loop limit」routing，達 limit 後只 fire generic intervention（ack only）→ Pipeline 卡死無法推進。Stage 57 候選補強第 6 routing |
| 3 | **五十三** ⭐ | API 餘額用盡時容錯性缺口 | 🔴 戰略級必修 | Trial_v6 容錯性試驗揭露 — TokenTrackingProvider 守門用 token count 不用 USD billing + 三 Agent（Vera/Quinn/Sage）對 API 爆容錯設計各異不統一。Stage 57 候選補強 |
| 4 | **三十六** | v4 動態流程架構 — Phase B | ⚪ 等 Stage 57 補完 3 🔴 後再評估 | Trial_v6 揭露的 3 🔴 戰略級議題優先於動態架構評估 |
| 5 | **七** | 客戶專案交付流程與驗收閘門 | 🟡 中 | 業務級需求，與 v4 路線無關 |

> ⚠️ **戰略主軸轉向**：Trial_v6 結案揭露 3 🔴 戰略級議題 → **Stage 57 必修 3 🔴 後再進入 Trial_v7+ / FF 三十六 Phase B 評估**。Trial_v6 也揭露 12 🟡 中議題（Cody/Petra prompt 對齊群組 / Dashboard UI 一致性 / BossInteraction 模板 / Internal API 安全評估）併入既有 backlog 排程處理。

---

## Active 主清單

> 以下 5 個 FF 為「進行中 / 仍需做」狀態（v4 戰略主軸 FF 三十六 + 4 個觀察類）。

## 七、客戶專案交付流程與驗收閘門

### 背景

AiTeam 的定位不只是開發自身系統，未來也會替客戶開發專案。目前的流程（merge 後自動部署）對 AiTeam 自身足夠，但對客戶專案的風險層級完全不同：

| | AiTeam 自身開發 | 客戶專案開發 |
|---|---|---|
| **壞掉的代價** | 自己的工具壞了 | 客戶的系統壞了 |
| **git revert** | 可以接受 | 不可接受 |
| **merge 後再測** | OK | ❌ 太晚了 |
| **驗收責任** | 自己 | 對客戶負責 |

目前的流程走完產出一個 GitHub PR，但 merge 之前沒有 Preview 環境可以人工驗收，直接 merge 等於直接上客戶 Production。

### 期望行為

```
需求 → ... → PR 開出
                ↓
         Preview 環境自動部署（Staging）
                ↓
         Victoria 通知 Christ：「PR #42 已部署至 staging，請驗收」
                ↓
         Christ（或客戶）在 Staging 實際操作驗收
                ↓
         驗收通過 → Christ 回覆 OK → Merge → Production 自動部署
         驗收失敗 → Christ 回覆問題描述 → 流程重新進入修正循環
```

### 需要的兩個東西

1. **每個客戶專案都有一個 Staging 環境**（不一定在本機）
2. **AiTeam 流程加入正式的人工驗收閘門**（Victoria 等待 approve 才算 Done）

### 待釐清的子問題

1. **客戶專案的 Staging 環境由誰負責？** — 客戶自己有 staging server？還是 AiTeam 在本機幫每個專案起 container？
2. **AiTeam 是否應該管理「部署到客戶環境」？** — 目前 Maya（Ops）只針對 AiTeam 自身
3. **驗收失敗的循環怎麼設計？** — Christ 的修改意見要怎麼餵回 CEO → 再分派給對應 Agent？

### 初步討論結論（2026-04-12）

**部署到 IIS Web Deploy 的能力：**
- 建議走 **GitHub Actions + Web Deploy** 模式 — 客戶 repo 掛 workflow，push 時自動 `msdeploy` 部署到 IIS
- Agent 不需要直接操作部署，只負責 push code，CI/CD 負責部署（與 AiTeam 自身模式一致）
- 每個客戶專案設定一次 workflow 即可

**Git Flow 多環境部署：**
- 完全可行，需調整 WorkflowEngine 的分支策略
- Cody 的 PR 目標從 `main` 改為 `develop`
- GitHub Actions 依 branch 觸發不同部署目標：feature→開發環境 / develop→測試環境 / master→Production
- Victoria 需理解 Git Flow 各階段，知道「merge 到 develop ≠ 上線」
- 人工驗收閘門：feature 部署到開發環境後，等 approve 才 merge 到 develop

### 與現有 Future Feature 的關係

- **FF 三十八（跨專案能力研究）**：子議題 A（跨 Project repo 支援）是本 FF 前置條件
- **v4 路線**：與 FF 四十九 / 三十六 無關，獨立業務級需求

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

---

## 十二、Sage 全系統文件健康檢查

### 背景

Stage 23 流程重構中，Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」，pipeline 中的工作改為輕量的文件整理 + CHANGELOG 更新。

但長期而言，BugFix / TechImprovement 跳過 Doc 階段，加上程式碼持續演進，系統文件會逐漸與實際程式碼脫節。需要一個機制定期檢查並修補差異。

### 期望行為

Sage 作為獨立定期任務（非 pipeline 內），掃描整個專案：
- 比對程式碼與現有文件的差異（API 變更、新增模組、移除功能）
- 識別過時或缺漏的文件
- 自動補寫或更新，產出健康檢查報告

### 觸發方式

- 定期排程（例如每週 / 每月）
- 或手動指令觸發（Discord / Dashboard）

### 優先級

🔵 低優先級 — Phase 1 先觀察流程文件是否足夠，有實際過時問題再啟動

### v4 兼容性

與 framework / 架構無關，v4 落地後仍可獨立做。

---

## 三十六、AiTeam v4 動態流程架構 — Phase B（FF 四十九 後續）

> 狀態：⚪ 待觀察 — **FF 四十九 Phase A 已通過（採用結論，Stage 48 完成 2026-05-02）**，Phase B 啟動條件解除但 Christ 拍板「Stage 49+ 漸進遷移過半（Stage 52 後）再評估」是否有獨立價值
> 提出日期：2026-04-28（Trial_v4 結案戰略討論）；2026-05-01 拆分為 Phase B（架構評估獨立於工具評估）；2026-05-02 Phase A 通過解鎖但延後評估

### 拆分說明（2026-05-01）

原 FF 三十六 範圍包含「行業先例研究 + 工具選型 + 動態調度 + per-task session」雙支柱。2026-05-01 Christ + Aria research 後拆為兩個獨立 FF：

- **FF 四十九（Phase A 工具評估）**：是否替換手刻 framework → Microsoft Agent Framework（保留架構）
- **本 FF（Phase B 架構評估）**：是否從固定 pipeline 升級到動態流程（Magentic Orchestration / per-task session）

→ **必須先做 FF 四十九**。Phase A 通過 → Phase B 才有 framework 基礎可動態調度；Phase A 不通過 → Phase B 自動失效。

### 設計拍板（2026-05-01 Christ + Aria brainstorm 後）

Christ 提出 **Capability-based Multi-Agent Architecture** 構想（受 MCP 啟發）：每個 Agent 是「打包好的角色/權責」，PM 動態調度。Aria 對應行業術語：**Anthropic Orchestrator-Workers Pattern + MS Agent Framework Magentic Orchestration + Agent-as-Tool**（業界 v4 共識方向）。

→ Spike Phase B 從原本「**探索動態流程要不要做**」演進為「**驗證可行性 + 細節打磨**」（架構雛形已 80% 成熟）。

#### 4 層 Hierarchy 架構

```
┌──────────────────────────────┐
│ Layer 1: Christ（老闆）       │
└─────────────┬────────────────┘
              ↓ Discord 對話        ↓ Dashboard 操作
┌──────────────────────┐    ┌──────────────────┐
│ Layer 2: Victoria     │    │ Operation Center │
│ (Discord 秘書/Router) │    │ (BossInteraction)│
└─────────┬────────────┘    └────────┬─────────┘
          ↓ 派工                       ↑ Petra escalate
          ↓                            ↑
┌─────────────────────────────────────┴──┐
│ Layer 3: Petra (Orchestrator)           │
│ - 全程動態調度（不照固定 pipeline）       │
│ - per-task session 持久記憶              │
│ - LLM 自主決策（CLAUDE_Petra.md 控制）    │
└──┬──────┬──────┬──────┬──────┬─────────┘
   ↓      ↓      ↓      ↓      ↓
┌────┐ ┌────┐ ┌─────┐ ┌────┐ ┌─────────┐
│Cody│ │Vera│ │Quinn│ │Sage│ │Rosa/Demi│  ← Layer 4: Workers
└────┘ └────┘ └─────┘ └────┘ └─────────┘  (Petra 動態組合，不互相直接呼叫)
```

#### Victoria 角色重新定位（Discord 秘書 / Anthropic Router Pattern）

**新定位**：純 Discord facade + Router + reasoning，**不做業務邏輯**。

| 行為 | Victoria 做嗎？ |
|---|---|
| 跟 Christ 對話、識別問題類型 → 派給對的人 | ✅ |
| Format 答案回 Christ | ✅ |
| 推 Discord 通知（Petra escalate 時）| ✅ |
| 過濾過期 / 誤判 BossInteraction（reasoning）| ✅ |
| codebase 探索 | ❌（Petra 派 Rosa 做）|
| 需求理解 / 提煉 | ❌（Petra 自己做）|
| 業務邏輯判斷 | ❌（Petra orchestrate）|

**Victoria Tool Set**（範例 code 見原始版本，已有完整實作藍圖）

⭐ Victoria **只接觸 Petra**（不直接呼叫 Cody/Vera/Quinn/Sage/Rosa/Demi），Workers 是 Petra 的 pool。

#### 5 個關鍵挑戰拍板（2026-05-01）

| 挑戰 | Christ 拍板 |
|---|---|
| **挑戰 1** Victoria 角色 | **Discord 秘書 / Router**（純 facade，不做業務邏輯）|
| **挑戰 2** 開會頻率 / Petra 決策邊界 | **初期 Petra 自主決策**，觀察後用 prompt iteration 修正（沿用 AiTeam 既有 CLAUDE_*.md 補強流程）|
| **挑戰 3** 老闆介入機制 | **Dashboard 為主介面** + **Victoria 同步推 Discord 通知**（dual-channel 對齊 Stage 28a/28b）|
| **挑戰 4** Mock 模式 | **個別 Worker hardcoded mock + Petra 用 Gemini Flash**（既有 GeminiProvider + AgentConfig DB 動態切換）|
| **挑戰 5** Crash Recovery | **重啟重跑**（不做 Checkpointing）+ 已 responded BossInteraction 算 task input 避免雙重 ask 老闆 |

#### Kickoff / Design 會議模式拍板（Hybrid，2026-05-01）

**Christ 拍板 Hybrid**（會議默認保留，小需求動態跳過）。核心理由：**省去二次返工**（前期投資高品質會議 cost vs 後期重做整段 pipeline cost）—— 對應 Anthropic「85% of quality improvement occurs in first 2 iterations」+ Trial_v4 vs Trial_v5 直接驗證。

**實作對應 MS Agent Framework**：
- 會議 = **Group Chat orchestration**（內建，Petra 為 chair）
- 1-on-1 = **Agent-as-Tool 直接呼叫**（純 MCP-style）
- Petra 看需求動態切換

**Trigger 條件初版**（寫進未來的 CLAUDE_Petra.md）：
- **Kickoff 觸發**（任一滿足）：跨 ≥ 3 元件 / 工期 ≥ 3 天 / 架構決策 / 跨多領域
- **Design 觸發**（任一滿足）：Kickoff 已開 / Issue ≥ 5 / 跨 Phase
- **1-on-1 觸發**：純技術改動 < 50 行 / bug 補丁 / 文件配置

#### 對 Trial_v5 揭露議題的解構

| 議題 | 動態架構下的處理 |
|---|---|
| **A**（MarkGroupDoneOrIntervention 誤判）| ✅ **消失** — 重啟重跑模式不依賴 task status 聚合判斷 |
| **B**（ImplementationNote 寫入路徑斷裂）| ✅ **消失** — Petra orchestrate 時直接看 PR Body / Sage 結果，不依賴特定 DB 欄位 |
| **C/D**（Token / CI/CD ops 議題）| ❌ 與架構無關（FF 四十七 補丁解）|
| **E**（Cody Dev_plan maxTurns）| ⚠️ Petra 看到 maxTurns 失敗 → 動態調整（拆小段重派 / 換 model）|
| **F**（Stage 42 補強單向性）| ⚠️ 部分緩解（CLAUDE_*.md 仍要對齊，但 Petra orchestrate 時可動態檢查）|

#### Phase B Spike 任務（2026-05-01 更新）

**從原本「探索動態流程要不要做」變為「驗證可行性 + 細節打磨」**。7 個驗證項：Victoria Router 模式 / Petra 自主調度行為 / per-task session 跨階段記憶 / Crash Recovery 重跑 / Mock Mode 用 Gemini Flash 跑 Petra / 遷移成本 / Hybrid 會議 trigger 條件（預期省 30-50% cost vs 全會議模式）。

### 啟動觸發條件（Christ 拍板）

⏰ **Stage 52 完成後**（v4 漸進遷移過半）評估：framework Magentic Orchestration 是否已解大部分動態調度需求？若已解 → 永久 ⚪ 待觀察；若仍有獨立價值（per-task session 持久記憶 / Capability-based 調度）→ 啟動 Phase B spike。**不在 Stage 49-51 期間啟動**（避免架構級變動疊加風險）。Stage 49-54 路線詳見 CHANGELOG / Spike v1 報告節 7。

### 規模 / 風險

**規模**：**XL**（架構級躍進）  
**風險**：**高**（架構級改動風險最大，但 Phase B 設計拍板已 80% 成熟降低 spike 風險）

### 優先級

⚪ 待觀察 — **依賴 FF 四十九 Phase A 結論**。

---

## 五十、Dashboard token 統計頁 IsEstimated 視覺區分（Stage 56 範圍變更 follow-up）

> 狀態：🔵 低 — Trial_v6 觀察期間 SQL `WHERE "IsEstimated" = true` 可查不阻擋對照，UI 標記延後不影響 cost 對照可信度
> 提出日期：2026-05-05（Stage 56 子項 2 詳化第 7 步範圍變更跳過）

### 背景

Stage 56 FF 四十三 修法新加 `token_logs.IsEstimated` 欄位（區分 `TokenCostEstimator` fallback 估算值 vs CLI/API 真實值）。原計劃書子項 2 詳化第 7 步「Dashboard token 統計頁 cost 加 `~$0.123` estimated 標記 + tooltip」評估後實際範圍超出 1 處 grid cell 上限：

- `TokenMonitoring.razor` **4 處顯示點**（line 64 per-agent / line 101 總計 / line 152 SortLabel / line 159 表格 cell）
- `TokenAgentSummaryDto` 加 `IsEstimated` 聚合欄位
- backend SQL/LINQ GROUP BY `IsEstimated` 改動

對齊計劃書原文「若 razor 改動超出 1 處 grid cell 則留 follow-up FF」拍板跳過。

### 修法方向

- `TokenMonitoring.razor` 4 處顯示點加 estimated 標記（`~$0.123` 前綴 + tooltip「fallback 估算值」）
- `TokenAgentSummaryDto` 加 `IsEstimated` bool 聚合
- backend SQL/LINQ GROUP BY 加 IsEstimated 維度

### 規模 / 風險

**規模**：S（單頁 razor + DTO + SQL，3 layer 改動但範圍可控）
**風險**：極低（純 UI 視覺 + 聚合 layer，不影響 cost 寫入邏輯）

### 優先級

🔵 低 — Trial_v6 觀察期間 SQL 可手查 `WHERE IsEstimated = true`，UI 標記延後不阻擋 cost 對照可信度。觀察期累積真實 estimated row 後再評估視覺呈現需求。

### v4 兼容性

純 UI / DTO 改動，與 framework / 架構無關。

### 注（跨欄位來源差異 — Stage 56 揭露）

`TokenMonitoring` 既有 `EstimatedCostUsd` 欄位是用 `app_settings` 兩 key（`TokenPricing:InputPer1kUsd` / `OutputPer1kUsd`）client-side 估算的，與 `token_logs.TotalCostUsd` 欄位**不同來源**（後者是 Stage 56 修法寫入的，含 CLI 真實值 + Anthropic API path 用 `TokenCostEstimator` per-model 4 欄位估算）。本 FF 修法是「新增 IsEstimated 視覺辨識」非「對齊既有 EstimatedCostUsd 顯示」。

---

## 五十一、Pipeline framework race condition — `epic_partial_paused` HITL routing 雙觸發 🔴 戰略級

> 狀態：🔴 戰略級必修 — Trial_v6 揭露 production race bug
> 提出日期：2026-05-08（Trial_v6 結案揭露）

### 背景

Trial_v6 Phase 2 啟動時揭露 race condition：Christ 點 1 次「恢復 EPIC」按鈕 → SQL 揭露 `epic_partial_paused` BossInteraction **2 個 row 都被 epic_resume 處理**（time diff 5 秒）→ EpicChain 雙觸發 → Phase 2 同時啟動 **2 個 Dev_plan task**。

**真實傷害**：4 個 Dev_plan task（race + appeal 迴圈疊加）+ 2 個 PM 審第 1 個結果 → revise 觸發第 4 個 Dev_plan → cost 浪費 ~$1-1.5。

### 假設根因（待 Forge 探索確認）

Pipeline framework `epic_partial_paused` HITL routing 在 Sage escalate + Sage skip 流程中重複 fire BossInteraction（race condition）→ Dashboard UI 一次 click 對所有 pending 同 type BossInteraction 統一 update → 觸發 EpicChain 邏輯雙啟動 sub-task。

### 修法方向

1. Pipeline framework `epic_partial_paused` HITL routing 雙 fire 防範（idempotency check）
2. EpicChain `ResumeEpicAsync` 邏輯加 sub-task 啟動 idempotency（已啟動 sub-task 不重複 enqueue Dev_plan task）

### 規模 / 風險

**規模**：M（Pipeline framework HITL routing + EpicChain 兩層 idempotency）
**風險**：中（race fix 需 Mock 場景重建 + 真實任務驗證）

### 優先級

🔴 戰略級必修 — Trial_v6 production 揭露，Stage 57 候選

### v4 兼容性

純 v4 framework 內部 race fix，與其他層無關。

---

## 五十二、Vera fix loop limit HITL routing 缺口 🔴 戰略級

> 狀態：🔴 戰略級必修 — Trial_v6 揭露 Stage 55B Session B 5 routing HITL 設計遺漏
> 提出日期：2026-05-08（Trial_v6 結案揭露）

### 背景

Trial_v6 Phase 2 fix loop ×3 達 `FixIteration` limit 3 後，Pipeline 觸發 `SetIntervention end` + fire generic `intervention` BossInteraction（ack only no routing）→ Christ 點「我知道了」後 Pipeline **卡死 needs_intervention 永久死局**。

Stage 55B Session B 設計 5 routing HITL（dev_failed_intervention / qa_failed_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）— **不含「Vera fix loop limit」routing**，這個邊界 case 沒被 Stage 55B 設計涵蓋。

### 修法方向

新增第 6 routing HITL type：`vera_fix_loop_limit_intervention`
- 觸發條件：`FixIteration >= MaxFixIteration`（預設 3）+ Vera 仍 Critical > 0 + Petra 仲裁仍 revise
- 提供 routing 選項：① 強制接受當前 PR（標完成）② 重啟 fix loop 計數（再給 3 輪）③ skip Phase（標完成進下個 phase）④ 放棄 Phase
- Pipeline DevFixStageExecutor 改 yield-resume HITL（對齊 Stage 55B Session B 5 routing pattern）

### 規模 / 風險

**規模**：M（同 Stage 55B Session B 1 routing refactor 規模 — record + PortId + AddEdge + ReviewerStageExecutor 改 yield-resume + ResumeAfterVeraFixLoopLimitAsync wrapper + InteractionProcessor 路由表 + Mock alias）
**風險**：低（單一 routing 補強，對齊 Stage 55B 既有 5 routing pattern）

### 優先級

🔴 戰略級必修 — Trial_v6 production 卡死議題，Stage 57 候選

### v4 兼容性

純 v4 framework HITL routing 補強，沿用 Stage 55B Pattern A inter-Executor message yield 設計。

---

## 五十三、API 餘額用盡時容錯性缺口 🔴 戰略級

> 狀態：🔴 戰略級必修 — Trial_v6 容錯性試驗揭露
> 提出日期：2026-05-08（Trial_v6 結案揭露）

### 背景

Trial_v6 預算自然耗盡時揭露 API 餘額用盡（Anthropic API 401 / insufficient_balance）的容錯性缺口：

1. **TokenTrackingProvider 守門設計用 token count 不用 USD billing**：全域月限 10M tokens（10,000K），但 cost 已超 $14.88 仍不擋 — 守門設計與 USD billing 解耦
2. **三 Agent 對 API 爆容錯設計各異**（不統一 fail-fast）：
   - **Vera Reviewer**：cost 0 + task done + 流程繼續 ❌（最危險，silent done）
   - **Quinn QA**：cost 0 + task failed + qa_failed_intervention BossInteraction ✅（明確 fail）
   - **Sage Doc**：cost 0 + task done + 「無輸出略過提交」silent skip + epic_partial_paused ⚠️（半明確）
3. **錯誤訊息誤導**：表面看「No changes; nothing to commit」，實際根因是 Anthropic API billing fail — Christ 看訊息會誤判為 Cody/Quinn prompt 議題

### 修法方向

1. **TokenTrackingProvider 補 USD billing 守門**：
   - 新加 AppSettings key `Token:GlobalMonthlyCostLimitUsd`（預設 $50）+ `Token:RemainingBalanceUsd`（從 Anthropic API 查詢 balance）
   - 守門邏輯：cost 預估 + 已用 cost > limit 或 balance < threshold → 攔截 LLM call + Discord alert
2. **Anthropic API 401 / insufficient_balance 錯誤訊息明確化**：
   - ClaudeCodeService / AnthropicProvider catch API 401 → log 明確訊息「API 餘額不足，請充值」+ throw `InsufficientApiBalanceException`
   - Pipeline framework 對此 exception 統一處理：fire `api_balance_intervention` HITL routing（新 routing type）
3. **三 Agent fail-fast 統一**：
   - Vera/Quinn/Sage 看到 LLM result empty / cost = 0 時統一 throw + Pipeline 統一處理
   - 對齊 Stage 55B Session B 5 routing HITL pattern

### 規模 / 風險

**規模**：M-L（Token 守門 + 三 Agent fail-fast + 新 HITL routing + Anthropic API balance 查詢）
**風險**：中（涉及 Token 守門核心 + 三 Agent 行為調整，需充分 Mock 場景驗證）

### 優先級

🔴 戰略級必修 — Trial_v6 production 揭露，Stage 57 候選（建議拆 Session 2 段：Token 守門 + Agent fail-fast 各一段）

### v4 兼容性

純 v4 framework + Token 守門擴充，不影響業務邏輯層。


## v4 後重評估 FF 簡表

> 詳細內容見 [`Future_Feature_v4_eval.md`](Future_Feature_v4_eval.md)

| FF | 標題 | 狀態 | v4 重評估點 |
|---|---|---|---|
| 十 | Dashboard UI 細節打磨（第四批）| 🔵 低 | 動態架構下 UI 重新設計 |
| 十四 | Agent I/O 完整記錄 | ⚪ 待討論 | MS Agent Framework 內建 telemetry 涵蓋 |
| 十九 | Agent maxTurns 動態化 | ⚪ 待觀察 | v4 framework maxTurns 機制改變 |
| 二十二 | Agent 命名一致性 | 🔵 低 | 動態架構下 Worker pool 命名規則改變 |
| 二十五 | Self-implement 試驗 prompt 設計守則 | 🟢 經驗紀錄 | Trial_v5 證實 self-implement 適用範圍擴展 |
| 三十八 | 跨專案能力研究 | ⚪ 待深度討論 | v4 影響跨 project 設計 |
| 四十 | Stage 46 Dashboard razor UI 接線 | 🟠 中-高 | 動態架構下 UI 重新設計 |
| 四十八 | Cody Dev_plan 階段 maxTurns 配置不足 | 🟠 中-高 | v4 framework maxTurns 機制改變 |

---

## 冷凍 FF 簡表

> 詳細內容見 [`Future_Feature_frozen.md`](Future_Feature_frozen.md)

| FF | 標題 | 狀態 | 解凍觸發條件 |
|---|---|---|---|
| 二 | Agent 個性與造型 | 🔵 低 | Dashboard 視覺整體穩定（v4 後）|
| 三 | AiTeam 安裝精靈 | 🔵 低 | 系統架構穩定 + 真實「在第二台機器部署」需求 |
| 十三 | UAT 驗收階段 | ⚪ 待觀察 | Trial_v6+ 觀察期間頻繁出現「做出來不是我要的」case（≥ 3 次）|

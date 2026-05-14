# Future Feature — 未來功能候選清單

> 版本：v7.95
> 建立日期：2026-04-01
> 最後更新：2026-05-14
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
> **2026-05-14 v7.95：Trial_v11 結案 🟡 部分成功 — 業務級重現 ✓ + Stage 65 4 議題 2 完整收口 1 半 1 復發 + 揭 3 🟡 新議題（不切 default flag → Stage 66 補強 → Trial_v12）** — Aria 全程自跑 9-step 模板第 2 次實踐穩定 / Christ 拍板 OK 跑後 0 操作 / Monitor tool 第一次用於 Bot log milestone 監控成功。**真實 lifecycle ~8 分 23 秒** + 真實 cost **$1.381**（vs Trial_v10 $1.07 +29%）+ **真實 PR [#373](https://github.com/darkleong/AiTeam/pull/373) 開出**（8 個 Dashboard 檔 +236/-53）。**Stage 65 4 議題收口效果**：① CLAUDE.md 不污染 ✅ 完整收口（PR diff 0 含 CLAUDE.md / `--append-system-prompt` 修根因路徑真實生效）② Vera token_logs ⚠️ 半收口（finally 紀律寫入 ✓ 但 Vera 0 token 0 cost 內容異常 — 連帶揭新議題 1）③ workspace permission ✅ 完整收口（owner=appuser / 0 手動 chown）④ Cody 5/5 cover ❌ 議題復發（4/5 漏 InteractionCenter 連兩 Trial / PR body 0 含「範圍對照表」段紀律未生效）。**揭 3 🟡 新工程議題 0 🔴 戰略級新類型**：① **Vera dispatch 0 work**（最高優先業務級）— Vera 接到 dispatch 但 subprocess 0 token 0 cost「多 worker chain」實質單 worker ② **PetraSessionMessages 缺 tool role** — adapter dispatch 結果沒回寫 messages 連帶 PR body Worker summary 段空 ③ **Cody InteractionCenter 連兩 Trial 漏** — CLAUDE_Cody.md 紀律強度不夠。**Petra DecideAsync ✓ 連兩 Trial 命中**（Stage 64 計劃前 WebSearch「framework 自動 chain」結論第二次實證對齊）。**業務品質評估 60-65%**（Toast 通知核心 ✓ + 4/5 範圍 + Vera 0 work = 0 真實 review pass）。**對戰略大重評估的關鍵實證升級**：① v5 動態架構業務級成功可重現連續 2 Trial（PR #372 + #373 都真實 deliver）= infinite loop pattern 確認打破 ② **但「多 worker chain」實質單 worker（Vera 0 work）= production 切 default flag 後 review path 0 保護** ⚠️ ③ 不切 default flag → Stage 66 補強 3 🟡 議題（Vera 0 work 最高優先）+ Trial_v12 重驗 ④ 0 🔴 戰略級新類型維持 = v5 設計層仍對 / 路線 D 採用拍板不需翻盤。**連續 6 Trial 議題密度進化曲線繼續**（v6 15 → v7 5 → v8 5 → v9 12+ → v10 4 → **v11 3 + 議題 2 完整收口**）= 系統成熟度進化曲線。**FF 動態**：FF 三十六 status 維持「Charter+spike+PoC+Stage 64+Stage 65 ✅ + Trial_v10 業務級成功 ✅ + Trial_v11 部分成功 → Stage 66 補強 3 🟡 → Trial_v12 重驗 → 通過才切 default flag」/ FF 六十一補強清單擴 Trial_v11 3 議題 → 共 11+ 點（Stage 65 已收 4 / Stage 66 候選 3 / 剩 8+ Stage 67+）。**Top 5 重排**：Stage 66 升 **#1**（3 🟡 議題收口 — Vera 0 work 最高優先）/ Trial_v12 #2（Stage 66 結案後重驗）/ FF 三十六 #3（Trial_v12 ✅ 後才切 default flag = v5 正式上線）/ FF 五十四 #4 / 保留群組 #5。**Aria 工作節奏觀察**：① Aria 全程自跑 9-step 模板第 2 次實踐穩定（7 工具熟練度高 / Monitor tool 第一次用 + 三 milestone alternation 含 failure signatures 全捕到）② Aria 主動切 Christ 對話 register（Trial 進度報告白話 + 結論直接給定見「不切 default flag」對齊 Trial_v9-v10 期間紀律累積）③ Vera 0 work 揭露對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第 N 次驗證（Stage 65 子項 2 Mock 驗 finally 紀律 ✓ 但 Mock 沒驗 Vera subprocess 真實有做事）。**Aria 校準錨待 Christ 補 Aria session context 數字**。**戰略主軸**：Trial_v11 業務級重現 ✅ + 4 議題 2 完整收口 + Vera 0 work 揭新業務級議題 → Stage 66 補強 3 🟡 → Trial_v12 重驗 → 通過 → Christ 拍板切 default flag = v5 動態架構正式上線。詳見 [Trial_v11_Plan.md](../experiments/Trial_v11_Plan.md) v2.0 結案紀錄。
> **2026-05-14 v7.94：Stage 65 ✅ 完成（v3.55.0）— v5 動態架構 Trial_v10 揭 4 🟡 議題收口 + 結案後 merge feature/v5-poc → main（production-ready 補強第二波 = v5 上線前最後一個工程 Stage）** — 4 子項一氣呵成 + Aria gate1 Tier 0+1 第二次實踐 + Forge 自驗 Mock 全綠（dotnet test 168/168）+ 0 v4 regression / feature flag default false 維持 production 0 影響。**Stage 65 計劃前 WebSearch 紀律第二次實踐生效**（議題 1 從 Stage 64「補丁修時序漏洞」升級為「修根因移除 ritual 改用 Claude Code CLI `--append-system-prompt`」對齊「修根因 > 補丁」哲學）。**4 子項**：CLAUDE.md inject 修根因 + Vera token_logs 移 finally + workspace volume permission compose entrypoint chown + Cody 廣範圍指令紀律。**Forge healthy 偏離 plan 三處全保 v4 0 regression**：systemPrompt 放 ct **之後**（揭 Aria reference 既有 pattern 憑印象沒 grep caller positional args 順序的同類根因第六次累積修根因 → workflow_aria.md 第三節 A 第 7 條延伸範圍段 #3 升級）+ ClaudeCodeProxy.cs 連動（interface 既有實作類漏列健康自補）+ Dockerfile gosu install 配套。**Stage 65 結案後 merge feature/v5-poc → main 拍板**（Christ 2026-05-14 — Trial_v11 在 main 跑驗證 + Directory.Build.props v3.52.0 → v3.55.0 一次性 bump 跳 v3.53.0/v3.54.0）。**Aria 校準錨 ×0.79**（297K vs 預估 mid 375K — **跟 Stage 64 連續兩次 ×0.79 同倍率穩定** = 紀律累積成熟度進化曲線實證 + production-ready 補強區間 ×0.78-0.99 完整驗證 6 資料點 Stage 56/58/60/61/64/65）。**結案第二段 step 0 升級 1 處**：workflow_aria.md 第三節 A 第 7 條延伸範圍段 #3（Stage 65 揭兩條同類根因第六次累積修根因 — Aria reference 既有 method 簽名 / pattern 必 grep 既有 caller positional args 順序 + grep interface 實作類完整列出）。**FF 動態**：FF 三十六 status 升「Charter+spike+PoC+Stage 64+Stage 65 ✅ + Trial_v11 待」/ FF 六十一 4 議題收口完成（CLAUDE.md inject + Vera token_logs + volume permission + Cody 廣範圍紀律）— FF 六十一剩 8+ 點 Stage 66+ 評估。**Top 5 重排**：Trial_v11 升 **#1**（Stage 65 結案後 main 直接驗證 — feature flag 切 true 即可）/ FF 三十六 #2（Trial_v11 ✅ 後 merge to main 已完成 + production 切 default flag = v5 正式上線）/ Stage 66+ FF 六十一其餘 8+ 點 #3（Trial_v11 後評估）/ FF 五十四 #4 / 保留群組 #5。**戰略主軸**：Stage 65 Mock 全綠 + merged main ✅ → Trial_v11 真實 production-readiness 驗證 → 通過 → Christ 拍板切 default flag = v5 動態架構正式上線。詳見 [Stage_65_Roadmap.md](Stage_65_Roadmap.md) v2.0 + [CHANGELOG v3.55.0](../../CHANGELOG.md#3550)。
> **2026-05-13 v7.93：Trial_v10 結案 ⭐⭐⭐ 業務級成功（vs Trial_v9 業務級失敗）+ 路線 D 採用實證充分** — Stage 64 v5 production-ready 收口後第一次真實任務試驗。**Aria 全程自跑試驗**（Christ 拍板 OK 跑後 0 操作 — `gh workflow run` deploy + docker / SQL / Bot log scan / PR 內容檢查 + 業務品質評估初稿全 Aria — 對齊「Christ 只動嘴」精神再進一步突破）。**真實 lifecycle ~6 分鐘**（PetraSession `b322d06d` 跑 15:27→15:35）+ 真實 cost **$1.07**（餘額 $35.66 → $34.59 / vs Trial_v6 baseline $15.81 便宜 -93%）+ **真實 PR #372 開出**（7 個 Dashboard 檔 +167/-181 行 / 對比 Trial_v9 5 次 0 commit / 0 PR 完全突破）。**核心問題全解決**：Cody 真實 deliver + Petra Design trigger 2 cap 命中 Cody → Vera 多 worker chain 真實 fire + CLAUDE.md inject ritual 兩 worker 真實生效 + CloneOrPull wire 真實生效（Aria 必修 2）。**揭 4 議題 0 🔴 戰略級 + 4 🟡 工程細節**：① CLAUDE.md 污染 commit（FinalizeGitAsync vs Vera adapter restore 時序漏洞）② Vera token_logs 沒寫（$0.044 blind spot — 真實餘額 $1.07 vs 系統 $1.026 差 4.3%）③ named volume permission（Stage 64 docker-compose 漏 chown — Aria 手動 fix 才跑）④ 業務範圍 cover 4/5（漏 InteractionCenter 操作中心）。**Q2 嫌疑推翻**：Petra 從 Stage 63B 起就回 4 cap，真實問題不是 prompt 而是 framework chain dispatch — Stage 64 Petra prompt 升級不影響 chain，Stage 64 計劃前 WebSearch 紀律「framework 自動 chain」實證對齊。**連續 5 Trial 累積 lifecycle visibility 進化**（v6 15 議題 → v10 4 議題 — 議題密度下降 + 戰略級減少 = 系統成熟度進化曲線）。**Aria 自省點候選 #33**（待 session 結束評估）：Aria 全程自跑試驗對「Christ 只動嘴」精神更進一步突破。**FF 動態**：FF 三十六 status 升「Charter+spike+PoC+Stage 64+Trial_v10 業務級成功 ✅ + Stage 65 4 🟡 議題收口 → Trial_v11 確認 → merge to main」/ FF 六十一補強清單擴 Trial_v10 4 議題 → 共 12+ 點留 Stage 65 範圍。**Top 5 重排**：Stage 65 升 **#1**（4 🟡 議題收口）/ Trial_v11 #2（Stage 65 結案後重跑驗證）/ FF 三十六 #3（Trial_v11 ✅ 後 merge to main + production 切 default flag）/ FF 五十四 #4 / 保留群組 #5。**Aria 校準錨待 Christ 補 Aria session context 數字**。**戰略主軸**：路線 D 採用實證充分 → Stage 65 4 🟡 議題收口 → Trial_v11 確認 → merge to main + production 切 default flag = v5 動態架構正式上線。詳見 [Trial_v10_Plan.md](../experiments/Trial_v10_Plan.md) v2.0 結案紀錄。
> **2026-05-13 v7.92：Stage 64 ✅ 完成（v3.54.0）— v5 動態架構 production-ready 收口（路線 D 採用拍板首發實作）** — 8 子項一氣呵成 + Aria gate1 雙必修點收口 + Forge 自驗 Mock 全綠 168 tests PASS / feature/v5-poc branch + feature flag default false v4 production 0 影響。**路線 D 採用拍板首發**（Christ 2026-05-13 Trial_v9 結案 + Aria 跨輪 WebSearch + code trace 雙確認後）— Trial_v9 揭 12+ 議題經兩議題 WebSearch（Q1 ChatClientAgent.instructions 語意 / Q2 BuildSequential dispatch protocol）+ trace v4 既有 CLAUDE.md inject ritual 後**工作量明顯下修**：Q1 從「設計級不確定」變「工程級明確」（v5 PoC adapter 漏接 v4 既有 inject 儀式）+ Q2 從「需要 spike framework」變「PoC Petra prompt 嫌疑 + DB 驗證」（official sequential orchestration sample 證實 framework 自動 chain）→ Stage 64+ 預估從「大投資」下修為「M 級工程收口」。**8 子項**：CLAUDE.md inject ritual + git commit/push/PR + Petra prompt 升級 + workspace volume mount + Gemini v1beta default + token_logs null-safe + transient retry + chain Mock 驗證 + CloneOrPull wire（Aria 必修 2）+ Stage 63A spike notes errata（限制 (b) 真實 root cause = 漏 TurnToken trigger）。**新立紀律 2 條**：① **計劃前置 WebSearch 複檢紀律**（workflow_aria.md 第三節 A 第 9 條 + aria-plan/aria-gate1 skill — Stage 64 計劃前 WebSearch 兩議題揭 Stage 63A 誤判 + 工作量下修首次實證）② **Gate1 Tier 0/1/2/3 分級紀律**（workflow_aria.md 第三節 D + aria-gate1 skill）。**範圍邊界守住**：feature flag default false 維持 / feature/v5-poc 累積 production-ready commits / **Directory.Build.props main 仍 v3.52.0 不動**（對齊 Stage 63B 模式 — Trial_v10 驗證通過後另排 merge 計畫）/ 範圍變更紀錄 0（8 changed files 8/8 對齊 plan Critical Files）。**Forge 自驗物理限制範疇明寫留 Trial_v10**：場景 2/4/6/7/8 真實 git wire / Anthropic 5xx retry / 多 worker chain / workspace 持久化 / Gemini endpoint — 全留 Trial_v10 真實任務驗收。**FF 動態**：FF 三十六 status 維持「Charter ✅ + API spike ✅ + PoC ✅ + Trial_v9 揭 12+ 議題 → Stage 64 production-ready 收口 ✅ + Trial_v10 待」/ FF 六十一補強清單擴 Stage 64 收口 4 點（CLAUDE.md inject / git wire / workspace volume + clone wire / Gemini default）→ 8+ 點留 Stage 65+ 全量遷移評估動工。**Aria 校準錨 待 Christ 補 Forge context 數字**（規模 M 預估 mid 350-400K）。**Top 5 重排**：Trial_v10 升 **#1**（啟動條件達成 — feature/v5-poc 完整 production-ready 工程 + 5 場景留真實任務驗收）/ FF 三十六 #2（待 Trial_v10 結果 = 路線 D 實證）/ 戰略大重評估 #3（Trial_v10 結案後再確認路線 D 採用 vs 回頭評估）/ FF 五十四 #4 / 保留群組 #5。**戰略主軸**：Stage 64 Mock 全綠 ✅ → Trial_v10 啟動條件達成 → 路線 D 真實任務 ROI 實證 vs Christ 拍板下一步（merge to main / 繼續 Stage 65+ 補強 / 回頭評估）。詳見 [Stage_64_Roadmap.md](Stage_64_Roadmap.md) v2.0 + [CHANGELOG v3.54.0](../../CHANGELOG.md#3540)。
> **2026-05-13 v7.91：Trial_v9 結案 ⭐ 戰略級成功 vs 業務級失敗（雙面，第 4 次連續 Trial）— v5 真實 work 證實 + production-ready 工作量量化 = Stage 64+ 全量遷移大投資 + 戰略大重評估時機到** — Trial_v9 跨 2 日 9 PetraSession 累積 5 fix（TurnToken 漏 / workingDir config / workspace init / token_logs 寫入 / nullable）+ 真實 cost ~$0.63（vs 預期 $5-15 偏低 -90% — 0 deliverable 模式）+ 最終跑通 PetraSession 0228e131 5:19 min Cody success 但 **0 deliverable**（workspace clean / 0 commit / 0 PR — Cody 跑 5 min 花 $0.57 完全沒做事）。**真實 root cause** — `PetraWorkerHelper.BuildAgent` Worker instructions 只一行 generic（「你是 Cody — Code Implementation Worker。負責依任務 input 寫程式碼。」），對比 v4 既有 production Worker prompt（CLAUDE.md 注入 / git workflow / branch / commit / push 完整 production-grade）— v5 PoC 沒對齊 → Cody 純 reasoning 沒做事 exit success。**揭 12+ 議題**（連續 9 PetraSession 累積）：Gemini API key / Gemini v1 API systemInstruction / TurnToken / workingDir config key / workspace 不持久 / Stage 63A spike limitation (b) 誤判 / token_logs fail path / AgentResponseUpdateEvent / Anthropic API 5xx transient / BuildSequential 4 worker chain 仍只 1 dispatch / Worker prompt 太簡 → 0 deliverable / etc。**戰略級成功**：v5 真實 work 證實（技術鏈完整 fire）+ Anthropic API 5xx confirmed transient（重試 OK）+ Stage 63A 「framework limitation (b)」誤判 root cause 揭真實（漏 TurnToken trigger — Charter 02/04 + spike notes errata 紀錄改寫）。**業務級失敗**：「能跑」≠「能 deliver」精準揭差距 — 5 次跑 0 deliverable / production-grade 工作量明確。**對戰略大重評估的關鍵實證升級**：① v5 不是設計問題（架構真實 work）② Production-ready 工作量 = Stage 64+ 全量遷移大投資（8 prompt 重寫 + framework dispatch protocol 解 + workspace 持久化 + retry path + 9+ FF 六十一補強整套）③ 連續 4 Trial 揭 18+ 🔴 + 第 4 次 0 deliverable = infinite loop pattern 真實實證 ④ Christ 拍板路線 D vs A vs B vs C 的 ROI 評估資料**充分**。**Aria 主動於 Trial_v9 結案後提醒戰略大重評估深度討論**（Christ 2026-05-10 拍板對齊精神延續）。**FF 動態**：FF 六十一補強清單擴 4 點 → 12+ 點完整 production-grade 議題清單（留 Stage 64+ 路線 D 採用才動工）。**Aria 自省點候選 #32**（Trial 紀律延伸）：Trial 修補連續 5 次後 cost / 戰略邊際遞減訊號明顯 → Aria 主動推薦結案進戰略大重評估比繼續修補有更高戰略價值。**Top 5 重排**：戰略大重評估升 **#1**（Trial_v9 結案後關鍵時機 — Christ 拍板路線 A vs B vs C vs D）/ FF 三十六 status 維持「Charter ✅ + API spike ✅ + PoC ✅ + Trial 揭 12+ 議題 → 待戰略大重評估拍板才決 Stage 64+ 啟動」/ FF 六十一 #3（Stage 64+ 才動工）/ FF 五十四 #4 / 保留群組 #5。**Aria 校準錨待 Christ 補 Aria session context**。詳見 [Trial_v9_Plan.md](../experiments/Trial_v9_Plan.md) v2.0 結案紀錄。
> **2026-05-12 v7.90：立 FF 六十一 — v5 PoC → production-ready simplification 補強清單**（Stage 63B Aria spot check 揭露 — Stage 64+ 處理）— Christ question「Context 偏低 ×0.49 是否該做沒做」拍板 Aria spot check 後揭 30% 隱性 production simplification（4 點）：① 🔴 PetraOrchestratorService.StartAsync 端對端 xUnit 漏測（7 test 真實覆蓋組件單元 / 沒測 BuildSequential + InProcessExecution + ChatClientAgent + adapter 完整 chain — 「組件單元測試全綠 ≠ 端對端整合測試全綠」對齊 Stage 60 自省點 #25 第 N 次精神延伸）② 🟡 ResumeAsync「PoC 簡化：mark 既有 session done + 開新 session」not「同 session 繼續」③ 🟡 BuildSessionContext fallback hardcode `Agents:Dev:Model` 給 7 Worker 共用（沒走 per-Worker AgentConfig DB Stage 38 pattern）④ 🟡 PetraSessionRepository.AppendMessage 同步 method（既有 BossInteractionRepository 是 async）。**全 Mock 階段足夠 / production 階段需要** — Stage 64+ 全量遷移時補。**Aria gate1 補強紀律候選不立自省點**（第一次資料點不夠 — 等 Stage 64+ 同類型再驗）：gate1 commit 檢查考慮加「spot check 1-2 個 critical service production-ready vs Mock 階段簡化」。**FF 三十六 status 不變**（Charter ✅ + API spike ✅ + PoC ✅ + Trial 待 Trial_v9）/ FF 六十一 standby ⚪ 不進 Top 5。詳見 [Future_Feature.md FF 六十一](Future_Feature.md#六十一v5-poc--production-ready-simplification-補強清單stage-63b-aria-spot-check-揭露--stage-64-處理)。
> **2026-05-12 v7.89：Stage 63B ✅ 完成（v3.53.0）— FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠** — 9 子項全部完成 / feature/v5-poc branch + main 0 動 + feature flag default=false v4 production 0 影響 / dotnet test 153 passed（+19 new Petra test）。**路線 A 拍板對應實作**（對齊 Stage 63A spike 已驗 path + framework 投資保留）：限制 (a) workaround 自寫 PetraOrchestratorService.DecideAsync + BuildSequential + InProcessExecution events 訂閱 + 限制 (b) workaround Worker.CreateAgent factory pattern 包 ChatClientAgent + ClaudeCodeChatClientAdapter : IChatClient（三層 wrapper 真實生效 / 7 capability dispatch IClaudeCodeService 7 真實 method）。**12 新建檔**：PetraSession + PetraSessionMessage entity + Repository + EF Migration Stage63PetraSessionTables + IAgentTool factory pattern + AgentCapabilityAttribute + PetraSessionContext + ClaudeCodeChatClientAdapter + PetraOrchestratorService + PetraOrchestratorResult + PetraSessionRecoveryService hosted service + xUnit 26 case。**修改檔**：AppDbContext +25 / WorkflowSettings +7 / Resolver +5 / CeoAgentService +15（flag forward）/ 7 Worker 各 +14-15（IAgentTool factory）/ Program.cs +15（DI multi-registration）/ CLAUDE_Petra.md 全砍 -206+255（FF 五十九 hand-off v5 PoC 期間紀律段）/ 7 partial CLAUDE_*.md 各 +2（FF 五十九 hand-off only — Victoria +7）/ Directory.Build.props v3.53.0。**FF 動態**：FF 三十六 status 升「🟡 進行中 Charter ✅ + API spike ✅ + PoC ✅ + Trial 待 Trial_v9」+ FF 五十九 hand-off ✅ 落實到 CLAUDE_Petra.md 全砍重寫開頭 + 7 partial 各 +2 行。**Aria gate1 第二輪通過放行**（v1.1 修正三點全完整：critical 路線 A + important Victoria 簡化 + nice-to-have xUnit only）+ **2 點 healthy 範圍變更**：① 7 partial CLAUDE_*.md 簡化為 FF 五十九 hand-off only（Mock 階段足夠 / 完整 partial 重寫留 Stage 64+ 全量遷移） ② Forge 揭 PetraWorkerHelper.cs 抽 helper 對齊 Stage 36+ refactor-sop 精神。**Charter 04 inconsistency 揭露補釘**（Aria grep source of truth 紀律）：「9 Worker」實際 7 個 AgentService / 「RunWriteAsync」實際 `RunAsync`。**dotnet test baseline 漂移 134 → 153**（+19 new Petra test — 未來新 Stage 對齊本 entry 紀錄）。**Aria 校準錨 待 Christ 補 Forge context 數字**（規模 L 預估 mid 750K — 對齊 production-ready 補強 4 Stage 區間 ×0.78-0.99 mid 中段 + Stage 63A 早期 derisk 範圍可控）。**Top 5 重排**：Trial_v9 升 #1（啟動條件達成 — 建議 Christ 儲值 ≥ $30 buffer 才開跑）/ FF 三十六 #2（Charter ✅ + API spike ✅ + PoC ✅ + Trial 待）/ FF 五十四 #3 / 戰略大重評估 #4（Trial_v9 結案後拍板路線 A vs B vs C vs D）/ 保留群組 #5。**戰略主軸**：Stage 63B PoC 架構基底 ✅ Mock 全綠 → Trial_v9 啟動條件達成（feature/v5-poc branch + 5 向對照 + 7 驗證項 #4 Crash Recovery + #6 遷移成本量化 真實任務驗證 + LLM cost ~$5-15 / 建議 Christ 儲值至 ≥ $30 buffer）→ Trial_v9 結案後 Christ 拍板戰略大重評估關鍵實證。詳見 [Stage_63B_Roadmap.md](Stage_63B_Roadmap.md) v2.0 + [CHANGELOG v3.53.0](../../CHANGELOG.md#3530)。
> **2026-05-11 v7.88：Stage 63A ✅ 完成（v3.52.0）— FF 三十六 Phase B 動態決策 API spike ✅ 硬通過 + 揭 2 framework limitation**（Stage 63B 戰略級早期 derisk）— Stage 62→63 拆 Stage 模式延伸到 63A→63B（廉價先驗驗證項 #2 unknown 過了才 commit 63B 大投資）。**✅ 硬通過**（Christ AI Studio Gemini 2.5 Flash key 真打 3 場景 trigger 命中率 100% / 真實 cost $0 免費 tier + 1 次 503 retry success）：① 修 typo → `Cody`（1 agent）→ 1-on-1 trigger ② 跨 5 元件 → `Cody → Vera`（2 agents）→ Design trigger ③ 架構級重構 → `Cody → Vera → Cody → Vera`（4 agents 多輪）→ Kickoff trigger。**核心 finding**：Charter 候選 `MagenticOrchestrator<TState>` **不存在於 nuget 1.3.0** — 動態決策真實 hook = `GroupChatManager.SelectNextAgentAsync` override + `AgentWorkflowBuilder.CreateGroupChatBuilderWith` + `HandoffWorkflowBuilder` 替代 pattern + `AIAgentExtensions.AsAIFunction` Worker-as-Tool 真實 API。**驗證項 #2 失敗條件未命中**（命名落差 ≠ 失敗 — Charter 用 errata 補釘到 spike notes）。**⚠️ 2 framework limitation 揭露**（戰略級對 Stage 63B）：① **Limitation (a)** base `GroupChatManager` subclass 不啟動 manager loop → Stage 63B 走候選 (B) 自寫 PetraOrchestratorService + BuildSequential ② **Limitation (b)** base `AIAgent` subclass 不被 framework workflow dispatch → Stage 63B 必走 `ChatClientAgent(IChatClient, ...)` ctor + 新建 `ClaudeCodeChatClientAdapter : IChatClient`（從「可選」升「必走」）。**範圍變更接受 +13%**：prototype 113 行 throwaway（超 100 行上限 13%）— 揭 framework limitation value 巨大早期 derisk。**5 deliverable**：PetraSpikePrototype.cs 113 行 + PetraSpikePrototypeTests.cs 3 場景 + 05_Stage_63A_Spike_Notes.md 5 段（含實測 log + 2 framework limitation + 結論升「硬通過」）+ 04_Stage_63_PoC_Roadmap_Draft.md errata 子項 1+4（候選 (B) / IChatClient adapter 升「必走」）+ Directory.Build.props v3.52.0。**Christ 拍板補充**：EF Migration 路線 **(c) in-memory session**。**FF 動態**：FF 三十六 status 升「🟡 進行中 Phase B Charter ✅ + API spike ✅ + PoC 待 Stage 63B」+ FF 五十九 落實 hand-off 紀錄到 spike notes 第 5 段。**Aria gate0 揭露 3 點 Forge 全收** + **Forge spike 新增第 6 自決點**（跨 assembly protected override CS0507 修正）。**dotnet test baseline 漂移 131 → 134**（3 spike test silently pass 無 GEMINI key / 真打 PASS）。**Aria 校準錨 待 Christ 補 Forge context 數字**（spike 規模 S 混合型 — 預估 ~200-300K）。**Top 5 重排**：Stage 63B PoC 升 #1（啟動條件達成 — 待 Christ 拍板開跑）/ FF 三十六 #2 / FF 五十四 #3 / 戰略大重評估 #4 / 保留群組 #5。**戰略主軸**：Stage 63A spike ✅ **硬通過 + 2 framework limitation 戰略級早期 derisk** → Stage 63B PoC 啟動條件達成（feature/v5-poc branch + 候選 (B) 自寫 orchestrator + ClaudeCodeChatClientAdapter + 6 子項全量實作 + Trial_v9 5 向對照 / 規模 L / cost ~600-1000K + LLM cost ~$5-15）。詳見 [Stage_63A_Roadmap.md](Stage_63A_Roadmap.md) v2.0 + [v5_charter/05_Stage_63A_Spike_Notes.md](../architecture/v5_charter/05_Stage_63A_Spike_Notes.md) + [CHANGELOG v3.52.0](../../CHANGELOG.md#3520)。
> **2026-05-11 v7.87：Stage 62 ✅ 完成（v3.51.0）— FF 三十六 Phase B Charter spike（v5 動態架構規劃文件 deliverable）** — 純文件 deliverable / main branch / 0 production code 改動 / 0 EF Migration / 0 DI 結構改動。**4 deliverable 完整**：① `01_Spike_Plan.md` 7 驗證項細節（Victoria Router / Petra 自主調度 / per-task session / Crash Recovery / Mock Gemini Flash / 遷移成本量化 / Hybrid 會議 trigger）+ 預測項（強信心 5 / 中信心 1 / 未知 1）② `02_Architecture_Wire.md` 4 層 Hierarchy 落具體 service / DI（含 per-task session 多 row table schema 候選 + Tool Set Capability attribute+interface hybrid 候選 + 9 Worker capability mapping）③ `03_v4_Code_Audit.md` 三類分類 + LoC 量化（**吸收 ~16,061 LoC ~26%** / 重寫 ~3,991 LoC + 925 prompt 行 ~7% / 全保留 ~38,700+ LoC ~67% — v4 投資保留 + 重寫 = 73% 對齊「換引擎不換車身」精神 + Aria 規劃預估吸收 ~6K 自省揭露補強對齊 +167% 超預估）④ `04_Stage_63_PoC_Roadmap_Draft.md` PoC 6 子項 + 5 向對照 + 規模 L / cost ~600-1000K / 驗收標準。**FF 動態**：✅ FF 五十七 / FF 五十八 / FF 五十九 / FF 六十（4 個 close 不做 — v5 動態架構吸收）+ FF 三十六 status 升「🟡 進行中 Phase B Charter spike」+ FF 五十四子項 2/3 「保留評估」（v5 重寫後哪些大檔仍存在）+ FF 二十五/四十六/四十八 「保留」（Cody Worker prompt 仍適用）。**8 條 Christ 拍板對齊**（Charter 文件 only / Charter main + PoC branch / 保留 v4 不動 / 保留 10 Agent / 同 prompt 任務 / Forge spike healthy 模式 / Stage 51 spike Charter 模板 / minor bump）。**5 Forge spike 自決點 Aria gate1 全通過**（per-task session 多 row table / Petra prompt 全砍 / Tool Set hybrid / wc -l Glob 量化 / docs/architecture/v5_charter/ 新資料夾）。**Top 5 重排**：FF 三十六 升 #1（進行中 Charter spike）/ Stage 63 PoC 候選 #2 / FF 五十四子項 2/3 #3 / 戰略大重評估候選 #4（Charter+PoC 後再啟動）/ 二十五/四十六/四十八 保留群組 #5。**戰略主軸**：Stage 62 Charter 通過 → Stage 63 PoC spike feature/v5-poc branch + 5 向對照 + 7 驗證項實證 = v5 動態架構 ROI 拍板實證。詳見 [Stage_62_Roadmap.md](Stage_62_Roadmap.md) + [v5_charter/](../architecture/v5_charter/) + [CHANGELOG v3.51.0](../../CHANGELOG.md#3510)。

> **2026-05-10 v7.86：Trial_v8 結案 ⭐ 戰略級成功（連續 3 Trial 揭 6 🔴）vs 業務級失敗** — Kickoff Petra Round 1 escalate → Christ 點需要修改 → Petra modify subprocess `!result.Success` → **Stage 60 第 7 routing 真實首次觸發**（fire BossInteraction + 三選 actions ✓ 5 件全綠 — Trial_v7 揭 1 🔴 收口完整循環驗證）→ Christ 點 retry → **Stage 60 retry/abort path silent 卡死** → Aria SQL cancel 結案 / cost $1.2023 / 13 LLM call。**揭 2 🔴 戰略級新類型**：① Trial 試驗框架 AI Team 認知錯位升級（Trial_v6 議題 #3 升級 — Petra 看到 codebase 已含 Stage 60+61 痕跡 + Stage 61 prompt「Stage 61」字樣 → 困惑 escalate Christ）② Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死（Mock 只驗 continue path — Forge 自驗物理限制範疇延伸 + 「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證）。**FF 動態**：**新立 FF 五十九**（Trial 試驗框架 AI Team 認知錯位升級紀律 — 同任務 codebase 已含試驗痕跡的試驗模式設計）+ **新立 FF 六十**（Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死收口）。**結案第二段 step 0 升級 1 處 + 立自省點 #30**（Aria 給 Christ 的 prompt 草稿必須對齊 Christ 對話 register — Christ 親自點破 modify prompt 第一版「太細節不像老闆風格」對齊既有自省點 #26 AI Agent prompt 議題層次紀律延伸到 Aria 給人類使用者 prompt 草稿層）。**戰略結論**：連續 3 Trial 揭 6 🔴 + deliver 度持續倒退（11/12 → 部分 → 0 → 0 卡死更前置）= **infinite loop pattern 真實實證 = 戰略大重評估時機到**。**Aria 主動進戰略大重評估深度討論**（Christ 2026-05-10 拍板對齊）。詳見 [Trial_v8_Plan.md](../experiments/Trial_v8_Plan.md) v2.0 結案紀錄。
> **2026-05-10 v7.85：Stage 61 ✅ 完成（v3.50.0）— Petra/Cody prompt 對齊群組 + Pipeline UI refresh + Dashboard 補強（Trial_v8 開跑前最後清掃）** — 7 子項收口 6 🟡 系統性議題群組（FF 五十六 + 二十五 + 四十六 + 四十八 + 五十 + 四十五 + 四十 + 議題 #B）。**FF 動態**：✅ FF 五十六 / FF 二十五 / FF 四十六 / FF 四十八 / FF 五十 / FF 四十五 / FF 四十（7 個 ✅ 移完成）+ **新立 FF 五十七**（Petra prompt 5 位置 SoT 維護紀律 / 是否抽 prompt template helper — candidate standby Stage 61 揭露 5 位置同步首次任務）+ **新立 FF 五十八**（其他 3 申訴 path supersede 評估 — candidate standby Stage 61 範圍縮小 YAGNI 揭露）。Forge 自驗 5 PASS + Christ 視覺驗收 2 PASS（含場景 7 follow-up fix Stage 46 後端 SubTasks 漏填 自抓自修）。Aria 校準錨 **×0.99**（419K vs 預估 mid 425K，混合型第 15 資料點 mid 中段 — production-ready 補強 4 Stage 區間 ×0.78-0.99 完整驗證）。**結案第二段 step 0 升級 4 處**：① **workflow_aria.md 第 7 條延伸範圍段**（加「prompt builder 檔名 + method 數量 + config default value」進 source of truth 範圍 — 同類根因第三次累積修根因 Stage 56→59 + Stage 57+58 + Stage 61）② **自省點 #29**（Aria 規劃 prompt builder / config 數值漏 grep 同類根因第三次累積具體化原因紀律）③ FF 五十七 ④ FF 五十八。**Top 5 重排**：戰略大重評估候選升 #1（Christ 提出 — Aria 主動於 Trial_v8 結案後提醒）/ FF 三十六 #2（待 Trial_v8 + 戰略大重評估後）/ FF 五十四 #3 / FF 五十七 #4 / FF 五十八 #5。**戰略主軸**：Trial_v8 前置條件全綠 — Trial_v8 是路線 A/B/C 拍板關鍵實證。詳見 [Stage_61_Roadmap.md](Stage_61_Roadmap.md) v2.1 + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)。
> **2026-05-10 v7.84：Stage 60 FF 五十五 ✅ 完成（v3.49.0）— v4 framework 邊角 user actions legacy 遷移 + meeting subprocess fail-fast 統一** — Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口（推翻 Trial_v6「3 🔴 收口 = production-ready」假設）。子項 7 全 PASS（MeetingCommons 三條 swallow path 改 throw / Kickoff+Design ModifyTaskPlan/DesignPlan 遷 framework 議題 C2+H1 收口 / [SUBPROCESS_FAILURE] catch path 命名語意 / 第 7 routing 擴 Petra-{Stage} per-stage Port + 命名 / 4 Mock 場景全 PASS）。Forge spike 揭露 3 議題 Aria gate 通過全 Forge 自決（catch 點 / marker 命名 / per-stage Port）+ Forge 自加 WorkflowExceptionHelper unwrap framework 1.x event 細節 + 自驗 4 commits 健康自診修。Aria 校準錨 **×0.80**（438K vs 預估 mid 550K，混合型第 14 資料點 mid 帶下半，接近 Stage 56 ×0.78 / Stage 58 ×0.94）— **production-ready 補強 Stage 區間 ×0.78-0.94 三資料點驗證**。**結案第二段 step 0 升級 3 處**：① workflow_aria_session_lessons.md 自省點 #27（Aria 規劃 framework Workflow decision routing 必連帶 grep Stage Executor BossInteraction 開卡行為 + Pipeline 接管條件擴範圍配對紀律 — Stage 60 踩坑 #4+#5 修根因）② 自省點 #28（aria-prep-session skill 開場 prompt 模板必對齊既有 skill 觸發紀律 — Stage 60 揭露漏對齊 forge-self-verify 觸發紀律 + 已立修紀律 2）③ workflow_aria.md 第三節 A 第 8 條（Pipeline 接管 decision 擴展時計劃書必明列「Stage Executor case 處理 + decision 拓撲 BossInteraction 開卡行為」配對檢查）。Top 5 重排：FF 五十五 ✅ 移除 / FF 五十六+二十五+四十六+四十八+五十 群組升 #1（Stage 61 候選）/ FF 三十六 #2（待 Stage 61 + Trial_v8 後評估）/ Trial_v8 後戰略大重評估候選 #5（Christ 提出 — Aria 主動提醒）。詳見 [Stage_60_Roadmap.md](Stage_60_Roadmap.md) v2.0 + [CHANGELOG v3.49.0](../../CHANGELOG.md#3490)。
> **2026-05-10 v7.83：Trial_v7 結案 ⭐ 戰略級成功 vs 業務級失敗（雙面）**— Kickoff 階段 ModifyTaskPlan path 卡死中斷 / total cost **$1.5233**（-90% vs Trial_v6 $15.81）/ 17 LLM call。揭露 5 議題（**1 🔴 戰略級新類型** + 3 🟡 + 1 🟢）— **🔴 揭露「v4 framework 邊角 user actions 還在 legacy + Petra subprocess silent failure 沒 fail-fast + Stage 58 第 7 routing 沒 catch」直接推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設**。**「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證**（第一次 Trial_v6 揭 3 🔴 / 第二次 Trial_v7 揭 v4 邊角 legacy + silent failure）。**新立 FF**：FF 五十五（v4 邊角 user actions legacy 遷移 + silent failure → fail-fast 統一 — 🔴 戰略級必修 Stage 60+ 第一順位）+ FF 五十六（Petra prompt 議題層次篩選紀律 — Christ 親自點破「Petra 像之前的 Aria」AI Agent prompt 層對齊 Aria 紀律）。**Top 5 重排**：FF 五十五 升 #1（推翻 v3-ready 假設）/ FF 三十六 降 #2（待 Stage 60 補完才評估）/ FF 五十六 #4（合併 prompt 對齊群組）。Trial_v6 三 🔴 收口真實驗證 + Trial_v7 揭 1 🔴 收口待 Trial_v8+ 重跑驗證。詳見 [Trial_v7_Plan.md](../experiments/Trial_v7_Plan.md) v2.0 結案紀錄。
> **2026-05-10 v7.82：Stage 59 完成（v3.48.0）— FF 五十四子項 1 ✅ TaskGroupService 怪物大檔拆解 -54% 瘦身**（1759 → 主檔 808 + 4 新子 service Boss/Epic/Routing 3 子目錄）。**Aria 校準錨 ×1.09**（FF 二十系列倍率從 ×1.58 → ×1.09 -31%，SOP 累積 + workflow_aria.md 第 5+6+7 條紀律生效 — Stage 59 計劃書 -65% vs Stage 58）。**Aria 結案第二段 step 0 升級首次實踐**（refactor-sop.md SOP 2/6 + 實戰數據 + forge-self-verify skill port 修正 + workflow_aria.md 第 7 條 source of truth 紀律）。FF 五十四子項 2/3 評估動工依 Stage 59 ROI（ButtonCallbackRouter 1091 / DevAgentService 958） — Trial_v7 後排程。
> **2026-05-10 v7.81：Stage 58 完成（v3.47.0）— v4 framework production-ready 補強第二波 ✅ FF 五十三 ✅ 收口 = Trial_v6 揭露 3 🔴 戰略級議題全完成 🎉** — 路線 A marker pattern（AgentQueueProcessor specific catch [API_FAILURE] summary 前綴 + 4 Stage Executor HandleResponseAsync marker check + HandleAgentApiFailureResponseAsync 真三選 continue/retry/abort）+ LlmApiFailureException + LlmProviderType enum + 統一 type agent_api_failure_intervention + per-stage 4 PortId + 第 7 routing wiring。**Aria 校準錨 ×0.94**（439K vs 預估 465K，混合型第 12 資料點 mid 中段，接近 Stage 51 ×0.96 / Stage 56 ×0.92） — **戰略結論：Stage 57 ×1.36 → 58 ×0.94 大幅下降推翻「production-ready 補強性質倍率系統性偏高」假設，真正關鍵是 Aria 教訓套入完整度 + Forge spike 揭露架構盲點紀律生效**（Stage 58 0 self-diag fix vs Stage 57 4 self-diag + 1 patch race）。**Forge 自驗 V1-V8 全 PASS**（V2/V4 ROI skip + V3 4 agent 一次跑通驗 4 fire + dotnet test 131 passed）+ 1 follow-up backlog（Dev_plan stage API failure 走既有 dev_plan_unable routing graceful，不擴 Stage 58 範圍）。**Trial_v7+ 重跑 Trial_v6 候選排程**。
> **2026-05-10 v7.80：新立 FF 五十四 — Stage 36 後怪物大檔復發追蹤**（Christ 觀察 Forge Plan context 漸漲根因之一）。3 候選 ≥ 800 行警戒線：TaskGroupService 1591 行（Stage 57 後超 Stage 36 拆解前 baseline）/ ButtonCallbackRouter 1091 行（Stage 36 後新觀察）/ DevAgentService 958 行（Stage 36 後新觀察）。立 FF 追蹤等 Trial_v7 重跑後評估動工（治本對齊 Stage 34-36 FF 二十系列拆解 SOP）。**配套治標**：workflow_aria.md 第三節 A 加第 6 條紀律「大檔 reference 標精準 line + method 簽名」（Stage 59+ 立刻生效）+ 第 5 條「不寫整段 code 範例」（Stage 56→57→58 計劃書漸漲反例修根因）。
> **2026-05-09 v7.79：Stage 57 完成（v3.46.0 + 自驗 patch v3.46.1）— v4 framework production-ready 補強第一波** ✅ — Trial_v6 揭露 3 🔴 議題前兩個合併修：**FF 五十一** ✅（race condition 雙層防：fire 端 TryCreateUniqueInteractionAsync helper + handler 端 transaction + AsNoTracking idempotent + **驗收 patch v3.46.1** partial unique index DB constraint 雙保險擋 read-then-write race window）+ **FF 五十二** ✅（補 Stage 55B Session B 第 6 routing reviewer_fix_loop_limit + Christ 拍板真三選 mark_done→QaStageBridge / skip_qa→DocStageBridge / abort→SetIntervention end）。**4 self-diag fix + 1 patch — 全 0 escalate Forge 自診自修**。剩 FF 五十三 API 容錯獨立 Stage 58。Aria 校準錨待 Forge context 數字補。
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

## 當前優先級 Top 5（2026-05-14 v7.95 — Trial_v11 結案後 = 部分成功 → Stage 66 補強 → Trial_v12 → 切 default flag）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **Stage 66** ⭐⭐ | **Trial_v11 揭 3 🟡 工程議題收口**（Vera dispatch 0 work / PetraSessionMessages 缺 tool role / Cody InteractionCenter 連兩 Trial 漏）| 🔴 高 — Trial_v11 結案啟動條件達成 / Vera 0 work 業務級 | Trial_v11 揭「多 worker chain 實質單 worker」業務級議題 — Vera 接 dispatch + CLAUDE 載入但 subprocess 0 token 0 cost = production 切 default flag 後 review path 0 保護 ⚠️。Vera 0 work 根因調查為最高優先（subprocess prompt 是不是太簡 / Claude Code CLI exit code 路徑 / capability=code_review template 對齊度）+ PetraSessionMessages tool role 寫入修法（連帶 PR body Worker summary 段空）+ CLAUDE_Cody.md 紀律強化（強制 PR body 範圍對照表段 + InteractionCenter 顯式列入廣範圍指令處理範例）|
| 2 | **Trial_v12** | Stage 66 結案後 v5 production-ready 重驗（連續 3 Trial 業務級成功 = 路線 D 完整實證）| 🟡 待 Stage 66 結案 | 對齊 Trial_v10/v11 同 prompt + Aria 全程自跑 9-step 模板第 3 次實踐 / 通過 → Christ 拍板切 default flag |
| 3 | **三十六** ⭐ | v4 動態流程架構 — Phase B（Charter+spike+PoC+Stage 64+Stage 65 ✅ + Trial_v10 業務級成功 ✅ + Trial_v11 部分成功 → Stage 66 + Trial_v12）| 🟡 進行中 — Trial_v12 ✅ 後切 default flag | 連續 2 Trial 業務級成功重現（PR #372 + #373）= infinite loop pattern 確認打破 / 0 🔴 戰略級新類型 = 設計層仍對 / Stage 66 收口 3 🟡 + Trial_v12 重驗 → 切 `Workflow:UsePetraOrchestratorV5` default true = v5 動態架構正式上線 |
| 4 | **五十四** ⭐ | Stage 36 後怪物大檔復發 — 子項 1 TaskGroupService ✅ Stage 59 / 子項 2/3 保留評估 | 🟡 中 — v5 上線後 | v5 上線後再評估（路線 D 採用後 v4 conditional path 規模可能可砍 — 等 production 切 default flag 後評估）|
| 5 | （多項保留群組）| FF 二十五 / 四十六 / 四十八（Cody Worker prompt 對齊）| 🟡 保留 — v5 上線後 | v5 上線後 Cody Worker prompt 路徑 = Petra orchestrator dispatch 動態 / 既有對齊紀律可能自然吸收 — 觀察 Trial_v12+v13 模式再決定 |

> ⚠️ **戰略主軸**：**Trial_v11 結案 🟡 部分成功（業務級重現 ✓ + 4 議題 2 完整收口 + Vera 0 work 揭新業務級議題）**。連續 2 Trial（Trial_v10 + Trial_v11）業務級成功 PR 真開 = infinite loop pattern 確認打破。**Stage 65 4 議題收口效果**：CLAUDE.md 不污染 ✅ + workspace permission ✅ / Vera token_logs ⚠️ 半收口（finally 紀律寫入 ✓ 但 Vera 0 token 內容異常）/ Cody 5/5 cover ❌ 議題復發（4/5 漏 InteractionCenter 連兩 Trial 同樣議題）。**揭 3 🟡 新工程議題 0 🔴 戰略級**：Vera dispatch 0 work（最高優先業務級 — 多 worker chain 實質單 worker / Vera subprocess 接到任務立刻 exit success 沒做事）+ PetraSessionMessages 缺 tool role + Cody InteractionCenter 連兩 Trial 漏。**對戰略大重評估的關鍵實證**：v5 設計層仍對（0 🔴 戰略級新類型）+ business-level 重現 ✓ 但 review path 0 保護（Vera 0 work）+ **不切 default flag** → Stage 66 補強 → Trial_v12 重驗 → 通過才切 = v5 動態架構正式上線。**連續 6 Trial 議題密度進化曲線繼續**（v6 15 → v7 5 → v8 5 → v9 12+ → v10 4 → v11 3 + 議題 2 完整收口）= 系統成熟度進化曲線。詳見 [Trial_v11_Plan.md](../experiments/Trial_v11_Plan.md) v2.0 結案紀錄。

### 2026-05-11 close FF 補充紀錄（Stage 62 Charter spike v5 吸收）

| FF | 原議題 | close 不做原因 |
|---|---|---|
| **五十七** | Petra prompt 5 位置 SoT 維護紀律 | v5 prompt 重寫 — CLAUDE_Petra.md 全砍重寫 + 4 prompt builder method 全砍重寫對齊 Petra orchestrator（v5 動態架構勝過 SoT helper 抽 — 不再有 5 位置漂移風險）|
| **五十八** | 其他 3 申訴 path supersede 評估 | v5 動態架構 Petra Tool Set 動態 review/appeal — 不需固定 supersede helper（Pm/* 4 services 1415 LoC 吸收）|
| **五十九** | Trial 試驗框架 AI Team 認知錯位升級紀律 | v5 Petra prompt 重寫吸收 — CLAUDE_Petra.md 全砍重寫不再含「Stage 61 follow-up」字樣困惑（Trial_v8 揭露根因 = Petra prompt 累積層偏見根除）|
| **六十** | Stage 60 第 7 routing api_failure_retry/abort path silent 卡死 | v4 修法 ROI 負 — v5 動態架構 Petra orchestrator 捕捉 LLM API 失敗動態決策（不依賴固定 routing path）|

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

## 三十六、AiTeam v5 動態流程架構 — Phase B（FF 四十九 後續）⭐ 進行中

> 狀態：🟡 **進行中 — Phase B Charter spike ✅ Stage 62 完成（v3.51.0，2026-05-11）/ Phase B PoC spike 啟動條件達成待 Christ 拍板 → Stage 63**
> 提出日期：2026-04-28（Trial_v4 結案戰略討論）；2026-05-01 拆分為 Phase B（架構評估獨立於工具評估）；2026-05-02 Phase A 通過解鎖但延後評估；2026-05-10 Christ 拍板路線 D（Trial_v8 結案後 — Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop 真實實證 = v4 hierarchical static 補強 ROI 為負）；2026-05-11 Stage 62 Charter spike 完成 — 4 deliverable + 8 條 Christ 拍板 + 5 Forge spike 自決點 Aria gate1 通過。

### Charter spike deliverable（Stage 62 ✅ 完成 — 詳見 [v5_charter/](../architecture/v5_charter/)）

- **`01_Spike_Plan.md`** — 7 驗證項細節（Victoria Router / Petra 自主調度 / per-task session / Crash Recovery / Mock Gemini Flash / 遷移成本量化 / Hybrid 會議 trigger）+ 預測項（強信心 5 / 中信心 1 / 未知 1）
- **`02_Architecture_Wire.md`** — 4 層 Hierarchy 落具體 service / DI（含 per-task session 多 row table schema 候選 + Tool Set Capability attribute+interface hybrid 候選 + 9 Worker capability mapping）
- **`03_v4_Code_Audit.md`** — 三類分類 + LoC 量化（**吸收 ~16,061 LoC ~26%** / 重寫 ~3,991 LoC + 925 prompt 行 ~7% / 全保留 ~38,700+ LoC ~67%）
- **`04_Stage_63_PoC_Roadmap_Draft.md`** — PoC 6 子項 + 5 向對照 + 規模 L / cost ~600-1000K / 驗收標準

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

#### Trial_v7 揭露補強：Victoria scan lazy 化具體 ROI 量化（2026-05-10）

Trial_v7 觀察 Victoria CEO 階段 cost **+224% vs Trial_v6 baseline**（$0.0567 → $0.1838 / output tokens 1,400 → 2,807 / 同 1 次成功 + 同 prompt + 同模型），根因是 Victoria proposal 內含「已掃描 codebase 確認受影響範圍 5 個主要元件 + MudBlazor ISnackbar 基礎設施已到位但 Error 路徑尚未接入 toast」這層 codebase scan 結果。

**當前 hierarchical static 架構下 Victoria scan 的三個系統性問題**：
1. **eager scan 浪費 cost** — Victoria 不知道下游真正要哪個面向就先全掃
2. **scan 結果重複** — Kickoff 5 人會議 4 Agent + Petra 又各自 clone repo 重 scan
3. **scan 範圍隨 codebase 線性擴大** — Stage 49-58 codebase 變大後 Victoria cost 持續漲（cost +224% 訊號）

**動態架構（本 FF）天生解這三個問題**：
| 問題 | 動態架構解法 |
|---|---|
| eager scan 浪費 | Lazy scan — 第一個被 Petra orchestrator 派工的 Agent 才 scan |
| scan 重複 | 單一 scan 結果由 orchestrator broadcast |
| 線性擴大 | scan 範圍隨任務需要 lazy 擴大，不被 codebase 大小拖累 |

**ROI 量化維度**（Phase B spike 啟動時 evaluation criteria 之一）：
- **單任務省 cost 估算**：~$0.1-0.3 / 任務（Victoria scan 移除）
- **隨 codebase 持續變大線性擴大** — 長期 ROI 隨 codebase 規模放大
- 對齊「挑戰 1 Victoria 角色 = Discord 秘書 / Router 純 facade」拍板既有方向，但補強具體 ROI 數字而非僅 design 方向

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

✅ **2026-05-10 Christ 拍板路線 D 啟動**（Trial_v8 結案後 — Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop 真實實證 = v4 hierarchical static 補強 ROI 為負 → 對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項）。**兩階段拆分**：Stage 62 Charter spike（純文件 deliverable）→ Stage 63 PoC spike（feature/v5-poc branch + 5 向對照）— Charter 通過才 commit PoC 投資。

### 規模 / 風險

**規模**：**XL**（架構級躍進）— Stage 62 Charter spike M / Stage 63 PoC spike L / Stage 64+ 全量遷移 XL 累積
**風險**：**中-高**（架構級改動風險，但 Charter spike + 80% 既有設計拍板降低 spike 風險）

### 優先級

🟡 **進行中 #1（Top 5）** — Stage 62 Charter spike ✅ 完成 / Stage 63 PoC spike 啟動條件達成待 Christ 拍板。

---

## 五十、Dashboard token 統計頁 IsEstimated 視覺區分（Stage 56 範圍變更 follow-up） ✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— TokenAgentSummaryDto HasEstimated + BOOL_OR + razor MudIcon Warning + Tooltip + cost「~」前綴
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

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

## 五十四、Stage 36 後怪物大檔復發 — TaskGroupService / ButtonCallbackRouter / DevAgentService 拆解 ⭐

> 狀態：🟡 子項 1 ✅（Stage 59，2026-05-10）/ 子項 2/3 待 Trial_v7 後評估動工
> 提出日期：2026-05-10（Christ 觀察 Forge Plan context 漸漲根因之一）

### 背景

Stage 34-36 FF 二十系列完成「四怪物大檔拆解」（MeetingService / PmAgentService / TaskGroupService / CommandHandler 全清零，2026-04-22）後，v4 漸進遷移路線（Stage 49-58）一路加 routing / NotifyBoss / TryRoute / dispatch case → 三檔復發超過 `docs/conventions/refactor-sop.md` 警戒線 800-1200 行：

| 檔案 | 行數 | 狀態 |
|---|---|---|
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | **1591 行**（Stage 57 後）| ⚠️ 超 Stage 36 拆解前 baseline 1300+ |
| `src/AiTeam.Bot/Discord/ButtonCallbackRouter.cs` | **1091 行**（Stage 36 後新觀察）| ⚠️ 超警戒線 |
| `src/AiTeam.Bot/Agents/DevAgentService.cs` | **958 行**（Stage 36 後新觀察）| ⚠️ 接近警戒上界 |

### 對 Forge Plan context 影響（Christ 2026-05-10 觀察根因之一）

- 怪物大檔 full read 一次 ~20-32K token（每行 ~20 token）
- partial read 多段 ~10-20K（仍偏高）
- Stage 56→57→58 Forge Plan context 漸漲（140K → 227K → ~270K）核心原因之一

### 修法方向（治本）

對齊 Stage 34-36 FF 二十系列既有拆解 SOP（[refactor-sop.md](../conventions/refactor-sop.md)）：
- TaskGroupService 拆 NotifyBoss helpers / TryRoutePipeline helpers / ProcessBossResponseAsync dispatch / HandleEpicPartialPaused 等職責分檔
- ButtonCallbackRouter 拆 dispatch table / handler 分組
- DevAgentService 拆 Dev / Dev_fix / Dev_plan 等職責分檔

### 配套治標（已立刻生效）

`workflow_aria.md` 第三節 A 計劃書格式硬規則 v1.1（2026-05-10 加）：
- **第 5 條**：計劃書內不寫整段 code 範例（>10 行 code block 一律砍）
- **第 6 條**：大檔 reference 標精準 line + method 簽名（Forge 用 Read offset+limit partial read 而非 full read）

Stage 59+ 立刻生效，預期 Forge Plan context 下降 ~30-40%。

### 規模 / 風險

**規模**：L（三檔拆解 + 對齊 refactor-sop.md SOP + Mock regression 確認既有行為不變）
**風險**：低（拆解類重構 + Stage 34-36 既有 SOP 範本可複用）

### 優先級

🟡 中 — Trial_v7 重跑後評估動工（累積到痛點才拆比預先拆 ROI 高，對齊 Stage 36 拆解節奏）。**Stage 57/58 完成後痛點主要落在 TaskGroupService（1591 行繼續漲，Stage 58 加 NotifyBossAgentApiFailure + TryRoutePipelineAgentApiFailure + dispatch case 預估再 +50-70 行）— TaskGroupService 優先拆**。

### v4 兼容性

純 refactor，不動業務邏輯，不影響 v4 framework 行為。

---

## 五十五、v4 framework 邊角 user actions legacy 遷移 + silent failure → fail-fast 統一 ⭐ ✅

> 狀態：✅ **完成**（Stage 60，v3.49.0，2026-05-10）— Trial_v7 揭露 1 🔴 戰略級新類型議題收口
> 提出日期：2026-05-10（Trial_v7 結案揭露 — 推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設）
> 收口：[Stage 60 Roadmap v2.0](Stage_60_Roadmap.md) + [CHANGELOG v3.49.0](../../CHANGELOG.md#3490) + Aria 校準錨 ×0.80

### 背景（Trial_v7 揭露）

Trial_v6 結案宣稱 v4 framework 9/9 達成 + Stage 57/58 收口 3 🔴 = production-ready。但 Trial_v7 在 Kickoff「需要修改」action 觸發時揭露：

- Bot log: `KickoffMeetingService.ModifyTaskPlan` ← **legacy path**（不是 Stage 50/51 framework）
- Petra subprocess 失敗 → `MeetingCommons：Petra session 執行失敗`
- 但 KickoffMeetingService 沒 fail-fast → `ModifyTaskPlan Petra 回應完成` silent skip → DB UPDATE TaskPlan = "" 空字串（從 6,279 字 → 5 字）
- Stage 58 第 7 routing `agent_api_failure_intervention` **沒 catch** — 因為走 legacy path 不接 framework routing

雙通道對照（Discord vs Dashboard）兩源都走相同 legacy path 同失敗 — source-agnostic root cause = ModifyTaskPlan path 本身。

### 戰略意義

**v4 framework 漸進遷移完整路線「主路徑」9/9 達成 ≠「邊角 user actions」全遷移**：
- 主路徑：proposal_approved → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → done（已遷 framework）
- **邊角 user actions（modify / rerun / pause 等）：仍在 legacy path** ← 未盤點
- silent failure 沒 fail-fast：對照 Trial_v6 議題 #15 Sage silent skip 同類根因 — Stage 58 第 7 routing 設計範圍只 cover Agent API failure 不 cover meeting subprocess failure

### 修法方向

**Stage 60A**：盤點 + 遷移 v4 邊角 user actions legacy paths 到 framework
- grep 找出所有 `ResponseAction` 對應 handler 走 legacy path 的（KickoffMeetingService.ModifyTaskPlan / 其他 modify 路徑）
- 對齊 Stage 50/51 framework Kickoff 既有 pattern 遷移到 framework path

**Stage 60B**：subprocess failure → Stage 58 第 7 routing 統一接管
- MeetingCommons + 各 Service 的 subprocess failure catch 點補 fail-fast
- 對齊 Stage 58 路線 A marker pattern：build `[SUBPROCESS_FAILURE]` summary 前綴 result → call HandleAgentCompletedAsync 走正常 callback flow → Stage Executor marker check → fire `agent_subprocess_failure_intervention` BossInteraction（或重用 `agent_api_failure_intervention` 第 7 routing）

### 規模 / 風險

**規模**：M-L（盤點 + 兩 Stage 拆解）
**風險**：中（Stage 50/51 framework Kickoff 既有 pattern 範本可複用，但邊角 actions 範圍未明確需先 spike 盤點）

### 優先級

🔴 戰略級必修 — Stage 60+ 候選（Stage 59 結案後第一順位）。Trial_v7 揭露 v4 production-ready 邊界仍有缺口，影響面廣（任何走 legacy modify path 的 user action 都同類踩坑）。

### v4 兼容性

本 FF 是 v4 漸進遷移的補強段，對齊 Stage 49-58 路線繼續推進。

---

## 五十六、Petra prompt「議題層次篩選紀律」+「給定見不攤議題」推廣到 AI Agent prompt 層 ✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— CLAUDE_Petra.md +「議題層次紀律 + 給定見紀律 + 工時禁字紀律」段 + 4 prompt builder 共用 AppendPetraDisciplineSection helper
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：🟡 中 — Stage 60+ 候選 / 與 FF 二十五 / 四十八 / 四十六 系統性 prompt 對齊群組合併修
> 提出日期：2026-05-10（Trial_v7 結案揭露 — Christ 親自點破 Petra「給 Christ 決策包」行為）

### 背景（Trial_v7 揭露）

Trial_v7 Kickoff Petra 產出 TaskPlan 內含 5 待拍板議題 + 給 A/B/C 三選讓 Christ 拍。Christ 親自點破：「Petra 她現在就有點像前不久的 Aria，請我拍板很多細節」。

對照 user_christ.md「議題層次篩選紀律」+ workflow_aria.md「給定見不攤議題」精神 — 這兩條是 Aria 之前被 Christ 校正過的工作風格紀律，Petra prompt 同類根因應該同步學。

### 修法方向

對齊既有 Aria 紀律精神，更新 Petra prompt（含 Kickoff Petra / Design Petra / Pipeline Petra）：
- 純技術 / 內部設計議題 → Petra 自決（不丟給 Christ）
- 對 Christ 看到行為 / 業務邏輯 / spec 有影響的議題 → 才丟拍板
- 拍板議題給定見（推薦 A 方案 + 理由），不只列三選

### 規模 / 風險

**規模**：S-M（純 prompt 修改，可與 FF 二十五 / 四十八 / 四十六 系統性 prompt 對齊群組合併修）
**風險**：低（prompt 修改 + Mock 場景驗證即可）

### 優先級

🟡 中 — Stage 60+ 候選。建議與 FF 二十五（Cody 繞道傾向）+ FF 四十八 + FF 四十六 合併成「AI Agent prompt 對齊紀律統一」一個 Stage 處理。

### v4 兼容性

純 prompt 修改不動 framework 路徑，全 v4 兼容。

---

## 五十七、Petra prompt 5 位置 SoT 維護紀律 / 是否抽 prompt template helper（Stage 61 揭露）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：CLAUDE_Petra.md 全砍重寫 + 4 prompt builder method 全砍重寫對齊 Petra orchestrator（v5 動態架構勝過 SoT helper 抽 — 不再有 5 位置漂移風險）
> 提出日期：2026-05-10（Stage 61 結案揭露 — Petra prompt 三處同步實際是五處同步首次踩到）

### 背景

Stage 61 實作 FF 五十六（Petra prompt 議題層次篩選紀律延伸）時，Forge spike 揭露 Petra prompt 真實位置不是 Aria 規劃寫的「三處同步」（CLAUDE_Petra.md + KickoffPrompts.cs + DesignState.cs）而是「**五處同步**」：
- CLAUDE_Petra.md（1 處 markdown）
- KickoffPrompts.BuildPetraRoundPrompt + BuildPetraPlanPrompt（2 method）
- DesignPrompts.BuildDesignPetraRoundPrompt + BuildDesignPetraPlanPrompt（2 method）

Forge 自決路線：直接 inline 紀律段文字到 5 位置 + 兩 prompts class 各自有同名 AppendPetraDisciplineSection helper（純 string 內聚 SoT）+ commit message 標 SoT 維護筆記。

### 5 位置漂移風險

未來其他 Stage 改 Petra 紀律時，必須記得 5 位置同步（CLAUDE_Petra.md + 4 個 AppendPetraDisciplineSection helper）— 修一處漏其他 4 處就 prompt 漂移。

### 修法方向（候選）

**選項 A**：繼續沿用 5 位置 inline + commit message 紀律提醒（當前 Stage 61 實作）
**選項 B**：抽 cross-class helper（PromptDisciplinePartials class 集中管理紀律段文字）
**選項 C**：改 markdown-driven prompt（CLAUDE_Petra.md 為 SoT，prompt builder 從 markdown 載入紀律段）

### 規模 / 風險

**規模**：S-M（選項 B）/ M（選項 C）/ **風險**：低

### 優先級

⚪ candidate standby — 累積到必要時拆 Stage（未來改 Petra 紀律時若漂移踩坑就動工，目前 commit message + Future_Feature SoT 維護筆記 + AppendPetraDisciplineSection helper 內聚 SoT 已覆蓋）

### v4 兼容性

純 prompt 結構，與 framework 無關。

---

## 五十九、Trial 試驗框架 AI Team 認知錯位升級紀律（Trial_v8 揭 🔴）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：CLAUDE_Petra.md 全砍重寫不再含「Stage 61 follow-up」字樣困惑（Trial_v8 揭露根因 = Petra prompt 累積層偏見根除 — Stage 63 PoC 用 5 向對照數據組驗證）
> 提出日期：2026-05-10（Trial_v8 結案揭露 — Trial_v6 議題 #3 升級 🟢 → 🔴）

### 背景

Trial_v8 v3.50.0 跑同任務 prompt（沿用 Trial_v6/v7 對照組精準度最高）— Petra Round 1 直接 escalate「需求定位不清——無法確認這是新 Stage 還是 Stage 61 follow-up」。真實 root cause = **Petra 看到 codebase 已含 Stage 60+61 痕跡**（commits / Roadmap / Petra prompt 紀律段「Stage 61」字樣）→ 認知錯位「這個任務感覺被處理過 / 是 follow-up?」。

對齊 Trial_v6 議題 #3「Trial 框架 AI team 認知錯位」🟢（無實質影響）→ Trial_v8 升級 🔴（直接 escalate 卡流程）。**Trial 試驗框架的設計缺口而非 v4 framework 缺口**。

### 修法方向（候選）

**選項 A**：Trial 任務 prompt 加「Trial 試驗模式」明確標記（如「請忽略 codebase 中既有 Stage 60+61 等試驗痕跡，當作新需求處理」）— 簡單但污染 prompt 對照精準度
**選項 B**：每次 Trial 用獨立 worktree / branch 跑（codebase 不含試驗痕跡）— 對照組真實但運維成本高
**選項 C**：改試驗任務 prompt（每次 Trial 用不同任務避免認知錯位）— 失去四向對照基線
**選項 D**：等戰略大重評估拍板後再評估（如路線 B/C 大砍複雜度則 Trial 模式本身可能改變）

### 規模 / 風險

**規模**：S-M（純 Trial 流程設計 / 不動 production code）/ **風險**：低

### 優先級

⚪ candidate standby — 戰略大重評估拍板路線後決定（如路線 A 才動工）

### v4 兼容性

純 Trial 試驗框架設計，與 v4 framework 無關（不修 production code）。

---

## 六十、Stage 60 第 7 routing api_failure_retry/abort path 真實處理 silent 卡死收口（Trial_v8 揭 🔴）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v4 修法 ROI 負：v5 動態架構 Petra orchestrator 動態決策 LLM API 失敗（不依賴固定 routing path 的 retry/abort case body — Petra Tool Set 收到 Worker exception 動態評估 retry 拆段 / 換 model / abort）
> 提出日期：2026-05-10（Trial_v8 結案揭露 — Stage 60 第 7 routing 真實首次觸發後 retry path silent 卡死）

### 背景

Trial_v8 揭露 Stage 60 第 7 routing `agent_api_failure_intervention` 三選 actions 中：
- ✅ `api_failure_continue` 已 Mock 驗證（Stage 60 結案紀錄場景 C MockMode auto-approve）
- ❌ `api_failure_retry` Trial_v8 真實首次觸發 = **silent 卡死**（Bot 標 ProcessedByBot=true 但無任何處理 log + token_logs 0 row + task state 不推進）
- ❓ `api_failure_abort` 真實未觸發但對齊推測同類根因 silent skip

對齊 Trial_v6 議題 #15 + Trial_v7 議題 #1 同類「silent failure」根因 — Stage 60 修了 fire interaction + 三選 UI 但 retry/abort path 真實 routing 處理沒做（或 silent skip）。對齊「Mock 全綠 ≠ production 全綠」自省點 #25 第三次驗證。

### 修法方向（候選）

`PipelineRoutingService.TryRoutePipelineAgentApiFailureAsync` retry/abort case 真實 wire 完整：
- **retry**：重 invoke 原 stage entry（如 KickoffStageBridge re-entry 觸發 Petra resume session 重跑 modify path）
- **abort**：SetIntervention end + 標 task cancelled + fire generic intervention
- 對齊既有 continue case wire pattern（KickoffAgentApiFailureResponse → KickoffStageExecutor.HandleAgentApiFailureResponseAsync continue → state.KickoffDone=true + DesignStageBridge）

**Mock 場景補強**：Stage 60 場景 C 擴 retry / abort 兩 path（auto-approve 切換 — 對齊 Forge 自驗物理限制範疇修根因）。

### 規模 / 風險

**規模**：S（Routing service + Stage Executor case 機械化擴）/ **風險**：低

### 優先級

⚪ candidate standby — 戰略大重評估拍板路線後決定（如路線 A 才動工）

### v4 兼容性

對齊 Stage 60 既有 marker pattern + per-stage Port pattern 延續，全 v4 兼容。

---

## 六十一、v5 PoC → production-ready simplification 補強清單（Stage 63B Aria spot check 揭露 — Stage 64+ 處理）

> 狀態：⚪ candidate standby — Stage 64+ 全量遷移時處理（v5 動態架構 production-ready 補完整 / Trial_v9 真實任務驗證後若採用路線 D 才動工）
> 提出日期：2026-05-12（Stage 63B 結案 Aria spot check 揭露 — Christ question「Context 偏低是否該做沒做」拍板開 FF 追蹤）

### 背景

Stage 63B PoC ✅ Mock 全綠 + 校準錨 ×0.49（vs Aria 預估 mid 750K 偏低 -51%）— Christ question 拍板 spot check 後揭 **30% 隱性 production simplification 我 Aria gate1 沒揭露**。所有都是 Mock 階段足夠 / production 階段需要 — Stage 64+ 全量遷移時補完整。對齊 Stage 60 揭露「Mock 全綠 ≠ production 全綠」自省點 #25 第 N 次精神延伸到「組件單元測試全綠 ≠ 端對端整合測試全綠」新類型。

### 補強清單（4 點）

**1. 🔴 PetraOrchestratorService.StartAsync 端對端 xUnit 漏測**

xUnit 7 test 真實覆蓋 7 個 critical 組件單元（DecideAsync parse / PetraSessionRepository 持久化 / WorkflowSettings default / Worker capability attribute reflection / ClaudeCodeChatClientAdapter dispatch 7 case）— 但**沒測完整 chain**：

> `PetraOrchestratorService.StartAsync → DecideAsync → BuildSequential → InProcessExecution.RunStreamingAsync → Worker.CreateAgent → ChatClientAgent → ClaudeCodeChatClientAdapter → IChatClient → IClaudeCodeService`

= 路線 A 三層 wrapper **端對端跑通** xUnit 漏做 — 「Mock 全綠 153 passed」實際是「組件單元測試全綠 ≠ 端對端整合測試全綠」。**Trial_v9 真實任務跑時才會驗端對端鏈**。

**2. 🟡 ResumeAsync「PoC 簡化」紀錄補強**

`PetraOrchestratorService.ResumeAsync` 註解寫「PoC 簡化：mark 既有 session done + 開新 session」— 不是真實「同 session 繼續」。production 階段需要切回**同 session 繼續**（rebuild context 不開新 session）對齊 5 挑戰拍板 #5「重啟重跑紀律」+ 不破壞 session_messages 連續性。

**3. 🟡 BuildSessionContext fallback hardcode `Agents:Dev:Model`**

`BuildSessionContext` 取 Worker model fallback hardcode 順序：`Agents:Dev:Model` → `Anthropic:DefaultModel` → `"claude-opus-4-6"` — **所有 7 Worker 共用 Dev model**。沒走 per-Worker AgentConfig DB Stage 38 既有 pattern（Cody=Opus / Vera=Sonnet / Quinn=Sonnet / 等）。production 階段需要 per-Worker model from AgentConfig DB。

**4. 🟡 PetraSessionRepository.AppendMessage 同步 method**

既有 BossInteraction Repository 是 async pattern — PetraSessionRepository.AppendMessage / Start 暴露同步 method 而不是 async。production 真實任務跑時可能踩 EF Core tracking 議題（跨 scope DbContext + 並行寫）— Mock 階段 InMemory DB 不踩。

### 修法方向

- 點 1 端對端 xUnit：加 PetraOrchestratorServiceTests.Test8 — stub ILlmProvider 回 capability 序列 + 跑 PetraOrchestratorService.StartAsync + 驗 BuildSequential events 真實 fire + 真實 worker 被 dispatch（透過 stub IClaudeCodeService LastInvokedMethod 多個）
- 點 2 ResumeAsync 改同 session 繼續 — 重 rebuild context 但保留 sessionId / 不寫新 PetraSession row
- 點 3 BuildSessionContext 走 AgentConfig DB query — 對齊 Stage 38 既有 dynamic model resolver pattern
- 點 4 PetraSessionRepository.AppendMessage / Start 改 async — 對齊既有 BossInteractionRepository pattern

### 規模 / 風險

**規模**：M（架構級 production-ready 補強 / 4 點獨立改 + 1 點端對端 xUnit）/ **風險**：低（feature/v5-poc branch + feature flag default=false 雙保險 + Stage 64+ 全量遷移精神對齊）

### 優先級

⚪ candidate standby — **Stage 64+ 全量遷移時處理**（路線 D 採用拍板後才動工 — Trial_v9 結案是關鍵實證）。若 Trial_v9 揭露其他 production simplification 議題 → 本 FF 擴 cover。

### v4 兼容性

完全在 feature/v5-poc branch + v5 動態架構範疇 — 與 v4 hierarchical static 無關。

### Aria gate1 補強紀律候選（候選自省點不立檔）

Stage 63B 結案揭：Aria gate1 commit 檢查只對照 Plan 子項 LoC + dotnet test pass + commit message — 沒 spot check production simplification flag points。**下次同類型 Stage 結案前考慮 gate1 補強紀律「commit 抽 1-2 個 critical service 看實作真實對齊 production-ready vs Mock 階段簡化」**— 但第一次資料點不立自省點，等 Stage 64+ 同類型再驗（healthy 累積成 baseline）。

---

## 五十八、其他 3 申訴 path supersede 評估（Review Appeal / QA Fix / Sage escalate）✅ close 不做

> 狀態：✅ **close 不做（Stage 62 Charter spike 2026-05-11）** — v5 動態架構吸收：Petra Tool Set 動態 review/appeal（Pm/* 4 services 1415 LoC + Appeal Orchestration 1375 LoC 全納吸收 — 不需固定 supersede helper / SupersedePriorFailedTasks pattern 在 v5 動態架構自然消失）
> 提出日期：2026-05-10（Stage 61 結案揭露 — SupersedePriorFailedTasks 只 cover Dev_plan path 兩處）

### 背景

Stage 61 實作 FF 四十五（Christ action supersede）時 Forge spike 揭露範圍縮小 YAGNI：SupersedePriorFailedTasks helper 只 cover Dev_plan path 兩處（escalate_devplan_skip / abort）— 對齊 Trial_v6 議題 #9 揭露的 Dev_plan path 具體場景。

**其他 3 申訴 path 沒實證同類 cross-Agent supersede 誤判**：
- Review Appeal escalate（Stage 23 Cody-Vera-Petra Appeal loop）
- QA Fix loop escalate（Stage 24/55B QA fix loop limit）
- Sage escalate（Stage 23 Sage 歸檔）

### 修法方向（候選）

待 Trial_v8 真實使用揭露其他 3 申訴 path 是否踩同類問題：
- 觸發：Trial_v8 真實 Christ 點其他申訴 button → 觀察 MarkGroupDoneOrIntervention 是否誤觸 needs_intervention（理應已修根因）
- 若實證踩 → SupersedePriorFailedTasks helper 擴 cover 其他 3 path
- 若實證不踩 → 範圍縮小確認，FF 五十八 close

### 規模 / 風險

**規模**：S（對齊 SupersedePriorFailedTasks helper pattern 機械化擴）/ **風險**：低

### 優先級

⚪ candidate standby — Trial_v8 真實使用揭露才動工（YAGNI 精神）

### v4 兼容性

純 button handler + status 邏輯，與 framework 無關。

---

## 十、Dashboard UI 細節打磨（第四批）

> 狀態：低優先級 — UI 組織與使用便利性優化，待 Christ 確認完整清單後排入 Stage
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static 沒重設計 UI，仍需做）

### 修法方向

待 Christ 確認完整清單（Dashboard 系統設定 / 任務列表 / 流程追蹤 / Agent 設定 / 規則管理等頁面 UX 整體打磨清單）。

### 規模 / 風險

**規模**：M-L（依清單範圍）/ **風險**：低

### 優先級

低 — 業務上不阻塞，UX 優化性質

### v4 兼容性

純 Dashboard UI 改動，與 framework 無關。

---

## 二十二、Agent 命名一致性（守門 + 名稱映射）

> 狀態：中 — Agent 名稱混雜（Cody/Dev/Vera/Reviewer 等），需建統一守門 + 名稱映射
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static Worker pool 命名規則沒改變）

### 修法方向

建立 Agent 名稱映射常數表（`AgentNames.cs`）+ 跨層 (Discord channel / DB AssignedAgent / Workflow executor key) 守門檢查。

### 規模 / 風險

**規模**：S-M（建 const + 跨層守門）/ **風險**：低

### 優先級

中 — Christ 觀察 Dashboard 顯示混淆時優先處理

### v4 兼容性

純 const + 守門，與 framework 無關。

---

## 二十五、Self-implement 試驗 prompt 設計守則（Cody 繞道傾向）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— CLAUDE_Cody.md「Dev_plan 結構規範（強制）」新段（Step 1/2/3 + 改哪些檔案 + Issue 對照表 + 禁止「現況確認」表格）+ ImplementationNote 強制標題
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 進一步揭露 Cody/Petra prompt 對齊系統性議題（×3 確認）
> 重新分類：2026-05-09（從 v4_eval 升 active — Trial_v6 揭露議題加重）

### 背景

Trial_v6 Phase 1/2/3 三個 phase Cody Dev_plan 都 escalate（×3 系統性確認）— Cody 寫「現況確認」表格 vs Petra 期待「實作步驟說明」結構衝突。對齊 Trial_v5 Checkpoint 4 同議題重演。

### 修法方向

- Cody Dev_plan prompt 補強「實作步驟說明」結構規範（Step 1 / Step 2 / 改哪些檔案 / 加哪些 class / DI 註冊等）
- 對齊 Petra prompt 期待結構（CLAUDE_Cody.md vs CLAUDE_Petra.md prompt 對齊 audit）

### 規模 / 風險

**規模**：S-M（純 prompt 改寫 + Trial_v7 重跑驗證）/ **風險**：低

### 優先級

中 — Stage 57+ 候選（與 FF 五十一/五十二/五十三 一起處理 Cody/Petra prompt 對齊群組）

### v4 兼容性

純 prompt 改動，與 framework 無關。

---

## 四十、Stage 46 Dashboard razor UI 接線（epic 折疊 + 進度條 + 暫停按鈕）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— PipelineList row IsEpic 視覺標 + Sub-task icon + EpicPaused chip + PipelineView Epic section sub-task 列表 + ⏸️ 暫停 / ▶️ 恢復 epic 按鈕。場景 7 follow-up fix Stage 46 後端 SubTasks 漏填 自抓自修
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中-高 — Stage 46 FF 三十五自動拆任務的 Dashboard UI 接線未完成
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static UI 沒重設計，仍需做）

### 背景

Stage 46 自動拆任務（FF 三十五）已完成 backend 邏輯（EpicChain + sub-task chain），但 Dashboard razor UI 接線未完成：epic 折疊面板 / sub-task 進度條 / 暫停 epic 按鈕等 UX 缺失。Trial_v6 揭露 parent group 流程追蹤不顯示 sub-task 內部 stage（議題 #5）對應這個 FF。

### 修法方向

- Dashboard 流程追蹤頁加 epic 折疊面板（parent group 顯示 N sub-task 子層）
- sub-task 進度條（顯示 sub-task 內部 stage：Dev_plan/Dev/Reviewer/QA/Doc）
- 暫停整個 epic 按鈕（對應 EpicPaused 邏輯）

### 規模 / 風險

**規模**：M（razor + DTO + SignalR push）/ **風險**：低

### 優先級

中-高 — Trial_v6 觀察期間 Christ 體驗痛點（議題 #5），Stage 57+ 候選

### v4 兼容性

純 Dashboard UI 接線，與 framework 無關。

---

## 四十五、Dashboard 重試/跳過後舊 failed task 沒清理（MarkGroupDoneOrIntervention 誤判）✅（範圍縮小）

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— ButtonCallbackRouter SupersedePriorFailedTasks helper 兩處呼叫 + TaskGroupService.MarkGroupDoneOrInterventionAsync InterventionReason 動態列出真實 escalate source。**範圍縮小 YAGNI**：只 cover Dev_plan path（escalate_devplan_skip / abort）— 其他 3 申訴 path 立 FF 五十八 Trial_v8 後評估 candidate
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 議題 #9 直接踩（generic intervention 訊息「Vera 0 次修復後仍發現問題」誤導，實際是 Sage escalate）
> 重新分類：2026-05-09（從 archived_v4 升 active — v4 hierarchical static 仍依賴 task status 聚合判斷）

### 背景

Trial_v5 + Trial_v6 都觀察：Christ 按「跳過審核」/「重啟 Dev」action 後前置 failed task 沒被自動清除 → `MarkGroupDoneOrIntervention` helper 看到歷史 failed task 仍掛在 group → 標 needs_intervention + 建 generic intervention BossInteraction（訊息誤導實際根因）。

### 修法方向

- Christ action 標記後（跳過審核 / 重啟 Dev）→ 對應 task 標 cancelled / superseded
- `MarkGroupDoneOrIntervention` 邏輯加「忽略已被 superseded 的 failed task」
- generic intervention 訊息模板按真實 escalate source 動態化（對齊 FF 五十三第三 Agent fail-fast 統一精神）

### 規模 / 風險

**規模**：S-M（純 status 邏輯 + 訊息模板）/ **風險**：中（涉及 group 判定邏輯，需 Trial 對照組重驗）

### 優先級

中 — Stage 57+ 候選（跟 FF 五十一/五十二/五十三 一起處理）

### v4 兼容性

純 status 聚合 + 訊息模板，與 framework 無關。

---

## 四十六、ImplementationNote 寫入路徑與 PR Body 對齊（Sage 過嚴 escalate + Cody 實作範本補強）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— Cody Dev/Dev_fix prompt 強制寫 ImplementationNote + DocAgentService prompt 引導 Sage 走 PR Body / git log fallback + CLAUDE_Sage.md 品質下限改 fallback path（兩備援皆失敗才 escalate）
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中 — Trial_v6 議題 #8 直接踩（Cody 跳過 Dev_plan 後沒寫 ImplementationNote → Sage 歸檔失敗）
> 重新分類：2026-05-09（從 archived_v4 升 active — v4 hierarchical static Sage 仍看 ImplementationNote）

### 背景

Trial_v5 PR #170：Cody PR Body 寫了完整實作說明，但 DB `task_groups.ImplementationNote = 0 字` → Sage escalate 誤判 Cody 沒寫實作。Trial_v6 Phase 1 / 3 同議題重演（Cody 跳過 Dev_plan 後沒寫 ImplementationNote → Sage 歸檔失敗）。

### 修法方向

- Cody Dev / Dev_fix prompt 強制寫 ImplementationNote DB 欄位（不論是否走 Dev_plan）
- Sage 歸檔流程加備援 source（PR Body / commit log）— 不只看 ImplementationNote
- 兩條路雙保險

### 規模 / 風險

**規模**：S-M（Cody prompt + Sage 邏輯）/ **風險**：低

### 優先級

中 — Stage 57+ 候選

### v4 兼容性

純 Agent prompt + 歸檔邏輯，與 framework 無關。

---

## 四十八、Cody Dev_plan 階段 maxTurns 配置不足（複雜任務踩 100%）✅

> 狀態：✅ **完成**（Stage 61，v3.50.0，2026-05-10）— Cody Dev_plan maxTurns 從 default **10**（不是 Aria 規劃寫的 40 — 真實值揭露）提升至 80。IClaudeCodeService.RunReadOnlyAsync 加 `int? maxTurns = null` 4 處同步擴 + DevAgentService caller 傳 `maxTurns: 80` + 3 處既有 caller（Designer/Requirements/PmReview）保持 default 10 不影響
> 收口：[Stage 61 Roadmap v2.1](Stage_61_Roadmap.md) + [CHANGELOG v3.50.0](../../CHANGELOG.md#3500)

> 狀態：中-高 — Trial_v6 Phase 1/2/3 三個 phase Cody Dev_plan 全 escalate
> 重新分類：2026-05-09（從 v4_eval 升 active — Trial_v6 真實任務驗證觸發）

### 背景

Trial_v5 / Trial_v6 觀察 Cody Dev_plan 在複雜任務踩 maxTurns 100% → escalate dev_plan_unable HITL routing。對應 FF 二十五（Cody/Petra prompt 對齊問題）的子議題 — maxTurns 不夠是 prompt 對齊問題的物理表現。

### 修法方向

- Cody Dev_plan maxTurns 從預設值（推測 40）提升至 80-100
- 或動態化（Dashboard 可調，對齊 FF 十九 Agent maxTurns 動態化精神）
- 跟 FF 二十五一起處理 prompt 對齊根因

### 規模 / 風險

**規模**：S（純 config 改動）/ **風險**：低

### 優先級

中-高 — Trial_v6 系統性議題確認（×3 phase 都踩），Stage 57+ 候選

### v4 兼容性

純 config 動態化，與 framework 無關。


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

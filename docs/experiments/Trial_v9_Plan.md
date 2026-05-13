# Trial_v9 試驗計劃書 — v5 動態架構 PoC 真實任務驗證 + 戰略大重評估路線 D 拍板實證

> 對應版本：**v3.53.0**（Stage 63B Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠 — feature/v5-poc branch）
> 建立日期：2026-05-12
> 狀態：規劃中（待 Christ 儲值 ≥ $30 buffer + 開跑時間）
> 文件版本：v1.0

---

## 一、背景與定位

### 戰略脈絡

**v5 動態架構 spike 三階段累積 derisk 後第一次真實任務試驗**：
- **Stage 62（v3.51.0）= FF 三十六 Phase B Charter spike** ✅ — 4 deliverable 完整（spike Plan + 架構 wire + v4 audit + PoC Roadmap 草稿）+ 8 條 Christ 拍板對齊 + 5 Forge spike 自決點通過
- **Stage 63A（v3.52.0）= API spike ✅ 硬通過** — Christ AI Studio Gemini 2.5 Flash 真打 3 場景 trigger 命中率 100% + 揭 **2 framework limitation 戰略級早期 derisk**（base GroupChatManager 不啟動 manager loop → 自寫 orchestrator + base AIAgent 不被 framework dispatch → ChatClientAgent + IChatClient adapter 必走）
- **Stage 63B（v3.53.0）= PoC spike + Mock 全綠** ✅ — 9 子項完成（PetraOrchestratorService 路線 A 自寫 + DecideAsync + BuildSequential + InProcessExecution + ClaudeCodeChatClientAdapter + 7 Worker IAgentTool factory + EF Migration + Feature flag + 8 CLAUDE_*.md 重寫含 FF 五十九 hand-off 落實）+ Aria 校準錨 ×0.49（前置三階段 derisk 真實生效）

**Trial_v9 定位**：

**v5 動態架構 PoC + 真實任務驗證 + 戰略大重評估路線 D 拍板關鍵實證** — 對齊 Christ 2026-05-10 拍板路線 D（v5 動態架構 spike）後第一次「真實能不能 deliver」的證據點。

**Stage 跟 Trial 分開拍板**（Christ 2026-05-11）— Trial_v9 對齊 Trial_v2-v8 既有獨立試驗計劃模式 / 不在 Stage 63B 範圍內。

**核心試驗問題**：v5 動態架構在 Stage 63B PoC + FF 六十一 4 點 production simplification 接受下，能否真正 deliver 完整 feature 打破 v4 hierarchical static 連續 3 Trial 揭 6 🔴 infinite loop pattern？還是 v5 也踩同類訊號 = 戰略大砍 / 維持 v4 / Claude Code 模式？

---

## 二、試驗目的（4 條）

1. **驗證 Stage 63B PoC 路線 A 端對端真實生效**（對齊 FF 六十一 點 1 漏測 — Trial 真實任務本身就是端對端驗證最佳場景）：
   - CeoAgentService flag check forward → PetraOrchestratorService.StartAsync 真實生效
   - DecideAsync 真實 LLM 動態決策（Gemini Flash + CLAUDE_Petra.md 三 trigger 條件）
   - BuildSequential + InProcessExecution.RunStreamingAsync events 訂閱真實 dispatch
   - Worker.CreateAgent → ChatClientAgent + ClaudeCodeChatClientAdapter → IChatClient → IClaudeCodeService 三層 wrapper 真實 chain 跑通

2. **驗證 FF 五十九 hand-off 落實效果** — Petra 看到 codebase 含 v4 漸進遷移 + v5 PoC + Stage 60+61 prompt 痕跡時**不要 escalate Christ「為什麼有兩套架構」** — spike + 漸進遷移期間是預期狀態繼續跑當前任務（CLAUDE_Petra.md 開頭 v5 PoC 期間紀律段真實生效）

3. **量化 v5 動態架構真實 ROI 對照 v4 連續 3 Trial 揭 6 🔴 infinite loop pattern**（5 向對照 Trial_v5/v6/v7/v8/v9）：
   - cost：Trial_v5 baseline $8.78 / v6 $15.81 / v7 $1.52 中斷 / v8 $1.20 卡死 / **Trial_v9 預期 $5-15**（Gemini Flash 免費 tier 省 Petra cost + 7 Worker hardcoded Dev model 偏高 +30-50% buffer）
   - 完成度：v5 11/12 / v6 部分 / v7 0 中斷 / v8 0 卡死 / **Trial_v9 預期 Phase 1 完整 deliver**（v5 動態破局訊號）
   - 揭 🔴：連續 3 Trial 揭 6 🔴 / **Trial_v9 預期 0 🔴 新類型**（路線 D 採用實證）或 ≥1 🔴（v5 也踩 infinite loop 訊號 = 戰略大砍 / 維持 v4 / Claude Code 模式拍板實證）

4. **戰略大重評估「路線 D 採用 vs A/B/C」拍板實證**：
   - v5 ROI ≥ Trial_v5 baseline + Phase 1 完整 deliver + 0 🔴 新類型 → **路線 D 採用** → Stage 64+ 全量遷移啟動 + FF 六十一補強
   - v5 ROI < Trial_v5 baseline / Phase 1 不完整 / ≥1 🔴 新類型 → infinite loop pattern v5 也踩 → 戰略大砍（路線 B）/ Claude Code 模式（路線 C）/ 維持 v4（路線 A）
   - **Aria 主動於 Trial_v9 結案後提醒進戰略大重評估深度討論**（Christ 2026-05-10 拍板對齊精神延續）

---

## 三、任務需求

### Christ 確認沿用 Trial_v6/v7/v8 同 prompt（5 向對照精準度最高）

任務原文（完全照搬 Trial_v6/v7/v8 — 對照組精準度最高）：

> Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。
>
> 最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。
>
> 舉個例子：首頁那個快速下達指令卡，如果我打的指令違規，提示就會在送出區上面冒出來，但有時候我已經滑下去看其他東西了，根本沒注意到指令沒送出去。
>
> 我希望能改成：表單裡的提示保留（緊貼違規來源），但同時要有個東西通知我「剛剛失敗了」，類似右下角彈出來的那種 toast 提示，讓我視線在哪裡都會看到。兩個地方訊息一致，toast 大概 3-5 秒自動消失就好。
>
> 不只快速下達指令卡，我覺得整個 Dashboard 凡是有「儲存失敗 / 操作失敗」的地方（系統設定、操作中心、Agent 設定、規則管理之類），都應該照同樣方式打磨一下。具體哪些頁面要動、要不要抽共用元件，你跟團隊判斷。

### 操作前置條件（Christ 開跑前必確認）

1. **API 餘額 ≥ $30** — Trial_v9 cost 預估 $5-15 + 容錯性 buffer（Trial_v6 揭露 API 餘額自然耗盡風險）
2. **Bot 切到 feature/v5-poc branch 跑** — `git checkout feature/v5-poc` + 重啟 docker compose（main 不動 / v4 production 保留）
3. **切 feature flag `Workflow:UsePetraOrchestratorV5=true`** — Dashboard 系統設定頁切換 / 5 分鐘內 Bot Cache reload（既有 Stage 47 機制）
4. **切 Petra Provider→Gemini Flash** — Dashboard Agent 設定頁 Petra（PM）卡片切換 Provider→Gemini + Model=gemini-2.5-flash（既有 Stage 38 功能）+ ReloadCache
5. **Christ 從 Discord 或 Dashboard 送出指令**（任一通道 — 對齊 Trial_v8 雙通道對照精神）

---

## 四、預期觀察清單（Trial_v9 vs v8 vs v7 vs v6 vs v5 五向對照）

| # | 維度 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 | Trial_v9 預期 | Aria 自驗工具 |
|---|---|---|---|---|---|---|---|
| 1 | Pipeline path 走通率 | v3 hardcoded | v4 100% | v4 100%（中斷前）| v4 100%（卡死前）| **v5 動態 100%**（PetraOrchestratorService.StartAsync 完整跑通端對端 chain） | `SELECT COUNT(*) FROM "PetraSessions" WHERE "Status" = 'done' AND "CreatedAt" >= '<開跑時間>'` |
| 2 | **總 cost** | **$8.78** | **$15.81 (+80%)** | **$1.5233 (-90% 中斷)** | **$1.2023（卡死）** | **預期 $5-15**（Gemini Flash 免費 tier 省 Petra LLM call + 7 Worker hardcoded Dev model 偏高 buffer）| `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` |
| 3 | **Petra 動態決策準度**（v5 新類型）| N/A | 固定 7-stage | 同 | 同 | **三 trigger 命中率 ≥ 80%**（小需求 1-on-1 / 中需求 Design / 大需求 Kickoff — Dashboard 錯誤打磨任務跨 5 元件預期命中 Design trigger）| PetraSessionMessages assistant message + Bot log Petra DecideAsync |
| 4 | **端對端 chain 真實生效**（v5 新類型 / FF 六十一 點 1 真實驗）| N/A | N/A | N/A | N/A | **預期 dispatch ≥ 2 worker**（DevAgentService + ReviewerAgentService 至少各 fire 1 次 — Design trigger 對應 code_implementation + code_review）| Bot log `ClaudeCodeChatClientAdapter dispatch capability=X` + DB session_messages tool 標記 |
| 5 | **FF 五十九 hand-off 真實效果** | N/A | 議題 #3 揭 | 議題 #C 重現 | **議題 #1 升級 🔴** | **預期 0 困惑 escalate**（Petra 看到 codebase v5 + v4 痕跡不 escalate Christ「為什麼有兩套架構」— CLAUDE_Petra.md 開頭 v5 PoC 期間紀律段真實生效）| BossInteraction Type=`intervention` + Petra reasoning log scan |
| 6 | **per-task session 持久化真實寫入** | N/A | N/A | N/A | N/A | **預期 PetraSession.Status=done + PetraSessionMessages ≥ 5 rows**（user input + assistant DecideAsync + tool worker outputs）| `SELECT COUNT(*) FROM "PetraSessionMessages" WHERE "SessionId" = '<id>'` |
| 7 | Cody Dev_plan 完成度 | 1-2 輪通過 | 3 phase 都 escalate | 未到 | 未到 | **預期 1-2 輪通過**（v5 動態 Petra 直接派 Cody = ChatClientAgent 透過 adapter dispatch RunAsync — 同 v4 Cody 行為對齊 Stage 61 prompt 紀律）| DB DevPlan + DevPlanRevision SQL |
| 8 | Quinn 測試 | 30 + 6 all passed | 0 | 未到 | 未到 | **預期 ≥ 10 xUnit + visual all passed**（Petra Kickoff trigger 才會 dispatch Quinn — Design trigger 不含 QA / 視 Petra 動態決策） | `dotnet test` + Playwright |
| 9 | Vera 審查精準度 | 9 處裸 catch 1 輪通過 | Phase 2 fix loop ×3 卡死 | 未到 | 未到 | **預期 1-2 輪通過**（v5 Vera 透過 adapter dispatch RunReviewAsync — 同 v4 Vera 行為對齊 Stage 61 prompt 紀律）| Reviewer comment + fix iteration count |
| 10 | Sage escalate 速度 | 13:30 → 14 秒 | Phase 1 escalate / Phase 3 silent skip | 未到 | 未到 | **預期不踩 Trial_v6 議題 #8**（Stage 61 ImplementationNote 強制寫 + Sage 備援 source 修法 v5 prompt 重寫對齊）| Bot log timestamp + Sage 行為 |
| 11 | **Worker capability dispatch 對應**（v5 新類型）| N/A | N/A | N/A | N/A | **預期 100% capability 命中對應 IClaudeCodeService method**（code_implementation→RunAsync / code_review→RunReviewAsync / qa_testing→RunQaAsync 等對齊 ClaudeCodeChatClientAdapter 7 case dispatch）| Bot log adapter dispatch + IClaudeCodeService method call SQL |
| 12 | **FF 六十一 4 點 simplification 真實影響觀察** | N/A | N/A | N/A | N/A | **觀察**：① ResumeAsync 是否觸發（短任務不踩） ② BuildSessionContext Dev model 共用 → Vera/Quinn 用 Opus model cost 偏高 +30-50%（vs per-Worker Sonnet） ③ sessionRepo sync method 是否踩 EF tracking | Bot log + DB scan |
| 13 | **HITL routing 真實觸發** | N/A | 5 種全觸發 | 2 種（中斷前）| 第 7 routing 真實首次觸發 | **預期 0-2 種**（v5 動態決策可能跳會議 / 視 Petra reasoning — kickoff proposal_approval / agent_api_failure_intervention 視情況觸發）| BossInteraction Type 分布 |
| 14 | **揭露議題數量** | 13 bug（5 🔴）| 15 議題（3 🔴）| 5 議題（1 🔴）| 5 議題（2 🔴 新類型）| **預期 ≤ 5（0-1 🔴 新類型 + 0-3 🟡 + 1-2 🟢）** — 若 ≥1 🔴 = v5 也踩 infinite loop pattern 訊號 / 若 0 🔴 = 路線 D 採用實證 | 試驗期間 Aria 紀錄 |
| 15 | **v5 ROI 量化 vs v4 連續 3 Trial 揭 6 🔴 infinite loop**（戰略大重評估關鍵）| baseline | 6 🔴 / +80% cost | 6 🔴 / -90% 中斷 | 6 🔴 / 卡死 | **路線 D 採用實證**：v5 ROI ≥ v5 baseline + Phase 1 完整 + 0 🔴 / **戰略大砍實證**：v5 也踩 infinite loop / cost 爆 / Phase 1 0 deliver | Aria 結案矩陣分析 |

---

## 五、Aria 自驗 SOP

### 5.1 分工矩陣（沿用 Trial_v6/v7/v8）

對齊 Trial_v8 5.1 — Aria 做 DB metrics + Bot log scan + commit/PR 結構分析 + 五向對照數字報告 + 異常 pattern detection + Trial 健檢報告初稿；Christ 做 UAT + 業務正確性 + AI Agent 主觀品質判斷 + Aria 異常報告 OK/NOK 拍板。

### 5.2 觸發點（沿用 + v5 端對端 chain + FF 五十九/六十一 觀察點擴展）

1. **Stage milestone 觸發**：每個 Petra 動態決策 step（DecideAsync / Worker dispatch / per-task session 寫入）完成時 Aria 跑健檢 SQL + log scan + 五向對照 baseline 報告
2. **異常觸發**：BossInteraction 開啟 / Petra 困惑 escalate（FF 五十九 hand-off 失效訊號）/ 端對端 chain 中斷（限制 (b) workaround 失效）/ adapter dispatch unknown capability / FF 六十一 4 點 simplification 真實影響 → Aria 立刻濃縮報告
3. **試驗結案觸發**：Trial 完成 → Aria 寫 Trial_v9 結案報告草稿 + **主動提醒進戰略大重評估深度討論**（路線 A vs B vs C vs D 拍板實證）

### 5.3 自驗工具清單

對齊 Trial_v8 5.3（Bash SQL via `docker exec aiteam-postgres-1 psql` / docker logs / git log / Chrome MCP / Read-Grep）。**環境細節 reference**：
- **container 名**：`aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
- **Bot Internal API port**：`5052`（見 `docker-compose.prod.yml`）
- **X-Api-Key 取值**：`docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- **DB schema**：`src/AiTeam.Data/Entities.cs` + Migrations + **新表 `PetraSessions` + `PetraSessionMessages`**（Stage 63B Migration `Stage63PetraSessionTables`）
- **SQL 欄位 PascalCase + quote 紀律**

### 5.4 Trial 健檢報告格式（五向對照）

對齊 Trial_v8 5.4 升級為 **15 維度五向對照**（Trial_v5 / v6 / v7 / v8 / v9 now）+ v5 動態架構觀察段：

```markdown
## Trial_v9 健檢報告 — <Checkpoint N> @ YYYY-MM-DD HH:MM

### 當前狀態
- TaskGroup ID：<guid>
- PetraSession ID：<guid>
- 已 dispatch worker：DevAgentService ✅ / ReviewerAgentService ✅ / ...
- 端對端 chain 真實生效：✅ / ⚠️ adapter fallback / ❌ 卡死
- FF 五十九 hand-off 狀態：✅ Petra 0 困惑 / ⚠️ escalate 1 次（檢查 reasoning）

### 15 維度 SQL 數字（五向對照 Trial_v5 / v6 / v7 / v8 / v9 now）
| 維度 | Trial_v5 | Trial_v6 | Trial_v7 | Trial_v8 | Trial_v9 (now) | 差異 | 解讀 |

### v5 動態架構觀察段
- Petra 動態決策軌跡：<DecideAsync capability 序列 + 三 trigger 命中分析>
- Worker dispatch chain：<adapter dispatch capability + IClaudeCodeService method call>
- per-task session 持久化：<PetraSession.Status + PetraSessionMessages row count>
- FF 六十一 4 點 simplification 影響：<ResumeAsync / Dev model 共用 / sync method 真實踩 vs 不踩>

### 異常揭露
- [若有] 異常 1：<描述> → 分類：<v4-pattern 復發 / v5 新類型 / FF 六十一 真實踩> → Aria 推薦：<繼續觀察 / escalate Christ>

### Christ 拍板項
- 議題 1：<具體問題 + 三選項 + Aria 推薦>

### 建議下一步
- Aria 自驗 OK / Christ UAT 拍板 / 進入下一階段
```

---

## 六、後續行動清單

### Trial_v9 結案後（Aria 主動觸發）

1. **`/aria-trial-summary` 結案** — Aria 寫 Trial_v9 結案報告（`docs/experiments/Trial_v9_Plan.md` v2.0 結案紀錄含 5 向對照數據 + 揭露議題分類 + v5 ROI 量化結論）
2. **更新 Future_Feature.md + Future_Feature_changelog.md** — FF 三十六 status 升「PoC ✅ + Trial ✅」+ 新議題立 FF 候選
3. **戰略大重評估深度討論** — Aria 主動提醒進「路線 A vs B vs C vs D」拍板實證討論（Christ 2026-05-10 拍板對齊精神延續）

### 戰略大重評估拍板實證後續

依 Trial_v9 揭露結果分三路徑：

**🟢 路線 D 採用實證**（v5 ROI ≥ v5 baseline + Phase 1 完整 deliver + 0 🔴 新類型）：
- Stage 64+ 全量遷移啟動（v4 ~16K LoC 廢棄 + 7 partial CLAUDE_*.md 完整重寫 + production 切 default flag）
- FF 六十一 4 點 production simplification 補強進 Stage 64+ 範圍
- 預期 Stage 64+ 多 Stage 累積完成 v3 → v5 major bump

**🟡 v5 部分採用實證**（Phase 1 完整但揭 1-2 🔴 新類型 + cost 偏高）：
- Aria 戰略大重評估深度討論評估「v5 部分採用 / 修補 v4 hybrid」可行性
- Stage 64+ 評估含 FF 六十一 補強 + 修 Trial_v9 揭露 🔴

**🔴 路線 D 失敗實證**（v5 也踩 infinite loop / cost 爆 +200% / Phase 1 0 deliver）：
- feature/v5-poc branch 不 merge / FF 六十一廢棄 / Stage 64+ 廢棄
- Aria 戰略大重評估深度討論評估路線 A（繼續 v4 修補但既有 6 🔴 訊號）/ B（戰略大砍複雜度）/ C（Claude Code 模式 — 1 Agent + 工具）
- Christ 拍板新方向

---

## 七、規模 / cost / 時程預估

- **Aria session context 預估**：~250-350K（對齊 Trial_v8 ~300-400K — Trial_v9 額外 v5 動態決策軌跡觀察 + 5 向對照 + FF 六十一 simplification 影響 + FF 五十九 hand-off 真實效果觀察 略多）
- **LLM cost 預估**：**$5-15**（對齊 Trial_v6 baseline ±50% — Petra Gemini Flash AI Studio 免費 tier 省 Petra LLM call / Workers 真打 Anthropic Claude cost 來源 / 7 Worker hardcoded Dev model 偏高 +30-50% buffer）
- **持續時間預估**：30-60 分鐘（Trial 短任務模式 — 對齊 Trial_v6 ~50 分鐘 baseline）
- **Aria 工作量預估**：5-8 健檢報告（CP1-CP8 對應 Stage milestone + 異常觸發）+ Trial_v9 結案第二段（Plan v2.0 + 新 FF 評估 + Future_Feature 三檔同步 + memory 寫入 + calibration_anchors + **戰略大重評估資料整合報告**）

---

## 八、技術約束

- **環境細節 source of truth**（對齊 workflow_aria.md 第 7 條紀律）：
  - Bot Internal API port `5052` 見 `docker-compose.prod.yml`
  - X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
  - container 名 `aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
  - Stage 63B 新表 `PetraSessions` + `PetraSessionMessages` PascalCase quote 必加
  - feature flag `Workflow:UsePetraOrchestratorV5` 對應 AppSettings DB 既有 5 flag pattern（WorkflowSettings.cs / WorkflowSettingsResolver.cs）
- **Trial_v9 在 feature/v5-poc branch 跑 / main 0 動** — Christ 切 branch 後 docker compose 重啟對齊
- **Petra Provider 切 Gemini Flash** — Dashboard Agent 設定頁 PM 卡片切換 + AI Studio 免費 tier 對齊 Stage 63A 真打驗證
- 對齊 Trial_v2-v8 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板（Christ 2026-05-11）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-12 | 初版試驗計劃書建立（Aria）— Trial_v9 = v5 動態架構 PoC 真實任務驗證 + 戰略大重評估路線 D 拍板實證。**戰略脈絡**：Stage 62 Charter ✅ + Stage 63A API spike ✅ 硬通過 + Stage 63B PoC ✅ Mock 全綠 三階段累積 derisk 後第一次真實任務試驗。**4 試驗目的**：① 驗證 Stage 63B PoC 路線 A 端對端真實生效（FF 六十一 點 1 漏測對齊）② FF 五十九 hand-off 真實效果（Petra 不困惑 escalate）③ 5 向對照量化 v5 ROI vs v4 連續 3 Trial 揭 6 🔴 infinite loop pattern ④ 戰略大重評估「路線 D 採用 vs A/B/C」拍板實證。**任務需求**：沿用 Trial_v6/v7/v8 同 prompt（5 向對照精準度最高）。**操作前置條件 5 條**：API 餘額 ≥ $30 / Bot 切 feature/v5-poc branch / 切 feature flag UsePetraOrchestratorV5=true / 切 Petra Provider→Gemini Flash / Christ 從 Discord 或 Dashboard 送出指令。**15 維度五向對照預期觀察**含 v5 新類型維度（Petra 動態決策準度 / 端對端 chain 真實生效 / FF 五十九 hand-off / per-task session 持久化 / Worker capability dispatch / FF 六十一 4 點 simplification 影響觀察 / v5 ROI 量化戰略大重評估關鍵）。**Aria 自驗 SOP**沿用 Trial_v6-v8 + v5 端對端 chain + FF 五十九/六十一 觀察點擴展。**規模**：Aria context 預估 250-350K / LLM cost $5-15 / 30-60 分鐘 / 5-8 健檢報告。**戰略主軸**：Trial_v9 結果 = Christ 拍板路線 A vs B vs C vs D 關鍵實證 — 路線 D 採用 → Stage 64+ 全量遷移 + FF 六十一補強 / 非路線 D → feature/v5-poc 不 merge + FF 六十一廢棄 + Aria 主動戰略大重評估深度討論。 |
| v2.0 | 2026-05-13 | **試驗結案 + 觀察紀錄**（Aria）— Trial_v9 = ⭐ **戰略級成功 vs 業務級失敗（雙面，第 4 次連續 Trial）**。**真實 lifecycle 跨 2 日 9 PetraSession** — Christ 從 Dashboard 送 + 後 Aria 直接透過 Internal API curl 送（CLAUDE.md 自主執行原則對齊）。**累積 5 fix 進 feature/v5-poc**：① TurnToken 漏（commit `ac048ef`）② workingDir config key LocalWorkRoot → WorkspacePath（`12c952a`）③ workspace 容器內手動 init（spike 階段堪用 / docker recreate 後不持久）④ adapter token_logs 寫入 + LogWorkflowEvent 加 AgentResponseUpdateEvent（`8dbe8f6`）⑤ adapter ctor TokenLogService nullable fix CS7036 build error（`de77d2a`）。**真實 cost 累積 ~$0.63**（Cody 第 5 次 success $0.571 + Cody 第 4 次 fail $0.059 + Petra Gemini Flash 9 次 ~$0.015 / Anthropic API 5xx transient 第 4 次踩 confirmed 重試成功）— vs 預期 $5-15 偏低 -90%（同 Trial_v7 中斷模式 cost 量化失真 — 0 deliverable 模式）。**最終跑通 PetraSession `0228e131` 5:19 min Cody success 但 0 deliverable**（workspace git status clean / 0 commit / 0 PR / 0 file write — Cody 跑了 5 分鐘 + 花 $0.57 + workspace 完全沒動）。**揭真實 root cause** — `PetraWorkerHelper.BuildAgent` 給 Worker 的 instructions 只一行 generic 描述（「你是 Cody — Code Implementation Worker。負責依任務 input 寫程式碼。」），對比 v4 既有 production Worker prompt（CLAUDE.md 注入 / git workflow / branch 創建 / commit / push 等完整 production-grade 規範）— v5 PoC 只用 generic 一句沒對齊 → Cody 純 reasoning + 探索 codebase 但完全沒做事就 exit success。**揭 12+ 議題清單**（連續 9 PetraSession 累積）：① Gemini API key 沒設（前置條件 1）② Gemini v1 API systemInstruction unsupported（v1 vs v1beta endpoint 不對齊）③ TurnToken trigger 漏 ✓ 修 ④ workingDir config key 不對齊 ✓ 修 ⑤ workspace 容器內不存在 ✓ 手動 init ⑥ workspace docker recreate 後不持久（FF 六十一新點補強候選）⑦ Stage 63A spike limitation (b) 是誤判（真實 root cause = TurnToken 漏，不是 framework dispatch fail — Charter 02/04 + spike notes errata 紀錄改寫）⑧ token_logs fail path Usage=null 沒寫 ✓ 修（部分）⑨ AgentResponseUpdateEvent 沒 cover ✓ 修 ⑩ Anthropic API 5xx Internal server error transient（不可控 / 重試 OK）⑪ BuildSequential 4 worker chain 仍只 1 worker dispatch 真實 break loop（framework protocol deep dive 待）⑫ **Worker prompt 太簡 → 0 deliverable**（FF 六十一新揭點 8）。**戰略級成功 — v5 真實 work 證實**：技術鏈完整 fire（Petra Gemini Flash DecideAsync → BuildSequential → ChatClientAgent → ClaudeCodeChatClientAdapter → ClaudeCodeService subprocess → Claude Code CLI 5 turns reasoning + Anthropic Claude API call 真實 cost）+ Anthropic API 5xx transient confirmed（重試成功）+ Stage 63A 「framework limitation (b)」誤判 root cause 揭真實（漏 TurnToken trigger）— 這個揭過去 Stage 63A spike notes 結論需 errata 改寫，戰略價值極大。**業務級失敗 — 「能跑」≠「能 deliver」精準揭差距**：5 次跑 0 deliverable / 真實 PoC 與 production-grade 工作量差距 visible。**對戰略大重評估的關鍵實證升級**：① v5 動態架構不是設計問題（架構真實 work）② Production-ready 工作量 = Stage 64+ 全量遷移大投資（8 prompt 完整重寫 + framework dispatch protocol 解 + workspace 持久化 + retry path + 9+ FF 六十一補強整套）③ 連續 4 Trial 揭 18 🔴 + 第 4 次跑 0 deliverable = infinite loop pattern 真實實證（v4 連續 3 Trial 揭 6 🔴 / v5 第一輪揭 12+ 議題 — 修補無止境）④ Christ 拍板路線 D vs A vs B vs C 的 ROI 評估資料**充分**。**Aria 主動於 Trial_v9 結案後提醒戰略大重評估深度討論**（Christ 2026-05-10 拍板對齊精神延續）。**FF 動態**：FF 六十一補強清單擴 4 點 → 12+ 點完整 production-grade 議題清單（留 Stage 64+ 路線 D 採用才動工）。**Aria 工作節奏觀察**：Trial 跨日 9 PetraSession 真實揭「戰略大重評估資料蒐集 vs 修補無止境」trade-off — 修法每輪 30-60 min（commit + deploy + 重跑 + 觀察） / 揭 1-2 新議題 / Christ 在線等 — 對齊「對冗餘不容忍」紀律應於早期 (Trial_v9 第 3 次跑後) 結案進戰略大重評估。**自省點候選 #32**（Trial 紀律延伸）：Trial 修補連續 5 次後 cost / 戰略邊際遞減訊號明顯 → Aria 主動推薦結案進戰略大重評估比繼續修補有更高戰略價值。**Aria 校準錨待 Christ 補 Aria session context 數字**（Trial 跨日真實 context 累積 + 多輪 polling + 5 commit 修法 + 多次 sleep + 多次 SQL 查詢 — 預估遠超 Trial_v8 ~300-400K baseline）。 |

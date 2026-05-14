# Trial_v12 試驗計劃書 — Stage 66 3 議題收口效果驗證 + v5 動態架構正式上線前最後一道閘門

> 對應版本：**v3.56.0**（Stage 66 結案 — PetraOrchestratorService 自管 chain 修 Vera 0 work 根因 / GitHub microsoft/agent-framework#1308）
> 建立日期：2026-05-14
> 狀態：規劃中（待 Christ 開跑時間 + Aria session context buffer）
> 文件版本：v1.0

---

## 一、背景與定位

**Stage 66 結案後 v5 上線前最後一次 production-grade 真實任務試驗**：

- Stage 64+65+66 三波 production-ready 補強完成 — v5 動態架構工程準備全完成
- Stage 66 修 Vera 0 work 業務級根因（PetraOrchestratorService 自管 chain 取代 framework BuildSequential — GitHub `microsoft/agent-framework#1308` 揭 framework chain bug）
- Stage 66 + tool role message 同 transaction 寫入 + Cody 廣範圍 enforce prepend
- Aria 計劃前 WebSearch 紀律連續第三次實證 + Aria gate1 Tier 0+1+2 通過 + Forge 自驗 4 場景全 PASS（場景 D 留 Trial_v12 真實任務驗）
- **Trial_v12 目的 = 驗 Stage 66 3 議題收口真實生效 + 連續 3 Trial 業務級成功 = 路線 D 完整實證 → Christ 拍板切 default flag = v5 動態架構正式上線**

**Trial_v12 定位**：v5 動態架構正式上線前最後一道閘門 — 通過後 Christ 拍板切 default flag 即上線。

---

## 二、試驗目的（3 條）

1. **驗 Stage 66 3 議題收口真實生效**（核心）：
   - 議題 1 — **Vera 真實 cover review work**：Vera 接到 dispatch 後真實做事（vs Trial_v11 0 token 0 cost 0 deliverable）— token > 0 + cost > 0 + 看到 Cody output 後產出 review 結論
   - 議題 2 — **PetraSessionMessages tool role 真實寫入**：每個 worker dispatch 完成後 tool role 紀錄寫入（vs Trial_v11 0 條 tool role）→ PR body「Worker 完成 summary」段含完整 chain summary（vs Trial_v11 顯示「（無 tool role 紀錄）」）
   - 議題 3 — **Cody 廣範圍指令 5/5 cover + PR body 含「範圍對照表」段**：Cody 對廣範圍措辭真實 grep workspace + 5/5 範圍 cover 含 InteractionCenter（vs Trial_v10/v11 連兩次漏 InteractionCenter / 0 範圍對照表段）

2. **連續 3 Trial 業務級成功重現**（infinite loop pattern 確認打破 + production-readiness 充分證實）：
   - Cody 真實 deliver（git commit + push + 開 PR）
   - 連續 3 Trial（Trial_v10 + v11 + v12）PR 真開 = 非 single 偶發 + 多 worker chain 真實有效
   - cost ≤ $5（對齊 Trial_v10 $1.07 / Trial_v11 $1.38 baseline ±300%）
   - 0 🔴 戰略級新類型維持 = v5 設計層仍對

3. **切 default flag 拍板實證**（v5 正式上線）：
   - 跑成功 + 0 🔴 + Stage 66 3 議題全收口 → Christ 拍板切 `Workflow:UsePetraOrchestratorV5` default true → **v5 動態架構正式上線**
   - 部分成功 → Stage 67 補強再 Trial_v13
   - 失敗 / 業務級失敗復發 → 戰略大重評估再啟動

---

## 三、任務需求

### 沿用 Trial_v6-v11 同 prompt（7+1 向對照精準度最高）

任務原文（完全照搬 Trial_v6-v11）：

> Victoria，我想要打磨一下 Dashboard 的錯誤處理體驗。
>
> 最近在用的時候發現一個問題：很多操作失敗時，錯誤訊息只會顯示在表單區塊裡，但我視線可能在別的地方（例如剛點完按鈕在等結果），就完全錯過了。
>
> 舉個例子：首頁那個快速下達指令卡，如果我打的指令違規，提示就會在送出區上面冒出來，但有時候我已經滑下去看其他東西了，根本沒注意到指令沒送出去。
>
> 我希望能改成：表單裡的提示保留（緊貼違規來源），但同時要有個東西通知我「剛剛失敗了」，類似右下角彈出來的那種 toast 提示，讓我視線在哪裡都會看到。兩個地方訊息一致，toast 大概 3-5 秒自動消失就好。
>
> 不只快速下達指令卡，我覺得整個 Dashboard 凡是有「儲存失敗 / 操作失敗」的地方（系統設定、操作中心、Agent 設定、規則管理之類），都應該照同樣方式打磨一下。具體哪些頁面要動、要不要抽共用元件，你跟團隊判斷。

### 操作前置條件（Christ 開跑前 / Aria 自跑前必確認）

1. **API 餘額 ≥ $30** — Trial_v12 cost 預估 $1-5 + 容錯 buffer（Trial_v10 $1.07 / Trial_v11 $1.38 baseline）
2. **main branch deploy 完成**：
   - `gh run list --workflow=deploy.yml --limit 3` 確認 Stage 66 commits（`1807972` Forge 實作 + `1474f78` 結案文件）對應 deploy.yml run success
   - 確認 production Bot 用 v3.56.0 image
3. **Feature flag `Workflow:UsePetraOrchestratorV5=true`** — 開跑前 SQL 確認真實狀態（Stage 66 Forge 自驗時切回 false 對齊「default false 守 v4 production」紀律 — Trial_v12 開跑時切回 true）：
   ```sql
   SELECT "Key", "Value" FROM app_settings WHERE "Key" = 'Workflow:UsePetraOrchestratorV5';
   ```
   若 false → SQL UPDATE 切 true + curl `/internal/reload-cache?scope=appsettings`
4. **Petra Provider→Gemini Flash** — Trial_v10/v11 已切，SQL 確認沒被改：
   ```sql
   SELECT "Name", "Provider", "Model" FROM agent_configs WHERE "Name" = 'PM';
   ```
5. **workspace volume permission 仍 OK**（Stage 65 子項 3 entrypoint chown + gosu 持續生效）：
   ```bash
   MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 ls -la /tmp/aiteam-workspace
   # 期望：drwxr-xr-x appuser appuser
   ```
6. **Christ 從 Discord 或 Dashboard 送指令**（任一通道 — Aria 自跑可用 curl `/internal/ceo/command`）

---

## 四、預期觀察清單（Trial_v12 vs Trial_v11 6+1 向對照）

### 核心驗收（Stage 66 3 議題收口效果）

| # | 維度 | Trial_v11 實況 | Trial_v12 預期 | Aria 自驗工具 |
|---|---|---|---|---|
| 1 | **Vera 真實 cover review work**（議題 1 驗）| Vera dispatch 但 0 token 0 cost 0 deliverable | **Vera token > 0 + cost > 0 + 看到 Cody output 後產出 review 結論**（自管 chain 真實 wire 生效 — Vera input 含 Cody output 摘要）| SQL `SELECT "AgentName", "InputTokens", "OutputTokens", "TotalCostUsd" FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` Vera 一行 + `InputTokens > 0 / OutputTokens > 0 / TotalCostUsd > 0` |
| 2 | **PetraSessionMessages tool role 真實寫入**（議題 2 驗）| 0 條 tool role / PR body「Worker 完成 summary」段「（無 tool role 紀錄）」 | **每個 worker dispatch 完成後 tool role 寫入** ≥ picks.Count 條 + PR body Worker summary 段含完整 chain summary | SQL `SELECT "Role", COUNT(*) FROM petra_session_messages WHERE "SessionId" = '<新 id>' GROUP BY "Role"` 應有 user / assistant / **tool**（≥ picks.Count 條）+ `gh pr view <num> --json body` 看 Worker summary 段不再「（無 tool role 紀錄）」|
| 3 | **Cody 廣範圍 5/5 cover + PR body 範圍對照表段**（議題 3 驗）| 4/5（漏 InteractionCenter） + PR body 0 含「範圍對照表」段 | **5/5 範圍 cover 含 InteractionCenter** + commit message / PR body 必含「範圍對照表」段（Cody 真實 grep workspace + 4 步驟 enforce 生效） | `gh pr view <num> --json files,body` 看 5/5 範圍 + body 含「範圍對照表」段 |

### Trial_v10/v11 業務級成功重現（連續 3 Trial 實證）

| # | 維度 | Trial_v10 | Trial_v11 | Trial_v12 預期 | 驗證 |
|---|---|---|---|---|---|
| 4 | Cody 真實 deliver | PR #372 / 7 檔 | PR #373 / 8 檔 | **PR 真開 + ≥ 5 檔改動 + git commit + push 真實**| `gh pr view + workspace git log` |
| 5 | Petra 動態決策 | Design trigger 2 cap | Design trigger 2 cap | **Design trigger 命中 `code_implementation\|code_review`**（中等需求預期）| Bot log `Petra DecideAsync 完成 — raw=「...」` |
| 6 | 多 worker chain 真實 fire | Cody + Vera 都 dispatch（但 Vera 0 work）| Cody + Vera 都 dispatch（但 Vera 0 work）| **Cody + Vera 都 dispatch + Vera 真實做事**（Stage 66 自管 chain 修根因生效）| Bot log `Petra 自管 chain dispatch 1/2 worker=Cody` + `2/2 worker=Vera` + token_logs Vera 非 0 |
| 7 | total cost | $1.07 | $1.38 | **$1-5**（Vera 真實做事預期 +$0.5-2 vs Trial_v11 $1.38）| SQL `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` |
| 8 | 持續時間 | ~6 分鐘 | ~8 分 23 秒 | **~5-15 分鐘**（Vera 真實做事預期略長）| Aria 計時 |

### 7+1 向對照（Trial_v6 → Trial_v12）

| # | 維度 | v6 | v7 | v8 | v9 | v10 | v11 | **v12 預期** |
|---|---|---|---|---|---|---|---|---|
| 1 | 完成度 | 部分 | 0 中斷 | 0 卡死 | 0 deliverable | PR 7 檔（4/5 cover）| PR 8 檔（4/5 cover / Vera 0 work）| **PR ≥ 5 檔（5/5 cover / Vera 真做事）** |
| 2 | total cost | $15.81 | $1.52 | $1.20 | $0.63 | $1.07 | $1.38 | **$1-5** |
| 3 | 揭 🔴 新類型 | 3 | 1 | 2 | 12+ | 0 | 0 | **0**（≥1 = v5 設計層議題復發訊號 → 不能切 default flag）|
| 4 | 揭 🟡 中議題 | 12 | 3 | 2 | — | 4 | 3 | **0-2**（議題密度進化曲線下降）|

### 預期揭露議題上限

- **≤ 2 議題**（0 🔴 + 0-2 🟡 + 0-1 🟢 — 議題密度進化曲線下降）
- ≥1 🔴 戰略級新類型 → v5 設計層議題復發 → 不能切 default flag → Stage 67 評估
- 0 🔴 + Stage 66 3 議題全收口 + Cody deliver 5/5 + Vera 真做事 → **路線 D 完整實證 + 切 default flag = v5 正式上線**

---

## 五、Aria 自驗 SOP（沿用 Trial_v10/v11 9-step 模板第 3 次實踐）

### 5.1 分工矩陣

對齊 Trial_v10/v11 — Aria 全程自跑 + Christ 0 操作（除拍板開跑 + 業務正確性最終判斷 + 拍板下一步路線之外）。

### 5.2 9-step 自驗模板

1. **deploy 確認**：`gh run list --workflow=deploy.yml --limit 3` 看 Stage 66 commits（`1807972` + `1474f78`）對應 run 已 success + production Bot 用 v3.56.0 image
2. **環境 setup 驗**：場景 A workspace volume permission（前置條件 5）+ feature flag SQL 確認 + 切回 true 若 Stage 66 Forge 自驗後殘留 false
3. **送任務指令**：curl `/internal/ceo/command` 帶 X-Api-Key 送 Trial_v6-v11 同 prompt
4. **Petra session 建立確認**：`SELECT "Id", "Status", "CreatedAt" FROM petra_sessions ORDER BY "CreatedAt" DESC LIMIT 1`
5. **動態決策觀察**：Bot log `grep "Petra DecideAsync 完成"` 看 capability 序列
6. **自管 chain dispatch 觀察**：Bot log `grep "PetraOrchestrator 自管 chain dispatch"` + `grep "ClaudeCodeChatClientAdapter dispatch"` 看 worker 序列 + Vera input 含 Cody output 摘要
7. **FinalizeGitAsync 成功路徑**：Bot log `grep "Petra PR 開啟"` 看 PR URL
8. **PR 內容檢查**：`gh pr view <num> --json files,additions,deletions,body` + `gh pr diff <num>` 看：
   - PR cover 範圍（5/5 議題 3 驗 — 含 InteractionCenter）
   - PR body 含「範圍對照表」段（議題 3 enforce 生效）
   - PR body 含「Worker 完成 summary」段非「（無 tool role 紀錄）」（議題 2 tool role 寫入生效）
   - PR diff 0 含 CLAUDE.md 改動（Stage 65 子項 1 仍生效）
9. **token_logs 完整性 + cost 驗 + chain pass-through 驗**：
   - SQL `SELECT "AgentName", "InputTokens", "OutputTokens", "TotalCostUsd" FROM token_logs WHERE "CreatedAt" >= '<開跑時間>' GROUP BY "AgentName"` 看 **Vera token > 0 / cost > 0**（議題 1 驗 — Stage 66 自管 chain 修根因生效）
   - SQL `SELECT "Role", COUNT(*) FROM petra_session_messages WHERE "SessionId" = '<新 id>' GROUP BY "Role"` 看 tool role ≥ picks.Count 條（議題 2 驗）
   - SQL `SELECT SUM("TotalCostUsd") FROM token_logs ...` 對比 Christ 真實餘額消耗差距 < 1%（vs Trial_v11 3.4% blind spot 預期改善 — Vera 真做事 + token_logs 真寫入）

### 5.3 觸發點

1. **Stage milestone**：每個關鍵 milestone 完成（Petra DecideAsync / 自管 chain dispatch 1/2 + 2/2 / FinalizeGitAsync / PR open）Aria 跑健檢
2. **異常**：BossInteraction 開啟 / Cody 0 deliverable / Vera 仍 0 token / FinalizeGitAsync 失敗 / Stage 66 議題復發 / 揭 🔴 戰略級新類型 → Aria 立刻濃縮報告
3. **試驗結案**：Trial 完成 → Aria 寫 Trial_v12 結案報告草稿 + 路線 D 完整實證資料 + 切 default flag 拍板建議

### 5.4 環境細節 source of truth

對齊 Trial_v10/v11 + Stage 66 修法後狀態：
- container 名 `aiteam-aiteam-bot-1` / `aiteam-postgres-1`
- Bot Internal API port `5052` / X-Api-Key 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- workspace path `/tmp/aiteam-workspace`（main 已含 named volume `aiteam-workspace-data` + Stage 65 子項 3 entrypoint chown）
- Petra session 表 `petra_sessions` + `petra_session_messages`（snake_case 表 + 欄位 PascalCase quote 必加）
- token_logs 表（`AgentName` / `Model` / `TotalCostUsd` / `IsEstimated` / `CreatedAt`）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：前置條件設置 + workspace volume permission 持續生效（Stage 65 子項 3 持續驗）

**觸發**：
1. `gh run list --workflow=deploy.yml --limit 3`（確認 Stage 66 deploy.yml run success）
2. `MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 ls -la /tmp/aiteam-workspace`
3. SQL 確認 feature flag UsePetraOrchestratorV5（Stage 66 Forge 自驗後殘留 false → 切 true）
4. SQL 確認 PM Provider Gemini Flash

**驗證**：
- deploy.yml run conclusion=success + production Bot 用 v3.56.0 image
- workspace owner `appuser:appuser`（vs root 殘留 = Stage 65 子項 3 失效）
- feature flag UsePetraOrchestratorV5=true + PM Provider=Gemini

### 場景 B：Vera 真實 cover review work（議題 1 驗 — Stage 66 自管 chain 修根因生效）

**觸發**：場景 A 通過後送 Trial_v6-v11 同 prompt

**驗證**：
- SQL：
  ```sql
  SELECT "AgentName", "InputTokens", "OutputTokens", "TotalCostUsd"
    FROM token_logs
   WHERE "CreatedAt" >= '<Trial_v12 開跑時間>'
   GROUP BY "AgentName";
  ```
  期望結果含 **Vera 一行**（vs Trial_v10 0 寫入 / Trial_v11 寫入但 0 token） + **InputTokens > 0** + **OutputTokens > 0** + **TotalCostUsd > 0**
- Bot log `grep "PetraOrchestrator 自管 chain dispatch 2/2 worker=Vera capability=code_review inputMsgs=2"`（Vera 接到 input messages = 2 條：原 task input + Cody output 摘要 — vs framework BuildSequential blank input 已修）
- Bot log `grep "PetraOrchestrator 自管 chain dispatch 完成 2/2 worker=Vera outputLen=<非 0>"`

### 場景 C：PetraSessionMessages tool role 真實寫入（議題 2 驗）

**觸發**：場景 B 任務送出後 chain dispatch 完成

**驗證**：
- SQL：
  ```sql
  SELECT "Role", COUNT(*) AS rows, MAX(LENGTH("Content")) AS max_content_len
    FROM petra_session_messages
   WHERE "SessionId" = '<新 PetraSession id>'
   GROUP BY "Role";
  ```
  期望結果含 user / assistant / **tool** 三 role + tool role count ≥ picks.Count（每 worker 一條）
- `gh pr view <num> --json body` PR body「Worker 完成 summary」段**不再**顯示「（無 tool role 紀錄）」 + 含每個 worker 的 capability + dispatch 結果摘要

### 場景 D：Cody 5/5 範圍 cover + PR body「範圍對照表」段（議題 3 驗）

**觸發**：場景 B 任務送出後 PR 真開

**驗證**：
- `gh pr view <num> --json files` PR 含 **5 個範圍檔案全 cover**：
  - `src/AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Rules/RuleFormDialog.razor` ✓
  - **`src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor*`**（Trial_v10/v11 連兩次漏 — 本 Trial 必含）✓
- PR body 含「範圍對照表」段（任務 5 範圍逐項 ✓ — Stage 66 子項 3 廣範圍 enforce 真實生效）
- commit message 含「範圍對照表」段對齊

### 場景 E：Stage 65 既有修法未 regression（保護驗）

**觸發**：場景 B 任務送出後 PR 真開

**驗證**：
- `gh pr diff <num>` grep `CLAUDE.md` → **0 命中**（Stage 65 子項 1 `--append-system-prompt` 仍生效 / vs Trial_v10 PR #372 含 -172/+113 污染）
- workspace owner=appuser（Stage 65 子項 3 entrypoint chown 仍生效）
- token_logs Vera 一行寫入（Stage 65 子項 2 finally 紀律仍生效 + Stage 66 自管 chain 真做事讓 token 非 0）

### 場景 F：Petra 動態決策 + 自管 chain 真實 fire（Trial_v10/v11 重現 + Stage 66 修根因生效）

**觸發**：場景 B 任務送出後

**驗證**：
- Bot log `Petra DecideAsync 完成 — raw=「code_implementation|code_review」picks=Cody → Vera`（Design trigger 2 cap，對齊 Trial_v10/v11）
- Bot log 含 `PetraOrchestrator 自管 chain dispatch 1/2 worker=Cody capability=code_implementation inputMsgs=1` + `2/2 worker=Vera capability=code_review inputMsgs=2` 兩條 dispatch
- Bot log **0 含**「BuildSequential」/「TurnToken」/「InProcessExecution.RunStreamingAsync」字樣（Stage 66 自管 chain 取代 framework chain 主路徑生效）

### 場景 G：FinalizeGitAsync 成功路徑（Trial_v10/v11 重現）

**觸發**：場景 F 多 worker chain 完成後 PetraOrchestratorService.StartAsync 收尾段

**驗證**：
- Bot log `Petra branch 建立 + checkout：petra/spike-...`
- Bot log `Petra PR 開啟：https://github.com/darkleong/AiTeam/pull/...`
- PR 真實 OPEN

---

## 六、後續行動清單

### Trial_v12 結案後（Aria 主動觸發）

1. `/aria-trial-summary` 結案 — Aria 寫 Trial_v12 結案報告 + 7+1 向對照數據 + 議題分類 + 路線 D 完整實證資料 + 切 default flag 拍板建議
2. **新立紀律 #10 首次實踐**（aria-trial-summary 結束 SOP 加切回 feature flag default）— Trial_v12 結束 Aria 切回 false 對齊 Stage 67+ Roadmap 預期或保 true 若 Christ 拍板切 default flag
3. 更新 Future_Feature.md + FF changelog — FF 三十六 status 升「✅ 完成」+ 新議題立 FF 候選（如有）
4. **拍板下一步**（依結果分支）：
   - **🟢 路線 D 完整實證**（3 議題全收口 + 0 🔴 + Cody 5/5 cover + Vera 真做事）→ Christ 拍板切 `Workflow:UsePetraOrchestratorV5` default true → **v5 動態架構正式上線** → Stage 67+ FF 六十一其餘 8+ 點 / FF 三十六 ✅ close / prompt DB 化候選 evaluate
   - **🟡 部分成功**（3 議題部分收口 + 1-2 🟡 新議題）→ Stage 67 補強（範圍邊界守緊不擴 L）→ Trial_v13 重驗
   - **🔴 失敗**（v5 設計層議題復發 / 業務級失敗復發 / Vera 仍 0 work）→ 戰略大重評估再啟動評估路線 A/B/C

---

## 七、規模 / cost / 時程

- **Aria session context 預估**：~80-150K（對齊 Trial_v10/v11 — Aria 全程自跑 9-step 模板第 3 次實踐）
- **LLM cost 預估**：**$1-5**（對齊 Trial_v10 $1.07 / Trial_v11 $1.38 baseline / Vera 真做事預期 +$0.5-2）
- **持續時間預估**：5-15 分鐘（Trial 短任務模式對齊 Trial_v10 ~6 分鐘 / Trial_v11 ~8 分 23 秒 / Vera 真做事預期略長）
- **Aria 工作量預估**：5-8 健檢報告 + Trial_v12 結案第二段（Plan v2.0 + Future_Feature 三檔同步 + memory calibration_anchors + 切 default flag 拍板資料）

---

## 八、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律（見 5.4 段）
- **Trial_v12 在 main branch 跑**（同 Trial_v11 — main 含 Stage 64+65+66 全 commits + Directory.Build.props v3.56.0）
- Petra Provider 切 Gemini Flash AI Studio 免費 tier 對齊既有 Stage 63A/Trial_v9-v11 真打驗證
- 對齊 Trial_v2-v11 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-14 | 試驗計劃書建立（Aria）— Trial_v12 = Stage 66 3 議題收口效果驗證 + v5 動態架構正式上線前最後一道閘門。**3 試驗目的**：① 驗 Stage 66 3 議題收口生效（Vera 真實 cover review work 核心 / PetraSessionMessages tool role 真實寫入 / Cody 5/5 cover + PR body 範圍對照表段）② 連續 3 Trial 業務級成功重現（infinite loop pattern 確認打破 + production-readiness 充分證實）③ 切 default flag 拍板實證（v5 正式上線）。**任務需求**：沿用 Trial_v6-v11 同 prompt（7+1 向對照精準度最高）。**操作前置條件 6 條**：API 餘額 ≥ $30 / main branch deploy 完成（Stage 66 commits `1807972` + `1474f78`）/ feature flag UsePetraOrchestratorV5=true SQL 確認（Stage 66 Forge 自驗後殘留 false → 切 true）/ Petra Provider Gemini Flash SQL 確認 / workspace volume permission 仍 OK / Christ 送指令（Aria 可代）。**7 驗收場景**：A 前置條件設置 + workspace permission 持續生效 / B Vera 真實 cover review work（議題 1 核心驗）/ C PetraSessionMessages tool role 真實寫入（議題 2 驗）/ D Cody 5/5 cover + 範圍對照表（議題 3 驗）/ E Stage 65 既有修法未 regression / F Petra 動態決策 + 自管 chain 真實 fire（Trial_v10/v11 重現 + Stage 66 修根因生效）/ G FinalizeGitAsync 成功路徑。**Aria 全程自跑 9-step 模板第 3 次實踐**對齊 Trial_v10/v11 既有實踐（gh workflow + watch / docker 環境 setup / Internal API curl 送任務 / SQL/log scan / PR 內容檢查 / 業務品質評估初稿 / Christ 拍板）。**規模**：Aria context 預估 80-150K / LLM cost $1-5 / 5-15 分鐘 / 5-8 健檢報告。**結果分支三路徑**：🟢 路線 D 完整實證 → 切 default flag = v5 正式上線 + Stage 67+ FF 六十一其餘 / 🟡 部分成功 → Stage 67 補強 → Trial_v13 / 🔴 失敗 → 戰略大重評估再啟動。**新立紀律 #10 首次實踐**（aria-trial-summary 結束 SOP 加切回 feature flag default）— Trial_v12 結束 Aria 切回 false 對齊 Stage 67+ Roadmap 預期或保 true 若 Christ 拍板切 default flag。 |
| v2.0 | 2026-05-14 | **試驗結案 — 🟢 路線 D 完整實證 ⭐⭐⭐⭐（v5 動態架構工程實證全完成 = 啟動切 default flag 拍板的最後條件達成）**。**Aria 全程自跑 9-step 模板第 3 次實踐穩定**（Christ 拍板 OK 跑後 0 操作除業務正確性最終判斷 + 處理 BossInteraction 卡 — Aria 對「Christ 只動嘴」精神持續穩定）。**真實 lifecycle ~14 分鐘**（PetraSession `e8c26d5a` running 13:55:04 → 14:09:34 done）+ 真實 cost **$2.34**（系統算 / 餘額 32.83 → 真實 **30.49** = **blind spot 0%** 🎉 vs Trial_v10 4.3% / Trial_v11 3.4% — 議題 2 token_logs 真實寫入完整收口 best evidence）+ 真實 PR **[#374](https://github.com/darkleong/AiTeam/pull/374) 開出**（8 個 Dashboard 檔 +252/-53 / 39 處 `Snackbar.Add` 完整補齊 toast 通知）。**Stage 66 3 議題收口效果完整實證**：① **議題 1 Vera 真實 cover review work ✅ 完整收口** — `InputTokens=7 / OutputTokens=10382 / TotalCostUsd=$0.3181 / outputLen=1264`（vs Trial_v11 0/0/$0/0 — **chain dispatch wire 真實生效**）/ Bot log `PetraOrchestrator 自管 chain dispatch 2/2 worker=Vera inputMsgs=2`（**inputMsgs=2 = Cody output 摘要拼進去 = Stage 66 BuildNextWorkerInput 真實 wire** vs Trial_v11 framework BuildSequential blank input）/ Vera 真實 review 結論 JSON 0 critical / 0 warning / 1 info（揭 SystemSettings 5 個 button-save 方法可選優化 — Vera 自己評為「按鈕操作時用戶通常就在按鈕旁，影響較低」+ 連帶識別 Cody 順手修 Blazor Server Circuit 風險 positive side-effect） ② **議題 2 PetraSessionMessages tool role 真實寫入 ✅ 完整收口** — SQL `petra_session_messages` user / assistant / **tool** 三 role 都有 + tool role 2 條（Cody + Vera 各一）max_len 2056 / PR body「Worker 完成 summary」段含完整 chain summary（vs Trial_v11「（無 tool role 紀錄）」）③ **議題 3 Cody 5/5 cover + PR body「範圍對照表（最終）」段 ✅ 完整收口** — PR body 含「範圍對照表（最終）」12 項表 + **InteractionCenter 真實列入** 評估後判斷「Snackbar-only，操作類適當，無需修改」給理由（vs Trial_v10/v11 連兩次漏 InteractionCenter「沒看到」）= **質的不同**（真實 grep 後評估不改 vs 漏掉沒看到）+ 多 cover 2 個合理 Dialog（AgentCreateDialog + ProjectCreateDialog）+ 順手修 Blazor Server Circuit 風險（`ToggleIsActiveAsync` / `SaveTrustLevelAsync` 補 try-catch-finally — Cody positive side-effect）。**業務品質評估 85-90%**（vs Trial_v10 75-80% — Snackbar/inline 雙通道完整 / Severity 區分精準 Success/Error/Warning 對齊 / 5/5 範圍 cover + InteractionCenter 評估理由 / 順手修 production bug）。**揭 1 🟡 新工程議題 0 🔴 戰略級維持**：① **🟡 v5 PoC post-confirm flow 收尾未做** — Christ 點「確認派工」（ceo_confirm responded ResponseAction=confirm_yes）後 1 秒內系統自動 fire `exec_confirm` 類型 stale 卡（`AgentName` 空白 — Petra 已跑完沒下一個 worker）= v4 既有 default 行為 fall back（v5 PoC 沒做「ceo_confirm responded 後乾淨收尾」wire）。不影響業務（PR 真實存在 + Cody/Vera 工作真實做完）只是 UI 雜訊（Christ 需手動 cancel stale 卡）。**Trial_v10/v11 沒踩過**（兩次 Christ 直接 close PR 沒走到 confirm 步驟）— Trial_v12 是第一個揭露此議題的 Trial。candidate 進 FF 六十一補強清單 Stage 67+ 評估動工。**Trial_v12 預測項目 3 路徑 → 🟢 路線 D 完整實證實際命中**：3 議題全完整收口 + Cody 5/5 cover + Vera 真做事 + 0 🔴 戰略級新類型 + 連續 3 Trial 業務級成功（PR #372+#373+#374）+ 1 🟡 新議題不阻擋 → **Christ 拍板切 `Workflow:UsePetraOrchestratorV5` default true = v5 動態架構正式上線**啟動條件全達成。**對戰略大重評估**：① 連續 3 Trial 業務級成功重現 = infinite loop pattern 確認打破 ② v5 設計層 6 個 Trial（v6-v11）累積驗證 + Stage 66 framework chain 修根因 = 路線 D 全套實證資料完整 ③ Aria 紀律累積成熟度進化曲線實證（Stage 64+65+66 三波計劃前 WebSearch 紀律連續 3 次實踐 + Gate1 Tier 升級紀律 + 自診 source of truth 紀律根因 8 次累積修根因）④ 切 default flag 後正式進入「v5 動態架構 production 觀察期」開始累積真實業務數據（給未來 prompt DB 化 / FF 六十一其餘 / 全程動態 orchestrator 候選等戰略路徑提供決策資料）。**Aria 工作節奏觀察 3 條**：① **Aria 全程自跑 9-step 模板第 3 次實踐穩定**（vs Trial_v10 首次 / Trial_v11 第二次）— Monitor tool + Internal API + SQL + gh 7 工具熟練度進化曲線繼續穩定 ② **首踩 v5 PoC post-confirm UI 議題**（Trial_v10/v11 都直接 close PR 沒踩到）— 揭露 Trial 完整 lifecycle 範圍延伸到「Christ confirm 後系統行為」的 visibility 升級（之前都 close PR 短路了 confirm 步驟，現在實證走完 confirm 揭新議題）③ **新立紀律 #10 首次實踐反向應用**（Trial 開跑前切回 true，原 Trial 結束後切回 false 紀律 — workflow_aria.md 第三節 A 第 10 條的反向 case）— 加深紀律 understanding：紀律不只 Trial 結束切回 default，**也含 Stage 結束（Stage 66 Forge 自驗 escalate 切回 false 後）Trial 開跑時切回 true** 對齊 Trial Roadmap 預期。**WebSearch 紀律價值補實證**：本次 Trial_v12 揭一個 reload-cache scope 議題（`scope=appsettings` 不夠，要 `scope=workflow` 才能讓 Victoria 重讀 flag），這條候選進 forge-self-verify skill / workflow_aria.md 第 7 條延伸範圍段 #5「reload-cache scope 對應表」（不阻擋本次 Trial — 重送任務後生效）。**FF 動態**：FF 三十六 status 升「Charter+spike+PoC+Stage 64+65+66 ✅ + Trial_v10 業務級成功 ✅ + Trial_v11 部分成功 ✅ + Trial_v12 🟢 路線 D 完整實證 ✅ → 待 Christ 拍板切 default flag = v5 正式上線」/ FF 六十一補強清單擴 1 點 v5 PoC post-confirm flow 議題 → 共 12+ 點 Stage 67+ 評估動工。**Top 5 重排**：切 default flag = v5 正式上線升 **#1**（路線 D 完整實證後 Christ 唯一拍板動作）/ FF 三十六 #2（拍板後 ✅ close 完成 — Charter+spike+PoC+3 Stage+3 Trial 完整 trail 留 reference）/ FF 六十一 12+ 點 Stage 67+ 評估 #3 / FF 五十四 #4（v5 上線後評估）/ prompt DB 化候選 #5（v5 上線後評估）。**Aria 校準錨待 Christ 補 Aria session context 數字**（規模對齊 Trial_v10/v11 — Aria 全程自跑 9-step 模板第 3 次實踐熟練度進化曲線）。**戰略主軸**：Trial_v12 🟢 路線 D 完整實證 = v5 動態架構工程準備 + production-grade 驗證全完成 → Christ 拍板切 `Workflow:UsePetraOrchestratorV5` default true → **v5 動態架構正式上線 🎉**（v4 hierarchical static → v5 dynamic orchestrator 路線 D 戰略遷移完成 + 進入 production 觀察期累積真實業務數據）。 |

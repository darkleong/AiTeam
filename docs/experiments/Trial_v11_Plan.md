# Trial_v11 試驗計劃書 — Stage 65 4 議題收口 + Trial_v10 業務級成功可重現驗證（main 上跑）

> 對應版本：**v3.55.0**（Stage 65 結案 + merge feature/v5-poc → main 完成 — main 直接驗證 vs Trial_v10 在 feature/v5-poc）
> 建立日期：2026-05-14
> 狀態：規劃中（待 Christ 開跑時間 + Aria session context buffer）
> 文件版本：v1.0

---

## 一、背景與定位

**Stage 65 結案 + merge to main 後第一次 production-grade 真實任務試驗**：

- Trial_v10 ⭐⭐⭐ 業務級成功（PR #372 真開 / 7 檔改動 / cost $1.07 / 連續 4 Trial infinite loop pattern 打破）
- Trial_v10 揭 4 🟡 工程議題 → Stage 65 全收口（CLAUDE.md inject 修根因 / Vera token_logs 移 finally / workspace volume permission init / Cody 廣範圍紀律）
- Stage 65 結案後 Christ 拍板 merge feature/v5-poc → main（commit `cb6d688` merge + `65a8c3a` Directory.Build.props v3.55.0 bump）
- self-hosted runner 自動 deploy.yml 觸發 → production 跑 v3.55.0 image（含 v5 code）+ feature flag default false 維持 v4 path
- **Trial_v11 目的 = 驗 Stage 65 修法生效 + Trial_v10 結果可重現 + 路線 D 完整實證**

**Trial_v11 定位**：v5 動態架構正式上線前最後一次 production-grade 驗證 — 通過後 Christ 拍板切 default flag = v5 正式上線。

---

## 二、試驗目的（3 條）

1. **驗 Stage 65 4 議題收口真實生效**：
   - 議題 1：PR diff 0 含 CLAUDE.md 改動（vs Trial_v10 PR #372 含 -172/+113 CLAUDE.md 污染）
   - 議題 2：token_logs 含 Vera 一行 + 真實餘額消耗 vs 系統累計差距 < 1%（vs Trial_v10 4.3% blind spot）
   - 議題 3：開跑前 `docker exec ls -la /tmp/aiteam-workspace` owner=appuser 0 手動 chown（vs Trial_v10 期間 Aria 手動 chown）
   - 議題 4：PR cover 5/5 範圍含 InteractionCenter 操作中心 + Cody 對廣範圍指令真實 grep 範圍對照表（vs Trial_v10 4/5 漏 InteractionCenter）

2. **Trial_v10 業務級成功可重現**：
   - Cody 真實 deliver（git commit + push + 開 PR）
   - Petra Design trigger 命中 2 cap → Cody + Vera 多 worker chain 真實 fire
   - cost ≤ $5（對齊 Trial_v10 $1.07 baseline ±300%）
   - 連續 2 Trial 業務級成功 = 非 single 偶發實證

3. **切 default flag 拍板實證**：
   - 跑成功 + 0 🔴 戰略級新類型 + 4 議題全收口 → Christ 拍板切 default flag = v5 動態架構正式上線
   - 部分成功 → Stage 66 補強再 Trial_v12
   - 失敗 / 業務級失敗復發 → 戰略大重評估再啟動

---

## 三、任務需求

### 沿用 Trial_v6/v7/v8/v9/v10 同 prompt（6+1 向對照精準度最高）

任務原文（完全照搬 Trial_v10）：

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

1. **API 餘額 ≥ $30** — Trial_v11 cost 預估 $1-5 + 容錯 buffer（Trial_v10 baseline $1.07）
2. **main branch deploy 完成**：
   - `gh run list --workflow=deploy.yml --limit 3` 確認 Stage 65 commits（`cb6d688` merge + `65a8c3a` v3.55.0 bump）對應 deploy.yml run 已 success
   - `docker ps` 確認 Bot / Dashboard 用 v3.55.0 image（vs Trial_v10 期間用 feature/v5-poc 直接 build）
3. **Feature flag `Workflow:UsePetraOrchestratorV5=true`** — Trial_v10 期間已切（保留下來），開跑前 SQL 確認：
   ```sql
   SELECT "Key", "Value" FROM app_settings WHERE "Key" = 'Workflow:UsePetraOrchestratorV5';
   ```
   如未切 → Dashboard 系統設定頁切換 + Internal API `/internal/reload-cache?scope=appsettings`
4. **Petra Provider→Gemini Flash** — Trial_v10 已切，SQL 確認：
   ```sql
   SELECT "Name", "Provider", "Model" FROM agent_configs WHERE "Name" = 'PM';
   ```
   如非 Gemini → Dashboard Agent 設定頁 PM 卡片切 + ReloadCache
5. **驗證場景 A workspace volume permission init**（Stage 65 子項 3 對應 — 開跑前必驗，不對 = Stage 65 子項 3 沒生效需要 escalate）：
   ```bash
   docker volume ls | grep aiteam-workspace-data
   MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 ls -la /tmp/aiteam-workspace
   # 期望：drwxr-xr-x appuser appuser（vs Trial_v10 期間預設 root-owned）
   ```
6. **Christ 從 Discord 或 Dashboard 送指令**（任一通道 — 對齊 Trial_v10 Internal API curl 模式 / Aria 自跑可用 curl `/internal/ceo/command`）

---

## 四、預期觀察清單（Trial_v11 vs Trial_v5/v6/v7/v8/v9/v10 6+1 向對照）

### 核心驗收（Stage 65 4 議題收口效果）

| # | 維度 | Trial_v10 實況 | Trial_v11 預期 | Aria 自驗工具 |
|---|---|---|---|---|
| 1 | **PR 0 CLAUDE.md commit 污染**（議題 1 驗）| 含 -172/+113 CLAUDE.md 污染 | **PR diff 0 含 CLAUDE.md 改動**（adapter ritual 移除 → workspace 0 動）| `gh pr diff <num>` grep CLAUDE.md / 應該 0 命中 |
| 2 | **Vera token_logs 100%**（議題 2 驗）| 0 寫入（4.3% blind spot）| **token_logs 含 Vera + 真實餘額消耗 vs 系統累計差距 < 1%**（移 finally + capturedException）| SQL `SELECT "AgentName" FROM token_logs WHERE "CreatedAt" >= '<開跑時間>' GROUP BY "AgentName"` 應含 Vera |
| 3 | **0 手動 chown workspace**（議題 3 驗）| 期間 Aria 手動 chown 才跑 | **開跑前順手 ls 確認 owner=appuser**（compose entrypoint chown + gosu exec）| 場景 A 已驗 |
| 4 | **Cody 5/5 範圍 cover**（議題 4 驗）| 4/5（漏 InteractionCenter）| **PR cover 5 範圍**（QuickCommandCard + SystemSettings + AgentSettings + RuleFormDialog + **InteractionCenter** ✓）+ commit message 或 PR body 含「範圍對照表」段（5 範圍逐項 ✓）| `gh pr view <num> --json files` |

### Trial_v10 業務級成功重現（連續 2 Trial 實證）

| # | 維度 | Trial_v10 | Trial_v11 預期 | 驗證 |
|---|---|---|---|---|
| 5 | Cody 真實 deliver | PR #372 開 / 7 檔改動 | **PR 真開 + ≥ 5 檔改動 + git commit + push 真實**| `gh pr view + workspace git log` |
| 6 | Petra 動態決策 | Design trigger 2 cap | **Design trigger 命中 `code_implementation\|code_review`**（中等需求預期 — Kickoff trigger 4 cap 也可接受 但 Cody 跑 4 次 cost 倍增）| Bot log `Petra DecideAsync 完成 — raw=「...」` |
| 7 | 多 worker chain 真實 fire | Cody + Vera 都 fire | **PetraSessionMessages tool role 條數 ≥ 2 + Bot log 含 Cody + Vera 兩 dispatch**| SQL `SELECT COUNT(*) FROM petra_session_messages WHERE "SessionId" = '<id>' AND "Role" = 'tool'` |
| 8 | total cost | $1.07 | **$1-5**（對齊 Trial_v10 baseline ±300%）| SQL `SELECT SUM("TotalCostUsd") FROM token_logs WHERE "CreatedAt" >= '<開跑時間>'` |
| 9 | 持續時間 | ~6 分鐘 | **~5-15 分鐘**（Cody real subprocess 1-3 次 turn）| Aria 計時 |

### 6+1 向對照（Trial_v5 → Trial_v11）

| # | 維度 | v5 | v6 | v7 | v8 | v9 | v10 | **v11 預期** |
|---|---|---|---|---|---|---|---|---|
| 1 | 完成度 | 11/12 | 部分 | 0 中斷 | 0 卡死 | 0 deliverable | PR 7 檔（4/5 cover）| **PR ≥ 5 檔（5/5 cover）** |
| 2 | total cost | $8.78 | $15.81 | $1.52 | $1.20 | $0.63 | $1.07 | **$1-5** |
| 3 | 揭 🔴 新類型 | 5 | 3 | 1 | 2 | 12+ | 0 | **0**（≥1 = 戰略級議題復發訊號）|
| 4 | 揭 🟡 中議題 | — | 12 | 3 | 2 | — | 4 | **0-2**（議題密度進化曲線下降）|

### 預期揭露議題上限

- **≤ 2 議題**（0 🔴 + 0-2 🟡 + 0-1 🟢 — 議題密度進化曲線下降）
- ≥1 🔴 戰略級新類型 → Stage 65 收口未真實生效 / 或新類型議題暴露 = Stage 66 評估
- 0 🔴 + Stage 65 4 議題全收口 + Cody deliver 5/5 → **路線 D 完整實證 + 切 default flag 拍板**

---

## 五、Aria 自驗 SOP（沿用 Trial_v10 9-step 模板）

### 5.1 分工矩陣

對齊 Trial_v10 — Aria 全程自跑 + Christ 0 操作（除拍板開跑 + 業務正確性最終判斷 + 拍板下一步路線之外）。

### 5.2 9-step 自驗模板

1. **deploy 確認**：`gh run list --workflow=deploy.yml --limit 3` 看 Stage 65 commits 對應 run 已 success
2. **環境 setup 驗**：場景 A workspace volume permission（前置條件 5）+ feature flag SQL 確認
3. **送任務指令**：curl `/internal/ceo/command` 帶 X-Api-Key 送 Trial_v10 同 prompt
4. **Petra session 建立確認**：`SELECT "Id", "Status", "CreatedAt" FROM petra_sessions ORDER BY "CreatedAt" DESC LIMIT 1`
5. **動態決策觀察**：Bot log `grep "Petra DecideAsync 完成"` 看 capability 序列
6. **多 worker dispatch 觀察**：Bot log `grep "ClaudeCodeChatClientAdapter dispatch"` 看 worker 序列
7. **FinalizeGitAsync 成功路徑**：Bot log `grep "Petra PR 開啟"` 看 PR URL
8. **PR 內容檢查**：`gh pr view <num> --json files,additions,deletions,body` + `gh pr diff <num>` 看：
   - PR cover 範圍（5/5 議題 4 驗）
   - PR diff 0 含 CLAUDE.md 改動（議題 1 驗）
   - PR body 含 Petra 動態決策 + 範圍對照表
9. **token_logs 完整性 + cost 驗**：
   - SQL `SELECT "AgentName" GROUP BY ...` 看 Vera 在不在（議題 2 驗）
   - SQL `SELECT SUM("TotalCostUsd") ...` 對比 Christ 真實餘額消耗差距 < 1%

### 5.3 觸發點

1. **Stage milestone**：每個關鍵 milestone 完成（Petra DecideAsync / Worker dispatch / FinalizeGitAsync / PR open）Aria 跑健檢
2. **異常**：BossInteraction 開啟 / Cody 0 deliverable / Vera token_logs 仍 0 / FinalizeGitAsync 失敗 / Stage 65 議題復發 → Aria 立刻濃縮報告
3. **試驗結案**：Trial 完成 → Aria 寫 Trial_v11 結案報告草稿 + 路線 D 完整實證資料 + 切 default flag 拍板建議

### 5.4 環境細節 source of truth

對齊 Trial_v10 + Stage 65 修法後狀態：
- container 名 `aiteam-aiteam-bot-1` / `aiteam-postgres-1`
- Bot Internal API port `5052` / X-Api-Key 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- workspace path `/tmp/aiteam-workspace`（main 已含 named volume `aiteam-workspace-data` + Stage 65 子項 3 entrypoint chown）
- Petra session 表 `petra_sessions` + `petra_session_messages`（snake_case 表 + 欄位 PascalCase quote 必加）
- token_logs 表（`AgentName` / `Model` / `TotalCostUsd` / `IsEstimated` / `CreatedAt`）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：前置條件設置 + workspace volume permission 自動 init（Stage 65 子項 3 驗）

**觸發**：
1. `gh run list --workflow=deploy.yml --limit 3`（確認 Stage 65 deploy.yml run success）
2. `docker volume ls | grep aiteam-workspace-data`
3. `MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 ls -la /tmp/aiteam-workspace`
4. `MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 id`

**驗證**：
- deploy.yml run conclusion=success
- named volume `aiteam-workspace-data` 存在
- workspace owner **`appuser:appuser`**（vs Trial_v10 預設 `root:root` — Stage 65 子項 3 compose entrypoint chown 真實生效）
- Bot container user `uid=999(appuser)`

### 場景 B：Cody 5/5 範圍 cover 真實生效（議題 4 驗）

**觸發**：場景 A 通過後送 Trial_v6-v10 同 prompt（Dashboard 錯誤處理打磨）

**驗證**：
- `gh pr view <num> --json files` PR 含 5 個範圍檔案（**全 5 範圍 cover**）：
  - `src/AiTeam.Dashboard/Components/Pages/Home/QuickCommandCard.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Agents/AgentSettings.razor.cs` ✓
  - `src/AiTeam.Dashboard/Components/Pages/Rules/RuleFormDialog.razor` ✓
  - **`src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor*`**（Trial_v10 漏 — 本 Trial 必含）✓
- PR body 含「範圍對照表」段（5 範圍逐項 ✓ — Stage 65 子項 4 紀律真實生效）

### 場景 C：PR 0 CLAUDE.md commit 污染（議題 1 驗）

**觸發**：場景 B 任務送出後 PR 真開

**驗證**：
- `gh pr diff <num>` grep `CLAUDE.md` → **0 命中**（vs Trial_v10 PR #372 含 -172/+113 CLAUDE.md 污染）
- workspace `MSYS_NO_PATHCONV=1 docker exec aiteam-aiteam-bot-1 cat /tmp/aiteam-workspace/AiTeam/CLAUDE.md | head -3` 內容仍是「AiTeam 專案指引」（git clone 原樣 — adapter 0 動）
- Bot log 含 `CLAUDE template 載入 worker=Cody template=CLAUDE_Cody.md len=...`（systemPrompt 載入而非 inject 到 workspace）
- Bot log **0 含**「CLAUDE.md inject 完成」字樣（adapter 整段 ritual 移除）

### 場景 D：Vera token_logs 100% 寫入（議題 2 驗）

**觸發**：場景 B 任務送出後 Vera dispatch 完成

**驗證**：
- SQL：
  ```sql
  SELECT "AgentName", COUNT(*) AS calls, SUM("TotalCostUsd") AS cost
    FROM token_logs
   WHERE "CreatedAt" >= '<Trial_v11 開跑時間>'
   GROUP BY "AgentName";
  ```
  期望結果含 **Vera 一行**（vs Trial_v10 只 PM + Cody）
- Aria 對比 Christ 真實餘額消耗 vs 系統累計 cost — **差距 < 1%**（vs Trial_v10 4.3% blind spot）

### 場景 E：Petra 動態決策 + 多 worker chain 真實 fire（Trial_v10 重現）

**觸發**：場景 B 任務送出後

**驗證**：
- Bot log `Petra DecideAsync 完成 — raw=「code_implementation|code_review」picks=Cody → Vera`（Design trigger 2 cap，對齊 Trial_v10）
- Bot log 含 `ClaudeCodeChatClientAdapter dispatch worker=Cody capability=code_implementation` + `worker=Vera capability=code_review` 兩條 dispatch
- `petra_session_messages` 表 tool role 條數 ≥ 2

### 場景 F：FinalizeGitAsync 成功路徑（Trial_v10 重現）

**觸發**：場景 E 多 worker 完成後 PetraOrchestratorService.StartAsync 收尾段

**驗證**：
- Bot log `Petra branch 建立 + checkout：petra/...`
- Bot log `Petra PR 開啟：https://github.com/darkleong/AiTeam/pull/...`
- PR 真實 OPEN

---

## 六、後續行動清單

### Trial_v11 結案後（Aria 主動觸發）

1. `/aria-trial-summary` 結案 — Aria 寫 Trial_v11 結案報告 + 6+1 向對照數據 + 議題分類 + 路線 D 完整實證資料
2. 更新 Future_Feature.md + FF changelog — FF 三十六 status 升「✅ 完成」+ 新議題立 FF 候選（如有）
3. **拍板下一步**（依結果分支）：
   - **🟢 路線 D 完整實證**（4/4 議題收口 + Cody 5/5 cover + 0 🔴）→ Christ 拍板切 `Workflow:UsePetraOrchestratorV5` default true → v5 動態架構正式上線 → Stage 66+ FF 六十一其餘 8+ 點評估
   - **🟡 部分成功**（3-4 議題收口 + 1-2 🟡 新議題）→ Stage 66 補強（範圍邊界守緊不擴 L）→ Trial_v12 重驗
   - **🔴 失敗**（議題未真實收口 / 業務級失敗復發）→ 戰略大重評估再啟動評估路線 A/B/C

---

## 七、規模 / cost / 時程

- **Aria session context 預估**：~80-150K（對齊 Trial_v10 — Aria 全程自跑 9-step 模板：deploy 確認 + 環境 setup + 送任務 + 5 健檢 + 結案紀錄）
- **LLM cost 預估**：**$1-5**（對齊 Trial_v10 baseline $1.07 ±300%）
- **持續時間預估**：5-15 分鐘（Trial 短任務模式對齊 Trial_v10 ~6 分鐘）
- **Aria 工作量預估**：5-8 健檢報告 + Trial_v11 結案第二段（Plan v2.0 + Future_Feature 三檔同步 + memory calibration_anchors + 切 default flag 拍板資料）

---

## 八、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第 7 條紀律（見 5.4 段）
- **Trial_v11 在 main branch 跑**（vs Trial_v10 在 feature/v5-poc）— Stage 65 結案後 merge 完成 + Directory.Build.props v3.55.0 + self-hosted runner 自動 deploy
- Petra Provider 切 Gemini Flash AI Studio 免費 tier 對齊既有 Stage 63A/Trial_v9-v10 真打驗證
- 對齊 Trial_v2-v10 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板（Christ 2026-05-11）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-14 | 試驗計劃書建立（Aria）— Trial_v11 = Stage 65 4 議題收口效果驗證 + Trial_v10 業務級成功可重現 + 路線 D 完整實證（v5 上線前最後一次 production-grade 驗證）。**3 試驗目的**：① 驗 Stage 65 4 議題收口生效（CLAUDE.md 0 commit / Vera token_logs 100% / 0 手動 chown / Cody 5/5 cover）② Trial_v10 業務級成功可重現（連續 2 Trial 確認非 single 偶發）③ 切 default flag 拍板實證（路線 D 完整實證後 Christ 拍板 v5 正式上線）。**任務需求**：沿用 Trial_v6-v10 同 prompt（6+1 向對照精準度最高）。**操作前置條件 6 條**：API 餘額 ≥ $30 / main branch deploy 完成（gh run list 確認）/ feature flag UsePetraOrchestratorV5=true SQL 確認 / Petra Provider Gemini Flash SQL 確認 / 場景 A workspace volume permission 0 手動 chown / Christ 送指令（Aria 可代）。**6 驗收場景**：A 前置條件設置 + workspace volume permission 自動 init / B Cody 5/5 範圍 cover（含 InteractionCenter 議題 4 驗）/ C PR 0 CLAUDE.md 污染（議題 1 驗）/ D Vera token_logs 100%（議題 2 驗）/ E Petra 動態決策 + 多 worker chain 真實 fire（Trial_v10 重現）/ F FinalizeGitAsync 成功路徑。**Aria 全程自跑 9-step 模板**對齊 Trial_v10 既有實踐（gh workflow + watch / docker 環境 setup / Internal API curl 送任務 / SQL/log scan / PR 內容檢查 / 業務品質評估初稿 / Christ 拍板）。**規模**：Aria context 預估 80-150K / LLM cost $1-5 / 5-15 分鐘 / 5-8 健檢報告。**結果分支三路徑**：🟢 路線 D 完整實證 → 切 default flag = v5 正式上線 + Stage 66+ FF 六十一其餘評估 / 🟡 部分成功 → Stage 66 補強 → Trial_v12 / 🔴 失敗 → 戰略大重評估再啟動。 |

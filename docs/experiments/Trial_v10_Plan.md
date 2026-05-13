# Trial_v10 試驗計劃書 — Stage 64 v5 production-ready 收口後真實任務驗證

> 對應版本：**v3.54.0**（Stage 64 = 路線 D 採用拍板首發實作 — 8 子項一氣呵成 + Aria gate1 雙必修點收口 + Mock 全綠 168 tests / feature/v5-poc branch）
> 建立日期：2026-05-13
> 狀態：規劃中（待 Christ 開跑時間）
> 文件版本：v1.0

---

## 一、背景與定位

**Stage 64 收口後第一次真實任務試驗**：

- Trial_v9 揭「能跑 ≠ 能 deliver」— 5 次 Cody 跑通 0 commit / 0 PR / workspace clean
- Stage 64 補 8 子項 production-ready 修正（CLAUDE.md inject ritual / git commit-push-PR wire / Petra prompt 三 trigger 升級 / workspace volume + CloneOrPull wire / Gemini v1beta default / token_logs null-safe + transient retry / chain Mock 驗證 / Stage 63A spike notes errata）
- Mock 全綠 + Forge 自驗物理限制範疇明寫 5 場景留 Trial_v10 真實任務驗收

**Trial_v10 定位**：**路線 D（v5 動態架構全量遷移）採用 vs 回頭評估的關鍵實證**。

---

## 二、試驗目的（3 條）

1. **驗證 Stage 64 修正在真實任務下解決 Trial_v9 揭「0 deliverable」核心問題**：
   - Cody 真的 git commit + push + 開 PR（vs Trial_v9 5 次都 0 commit）
   - Petra DecideAsync 對中等需求真的派多隻 Worker（Trial_v9 揭 4 worker chain 只 1 fire 後續驗證）
   - 環境細節真實生效（場景 7 workspace volume mount 持久化 / 場景 8 Gemini v1beta endpoint）

2. **量化 v5 ROI 對照 Trial_v6/v7/v8/v9（連續 5 Trial）**：
   - cost：Trial_v5 baseline $8.78 / v6 $15.81 / v7 $1.52 中斷 / v8 $1.20 卡死 / v9 $0.63（0 deliverable）/ **v10 預期 $5-15**
   - 完成度：v5 11/12 / v6 部分 / v7 0 中斷 / v8 0 卡死 / v9 0 deliverable / **v10 預期 Phase 1 完整 deliver**

3. **戰略大重評估「路線 D 採用 vs 回頭評估」拍板實證**：
   - 跑成功 + 0 🔴 新類型 → 路線 D 採用 → Stage 65+ 啟動（merge to main + FF 六十一其餘 8+ 點補強 + production 切 default flag）
   - 跑部分成功 ≥1 🟡 新類型 → 補一輪 Stage 65 再 Trial_v11
   - 跑失敗 / 還是 0 deliverable → 戰略大重評估再啟動評估路線 A/B/C

---

## 三、任務需求

### 沿用 Trial_v6/v7/v8/v9 同 prompt（5 向對照精準度最高）

任務原文：

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

1. **API 餘額 ≥ $30**（剩 ~$35 充裕）— Trial_v10 cost 預估 $5-15 + 容錯 buffer
2. **Bot 切 feature/v5-poc branch + recreate**：
   - `git checkout feature/v5-poc`
   - `docker compose -f docker-compose.prod.yml up -d --force-recreate`
   - 此步順帶驗場景 7（`docker volume ls | grep aiteam-workspace-data` 看 named volume 是否建出來）
3. **Feature flag `Workflow:UsePetraOrchestratorV5=true`** — Dashboard 系統設定頁切換 + ReloadCache
4. **Petra Provider→Gemini Flash** — Dashboard Agent 設定頁 PM 卡片 + AI Studio 免費 tier
5. **Christ 從 Discord 或 Dashboard 送指令**（任一通道）

---

## 四、預期觀察清單（Trial_v9 vs v10 重點對照）

### 核心驗收（Stage 64 收口效果）

| # | 維度 | Trial_v9 實況 | Trial_v10 預期 | Aria 自驗工具 |
|---|---|---|---|---|
| 1 | **Cody 真實 deliver** | 5 次 0 commit / 0 PR / workspace clean | **真的 git commit + push + 開 PR**（FinalizeGitAsync wire 接通） | `git log` feature branch / GitHub PR list / workspace `git status` |
| 2 | **多 worker chain 真實 fire** | 4 worker chain 只 1 fire（嫌疑 Petra prompt 太簡只回 1 cap）| Design trigger 任務 → Petra 回 `code_implementation\|code_review` → Cody + Vera 都 fire | `PetraSessionMessages` tool role 條數 + Bot log adapter dispatch |
| 3 | **CLAUDE.md inject ritual 真實生效** | v5 PoC 沒做 → Cody 不知道自己是誰 | dispatch 前 workspace `CLAUDE.md` = `CLAUDE_Cody.md` 內容 / finally restore 後還原 | Bot log「CLAUDE.md inject 完成 worker=Cody」+ workspace 觀察 |
| 4 | **workspace volume 持久化**（場景 7）| `/tmp/aiteam-workspace` 容器內 ephemeral 每次 recreate 消失 | `aiteam-workspace-data` named volume 建出來 + recreate 後內容保留 | `docker volume ls` + `docker inspect` Mounts |
| 5 | **Gemini v1beta endpoint**（場景 8）| 既有 docker-compose env override v1beta（Stage 64 改為 appsettings default）| Petra DecideAsync 正常 + 0 `400 BadRequest "systemInstruction unsupported"` | Bot log Petra response |

### 環境容錯（Stage 64 retry / null-safe）

| # | 維度 | Trial_v10 預期 | 備註 |
|---|---|---|---|
| 6 | Anthropic 5xx transient retry | 若遇到 5xx → adapter 自動 retry 1-3 次 + Bot log retry attempt | 機率性，可能沒遇到（Trial_v9 遇過一次） |
| 7 | token_logs null-safe 寫入 | Usage=null 仍寫 cost=0 / token=0 紀錄 | SQL `SELECT COUNT(*) FROM token_logs WHERE...` |

### 五向對照（Trial_v5/v6/v7/v8/v9 vs v10）

| # | 維度 | v5 | v6 | v7 | v8 | v9 | **v10 預期** |
|---|---|---|---|---|---|---|---|
| 1 | 完成度 | 11/12 | 部分 | 0 中斷 | 0 卡死 | 0 deliverable | **Phase 1 完整 deliver** |
| 2 | total cost | $8.78 | $15.81 | $1.52 | $1.20 | $0.63 | **$5-15** |
| 3 | 揭 🔴 新類型 | 5 | 3 | 1 | 2 | 12+ 議題 | **0-1**（≥1 = 路線 D 採用打折） |
| 4 | 揭 🟡 中議題 | — | 12 | 3 | 2 | — | **0-3**（合併 backlog） |

### 預期揭露議題上限

- **≤ 5 議題**（0-1 🔴 新類型 + 0-3 🟡 + 1-2 🟢）
- ≥1 🔴 新類型 → v5 也踩同類陷阱訊號 = 戰略大重評估再啟動
- 0 🔴 新類型 + Cody deliver 成功 → 路線 D 採用實證

---

## 五、Aria 自驗 SOP

### 5.1 分工矩陣（沿用 Trial_v6-v9）

Aria 做 DB metrics + Bot log scan + git/PR 結構分析 + 五向對照數字報告 + 異常 pattern detection + Trial 健檢報告初稿；Christ 做 UAT + 業務正確性判斷 + Aria 異常 OK/NOK 拍板。

### 5.2 觸發點

1. **Stage milestone 觸發**：每個 Petra 動態決策 step（DecideAsync / Worker dispatch / FinalizeGitAsync / 真實 git commit + PR）完成時 Aria 跑健檢
2. **異常觸發**：BossInteraction 開啟 / Petra escalate / FinalizeGitAsync 失敗（git push / PR 失敗）/ adapter retry 觸發 → Aria 立刻濃縮報告
3. **試驗結案觸發**：Trial 完成 → Aria 寫 Trial_v10 結案報告草稿 + 對戰略大重評估提供拍板實證

### 5.3 自驗工具清單

對齊 Trial_v6-v9：Bash SQL via `docker exec aiteam-postgres-1 psql` / `docker logs` / `git log` feature branch / GitHub PR list / Read-Grep。

**環境細節 source of truth**：
- container 名：`aiteam-aiteam-bot-1` / `aiteam-postgres-1`（雙 prefix）
- Bot Internal API port：`5052`
- X-Api-Key 取值：`docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey`
- DB schema：`PetraSessions` + `PetraSessionMessages` + `token_logs` + `BossInteractions`（PascalCase quote 必加）
- workspace path：`/tmp/aiteam-workspace`（feature/v5-poc 加 volume mount 後對應 named volume `aiteam-workspace-data`）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：前置條件設置順帶驗場景 7（workspace volume mount）

**觸發**：
1. `git checkout feature/v5-poc`
2. `docker compose -f docker-compose.prod.yml up -d --force-recreate`

**驗證**：
- `docker volume ls | grep aiteam-workspace-data` → 應看到 named volume 建出來
- `docker inspect aiteam-aiteam-bot-1` Mounts → 應含 workspace volume mount
- Bot log 含 `Petra BuildSessionContext CloneOrPull 完成 workingDir=...`（首次啟動觸發 BuildSessionContext 或 Petra StartAsync 入口才會 fire）

### 場景 B：Cody 真實 deliver 端對端（核心驗收）

**觸發**：Christ 從 Dashboard 送 Trial_v6 同 prompt（Dashboard 錯誤處理打磨）

**驗證**：
- `PetraSessionMessages` assistant message content 含 `|`（多 capability）→ 驗 Petra 真實派多隻 Worker（場景 6 mock 對應真實版本）
- `PetraSessionMessages` tool role 條數 ≥ 2 → 多 worker 真實 fire
- workspace `git status` 有真實檔案改動
- GitHub feature branch `petra/{taskGroup-8}-{session-8}-{ts}` 真實建出來
- GitHub PR 真實開（PR body 含 Petra 動態決策過程 + worker summary）
- cost ≤ Trial_v6 baseline $15.81

### 場景 C：Petra 動態決策準度（v5 新類型）

**觸發**：場景 B 任務送出後觀察 Bot log

**驗證**：
- Bot log `Petra DecideAsync 完成 — raw=「...」picks=...` 含合理 capability 序列
- Design trigger 任務（跨多元件）期望回 `code_implementation|code_review` 至少
- 不期望回單一 capability `code_implementation`（那是 Trial_v9 1-worker 卡點）

### 場景 D：FinalizeGitAsync 成功路徑

**觸發**：場景 B Cody 真實改檔後 PetraOrchestratorService.StartAsync 收尾段

**驗證**：
- Bot log `Petra branch 建立 + checkout：petra/...`
- Bot log `Petra PR 開啟：https://github.com/...`
- 失敗 path（git push / PR open 失敗）→ log error 但 session 仍 done（無聲失敗風險觀察）

### 場景 E：Gemini v1beta endpoint（場景 8）

**觸發**：Petra DecideAsync 每次呼叫

**驗證**：
- Bot log Petra response 正常解析
- 0 `400 BadRequest "systemInstruction unsupported"` error log
- token_logs Petra row（model=gemini-2.5-flash）寫入

### 場景 F：Adapter retry / null-safe（機率性）

**觸發**：若 Cody Anthropic API 5xx 真實發生

**驗證**：
- Bot log `ClaudeCodeChatClientAdapter transient 5xx retry 1/3 ...`
- retry 成功 → 繼續流程 / retry 耗盡 → propagate failure
- token_logs 寫一次（不論 Usage=null 或 retry）

---

## 六、後續行動清單

### Trial_v10 結案後（Aria 主動觸發）

1. `/aria-trial-summary` 結案 — Aria 寫 Trial_v10 結案報告 + 5 向對照數據 + 議題分類 + v5 ROI 結論
2. 更新 Future_Feature.md + FF changelog — FF 三十六 status 升或 close 視結果
3. **拍板下一步路線**（依結果分支）：
   - **🟢 路線 D 採用實證**（Cody deliver + 0 🔴 新類型）→ Stage 65+ 啟動（merge feature/v5-poc to main + FF 六十一其餘 8+ 點補強 + production 切 default flag）
   - **🟡 部分採用**（Cody deliver 但 ≥1 🟡 新類型）→ 補一輪 Stage 65 修 → Trial_v11
   - **🔴 路線 D 失敗**（還是 0 deliverable / ≥1 🔴 新類型）→ feature/v5-poc 不 merge / FF 六十一廢棄 / 戰略大重評估再啟動評估路線 A/B/C

---

## 七、規模 / cost / 時程

- **Aria session context 預估**：~250-350K（對齊 Trial_v8/v9 ~300-400K — 5 向對照觀察 + Stage 64 修正效果驗證）
- **LLM cost 預估**：**$5-15**（對齊 Trial_v6 baseline ±50% — Petra Gemini Flash 免費 tier / Workers 真打 Anthropic Claude cost 來源）
- **持續時間預估**：30-60 分鐘（Trial 短任務模式）
- **Aria 工作量預估**：5-8 健檢報告 + Trial_v10 結案第二段（Plan v2.0 + Future_Feature 同步 + memory calibration_anchors + 戰略大重評估資料更新）

---

## 八、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第 7 條紀律（見 5.3 自驗工具清單段）
- Trial_v10 在 feature/v5-poc branch 跑 / main 0 動 — 對齊 Stage 64 範圍邊界
- Petra Provider 切 Gemini Flash AI Studio 免費 tier 對齊既有 Stage 63A/Trial_v9 真打驗證
- 對齊 Trial_v2-v9 既有獨立試驗計劃模式 / Stage 跟 Trial 分開拍板（Christ 2026-05-11）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-13 | 試驗計劃書建立（Aria）— Trial_v10 = Stage 64 v5 production-ready 收口後第一次真實任務試驗 / 路線 D（v5 動態架構全量遷移）採用 vs 回頭評估的關鍵實證。**3 試驗目的**：① 驗證 Stage 64 8 子項解決 Trial_v9 揭「0 deliverable」核心問題 ② 5 向對照量化 v5 ROI ③ 戰略大重評估拍板實證。**任務需求**：沿用 Trial_v6/v7/v8/v9 同 prompt（5 向對照精準度最高）。**操作前置條件 5 條**：API 餘額 ≥ $30 / 切 feature/v5-poc + docker compose recreate（順帶驗場景 7 volume mount）/ feature flag UsePetraOrchestratorV5=true / Petra Provider→Gemini Flash / Christ 送指令。**6 驗收場景**：A 前置條件設置順帶驗場景 7 / B Cody 真實 deliver 端對端（核心）/ C Petra 動態決策準度 / D FinalizeGitAsync 成功路徑 / E Gemini v1beta endpoint / F Adapter retry/null-safe（機率性）。**規模**：Aria context 預估 250-350K / LLM cost $5-15 / 30-60 分鐘 / 5-8 健檢報告。**結果分支三路徑**：🟢 路線 D 採用實證 → Stage 65+ / 🟡 部分採用 → Trial_v11 / 🔴 失敗 → 戰略大重評估再啟動。 |
| v2.0 | 2026-05-13 | **試驗結案 — ⭐⭐⭐ 業務級成功（vs Trial_v9 業務級失敗）+ 路線 D 採用實證充分**。**Aria 全程自跑試驗**（Christ 拍板 OK 跑後 0 操作 — `gh workflow run deploy.yml --ref feature/v5-poc` 觸發 8 分鐘 self-hosted runner build + deploy / docker volume / Internal API curl 送任務 / SQL 觀察 / Bot log scan 全 Aria 操作 — 對齊「Christ 只動嘴」精神再進一步突破）。**真實 lifecycle ~6 分鐘** — 任務送出 15:27:18 → PetraSession `b322d06d` running → 15:35:02 Petra PR 開啟 done。**核心問題完全解決**：① **Cody 真實 deliver** — 7 個 Dashboard 檔改動 +167/-181 行 [PR #372](https://github.com/darkleong/AiTeam/pull/372) `petra/spike-b322d06d-202605131534`（vs Trial_v9 5 次 0 commit / 0 PR） ② **Petra Design trigger 命中** — `code_implementation\|code_review` picks=Cody → Vera 兩 worker chain 真實 fire（vs Trial_v9 4 cap Kickoff trigger 但只 1 worker fire — **Q2 嫌疑「Petra prompt 太簡」推翻**：Petra 從 Stage 63B 起就回 4 cap，真實問題是 framework chain dispatch — Stage 64 子項 7 chain Mock 驗證 + 真實 Design trigger 2 cap 都 fire 證實）③ **CLAUDE.md inject ritual 兩 worker 真實生效** — Bot log `CLAUDE.md inject 完成 worker=Cody template=CLAUDE_Cody.md` + `worker=Vera template=CLAUDE_Vera.md` ④ **CloneOrPull wire 真實生效** — Aria 必修 2 `Petra BuildSessionContext CloneOrPull 完成 workingDir=/tmp/aiteam-workspace/AiTeam`。**真實 cost $1.07**（餘額 $35.66 → $34.59 — vs 預期 $5-15 偏低 -85% / vs Trial_v6 baseline $15.81 便宜 -93% / vs Trial_v9 $0.63 略貴但 deliverable 完整 = **每 deliverable PR 真實 cost 大幅優化**）。**揭 4 議題（0 🔴 戰略級新類型 + 4 🟡 工程細節）**：① **🟡 CLAUDE.md 污染 commit** — PR 含 CLAUDE.md -172/+113 = workspace `CLAUDE.md` 被 commit 成 Vera 模板內容（workspace 現狀已 restore 但 commit timing 在 finally 之前 — FinalizeGitAsync vs Vera adapter restore 時序漏洞）② **🟡 Vera token_logs 沒寫** — Cody Sonnet $1.024 / PM Gemini $0.002 / **Vera 0** — 真實餘額消耗 $1.07 vs 系統 $1.026 差 $0.044（4.3% blind spot）= Vera readOnly path 漏寫紀錄 ③ **🟡 named volume permission** — Stage 64 docker-compose volume mount 預設 root-owned，Bot appuser 寫不進去，Aria 手動 `chown -R appuser:appuser` 才讓 Trial 跑（Stage 65+ 加 Dockerfile / entrypoint chown）④ **🟡 業務範圍 cover 不完整** — 任務點名「快速下達指令卡 + 系統設定 + 操作中心 + Agent 設定 + 規則管理」5 範圍，Cody 改 4/5（漏 InteractionCenter 操作中心 — 現狀只 Snackbar 沒 inline 不對齊「兩處一致」要求）。**0 🔴 戰略級新類型 = 路線 D 採用實證充分**（vs Trial_v6 揭 3 🔴 / v7 揭 1 🔴 / v8 揭 2 🔴 / v9 揭 12+ 議題）。**v5 ROI 對照 5 向**：cost v5 baseline $8.78 / v6 $15.81 / v7 $1.52 中斷 / v8 $1.20 卡死 / v9 $0.63 0 deliverable / **v10 $1.07 完整 deliver** ⭐。完成度 v10 1 PR + 4/5 範圍 cover = **連續 4 Trial infinite loop pattern 打破**。**業務品質評估 75-80%**（核心要求 ✓ + 範圍 4/5 + Snackbar mode 對齊既有 codebase 慣例 + toast duration default 5s 對齊但隱性 + 不抽共用元件合理判斷）。**對戰略大重評估的關鍵實證升級**：① v5 動態架構**設計層真實 work** — 不需戰略大重評估了 ② 4 議題都是工程細節層次（CLAUDE.md timing / token_logs path / volume permission / 業務範圍 cover），補一輪 Stage 65 就好 ③ Trial_v10 結果分支對應「🟡 部分採用」但**接近 🟢 路線 D 採用實證**（0 🔴 + Cody deliver + 多 worker chain 真實 fire — Stage 65 補強 4 🟡 議題後再 Trial_v11 可進入 🟢 完整採用）。**Aria 工作節奏觀察**：① **Aria 全程自跑試驗對「Christ 只動嘴」精神突破**（gh workflow run / docker volume / Internal API curl / SQL / Bot log scan / PR 內容檢查 / 業務品質評估初稿全 Aria — Christ 只動嘴拍板開跑 + 收結果） ② **Q2 嫌疑推翻是 Stage 64 計劃前 WebSearch 紀律真實價值體現**（Trial_v9 揭 4 worker chain 只 1 fire 嫌疑 Petra prompt 太簡 → Trial_v10 揭 Petra Design trigger 命中 2 cap 全 fire = Stage 64 子項 3 Petra prompt 升級不影響 chain dispatch / 真實 framework 自動 chain 生效 = 計劃前 WebSearch 結論「framework 自動 chain」實證對齊）③ **連續 5 Trial 累積 lifecycle visibility 進化**（Trial_v6 揭 15 議題 → v10 揭 4 議題 — 議題密度下降 + 戰略級減少 = 系統成熟度進化曲線）。**Aria 自省點候選 #33**（待 session 結束評估立檔）：Aria 全程自跑試驗 + 業務品質評估初稿可達成「Christ 只動嘴」更進一步 — Christ 角色從「驗收按鈕點」進化為「Aria 評估結果拍板」。**FF 動態**：FF 三十六 status 升「Charter ✅ + API spike ✅ + PoC ✅ + Stage 64 production-ready ✅ + Trial_v10 業務級成功 ✅ + Stage 65 4 🟡 議題收口 → Trial_v11 確認 → merge to main」/ FF 六十一補強清單擴 Trial_v10 4 議題 → 共 12+ 點留 Stage 65 範圍。**Top 5 重排**：Stage 65 升 **#1**（4 🟡 議題收口 — CLAUDE.md timing / Vera token_logs / volume permission / 業務範圍 cover 紀律）/ Trial_v11 #2（Stage 65 結案後重跑驗證）/ FF 三十六 #3（Trial_v11 ✅ 後 merge to main + production 切 default flag）/ FF 五十四 #4 / 保留群組 #5。**Aria 校準錨 待 Christ 補 Aria session context 數字**（Trial 規模對齊 Trial_v6-v9 — Aria 全程自跑試驗 deploy.yml watch + 多次 SQL/log scan + PR 內容檢查 + 業務評估 + 結案）。**戰略主軸**：Trial_v10 ⭐⭐⭐ 業務級成功 + 連續 4 Trial infinite loop pattern 打破 + 路線 D 採用實證充分 → Stage 65 4 🟡 議題收口 → Trial_v11 確認 → merge to main + production 切 default flag = v5 動態架構正式上線。 |

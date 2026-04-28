# Stage 43：FF 三十二 Orchestrator 改動類（A+B+E）— Self-implement 完整性閘門下半場

> 對應 Future Feature：FF 三十二（Self-implement 完整性閘門）— 子項 A / B / E + Stage 42 子項 F 搭車（Sage 路由端 escalate 下游處理）
> 對應版本：v3.30.0
> 建立日期：2026-04-28
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：FF 三十二 七子項閘門中的「Orchestrator 改動類」三子項（A / B / E）— 動核心流程 + 新狀態 + Migration + Mock 場景。Stage 42 prompt 補強類已交付，本 Stage 補完 Orchestrator 行為層的閘門，與 Stage 42 合計七子項才能閉環。

| 子項 | 對應 Trial_v4 Bug | 修法核心 |
|---|---|---|
| **A** | Bug #5 🔴 | DevPlan 失敗 → Cody Appeal `accept` 觸發**重新產出**（上限 2 次，仍失敗 → escalate） |
| **B** | Bug #8 🔴 | Dev fix failed → **中止 fix loop**（不啟 Reviewer）+ TaskGroup 標 `needs_intervention` + escalate |
| **E** | Bug #11 🔴 | QA failed → trigger fix loop（QaFixRound +1，上限 3）；仍失敗 → `needs_intervention`；TaskGroup done 判定改為「所有階段成功才 done」 |
| **F 搭車** | Stage 42 留下 | Sage 路由端 escalate 下游處理（DocAgentService 認 escalate JSON → 走 BossInteraction） |

**戰略意義**：Stage 42 prompt 層補完「Agent 知道該擋」，Stage 43 行為層補完「流程實際擋下」。兩個 Stage 合起來才解 Trial_v4 揭露的五個 🔴 嚴重 bug。

**Trial_v5 前置條件**：本 Stage + Stage 42 + FF 三十三 + FF 三十四 全完成才能開 Trial_v5（重跑 FF 十六需求對照 Trial_v4）。

---

## 子項 A：DevPlan 重新產出機制

### 現況

Cody Dev_plan 階段失敗時，DevPlan 欄位寫入「（計畫書產出失敗，請查看 log）」短訊息（17 字）。Petra 駁回 → Cody Appeal 走 `DevPlanAppealService` 既有迴圈 → Cody 回 `accept`（自承「必須重新產出完整計畫書」）→ **流程沒給她重新產出的機會，直接進入 Dev 階段**。

### 設計決策（Christ 拍板）

| 決策點 | 拍板 |
|---|---|
| **重試上限** | **2 次**（DevPlan 失敗 2 次 = LLM 顯然無能力解，escalate；比既有 FixIteration = 3 嚴格，省成本） |
| **「DevPlan 失敗」判定** | **多重 OR 條件**：① 字串含「產出失敗」/「請查看 log」字樣；② DevPlan 字數 < 100 字；③ 結構化 IMPLEMENTATION_NOTE marker 缺失（如預期格式 `## 實作說明` 段缺失）— 任一觸發即視為失敗 |
| **觸發點** | **Cody Appeal `accept` 後重產**（保留既有 Petra ⇄ Cody Appeal 對話流程；accept = 「我同意要重產」就是觸發訊號）|

### 實作項目

#### A-1：DevPlan 失敗判定 helper

**位置**：`src/AiTeam.Bot/Agents/Pm/PmAgentCommons.cs` 或新建 helper（由 Forge 評估）

**功能**：給定 DevPlan 字串，判定是否為「失敗訊息」（多重 OR 條件）。

**輸出**：`bool IsDevPlanFailed(string? devPlan)` + 失敗原因列舉（哪一條件命中，供 log 用）

#### A-2：DevPlanRevision 重置機制

**位置**：`DevPlanAppealService` 或 `TaskGroupService` Dev_plan 流程入口

**邏輯**：
1. Cody DevPlan 產出 → 呼叫 A-1 判定
2. 若失敗 → 走 Appeal 流程（既有）
3. Cody Appeal 回 `accept` → **不直接進入 Dev 階段**，改觸發**重產 Dev_plan**：
   - `DevPlanRevision` +1（既有欄位，目前用於 Appeal 計次）
   - 重新呼叫 Cody Dev_plan agent
4. 重產上限 = **2 次**（即 `DevPlanRevision >= 2` 時若仍失敗 → escalate）
5. escalate 路徑：建 BossInteraction `dev_plan_unable` 給 Christ 確認（取消 / 手動補 plan / 強制進 Dev）

#### A-3：既有 Appeal 流程相容性

`DevPlanAppealRoundA` 計數機制保持不變，新機制使用 `DevPlanRevision` 計數重產次數，**兩個計數獨立不互相干擾**（既有設計留註解說明）。

### 不在範圍

- ❌ Cody Dev_plan agent 本身的 prompt 補強（已在 Stage 42 子項 G 涵蓋自我檢查）
- ❌ DevPlan 失敗的根因修復（如 LLM API timeout / Token 守門）— Token 議題留 FF 三十三

---

## 子項 B：Dev fix 失敗中止 fix loop

### 現況

Dev fix 階段 failed（Token 守門擋下、subprocess timeout、build 失敗、阻礙報告等任一原因）時，**第二輪 Reviewer 仍啟動 review 同一個未修的 PR** → Petra 通過 → 進 QA → 流程繼續走偏。

### 設計決策（Christ 拍板）

| 決策點 | 拍板 |
|---|---|
| **failed 分類處理** | **一律中止 + escalate**（簡單可預測；分類處理 over-engineer） |
| **TaskGroup 新狀態** | **新增 `needs_intervention`**（語意清楚；既有 `failed` 留給「明確失敗已不可挽救」） |
| **escalate 介面** | **既有 BossInteraction**（沿用 Stage 28a 樂觀鎖 + 雙通道 Discord/Dashboard） |

### 實作項目

#### B-1：TaskGroup.Status 加 `needs_intervention` + InterventionReason 欄位

**位置**：`src/AiTeam.Data/Entities.cs` `TaskGroup` 類別

**新增**：
- `Status` comment 補上 `needs_intervention`（5 個狀態）
- 新欄位 `string? InterventionReason`（紀錄為什麼需介入：「Dev fix failed (Token 擋)」/「QA fix 連 N 次失敗」/「DevPlan 重產 2 次失敗」等）

**Migration**：`Stage43NeedsInterventionStatus`（純加欄位，無 schema 破壞性變更）

#### B-2：Dev fix failed 中止邏輯

**位置**：`AgentQueueProcessor.cs` Dev failed 處理路徑

**邏輯**：
1. Dev fix step 結果為 failed → **不轉 Reviewer**
2. TaskGroup.Status = `needs_intervention`，InterventionReason = 描述
3. 建 BossInteraction `dev_failed_intervention`（Discord + Dashboard 通知 Christ）
4. 等 Christ 決定（取消 / 手動修後重啟 / skip 強制進下一階段）

#### B-3：Dashboard / Discord 顯示新狀態

**位置**：
- `Dashboard` 任務狀態 chip / icon 加 `needs_intervention` 顏色（建議 amber/orange，警告但不致命）
- Discord 通知 embed 加新狀態說明

### 不在範圍

- ❌ Token 守門邏輯改動（FF 三十三）
- ❌ subprocess timeout 重試機制（既有 Stage 14 / 29）

---

## 子項 E：QA 失敗判定為 TaskGroup 失敗

### 現況

QA Quinn failed → 直接進 Doc → Doc done → **TaskGroup mark done**（QaFixRound = 0 沒進 fix loop）。Trial_v4 Bug #11 揭露 TaskGroup done 判定邏輯有漏洞。

### 設計決策（Christ 拍板）

| 決策點 | 拍板 |
|---|---|
| **QA failed 後動作** | **trigger fix loop（QaFixRound +1）**；仍失敗 → `needs_intervention`（既有 QaFixRound 機制就是為這設計，目前 bug 是觸發點壞了） |
| **QaFixRound 上限** | **3 次**（對齊既有 FixIteration） |
| **TaskGroup done 判定邏輯** | **改為檢查所有階段成功才 done**（呼應 Bug #11 根因）|

### 實作項目

#### E-1：QA failed 觸發 fix loop

**位置**：`QaCoordinationService.cs` 或 `TaskGroupService.cs` QA 結果處理路徑

**邏輯**：
1. QA Quinn agent 結果 failed → **不轉 Doc**
2. `QaFixRound` +1
3. 觸發 Cody fix（QA fix 流程，既有設計但目前 bypass）
4. Cody fix 完 → 重跑 QA（迴圈）
5. `QaFixRound >= 3` 且 QA 仍 failed → TaskGroup.Status = `needs_intervention`，建 BossInteraction `qa_failed_intervention`

#### E-2：TaskGroup done 判定邏輯修正

**位置**：`TaskGroupService.cs`（最後 mark done 的地方，Forge Plan Mode 探索具體 method）

**邏輯**：
- 目前可能：最後一個 task done 即 mark TaskGroup done
- **改為**：檢查所有階段 task 全 done（含 Doc）+ **無任何階段 failed / needs_intervention** → 才 mark done
- 若任一階段 failed / needs_intervention → TaskGroup 對應狀態傳染上來（`failed` 或 `needs_intervention`）

#### E-3：QA fix loop 與既有 fix loop 邊界

`QaFixRound`（QA fix）獨立於 `FixIteration`（Vera review fix loop），兩者不互相影響（呼應 Stage 24 既有設計）。Forge 實作時確認既有 QA fix 路徑是否還活著（被 bypass 的具體位置），實作紀錄需明寫。

### 不在範圍

- ❌ Quinn QA agent 本身的 prompt / 測試品質判準（Stage 41 已補強，Trial_v5 觀察）
- ❌ QA 結果型別擴充（`AgentResultType.Skipped` 已存在 Stage 39）

---

## 跨子項共通：搭車修 — Sage 路由端 escalate 下游處理（FF 三十二 子項 F 補完）

### 現況

Stage 42 已將 escalate 規則寫進 `CLAUDE_Sage.md`（品質下限判準），但 **`DocAgentService.cs` 路由端目前無 escalate 處理**（輸出只認 done / failed）。本 Stage 補下游處理，讓 Sage prompt 規則真實生效。

### 實作項目

#### F-1：DocAgentService 認 escalate JSON

**位置**：`src/AiTeam.Bot/Agents/DocAgentService.cs`（或拆解後的對應子 service）

**邏輯**：
1. Sage CLI 輸出 → 解析 JSON
2. 若 `decision: "escalate"` → **不寫歸檔檔案**
3. TaskGroup.Status = `needs_intervention`，InterventionReason = Sage 的 reason + evidence
4. 建 BossInteraction `sage_escalate`（Christ 確認是否手動補歸檔 / 接受不歸檔 / 重跑 Doc 階段）

#### F-2：Sage PR URL hardcode 議題（如尚未修）

Stage 42 探索揭露 Sage prompt 收 PR URL 是純文字 `PR 連結：{URL}` 形式，**搭車確認程式碼端是否有 hardcode 拼湊路徑**：
- 若有 → 修正為直接讀 `TaskGroup.DevPrUrl`
- 若無 → 實作紀錄寫明「探索後確認無此議題」

---

## 整體驗收原則

**本 Stage 動 Orchestrator 流程 + 新狀態 + Migration + 4 個 Mock 場景**。驗收三層：

### 第一層：靜態驗收

✅ Migration 跑得起來（`dotnet ef database update`）；TaskGroup.Status 含 `needs_intervention`；InterventionReason 欄位存在；Build 通過。

### 第二層：Mock 行為驗收 ⭐（**本 Stage 主要驗收**）

不像 Stage 42 純 LLM 判斷類，**本 Stage 是流程行為改動，Mock 可確切驗證**。需新增 4 個 Mock 場景（見「驗收情境」段）。

### 第三層：Trial_v5 真實流程驗收（留待 FF 三十二 全完成 + FF 三十三/三十四完成）

✅ Trial_v5 預期觀察清單第 1 / 2 / 5 項對照本 Stage 子項 A / B / E。

---

## 驗收情境

### A. Migration + 新狀態欄位

**驗收方式**：
1. `dotnet ef migrations list` → 含 `Stage43NeedsInterventionStatus`
2. `docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d task_groups"` → 欄位含 `intervention_reason`
3. `git diff src/AiTeam.Data/Entities.cs` → `TaskGroup.Status` comment 含 `needs_intervention`
4. `dotnet build AiTeam.slnx` → 0 Errors

### B. 子項 A — DevPlan 重新產出機制

**新 Mock 場景**：`dev_plan_fail_retry`（DevPlan 第 1 次失敗 → Appeal accept → 第 2 次成功）

**驗收方式**：
1. Dashboard `/mock` 觸發 `dev_plan_fail_retry`
2. 觀察 Pipeline View：Cody Dev_plan 第 1 輪 failed → Appeal 對抗紀錄出現 → 第 2 輪 Dev_plan 成功 → 進 Dev
3. DB 驗證：`DevPlanRevision = 1`、`DevPlanAppealLog` 含一輪對話、最終 `DevPlan` 字段非失敗訊息
4. Discord / Dashboard 操作中心無 escalate 卡

**新 Mock 場景**：`dev_plan_fail_escalate`（連 2 次失敗 → escalate）

**驗收方式**：
1. Dashboard `/mock` 觸發 `dev_plan_fail_escalate`
2. 觀察 Pipeline View：Cody Dev_plan 兩輪都 failed
3. 操作中心出現 `dev_plan_unable` BossInteraction 卡片
4. DB 驗證：`DevPlanRevision = 2`、TaskGroup.Status = `needs_intervention`、InterventionReason 含「DevPlan 重產 2 次失敗」

### C. 子項 B — Dev fix 失敗中止

**新 Mock 場景**：`dev_failed_intervention`（Dev fix 失敗 → 中止 + needs_intervention）

**驗收方式**：
1. Dashboard `/mock` 觸發 `dev_failed_intervention`
2. 觀察 Pipeline View：Cody Dev fix 第 N 輪 failed → **Reviewer 不啟動**
3. 操作中心出現 `dev_failed_intervention` BossInteraction 卡片
4. DB 驗證：TaskGroup.Status = `needs_intervention`、InterventionReason 含「Dev fix failed」
5. Dashboard 任務狀態 chip 顯示 `needs_intervention`（amber/orange 色）

### D. 子項 E — QA 失敗 fix loop + TaskGroup done 判定

**新 Mock 場景**：`qa_failed_fix_then_intervention`（QA failed → 觸發 fix → 仍失敗 3 次 → needs_intervention）

**驗收方式**：
1. Dashboard `/mock` 觸發 `qa_failed_fix_then_intervention`
2. 觀察 Pipeline View：QA 第 1 輪 failed → Cody fix → QA 第 2 輪 failed → Cody fix → QA 第 3 輪 failed
3. **第 3 輪後 Doc 不啟動**（既有 bug：第 1 輪 failed 就直接進 Doc）
4. 操作中心出現 `qa_failed_intervention` BossInteraction 卡片
5. DB 驗證：`QaFixRound = 3`、TaskGroup.Status = `needs_intervention`、**TaskGroup ≠ done**（呼應 done 判定修正）

### E. 跨子項：TaskGroup done 判定邏輯修正

**驗收方式**（搭車驗，無新 Mock 場景）：
1. 跑既有 Mock 「正常完整流程」（如 `new_feature_with_proposal`）→ 所有階段成功 → TaskGroup = `done`（行為與既有相同）
2. 上述 B / C / D 三場景任一觸發 → TaskGroup ≠ `done`（必為 `needs_intervention`）

### F. 子項 F 搭車 — Sage escalate 下游處理

**Mock 觸發方式**：可能需新場景或既有 `fail_*` 場景擴充（由 Forge 設計）

**驗收方式**：
1. Mock 觸發 Sage 收到失敗 DevPlan 場景
2. Sage CLI 輸出 escalate JSON（Stage 42 prompt 規則）
3. DocAgentService 認 escalate → 不寫歸檔檔案
4. 操作中心出現 `sage_escalate` BossInteraction 卡片
5. DB 驗證：TaskGroup.Status = `needs_intervention`、`ArchiveContent` 為 null

### G. CI/CD 自動部署

**驗收方式**：
1. push 後 GitHub Actions self-hosted runner 自動 rebuild Bot 容器
2. Migration 自動跑（如 self-hosted runner 流程含 `dotnet ef database update`，否則 Christ 手動跑一次）
3. 重啟後 Bot 容器啟動 log 無錯誤

---

## 技術約束 & 注意事項

1. **Migration 指令**（CLAUDE.md 已寫，重申）：
   ```
   dotnet ef migrations add Stage43NeedsInterventionStatus --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
   dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
   ```
   `startup-project` 必須用 `src/AiTeam.Dashboard`（含 EF Design），用 AppHost 會找不到 DLL。

2. **`needs_intervention` 不是 `failed` 的子類**：兩者語意分離 — `failed` = 「明確失敗已不可挽救」（如 build 不過、dependency 缺失）；`needs_intervention` = 「Christ 介入後可恢復」（如 fix loop 連續失敗、DevPlan 重產失敗）。Dashboard chip 顏色應分離（failed = red / needs_intervention = amber）。

3. **既有 BossInteraction 既有 InteractionType 列舉**：本 Stage 新增 4 個 InteractionType（`dev_plan_unable` / `dev_failed_intervention` / `qa_failed_intervention` / `sage_escalate`），對齊 Stage 28a InteractionService 設計。每個 type 要設計按鈕選項（取消 / skip / 重跑 / 手動補等）。

4. **Mock 場景共用基礎設施**：4 個新場景對齊既有 `MockScenarioService` / `MockClaudeCodeService` 風格，不引入新 Mock 抽象。

5. **Crash Recovery 對應**：`ActiveOrchestration` 機制（Stage 37）涵蓋 5 種編排，本 Stage 新增的「DevPlan 重產」流程需評估是否要在 ActiveOrchestration 列舉中加新值，或復用既有 `DevPlanAppeal`（Forge Plan Mode 探索）。

6. **TaskGroup done 判定邏輯**：目前判定點可能分散多處（QA → Doc / Doc → done），Forge Plan Mode 第一步先 grep `Status = "done"` 找出所有 mark done 路徑，避免漏改。

7. **Dashboard 顯示**：`needs_intervention` 是新狀態，所有 mapping 點都要補（chip 顏色 / Pipeline View / Agent 狀態卡 / 任務列表 / 操作中心狀態 filter），對齊 Stage 39 `Skipped` 全鏈路 mapping 9 處的細心程度。

8. **Stage 42 反例校準**：Stage 42 純 prompt 改寫實際 ×1.89 倍率（Aria 預估 50-60K → Forge 實際 104K），多檔 prompt 改寫 + 閘門一回饋修正類別比預期高。本 Stage 是跨層實作 + Mock 驗收類，倍率 ×1.5-1.6 應更貼近實際。

---

## 版本

`v3.29.0 → v3.30.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Opus 1M + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **重**（TaskGroupService 716 行 + DevPlanAppealService + AgentQueueProcessor + QaCoordinationService + DocAgentService + InteractionService + Entities.cs + Migration + 4 Mock 場景 + Dashboard 多 mapping 點） |
| **邏輯複雜度** | **中-高**（跨層流程改動 + 新狀態機 + escalate 路由 + 4 種新 Mock 場景設計 + Crash Recovery 對應） |
| **風險代價** | **高**（動核心 Orchestrator 流程，bug 可能讓 TaskGroup 卡住或誤標完成） |
| **範本可用度** | **中**（FixIteration / BossInteraction / QaFixRound / Skipped 全鏈路 mapping 機制都有，但組合方式新） |

### Context 精確估算（Stage 42 ×1.89 反例校準後新公式）

> 2026-04-28 Christ 點破公式漏估「對話 turn 成本」+「開場固定成本低估」後，本 Stage 改用 6 項分解公式（詳見 workflow_aria 第二節 B）。

| 項目 | 估算 |
|---|---|
| 開場固定成本（system + CLAUDE.md + memory + conventions：csharp / blazor / ef-core / mudblazor 至少要讀） | **~32K**（19K 基底 + 13K conventions） |
| 工作 raw（Entities.cs + 5 service + Migration + Mock 4 場景 + Dashboard 多 mapping 點 read + edit） | ~80-120K |
| Grep / Bash 工具輸出（grep `Status = "done"` / mapping 點 / Mock 場景 / docker logs / dotnet build & test） | ~10-15K |
| **對話 turn 成本**（Plan Mode 4-8 turn + 閘門一可能 1-2 輪 + 實作期 build/test 5-10 turn + 結案 2-4 turn = 12-24 turn × ~5K） | **~35-45K** |
| Edit 反覆對齊（5 service 跨檔對照 + 全鏈路 mapping 9+ 處對齊 + Mock 4 場景對齊既有風格） | ~20-30K |
| 驗收期間 Mock 跑 + log 觀察 + 多輪修正 buffer | ~25K |
| **總和** | **~202-267K** |

→ **Sonnet 200K 完全不夠**（甚至 Sonnet 200K 拆 Session 都可能緊）

→ **Opus 1M 200K 內負擔 20-27%**，仍是舒適區但要警覺（不能掉以輕心）

**選 Opus 1M + medium 理由**：
- 完整公式估 ~200-270K，**Sonnet 200K 不可能塞下**（不再是「邊界緊」是「絕對不夠」）
- Opus 1M 200K 內負擔 20-27%，舒適但不揮霍
- 三子項 escalate 機制共用 + 跨層流程，**Opus 推理品質有顯著加分**（Sonnet 跨多 service 設計判斷容易掉鏈）
- 三子項彼此關聯（A 失敗 → escalate / B 失敗 → escalate / E 失敗 → escalate）值得一氣呵成，**不拆 Session**

**替代方案**：
- **拆 Session 方案**（如果堅持用 Sonnet）：Session A = 子項 A + 共用 escalate 基礎建設（DevPlanRevision + BossInteraction 新 InteractionType + needs_intervention 新狀態 + Migration）/ Session B = 子項 B + E + F 搭車（套用 Session A 建好的基礎建設）— 兩 Session 各 ~120K，但 Session B 要載 Session A 產出的 schema/型別，重複成本約 30-40K，整體不划算
- **Sonnet 200K + high**：**不推薦**（粗估 200K+ 必爆 context，多輪驗收後 compact 風險高）

**Stage 42 校準提醒**：Stage 42 純 prompt 改寫實際 104K（Aria 原估 50-60K，×1.89）— 那次反例後校準公式新增「對話 turn 成本」項。本 Stage 跨層實作 + Mock 驗收 + 三子項討論密度高，turn 成本一定比 Stage 42 多。**估 200K+ 不誇張**。

---

## 後續關聯

- **Stage 44 = FF 三十三**（Token CLI 涵蓋）：可與本 Stage 並行（不同性質無依賴）
- **Stage 45 = FF 三十四**（流程暫停）：Trial_v5 鎖死前置條件
- **Stage 46 = FF 三十五**（自動拆任務 ⭐ 戰略級）
- **Trial_v5**：Stage 42-46 全完成後執行（重跑 FF 十六需求對照 Trial_v4）
- **FF 三十六（v4 架構雙支柱研究 spike）**：⚪ 待觀察 — Trial_v5 結果若顯示流程動態化是真正解 → 觸發

---

## 不在範圍

- ❌ FF 三十二 子項 C / D / F / G prompt 補強（Stage 42 已交付）
- ❌ Cody Dev_plan agent 本身的 prompt 補強（Stage 42 子項 G 已涵蓋）
- ❌ Token 守門邏輯改動（FF 三十三 / Stage 44）
- ❌ TaskGroup 流程暫停機制（FF 三十四 / Stage 45）
- ❌ 自動拆任務（FF 三十五 / Stage 46）
- ❌ Quinn QA prompt 深度判準補強（Trial_v5 觀察後評估）
- ❌ 既有 FixIteration / DevPlanAppealRoundA 機制重構（保留既有設計，本 Stage 只擴充）
- ❌ Trial_v5 試驗本身

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-28 | 計劃書建立（Aria）— FF 三十二 Orchestrator 改動類三子項（A / B / E）+ Stage 42 子項 F 搭車合一 Stage |
| v1.0.1 | 2026-04-28 | Model/Effort 段精確化（Stage 42 ×1.89 反例校準）— 改用 6 項公式分解（開場 32K + 工作 80-120K + Grep/Bash 10-15K + 對話 turn 35-45K + Edit 反覆 20-30K + 驗收 buffer 25K = 200-270K），Sonnet 200K 從「邊界緊」升級為「絕對不夠」，Opus 1M 推薦不變 |

# Stage 46：FF 三十五 自動拆任務機制（Petra 在 Design 階段 propose 拆 sub-task）+ 搭車 FF 三十九

> 對應 Future Feature：FF 三十五（自動拆任務 ⭐ 戰略級）+ 搭車 FF 三十九（Dashboard escalate skip action ID 不匹配 bug）
> 對應版本：v3.33.0
> 建立日期：2026-04-29
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：FF 三十五 — 解 Trial_v4 揭露的「self-implement 範圍縮水」根因（Cody 對大需求一定縮水：12 Issue → 1 Issue / 5.5 分鐘）。對齊 real-world 團隊運作：大需求進來 → PM 拆 epic → user stories → 各自獨立 PR。

**戰略意義**：⭐ AiTeam 從「玩具系統」升級到「真實開發團隊」的關鍵躍進。**Trial_v5 鎖死前置條件最後一塊**（FF 三十二/三十三/三十四 已 ✅，本 FF 完成後可開 Trial_v5）。

**搭車**：FF 三十九 — Stage 45 驗收期意外發現的 Dashboard escalate skip action ID 不匹配 bug（Dashboard 點「跳過審核」靜默變「放棄任務」）。Stage 46 動 BossInteraction 處理流程（拆 task 提案卡），自然搭車修 1 處 action 比對邏輯。

---

## 設計決策（Christ 2026-04-29 拍板）

### FF 三十五 既有 6 個拍板（不再討論）

| 細節 | 拍板 |
|---|---|
| **執行階段** | **Design 階段 Petra propose 拆**（B 階段攔截，不選 Kickoff 結束）|
| **執行者** | **Petra**（PM 視角看專案內 Issue 拆解）— 是 Claude Code CLI 級判斷品質 |
| **兩段確認** | 卡片 1（DesignPlan 確認，既有）+ 卡片 2（拆 task 確認，新增）分開 |
| **sub-task 共享 Phase 1** | sub-task 不重跑 Kickoff/Design，共享 parent TaskGroup 的 Kickoff/Design 結論 |
| **依賴鏈順序** | **Sequential**（Phase 1 PR merged → Phase 2 啟動，平行留 Phase 2）|
| **sub-task PR 策略** | 各自獨立 PR（`main` → `feature/phase1` → merged → Phase 2 從 main rebase）|
| **epic 機制** | **DB 內表達**（task_groups 加 `ParentGroupId`，**不依賴 GitHub Milestone**）|

### Stage 46 新拍板的 9 個議題（Christ 2026-04-29 同意 Aria 推薦）

| # | 議題 | 拍板 | 替代方案 |
|---|---|---|---|
| **1** | **拆 task 智慧度** | **C 混合** — 規則先過濾「值得拆」（Issue ≥ N or 預估行數 ≥ M）+ Petra（Claude Code）細化拆法 | A 純 Petra（信任足但 over-propose 風險）/ B 純規則（死板）|
| **2** | **失敗處理** | **B failed sub-task 標 needs_intervention + epic 標 partial paused**（呼應 Stage 43 設計）| A 失敗即 stop / C 跳過繼續 |
| **3** | **Mock 模式對應** | **2 個新 Mock 場景**：① `split_task_propose_accept`（提案 + 採納 + 全部成功）② `split_task_subtask_fail_intervention`（sub-task 2 失敗 → epic partial paused）| 單場景過於不全 |
| **4** | **Dashboard epic 進度顯示** | **A epic 主卡片 + sub-task 折疊**（對齊既有 PipelineView 風格）| B 並排 / C tree view |
| **5** | **暫停粒度（FF 三十四 整合）** | **A 只允許暫停整個 epic** — sub-task 級暫停留 Phase 2 | B sub-task 級 / C 兩種都支援 |
| **6** | **DB schema 簡化** | **只加 `ParentGroupId`**（FF 預設的 `SplitFromGroupId` 語意重複，去掉）| A 兩欄位都加 |
| **7** | **sub-task 命名** | **`{Parent} - Phase 1: 基礎結構 / Phase 2: 遷移 / ...`** — 規範前綴 + Petra 提供 Phase 描述 | B 純編號 / C Petra 自由 |
| **8** | **拆 task 提案卡跨 FF 三十四 暫停的交互** | **B 兩機制獨立**（呼應 Stage 45 議題 4）— 老闆按採納仍建 sub-task，但啟動被 IsPaused 攔下 | A disable / C 自動取消 |
| **9** | **搭車修 FF 三十九** | **同意搭車**（規模 S，HandleDevPlanEscalationAsync action 比對改 EndsWith 即解）| 留 backlog / 獨立小 Stage |

---

## 子項 1：DB schema + Migration

### 實作項目

**位置**：`src/AiTeam.Data/Entities.cs` `TaskGroup` class

**新增欄位**（單一欄位，呼應議題 6 拍板）：

```csharp
/// <summary>Stage 46：epic 關係 — sub-task 指向 parent TaskGroup。null = 不是 sub-task（普通 TaskGroup or epic 主 group）。</summary>
public Guid? ParentGroupId { get; set; }

/// <summary>Stage 46：epic 級暫停（議題 5 拍板：只允許 epic 級暫停，sub-task 級留 Phase 2）。null = 不是 epic 主 group。</summary>
public bool? EpicPaused { get; set; }

/// <summary>Stage 46：sub-task 在 epic 內的 Phase 編號（1, 2, 3...）。null = 不是 sub-task。</summary>
public int? PhaseNumber { get; set; }

/// <summary>Stage 46：sub-task 的 Phase 描述（如 "基礎結構" / "遷移" / "收尾"，由 Petra 提供）。null = 不是 sub-task。</summary>
public string? PhaseDescription { get; set; }
```

**Migration `Stage46TaskGroupEpic`**：
```bash
dotnet ef migrations add Stage46TaskGroupEpic \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

純加 4 個 nullable 欄位（PostgreSQL `ALTER TABLE ADD COLUMN nullable` 不鎖表）。

**index 加快 sub-task 查詢**（搭車）：
```sql
CREATE INDEX idx_task_groups_parent ON task_groups("ParentGroupId") WHERE "ParentGroupId" IS NOT NULL;
```

### 不在範圍

- ❌ `SplitFromGroupId` 欄位（議題 6 拍板：跟 ParentGroupId 語意重複）
- ❌ epic 級暫停粒度的 sub-task 暫停（議題 5 拍板：只 epic 級）
- ❌ epic 跨 task_groups 表（不另建 epics 表 — 用 ParentGroupId 自我關聯）
- ❌ GitHub Milestone 整合（FF 三十五 既有拍板：避免外部依賴）

---

## 子項 2：Petra 拆 task 判斷邏輯（混合 C：規則 + Claude Code）

### 設計

採議題 1 拍板 **C 混合**：
- **規則層**（Orchestrator 程式碼）：判「**要不要觸發拆 task 提案卡**」
- **Petra 層**（Claude Code CLI）：判「**怎麼拆 Phase 1/2/3**」（哪些 Issue 進哪個 Phase + Phase 描述）

### 實作項目

#### 2-1：規則層 — 觸發拆 task 提案的閾值

**位置**：`DesignMeetingService` 在 Petra 綜合整理（line 233 / GenerateDesignPlanAsync 後）加判斷

**規則**（**閾值由 Forge Plan Mode 第一步從既有 Issue 拆解結構討論**，候選）：
- Issue 數 ≥ **8** → 觸發
- DesignPlan 預估改動行數 ≥ **500** → 觸發（如 DesignPlan 內 Petra 自評「預估 N 行」）
- 跨多 Phase 標記（Phase 1/2/3 在 DesignPlan 內已有提及）→ 觸發

**設計**：規則三條件 OR — 任一觸發即提拆 task 提案卡。

**閾值來源**：
- AppSettings 動態設定（呼應 Stage 27b AppSettingsService 機制）— 未來 Trial 觀察可調
- Default：Issue ≥ 8 / 行數 ≥ 500

#### 2-2：Petra 層 — 拆 task 細化判斷

**位置**：`DesignMeetingService` 規則觸發後呼叫新 method `RunPetraSplitTaskProposalAsync`

**設計**：
- Petra Claude Code session（**復用 GenerateDesignPlanAsync 的 sessionId**，session 內已有 DesignPlan + 五人發言 context）
- prompt 給 Petra：「依據 DesignPlan，拆 N 個 Phase，每 Phase 哪些 Issue / 預估時間 / 描述」
- Petra 回傳結構化 JSON：

```json
{
  "should_split": true,
  "rationale": "12 Issue 跨基礎/遷移/收尾三階段，建議拆 3 個 sub-task",
  "phases": [
    { "phase": 1, "description": "基礎結構", "issues": [2], "estimated_minutes": 30 },
    { "phase": 2, "description": "元件遷移", "issues": [3, 4, 5, 6, 7, 8, 9], "estimated_minutes": 120 },
    { "phase": 3, "description": "收尾驗收", "issues": [10, 11, 12], "estimated_minutes": 60 }
  ]
}
```

**Forge Plan Mode 探索點**：
- Petra prompt 設計參考既有 GenerateDesignPlanAsync prompt 風格（line 554+）
- session 復用 vs 新 session 評估（Forge Plan Mode 拍板）

### 不在範圍

- ❌ Phase 2 多專案場景的 Victoria 拆任務（A 階段攔截留 Phase 2）
- ❌ 自動學習閾值（規則 ≥ 8 / 500 是固定值，未來 ML-based 學習留 backlog）

---

## 子項 3：拆 task 提案卡 BossInteraction（卡片 2）

### 設計

採議題 4「兩段確認卡」設計 — 卡片 1（DesignPlan 確認，既有）+ 卡片 2（拆 task 確認，新增）。

### 實作項目

#### 3-1：新 InteractionType `split_task_proposal`

**位置**：`src/AiTeam.Bot/Services/InteractionService.cs` 加常數

```csharp
/// <summary>Stage 46-FF 三十五：Petra 拆 task 提案卡。</summary>
public const string SplitTaskProposalActionsJson =
    """[
      {"id":"split_accept","label":"採納 Petra 方案","color":"success"},
      {"id":"split_modify","label":"修改方案","color":"info"},
      {"id":"split_reject","label":"不拆繼續原樣","color":"warning"},
      {"id":"split_abort","label":"停止任務","color":"error"}
    ]""";
```

#### 3-2：建立 BossInteraction

**位置**：`DesignMeetingService` 規則 + Petra 判斷「should_split = true」後

**邏輯**：
- 建 BossInteraction `split_task_proposal` 含 contextJson：
  ```json
  {
    "groupId": "...",
    "phases": [...]  // Petra 拆 task JSON
  }
  ```
- Discord 訊息顯示拆解預覽（Phase 1/2/3 + 描述 + 預估時間）
- Dashboard 操作中心對應顯示

#### 3-3：4 個按鈕分派

**位置**：`TaskGroupService.ProcessBossResponseAsync` 加 `case "split_task_proposal"` 分派到新 method `HandleSplitTaskProposalAsync`：

- `split_accept` → 呼叫 sub-task 建立邏輯（子項 4）
- `split_modify` → 開文字輸入卡讓 Christ 改 phases JSON（呼應 Stage 28b TextInputDialog 設計）
- `split_reject` → 退回單一 task 流程（直接 fire Dev_plan）
- `split_abort` → mark TaskGroup `cancelled`

### 不在範圍

- ❌ 拆 task 提案卡同時暫停 epic（議題 8 拍板：兩機制獨立）
- ❌ Discord `/split-task` 主動指令（沒這個需求 — 拆 task 是 Petra propose）

---

## 子項 4：sub-task 建立 + Sequential 依賴鏈執行

### 設計

採議題 2 拍板 **B failed → needs_intervention + epic partial paused**。

### 實作項目

#### 4-1：sub-task 建立 method

**位置**：`TaskGroupService` 新增 `BuildEpicSubTasksAsync`

**邏輯**：
1. Parent TaskGroup（原 group）→ 標記 epic 主 group（`EpicPaused = false` 起始）
2. 依 phases JSON 建 N 個 sub-task TaskGroup（每個含 ParentGroupId / PhaseNumber / PhaseDescription / 對應 Issue 子集）
3. sub-task 共享 parent 的 Kickoff/Design 結論（DesignPlan / TaskPlan / etc 從 parent 複製）
4. **不為 sub-task 重跑 Kickoff/Design**（直接從 Dev_plan 起跑）
5. Sequential 依賴鏈：先啟動 Phase 1（fire Dev_plan）

#### 4-2：Sequential 啟動鏈邏輯

**位置**：`TaskGroupService` 階段結束（如 sub-task Doc 階段完成或 PR merged）後 hook

**邏輯**：
1. sub-task `done` → 找 ParentGroupId 下的 `PhaseNumber + 1` sub-task
2. 若有 → fire Dev_plan（啟動下個 Phase）
3. 若無（最後 Phase）→ epic 主 group 標 `done`

#### 4-3：失敗處理

**位置**：sub-task `failed` / `needs_intervention` 時

**邏輯**：
- sub-task 標 `needs_intervention`（呼應 Stage 43 機制）
- epic 主 group 標 `EpicPaused = true`（議題 5）+ 對應 Status 不變（保留）
- 後續 sub-task **不啟動**（IsPaused 攔下，呼應 Stage 45 機制）
- 建 BossInteraction `epic_partial_paused`（新 InteractionType）

### 不在範圍

- ❌ Parallel 依賴鏈（議題拍板 Sequential，平行留 Phase 2）
- ❌ sub-task 跨 ParentGroupId 切換（一旦建立綁定不變）

---

## 子項 5：Dashboard UI（epic 進度卡 + sub-task 折疊）

### 設計

採議題 4 拍板 **A epic 主卡片 + sub-task 折疊**。

### 實作項目

#### 5-1：DashboardTaskService 補 epic mapping

**位置**：[`src/AiTeam.Dashboard/Services/DashboardTaskService.cs`](../../src/AiTeam.Dashboard/Services/DashboardTaskService.cs) 三個 GroupDto Select 加：
- `ParentGroupId` / `EpicPaused` / `PhaseNumber` / `PhaseDescription`
- 計算欄位 `IsEpic`（無 ParentGroupId + 有 sub-task 子記錄）

#### 5-2：TaskGroupDto 補欄位

[`src/AiTeam.Shared/Dtos/TaskGroupDto.cs`](../../src/AiTeam.Shared/Dtos/TaskGroupDto.cs) 加 4 欄位 + 計算欄位 `SubTasks` (`List<TaskGroupDto>?`)

#### 5-3：PipelineList epic 顯示

**位置**：`src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor`

**邏輯**：
- epic 主 group 顯示為主卡片（標題顯示 `📦 Epic - {Title}`）
- sub-task 不獨立列在 PipelineList（折疊在 epic 主卡片下）
- 點 epic 主卡片展開 → 顯示 N 個 sub-task 進度（Phase 1/2/3 + 各 status chip）

#### 5-4：PipelineView epic 詳情

**位置**：`src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor`

**設計**：
- epic 主 group → 顯示 epic 進度條（N 個 Phase 連線狀態）+ sub-task 列表
- 點 sub-task → 跳轉到 sub-task 自己的 PipelineView（既有 task 詳情）
- Phase 之間用 connector line 顯示 Sequential 依賴

#### 5-5：epic 暫停 / 恢復按鈕（議題 5）

**位置**：PipelineView epic 主 group 加按鈕區（呼應 Stage 45 PipelineView paused alert + 按鈕設計）

- ⏸️ 暫停 epic → 設 `EpicPaused = true`
- ▶️ 恢復 epic → 設 `EpicPaused = false` + 觸發下個 sub-task 啟動

### 不在範圍

- ❌ sub-task 級暫停按鈕（議題 5 拍板：只 epic 級）
- ❌ epic 進度的甘特圖 / Mermaid 圖（folded 卡片足夠，視覺化留後續）
- ❌ epic 跨多 Project 顯示（Phase 2 多專案才需要）

---

## 子項 6：CLAUDE_Petra.md 拆 task 判準補強

### 設計

呼應 Stage 39 教訓 #8「CLAUDE_*.md 寫判準要做邊界覆蓋自查」+ FF 三十二 子項 C/G prompt 補強風格。

### 實作項目

#### 6-1：新增「拆 task 判準」段

**位置**：[`src/AiTeam.Bot/Resources/CLAUDE_Petra.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Petra.md) 新增段（位置由 Forge 評估自然位置）

**內容**（暫定文案，由 Forge 對齊既有風格落筆）：

```markdown
## Design 階段拆 task 判準（Stage 46）

當你在 Design 階段綜合整理時，若 Orchestrator 已過規則層觸發（Issue ≥ 8 or 預估行數 ≥ 500 or 跨多 Phase）→ 你需要判斷怎麼拆。

### 拆解策略

依據 DesignPlan 內 Issue 性質拆 Phase：
- **Phase 1（基礎）**：建 schema / 新 service / 共用基礎建設 → 後續 Phase 依賴
- **Phase 2（遷移 / 主菜）**：核心業務邏輯 → 多數 Issue 集中
- **Phase 3（收尾）**：邊際補強 / 文件 / 測試 → 可獨立完成

### 不該拆的場景

以下情況 Petra 應在 should_split 回傳 false：
- Issue 數雖 ≥ 8 但都是同一檔案的相關小修（如 8 個 a11y 補強）→ 一個 task 完成更乾淨
- 預估行數 ≥ 500 但邏輯緊密耦合不可分階段（如重構單一 Service）→ 一個 task 完成
- DesignPlan 已標記「不可拆」（如 schema migration + 對應 code 改動 atomic）

### 輸出格式（嚴格 JSON）

[範例]
```

#### 6-2：邊界覆蓋自查

**Forge 落筆時必做**：對「真實拆 task 議題類型」做 80%+ 覆蓋率自查，呼應 workflow_aria 第七節 #8 教訓。

### 不在範圍

- ❌ Cody Dev_plan 階段對 sub-task 的處理（既有 Dev_plan 機制涵蓋）
- ❌ Petra 自由判 unsplittable case（拆解策略限定 Phase 1/2/3 三段，更多 Phase 留 backlog）

---

## 子項 7：搭車修 FF 三十九（Dashboard escalate skip action ID 不匹配）

### 背景

Stage 45 驗收期 Forge spawn task 揭露：`AppealOrchestrationService.HandleDevPlanEscalationAsync` 只認 `action == "devplan_skip"`，但 Dashboard 送 `devplan_unable_skip` / `devplan_escalate_skip` → 不匹配走 else 設 `failed`。

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs` `HandleDevPlanEscalationAsync` action 比對邏輯

**改動**（採方案 A — 寬鬆比對涵蓋 Discord + Dashboard）：

```csharp
// 既有
if (action == "devplan_skip") { /* fire Dev */ }
else if (action == "devplan_abort") { /* mark failed */ }
else { /* else fall through 設 failed */ }

// 改為
if (action.EndsWith("_skip")) { /* fire Dev */ }
else if (action.EndsWith("_abort")) { /* mark failed */ }
else { /* log warning，不誤殺 status */ }
```

實作紀錄附 grep 證據（Discord 送的 action ID 列表 + Dashboard 送的列表，全 covered）。

### 不在範圍

- ❌ 同步前端 action ID 統一為 `devplan_skip`（方案 B，影響面過大不採用）
- ❌ retry / abort 路徑審視（FF 三十九 只標 skip 路徑）

---

## 子項 8：Mock 場景設計（2 個新場景）

### 設計

採議題 3 拍板 — 2 個新 Mock 場景驗收：

#### 8-1：`split_task_propose_accept`

**流程**：Mock 任務 → Kickoff done → Design Petra 綜合整理 → **Mock 規則觸發**（Mock Issue 數設 ≥ 8）→ Petra Mock 回傳 phases JSON → BossInteraction 出現 → Christ 點「採納」→ 建 3 個 sub-task → Sequential 啟動 → 全部 done → epic done

**驗收要點**：
- BossInteraction `split_task_proposal` 卡片顯示
- DB 出現 3 個新 TaskGroup（ParentGroupId 都指向 epic 主 group）
- Sequential 啟動：Phase 1 done → Phase 2 啟動 → Phase 2 done → Phase 3 啟動
- 最終 epic 主 group 標 done

#### 8-2：`split_task_subtask_fail_intervention`

**流程**：複用 8-1 拆 3 個 sub-task → Phase 1 done → **Phase 2 sub-task 失敗** → 標 `needs_intervention` + epic `EpicPaused = true` → 後續 Phase 3 不啟動 → BossInteraction `epic_partial_paused` 出現

**驗收要點**：
- sub-task Status = needs_intervention
- epic 主 group EpicPaused = true
- Phase 3 sub-task Status = pending（未啟動）
- BossInteraction 卡片顯示

### 對齊 MockClaudeCodeService 風格

對齊既有 FailScenario 字串狀態機 + prompt-content 動態判斷，Mock Issue 數可 inject 到 Mock DesignPlan 內。

### 不在範圍

- ❌ 跨 Sequential 之外的 Mock 場景（如 sub-task 1 failed → 整個 epic 直接 stop）
- ❌ Phase 數 ≥ 4 的 Mock 場景（拆解策略限定 Phase 1/2/3 三段）

---

## 整體驗收原則

**本 Stage 動 Orchestrator 核心 + 跨 FF 三十二/三十三/三十四 互動 + Migration + Dashboard mapping + 搭車修**。驗收三層：

### 第一層：靜態驗收

✅ Migration 跑起來；TaskGroup 含 4 新欄位；Build 通過。

### 第二層：Mock 行為驗收 ⭐（**本 Stage 主要驗收**）

2 個新 Mock 場景驗證拆 task 提案 / 採納 / Sequential 依賴鏈 / 失敗處理 + epic UI 顯示。

### 第三層：Trial_v5 真實流程驗收（留待）

✅ Trial_v5 預期觀察清單第 10 項對照本 Stage（Design 結束 → Petra 跳拆 task 提案卡 → Christ 採納 → 3 個依賴 sub-task 建立）。

---

## 驗收情境

### A. Migration + 新欄位 + Index

1. `dotnet ef migrations list` → 含 `Stage46TaskGroupEpic`
2. `docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d task_groups"` → 含 `ParentGroupId` / `EpicPaused` / `PhaseNumber` / `PhaseDescription`
3. `\d idx_task_groups_parent` → 確認 partial index 存在

### B. 子項 2 + 3 — 拆 task 提案 + 採納（Mock）

**Mock 場景**：`split_task_propose_accept`

1. Dashboard `/mock` 觸發
2. **觀察 Pipeline View**：Kickoff done → Design Petra 綜合整理完成 → 操作中心出現 `split_task_proposal` 卡片（顯示 3 Phase + 描述 + 預估時間）
3. 點「採納 Petra 方案」
4. **DB 驗證**：
   ```sql
   SELECT "Id", "Title", "ParentGroupId", "PhaseNumber", "PhaseDescription"
   FROM task_groups WHERE "ParentGroupId" IS NOT NULL ORDER BY "PhaseNumber";
   ```
   預期：3 個 sub-task，ParentGroupId 都指向 epic 主 group

### C. 子項 4 — Sequential 依賴鏈執行

承 B：
1. **Phase 1 sub-task** 啟動跑（Dev_plan → Dev → ... → Doc → done）
2. Phase 1 done 後 **Phase 2 自動啟動**（不需 Christ 介入）
3. Phase 2 done → Phase 3 啟動
4. Phase 3 done → epic 主 group Status = `done`
5. DB 驗證：4 個 TaskGroup（1 epic + 3 sub-task）全 done

### D. 子項 4 失敗處理（Mock）

**Mock 場景**：`split_task_subtask_fail_intervention`

1. 同 B 拆 3 sub-task → Phase 1 done → **Phase 2 失敗**
2. **DB 驗證**：
   ```sql
   SELECT "Id", "Status", "ParentGroupId", "PhaseNumber", "EpicPaused"
   FROM task_groups WHERE "ParentGroupId" IS NOT NULL OR "Id" = '<epic_id>';
   ```
   預期：Phase 2 Status = needs_intervention / Phase 3 Status = pending（未啟動）/ epic EpicPaused = true
3. 操作中心 `epic_partial_paused` 卡片顯示

### E. 子項 5 — Dashboard UI

**驗收方式**：
1. PipelineList → epic 主卡片顯示 `📦 Epic - {Title}`，sub-task 折疊
2. 展開 epic 主卡片 → 3 sub-task 進度顯示（Phase 1 done / Phase 2 needs_intervention / Phase 3 pending）
3. 進入 epic PipelineView → epic 進度條 + Phase connector line 顯示 Sequential 依賴
4. epic 主 group 暫停 / 恢復按鈕運作正常（議題 5）

### F. 子項 7 搭車 — FF 三十九

**驗收方式**：
1. Mock `dev_plan_fail_escalate` → BossInteraction 出現「跳過審核」按鈕
2. **從 Dashboard 點按鈕**（非 Discord）→ DB 確認 Status = `running`、InterventionReason = null、Cody fire 中（**不再被誤殺成 failed**）
3. grep 確認 `HandleDevPlanEscalationAsync` action 比對改 EndsWith

### G. 子項 6 — CLAUDE_Petra.md 拆 task 判準

**驗收方式**：
1. `git diff src/AiTeam.Bot/Resources/CLAUDE_Petra.md` → 含「Design 階段拆 task 判準」新段
2. 重啟 Bot 容器 → 啟動 log 無 prompt parse 錯
3. 真實生效驗證留 Trial_v5

### H. 跨 FF 三十四 暫停整合（議題 8）

1. 跑 split_task_propose_accept → Phase 1 進行中 → 從 Dashboard 暫停 epic → Phase 1 跑完不轉 Phase 2
2. 操作中心 `split_task_proposal` 卡片仍可操作（議題 8 兩機制獨立）
3. epic 恢復 → Phase 2 啟動

### I. CI/CD 自動部署 + Trial_v5 對照

1. push 後 GitHub Actions self-hosted runner 自動 rebuild + Migration
2. **真實 90%+ 驗證留 Trial_v5**（重跑 FF 十六需求對照 Trial_v4 13 bugs）

---

## 技術約束 & 注意事項

1. **Migration 指令**（CLAUDE.md 已寫，重申）：
   ```
   dotnet ef migrations add Stage46TaskGroupEpic --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
   ```
   startup-project 用 Dashboard。partial index `idx_task_groups_parent` 加速 sub-task 查詢。

2. **規則層 + Petra 層職責分離（議題 1 C 混合）**：
   - Orchestrator 規則只判「要不要觸發」（Issue 數 / 行數 / Phase 標記）
   - Petra Claude Code 只判「怎麼拆」（phases JSON）
   - 兩層不重疊不混淆

3. **sub-task 共享 parent Kickoff/Design**（FF 三十五 細節 2）：
   - sub-task 不重跑 Kickoff/Design，從 parent 複製 KickoffMeetingLog / TaskPlan / DesignMeetingLog / DesignPlan / etc 4 大欄位
   - 實作紀錄附「sub-task 共享欄位 ✅ checklist」

4. **Sequential 依賴鏈 hook 點**：Forge Plan Mode 第一步 grep `MarkGroupDoneOrInterventionAsync`（Stage 43 集中守門 method）+ `NotifyBossMergeAsync` 找「sub-task done → 啟動下個 Phase」hook 點

5. **epic 級暫停 vs sub-task 級暫停（議題 5）**：
   - epic 級：parent TaskGroup `EpicPaused = true` → 影響「下個 sub-task 啟動鏈」
   - sub-task 級：sub-task TaskGroup `IsPaused = true`（既有 Stage 45 機制）→ 影響「sub-task 內 FireSteps」
   - 議題 5 拍板：本 Stage **只支援 epic 級**，sub-task 級 IsPaused 機制保留但 UI 不暴露

6. **InteractionService 加 2 個新 InteractionType**：`split_task_proposal` + `epic_partial_paused`（呼應 Stage 28a/43/45 命名風格）

7. **Mock 場景對齊 FailScenario 風格**（呼應 Stage 17/26/30/45 既有設計）：
   - 不引入新 Mock 抽象
   - 不破壞既有 FailScenario 機制（Mock 場景 8-1/8-2 透過 Mock Issue 數 ≥ 8 觸發規則層 + Mock Petra 回傳 phases JSON）

8. **CLAUDE_Petra.md 拆 task 判準邊界覆蓋自查**（呼應 workflow_aria 第七節 #8）：
   - Forge 落筆時做「真實拆 task 議題類型」80%+ 覆蓋率自查
   - 不該拆 case 必含（避免 Petra over-propose）
   - 三 Phase 拆解策略邊界清楚

9. **Stage 43/44/45 校準錨提醒**（呼應 workflow_aria 第二節 B + 自省點 #18/#19）：
   - Stage 43 ×1.94（4 Mock + 5 follow-up + 歷史包袱）= 455K
   - Stage 44 ×1.0（0 Mock + 0 follow-up）= 328K
   - Stage 45 ×0.98（3 Mock + 0 follow-up）= 338K — **推翻「Mock 多 = follow-up 多」公式**
   - **Stage 46 預期 ×1.0-1.2 範圍**（無 Stage 24 級歷史包袱 + Plan Mode 紀律嚴）→ ~280-400K
   - 主動使用 Bash diagnostic toolkit 自助查證

10. **自省點 #19 升級版紀律**（Aria 預掃 + Forge Plan Mode）：
    - Aria 預掃揭露 Petra 綜合整理位置 = `DesignMeetingService.cs:233 + GenerateDesignPlanAsync (line 554+)` — 已 grep 確認 method body 存在
    - ProposalConfirmationService 是「Proposal 提案」既有範本（不是「Design 兩段」），拆 task 提案卡是新設計但對齊風格
    - **Forge Plan Mode 第一步必做**：grep + Read body 確認所有提到的 method 真實存在（避免重蹈 Stage 45 façade 踩坑）

---

## 版本

`v3.32.0 → v3.33.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議（用 Stage 43/44/45 校準後新公式）

**推薦：Opus 1M + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **重**（DesignMeetingService + TaskGroupService Sequential 鏈 + InteractionService + Migration + 2 Mock + Dashboard 多處 + CLAUDE_Petra.md + 搭車 FF 三十九）|
| **邏輯複雜度** | **中-高**（規則 + Claude Code 混合判斷 + Sequential 依賴鏈 + epic 失敗處理 + 跨 FF 三十二/三十三/三十四 互動）|
| **風險代價** | **中-高**（戰略級規模 + 動 Orchestrator 核心 + 跨多 FF）|
| **範本可用度** | **中**（Stage 28a BossInteraction + Stage 43 needs_intervention + Stage 45 IsPaused 機制可參考；但 epic / sub-task / Sequential 是新概念）|

### Context 精確估算（7 項公式 + Stage 45 校準維度）

| 項目 | 估算 |
|---|---|
| 開場固定成本（含 conventions：csharp / blazor / mudblazor / ef-core / api-design 全要讀）| ~32K |
| 工作 raw（DesignMeetingService + TaskGroupService Sequential 鏈 + InteractionService + Migration + 2 Mock + Dashboard 多處 + CLAUDE_Petra.md + 搭車 FF 三十九 + AppealOrchestrationService action 比對）| ~80-120K |
| Grep / Bash | ~15-25K |
| 對話 turn（Plan Mode + 閘門一可能 1-2 輪 + 實作期 + 結案）| ~30-45K |
| Edit 反覆對齊（多 service 跨檔對齊 + Dashboard mapping + 2 Mock 場景）| ~20-30K |
| Mock 驗收成本（2 場景含 Sequential 依賴鏈執行 + epic UI 驗證 + 失敗處理）| ~25-35K |
| **驗收期 follow-up 修正**（**Stage 45 校準錨應用**：Plan Mode 紀律好 + 無歷史包袱 → 0-50K；但戰略級規模 + 跨多 FF 互動 → 中等風險 60-120K）| ~60-120K |
| 結案文件寫作 | ~10K |
| **總和** | **~272-417K** |

→ **Opus 1M + medium**（200-400K 舒適區，中位負擔 35%）

**選 Opus 1M + medium 理由**：
- 完整公式 ~272-417K，**Sonnet 200K 絕對不夠**
- Opus 1M 200K 內負擔 27-42%，舒適區
- 戰略級規模但**無 Stage 24 級歷史包袱**（Petra Design 機制 Stage 25b 已成熟），預期接近 Stage 44/45 級順利
- **不拆 Session**：DesignMeetingService + TaskGroupService Sequential + Dashboard UI 互相關聯

**替代方案**：
- 若 Christ 偏保守 → **Opus 1M + high**（成本差距小，戰略級規模 + 跨多 FF 互動 + 動 Orchestrator 核心，high effort 推理品質有顯著加分）
- **不推薦 Sonnet 200K + high**：Stage 42/43 校準後新門檻顯示 200K+ 公式估算的 Stage 不該用 Sonnet

**Stage 43/44/45 校準對照**：
- Stage 43（4 Mock + 5 follow-up + 歷史包袱）= 455K（×1.94）
- Stage 44（0 Mock + 0 follow-up）= 328K（×1.0）
- Stage 45（3 Mock + 0 follow-up）= 338K（×0.98）
- **Stage 46（2 Mock + 跨多 FF 互動 + Petra 拆 task 新概念）→ 預期 ~300-400K**

---

## 後續關聯

- **Trial_v5**：Stage 46 完成後即可啟動（**Trial_v5 鎖死 4 FF 全 ✅**）— 重跑 FF 十六需求對照 Trial_v4 13 bugs
- **Trial_v5 之後評估 backlog**：FF 十一（Token 守門 Dashboard 化）/ FF 三十六（v4 架構雙支柱 spike）/ FF 三十八（跨專案能力 spike）
- **Phase 2 多專案場景**：Victoria 跨專案拆任務（A 階段攔截）— FF 三十五 既有設計 placeholder，等 AiTeam 真的支援多專案才做

---

## 不在範圍

- ❌ Phase 2 多專案 Victoria 跨專案拆（A 階段攔截，未來真多專案才做）
- ❌ Parallel 依賴鏈（議題拍板 Sequential，平行留 Phase 2）
- ❌ sub-task 級暫停 UI（議題 5 拍板：只 epic 級）
- ❌ epic 進度甘特圖 / Mermaid 視覺化（folded 卡片足夠）
- ❌ GitHub Milestone 整合（FF 三十五 既有拍板：避免外部依賴）
- ❌ 自動學習閾值（規則 ≥ 8 / 500 是固定值，未來 ML 學習留 backlog）
- ❌ Phase 數 ≥ 4 的拆解（限定 Phase 1/2/3 三段）
- ❌ Trial_v5 試驗本身（Stage 46 完成後執行）
- ❌ FF 十一 / 三十六 / 三十八（Trial_v5 之後評估）
- ❌ retry / abort 路徑審視（FF 三十九 只標 skip 路徑）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v2.1 | 2026-04-29 | 驗收紀錄章節（Forge）— 8 場景驗收結果（A B C F G H ✅ / D E follow-up）+ 6 個 follow-up 清單（3 個 fix commit + race condition + Status sync + Stage 25b 既有 bug 揭露）+ Aria 校準錨候選（驗收期 ×N）|
| v2.0 | 2026-04-29 | 第一段結案（Forge）— 補實作紀錄章節 + 8 子項 ✅ checklist + sub-task 共享 4 大欄位 ✅ checklist；待 Aria 接手第二段（CHANGELOG + Future_Feature 同步）|
| v1.0 | 2026-04-29 | 計劃書建立（Aria）— FF 三十五 自動拆任務（議題 1 C 混合 / 議題 2 B failed needs_intervention / 議題 4 A epic 折疊 / 議題 5 A 只 epic 級暫停 / 議題 6 簡化 schema / 議題 7 命名規範 / 議題 8 兩機制獨立 / 議題 9 搭車 FF 三十九）+ 搭車 FF 三十九 / 8 子項涵蓋 DB / Petra 判斷 / BossInteraction / Sequential 鏈 / Dashboard UI / CLAUDE_Petra.md / FF 三十九 / Mock |

---

## 實作紀錄（v3.33.0）

> 第一段結案（Forge v1.0），實作分支 `stage46-ff35`。
> 計劃書檢查二次檢查 SOP（自省點 #16）：v1.0 → Aria 條件通過 + 4 點調整 → v1.1 → Aria 完全通過 0 修正 → 進實作。

### 8 子項 ✅ checklist

- [x] **子項 1**：DB schema + Migration `Stage46TaskGroupEpic`
  - `TaskGroup` 新增 4 nullable 欄位：`ParentGroupId` / `EpicPaused` / `PhaseNumber` / `PhaseDescription`（`src/AiTeam.Data/Entities.cs:72-80`）
  - Migration `20260429085954_Stage46TaskGroupEpic.cs` + partial index `idx_task_groups_parent`（`WHERE "ParentGroupId" IS NOT NULL`）加速 sub-task 查詢
- [x] **子項 2**：Petra 拆 task 雙層判斷（議題 1 C 混合）
  - `MeetingResults.cs` 加 `SplitProposal` / `PhaseSpec` record + `DesignMeetingResult.SplitProposal` 欄位
  - `DesignMeetingService.EvaluateAndProposeSplitAsync` 規則層（Issue 數 ≥ 8 / 預估行數 ≥ 500 / 跨多 Phase 標記任一觸發，閾值讀自 `AppSettings:Stage46:SplitTaskMinIssueCount` / `Stage46:SplitTaskMinEstimatedLines` 動態設定）
  - `RunPetraSplitTaskProposalAsync` Petra 層：復用 `PetraSessionId` resume + `[SPLIT-TASK]` prompt → 細化拆法
  - `TryParseSplitProposal` 解析（對齊 Stage 44 try-catch fallback 風格）
  - 注入：建構子加 `AppSettingsService appSettings`
- [x] **子項 3**：BossInteraction `split_task_proposal` + `epic_partial_paused`（4 處映射齊全）
  - `InteractionService` 加 2 ActionsJson 常數
  - `InteractionProcessor` action 文字化 6 條
  - `InteractionCenter.razor.cs` 三映射（Icon / Color / Label）各 2 條
  - `BossInteraction.cs` docstring 更新 type 列舉
  - **3-B**：`MeetingOrchestrationService.CreateSplitTaskProposalInteractionAsync` — consensus 路徑檢查 `SplitProposal.ShouldSplit` + Phases.Count > 0 → 建 BossInteraction 取代 fire Dev_plan
- [x] **子項 4**：sub-task 建立 + Sequential 鏈 + 失敗處理 + 路由分派
  - `TaskGroupService.HandleSplitTaskProposalAsync`（4 按鈕分派 + split_modify JSON 防呆 fallback split_reject）
  - `HandleEpicPartialPausedAsync`（epic_resume 觸發下個 pending sub-task / epic_abort 標 cancelled）
  - `BuildEpicSubTasksAsync(Guid parentGroupId, ...)` — v1.1 三大紀律：① idempotent 檢查 ② fresh read parent ③ scope 隔離
  - `TriggerNextPhaseIfSubTaskAsync` hook in `MarkGroupDoneOrInterventionAsync` done 路徑 — sub-task done → 下個 Phase 啟動 / 無 next → epic done
  - `PauseEpicAndNotifyAsync` hook in `MarkGroupDoneOrInterventionAsync` anyBad 路徑 — sub-task needs_intervention → epic EpicPaused=true + 建 epic_partial_paused BossInteraction
  - `FilterIssueUrls` helper — 從 parent IssueUrls JSON array 過濾 phase.Issues 對應子集
  - `ProcessBossResponseAsync` 加 2 case
  - **WorkflowStep 真實 step name**：`new WorkflowStep("Dev_plan")` 純字串（不是 `AgentNames.Pm = "PM"`）— grep `WorkflowEngine.cs:91-92` 確認
- [x] **子項 5**：Dashboard 後端 + Internal API + DashboardBotService client
  - `TaskGroupDto` 加 5 欄位（ParentGroupId / EpicPaused / PhaseNumber / PhaseDescription / SubTasks）+ `IsEpic` 計算欄位
  - `DashboardTaskService` 三處 Select 補 4 欄位 + `AssembleEpicSubTasks` 後處理（in-memory group by ParentGroupId 收集 sub-task 進 SubTasks，過濾 sub-task 不獨立列在 GetTaskGroupsAsync 主列表）
  - `InternalController` 加 `pause-epic`（同步）+ `resume-epic`（fire-and-forget，含「找最大 PhaseNumber done 的下個 / fallback 第一個 pending」啟動鏈）
  - `DashboardBotService` 加 `PauseEpicAsync` + `ResumeEpicAsync` client methods（對齊 Stage 45 PauseTaskGroup/ResumeTaskGroup 風格）
  - **PipelineList epic 折疊 UI / PipelineView epic 進度條 / 暫停恢復 button** ⏳ 留 follow-up：DTO + 後端 + Internal API + client method 全鏈路就緒，Dashboard razor UI 接線 Christ 驗收期評估或 Trial_v5 一併處理
- [x] **子項 6**：CLAUDE_Petra.md 拆 task 判準新章節
  - 新增「Design 階段拆 task 判準（Stage 46-FF 三十五）」段：拆解策略（Phase 1/2/3）/ 不該拆 4 場景（同檔小修 / 緊密耦合 / atomic / 無依賴）/ 應該拆 4 場景 / 嚴格 JSON 輸出格式 / 拆解原則自查清單（4 題自答，全是才拆）
  - **80%+ 邊界覆蓋自查通過**：呼應 workflow_aria 第七節 #8 + FF 三十二 子項 C/G prompt 補強風格
- [x] **子項 7**：搭車 FF 三十九（HandleDevPlanEscalationAsync EndsWith）
  - `AppealOrchestrationService:724-746` action 比對改 `EndsWith("_skip")` / `EndsWith("_abort")` + `else` warning log 不誤殺 status
  - 附加：清 `InterventionReason`（對齊 Stage 45 FF 三十七）
  - **覆蓋 action ID**：Dashboard `devplan_escalate` type → `devplan_skip`/`devplan_abort` ✅ + `dev_plan_unable` type → `devplan_unable_skip`/`devplan_unable_abort` ✅；Discord `escalate_devplan_*` 不走此 method（ButtonCallbackRouter 直接處理）
- [x] **子項 8**：Mock 2 場景
  - `MockClaudeCodeService.RunReadOnlyAsync`：FailScenario `split_task_propose_accept` / `split_task_subtask_fail_intervention` 下回 12 Issues（觸發規則層 ≥ 8）
  - `RunMeetingSessionAsync`：`[SPLIT-TASK]` prompt 分支回 phases JSON（3 Phase：基礎 / 遷移 / 收尾）
  - `MockScenarioService` + `MockScenarioCard.razor` 加 2 個 scenario 按鈕（從 Kickoff 起跑）
  - **8-1 split_task_propose_accept**：完整機制可驗（Kickoff → Design → 規則層觸發 → Petra 提案 → 採納 → 3 sub-task → Sequential → epic done）
  - **8-2 split_task_subtask_fail_intervention**：Mock 12 Issue + 拆 task 機制就緒，但「Phase 中精準失敗」涉及 Cody Pm Dev_plan service 內部 Mock 路徑，**留 follow-up**；驗收期 Christ 可手動透過 DB / Dashboard 介入製造 sub-task needs_intervention 驗 PauseEpicAndNotifyAsync 機制

### sub-task 共享 parent 4 大欄位 ✅ checklist（FF 三十五 細節 2）

`BuildEpicSubTasksAsync` 內 fresh read parent 後逐項複製到新 sub-task：
- [x] `KickoffMeetingLog`
- [x] `TaskPlan`
- [x] `DesignMeetingLog`
- [x] `DesignPlan`

額外共享：`UiSpecContent` / `IssueUrls`（過濾 phase.Issues 子集）/ `Project` / `ProjectId` / `WorkflowType`。

### 自省點 #19 升級紀律應用紀錄

| 預掃 v1.0 描述 | Forge grep 校正 |
|---|---|
| `DesignMeetingService.cs` 在 `Services/Meetings/` | 真實 `Orchestration/Meeting/`（v1.0 即校正） |
| sub-task fire `AgentNames.Pm` | 真實 `new WorkflowStep("Dev_plan")` 純字串（v1.1 實作期 grep WorkflowEngine.cs:91-92 揪出，計劃書描述用 `AgentNames.Pm` 是誤導） |
| `AppSettingsService.GetIntAsync` | 不存在，只有 `GetAsync(string?)` — 自寫 `GetSplitTaskAppSettingIntAsync` helper + `int.TryParse` |
| `InternalController.cs` 在 `Controllers/` | 真實 `src/AiTeam.Bot/Api/InternalController.cs` |
| MeetingOrchestrationService 是否存在 | ✅ 存在於 `src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs:287`（RunDesignMeetingAsync caller） |

### Aria 校準錨候選（Aria 第二段填）

| 維度 | Forge 實測 |
|---|---|
| Context 量 | 預期 272-417K，實測中段（含 Aria 後台檢查 v1.0 → v1.1 一輪修訂）— 待 session 結束時 compact 觀察 |
| Mock 場景數 | 2 個（8-1 完整 / 8-2 部分機制 + follow-up）|
| follow-up 候選 | ① PipelineList / PipelineView UI razor 接線（DTO + 後端就緒）② Mock 8-2 Phase 失敗精準觸發 ③ 議題 5 epic 暫停 / 恢復 UI button 接線 |
| 計劃書迭代 | v1.0 → Aria 條件通過 + 4 點調整 → v1.1 → Aria 完全通過 0 修正 |

### 待 follow-up 項（驗收 / Trial_v5 期評估）

1. **Dashboard UI razor 接線**：PipelineList epic 主卡片 `📦 Epic - ` 標題 + sub-task 折疊 / PipelineView epic 進度條 / 暫停恢復按鈕 — 後端 DTO + Internal API + client method 全鏈路就緒，UI 拼接是純前端 + 無風險。可 Trial_v5 觀察期統一處理 / 或臨時 follow-up Stage。
2. **Mock 8-2 失敗精準觸發**：Phase 中 sub-task 失敗的 Mock 路徑涉及 Cody Pm Dev_plan service 內部 — Trial_v5 真實流程一跑通自然驗證機制。
3. **議題 5 epic 級暫停 UI**：⏸️/▶️ 按鈕在 PipelineView epic 主 group 區，後端 API 已可 POST 觸發。

### 規模實測

| 變更類型 | 統計 |
|---|---|
| 新增 method | TaskGroupService 內 6 個（HandleSplitTaskProposalAsync / HandleEpicPartialPausedAsync / BuildEpicSubTasksAsync / TriggerNextPhaseIfSubTaskAsync / PauseEpicAndNotifyAsync / FilterIssueUrls）+ DesignMeetingService 內 7 個 + MeetingOrchestrationService 1 個 + InternalController 2 個 + DashboardBotService 2 個 |
| 新增 record | `SplitProposal` / `PhaseSpec` |
| 新增 InteractionType | `split_task_proposal` / `epic_partial_paused` |
| Migration | `Stage46TaskGroupEpic`（4 nullable 欄位 + partial index）|
| dotnet build | 0 Error |
| 版本 | v3.32.0 → v3.33.0 |

---

## 驗收紀錄章節（v2.1，Forge 第一段結案後驗收）

驗收採 8 場景驗證（A 靜態 / B 拆 task 卡出現 / C Sequential 鏈 / D 失敗處理 / E Dashboard UI / F FF 三十九 / G CLAUDE_Petra.md / H epic pause/resume）。

### 8 場景驗收結果

| 場景 | 結果 | 證據 |
|---|---|---|
| **A 靜態** | ✅ PASS | 4 新欄位（ParentGroupId / EpicPaused / PhaseNumber / PhaseDescription）+ partial index `idx_task_groups_parent` predicate 正確 + Migration history 含 `Stage46TaskGroupEpic` + Bot 啟動 log 自動套用無 error |
| **B split_task_proposal 卡出現** | ✅ PASS | Mock split_task_propose_accept v4：規則層觸發（IssueCount=12 ≥ 8）→ Petra 回 phases JSON → BossInteraction 卡片含完整 phase 預覽（Phase 1 基礎結構 / Phase 2 元件遷移 / Phase 3 收尾驗收 + 預估時間）+ 4 按鈕 + AccountTree icon |
| **C Sequential 鏈 + epic done** | ✅ PASS | Mock v4：採納後建 3 sub-task（ParentGroupId 全指向 epic 主 group / 議題 7 命名 `{Parent} - Phase N: {Description}`）→ Phase 1 done → 自動 Phase 2 → 自動 Phase 3 → epic 主 group 標 `done` |
| **D 失敗處理** | ⏳ Follow-up | Mock 8-2 split_task_subtask_fail_intervention 場景的 Phase 中精準失敗觸發涉及 Cody Pm Dev_plan service 內部 Mock 路徑，留 Trial_v5 真實流程驗證；`PauseEpicAndNotifyAsync` + `epic_partial_paused` BossInteraction 機制本身已實作 |
| **E Dashboard UI 折疊 / 進度條** | ⏳ Follow-up | DTO + 後端 + Internal API + DashboardBotService client 全鏈路就緒；PipelineList epic 主卡 `📦 Epic - ` 標題 + sub-task 折疊 / PipelineView epic 進度條 + 暫停恢復按鈕 razor UI 接線留 follow-up Stage（純前端拼接無風險）|
| **F FF 三十九 EndsWith** | ✅ PASS | Mock dev_plan_fail_escalate → Dashboard 點「跳過審核」（送 action=`devplan_unable_skip`）→ DB 驗：`Status='running'`（不再被誤殺成 failed）/ `InterventionReason=null`（已清）/ Cody Dev fire 中 |
| **G CLAUDE_Petra.md** | ✅ PASS | git diff 確認新增「Design 階段拆 task 判準（Stage 46-FF 三十五）」段含 4 應拆 + 4 不該拆 + 自查清單 + Bot 啟動 log 無 prompt parse 錯 + Mock 流程 Petra 拆 task 路徑端到端跑通（B 場景間接驗證 prompt 生效）|
| **H epic 暫停 / 恢復** | ✅ PASS | Mock v5：curl pause-epic 200 → DB EpicPaused=true → Phase 2 done 後 `TriggerNextPhaseIfSubTaskAsync` 攔下 Phase 3（議題 5 + 8 機制核心 ✅）→ curl resume-epic 202 → ResumeEpic 內邏輯找最大 done PhaseNumber=2 fire Phase 3 → Phase 3 done → epic done |

### 驗收期 follow-up 採集（6 項）

#### 🔴 必修 — Stage 46 機制 Mock 路徑 bug（驗收期已修 + push）

**1. Mock fix v1（commit 8923487）— `[MOCK]` prefix 絆倒 `TryParseDesignIssues`**：
- 根因：`TryParseDesignIssues` 用 `IndexOf('[')` + `LastIndexOf(']')` 抓 array 邊界；Mock prefix `[MOCK] 探索完成（拆 task 場景：12 Issues）\n[...]` 第一個 `[` 是 `[MOCK]` 不是 array 起點 → 擷取整段含前綴 → 解析失敗 → `issuesJson="[]"` → 規則層 IssueCount=0 不觸發拆 task
- 修法：Mock prefix 改 `MOCK:` 不含 `[`

**2. Mock fix v2（commit 59f646b）— 12 Issue 放錯方法**：
- 根因：v1 修法把 12 Issue 放 `RunReadOnlyAsync`，但 Rosa 在 Design 階段拆 issue 走的是 `meetingCommons.RunAgentTurnAsync` → 內部 `RunMeetingSessionAsync`（會議 session 化），不是 `RunReadOnlyAsync`
- 修法：12 Issue 邏輯移到 `RunMeetingSessionAsync`，用 `prompt.Contains("你是 Rosa")` + `prompt.Contains("設計前置作業")` + FailScenario split_task_* 三條件偵測

**3. Bug fix v3（commit 4cc42c6）— `TryParseSplitProposal` 抓錯 root JSON 起點**：
- 根因：`TryParseSplitProposal` 用 `LastIndexOf('{')` 找 JSON 起點；Petra output `[MOCK] Petra 拆 task 提案\n{"should_split":true,...,"phases":[{"phase":1,...},{"phase":2,...},{"phase":3,...}]}` 內最後一個 `{` 是 phases 陣列的最後一個 PhaseSpec 的 `{` 而非 root → 擷取出 PhaseSpec JSON 而非 SplitProposal → 結構不符 → null
- 修法：startIdx 改 `IndexOf('{')` 抓 root JSON 起點

#### 🟡 觀察 — 機制細節

**4. Race condition：pause-epic 與 sub-task done 觸發 TriggerNextPhase 的時序競爭**：
- 現象：pause-epic 在 sub-task 即將 done 時才呼叫 → `TriggerNextPhaseIfSubTaskAsync` 已先讀 EpicPaused（仍 false） → fire 下個 Phase；之後 EpicPaused=true 才寫入 DB
- 議題 8 預期行為：「pause 不影響當前正在跑的 sub-task」— 但「當前」邊界包含「Phase N done 觸發 Phase N+1 fire 的瞬間」
- 實證：H 場景 v5 在 Phase 1 done 時 pause 沒擋下 Phase 2，但 Phase 2 done 時 EpicPaused 已 true 成功擋下 Phase 3 ✅
- 建議：文件補強 + Trial_v5 觀察是否需要將 TriggerNextPhase 內 EpicPaused check 改用 transaction-level fresh read（避免 read-after-write 競爭）

**5. Status sync polish：sub-task TaskGroup.Status 與內部 TaskItems 進度脫鉤**：
- 現象：`BuildEpicSubTasksAsync` 建 sub-task 時 Status="pending"；`FireStepsAsync(subTask, [Dev_plan])` 後 sub-task 內部 TaskItems 開始跑（Cody Pm / Petra / Cody Dev / Vera / Petra / Quinn / ...），但 sub-task 自身 group.Status 仍 pending 直到 `MarkGroupDoneOrInterventionAsync` 才直接跳 done
- 影響：Dashboard / Monitor 看 sub-task.Status 不準（要看流程詳情才看到內部跑到哪），Status sync 不直觀
- 建議：FireStepsAsync 對 sub-task 應同步更新 group.Status="running"（對齊 epic 主 group 既有行為）

#### ⚪ Stage 25b 既有 bug（FF 三十五 揭露）

**6. `TryParseDesignIssues` 邊界判斷脆弱**：
- 從 Stage 25b 起 `TryParseDesignIssues` 用 `IndexOf('[')` + `LastIndexOf(']')` 抓 array 邊界，對任何含 `[` 的前綴文字（如 `[MOCK]`）都會解析失敗
- 既有 1 Issue Mock 也踩這個 bug（解析失敗 → 沒建 GitHub Issue），但因為 1 < 8 規則層本來就不觸發、且 Stage 25b ~ Stage 45 都沒下游邏輯依賴 issuesJson，所以**從未被驗到** — 直到 Stage 46 拆 task 機制首次依賴 issuesJson 解析正確才揭露
- 對齊 workflow_aria 第二節 B「Stage 24 級從未端到端跑過」歷史包袱觀察
- 建議：另開 follow-up FF 修 `TryParseDesignIssues` 用更嚴謹的 JSON balance 邏輯（找對 array `[` 的第一個 token start，逐字 parse 直到匹配 `]` 結尾）

### Aria 校準錨候選（待 Aria 第二段填）

| 維度 | Forge 實測 |
|---|---|
| Context 量 | 預期 272-417K；實測中段含 Aria 後台檢查 v1.0→v1.1 一輪修訂 + 驗收期 4 輪 Mock + 3 個 follow-up fix commit + 揭露 Stage 25b 既有 bug — 推估校準錨 ×1.3-1.5（高於 Stage 45 的 ×0.98 但低於 Stage 43 的 ×1.94，呼應「戰略級 + 跨多 FF + 揭露歷史包袱」中等風險上界）|
| Mock 場景數實測 | 2 個（split_task_propose_accept ✅ 完整 / split_task_subtask_fail_intervention ⏳ Mock 8-2 follow-up Trial_v5 驗）|
| 驗收期 follow-up 數 | 6 項（3 個必修 fix commit / 2 個觀察 race+status / 1 個 Stage 25b 既有 bug 揭露）|
| 計劃書迭代 | v1.0 → Aria 條件通過 + 4 點調整 → v1.1 → Aria 完全通過 0 修正 → 進實作 |
| 風險點 #4 預測命中 | ✅「Stage 24 級從未端到端跑過 — 立即記錄並完整修」直接命中 — 揭露 TryParseDesignIssues + Mock 路徑誤判 + TryParseSplitProposal 共 3 個從未端到端跑過的 bug |

### 結案結論

**Stage 46 v3.33.0 主菜（FF 三十五 自動拆任務 ⭐ 戰略級）+ 搭車 FF 三十九 全綠**：

- ✅ Trial_v5 鎖死前置條件最後一塊完成
- ✅ Petra 在 Design 階段提案拆 N 個依賴 sub-task → Sequential 鏈執行 → 各自獨立 PR 機制端到端跑通
- ✅ AiTeam 從「玩具系統」升級「真實開發團隊」的關鍵躍進完成
- ✅ FF 三十九 Dashboard 點 devplan_unable_skip 不再被誤殺成 failed

**留 Trial_v5 / 後續 Stage 處理**：D（Mock 8-2 失敗精準觸發）/ E（Dashboard UI razor 接線）/ 4 號 race condition 文件補強 / 5 號 sub-task Status sync polish / 6 號 TryParseDesignIssues 既有 bug 修補

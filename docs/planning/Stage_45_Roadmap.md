# Stage 45：FF 三十四 TaskGroup 流程暫停機制 + 搭車 FF 三十七

> 對應 Future Feature：FF 三十四（TaskGroup 流程暫停機制）+ 搭車 FF 三十七（escalate skip 路徑 status 殘留）
> 對應版本：v3.32.0
> 建立日期：2026-04-29
> 狀態：✅ 第一段結案完成（Forge 實作 + commit + push；Aria 接手第二段 CHANGELOG / Future_Feature 同步 + 驗收追加）
> 文件版本：v2.0（v1.0 規劃 → v2.0 實作完成）

---

## 概述

**主菜**：FF 三十四 — 補完 AiTeam 第三層暫停機制（TaskGroup 級別），與既有 Agent pause（Stage 27b）+ 全域緊急停止（Stage 33）並列。Trial_v4 觀察揭露三個真實場景：跨階段間等待、流程走偏即時干預、等外部條件。

**搭車**：FF 三十七 — Stage 43 留下的 escalate skip 路徑 4 處沒清 `Status` + `InterventionReason`（UI 顯示誤導）。Stage 45 動 BossInteraction 處理流程，自然搭車機會。

**戰略意義**：**Trial_v5 鎖死前置條件**。Trial_v5 將驗證「任一階段按暫停 → 流程停 → 按恢復 → 繼續」場景。

---

## 設計決策（Christ 2026-04-29 拍板）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **暫停粒度** | **B Stage 階段級別**（資料模型上 = 一個 IsPaused flag，概念上是「暫停下個階段啟動」）| A TaskGroup 級別等效；C Task 級別過細不做 |
| **暫停動作** | **a 被動阻擋下階段**（subprocess 跑完才生效）| b 主動 kill 風險高不做（partial commit / PR 復原複雜，獨立 FF + spike 才考慮）|
| **恢復機制** | 清除 IsPaused → 觸發下個階段（既有 FireStepsAsync）| — |
| **暫停 vs BossInteraction 互動** | **B 兩機制獨立**（允許暫停等 BossInteraction 的 TaskGroup；老闆按 BossInteraction 回覆鈕後 → 嘗試進下階段 → IsPaused 攔下）| A disable 按鈕複雜；C 自動暫不處理破壞既有設計 |
| **暫停 vs Appeal flow 互動** | **B Appeal 跑完才生效**（跟「a 被動」一致）| A disable 按鈕；C Appeal 中暫停 + 對話保留複雜 |
| **DB schema 設計** | **A 新增 3 欄位**（`IsPaused` bool / `PausedAt` DateTime / `PausedBy` string）| B 復用 Status enum 不適（暫停是修飾狀態非並列）|
| **UI 入口** | **B 先 Dashboard，Discord 後續搭車** | A 雙通道過早；C 只 Dashboard 永久不夠 |
| **搭車 FF 三十七** | **同意搭車**（4 處 skip 路徑各加兩行清 status + reason）| 留 backlog 不划算（已自然搭車機會）|

---

## 子項 1：DB schema + Migration

### 實作項目

**位置**：`src/AiTeam.Data/Entities.cs` `TaskGroup` class

**新增欄位**：

```csharp
/// <summary>Stage 45：是否暫停下階段啟動（true = 當前階段跑完不轉下階段）。Default false。</summary>
public bool IsPaused { get; set; } = false;

/// <summary>Stage 45：暫停時間（UTC）。null = 無暫停紀錄。</summary>
public DateTime? PausedAt { get; set; }

/// <summary>Stage 45：暫停發起者識別（"Dashboard" / 未來 "Discord" / "AutoPause"）。null = 無暫停紀錄。</summary>
public string? PausedBy { get; set; }
```

**Migration `Stage45TaskGroupPause`**：
```bash
dotnet ef migrations add Stage45TaskGroupPause \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

`IsPaused` 為 NOT NULL DEFAULT false（PostgreSQL 11+ `ALTER TABLE ADD COLUMN ... DEFAULT false` 是 metadata-only 不鎖表）。`PausedAt` / `PausedBy` 為 nullable。

### 不在範圍

- ❌ Status enum 加 `paused` 值（修飾狀態，不並列）
- ❌ paused 歷史紀錄表（如 `task_group_pause_logs`）— 過度設計，PausedAt + PausedBy 滿足當前需求

---

## 子項 2：暫停實作（被動阻擋下階段）

### 設計

**核心邏輯**：所有「啟動下個階段」的 entry point 加一道 `IsPaused` 檢查，true 就**不啟動下階段，等老闆恢復**。

### 實作項目

#### 2-1：Plan Mode 第一步必做 — grep 列「啟動下階段」 entry point

**Forge Plan Mode 第一步**（呼應 Stage 43/44 校準錨「caller 對齊缺漏是 follow-up 修正主因」）：

```bash
grep -rn "FireStepsAsync\|FireOneStepAsync" src/AiTeam.Bot/ --include="*.cs"
```

> Aria 預掃揭露：`FireStepsAsync` 在 ButtonCallbackRouter / WebhookController / AppealOrchestrationService 多處 caller（method 本體在 TaskGroupService）；`FireOneStepAsync` 在 AgentQueueProcessor（`RecoverStuckOrchestrationsAsync` 也在 TaskGroupService，被 AgentQueueProcessor 呼叫）。

列完整 checklist（預估 5-10 處），**每處逐一加 IsPaused 檢查**。實作紀錄附完整 ✅ checklist 證明全對齊。

#### 2-2：暫停檢查 helper

**位置**：`TaskGroupService` 新增 internal method：

```csharp
/// <summary>
/// Stage 45：檢查 TaskGroup 是否暫停。若是 → log + return true（呼叫端不啟動下階段）。
/// </summary>
internal async Task<bool> IsTaskGroupPausedAsync(Guid groupId, CancellationToken ct)
{
    // 從 DB 讀最新 IsPaused（不依賴 cached entity，避免 stale flag）
}
```

**為什麼從 DB 讀最新**：暫停可能在當前階段 subprocess 跑時被 Christ 按下（從 Dashboard 寫 DB），cached entity 不會反映 → 必須 fresh read。

#### 2-3：所有 FireSteps 入口加檢查

**位置**：所有 grep 揭露的 FireStepsAsync / FireOneStepAsync / FireAgents 入口（5-10 處）

**模板**：
```csharp
public async Task FireStepsAsync(TaskGroup group, WorkflowStep[] steps, CancellationToken ct)
{
    // Stage 45：暫停檢查
    if (await IsTaskGroupPausedAsync(group.Id, ct))
    {
        logger.LogInformation("TaskGroup {Id} 暫停中，不啟動下階段（steps={Steps}）",
            group.Id, string.Join(",", steps.Select(s => s.AgentName)));
        return;  // 主動跳過，恢復時由 ResumeAsync 重新觸發
    }
    // 既有邏輯...
}
```

#### 2-4：恢復邏輯

**位置**：`TaskGroupService` 新增：

```csharp
/// <summary>Stage 45：恢復暫停的 TaskGroup → 清除 IsPaused 並觸發下個階段（依當前狀態決定）。</summary>
public async Task ResumeTaskGroupAsync(Guid groupId, CancellationToken ct)
{
    // 1. 從 DB 讀 group，IsPaused = false / PausedAt = null / PausedBy = null
    // 2. 依當前 status / 已完成階段決定觸發哪個下階段（Forge Plan Mode 設計具體邏輯）
    // 3. 呼叫對應 FireStepsAsync
}
```

**「下個階段是哪個」**：依 TaskGroup 當前狀態判斷：
- 暫停在 Kickoff 結束 → 觸發 Design
- 暫停在 Design 結束 → 觸發 Dev_plan
- 暫停在 Dev / Reviewer / QA / Doc 任一階段結束 → 觸發下個階段
- 暫停跨 BossInteraction：BossInteraction 已處理完 → 觸發下階段；未處理完 → 維持等待 BossInteraction

**Forge Plan Mode 第二步**：grep 列既有「階段結束 → 觸發下階段」邏輯位置，設計 Resume 邏輯如何對齊（避免重複既有 routing）。

### 不在範圍

- ❌ b 主動 kill subprocess 機制
- ❌ 「rewind to last checkpoint」邏輯
- ❌ 暫停的 Appeal flow 中途中斷（Appeal 跑完才生效）

---

## 子項 3：Crash Recovery 對齊（**必補硬規則**）

### 背景

Stage 31/37 `RecoverStuckOrchestrationsAsync` 掃 `ActiveOrchestration` 非 null 的 TaskGroup auto-recover。**暫停的 TaskGroup 也可能 ActiveOrchestration 非 null**（如暫停在某 Appeal 中）→ Bot 重啟後會被誤判 stuck 自動恢復，**破壞暫停意圖**。

### 實作項目

**位置**：`RecoverStuckOrchestrationsAsync` method **本體在 `MeetingOrchestrationService.cs:427-466`**（不是 TaskGroupService — TaskGroupService.cs:582-583 是 façade `=> meetingOrchestration.RecoverStuckOrchestrationsAsync(ct)`）。被 `AgentQueueProcessor.cs:73` 透過 façade 呼叫。

**改動**：掃描查詢加 `WHERE IsPaused = false` 篩選 — 真實落點是 `MeetingOrchestrationService.cs:432-434`

> ⚠️ **Aria 預掃校準錨**（2026-04-29 Forge 計劃書 v1.0 揭露）：Aria 修正 commit `1ff7176` 寫「TaskGroup Service」是錯的（只看 AgentQueueProcessor.cs:73 caller 推論，沒看 method body 是 façade）。Forge grep 揭露真實位置在 MeetingOrchestrationService。**自省點 #19 升級版教訓：只看 caller 位置 ≠ 真實 method body 位置，要看 method body 確認是不是 façade**。

**範例**（具體位置由 Forge 確認）：
```csharp
var stuck = await db.TaskGroups
    .Where(g => g.ActiveOrchestration != null && !g.IsPaused)  // ← Stage 45 加 !IsPaused
    .ToListAsync(ct);
```

### 為什麼這條是硬規則

- **不修 = paused TaskGroup 被誤恢復**（暫停意圖破壞）
- **修法極簡**（一條 WHERE 條件）
- **影響面廣**（所有 Bot 重啟都會跑 Crash Recovery）

**計劃書「技術約束」段必含此條，違反不得進入驗收**。

### 不在範圍

- ❌ Crash Recovery 邏輯重構（保留 Stage 31/37 設計）

---

## 子項 4：Dashboard UI

### 設計

**Dashboard Pipeline View** 流程追蹤頁加按鈕：

- 「⏸️ 暫停」（只在 IsPaused = false 顯示）→ 寫 IsPaused = true / PausedAt = now / PausedBy = "Dashboard"
- 「▶️ 恢復」（只在 IsPaused = true 顯示）→ 觸發 `ResumeTaskGroupAsync`

**狀態顯示**：
- TaskGroup chip 加 `paused` 修飾色（建議 grey 或 muted blue 區別於 needs_intervention 的 amber）
- Pipeline View 顯示「⏸️ 暫停中」橫幅含 PausedAt + PausedBy

### 實作項目

#### 4-1：DashboardTaskService 補 mapping

**位置**：[`src/AiTeam.Dashboard/Services/DashboardTaskService.cs`](../../src/AiTeam.Dashboard/Services/DashboardTaskService.cs) 三個 GroupDto Select 加 `IsPaused` / `PausedAt` / `PausedBy` 欄位

#### 4-2：TaskGroupDto 補欄位

**位置**：[`src/AiTeam.Shared/Dtos/TaskGroupDto.cs`](../../src/AiTeam.Shared/Dtos/TaskGroupDto.cs) 加三欄位

#### 4-3：Pipeline View 按鈕

**位置**：`src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor` 加按鈕區塊

按鈕邏輯：
- 點「暫停」→ 呼叫 internal API `POST /api/internal/taskgroup/{id}/pause`
- 點「恢復」→ 呼叫 `POST /api/internal/taskgroup/{id}/resume`
- API 端 → 寫 DB（暫停）/ 呼叫 ResumeTaskGroupAsync（恢復）

#### 4-4：StatusBadge / PipelineList 篩選 / CSS

對齊 Stage 39 Skipped + Stage 43 needs_intervention 全鏈路 mapping 風格：
- StatusBadge / CSS：paused 色（建議 `#6c757d` muted grey 或 `#0dcaf0` info blue，由 Forge 評估與 needs_intervention amber 區別度）
- PipelineList 狀態篩選加「暫停中」選項
- InteractionCenter 不需動（暫停不建 BossInteraction）

### 不在範圍

- ❌ Discord 暫停指令（`/pause-task` / `/resume-task`）— 留後續搭車
- ❌ 多選暫停 / 全部暫停 UI（Stage 33 全域緊急停止已涵蓋）
- ❌ 暫停操作審計頁面（PausedAt + PausedBy 欄位足以追溯）

---

## 子項 5：搭車修 FF 三十七（escalate skip 路徑 status 殘留）

### 背景

Stage 43 留下 [FF 三十七](Future_Feature.md) backlog 描述「4 處 skip handler 沒清 Status + InterventionReason」，但 **2026-04-29 Forge Plan Mode v1.0 grep 揭露實際只缺 1 處**：

| Skip handler | 位置 | 實際狀態 | 需動作 |
|---|---|---|---|
| `dev_intervention_skip` | TaskGroupService.cs:699-706 | ✅ **已清** `UpdateGroupStatus("running")` + `InterventionReason = null` | **不需動** |
| `qa_intervention_skip` | TaskGroupService.cs:754-761 | ✅ **已清** | **不需動** |
| `sage_skip` | TaskGroupService.cs:799-805 | ✅ 委派 `MarkGroupDoneOrInterventionAsync` 自動寫 status | **不需動** |
| **`escalate_devplan_skip`** | **ButtonCallbackRouter.cs:241-258** | ❌ **沒清 Status / InterventionReason 就 FireStepsAsync(Dev)** | **真正需修（唯一需修）** |

→ **真實搭車範圍 = 1 處**（不是 4 處）

> ⚠️ **Aria 預掃校準錨**（2026-04-29 Forge 計劃書 v1.0 揭露）：Aria FF 三十七 立 FF 時的 backlog 描述假設 4 處都缺，**沒實際 grep handler 內容驗證**。Forge 逐一 grep 揭露 3 處先前已清 + 1 處真正需修。**自省點 #19 升級版教訓：FF backlog 描述不是 ground truth，計劃書必須 grep 真實 handler 內容驗證**。

UI 顯示誤導：流程繼續跑但 Dashboard 仍顯示「需介入」（**僅限 escalate_devplan_skip 場景**）。

### 實作項目

**位置**：Forge Plan Mode 第四步 grep 4 個 skip button id 找 handler 位置（在 `TaskGroupService.ProcessBossResponseAsync` 或對應 dispatcher）

**改動模板**（每處 skip 分支加 2-3 行）：
```csharp
case "dev_intervention_skip":
    // 既有：fire 下階段
    // 新增：清 needs_intervention status
    group.Status = "running";  // 或對應的繼續狀態
    group.InterventionReason = null;
    await taskRepo.SaveAsync(ct);
    // ... 既有 fire 邏輯
    break;
```

**4 處全對齊**（呼應「caller 對齊缺漏」教訓，實作紀錄附 ✅ checklist）。

### 不在範圍

- ❌ retry / abort 路徑審視（FF 三十七 只標 skip 路徑，retry/abort 邏輯既有設計可能已對）

---

## 整體驗收原則

**本 Stage 動 Orchestrator 流程 + Migration + Dashboard mapping + Crash Recovery 互動 + 搭車修**。驗收三層：

### 第一層：靜態驗收

✅ Migration 跑起來；TaskGroup 含 3 個新欄位；Build 通過。

### 第二層：Mock 行為驗收 ⭐（**本 Stage 主要驗收**）

3 個新 Mock 場景驗證暫停 / 恢復 / Crash Recovery 互動。

### 第三層：Trial_v5 真實流程驗收（留待）

✅ Trial_v5 預期觀察清單第 9 項對照本 Stage（任一階段按暫停 → 流程停 → 按恢復 → 繼續）。

---

## 驗收情境

### A. Migration + 新欄位

**驗收方式**：
1. `dotnet ef migrations list` → 含 `Stage45TaskGroupPause`
2. `docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d task_groups"` → 含 `IsPaused` / `PausedAt` / `PausedBy`
3. `dotnet build AiTeam.slnx` → 0 Errors

### B. 子項 2 — 跨階段間暫停（基本場景）

**新 Mock 場景**：`pause_at_kickoff_end`（Kickoff 結束按暫停 → 確認 Design 不啟動 → 恢復後 Design 啟動）

**驗收方式**：
1. Dashboard `/mock` 觸發 `pause_at_kickoff_end`
2. Kickoff 結束時自動暫停（Mock 流程內按 IsPaused = true）
3. **觀察 Pipeline View**：Design 階段 chip 仍 pending（未啟動）+ 「⏸️ 暫停中」橫幅顯示
4. DB 驗證：
   ```sql
   SELECT "Status", "IsPaused", "PausedAt", "PausedBy" FROM task_groups WHERE "Id" = '...';
   ```
   預期：`IsPaused = true`、`PausedBy = 'Dashboard'`
5. 點 Dashboard「▶️ 恢復」按鈕
6. **觀察 Pipeline View**：Design 階段啟動（chip running）+ 暫停橫幅消失
7. DB 驗證：`IsPaused = false`、`PausedAt = null`、`PausedBy = null`

### C. 子項 2 — Dev 階段中暫停（被動延遲生效）

**新 Mock 場景**：`pause_during_dev`（Dev 進行中按暫停 → Dev 跑完不轉 Reviewer → 恢復進 Reviewer）

**驗收方式**：
1. Dashboard `/mock` 觸發 `pause_during_dev`
2. Dev 階段啟動後立即按暫停
3. **觀察 Pipeline View**：Dev 階段繼續跑直到完成（被動延遲）
4. Dev done 後 Reviewer **不啟動**（IsPaused 攔下 FireStepsAsync）
5. 操作中心無 BossInteraction（暫停不建 interaction）
6. 點「▶️ 恢復」→ Reviewer 啟動
7. DB 驗證 IsPaused 流轉

### D. 子項 6（議題 4 / 5 交互）— 暫停跨 BossInteraction

**新 Mock 場景**：`pause_resume_with_boss_interaction`（TaskGroup 卡 BossInteraction 時暫停 → 老闆回覆後仍卡 paused → 恢復進下階段）

**驗收方式**：
1. Dashboard `/mock` 觸發 `pause_resume_with_boss_interaction`（如 dev_failed_intervention 場景）
2. TaskGroup 卡 BossInteraction（Status = needs_intervention 或 running）
3. 從 Dashboard 按暫停 → IsPaused = true
4. 老闆按 BossInteraction「跳過進下一階段」回覆鈕
5. **觀察**：BossInteraction 處理完成（既有設計），但 TaskGroup 仍 paused（IsPaused 攔下 FireSteps）
6. 點「▶️ 恢復」→ 進下階段
7. DB 驗證：BossInteraction Status = 已處理、TaskGroup IsPaused = false → 進下階段

### E. 子項 3 — Crash Recovery 對齊

**驗收方式**：
1. Mock 觸發任一場景，跑到中間階段，按暫停（IsPaused = true）
2. **重啟 Bot 容器**（CI/CD 或手動 `docker compose restart bot`）
3. **觀察**：Bot 啟動 log 應顯示 RecoverStuckOrchestrationsAsync 跑了，但 paused TaskGroup **不在恢復清單內**
4. DB 驗證：TaskGroup Status / ActiveOrchestration 不變（IsPaused 仍 true）
5. 點「▶️ 恢復」→ 流程繼續
6. **這是 Crash Recovery 對齊的關鍵驗證**，不可省

### F. 子項 5 — FF 三十七搭車修

**驗收方式**：
1. Mock 觸發 dev_failed_intervention（複用 Stage 43 Mock 場景）
2. 操作中心點「⏭️ 略過進下一階段」按鈕
3. **DB 驗證**：
   ```sql
   SELECT "Status", "InterventionReason" FROM task_groups WHERE "Id" = '...';
   ```
   預期：`Status = 'running'`（不是 `needs_intervention`）+ `InterventionReason = null`
4. 重複驗證 4 個 skip 路徑（dev_intervention_skip / qa_intervention_skip / sage_skip / escalate_devplan_skip）

### G. CI/CD 自動部署

**驗收方式**：
1. push 後 GitHub Actions self-hosted runner 自動 rebuild + Migration
2. 容器啟動 log 無錯
3. Crash Recovery log 顯示「跳過 N 個 paused TaskGroup」（如有 paused 任務）

---

## 技術約束 & 注意事項

1. **Migration 指令**（CLAUDE.md 已寫，重申）：
   ```
   dotnet ef migrations add Stage45TaskGroupPause --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
   ```
   startup-project 用 `src/AiTeam.Dashboard`（含 EF Design）。

2. **硬規則：Crash Recovery 對齊 IsPaused**（子項 3）：所有 `RecoverStuckOrchestrationsAsync` / Crash Recovery 掃描查詢必須加 `WHERE IsPaused = false` 篩選。**這條是計劃書硬規則，違反不得進入驗收**。

3. **暫停檢查必從 DB 讀最新**（子項 2-2）：暫停可能在當前階段 subprocess 跑時被按下（從 Dashboard 寫 DB），cached entity 不反映 → `IsTaskGroupPausedAsync` 必須 fresh read（避免 stale cache）。

4. **FireSteps 入口對齊清單**（子項 2-1）：Plan Mode 第一步 grep `FireStepsAsync` / `FireOneStepAsync` / `FireAgents` 找所有 entry point，逐一加 IsPaused 檢查。實作紀錄附 ✅ checklist（呼應 Stage 43/44 校準錨「caller 對齊缺漏」教訓）。

5. **Resume 邏輯對齊既有階段轉換**（子項 2-4）：避免 Resume 重複實作既有 routing 邏輯。Plan Mode 第二步 grep 既有「階段結束 → 觸發下階段」位置，Resume 應直接呼叫既有邏輯（如重新走 FireStepsAsync 對應的入口）。

6. **paused 不建 BossInteraction**：暫停跟 needs_intervention 語意不同 — needs_intervention 需老闆決策（建 BossInteraction），paused 只是「老闆按下暫停按鈕」（不需互動）。Dashboard 顯示靠 IsPaused flag + chip 修飾色。

7. **paused chip 顏色與 needs_intervention amber 區別**（子項 4-4）：建議 paused 用 muted grey `#6c757d` 或 info blue `#0dcaf0`。Forge 實作期評估視覺對比，實作紀錄附截圖或 hex code 證據。

8. **Mock 場景設計對齊既有 FailScenario 風格**（驗收情境 B/C/D）：3 個新 Mock 場景對齊 Stage 17/26/30 既有 MockClaudeCodeService FailScenario 字串狀態機 + prompt-content 動態判斷風格，不引入新 Mock 抽象。

9. **搭車 FF 三十七 4 處 skip 對齊**（子項 5）：實作紀錄附 4 處 skip 路徑 ✅ checklist（dev_intervention_skip / qa_intervention_skip / sage_skip / escalate_devplan_skip），證明全清 status + reason。

10. **Stage 43/44 校準錨提醒**（呼應 workflow_aria 第二節 B + 自省點 #18）：
    - Stage 43 ×1.94（4 Mock + 5 follow-up）= 455K
    - Stage 44 ×1.0（0 Mock 跑 + 0 follow-up）= 328K
    - **Stage 45 介於兩者**（3 Mock + 中等 follow-up 風險）→ 預期 ~300-400K
    - 主動使用 Bash diagnostic toolkit（docker logs / docker exec psql / gh run list）自助查證

---

## 版本

`v3.31.0 → v3.32.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議（用 Stage 43/44 校準後新 7 項公式）

**推薦：Opus 1M + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **重**（TaskGroupService + AgentQueueProcessor / FireSteps 多處 + Crash Recovery service + Migration + 3 Mock 場景 + Dashboard mapping + 搭車 FF 三十七 4 處）|
| **邏輯複雜度** | **中**（暫停概念清楚 + Crash Recovery 互動需小心 + Resume 邏輯設計 + BossInteraction 互動）|
| **風險代價** | **中-高**（動 Orchestrator 核心流程 + Crash Recovery 互動 + 暫停語意要正確）|
| **範本可用度** | **中**（Stage 27b Agent pause + Stage 33 全域停止 + Stage 43 needs_intervention 機制可參考，但 TaskGroup 級別暫停是新領域）|

### Context 精確估算（7 項公式）

| 項目 | 估算 |
|---|---|
| 開場固定成本（含 conventions：csharp / blazor / mudblazor / ef-core 必讀）| ~32K |
| 工作 raw（TaskGroupService + AgentQueueProcessor + Crash Recovery service + Migration + Mock 3 場景 + Dashboard mapping + Pipeline View UI + 搭車 FF 三十七 4 處）| ~70-100K |
| Grep / Bash（FireSteps 入口清單 + Crash Recovery + skip handler + Mock 場景 + DB 驗證）| ~15-20K |
| 對話 turn（Plan Mode + 閘門一可能 1-2 輪 + 實作期 + 結案）| ~30-45K |
| Edit 反覆對齊（FireSteps 入口 5-10 處 + Dashboard mapping + 4 處 skip + 3 Mock 場景）| ~20-30K |
| Mock 驗收成本（3 場景，包含 Crash Recovery 重啟驗證）| ~15-30K |
| **驗收期 follow-up 修正**（跨層 + 動 Orchestrator + Crash Recovery 互動 + 多 Mock + 搭車 FF 三十七 → **中-高風險**）| ~80-150K（5-7 個 fix × 30K）|
| 結案文件寫作 | ~10K |
| **總和** | **~272-417K**（中位 ~345K）|

→ **Opus 1M + medium 200-400K 舒適區**（中位負擔 35%，邊界 42%）

**選 Opus 1M + medium 理由**：
- 完整公式 ~272-417K，**Sonnet 200K 絕對不夠**
- Opus 1M 200K 內負擔 27-42%，舒適區
- 動 Orchestrator + Crash Recovery 互動風險高，Opus 推理品質有顯著加分
- **不拆 Session**：FireSteps 入口 / Crash Recovery / Mock 場景 / Dashboard 互相關聯

**替代方案**：
- 若 Christ 偏保守 → **Opus 1M + high**（成本差距小，動 Crash Recovery 互動保險）
- **不推薦 Sonnet 200K + high**：Stage 42/43 校準後新門檻顯示 200K+ 公式估算的 Stage 不該用 Sonnet

**Stage 43/44 校準對照**：
- Stage 43（4 Mock + 5 follow-up）= 455K（×1.94）
- Stage 44（0 Mock 跑 + 0 follow-up）= 328K（×1.0）
- **Stage 45 介於兩者**（3 Mock + 動 Crash Recovery + 搭車 FF 三十七）→ 預期 **~300-400K**

---

## 後續關聯

- **Stage 46 = FF 三十五**（自動拆任務 ⭐ 戰略級）：本 Stage 完成後接續
- **Trial_v5**：Stage 45-46 全完成後執行（重跑 FF 十六需求對照 Trial_v4，驗 FF 三十二/三十三/三十四/三十五 全四項補強）
- **Trial_v5 之後評估 backlog**：FF 十一（Token 守門 Dashboard 化）/ FF 三十六（v4 架構雙支柱 spike）/ FF 三十八（跨專案能力 spike）

---

## 不在範圍

- ❌ b 主動 kill subprocess 機制（FF 三十四 不做，獨立 FF + spike 才考慮）
- ❌ Discord 暫停指令（`/pause-task` / `/resume-task`）— 後續搭車或獨立 FF
- ❌ 多選暫停 / 全部暫停 UI（Stage 33 全域緊急停止已涵蓋）
- ❌ paused 操作審計頁面（PausedAt + PausedBy 足以追溯）
- ❌ Status enum 加 paused 值（修飾狀態，新增三欄位處理）
- ❌ Auto-pause 機制（如 Token 限額觸發 auto-pause）— 留 FF 一 / FF 十一 評估
- ❌ AgentResultType 擴充（暫停不需新結果型別）
- ❌ Crash Recovery 邏輯重構（保留 Stage 31/37 設計，只加 IsPaused 篩選）
- ❌ FF 三十五 自動拆任務（Stage 46）
- ❌ FF 十一 Token Dashboard 化（Trial_v5 之後評估）

---

## 實作紀錄（Forge 結案第一段，2026-04-29）

### 範圍對齊
- 全部子項 1-6 完成（DB schema / 暫停 / Crash Recovery / Dashboard UI / FF 三十七搭車 / Mock 場景）
- ✅ 計劃書 v1.2（Aria 6 項審查清單 0 必修 + 路線 C race observation）作為實作基準
- 計劃書中的 16 檔改動清單**全數落地**，無範圍變更

### Aria 校準錨 #1 — Crash Recovery 真實位置

| 項目 | Aria 預掃 | grep 實證 | 落地 |
|---|---|---|---|
| `RecoverStuckOrchestrationsAsync` body | TaskGroupService | **`MeetingOrchestrationService.cs:427-466`**（TaskGroupService.cs:582-583 是 façade） | 改在 `MeetingOrchestrationService.cs:432-434` |
| 修改內容 | `WHERE !IsPaused` | 同上 | `.Where(g => g.ActiveOrchestration != null && !g.IsPaused)` + 加 paused-skipped count log |

> Roadmap commit `c44bb88` 已將描述校準回實際位置。

### Aria 校準錨 #2 — FF 三十七 真實搭車範圍

| Skip handler | 位置 | 預掃聲稱 | grep 實證 | 落地 |
|---|---|---|---|---|
| `dev_intervention_skip` | TaskGroupService.cs:699-706 | 需修 | ✅ **已清** Status + InterventionReason | ✅×1 不需動 |
| `qa_intervention_skip`  | TaskGroupService.cs:754-761 | 需修 | ✅ **已清** | ✅×1 不需動 |
| `sage_skip`             | TaskGroupService.cs:799-805 | 需修 | ✅ 清 InterventionReason 後委派 `MarkGroupDoneOrInterventionAsync`（自動寫 done / needs_intervention） | ✅×1 不需動 |
| `escalate_devplan_skip` | **`Discord/Routing/ButtonCallbackRouter.cs:241-258`** | 需修 | ❌ 沒清 Status / InterventionReason | ✅×1 **本 Stage 新修** |
| `escalate_devplan_skip` 在 AppealOrchestrationService.cs:625 | — | 需修 | 那是**按鈕定義**（`.WithButton(...)`）**不是 handler** | 不存在 handler，無需動 |

**Checklist 4 處 ✅×3 已先前修 + ✅×1 本 Stage 新修**。

> Roadmap commit `c44bb88` 已將「4 處」校準為「ButtonCallbackRouter.cs:241 1 處」。

### FireSteps 22 caller 統一閘門（不在 22 處逐一修）

採計劃書設計：**統一閘門加在 `FireStepsAsync` body 入口**（TaskGroupService.cs FireStepsAsync），22 個 caller 自動受保護。Aria 表揚為「超越 Roadmap 預想的加分項 #1」。

完整 caller checklist（grep 確認，全部受 FireStepsAsync 入口閘門保護）：

| 模組 | 檔案 | 行號 |
|---|---|---|
| TaskGroupService（自身 self-call） | `Orchestration/TaskGroupService.cs` | 236, 287, 319, 705, 714, 751, 760, 796 |
| ButtonCallbackRouter | `Discord/Routing/ButtonCallbackRouter.cs` | 253, 404, 651 |
| WebhookController | `Discord/.../WebhookController.cs` | 279 |
| AppealOrchestrationService | `Orchestration/Appeal/AppealOrchestrationService.cs` | 243, 337, 369, 376, 729 |
| MeetingOrchestrationService | `Orchestration/Meeting/MeetingOrchestrationService.cs` | 325, 570, 696, 732 |
| ProposalConfirmationService | `Orchestration/Proposal/ProposalConfirmationService.cs` | 119, 191 |
| QaCoordinationService | `Orchestration/Qa/QaCoordinationService.cs` | 107, 133, 174, 181, 197 |
| MockScenarioService（fire-and-forget 初始） | `Services/MockScenarioService.cs` | 107 |

**共 22 caller，全部受統一閘門保護 ✅**。

### 4 大欄位（含 Roadmap 沒明列的 PendingStepsJson）

```csharp
public bool      IsPaused         { get; set; } = false;
public DateTime? PausedAt         { get; set; }
public string?   PausedBy         { get; set; }
public string?   PendingStepsJson { get; set; }   // ← Aria 表揚加分項 #2：避免 Resume 重做 8+ 種 routing
```

Migration `Stage45TaskGroupPause` (20260429043258) — 4 欄位 metadata-only ALTER（PG 11+ 不鎖表）。

### Mock 3 場景（PausePoint 設計，Aria 表揚加分項 #3）

`MockClaudeCodeService.PausePoint = (Guid groupId, string beforeStep)?` 靜態欄位，由 `MockScenarioService` 設定，由 `FireStepsAsync` 入口偵測。模擬「外部按下暫停」時序，等同 Christ 從 Dashboard 按暫停。一次性，觸發後自清。

| Scenario | PausePoint | 預期行為 |
|---|---|---|
| `pause_at_kickoff_end` | (groupId, "Design") | Kickoff done → 即將 fire Design → 自動暫停 |
| `pause_during_dev` | (groupId, "Reviewer") | Dev done → 即將 fire Reviewer → 自動暫停（被動延遲生效） |
| `pause_resume_with_boss_interaction` | (groupId, "Reviewer") | dev_failed_intervention → 老闆按 skip → fire Reviewer → 自動暫停（議題 4 兩機制獨立） |

### paused chip 配色（hex 證據）

- `--color-status-paused: #6c757d`（muted grey，靜止／中立色）
- `--color-status-needs-intervention: #f59e0b`（amber，警示色）
- 色相距 ~190°，語意「警示 vs 靜止」清楚對比 ✅

### 路線 C — Resume race condition 觀察 log

`ResumeTaskGroupAsync` 加 `[Stage45-ResumeFire]` log：
```
logger.LogInformation("[Stage45-ResumeFire] Group {Id} resume → fire steps={Steps}", ...);
```
驗收期掃 `docker logs aiteam-bot-1 --tail 1000 | grep "Stage45-ResumeFire"`，觀察同 Group 短時間內是否 ≥ 2 行。

**驗收期觀察結果**：（待 Christ 驗收後 Aria 結案第二段補）

### 改動檔案清單（16 檔，全數落地）

| 檔案 | 改動 |
|---|---|
| `src/AiTeam.Data/Entities.cs` | TaskGroup 加 4 欄位 |
| `src/AiTeam.Data/Migrations/20260429043258_Stage45TaskGroupPause*` | 新 Migration（自動生成） |
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | IsTaskGroupPausedAsync / PauseTaskGroupAsync / ResumeTaskGroupAsync + FireStepsAsync 統一閘門 + Mock PausePoint 偵測 |
| `src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs` | Crash Recovery `&& !g.IsPaused` + paused-skipped count log |
| `src/AiTeam.Bot/Agents/MockClaudeCodeService.cs` | 加 `PausePoint` 靜態欄位 |
| `src/AiTeam.Bot/Services/MockScenarioService.cs` | 3 scenarios + workflow tuple + PausePoint 設定 |
| `src/AiTeam.Bot/Api/InternalController.cs` | POST `/internal/taskgroup/{id}/pause` + `/resume` + PauseTaskGroupRequest record |
| `src/AiTeam.Bot/Discord/Routing/ButtonCallbackRouter.cs` | escalate_devplan_skip 加 3 行清 status（FF 三十七 ✅） |
| `src/AiTeam.Shared/Dtos/TaskGroupDto.cs` | 加 IsPaused / PausedAt / PausedBy |
| `src/AiTeam.Dashboard/Services/DashboardTaskService.cs` | 3 個 Select mapping + paused 虛擬篩選 |
| `src/AiTeam.Dashboard/Services/DashboardBotService.cs` | PauseTaskGroupAsync / ResumeTaskGroupAsync client |
| `src/AiTeam.Dashboard/wwwroot/css/app.css` | `--color-status-paused: #6c757d` + `.status-paused` |
| `src/AiTeam.Dashboard/Components/Shared/StatusBadge.razor` | switch 加 `"paused" => "暫停中"` |
| `src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor` | 篩選下拉加 paused |
| `src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor` + `.razor.cs` | 暫停 / 恢復 按鈕 + alert + handlers + ApplyGroupContent 補 paused 三欄 |
| `src/AiTeam.Dashboard/Components/Pages/Home/MockScenarioCard.razor` | 3 新 scenario 選項 |
| `src/Directory.Build.props` | v3.31.0 → v3.32.0 |

### 編譯結果

`dotnet build AiTeam.slnx` → **0 Errors / 75 Warnings（全部既有 NU1902 套件警告 + Playwright MSTEST 建議 + 1 個既有 MUD0002 PipelineView Color attribute 警告，無新增）**

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-29 | 計劃書建立（Aria）— FF 三十四 TaskGroup 流程暫停機制（被動阻擋下階段，採方案 Ba + B 互動 + B Appeal flow）+ 搭車 FF 三十七（4 處 skip status 殘留修） |
| v2.0 | 2026-04-29 | 第一段結案（Forge）— 全 6 子項落地 / 16 檔改動 / 2 條 Aria 校準錨 / 22 caller checklist / Mock 3 場景 / paused chip 配色 / 路線 C race log / 待驗收 |

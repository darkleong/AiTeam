# Stage 34：MeetingService 拆解（FF 二十-C）

> 對應 Future Feature：二十（大檔案拆解技術債合集）子項 C
> 對應版本：v3.21.0
> 建立日期：2026-04-22
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

**主題**：把 `MeetingService.cs`（1415 行）拆成三個職責清楚的 service，作為 AiTeam 大檔案拆解技術債的**首次正式實踐**——為後續 FF 二十-D（PmAgentService）、FF 二十-B（CommandHandler）、FF 二十-A（TaskGroupService）建立 SOP。

**為什麼選 MeetingService 先做**：
- Kickoff 會議 vs Design 會議幾乎沒有共用狀態（除了 `CloseAllSessionsAsync` 和 `RunAgentTurnAsync` 兩個 helper），**拆解界線最乾淨、風險最低**
- 首次做 service 拆解，需要一次乾淨的經驗累積「拆解 SOP」寫進 FF 二十 作為後續子項的參考

**Mock 階段動是對的時機**：趁真實 API 測試前做結構清理，不影響 runtime 行為，驗收用 Mock Mode 走流程即可。

---

## 現況分析

### MeetingService 的 Public API（5 個）

| Method | 行數估算 | 歸屬 |
|---|---|---|
| `RunKickoffMeetingAsync` | 大，Kickoff 主流程 | → `KickoffMeetingService` |
| `ModifyTaskPlanAsync` | 中，Kickoff 後改 TaskPlan | → `KickoffMeetingService` |
| `RunDesignMeetingAsync` | 大，Design 主流程 | → `DesignMeetingService` |
| `ModifyDesignPlanAsync` | 中，Design 後改 DesignPlan | → `DesignMeetingService` |
| `CloseAllSessionsAsync` | 小，關所有 session | → `MeetingCommons` |

### MeetingService 的 Private Helper

- `RunDesignAdjustmentAsync` → Design 內部用，搬 `DesignMeetingService` 為 private
- `GenerateDesignPlanAsync` → Design 內部用，搬 `DesignMeetingService` 為 private
- `RunAgentTurnAsync` → Kickoff + Design 都用，搬 `MeetingCommons` 為 public / internal

### Call Sites（`TaskGroupService` 內共 8 處）

| Line | 用途 | 拆解後指向 |
|---|---|---|
| 1514 | `RunKickoffMeetingAsync` | `KickoffMeetingService` |
| 1707 / 1714 / 1837 / 2079 | `CloseAllSessionsAsync` × 4 | `MeetingCommons` |
| 1733 | `ModifyTaskPlanAsync` | `KickoffMeetingService` |
| 1912 | `RunDesignMeetingAsync` | `DesignMeetingService` |
| 2107 | `ModifyDesignPlanAsync` | `DesignMeetingService` |

WorkflowEngine 也有引用，但僅 DI 相關（Program.cs）。

---

## 拆解設計

### 三個新 Service

#### 1. `KickoffMeetingService`（~500 行）

**位置**：`src/AiTeam.Bot/Orchestration/KickoffMeetingService.cs`

**API**：
```csharp
Task<MeetingResult> RunKickoffMeetingAsync(
    TaskGroup group, string proposalContent, string owner, string repo, CancellationToken ct);

Task<ModifyResult> ModifyTaskPlanAsync(
    TaskGroup group, string modifyInstruction, CancellationToken ct);
```

**依賴**：`MeetingCommons`（拿 `RunAgentTurnAsync`、session 管理）+ 原本 MeetingService 的 DI（IClaudeCodeService / TaskRepository 等）

#### 2. `DesignMeetingService`（~700 行）

**位置**：`src/AiTeam.Bot/Orchestration/DesignMeetingService.cs`

**API**：
```csharp
Task<DesignMeetingResult> RunDesignMeetingAsync(
    TaskGroup group, string owner, string repo, CancellationToken ct);

Task<ModifyResult> ModifyDesignPlanAsync(
    TaskGroup group, string modifyInstruction, CancellationToken ct);
```

**內部 private**：`RunDesignAdjustmentAsync` + `GenerateDesignPlanAsync`

**依賴**：`MeetingCommons` + 原本 MeetingService 的 DI

#### 3. `MeetingCommons`（~200 行）

**位置**：`src/AiTeam.Bot/Orchestration/MeetingCommons.cs`

**API**：
```csharp
Task CloseAllSessionsAsync(Guid groupId);

// 供 Kickoff / Design 共用的單輪 Agent 對話封裝
internal Task<string> RunAgentTurnAsync(...);

// 跨 session 管理的 state（若 MeetingService 內有 session dictionary 之類）
```

**依賴**：`IClaudeCodeService` 等基礎 DI，無其他 meeting service 依賴

### Record / Types 保留

`MeetingResult` / `ModifyResult` / `DesignMeetingResult` 等 record（目前在 MeetingService.cs 尾端 line 1340+）**搬到獨立檔案** `src/AiTeam.Bot/Orchestration/MeetingResults.cs`，避免新 service 內有 record 定義污染。

---

## Migration 策略：直接切換（不做 thin wrapper）

**決策理由**：
- Caller 不多（TaskGroupService 8 處 + Program.cs DI）
- 保留 thin wrapper 增加混淆、維護成本高於收益
- git history 可追溯

**步驟**：
1. 建 `MeetingResults.cs` 搬 record
2. 建 `MeetingCommons` 搬共用邏輯
3. 建 `KickoffMeetingService` 搬 Kickoff 方法
4. 建 `DesignMeetingService` 搬 Design 方法
5. 更新 TaskGroupService 8 處 caller：建構子新增 3 個依賴、8 處呼叫改指向對應新 service
6. Program.cs DI：移除 `AddSingleton<MeetingService>`，新增三個 service 註冊
7. **刪除 `MeetingService.cs`**
8. `dotnet build` 確認 0 error

---

## 實作順序建議

1. **先建 `MeetingCommons`** — 共用 helper 先穩定，另外兩個 service 才能依賴
2. **再建 `KickoffMeetingService` + `DesignMeetingService`** — 可平行，但順序做省切換 context
3. **改 TaskGroupService** — 8 處 call site 集中改一波
4. **DI 註冊 + 刪舊檔**
5. **Build + Mock 驗收**

---

## 驗收情境

Mock Mode 開啟，跑以下流程各一次：

1. **`/mock new_feature_with_proposal`** → Proposal 確認 → Kickoff 會議跑完 → Christ 按「修改計劃書」→ `ModifyTaskPlanAsync` 應正常 → 再「繼續開發」→ Design 會議跑完 → `ModifyDesignPlanAsync` 流程也正常 → 進入開發
2. **`/mock new_feature`**（跳過 proposal）→ Kickoff 直接進入 → 流程跑完
3. **`/mock fail_review`** → 完整走到 Review Appeal，確認 Appeal 中的 PmAgentService 呼叫 `IClaudeCodeService.RunMeetingSessionAsync`（注意：這跟本 Stage 拆的 `MeetingService` 是**不同東西**，不受影響）仍正常
4. **中斷一次會議**（`CloseAllSessionsAsync` 路徑）→ 確認 cleanup 正常

**負面驗證**：
- `dotnet build` 0 error 0 warning（新增的）
- `MeetingService.cs` 確實被刪除（不要殘留）
- Program.cs 正確註冊三個新 service

---

## 版本

`v3.20.0 → v3.21.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Sonnet 200K + high**

### Context 估算（按 memory 新預估法）

| 來源 | 估算 |
|---|---|
| MeetingService Read 一次 | ~28K（1415 行）|
| TaskGroupService 改 8 處 caller | 需 Read 大檔（~52K）—— **但只讀動的段落**，不要整檔 Read，估 ~20K |
| Program.cs DI | ~3K |
| 新寫 3 個 service（寫新檔不 Read 舊） | 低 |
| CLAUDE.md / conventions 開場 | ~15K |
| Grep / Edit / Build 緩衝 | ~20K |

**總計 ~85K**，Sonnet 200K + high 充裕（預留 40% buffer）。

### 為何 high effort

- 拆解要保留現有行為，不能降 medium（首次做 service 拆解，沒有既有 SOP 可抄）
- DI 依賴關係 + callers 改動需要跨檔推理
- thin wrapper 的 migration 策略抉擇需要判斷

### 為什麼不是 Opus 200K

本 Stage 是「機械化搬遷」為主，不是「架構深度思考」。Sonnet 200K + high 的推理品質已足夠，且 context 充裕。

---

## 設計約定

- **不保留 thin wrapper**（Migration 策略直接切換）
- **Record 搬獨立檔**（`MeetingResults.cs`），避免新 service 內有 type 定義污染
- **namespace 統一**：三個新 service 都在 `AiTeam.Bot.Orchestration`
- **DI 生命週期**：三個新 service 都是 Singleton（對齊舊 MeetingService）
- **循環依賴檢查**：`KickoffMeetingService` / `DesignMeetingService` → `MeetingCommons`（單向），不可反向

---

## 預期產出（給 FF 二十 累積「拆解 SOP」）

Stage 34 完成後，Roadmap 實作紀錄要寫**清楚**這些決策，供後續 FF 二十-D / B / A 子項參考：

1. **Record 該不該共用 / 獨立檔**（這次選獨立檔 `MeetingResults.cs`）
2. **thin wrapper 該不該做**（這次選不做，直接切換）
3. **Commons service 的範圍如何界定**（只放「真共用」的，不放「只為避免 new service 互相依賴而抽」的）
4. **DI 註冊該如何安排**（三個 Singleton 的順序）
5. **跨 service 的 session state 管理策略**（若 MeetingService 有維護 session dictionary，Commons 要集中管理）

這些經驗會被 FF 二十 更新為「通用拆解 SOP」，讓未來 D/B/A 子項 briefing 更省事。

---

## 結案檢查清單（兩段式分工）

- **實作 Session 做**：Stage_34_Roadmap 補「實作紀錄」章節 + 狀態 ✅ + 文件版本 v2.0 + 版本歷史 + commit；**特別強調記錄上方「預期產出」的五項決策**
- **Aria 做**：Master Plan header + 索引 ✅ + changelog；Future_Feature 更新 FF 二十 子項 C 狀態為已完成、把 Stage 34 累積的拆解 SOP 寫進 FF 二十 的「共通拆解策略」小節；掃 git log 確認驗收期間 commits 補進 Roadmap

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-22 | 初版規劃書，MeetingService 拆三塊（KickoffMeetingService / DesignMeetingService / MeetingCommons）；Migration 策略為直接切換不做 thin wrapper；目的是累積拆解 SOP 供 FF 二十 後續子項 |

# Stage 36：TaskGroupService + CommandHandler 拆解（FF 二十 A+B 合併）

> 對應 Future Feature：二十（大檔案拆解技術債合集）子項 A + B 合併
> 對應版本：v3.23.0
> 建立日期：2026-04-22
> 完成日期：2026-04-22
> 狀態：✅ 已完成（實作 + build 綠，待 Christ 驗收 Mock Mode 8 情境）
> 文件版本：v2.0

---

## 概述

**主題**：FF 二十最終兩個子項合併做——`TaskGroupService.cs`（2623 行）+ `CommandHandler.cs`（2172 行）合計 4795 行一次拆完。合併理由：兩者透過 `_pendingConfirmations` dictionary + Register*/Handle* 模式互相呼叫，**強耦合**，分開拆會重複設計介面兩次。

**為什麼兩個合併**：FF 二十 搭車優先順序建議原文「**子項 B + 子項 A 合併一次做最乾淨**」——Stage 34/35 已驗證 SOP 可靠，現在有信心做最大一次。

**這是 FF 二十最後一次拆解**，完成後合集全部 ✅。

---

## 現況分析（輕量探索）

### TaskGroupService 9 個 public method

| Method | 歸屬類別 |
|---|---|
| `CreateGroupAsync` | CRUD（留原 TaskGroupService 瘦身版）|
| `HandleAgentCompletedAsync` | **主 dispatcher**（留原檔但委派到新 service）|
| `FireMockProposalAndContinueAsync` | Mock 流程（留原檔或歸 Proposal）|
| `FireStepsAsync` | 步驟派工（留原檔或歸 Dispatcher）|
| `CancelAsync` | 取消（留原檔）|
| `RecoverStuckMeetingsAsync`（Stage 31）| → `MeetingOrchestrationService` |
| `HandleKickoffConfirmedAsync` | → `MeetingOrchestrationService` |
| `HandleDesignConfirmedAsync` | → `MeetingOrchestrationService` |
| `ProcessBossResponseAsync`（Stage 28a）| → `ProposalConfirmationService`（與 CommandHandler 路徑共用）|

外加 `HandleAgentCompletedAsync` 內大量會議 / Appeal / QA 邏輯要進一步拆進對應子 service。

### CommandHandler 6 個 public method + 大量 private

- `RegisterCommandsAsync` — 註冊 Discord slash commands（留原檔或 → `SlashCommandRouter`）
- `RegisterDevPlanEscalation` / `RegisterKickoffConfirmation` / `RegisterDesignConfirmation` / `RegisterProposalConfirmation` — 登記 pending confirmation
- `HandleCeoResponseFromDashboardAsync`（Stage 29-5）— Dashboard 路徑
- 大量 private：slash command handlers、button callbacks、`_pendingConfirmations` dictionary

---

## 拆解設計

### TaskGroupService 子 service（4 個）

| 新 Service | 職責 | 預估 |
|---|---|---|
| `MeetingOrchestrationService` | Kickoff + Design + Crash Recovery + Confirm 路由 | ~700 |
| `AppealOrchestrationService` | Review Appeal 雙迴圈 + Dev_plan Appeal + Petra 仲裁 | ~500 |
| `QaCoordinationService` | QA Fix 迴圈 + Petra 四路由 | ~300 |
| `ProposalConfirmationService` | Proposal 按鈕路由（Discord + Dashboard 雙通道，與 CommandHandler 的 `ProposalConfirmationHandler` 合作）| ~300 |
| `TaskGroupService`（瘦身）| CRUD + 主 dispatcher（入口）+ `FireStepsAsync` + `CancelAsync` | ~400 |

### CommandHandler 子模組（3+1 個）

| 新 Module | 職責 | 預估 |
|---|---|---|
| `SlashCommandRouter` | RegisterCommandsAsync + 各 slash command handler dispatcher | ~400 |
| `ButtonCallbackRouter` | 按鈕回調 dispatcher + 呼叫 TaskGroupService 對應 Handle* | ~400 |
| `PendingConfirmationStore`（Singleton）| 把 `_pendingConfirmations` dictionary 抽成獨立 store 供多方 Register / Lookup | ~100 |
| `CommandHandler`（瘦身）| Discord.Net event binding + Register*/Handle* 薄 wrapper 委派到 Router | ~400 |

### 檔案夾組織（套用 SOP 6）

**Orchestration/** 根目錄已 12 檔，拆後加 4 個會 16 個，**建子資料夾**：

```
Orchestration/
├── TaskGroupService.cs（瘦身）
├── WorkflowEngine.cs
├── WorkflowSettingsResolver.cs
├── AgentQueueService.cs
├── AgentQueueProcessor.cs
├── InteractionProcessor.cs
├── Meeting/          ← 子資料夾（Kickoff / Design / Recovery）
│   ├── KickoffMeetingService.cs（從 Stage 34 搬入）
│   ├── DesignMeetingService.cs（同上）
│   ├── MeetingCommons.cs（同上）
│   ├── MeetingResults.cs（同上）
│   └── MeetingOrchestrationService.cs（新）
├── Appeal/           ← 子資料夾
│   └── AppealOrchestrationService.cs（新）
├── Qa/               ← 子資料夾
│   └── QaCoordinationService.cs（新）
└── Proposal/         ← 子資料夾
    └── ProposalConfirmationService.cs（新）
```

**Discord/** 類似處理：

```
Discord/
├── CommandHandler.cs（瘦身）
├── ConversationContextStore.cs
├── Routing/          ← 子資料夾
│   ├── SlashCommandRouter.cs
│   ├── ButtonCallbackRouter.cs
│   └── PendingConfirmationStore.cs
```

**namespace 更新**：
- `AiTeam.Bot.Orchestration.Meeting` / `.Appeal` / `.Qa` / `.Proposal`
- `AiTeam.Bot.Discord.Routing`

**搬 Stage 34 既有檔案也是本 Stage 的搭車**：Meeting 子資料夾同時收納 Stage 34 四個檔，namespace 升級。

---

## 核心設計：`PendingConfirmationStore` 解耦

**現況**：`_pendingConfirmations` dictionary 在 CommandHandler 內 private，但 TaskGroupService（Stage 28b 雙通道路徑）也要 Register—— Stage 28b 為此把 `RegisterProposalConfirmation` 改 public。

**拆解後**：

```csharp
// Singleton Store
public class PendingConfirmationStore
{
    public void Register(ulong messageId, PendingConfirmation pending);
    public PendingConfirmation? Lookup(ulong messageId);
    public void Remove(ulong messageId);
}
```

**好處**：
- TaskGroupService 的 `ProposalConfirmationService` 和 CommandHandler 的 `ButtonCallbackRouter` 都注入 Store，不用互相 reference
- 消除 Stage 28b 的「單向硬耦合」（public method 給外部用）
- CommandHandler 本身不再需要維護字典狀態，變更純

---

## SOP 套用對照（Stage 34/35 六項 + Stage 35 SOP 6 新結論）

| SOP | 本次實踐 |
|---|---|
| 1. Record 組織 | `PendingConfirmation` 等 public type 搬獨立檔（`Discord/Routing/RoutingResults.cs` 或類似）|
| 2. Migration 策略 | Caller 分散但多數是內部呼叫，直接切換；為了安全，**可考慮 TaskGroupService 入口 method 先留 thin wrapper**（因為有 external caller 如 WorkflowEngine），confirm 後再拿掉 |
| 3. Commons 範圍 | 不用 Commons（不同子 service 各自職責獨立）|
| 4. DI 順序 | Store 先 → Router/Confirmation/Meeting/Appeal/Qa 後 |
| 5. Session state | `_pendingConfirmations` 抽 `PendingConfirmationStore` Singleton |
| 6. **子資料夾** | Orchestration/Meeting + Appeal + Qa + Proposal；Discord/Routing；對齊 Stage 35「Agent vs Orchestration 歸屬原則」——Orchestration 是協調流程，子資料夾依「主題子領域」分 |

---

## Migration 步驟建議

1. **建 `Orchestration/Meeting/` 子資料夾，搬 Stage 34 四檔 + 更新 namespace**（第一步先穩定，確認拆解機制可行）
2. **建 `PendingConfirmationStore`**（Singleton Store）
3. **建 `Discord/Routing/` 子資料夾 + ButtonCallbackRouter + SlashCommandRouter**
4. **CommandHandler 瘦身**：Register*/Handle* 改薄 wrapper 委派 Router + Store
5. **建 `Orchestration/Meeting/MeetingOrchestrationService`** + 搬 Kickoff/Design/Recovery 相關邏輯
6. **建 `Orchestration/Appeal/AppealOrchestrationService`**
7. **建 `Orchestration/Qa/QaCoordinationService`**
8. **建 `Orchestration/Proposal/ProposalConfirmationService`**
9. **TaskGroupService 瘦身**：主 dispatcher + CRUD 留，其他 delegate 到新 service
10. **Program.cs DI**：調整註冊順序（Store 先、Commons 其次、各 Orchestration 再後）
11. **`dotnet build` + Mock Mode 全情境驗收**
12. **版本號 v3.22.0 → v3.23.0**

---

## 驗收情境

Mock Mode 全套（8 個情境，比前幾次都多）：

1. `/mock new_feature_with_proposal` — Proposal 確認 → Kickoff → Design → Dev → Review → QA → Doc
2. `/mock new_feature` — 跳 Proposal，走完整流程
3. `/mock bug_fix` — 精簡路徑
4. `/mock fail_review` — Review Appeal 三角互動
5. `/mock fail_dev_plan` — Dev_plan Appeal
6. `/mock fail_qa` — QA 路由
7. Discord `/pause`、`/resume`、`/stop-all`、`/resume-all` — 確認 CommandHandler 瘦身後 slash command 回歸
8. Dashboard MockScenarioCard、AgentStatusCard、QuickCommandCard 等 — 確認 HandleCeoResponseFromDashboardAsync 路徑不受影響

### 負面驗證

- `dotnet build AiTeam.slnx` 0 error（namespace 改動量最大一次）
- `TaskGroupService.cs` 瘦身至 ~400 行、`CommandHandler.cs` 瘦身至 ~400 行
- 子資料夾結構齊全
- Program.cs DI 順序正確

---

## 版本

`v3.22.0 → v3.23.0`（minor bump）

---

## Model / Effort 建議

**必選：Opus 1M + high**

### Context 預估（×1.6 公式）

| 項目 | tokens |
|---|---|
| TaskGroupService Read 整檔 | 52K |
| CommandHandler Read 整檔 | 44K |
| 新寫 4+3 個 service + Store（~2500 行 Write）| 50K |
| 搬 Stage 34 四檔到 Meeting/ 子資料夾 + namespace 更新 | 10K |
| 驗收 buffer（8 個情境）| +40K |
| 開場 CLAUDE.md / conventions | +15K |
| Grep / Build / Edit 緩衝 | +30K |
| **粗估** | **~241K** |
| **實際預期（× 1.6）** | **~386K** |

**結論**：遠超 Sonnet 200K（即使拆兩 Session 也緊），**必須 Opus 1M**。386K / 1M = 39%，充裕。

### 為何 high effort

- 首次合併兩個大檔拆解、耦合點需仔細設計
- `PendingConfirmationStore` 抽離要確保 Register / Lookup / Remove 的 thread-safety 跟原本一致
- `HandleAgentCompletedAsync` 的內部邏輯很大（可能 500+ 行），如何 delegate 到子 service 是關鍵設計

---

## 設計約定

- **子資料夾 namespace 更新**：用 `using` 補齊避免 break（caller 原 `using AiTeam.Bot.Orchestration` 仍有效，新 namespace 用 `using AiTeam.Bot.Orchestration.Meeting;` 等補加）
- **Stage 34 Meeting 四檔搬到子資料夾**屬搭車工作，算進本 Stage 範圍
- **不保留 thin wrapper** — 除了 TaskGroupService 入口 method（若外部 caller 多）可例外留短暫 wrapper，其他直接切換
- **`_pendingConfirmations` thread-safety**：原本是 `ConcurrentDictionary`（應該是，建議驗證），抽 Store 後維持同樣類型

---

## 結案檢查清單（兩段式分工）

- **實作 Session 做**：Stage_36_Roadmap v2.0 + 實作紀錄 + 版本歷史 + commit；記錄合併拆解兩大檔的經驗供未來 refactor 參考
- **Aria 做**：Master Plan 索引 ✅ + changelog；Future_Feature 更新 FF 二十 **整項完成**（ABCD 全部 ✅）、移至已完成摘要；掃 git log 補 follow-up commits
- **FF 二十 完全結束** — 整項從主清單移除

---

## 實作紀錄（v2.0 追加）

### 完成成果

**檔案行數變化**：

| 檔案 | 拆解前 | 拆解後 | 備註 |
|---|---|---|---|
| `TaskGroupService.cs` | 2623 | **716** | 瘦身 -73%（CRUD + dispatcher + FireSteps/Cancel + Dashboard 分派 + BuildTaskDescription/Notify 共用）|
| `CommandHandler.cs` | 2172 | **556** | 瘦身 -74%（事件 wiring + 訊息路由 + Dashboard 入口）|
| **合計** | **4795** | **1272** | **瘦身 -73.5%** |

**新檔（10 個）**：

```
Orchestration/
├── Meeting/MeetingOrchestrationService.cs     775
├── Appeal/AppealOrchestrationService.cs       667
├── Qa/QaCoordinationService.cs                164
└── Proposal/ProposalConfirmationService.cs    319

Discord/Routing/
├── ButtonCallbackRouter.cs                   1091（含共用 UI flow helpers：ShowProposalAsync、ShowDirectAgentConfirmAsync、Build*Embed、Build*Buttons 等）
├── SlashCommandRouter.cs                      410
├── PendingConfirmationStore.cs                 82
└── RoutingTypes.cs                             21
```

**Stage 34 搬家**：Meeting 4 檔（KickoffMeetingService / DesignMeetingService / MeetingCommons / MeetingResults）由 `Orchestration/` 根目錄搬至 `Orchestration/Meeting/`，namespace 從 `AiTeam.Bot.Orchestration` 升至 `AiTeam.Bot.Orchestration.Meeting`。TaskGroupService、Program.cs 兩處 `using` 補齊。

### 設計決策紀錄

**Q1 決定：`PendingConfirmationStore` 升級為 `ConcurrentDictionary`**
- 探索發現：原 `_pendingConfirmations` 並非 ConcurrentDictionary（Roadmap v1.0 誤判）
- Store 抽出後，Register 端（OrchestrationService 散於多 thread）與 Lookup/Remove 端（ButtonCallbackRouter）分散，原「都在 Discord event loop 單 thread」假設不成立
- Store 內 6 個字典全用 ConcurrentDictionary + `TryRemove` 原子化

**Q2 決定：Dev_plan Petra 審核鏈歸 `AppealOrchestrationService`**
- `RunPetraDevPlanReviewAsync`、`FinalizePetraDevPlanTaskAsync`、`RunDevPlanAppealLoopAsync`、`NotifyBossDevPlanEscalationAsync`、`HandleDevPlanEscalationAsync` 五者邏輯連續（Petra 初審 disagree → 觸發 Appeal 迴圈 → 升級老闆）
- 按 Christ 建議歸 Appeal 側（Dev_plan Appeal 本來就在 Appeal），Proposal service 瘦至 319 行只負責 Dashboard 路徑

**Q3 決定：共用 UI flow helpers 放 `ButtonCallbackRouter` 而非另立 helper class**
- `ShowProposalAsync` / `ShowDirectAgentConfirmAsync` / `BuildCeoDecisionEmbed` / `BuildConfirmButtons` 等跨 3 entry points（slash / button / 自然語言 / Dashboard）共用
- 以 `internal` 標記暴露給 SlashCommandRouter 與 CommandHandler 使用，避免過度抽象為「第 4 個 helper service」

**Q4 決定：循環依賴用 IServiceProvider 解決**
- 4 個子 Orchestration service 需要回呼 TaskGroupService（FireSteps/NotifyBoss）
- TaskGroupService 也需要注入 4 子 service
- 子 service 透過 `serviceProvider.GetRequiredService<TaskGroupService>()` 在方法內取得，避免建構子循環

### 踩坑紀錄

| 踩坑 | 解法 |
|---|---|
| 初建 `QaCoordinationService.cs` 忘記 `using AiTeam.Bot.Agents`，AgentExecutionResult 解析失敗 | 補 using |
| `_pendingConfirmations` 型別誤判（Roadmap 寫 Concurrent 實為普通 Dictionary） | 探索 Agent 實測確認 + 升級 Store |
| Meeting/ 子資料夾搬家後，TaskGroupService + Program.cs 兩處失去 `KickoffMeetingService` 等型別 | 兩處補 `using AiTeam.Bot.Orchestration.Meeting` |
| MockScenarioService 原呼叫 `CommandHandler.BuildProposalEmbed` / `BuildProposalConfirmButtons`（瘦身後 CommandHandler 不再持有） | 改呼 `ButtonCallbackRouter.BuildProposalEmbed` / `BuildProposalConfirmButtons`（internal 暴露） |

### FF 二十 整項完成

Stage 32（C）+ 33（D）+ 34（E，Meeting 拆解）+ **36（A+B 合併）** = FF 二十 **ABCDE 全部 ✅**，AiTeam 大檔案技術債清零。

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-22 | 初版規劃書，FF 二十 A+B 合併（TaskGroupService 2623 + CommandHandler 2172 = 4795 行）拆 4+3+1 個 service/module + Store，含 Orchestration/Discord 子資料夾；`PendingConfirmationStore` 為核心解耦；Stage 34 Meeting 四檔搬子資料夾屬搭車工作；Opus 1M + high（粗估 241K × 1.6 = 386K，Sonnet 不可能）|
| v2.0 | 2026-04-22 | **實作完成**。TaskGroupService 2623→716（-73%）、CommandHandler 2172→556（-74%）。新增 10 檔（4 Orchestration service + 4 Routing + Store + RoutingTypes）。Dev_plan Petra 審核鏈改歸 AppealOrchestrationService（與 Appeal 迴圈連續）。`PendingConfirmationStore` 升 ConcurrentDictionary × 6 組（探索發現原為普通 Dictionary）。共用 UI flow helpers（ShowProposalAsync 等）集中 ButtonCallbackRouter 以 internal 暴露。循環依賴用 IServiceProvider 解。FF 二十 整項完成。|

# 02 Architecture Wire — 4 層 Hierarchy 落具體 service / DI 結構

> Charter spike deliverable 2/4。對齊 [Future_Feature.md:180 4 層 Hierarchy 圖](../../planning/Future_Feature.md)。每層落具體檔案 / class / DI 結構 + Tool Set 細節 + per-task session DB schema 候選 + Capability schema 候選。
>
> ⚠️ **Charter spike 階段紀律**：所有 v5 新建 class / interface 名只寫**候選 signature**，**Stage 63 PoC 落 MS Agent Framework nuget 1.3.0 真實 API grep 驗證**（對齊 workflow_aria.md 第 7 條延伸範圍段紀律 — 不憑印象寫 nuget class 名）。

---

## 4 層 Hierarchy 全景

```
┌──────────────────────────────────────────────┐
│ Layer 1: Christ（老闆 — 0 改動）              │
└─────────┬──────────────────┬────────────────┘
          ↓ Discord 對話    ↓ Dashboard 操作
┌──────────────────────┐  ┌──────────────────┐
│ Layer 2: Victoria     │  │ Operation Center │
│ (Discord 秘書/Router) │  │ (BossInteraction)│
└─────────┬────────────┘  └────────┬─────────┘
          ↓ RouteToPetra              ↑ Petra escalate
          ↓ (新工具)                   ↑
┌─────────────────────────────────────┴────┐
│ Layer 3: Petra Orchestrator（新建）       │
│ - PetraOrchestratorService               │
│ - per-task session 持久化（多 row table） │
│ - Tool Set + Capability-based            │
│ - Magentic Orchestration class wire      │
└──┬──────┬──────┬──────┬──────┬──────────┘
   ↓ tool ↓ tool ↓ tool ↓ tool ↓ tool
┌────┐ ┌────┐ ┌─────┐ ┌────┐ ┌─────────┐
│Cody│ │Vera│ │Quinn│ │Sage│ │Rosa/Demi│  ← Layer 4: Workers
└────┘ └────┘ └─────┘ └────┘ └─────────┘  （8 Worker + Designer + Release）
```

---

## Layer 1：Christ 入口（0 改動）

| 範圍 | 檔案 | LoC | v5 處理 |
|---|---|---|---|
| Discord 雙通道 | `src/AiTeam.Bot/Discord/CommandHandler.cs` + `Discord/Routing/ButtonCallbackRouter.cs` | 1202 + caller's | **0 改動** — Stage 28a/b 樂觀鎖 + Stage 27 DB-as-Queue 全保留 |
| Dashboard Operation Center | `src/AiTeam.Dashboard/**` | 大量 | **0 改動** — BossInteraction 樂觀鎖既有保留 |
| BossInteraction DB | `src/AiTeam.Data/**` | 大量 | **0 改動** — entities / migrations 全保留 |

---

## Layer 2：Victoria（Discord 秘書 / Router）

| 範圍 | 檔案 | LoC | v5 處理 |
|---|---|---|---|
| Service | `src/AiTeam.Bot/Agents/CeoAgentService.cs` | 544 | **保留 + prompt 重寫**（Tool Set 改純 Petra 派工 + 移除 codebase scan 段）|
| Prompt | `src/AiTeam.Bot/Resources/CLAUDE_Victoria.md` | 93 | **partial 重寫**（Discord 秘書定位 + 移除「業務邏輯 / codebase scan」）|

**新工具 Tool Set 候選 signature**（Charter spike 階段只寫 signature — Stage 63 PoC 落實 attribute / interface 名 grep 驗證）：

```
RouteToPetra(taskDescription: string, taskGroupId: long) → PetraOrchestratorService.StartAsync()
NotifyChrist(message: string, urgency: enum) → Discord push
QueryTaskStatus(taskGroupId: long) → status snapshot
```

**DI 註冊**：CeoAgentService 既有 scoped registration 保留（v5 PoC 階段 0 DI 結構改動 — 紀律對齊 Charter 文件 only / Stage 63 PoC 才實際 wire）。

---

## Layer 3：Petra Orchestrator（新建 — Charter spike 候選 signature）

> ⚠️ Charter spike 階段：本 Layer 全 candidate signature — Stage 63 PoC 落實 grep 驗證 nuget 1.3.0 真實 class 名。

### Service 候選

| 範圍 | 檔案 候選 | LoC 預估 | 說明 |
|---|---|---|---|
| Orchestrator | `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` | 300-500（PoC 初版）| MS Agent Framework Magentic Orchestration class wire — 真實 class 名 Stage 63 PoC grep 驗證（候選想像為 `MagenticOrchestrator<TState>` 之類 — Charter spike 階段不寫死）|
| Session Store | `src/AiTeam.Bot/Orchestration/Petra/PetraSessionStore.cs` | 100-200 | 包 EF Core 對 `petra_sessions` + `petra_session_messages` 兩表的 read/write |
| Tool Set 介面 | `src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs` | 50-100 | hybrid pattern：interface + attribute（Forge spike 自決 Aria 通過）|

### per-task session DB schema 候選（Forge spike 自決 Aria 通過 — 多 row table）

對齊既有 EF Core PostgreSQL 既有 pattern + Stage 27 DB-as-Queue 多 row pattern reference。

**`petra_sessions` 表**：

| 欄位 | 型別 | 說明 |
|---|---|---|
| `Id` | bigint PK | session 唯一鍵 |
| `TaskGroupId` | bigint FK → `task_groups.Id` | 對應任務組 |
| `Status` | text enum | `running` / `escalated` / `done` |
| `CreatedAt` | timestamptz | 創立時刻 |
| `UpdatedAt` | timestamptz | 最後更新時刻 |

**`petra_session_messages` 表**：

| 欄位 | 型別 | 說明 |
|---|---|---|
| `Id` | bigint PK | message 唯一鍵 |
| `SessionId` | bigint FK → `petra_sessions.Id` | 對應 session |
| `Role` | text enum | `system` / `user` / `tool` / `assistant` |
| `Content` | text | message 內容 |
| `ToolCallId` | text nullable | tool 呼叫 id（assistant 帶 tool_calls 時填 / tool result 回填）|
| `CreatedAt` | timestamptz index | 時序排序 |

**Index**：
- `petra_sessions(TaskGroupId)` index — 按 TaskGroup 查 session
- `petra_session_messages(SessionId, CreatedAt)` composite index — 按 session 時序拉 history

> ⚠️ **Charter spike 階段紀律**：本 schema 只寫 candidate — **Stage 63 PoC 寫 EF Migration `Stage63PetraSessionTables`** 才實際落實（紀律對齊 Charter 文件 only / 不動 EF Migration / `dotnet ef migrations add` 命令在 Stage 63 PoC 才執行）。

**對齊 SoT 紀律**：
- 對齊既有 `chat_messages` schema pattern（Stage 63 PoC 寫 Migration 時 grep 驗證命名 PascalCase quote 紀律）
- 對齊既有 `agent_queue` 多 row 設計（Stage 27 DB-as-Queue reference）
- 對齊 EF Core PostgreSQL 既有 entity pattern（src/AiTeam.Data/Entities/）

### Tool Set + Capability-based 標籤候選（Forge spike 自決 Aria 通過 — attribute + interface hybrid）

**Pattern A（compile-time）**：C# attribute on service class
```
[AgentCapability("code_implementation")]
public class DevAgentService : IAgentExecutor { ... }
```

**Pattern B（runtime）**：interface 提供 runtime 暴露 capability
```
public interface IAgentTool {
    string Name { get; }
    string[] Capabilities { get; }
    Task<string> ExecuteAsync(string input, CancellationToken ct);
}
```

**hybrid 用法**：attribute 紀錄 capability + interface 提供 runtime dispatch（Petra orchestrator 透過 DI scan IAgentTool implementations + Reflection 取 attribute）。

**9 Worker capability mapping 候選**：

| 角色（lore）| Service（code）| Capability tag |
|---|---|---|
| Cody | `DevAgentService.cs` | `code_implementation` |
| Vera | `ReviewerAgentService.cs` | `code_review` |
| Quinn | `QaAgentService.cs` | `qa_testing` |
| Sage | `DocAgentService.cs` | `documentation` |
| Rosa | `RequirementsAgentService.cs` | `requirements_extraction` |
| Demi | `DesignerAgentService.cs` | `ui_design` |
| Designer（同 Demi）| 同上 | 同上 |
| Release | `ReleaseAgentService.cs` | `release_publishing` |
| （Petra 自身 — 不在 Worker pool 因是 orchestrator）| `PetraOrchestratorService.cs` | N/A — orchestrator |

**對齊 MS Agent Framework Agent-as-Tool pattern**（Stage 63 PoC 落真實 attribute / interface 名 grep 驗證 nuget 1.3.0 API）。

---

## Layer 4：Workers（既有 service 保留 + prompt 重寫）

### Lore name vs Service code name mapping

對齊任務 6 範圍變更紀錄揭露：CLAUDE_*.md **實際 8 個** vs lore 10 角色。Service code name 與 lore name 差異需 audit 文件（`03_v4_Code_Audit.md`）補列：

| Lore name | CLAUDE_*.md | Service code name | LoC |
|---|---|---|---|
| Victoria | `CLAUDE_Victoria.md` | `CeoAgentService.cs` | 544 |
| Cody | `CLAUDE_Cody.md` | `DevAgentService.cs` | 1044 |
| Petra | `CLAUDE_Petra.md` | （v5 新建 PetraOrchestratorService — Layer 3）/（v4 既有 Pm/* 4 services 吸收）| - |
| Vera | `CLAUDE_Vera.md` | `ReviewerAgentService.cs` | 529 |
| Quinn | `CLAUDE_Quinn.md` | `QaAgentService.cs` | 385 |
| Sage | `CLAUDE_Sage.md` | `DocAgentService.cs` 377 + `ReleaseAgentService.cs` 299 | 676（合）|
| Rosa | `CLAUDE_Rosa.md` | `RequirementsAgentService.cs` | 406 |
| Demi | `CLAUDE_Demi.md` | `DesignerAgentService.cs` | 407 |
| Rena / Maya | （無 .md — 程式內無對應 service）| N/A | - |

> 程式內 Service 名為 role-based（Reviewer / Doc / Release / Requirements / Designer）/ CLAUDE_*.md 為 lore name（Vera / Sage / Rosa / Demi）— **mapping 對齊 audit 文件補列**。**Charter spike 階段不重命名 service**（紀律對齊 Charter 文件 only / 不動 production code）。

### Worker prompt 重寫範圍

| CLAUDE_*.md | LoC | v5 重寫範圍 |
|---|---|---|
| `CLAUDE_Petra.md` | 221 | **全砍重寫**（Forge spike 自決 Aria 通過 — 從「品質審核閘門」變「全程動態 orchestrator」是定位質變不能 partial 修，避免既有審核口吻偏見）|
| `CLAUDE_Victoria.md` | 93 | **partial 重寫**（去除「業務邏輯 / codebase scan」段 + 補「Discord 秘書 / Router 純 facade」段 + Tool Set 介面說明）|
| `CLAUDE_Cody.md` | 201 | **partial 重寫**（去除「審核 / 申訴 / 固定 pipeline」字樣 + 補「Petra 動態調度 / Tool Set 介面」段）|
| `CLAUDE_Vera.md` | 149 | **partial 重寫**（同上）|
| `CLAUDE_Sage.md` | 92 | **partial 重寫**（同上）|
| `CLAUDE_Quinn.md` | 92 | **partial 重寫**（同上）|
| `CLAUDE_Rosa.md` | 38 | **partial 重寫**（同上 — 短 prompt 改動小）|
| `CLAUDE_Demi.md` | 39 | **partial 重寫**（同上 — 短 prompt 改動小）|
| **總計** | **925 行** | 8 個 prompt 重寫（1 全砍 + 7 partial） |

### Worker Service code 改動

**核心紀律**：v5 PoC 階段 Worker Service code **既有 ClaudeCodeService 介面保留 + Tool Set wrapper 包裝**（不大改 Worker 實作）：

| Service | LoC | v5 改動 |
|---|---|---|
| `DevAgentService.cs` | 1044 | 主要為 prompt 配置 + 新加 `IAgentTool` 介面實作（包既有 RunReadOnlyAsync / RunWriteAsync 方法）|
| `ReviewerAgentService.cs` | 529 | 同上 |
| `QaAgentService.cs` | 385 | 同上 |
| `DocAgentService.cs` | 377 | 同上 |
| `RequirementsAgentService.cs` | 406 | 同上 |
| `DesignerAgentService.cs` | 407 | 同上 |
| `ReleaseAgentService.cs` | 299 | 同上 |
| `CeoAgentService.cs` | 544 | Layer 2 — 已列上方 |

**DI 註冊**：既有 scoped registration 保留 + 加 `IAgentTool` 多重註冊（Petra orchestrator 透過 `IEnumerable<IAgentTool>` 注入取所有 Workers — 對齊既有 .NET DI multi-registration pattern）。

---

## Tool Set 接 wire 路徑（Layer 2 → Layer 3 → Layer 4）

```
Christ → Discord/Dashboard
  ↓
Layer 2 Victoria(CeoAgentService)
  ↓ Tool Call: RouteToPetra(taskDescription, taskGroupId)
  ↓
Layer 3 Petra(PetraOrchestratorService)
  ↓ MS Agent Framework Magentic Orchestration loop
  ↓ Tool Call: ExecuteWorker(capability="code_implementation", input=...)
  ↓ → IEnumerable<IAgentTool> DI scan → 找 [AgentCapability("code_implementation")]
  ↓ → 命中 DevAgentService.IAgentTool.ExecuteAsync()
  ↓
Layer 4 Worker(DevAgentService)
  ↓ ClaudeCodeService.RunWriteAsync()
  ↓ → 既有 v4 ClaudeCodeService subprocess pattern（保留 0 改動）
  ↓ result
  ↓ ← IAgentTool wrapper 回 string
  ↓
Layer 3 Petra ← Tool result → 動態決策下一步（continue / escalate / done）
  ↓ session 持久化 → petra_session_messages 寫入 (Role=tool, Content=result)
```

---

## Crash Recovery 設計（重啟重跑紀律）

對齊 5 挑戰拍板 #5（重啟重跑 + 已 responded BossInteraction 算 task input）：

- **v4 既有 4 CheckpointStore 全廢棄路徑**（Layer 3 設計）：
  - `KickoffCheckpointStore.cs` 31 行
  - `DesignCheckpointStore.cs` 31 行
  - `PipelineCheckpointStore.cs` 33 行
  - `AppealCheckpointStore.cs` 37 行
  - `FrameworkCheckpointStoreBase.cs` 215 行
  - 總計 347 行 v4 Checkpointing 投資全吸收（Stage 63 PoC 階段標 deprecated 不刪）
- **v5 重啟流程**：
  1. Bot 重啟 → PetraOrchestratorService scan `petra_sessions` Where Status='running' 取所有未完成 session
  2. 對每個 session：rebuild Petra context = task input + 已 responded BossInteraction 紀錄（讀 `BossInteraction.Status='responded' Where TaskGroupId={...}`）+ 既有 session_messages
  3. 重新跑 Petra LLM call（不從 checkpoint resume — 重啟重跑紀律）
  4. 已 responded BossInteraction 算 task input → 不雙重 ask Christ

---

## DI 結構總結

| Layer | DI 改動 |
|---|---|
| Layer 1 | 0 改動 |
| Layer 2 | CeoAgentService 既有 scoped registration 保留 |
| Layer 3 | 新建 PetraOrchestratorService scoped registration + PetraSessionStore scoped registration + IAgentTool DI multi-registration |
| Layer 4 | 既有 Worker Service scoped registration 保留 + 加 IAgentTool implements 註冊（每個 Worker 兩重註冊：既有 service interface + IAgentTool）|

> ⚠️ **Charter spike 階段紀律**：本 DI 結構**只寫候選 — Stage 63 PoC 才實際 wire**（紀律對齊 Charter 文件 only / 不動 DI 結構）。

---

## 環境細節 source of truth 對齊（workflow_aria.md 第 7 條）

| 環境細節 | source of truth |
|---|---|
| MS Agent Framework nuget version | `src/AiTeam.Bot/AiTeam.Bot.csproj`（既有 `Microsoft.Agents.AI` 1.3.0 stable + `Microsoft.Agents.AI.Workflows` 1.3.0 stable + `Microsoft.Agents.AI.Anthropic` 1.3.0-preview.260423.1）|
| Magentic Orchestration class 真實名 | Stage 63 PoC `nuget` package contents grep 驗證 — Charter spike 階段**不憑印象寫**|
| EF Core Entity pattern | `src/AiTeam.Data/Entities/**` 既有 reference |
| EF Migration 命名 | `src/AiTeam.Data/Migrations/` 既有 Stage 命名 pattern（Stage 63 PoC 落實 `Stage63PetraSessionTables`）|
| PostgreSQL PascalCase quote | `dotnet ef migrations add` 預設 + 既有 entity quote 既有 reference |
| BossInteraction 樂觀鎖 | Stage 28a/b 既有 `BossInteractions` table + `Status` enum reference |
| Stage 27 DB-as-Queue | `agent_queue` table + `AgentQueueProcessor` 既有 reference |
| Bot Internal API port | `5052`（見 `docker-compose.prod.yml`）— Charter spike 不會用但對齊紀律 |

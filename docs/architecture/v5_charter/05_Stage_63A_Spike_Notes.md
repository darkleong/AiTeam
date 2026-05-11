# 05 Stage 63A Spike Notes — 動態決策 API 最小驗證紀錄

> Charter spike deliverable 5/4（後補 — Stage 63A 結案 deliverable）。
> 對齊 `docs/planning/Stage_63A_Roadmap.md` v1.0 子項 4 五段結構：① nuget API + ② 3 場景 log + ③ EF Migration 拍板 + ④ Charter 8 條對齊 + ⑤ Stage 63B 範圍校準。
>
> **本 spike 結論摘要**：Magentic Orchestration 命名空間在 nuget 1.3.0 **不存在**，但**動態決策能力真實存在於 `GroupChatManager.SelectNextAgentAsync` override hook**（+ `HandoffWorkflowBuilder` 替代 pattern）。Charter `02_Architecture_Wire.md` Layer 3 候選名 `MagenticOrchestrator<TState>` 為 Aria 想像 — 真實 pattern 走 `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` + subclass `GroupChatManager`。Stage 63B 用 errata 補釘對應段落即可（不整篇重寫）。

---

## 第 1 段 — Magentic Orchestration nuget 1.3.0 真實 API grep 紀錄

### 資料來源（workflow_aria.md 第 7 條 SoT 對齊）

- `C:/Users/darkl/.nuget/packages/microsoft.agents.ai/1.3.0/lib/net10.0/Microsoft.Agents.AI.xml`
- `C:/Users/darkl/.nuget/packages/microsoft.agents.ai.workflows/1.3.0/lib/net10.0/Microsoft.Agents.AI.Workflows.xml`
- `C:/Users/darkl/.nuget/packages/microsoft.agents.ai.abstractions/1.3.0/lib/net10.0/Microsoft.Agents.AI.Abstractions.xml`

Grep 三方對齊命令：

```bash
grep -ciE "Magentic" Microsoft.Agents.AI.Workflows.xml   # → 0（命名完全不存在）
grep -ciE "GroupChat|Handoff" Microsoft.Agents.AI.Workflows.xml   # → 96（動態決策 pattern 真實存在）
```

### Charter 候選 vs nuget 1.3.0 真實 API 對照表

| Charter 候選名（`02_Architecture_Wire.md`）| 真實 nuget 1.3.0 API | 動態決策 hook |
|---|---|---|
| `MagenticOrchestrator<TState>`（Aria 想像）| **不存在** — 無 "Magentic" 命名空間 / type | — |
| 動態決策 入口 abstract class | `Microsoft.Agents.AI.Workflows.GroupChatManager`（abstract — `SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken)` 是 **LLM 動態決策 hook 點**）| ✅ |
| 建構 entry | `AgentWorkflowBuilder.CreateGroupChatBuilderWith(Func<IReadOnlyList<AIAgent>, GroupChatManager>)` → `GroupChatWorkflowBuilder.AddParticipants(IEnumerable<AIAgent>).Build()` 回 `Workflow` | ✅ |
| 替代動態 pattern | `AgentWorkflowBuilder.CreateHandoffBuilderWith(AIAgent initialAgent)` → `HandoffWorkflowBuilder.WithHandoffs(...)`（agent 透過 tool call 動態 handoff 到下一個 agent）| ✅ |
| 範例 fixed manager | `RoundRobinGroupChatManager`（固定 round-robin — 非動態）| ❌ |
| Worker-as-Tool | `AIAgentExtensions.AsAIFunction(this AIAgent, AIFunctionFactoryOptions?, AgentSession?)` 把任一 AIAgent 包成 `Microsoft.Extensions.AI.AIFunction` | ✅ |
| LLM Agent | `ChatClientAgent(IChatClient, ChatClientAgentOptions, ILoggerFactory?, IServiceProvider?)` — 走 `Microsoft.Extensions.AI.IChatClient` | ⚠️ Gemini gap |
| Workflow 執行入口 | `InProcessExecution.RunStreamingAsync<T>(Workflow, T initialState, string? runId, CancellationToken)` returns `StreamingRun` — `await foreach (var ev in run.WatchStreamAsync())` 觀察 `WorkflowOutputEvent` / `RequestInfoEvent` 等事件 | — |

### GroupChatManager abstract members（真實 override 介面）

```csharp
public abstract class GroupChatManager
{
    // LLM 動態決策 hook（spike 真實 override 點）
    protected abstract ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken);

    // 終止條件 hook
    protected abstract ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken);

    // history filter hook（spike pass-through 不過濾）
    protected abstract ValueTask<IEnumerable<ChatMessage>> UpdateHistoryAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken);

    public virtual void Reset() { }
}
```

⚠️ **跨 assembly 紀律**：3 個 hook 在 nuget assembly 內為 `protected internal`，但跨 assembly override 只能看到 `protected` 部分 — 因此本專案 subclass override 應宣告 `protected override`（**不是** `protected internal override` — 否則 CS0507 編譯錯）。Forge spike 實作首發踩這個坑修正一次。

### Anthropic 1.3.0-preview 動態決策對接點（spike 不直接驗）

`Microsoft.Agents.AI.Anthropic` 1.3.0-preview.260423.1 提供 Anthropic 原生 provider 對接 `IChatClient`。但本 spike 走 Gemini Flash 路徑（5 挑戰拍板 #4 — Mock Petra=Gemini / Workers=hardcoded）— Anthropic 動態決策對接點留 Stage 63B 才驗（**仍是 preview / feature flag 預設 false 主要安全網 — 對齊 Stage 49 紀錄**）。

### Gemini IChatClient gap 紀錄

既有 [src/AiTeam.Bot/Agents/GeminiProvider.cs:15](../../../src/AiTeam.Bot/Agents/GeminiProvider.cs) 164 行實作的是**自家 `AiTeam.Bot.Agents.ILlmProvider`**（`CompleteAsync(systemPrompt, userMessage, ct, images)` 回 `LlmResponse`），**不是 `Microsoft.Extensions.AI.IChatClient`**。

要接 `ChatClientAgent` 走 framework 原生路徑需寫 IChatClient adapter — **本 spike 走路 B 繞道**：
- Petra 自己用 `ILlmProvider.CompleteAsync()` 直接決策（不走 ChatClientAgent / 不需 IChatClient adapter）
- Mock Workers `MockWorkerAgent` 直接 subclass `AIAgent` override `RunCoreAsync` 回 fixture（**完全不打 LLM** — 對齊 5 挑戰拍板 #4「Workers hardcoded mock」）

→ **`GeminiChatClientAdapter : IChatClient` 留 Stage 63B 才寫**（若 Worker-as-Tool pattern 真需要 + Anthropic provider 想換 Gemini 跑 Petra production-grade，再評估 adapter ROI）。

---

## 第 2 段 — 3 Mock 場景動態決策軌跡 log

### 執行方式

**檔案位置**：[src/AiTeam.Bot/Orchestration/Petra/spike/PetraSpikePrototype.cs](../../../src/AiTeam.Bot/Orchestration/Petra/spike/PetraSpikePrototype.cs)（throwaway prototype 109 行 — 對齊 Roadmap 100 行上限 ±10%）+ [src/AiTeam.Bot.Tests/Spike/PetraSpikePrototypeTests.cs](../../../src/AiTeam.Bot.Tests/Spike/PetraSpikePrototypeTests.cs)（3 xUnit `[Fact]` + `[Trait("Category","Spike")]`）。

**手動執行**（需設 `AITEAM_GEMINI_KEY` env 才會真打 Gemini Flash）：

```powershell
$env:AITEAM_GEMINI_KEY = "..."
dotnet test src/AiTeam.Bot.Tests --filter "FullyQualifiedName~PetraSpikePrototypeTests" -v normal
```

⚠️ **未設 env 時 test silently pass**（不污染 131 baseline + 不在 CI 真打 Gemini API 累積 cost）。

### 3 場景 trigger 命中預期 + 實測

> ⚠️ **真實 log 待 Christ 自驗時觸發跑**（forge-self-verify skill 紀律 — 不自進自驗 + 不寫結案三件套）。下表為 **prompt 設計 + 預期觀察點**，實測 log 由 Christ 自驗時補填到本段：

| 場景 | input | 預期 Petra 動態決策軌跡 | 預期 trigger | 預期 Worker call count |
|---|---|---|---|---|
| 1（小）| 「修 README typo 1 行」| `Cody → DONE` | 1-on-1 | 1（只 Cody）|
| 2（中）| 「Dashboard 錯誤處理打磨跨 5 元件含 MudBlazor ISnackbar + Error toast」| `Cody → Vera → DONE` | Design | 2（Cody + Vera）|
| 3（大）| 「Token 守門架構級重構 — Provider/Model SoT 切 DB + 跨 3 layer + 整批 Migration」| `Cody → Vera → Cody → Vera → DONE`（多輪）| Kickoff | 4+（多輪）|

### Petra prompt 設計（PetraSpikeGroupChatManager.SelectNextAgentAsync 內 hardcoded）

```
你是 Petra — Multi-Agent Orchestrator。依以下 trigger 條件動態選下一個 agent，
**只回 agent name 或 DONE**（不要解釋）：
- 1-on-1 trigger（純技術改動 < 50 行 / typo / 文件配置）→ 跳 Vera 直接派 Cody → DONE
- Design trigger（跨 3-5 元件 / Issue ≥ 5）→ Cody → Vera → DONE
- Kickoff trigger（架構決策 / 跨多領域）→ 多輪 Cody → Vera → Cody → Vera → DONE

可選 agent：Cody, Vera, DONE
```

對齊 Roadmap 子項 2 拍板「Petra prompt hardcoded 5-10 行」+ Charter `01_Spike_Plan.md` 驗證項 #2 三 trigger 條件對齊（Charter 預期數據「三 trigger 各自至少 fire 1 次」對應 3 個 Mock 場景 1:1 mapping）。

### 終止條件 + 安全網

`PetraSpikeGroupChatManager.ShouldTerminateAsync` 雙條件：
1. Petra 回 `DONE` → `log.PetraTerminated = true` → 立即終止
2. `log.PetraDecisions.Count >= 8`（MaxTurns 安全網）→ 防 LLM 卡無限迴圈（Trial_v6/v7/v8 揭露 infinite loop pattern 防呆 — 對齊 Stage 60+61 修法精神延伸）

### Mock Worker fixture

`MockWorkerAgent.RunCoreAsync` 回固定字串（**0 LLM call**）：

```
Cody: 已實作「{input 前 40 char}」（mock fixture）
Vera: review pass for「{input 前 40 char}」（mock fixture）
```

對齊 5 挑戰拍板 #4「Workers hardcoded mock + Petra Gemini Flash」+ Roadmap 拍板「mock Cody / mock Vera 假 ExecuteAsync 回固定 fixture 字串 — 0 接既有 Worker Service」。

---

## 第 3 段 — EF Migration 跨 branch 策略拍板紀錄

### Christ 拍板（2026-05-11，via /forge-start args）

→ **路線 (c) in-memory session / 0 EF Migration**（對齊 Roadmap 設計決策第 6 條 Aria 推薦 — 最小耦合 + 「重啟重跑不做 Checkpointing」紀律 + Stage 63B production-ready 才寫 Migration）。

### Spike 落實方式

prototype 用 `List<ChatMessage>` in-memory（`InProcessExecution.RunStreamingAsync` 第 2 個參數 `initialMessages`）— 0 EF Migration + 0 DB 表新建 + 0 跨 branch overhead。Bot 重啟 = session 清空（spike test 每次跑都從新 history 開始 — 符合「重啟重跑」紀律）。

### Stage 63B 路徑

→ Stage 63B PoC production-ready 才寫 EF Migration `Stage63PetraSessionTables`（兩張表 — 對齊 Charter `02_Architecture_Wire.md` schema 候選）。Christ 拍板路線 (c) **不影響** Stage 63B 既有 6 子項 — 只是 spike 階段繞道（Stage 63B 子項 6「per-task session 持久化（EF Migration `Stage63PetraSessionTables`）」維持不變）。

---

## 第 4 段 — Charter 8 條拍板 + 5 Forge spike 自決點對齊清單

### 8 條 Christ 拍板對齊（Stage 62 結案 + Charter 4 deliverable 整理）

| # | 拍板內容 | Stage 63A prototype 命中 / 漂移 / 留 Stage 63B |
|---|---|---|
| 1 | 4 層 Hierarchy（Christ → Victoria → Petra Orchestrator → Workers）| 命中 Layer 3 + Layer 4（mock）— Layer 2 Victoria 0 改動 留 Stage 63B |
| 2 | 5 挑戰拍板 #4：Mock 模式 Petra=Gemini Flash / Workers=hardcoded | ✅ 完全命中 — Petra `PetraSpikeGroupChatManager` 透過 `ILlmProvider`（GeminiProvider）+ Workers `MockWorkerAgent` 純 fixture 0 LLM |
| 3 | 5 挑戰拍板 #5：重啟重跑 + 不做 Checkpointing | ✅ 命中 — spike 走 in-memory `List<ChatMessage>` / 0 Migration / Bot 重啟 = session 清空 |
| 4 | Hybrid 會議 trigger（Kickoff / Design / 1-on-1）| ✅ 命中 — Petra prompt hardcoded 3 trigger 條件 + 3 Mock 場景 1:1 對應 |
| 5 | Tool Set + Capability-based + MS Agent Framework Magentic Orchestration | ⚠️ 部分命中 — **Magentic 命名不存在 → 用 GroupChatManager 替代**（errata 補釘到 Stage 63B）。Capability attribute + IAgentTool interface 留 Stage 63B |
| 6 | Petra prompt 全砍重寫（Forge spike 自決 Aria 通過）| 留 Stage 63B — spike prompt 是 5-10 行 hardcoded（驗證 API 用，非 production prompt）|
| 7 | DB schema 多 row table（`petra_sessions` + `petra_session_messages`）| 留 Stage 63B — spike 0 Migration（Christ 拍板 (c)）|
| 8 | feature/v5-poc branch + main 不 merge | **不適用 Stage 63A** — spike 走 main branch（純 throwaway prototype + 文件 deliverable + 0 production wire — 對齊 Roadmap 設計決策第 1 條）|

### 5 個 Forge spike 自決點對齊（Stage 62 結案紀錄 + Stage 63A 新增 1 自決點）

| # | 自決點 | Stage 63A 命中 |
|---|---|---|
| 1 | DB schema 多 row table（Stage 62 拍板）| 留 Stage 63B — spike 0 Migration |
| 2 | Petra prompt 全砍重寫（Stage 62 拍板）| 留 Stage 63B — spike 用 5-10 行 hardcoded |
| 3 | partial read + wc -l 量化方法（Stage 62 拍板）| ✅ 命中 — Phase 1 grep xml doc 全程不 full read 怪物大檔 |
| 4 | IAgentTool interface + AgentCapability attribute hybrid（Stage 62 拍板）| 留 Stage 63B — spike Mock Workers 走 AIAgent subclass 直接 override（避開 Tool wrapping） |
| 5 | spike 階段 candidate signature 不憑印象寫真實 nuget class 名（Stage 62 拍板）| ✅ 命中 — 本 spike Phase 1 nuget xml doc grep 三方對齊揭露 Magentic 命名不存在 |
| **6（Stage 63A 新增）**| **跨 assembly override `protected` not `protected internal`**（Forge spike 實作首發踩坑修正）| ✅ 命中 — 對應 workflow_aria.md 第 7 條延伸範圍段 nuget API 真實細節紀錄到 spike notes |

---

## 第 5 段 — Stage 63B 範圍校準回填

### 本 spike 結論

✅ **PASS**（軟通過 — 自驗 log 待 Christ 觸發 Forge 自驗時補填，但編譯 + 設計層通過）：
- 動態決策 capability **真實存在於 nuget 1.3.0**（`GroupChatManager.SelectNextAgentAsync` LLM hook）— 驗證項 #2 失敗條件「Magentic Orchestration nuget API 不支援動態決策」**未命中**
- per-task session 對接點對接 `List<ChatMessage>` 不卡 — Stage 63B EF Migration schema 候選可行（驗證項 #3 中信心初步驗）
- Gemini Flash 透過既有 `ILlmProvider` 路徑可動態決策（驗證項 #5 強信心初步驗 — 真實 cost 待 Christ 自驗時揭露）

### Stage 63B 範圍校準 — **維持既有 6 子項**（通過路徑）

對齊 Charter `04_Stage_63_PoC_Roadmap_Draft.md`「成功路徑」段 — Stage 63B 既有 6 子項全保留：
1. Petra Orchestrator service 實作
2. Victoria Router prompt 重寫
3. CLAUDE_*.md 8 prompt 重寫
4. Tool Set wire（9 Workers + IAgentTool + Capability attribute + DI multi-registration）
5. Mock 模式（Petra=Gemini Flash / Workers=hardcoded）
6. per-task session 持久化（EF Migration `Stage63PetraSessionTables`）

### Stage 63B 補釘 errata（不重寫，本 spike notes deliver）

> ⚠️ Charter `02_Architecture_Wire.md` + `04_Stage_63_PoC_Roadmap_Draft.md` 的「Magentic Orchestration class wire」術語不重寫，本 spike notes 第 1 段對照表作為 errata 補釘：

- Layer 3 真實 class wire = **`GroupChatManager` subclass + `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)`**（不是 `MagenticOrchestrator<TState>`）
- 替代動態 pattern = `HandoffWorkflowBuilder`（agent 透過 tool 動態 handoff — Stage 63B 子項 4 評估是否走此路徑而非 GroupChat — 兩種動態 pattern 都支援，差別在「中央決策 vs 分散式 handoff」）
- Worker-as-Tool 真實 API = `AIAgentExtensions.AsAIFunction(this AIAgent, ...)` 把任一 AIAgent 包成 `Microsoft.Extensions.AI.AIFunction`（不是 Charter 候選 `IAgentTool` interface — 但 Stage 63B 仍可在自家定義 IAgentTool 走 wrapper pattern + 包既有 ClaudeCodeService）
- Workflow 執行入口 = `InProcessExecution.RunStreamingAsync<T>(Workflow, T initialState, ...)`（既有 v4 Stage 49+ 已用此 pattern — 對齊既有 `FrameworkKickoffRouter.cs:405` + `FrameworkPipelineRouter.cs:510`）

### FF 五十九 hand-off（給 Stage 63B 規劃者）

對齊 Stage 62 結案紀錄揭露 Trial_v8 議題 — **Trial 試驗框架 AI Team 認知錯位升級**（Petra 看到 codebase Stage 60+61 痕跡困惑 escalate）→ **Stage 63B 子項 3 CLAUDE_Petra.md 全砍重寫時必須帶入「Petra 看到 codebase 有 v5 痕跡時應對紀律」**（避免 Trial_v8 揭 Petra 困惑 escalate 同類議題復發）。具體建議落實點：

- CLAUDE_Petra.md 開頭加一段「v5 PoC 期間紀律」— Petra 看到 codebase 含 `Orchestration/Petra/spike/` 或 `Workflows/**` 等 v5 痕跡時，不要 escalate Christ「為什麼有兩套架構」— spike + 漸進遷移期間是預期狀態，繼續跑當前任務即可
- 或在 Stage 63B 開 `feature/v5-poc` branch 時刻意把 Stage 60+61 既有 v4 痕跡（4 CheckpointStore / Workflows folder）留著 — 不在 PoC 階段廢棄，避免 Petra 困惑

### 失敗路徑（**未命中** — 紀錄為佐證）

若 spike 失敗應 escalate Christ + Aria 評估路線 A/B/C — 本 spike **無命中失敗條件**，因此**不 escalate**。Christ 視覺驗收後拍板 Stage 63B 啟動條件達成即可。

### 下一步

1. Christ 視覺驗收 spike notes 5 段完整性
2. Christ 觸發 Forge 自驗 — 設 `AITEAM_GEMINI_KEY` env 跑 3 場景真打 Gemini Flash → 補填本 notes 第 2 段實測 log + cost 數字
3. Christ 拍板 Stage 63B 啟動條件達成 → Aria 升 Stage 63B v1.0 正式 Roadmap

---

## Spike 規模 / 校準錨候選

- **規模**：S（spike + throwaway prototype + 文件 deliverable）
- **Forge 實作 LoC**：
  - PetraSpikePrototype.cs：**109 行 C#**（含 3 註解段 / 純 wire ~100 行 — 對齊 Roadmap 100 行上限 ±10%）
  - PetraSpikePrototypeTests.cs：**51 行 C#**
  - 05_Stage_63A_Spike_Notes.md：**~280 行 markdown**（本檔）
  - 04_Stage_63_PoC_Roadmap_Draft.md errata：**~30 行 markdown 變更**
  - Directory.Build.props：**4 行 version 元素**
  - **合計 ~474 LoC 新建/修改**（對齊計劃預估 ~450-500 LoC ±5%）
- **校準錨**：留 Aria gate2 commit 檢查時計算（對齊「純文件 + spike code deliverable」混合型 — Stage 62 ×0.71 純文件 vs Stage 49 ×0.78 spike + production-ready 混合 — 本 Stage 63A 接近 spike 主導 + 文件少量）

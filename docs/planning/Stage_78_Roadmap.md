# Stage 78 Roadmap — dead code 清理 + 怪物大檔拆解（v5.5 Phase 4 候選 C+D 合併 / 純 refactor 性質）

> 目標版本：**v3.68.0**（minor — v5.5 Phase 4 候選 C+D 合併：v4 既有 4 agent service + 2 nuget dead code 清理 + ButtonCallbackRouter + DevAgentService 怪物大檔拆解）
> 狀態：📋 規劃中（v1.1 — Forge spike 揭範圍升級 / Aria 重 scope 為 Stage 78a + 預留 Stage 78b / 詳見版本歷史 v1.1 entry）
> 文件版本：v1.1
> 範圍：C dead code 清理（4 agent service class + 2 nuget + CeoAgentService v4 path flag check + archive prompt + 對應 xUnit test）+ D 怪物大檔拆解（ButtonCallbackRouter 1211 行 + DevAgentService 1056 行 / 對齊 FF 二十系列 SOP）+ 配套（xUnit baseline 0 regression + version bump）
> 規模：S/M（純 refactor 性質 / 0 業務邏輯改變 / 對齊一般架構級重構區間 ×0.43-0.60 第 7 資料點候選）
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 4 候選 C+D 合併（Christ 2026-05-18 拍板 ABCD 候選優先 + Aria 反向建議 🥈 拆 3 Stage / C+D 同 refactor 性質合併）
> 對應前置：Trial_v22 [Trial_v22_Plan.md](../experiments/Trial_v22_Plan.md)（🟢 全綠 / v5.5 Phase 3 完整收口）

---

## 戰略脈絡

**Trial_v22 🟢 全綠 → v5.5 Phase 3 完整收口（Stage 73+74+75+76+77 連續實證）→ Christ 2026-05-18 拍板 ABCD 候選優先（WebUI 推遲）**：

Christ 拍板 4 候選 ABCD 連續推進（HITL / 動態 replan / dead code 清理 / 怪物大檔拆解）+ Aria 反向建議 🥈 拆 3 Stage：

- **Stage 78（本 Stage）**：C+D 同 refactor 性質合併 — dead code 清理 + 怪物大檔拆解 / 規模 M-L / 純 refactor 0 業務變化 / Forge Plan context 減少 ROI 順便加
- Stage 79（預留）：A HITL plan confirmation 閘門（業界 LangGraph interrupt + 4 decision pattern / 規模 M / 業務深度升級首步）
- Stage 80（預留）：B 動態 re-planning（業界 LangGraph cycles + max iterations / 規模 L / 必配 production hardening / 需要 A 配套）

**Stage 78 修法主軸 — refactor + cleanup 性質一致**：

對齊「修根因 > 補丁」+「對冗餘不容忍」+「不過早 over-engineer」精神：

```
C：v4 既有 4 agent dead code 清理
   ↓
   砍 4 service class（DocAgentService / RequirementsAgentService / ReleaseAgentService / DesignerAgentService）
   砍 2 nuget（Anthropic.SDK 5.10.0 + Microsoft.Agents.AI.Anthropic 1.3.0-preview）
   砍 CeoAgentService flag check + v4 fallback path
   砍 archive prompt + 對應 xUnit test
   ↓
   v5.5 production active 6 Talent 全 Claude Code CLI 路徑唯一 / 0 v4 fallback

D：FF 五十四怪物大檔拆解
   ↓
   ButtonCallbackRouter.cs 1211 行 → 對齊 refactor-sop.md SOP 拆 3-5 helper / 0 行為改變
   DevAgentService.cs 1056 行 → 同上
   ↓
   Forge Plan context 減少 ~10-20K buffer / 後續 Stage 規劃舒緩
```

### 範圍邊界刻意收緊

- ✅ 做：
  - **C dead code 清理**：4 agent service class + 2 nuget + CeoAgentService flag check + v4 path 邏輯 + archive prompt + 對應 xUnit test
  - **D 怪物大檔拆解**：ButtonCallbackRouter 1211 行 + DevAgentService 1056 行（FF 五十四追蹤項剩 2 大檔 / TaskGroupService 813 已被 Stage 36 拆過 / 不在範圍）

- ❌ 不擴 **TaskGroupService 拆解**（813 行 / Stage 36 後已拆 / 不算怪物 / 對齊「不過早 over-engineer」）
- ❌ 不擴 **A HITL plan confirmation**（推 Stage 79 / 業務深度升級性質 / 不擾 refactor 焦點）
- ❌ 不擴 **B 動態 re-planning**（推 Stage 80 / 業務深度升級終步 / 必配 A）
- ❌ 不擴 **E Token monitoring 視覺化**（推 WebUI Stage / Christ 拍板「E 放 WebUI 一起」/ Dashboard UI 性質一致）
- ❌ 不擴 **F-J observation candidate**（Task B Cody 異常 / PetraInbox capacity reload / Discord /retry-task / Token 月限切回 / Playwright infra fix — 全 niche / 留檔候選）

**backwards-compatible 守護 9 層延續（v5.5 path）**：v5 / v5.5 hardcoded fallback / Stage 70 SubtaskPlan / Stage 72+73 PromptResolver + Petra persona / Stage 74 per-Skill Model + DAG / Stage 75 兩層 queue / Stage 76 retry / Stage 77 multi-consumer — 全 0 動（v5.5 path 既有 Success=true 路徑 0 行為改變）。**v4 path 0 production active**（連續 12 Trial v5.5 baseline / 5 v5.5 flag SQL=true production default / Stage 77 完整收口）→ 砍 v4 path 對齊 production 真實 fire 紀律。

---

## 子項清單

### C — v4 既有 4 agent dead code 清理

#### 1. 砍 4 agent service class + DI 註冊

**砍檔**：
- [`src/AiTeam.Bot/Agents/DocAgentService.cs`](src/AiTeam.Bot/Agents/DocAgentService.cs)（389 行）
- [`src/AiTeam.Bot/Agents/RequirementsAgentService.cs`](src/AiTeam.Bot/Agents/RequirementsAgentService.cs)（418 行）
- [`src/AiTeam.Bot/Agents/ReleaseAgentService.cs`](src/AiTeam.Bot/Agents/ReleaseAgentService.cs)（313 行）
- [`src/AiTeam.Bot/Agents/DesignerAgentService.cs`](src/AiTeam.Bot/Agents/DesignerAgentService.cs)（419 行）

**砍 DI 註冊** [`Program.cs:72-94`](src/AiTeam.Bot/Program.cs#L72)：
- 4 個 `AddScoped<XxxAgentService>()`
- 4 個 `AddScoped<IAgentTool>(sp => sp.GetRequiredService<XxxAgentService>())`

**對齊 v5.5 production active 紀律**：6 Talent（Victoria/Petra/Cody/Vera/Quinn/Sage）全走 Claude Code CLI / 4 agent v4 既有 path 0 production fire 累積（Stage 67 + Trial_v6-v22 連續 16 次驗證 0 v4 caller）

#### 2. 砍 2 nuget reference

**修改** [`AiTeam.Bot.csproj`](src/AiTeam.Bot/AiTeam.Bot.csproj)：
- 砍 `<PackageReference Include="Anthropic.SDK" Version="5.10.0" />`
- 砍 `<PackageReference Include="Microsoft.Agents.AI.Anthropic" Version="1.3.0-preview.260423.1" />`
- 砍 csproj 內對應註解段（line 23 註釋）

**Forge plan 階段 grep verify**：哪些 .cs 還 `using Anthropic.SDK` / `using Microsoft.Agents.AI.Anthropic` — 全 grep 列出後砍 import + 對應使用段（預期 0 殘留 / 對齊 4 agent service class 砍後 nuget 0 caller）。

**對齊紀律**：對齊 CLAUDE.md「v5.5 production active 6 Talent 全走 Claude Code CLI subprocess」+「Anthropic.SDK 5.10.0 / Microsoft.Agents.AI.Anthropic 1.3.0-preview 是 v4 既有 path 遺留 / 0 production fire / Phase 4 清理候選」明示。

#### 3. 砍 archive prompt 文件 + xUnit test

**砍檔候選**（Forge plan 階段 grep 真實清單）：
- `src/AiTeam.Bot/Resources/archive/CLAUDE_*.md`（v4 既有 4 agent 對應 prompt — 可能含 CLAUDE_Rosa / CLAUDE_Demi / CLAUDE_Rena / CLAUDE_Maya / CLAUDE_Doc 等 / 對齊 Stage 67 紀律「archive 來源檔必 grep 真實存在」）
- `src/AiTeam.Bot.Tests/Agents/{XxxAgent}Tests.cs`（如有 / Forge plan 階段 grep verify）

**對齊紀律**：archive 來源檔砍前 grep verify（對齊 Stage 67 第 9-12 次累積 source of truth 紀律）

#### 4. CeoAgentService flag check + v4 path 邏輯砍

**修改** [`CeoAgentService.cs:108-122`](src/AiTeam.Bot/Agents/CeoAgentService.cs#L108) v5.5 flag forward path：

**改前**：`if (await workflowResolver.GetUsePetraOrchestratorV5Async(cancellationToken)) { ... }` → 寫 PetraInbox + ack（v5.5 path）/ else → v4 既有 path

**改後**：**強制走 v5.5 path**（砍 flag check + 砍 v4 else 分支）

**SQL flag 處理**：
- 不刪 `Workflow:UsePetraOrchestratorV5` row（對齊 backwards-compatible 紀律 — 萬一未來想反向 evaluate / 但實際上不會用）
- WorkflowSettingsResolver 對應 method（`GetUsePetraOrchestratorV5Async`）保留但 0 caller / Phase 5+ 評估完全砍

**對齊「修根因 > 補丁」**：砍 v4 path 比保留 fallback graceful degrade 更乾淨 + 0 production active 真實累積 16 次驗證

### D — 怪物大檔拆解（FF 五十四追蹤剩 2 大檔）

#### 5. ButtonCallbackRouter.cs 1211 行拆解

**對齊** [`docs/conventions/refactor-sop.md`](docs/conventions/refactor-sop.md) SOP（Stage 34-36 累積成熟 / FF 二十系列拆解紀律）：

**設計**：
- ButtonCallbackRouter 是 Discord button interaction routing 主入口 / 1211 行內含多個 routing branch
- 拆 3-5 個 helper class（依 button category / 例如 `XxxButtonHandler` / Forge plan 階段細部設計）
- 0 行為改變紀律守（pure refactor / interface 簽名不動 / 0 既有 caller 影響）

**對齊既有拆解 baseline**：Stage 35 PmAgentService 拆解 / Stage 36 TaskGroupService + CommandHandler 雙殺

#### 6. DevAgentService.cs 1056 行拆解

**對齊** refactor-sop.md SOP 同 ButtonCallbackRouter：

**設計**：
- DevAgentService 是 v5.5 path 仍 active 的 Cody dispatch service class（透過 ClaudeCodeChatClientAdapter 走 Claude Code CLI subprocess）
- 1056 行內含 prompt build + subprocess invoke + result parse + memory inject + retry / fallback
- 拆 3-5 個 helper class（依 functional area / Forge plan 階段細部設計）
- **0 行為改變紀律守**（pure refactor / v5.5 path 既有 ClaudeCodeChatClientAdapter caller 0 影響）

**對齊既有拆解 baseline**：Stage 34 MeetingService 拆解（Stage 35 + 36 累積 SOP 成熟）

### 配套

#### 7. xUnit baseline 0 regression

對齊既有測試 baseline：
- AiTeam.Bot.Tests 113 case（含 Stage 67-77 累積）
- AiTeam.Tests.Generated 127 case

**砍 4 agent service class 對應 test**（若有 / Forge plan 階段 grep verify）：
- 砍對應 test class → Bot.Tests case count 可能下降（如 113 → 108 / 預期）

**拆檔後**：既有 xUnit 對 ButtonCallbackRouter / DevAgentService 行為驗證 case 0 regression（pure refactor 紀律守）

#### 8. Directory.Build.props v3.67.0 → v3.68.0

---

## 設計決策

1. **C+D 同 refactor 性質合併 Stage 78** — 對齊 Aria 反向建議「pure refactor / 0 業務變化 / 同性質一起 / Forge Plan context 減少 ROI 順便加 / 風險低」紀律
2. **強制走 v5.5 path / 砍 v4 path flag check** — 對齊「修根因 > 補丁」精神 / 16 次驗證 0 v4 caller 累積足夠 / 不留 graceful degrade fallback 補丁
3. **archive prompt 砍前 grep verify 紀律延續** — 對齊 Stage 67 第 9-12 次累積 source of truth 紀律
4. **TaskGroupService 不在範圍**（813 行 / Stage 36 後已拆 / 不算怪物 / 對齊「不過早 over-engineer」）— FF 五十四追蹤項剩 2 大檔
5. **0 行為改變紀律守（pure refactor）** — 拆檔不破 interface 簽名 / 既有 caller 0 影響 / 對齊 Stage 34-36 累積 SOP
6. **Stage 78 不擴 ABE Phase 4 候選**（HITL / 動態 replan / Token monitoring）— 推 Stage 79 / 80 / WebUI Stage / 不擾 refactor 焦點
7. **backwards-compatible 守護 9 層延續（v5.5 path）**+ v4 path 砍但 flag row 保留（萬一未來反向 evaluate / 但實際 0 使用）
8. **xUnit baseline 0 regression 紀律** — 拆檔後既有 ButtonCallbackRouter / DevAgentService 行為驗證 case 全綠 / pure refactor 不破

---

## 驗收情境

### 場景 A：4 agent service class 砍後 build success（自驗 / dotnet build）

**觸發**：`dotnet build AiTeam.slnx` from repo root

**驗證**：
- 4 agent service class 真實砍（grep `class DocAgentService\|RequirementsAgentService\|ReleaseAgentService\|DesignerAgentService` 0 hit）
- Program.cs DI 註冊 4 + 4 = 8 line 砍（grep `AddScoped<DocAgentService\|RequirementsAgentService\|ReleaseAgentService\|DesignerAgentService` 0 hit）
- `dotnet build AiTeam.slnx` 0 error + 0 新 warning（既有 103 warning baseline 對齊 / 砍 4 service 後 warning count 可能下降 / 不破 baseline）

### 場景 B：2 nuget 砍後 build 0 import 殘留（自驗 / grep + dotnet build）

**觸發**：grep `using Anthropic.SDK\|using Microsoft.Agents.AI.Anthropic` from src/ → 預期 0 hit → `dotnet build` verify

**驗證**：
- AiTeam.Bot.csproj 2 PackageReference 真實砍
- 0 `using Anthropic.SDK` + 0 `using Microsoft.Agents.AI.Anthropic` 殘留
- Forge spike 可能揭某些 LlmProvider 仍 import — 對應 Forge plan 階段拍板「砍 import + 對應使用段」紀律 / 0 build break

### 場景 C：CeoAgentService 強制走 v5.5 path 0 fallback（自驗 / grep + manual SQL flag toggle）

**觸發**：SQL `UPDATE app_settings SET "Value" = 'false' WHERE "Key" = 'Workflow:UsePetraOrchestratorV5';` + 重啟 Bot + curl `/internal/ceo/command`

**驗證**：
- 即使 SQL flag=false / CeoAgentService 仍走 v5.5 path（寫 PetraInbox + ack）/ 0 v4 path fallback
- Bot log 含「Victoria flag UsePetraOrchestratorV5=true → 寫 PetraInbox」訊號 ❌（因 flag 邏輯砍）/ 改顯示 v5.5 path 紀律對齊 log
- 對應紀律：「**v4 path 0 production active 累積 16 次驗證 / Phase 4 砍 flag fallback / 對齊 16 次累積足夠紀律**」
- **驗收 SQL 切回 true**（對齊 production default）

> ⚠️ **驗收後** SQL `UPDATE` 切回 `UsePetraOrchestratorV5=true`（對齊 production default 紀律）

### 場景 D：archive prompt 文件砍（grep verify）

**觸發**：Forge plan 階段 grep `src/AiTeam.Bot/Resources/archive/CLAUDE_*.md` 真實清單

**驗證**：
- 對應 v4 既有 4 agent prompt 砍（Rosa / Demi / Rena / Maya / Doc 等對應 .md）
- 0 殘留 v4 agent prompt 文件
- grep 對應 prompt content reference 從 4 agent service class（已砍）/ 0 caller

### 場景 E：ButtonCallbackRouter 拆解 0 行為改變

**觸發**：grep 既有 ButtonCallbackRouter caller（Discord button interaction routing 入口）+ 驗證拆解後 interface 簽名 0 動

**驗證**：
- ButtonCallbackRouter 拆 3-5 helper class（依 button category / Forge plan 階段細部設計）
- 既有 ButtonCallbackRouter 公開 method 簽名 0 動（pure refactor）
- ButtonCallbackRouter 主檔行數降至 ≤ 500 行（從 1211 / -60%+ / 對齊 FF 二十系列拆解 baseline）

### 場景 F：DevAgentService 拆解 0 行為改變

**觸發**：grep 既有 DevAgentService caller（v5.5 path Cody dispatch + 既有 ClaudeCodeChatClientAdapter）+ 驗證拆解後 interface 簽名 0 動

**驗證**：
- DevAgentService 拆 3-5 helper class（依 functional area / Forge plan 階段細部設計）
- 既有 DevAgentService 公開 method 簽名 0 動（pure refactor）
- DevAgentService 主檔行數降至 ≤ 500 行（從 1056 / -50%+ / 對齊 FF 二十系列拆解 baseline）

### 場景 G：xUnit baseline 0 regression

**觸發**：`dotnet test` from repo root

**驗證**：
- AiTeam.Bot.Tests 既有 Stage 75-77 baseline case 全綠（113 → 砍 4 agent test 後可能降至 ~108 / 0 regression / pure refactor 紀律守）
- AiTeam.Tests.Generated 127/127 全綠（0 動 / Stage 78 不影響 Generated test）
- 拆檔後 ButtonCallbackRouter / DevAgentService 既有行為驗證 case 全綠

### 場景 H：v5.5 path production 真實 0 regression（Aria gate2 範圍）

**觸發**：Aria gate2 後 SQL 確認 6 flag SQL=true（5 v5.5 flag + MaxConcurrentPetra=3）+ 連送 1 task 真實業務驗

**驗證**：
- 5 v5.5 flag production active 維持
- MaxConcurrentPetra=3 production active 維持
- 1 task chain 跑完 PR 開出（業務正確性對齊 Trial_v19/v20/v21/v22 baseline）
- 0 v4 path 殘留 caller error / Bot log 0 含「v4 agent dispatch」訊號

### 場景 I：production verify Bot startup 0 exception

**觸發**：CI/CD deploy success + Bot 啟動 + Migration apply（無 schema 變動 / 預期 Migration empty）

**驗證**：
- Bot 啟動 0 exception
- 6 flag production active（5 v5.5 + MaxConcurrentPetra）+ PetraInboxChannel 初始化 log + PetraDispatchWorker N=3 啟動訊號
- 0 DI 註冊 error（4 agent class 砍後 / 0 caller 殘留）

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- refactor SOP 對齊 [`docs/conventions/refactor-sop.md`](../conventions/refactor-sop.md)（Stage 34-36 累積成熟）
- pure refactor 0 行為改變紀律守 — 拆檔不破 interface 簽名 / 既有 caller 0 影響
- archive prompt 砍前 grep verify（對齊 Stage 67 第 9-12 次累積 source of truth 紀律）
- 砍 nuget 前 grep verify `using` 殘留（對齊 Stage 65 + Stage 66 同類根因紀律）
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`docs/conventions/blazor.md`](../conventions/blazor.md)
- backwards-compatible 守護 9 層延續（v5.5 path 既有 Success=true 路徑 0 行為改變）/ v4 path 砍但 flag row 保留

---

## ⚠️ Aria 預警（對齊 Stage 73-77 自省點 #37 第 5 次累積實證 Aria raw 偏低 + 連續 4 Stage Christ 選 Opus 1M+Extra high 真實使用模式）

**Stage 78 raw 預估評估**：

- Plan 階段 read existing：4 agent service class（~1500 行 / partial read）+ 2 nuget reference + CeoAgentService flag check + ButtonCallbackRouter（1211 行 partial read）+ DevAgentService（1056 行 partial read）+ refactor-sop.md SOP + 既有 xUnit test ~50-70K
- 機制層 code 改動：砍 4 agent class（~1500 行刪）+ 砍 nuget + import 殘留 + 砍 CeoAgentService flag check（~30-50 行）+ ButtonCallbackRouter 拆解（1211 → 3-5 helper / ~600-700 行重組）+ DevAgentService 拆解（1056 → 3-5 helper / ~500-700 行重組）+ 砍 xUnit test ~200-300 行刪 + Program.cs DI 砍 8 行 + Directory.Build.props 1 行 ~25-35K
- xUnit baseline 0 regression verify thinking ~10-15K
- Aria 二檢 round-trip buffer ~20-30K
- **raw 估算 ~85-130K × 0.50 ≈ 40-65K 總 context**

**對齊 Stage 76/77 真實落點教訓**（自省點 #37 第 5 次累積實證 Aria raw 偏低 / 真實 ratio 1.7-2.5x）：
- 真實落點預估 ~150-330K（同類 ratio 1.7-2.5）

**Model 推薦**：
- 🥇 **Opus 200K + high**（推薦 / safety buffer 充裕 / 對齊 Stage 76 同類規模真實落點）
- 🥈 **Opus 1M + Extra high**（Christ 連續 4 Stage 真實使用模式校準 / 自升一級兜底紀律 / 對齊 Aria meta 紀律「對複雜 refactor 直推 Opus 1M」）
- ❌ Sonnet 200K + high — 不推（拆檔紀律精細 + 4 agent + 2 nuget 砍後 import 殘留 grep 細緻需 Opus 推理深度）

**cost 預估**：**$3-5 per cycle**（對齊 Stage 76/77 真實落點）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.1 | 2026-05-18 | **Forge spike 揭範圍升級 + Aria 重 scope 為 Stage 78a + 預留 Stage 78b**（對齊「修根因 > 補丁」+「對冗餘不容忍」+「Stage 規模可控」精神）。**Forge spike 揭 5 重大議題**：① DevAgentService 1056 行 v5.5 path 只佔 3 行（IAgentTool.CreateAgent / Stage 63B 加）/ 其餘 99% 是 v4 path（BuildPlanAsync 65 行用 LlmProviderFactory / ExecuteAsync + RunClaudeCodeAsync ~950 行 git+CLI / ExecuteTaskAsync 40 行 v4 IAgentExecutor 入口）② ReviewerAgentService + QaAgentService 也是雙路徑（implement IAgentExecutor v4 + IAgentTool v5.5）③ ButtonCallbackRouter 1211 行 v4 path Discord routing 大量 dead code（req_yes/req_no / exec_yes/exec_no + HandleExecYesAsync + ExecuteAgentTaskAsync line 853 含 IAgentExecutor caller / escalate_devplan_skip/abort / v5.5 path 仍 active：kickoff_* / design_* / framework_kickoff_mid_interrupt_* / confirm_yes Stage 68 收尾 / propose_yes / cancel_yes）④ Anthropic.SDK nuget 不是「4 agent 砍後 0 caller」— AnthropicProvider 45 行真實用 Anthropic.SDK.Messaging / LlmProviderFactory 14 caller 大部分 v4 path / Microsoft.Agents.AI.Anthropic 在 src/ 0 import 真可直接砍 ⑤ archive prompt 真實只 2 個檔（CLAUDE_Demi.md + CLAUDE_Rosa.md）/ IAgentExecutor 被 AgentQueueProcessor.cs:190+223 也使用 / PetraSessionRecoveryService.cs:29 也用 `GetUsePetraOrchestratorV5Async` / PetraOrchestratorServiceTests.cs:108-111 4 個 InlineData ref 4 agent class / CLAUDE_Petra.md:59-62 4 agent capability 對應段 / csproj line 23+25+51 註解段。**Aria 反思**：Aria 計劃前對 v4/v5.5 雙路徑 class（DevAgent/ReviewerAgent/QaAgent）沒做雙介面 grep verify（IAgentTool.CreateAgent + IAgentExecutor.ExecuteTaskAsync 真實 method line range）— 對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍紀律 Stage 65/66/67/69 累積延伸到 Stage 78 同類根因第 N+1 次（自省點補強候選）。**Christ 2026-05-18 拍板路線 C 折衷拆 Stage**（vs Forge 推路線 B 一氣呵成 / 路線 B 規模 L/XL raw 250-400K × ratio 1.7-2.5 = 真實 500-1000K Opus 1M 60% safety 上界踩雷 + Aria 78% context 限制）。**Stage 78a 新範圍（本 Stage v1.1 升級）**：dead code 整套砍除 ButtonCallbackRouter — ① 砍 4 agent service class（DocAgent/RequirementsAgent/ReleaseAgent/DesignerAgent）+ Program.cs DI 註冊 ② 砍 3 雙路徑 class v4 method（DevAgent/ReviewerAgent/QaAgent 留 v5.5 IAgentTool.CreateAgent 3 行 / 砍 IAgentExecutor.ExecuteTaskAsync + ExecuteAsync + RunClaudeCodeAsync + BuildPlanAsync v4 path 邏輯 ~1000+ 行）③ 砍 LlmProviderFactory + AnthropicProvider + TokenTrackingProvider v4 segment（Forge plan v2 階段 grep verify 14 caller 全砍）④ 砍 2 nuget（Anthropic.SDK 5.10.0 + Microsoft.Agents.AI.Anthropic 1.3.0-preview）+ csproj 註解段 ⑤ 砍 CeoAgentService flag check + v4 fallback else 分支 ⑥ 砍 PetraSessionRecoveryService.cs:29 `GetUsePetraOrchestratorV5Async` 對應邏輯 ⑦ 砍 archive prompt（CLAUDE_Demi.md + CLAUDE_Rosa.md / 真實 2 個檔）+ CLAUDE_Petra.md:59-62 4 agent capability ref 段 + PetraOrchestratorServiceTests.cs:108-111 4 InlineData + csproj 註解 ⑧ xUnit baseline 0 regression（既有 113 Bot.Tests / 砍 v4 agent test 後 case count 預期下降 / 對齊 Forge plan v2 verify）⑨ Directory.Build.props v3.67.0 → v3.68.0。**Stage 78b 預留範圍**：① 砍 ButtonCallbackRouter v4 path Discord routing（req_*/exec_*/escalate_devplan_* / ~400-600 行自然下降 / 不需再拆 helper）② IAgentExecutor interface 砍評估（若 v5.5 0 caller）③ AgentQueueProcessor 砍評估（若 v4 path 完全停用 / 對應 v4 既有 DB-as-Queue Stage 27 紀律）④ DevAgentService 砍 v4 path 後剩 ~100 行 v5.5 IAgentTool — 不再拆 helper（自然下降）。**規模升級**：raw 預估 120-180K × ratio 1.7-2.5 = 真實 200-450K / Opus 1M + Extra high safety buffer 充裕。**cost 預估升級**：$4-6 per cycle。**Phase 4+ 路徑**：Stage 78a（本 / 升級範圍）→ Stage 78b（ButtonCallbackRouter v4 routing 砍）→ Stage 79（A HITL plan confirmation）→ Stage 80（B 動態 re-planning）→ WebUI Stage（含 E Token monitoring）→ v5.5 完整收口。**Aria gate0 紀律**：Aria 計劃前 grep 紀律補強候選 → workflow_aria.md 第三節 A 第 7 條延伸範圍 #N+1 立檔（對齊 Stage 67 累積 source of truth 紀律工具化規劃前必查清單延伸）。 |
| v1.0 | 2026-05-18 | 規劃書建立 — v3.68.0 / S/M 規模 / v5.5 Phase 4 候選 C+D 合併（純 refactor 性質）。**戰略脈絡**：Trial_v22 🟢 全綠 + v5.5 Phase 3 完整收口（Stage 73+74+75+76+77 連續實證）+ Christ 2026-05-18 拍板 ABCD 候選優先 + Aria 反向建議 🥈 拆 3 Stage / C+D 同 refactor 性質合併 Stage 78。**8 子項**：① 砍 4 agent service class（DocAgentService/RequirementsAgentService/ReleaseAgentService/DesignerAgentService ~1500 行）+ DI 註冊 ② 砍 2 nuget（Anthropic.SDK 5.10.0 + Microsoft.Agents.AI.Anthropic 1.3.0-preview）+ grep verify 0 import 殘留 ③ 砍 archive prompt 文件（v4 4 agent .md）+ 對應 xUnit test ④ CeoAgentService 強制走 v5.5 path / 砍 flag check + v4 fallback 邏輯 ⑤ ButtonCallbackRouter.cs 1211 → ≤500 行拆解（對齊 refactor-sop.md SOP / 拆 3-5 helper / 0 行為改變）⑥ DevAgentService.cs 1056 → ≤500 行拆解（同上 / v5.5 path Cody dispatch service / pure refactor）⑦ xUnit baseline 0 regression（Bot.Tests 113 / Generated 127）⑧ Directory.Build.props v3.67.0 → v3.68.0。**範圍邊界刻意收緊**：❌ TaskGroupService 813 行（Stage 36 已拆 / 不在範圍）/ ❌ A HITL（推 Stage 79）/ ❌ B 動態 replan（推 Stage 80）/ ❌ E Token monitoring（推 WebUI Stage）/ ❌ F-J observation candidates niche 留檔。**設計決策核心**：C+D 同 refactor 性質合併 + 強制走 v5.5 path 砍 v4 fallback（16 次驗證 0 v4 caller 累積足夠）+ 0 行為改變紀律守（pure refactor）+ backwards-compatible 守護 9 層延續。**驗收 9 場景**：A 4 agent 砍後 build success / B 2 nuget 砍後 0 import 殘留 / C CeoAgentService 強制 v5.5 path / D archive prompt 砍 / E ButtonCallbackRouter 拆解 0 行為改變 / F DevAgentService 拆解 0 行為改變 / G xUnit baseline 0 regression / H v5.5 path production 0 regression / I Bot startup 0 exception。**校準錨預期**：一般架構級重構區間 ×0.43-0.60 第 7 資料點候選 / raw 85-130K × 0.50 ≈ 40-65K / Opus 200K + high 推薦 + Opus 1M + Extra high 自升兜底。**cost 預估**：$3-5 per cycle。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Aria gate2 production 0 regression 驗 → 通過後 Stage 79 開（A HITL plan confirmation 閘門）→ Stage 80（B 動態 re-planning）→ WebUI Stage（含 E Token monitoring）→ v5.5 完整收口。 |

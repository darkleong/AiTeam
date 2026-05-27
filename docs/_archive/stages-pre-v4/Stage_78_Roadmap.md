# Stage 78 Roadmap — dead code 清理 + 怪物大檔拆解（v5.5 Phase 4 候選 C+D 合併 / 純 refactor 性質）

> 目標版本：**v3.68.0**（minor — v5.5 Phase 4 Stage 78a：v4 既有 path 整套 dead code 清理 / 純 refactor 性質）
> 狀態：✅ 已完成（2026-05-18）
> 文件版本：v2.0
> 範圍（v1.2 final 10 子項）：① 砍 3 純 v4 class（Rosa/Demi/Release）② 砍 4 雙路徑 class v4 method（Doc/Dev/Reviewer/Qa 留 v5.5 IAgentTool）③ LlmProviderFactory 系列全保留（Petra active）④ 砍 1 dead nuget（Microsoft.Agents.AI.Anthropic）⑤ CeoAgentService v4 fallback 砍 ⑥ PetraSessionRecoveryService flag check 砍 ⑦ 配套 propagation 精準化 ⑧ xUnit 113→104 0 regression ⑨ Directory.Build.props v3.68.0 ⑩ CLAUDE.md production active path 修根因 / **Stage 78b 預留**：ButtonCallbackRouter v4 routing + IAgentExecutor + AgentQueueProcessor + OpsAgent + ProcessAsync 評估
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

## 實作紀錄（Stage 78a Forge 結案第一段 — 2026-05-18）

### 對應 commit

- **主 commit**：[`c6f81d6`](https://github.com/darkleong/AiTeam/commit/c6f81d6) `feat(stage78a): v4 dead code 大清理 v3.68.0 — v5.5 Phase 4 C`（22 files changed / +135 / -4092 / net -3957 行）
- **Follow-up commit**（Aria gate1 🟡 修補）：[`6fcd828`](https://github.com/darkleong/AiTeam/commit/6fcd828) `fix(stage78a): DefaultSkillRegistry 4 Skill baseline 收口 — Aria gate1 🟡 修補`（4 files changed / +31 / -39）

### 實作完成項目（對齊 v1.2 final 10 子項）

| 子項 | 實作位置 | 狀態 |
|---|---|---|
| ① 砍 3 純 v4 class（Rosa/Demi/Release）+ DI 註冊 | `git rm` 3 .cs 檔 + Program.cs DI 砍 6 行（AddScoped + AddKeyedScoped<IAgentExecutor> + IAgentTool）| ✅ |
| ② 砍 4 雙路徑 class v4 method（Doc/Dev/Reviewer/Qa 留 v5.5 IAgentTool）| Write overwrite 4 class 為純 v5.5 IAgentTool（DocAgent 389→25 / DevAgent 1030→27 / ReviewerAgent 540→26 / QaAgent 399→26）+ class declaration `: IAgentTool`（移除 IAgentExecutor）+ Program.cs 砍 4 個 AddKeyedScoped<IAgentExecutor> registration | ✅ |
| ③ LlmProviderFactory 系列全保留（PetraOrchestratorService 3 call sites active）| 0 動 LlmProviderFactory / AnthropicProvider / TokenTrackingProvider / AnthropicClient DI / Anthropic.SDK nuget — Pm/PmRoutingService/PmReviewService/CeoAgentService.ProcessAsync 全保留 | ✅ |
| ④ 砍 1 dead nuget（Microsoft.Agents.AI.Anthropic）| AiTeam.Bot.csproj line 33 PackageReference 砍 + line 22-23 註解段 update | ✅ |
| ⑤ CeoAgentService flag check + v4 fallback else 分支砍 | ProcessWithClaudeCodeAsync method body 改寫為直接 v5.5 path（~200 行 → ~15 行）+ BuildVictoriaPrompt + TryParseActionBlock + VictoriaLock 砍 + ctor inject 砍 4 unused dep（claudeCodeService / configuration / workflowResolver / conversationRepository / memoryRepository / tokenLogService / petraOrchestrator）| ✅ |
| ⑥ PetraSessionRecoveryService.cs:29 flag check 砍 | resolver fetch + flag check 6 行砍 + using AiTeam.Bot.Configuration 砍 + XML doc summary update | ✅ |
| ⑦ 配套 propagation 精準化 | archive prompt（CLAUDE_Demi.md + CLAUDE_Rosa.md 2 個 git rm）+ ClaudeCodeChatClientAdapter capability map 7→4 + dispatch chain switch 7→4 + CLAUDE_Petra.md:60-62 砍 + PetraOrchestratorServiceTests Test6/Test7 InlineData 各 -3 + csproj line 47-51 註解 8→6 Agent + RoutingTypes.cs PreviewIssues field 砍 + ButtonCallbackRouter v4 Requirements path（連帶 spike 揭）+ QaReport.cs 搬獨立檔 | ✅ |
| ⑧ xUnit baseline 0 regression | Bot.Tests 113→104 passed（v5.5 path 既有 case 全綠 / 砍 6 v4 InlineData + ClaudeCodeChatClientAdapterTests 3 case + DefaultSkillRegistry/Stage74 T7 同步 update / Aria gate1 修補後）+ Generated 127/127 passed | ✅ |
| ⑨ Directory.Build.props v3.67.0 → v3.68.0 | 3 行 Version / AssemblyVersion / FileVersion update | ✅ |
| ⑩ CLAUDE.md production active path 真實描述修根因 | v4 既有 4 agent 砍紀錄段 + LLM 段 update（Petra LlmProviderFactory Gemini default / 非 Claude Code CLI / 對齊 Trial_v22 token_logs 真實 verify）| ✅ |

### 關鍵設計決策（為什麼這樣選）

1. **4 雙路徑 class 對稱結構砍 v4 method 留 v5.5 IAgentTool**（vs 拆 helper class 維持 v4 path）— DocAgent/Dev/Reviewer/Qa class declaration 改 `: IAgentTool` 後 ~25-27 行純 v5.5。對齊「修根因 > 補丁」+「對冗餘不容忍」+ Spike v2 揭 v5.5 path 只 3 行 / v4 path 95-99% 是 dead code 真實。
2. **Rosa/Demi/Release 路線 A 砍整套**（含 v5.5 IAgentTool registration）— 對齊 v5.5 6 Talent baseline（Victoria/Petra/Cody/Vera/Quinn/Sage）+ Trial_v6-v22 連續 17 次 Petra LLM 0 拆 Rosa/Demi/Release capability 累積。同步砍 Petra capability map（Adapter 7→4）+ SkillRegistry（6→4 / Aria gate1 揭修補）+ Petra prompt ref 段 + xUnit InlineData。
3. **LlmProviderFactory 系列全保留**（推翻 v1.1 Aria 假設）— Spike v2 揭 PetraOrchestratorService 真實 3 call sites（line 227/407/474 Petra DecideTalents/Skill/SubtaskPlan）/ Pm/PmRoutingService/PmReviewService v5.5 Appeal workflow active / CeoAgentService.ProcessAsync v4 LLM 直接模式 Stage 78b 評估。Anthropic.SDK 保留 = Petra 可改 Provider production flexibility。
4. **Stage 78a 範圍邊界守住 — Stage 78b 預留**（ButtonCallbackRouter v4 routing + IAgentExecutor + AgentQueueProcessor + OpsAgent + ProcessAsync）— 對齊 Christ 拍板路線 C 折衷拆 Stage / 規模 M+ 可控（vs 路線 B 一氣呵成 L/XL Opus 1M 60% safety 上界踩雷風險）。
5. **DocAgent.Name="Sage" 對齊 v5.5 6 Talent baseline**（Spike v2 推翻 v1.1 分類「4 純 v4 + 3 雙路徑」）— DocAgent 真實是 v5.5 Sage Talent 的 IAgentTool 實作 / 砍 class 會 break Sage v5.5 dispatch。修正範圍：4 雙路徑 class（Doc/Dev/Reviewer/Qa）+ 3 純 v4 class（Rosa/Demi/Release）。
6. **CeoAgentService ProcessAsync v4 path 留 Stage 78b 評估**（不擾 Stage 78a refactor 焦點）— BuildSystemPrompt + BuildUserMessageAsync + BuildGitHubContextAsync + TryParseResponse + providerFactory ctor inject 全保留（ProcessAsync 內 4 caller）/ 砍 ProcessWithClaudeCodeAsync 內 v4 Claude Code helper（BuildVictoriaPrompt + TryParseActionBlock + VictoriaLock）連帶砍。
7. **0 行為改變紀律守 — backwards-compatible 守護 9 層延續**：v5 / v5.5 hardcoded fallback / Stage 70-77 全 0 動（v5.5 path Success=true 路徑 0 行為改變）/ SQL `Workflow:UsePetraOrchestratorV5` row 保留（Phase 5+ 評估 / 對齊 backwards-compatible 紀律）。
8. **QaReport class 搬獨立檔**（Forge spike 自決）— Write overwrite QaAgentService 砍 v4 method 連帶砍 QaReport class declaration / 但 QaCoordinationService 真實 caller。修根因 = 搬獨立 file `src/AiTeam.Bot/Agents/QaReport.cs`（vs 留 QaAgentService 內 inline declaration）。

### Aria 4 點 + gate1 🟡 修補（Plan 階段 + 驗收期）

| 議題 | 修補位置 | 詳情 |
|---|---|---|
| Plan v1 → v1.2 spike 兩輪揭真實 | Roadmap + Plan v3 + commit 範圍 | ① Plan v1 假設「pure refactor 拆 helper」→ Spike v1 揭重大議題（DevAgent 99% v4 dead code / Anthropic.SDK 用 / 雙路徑）→ Christ 拍板路線 C 拆 Stage（vs B 一氣呵成）② Plan v1.1 假設「4 純 v4 + 3 雙路徑 + 全砍 LlmProviderFactory」→ Spike v2 揭 2 critical（DocAgent.Name="Sage" / PetraOrchestratorService 3 call sites）→ Christ 拍板 5 議題精準範圍 |
| 🟢 v1.1 nit Plan v3 §G.4 同 commit 紀律 | Plan v3 §G.4 | 砍 class + 砍 Test InlineData 必同 commit / 避免中間 build error（紀律寫進 commit message） |
| 🟢 v1.1 nit Plan v3 §B.2 BlockedOperationException 砍前 grep verify | Plan v3 §B.2 + 實作前 grep | 砍前 grep 4 hit 全 DevAgentService 內 v4 / 0 v5.5 caller 才砍 |
| 🟢 v1.1 nit Plan v3 §E.3 IConfiguration grep verify 具體化 | Plan v3 §E.3 + 實作前 grep | 砍前 grep `configuration[` 在 CeoAgentService.cs 真實 caller（line 200/201/203 v4 fallback 唯一用）/ 砍後 0 caller |
| **🟡 Aria gate1 必修** | follow-up commit `6fcd828` | DefaultSkillRegistry 6 Skill 仍含 ui_design + release_publishing（Petra LLM 偶發 dispatch 會 throw "未知 capability" production silent regression）→ 修補 DefaultSkillRegistry 6→4 + Test 10/14 + Stage74 T7 連帶 fix + DbSeeder Cody description + talent_skills seed 砍 |

### 驗收紀錄

**本機驗證**：
- ✅ `dotnet build AiTeam.slnx` — 0 error / warning 從 103 baseline → 102（砍 v4 path 後些微下降）
- ✅ `dotnet test` — AiTeam.Bot.Tests 104/104 passed + AiTeam.Tests.Generated 127/127 passed
- ✅ grep verify：0 `using Microsoft.Agents.AI.Anthropic` in src/ / 0 v4 class 殘留（Rosa/Demi/Release）/ 0 `GetUsePetraOrchestratorV5Async` caller / 0 v4 dispatch error

**production 自驗 5 層守門全綠**：
- ✅ CI/CD deploy success（gh run 26040092914 + 26042042861 兩階段）
- ✅ Bot startup 0 exception（容器 Up 31s 後）
- ✅ PetraInboxChannel 初始化 + PetraDispatchWorker 3 consumer 啟動
- ✅ 16 個 Workflow flag DB 確認（含 5 v5.5 flag + MaxConcurrentPetra=3 + UsePetraOrchestratorV5=true）
- ✅ 0 `未知 capability` throw / 0 v4 dispatch error / 0 DI lookup failure / talents + talent_skills 真實 DB query 在跑

**Aria gate2 範圍**（Forge 自驗範圍外）：
- 場景 H：v5.5 path production 真實業務驗（1 task chain 跑完 PR 開出 / Trial_v23 baseline 對齊 Trial_v19/v20/v21/v22）

### Mock 覆蓋情況

**N/A — Stage 78a 純 refactor / 0 新 Mock 場景 / 0 業務邏輯改變**。對齊驗收場景 A-G xUnit 覆蓋 + 場景 I production verify 已 cover。

### 踩坑紀錄（Forge spike 揭 5+ 處範圍延伸 / 自決 + Aria 揭 1 處）

1. **DevAgentService 1056 行 v5.5 path 只 3 行**（Spike v1 揭）— Roadmap v1.0 假設「v5.5 path Cody dispatch service / 拆 helper」嚴重誤判 / 99% 是 v4 path dead code。修法：砍 v4 method 留 v5.5 IAgentTool 3 行 = pure refactor 紀律對齊「修根因 > 補丁」。
2. **PetraOrchestratorService 真實 use LlmProviderFactory 3 call sites**（Spike v2 揭）— Roadmap v1.1 假設「全砍 LlmProviderFactory + AnthropicProvider + Anthropic.SDK」會 break v5.5 Petra production 真實 active path。修法：全保留 + 只砍 v4 path caller ctor inject。
3. **DocAgentService.Name="Sage"**（Spike v2 揭）— Roadmap v1.1 把 DocAgent 列入「4 純 v4 class」但 v5.5 Sage Talent 真實依賴。修法：路線 A 留 class 砍 v4 method（同 Dev/Reviewer/Qa pattern）。
4. **ButtonCallbackRouter `GetRequiredService<RequirementsAgentService>()` type reference**（Forge 實作中揭 / 自決連帶砍）— 砍 RequirementsAgentService class 後 type reference build error / Plan v3 §B.5 漏 cover line 532+570。自決連帶砍 ButtonCallbackRouter v4 Requirements path（req_yes branch + HandleExecYesAsync Requirements 分支 + ShowRequirementsPreviewAsync + ExecuteRequirementsFromPreviewAsync + BuildRequirementsPreviewEmbed ~150 行）+ RoutingTypes PendingConfirmation.PreviewIssues field 連帶砍。
5. **QaReport class 在 QaAgentService.cs:390 declare**（Forge 實作中揭 / 自決搬獨立檔）— Write overwrite QaAgentService 後 QaReport class 砍 / 但 QaCoordinationService.cs:70+75 真實 caller / build error。自決搬獨立檔 `src/AiTeam.Bot/Agents/QaReport.cs`（對齊「修根因 > 補丁」+ 紀律延續）。
6. **CeoAgentService BuildGitHubContextAsync 是 BuildUserMessageAsync caller**（Forge 實作中揭 / 修正 Plan v3 範圍）— Plan v3 §E 假設「砍 BuildGitHubContextAsync」/ 實際是 ProcessAsync path 唯一用（不能砍）。修正：只砍 v4 Claude Code path helper（BuildVictoriaPrompt + TryParseActionBlock + VictoriaLock）/ BuildGitHubContextAsync + BuildSystemPrompt + BuildUserMessageAsync + TryParseResponse + providerFactory ctor inject 全保留。
7. **DefaultSkillRegistry 6 Skill 含 ui_design + release_publishing**（Aria gate1 🟡 揭 / follow-up `6fcd828` 修）— Aria + Forge spike v3 雙漏揭 production silent regression risk（Petra LLM 偶發 dispatch ui_design/release_publishing → Adapter 找不到 → throw "未知 capability" crash）。修法：DefaultSkillRegistry 4 Skill baseline + Test 10/14 + Stage74 T7 連帶 fix + DbSeeder Cody description + talent_skills seed 砍。
8. **ClaudeCodeChatClientAdapterTests 3 test fail**（Forge 自驗中揭 / 自修）— ClaudeCodeChatClientAdapter dispatch 砍 3 capability 後對應 T1 InlineData（requirements_extraction / ui_design）+ T6（release_publishing）fail。修法：T1 InlineData 砍 2 行 / T6 整個砍 + class XML doc summary update。

### 0 Migration（純 refactor）

對齊 ef-core.md「每 Stage 確認 0 Migration 漏」紀律 — Stage 78a 0 schema 變化 / 0 `dotnet ef migrations add`。grep verify 0 entity schema 改動 ✓。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-18 | **Stage 78a Forge 結案第一段 — 實作紀錄章節完整 ✅**。對應 2 commit：主 commit `c6f81d6`（22 files / +135/-4092 / net -3957 行）+ follow-up `6fcd828`（Aria gate1 🟡 修補 4 files / +31/-39）。**10 子項全 ✅**：① 砍 3 純 v4 class（Rosa/Demi/Release）含 v5.5 IAgentTool 整套 + Program.cs DI 砍 6 行 ② 砍 4 雙路徑 class v4 method（Doc 389→25 / Dev 1030→27 / Reviewer 540→26 / Qa 399→26 / 留 v5.5 IAgentTool / class declaration 改 `: IAgentTool`）+ Program.cs 砍 4 個 AddKeyedScoped<IAgentExecutor> registration ③ LlmProviderFactory 系列全保留（PetraOrchestratorService 3 call sites active）④ 砍 Microsoft.Agents.AI.Anthropic dead nuget + csproj 註解 update ⑤ CeoAgentService v4 fallback 砍（ProcessWithClaudeCodeAsync ~200→~15 行 + BuildVictoriaPrompt + TryParseActionBlock + VictoriaLock 砍 + ctor 砍 7 unused dep）⑥ PetraSessionRecoveryService flag check 砍 ⑦ 配套 propagation（archive prompt 2 檔 + Adapter capability 7→4 + CLAUDE_Petra.md + xUnit InlineData -6 + csproj 註解 + RoutingTypes PreviewIssues + ButtonCallbackRouter v4 Requirements + QaReport 搬獨立檔）⑧ xUnit Bot.Tests 113→104 / Generated 127/127 passed ⑨ Directory.Build.props v3.67.0 → v3.68.0 ⑩ CLAUDE.md production active path 修根因。**Forge spike 揭 5+ 處範圍延伸**：① DevAgent 99% v4 dead code（Spike v1）② PetraOrchestratorService 3 call sites（Spike v2）③ DocAgent.Name="Sage" v5.5 active（Spike v2）④ ButtonCallbackRouter type reference build error 自決連帶砍 v4 Requirements path ~150 行 ⑤ QaReport class 搬獨立檔（v5.5 path 留 QaCoordinationService caller）⑥ CeoAgentService BuildGitHubContextAsync 是 BuildUserMessageAsync caller 修正範圍 ⑦ ClaudeCodeChatClientAdapterTests 3 test fail Forge 自驗中自修。**Aria gate1 🟡 修補**：DefaultSkillRegistry 6→4 + Test 10/14 + Stage74 T7 + DbSeeder（production silent regression risk 修根因）。**驗收結果**：dotnet build 0 error / dotnet test Bot 104 + Generated 127 全綠 / production 5 層守門全綠（CI/CD success + Bot startup 0 exception + 16 flag SQL + 0 未知 capability throw + talents/talent_skills DB active）/ 場景 H 真實業務驗 Aria gate2 範圍。**0 Migration**（純 refactor / 對齊 ef-core.md 紀律）。**backwards-compatible 守護 9 層延續**（v5.5 path Success=true 路徑 0 行為改變 / SQL flag row 保留）。**Stage 78b 預留範圍**：ButtonCallbackRouter v4 routing 砍 + IAgentExecutor + AgentQueueProcessor + OpsAgent + CeoAgentService.ProcessAsync 評估。**下一步**：Aria 接手結案第二段（CHANGELOG + Future_Feature 同步）+ Aria gate2 production 真實業務驗（Trial_v23 baseline 對齊 v5.5 4 capability dispatch 真實 fire）。 |
| v1.2 | 2026-05-18 | **Forge spike v2 揭 v1.1 分類錯誤 + Aria 第二層反思 + 5 議題精準拍板**。**Forge spike v2 揭 2 critical**：① DocAgentService.Name="Sage" — v5.5 Sage Talent 真實依賴 DocAgent IAgentTool 實作 / v5.5 真實是 **4 雙路徑 class（Cody/Vera/Quinn/Sage）+ 3 純 v4（Rosa/Demi/Release）**/ 不是 v1.1 分類「4 純 v4 + 3 雙路徑」② LlmProviderFactory + AnthropicProvider + TokenTrackingProvider + Anthropic.SDK **不能砍** — PetraOrchestratorService 3 call sites（line 227/407/474 Petra DecideTalents/Skill/SubtaskPlan LLM call）真實 active + Pm/PmRoutingService/PmReviewService + CeoAgentService 都 ctor inject。**Aria 第二層反思**：CLAUDE.md commit `4f862ca` Petra path 描述錯 — 寫「v5.5 6 Talent 全走 Claude Code CLI（Petra 含）」/ Trial_v22 token_logs `PM Model=gemini-2.5-flash` 真實揭 Petra 用 LlmProviderFactory（Gemini default / 可改 Anthropic）/ **不是 Claude Code CLI** / 自省點補強候選「CLAUDE.md production active path 更新時必 grep 真實 caller verify」第 N+2 次累積。**Christ 2026-05-18 拍 5 議題（Aria 對等獨立判斷 0 翻面式接受 + 全同意 Forge 推薦）**：① DocAgent 路線 A 留 class 砍 v4 method 留 v5.5 IAgentTool 3 行（同 Dev/Reviewer/Qa pattern）② LlmProviderFactory + AnthropicProvider + TokenTrackingProvider + Anthropic.SDK + AnthropicClient **全保留**（spike v2 confirm + Petra 可改 Provider production flexibility）③ Rosa/Demi/Release **路線 A 砍整套**含 v5.5 IAgentTool（對齊 v5.5 6 Talent baseline + Trial_v6-v22 連續 17 次 Petra plan 0 拆 Rosa/Demi/Release capability 累積）④ AgentQueueProcessor + IAgentExecutor interface 砍 → **Stage 78b 預留**（連動規模 + 78a 已 M+ 規模可控紀律）⑤ ButtonCallbackRouter v4 routing 砍 → **Stage 78b 預留**（對齊 Aria 訊息既有預留）。**Stage 78a v1.2 精準 10 子項**：① 砍 3 純 v4 class（Rosa/Demi/Release 含 v5.5 IAgentTool 整套 / Program.cs DI 註冊）② 砍 4 雙路徑 class v4 method（Doc/Dev/Reviewer/Qa **留 v5.5 IAgentTool.CreateAgent 3 行** / 砍 v4 IAgentExecutor.ExecuteTaskAsync + ExecuteAsync + RunClaudeCodeAsync + BuildPlanAsync v4 path ~1000+ 行）③ **LlmProviderFactory 系列全保留**（只砍 v4 path 14 caller 內的 4 agent + 雙路徑 v4 method ctor inject 對應段 + Pm/PmRoutingService/PmReviewService/CeoAgentService 保留）④ 砍 1 nuget（**Microsoft.Agents.AI.Anthropic** 1.3.0-preview / src/ 0 import 真實 dead 可直接砍 / **Anthropic.SDK 5.10.0 保留** — AnthropicProvider 真實用 / Petra 可改 Anthropic Provider production flexibility）⑤ CeoAgentService flag check + v4 fallback else 分支砍 ⑥ PetraSessionRecoveryService.cs:29 `GetUsePetraOrchestratorV5Async` 邏輯砍 ⑦ 配套 propagation（精準化）：archive prompt（CLAUDE_Demi.md + CLAUDE_Rosa.md 真實 2 個檔）+ **Petra capability map 7→4**（Forge plan v2 階段 grep verify 真實 capability dispatch map 位置）+ **CLAUDE_Petra.md:59-62 4 agent capability ref 段砍** + **ClaudeCodeChatClientAdapter capability dispatch 3 行砍**（requirements_extraction / ui_design / release_publishing）+ PetraOrchestratorServiceTests.cs:108-111 4 InlineData（Rosa/Demi/Release 砍 / Doc/Dev/Reviewer/Qa 保留）+ csproj 註解段 ⑧ xUnit baseline 0 regression（既有 113 Bot.Tests / 砍 Rosa/Demi/Release 對應 test + Doc/Dev/Reviewer/Qa v4 method test 後 case count 預期下降 / Forge plan v2 verify 具體數字）⑨ Directory.Build.props v3.67.0 → v3.68.0 ⑩ **CLAUDE.md production active path 真實描述修根因**（Aria 順手 update / Victoria flag forward / **Petra LlmProviderFactory（Gemini default / 可改 Anthropic）/ 非 Claude Code CLI** / Cody/Vera/Quinn/Sage Claude Code CLI / 對齊 Trial_v22 token_logs 真實 verify）。**Stage 78b 預留**：ButtonCallbackRouter v4 routing 砍 + IAgentExecutor + AgentQueueProcessor 評估。**規模仍 M+ / Opus 1M + Extra high 推薦 / cost $4-6 per cycle**（v1.1 預估維持 / 範圍精準後 raw 落點仍 200-450K 區間）。Forge 在 v1.2 範圍基礎上重做 Plan v3。 |
| v1.1 | 2026-05-18 | **Forge spike 揭範圍升級 + Aria 重 scope 為 Stage 78a + 預留 Stage 78b**（對齊「修根因 > 補丁」+「對冗餘不容忍」+「Stage 規模可控」精神）。**Forge spike 揭 5 重大議題**：① DevAgentService 1056 行 v5.5 path 只佔 3 行（IAgentTool.CreateAgent / Stage 63B 加）/ 其餘 99% 是 v4 path（BuildPlanAsync 65 行用 LlmProviderFactory / ExecuteAsync + RunClaudeCodeAsync ~950 行 git+CLI / ExecuteTaskAsync 40 行 v4 IAgentExecutor 入口）② ReviewerAgentService + QaAgentService 也是雙路徑（implement IAgentExecutor v4 + IAgentTool v5.5）③ ButtonCallbackRouter 1211 行 v4 path Discord routing 大量 dead code（req_yes/req_no / exec_yes/exec_no + HandleExecYesAsync + ExecuteAgentTaskAsync line 853 含 IAgentExecutor caller / escalate_devplan_skip/abort / v5.5 path 仍 active：kickoff_* / design_* / framework_kickoff_mid_interrupt_* / confirm_yes Stage 68 收尾 / propose_yes / cancel_yes）④ Anthropic.SDK nuget 不是「4 agent 砍後 0 caller」— AnthropicProvider 45 行真實用 Anthropic.SDK.Messaging / LlmProviderFactory 14 caller 大部分 v4 path / Microsoft.Agents.AI.Anthropic 在 src/ 0 import 真可直接砍 ⑤ archive prompt 真實只 2 個檔（CLAUDE_Demi.md + CLAUDE_Rosa.md）/ IAgentExecutor 被 AgentQueueProcessor.cs:190+223 也使用 / PetraSessionRecoveryService.cs:29 也用 `GetUsePetraOrchestratorV5Async` / PetraOrchestratorServiceTests.cs:108-111 4 個 InlineData ref 4 agent class / CLAUDE_Petra.md:59-62 4 agent capability 對應段 / csproj line 23+25+51 註解段。**Aria 反思**：Aria 計劃前對 v4/v5.5 雙路徑 class（DevAgent/ReviewerAgent/QaAgent）沒做雙介面 grep verify（IAgentTool.CreateAgent + IAgentExecutor.ExecuteTaskAsync 真實 method line range）— 對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍紀律 Stage 65/66/67/69 累積延伸到 Stage 78 同類根因第 N+1 次（自省點補強候選）。**Christ 2026-05-18 拍板路線 C 折衷拆 Stage**（vs Forge 推路線 B 一氣呵成 / 路線 B 規模 L/XL raw 250-400K × ratio 1.7-2.5 = 真實 500-1000K Opus 1M 60% safety 上界踩雷 + Aria 78% context 限制）。**Stage 78a 新範圍（本 Stage v1.1 升級）**：dead code 整套砍除 ButtonCallbackRouter — ① 砍 4 agent service class（DocAgent/RequirementsAgent/ReleaseAgent/DesignerAgent）+ Program.cs DI 註冊 ② 砍 3 雙路徑 class v4 method（DevAgent/ReviewerAgent/QaAgent 留 v5.5 IAgentTool.CreateAgent 3 行 / 砍 IAgentExecutor.ExecuteTaskAsync + ExecuteAsync + RunClaudeCodeAsync + BuildPlanAsync v4 path 邏輯 ~1000+ 行）③ 砍 LlmProviderFactory + AnthropicProvider + TokenTrackingProvider v4 segment（Forge plan v2 階段 grep verify 14 caller 全砍）④ 砍 2 nuget（Anthropic.SDK 5.10.0 + Microsoft.Agents.AI.Anthropic 1.3.0-preview）+ csproj 註解段 ⑤ 砍 CeoAgentService flag check + v4 fallback else 分支 ⑥ 砍 PetraSessionRecoveryService.cs:29 `GetUsePetraOrchestratorV5Async` 對應邏輯 ⑦ 砍 archive prompt（CLAUDE_Demi.md + CLAUDE_Rosa.md / 真實 2 個檔）+ CLAUDE_Petra.md:59-62 4 agent capability ref 段 + PetraOrchestratorServiceTests.cs:108-111 4 InlineData + csproj 註解 ⑧ xUnit baseline 0 regression（既有 113 Bot.Tests / 砍 v4 agent test 後 case count 預期下降 / 對齊 Forge plan v2 verify）⑨ Directory.Build.props v3.67.0 → v3.68.0。**Stage 78b 預留範圍**：① 砍 ButtonCallbackRouter v4 path Discord routing（req_*/exec_*/escalate_devplan_* / ~400-600 行自然下降 / 不需再拆 helper）② IAgentExecutor interface 砍評估（若 v5.5 0 caller）③ AgentQueueProcessor 砍評估（若 v4 path 完全停用 / 對應 v4 既有 DB-as-Queue Stage 27 紀律）④ DevAgentService 砍 v4 path 後剩 ~100 行 v5.5 IAgentTool — 不再拆 helper（自然下降）。**規模升級**：raw 預估 120-180K × ratio 1.7-2.5 = 真實 200-450K / Opus 1M + Extra high safety buffer 充裕。**cost 預估升級**：$4-6 per cycle。**Phase 4+ 路徑**：Stage 78a（本 / 升級範圍）→ Stage 78b（ButtonCallbackRouter v4 routing 砍）→ Stage 79（A HITL plan confirmation）→ Stage 80（B 動態 re-planning）→ WebUI Stage（含 E Token monitoring）→ v5.5 完整收口。**Aria gate0 紀律**：Aria 計劃前 grep 紀律補強候選 → workflow_aria.md 第三節 A 第 7 條延伸範圍 #N+1 立檔（對齊 Stage 67 累積 source of truth 紀律工具化規劃前必查清單延伸）。 |
| v1.0 | 2026-05-18 | 規劃書建立 — v3.68.0 / S/M 規模 / v5.5 Phase 4 候選 C+D 合併（純 refactor 性質）。**戰略脈絡**：Trial_v22 🟢 全綠 + v5.5 Phase 3 完整收口（Stage 73+74+75+76+77 連續實證）+ Christ 2026-05-18 拍板 ABCD 候選優先 + Aria 反向建議 🥈 拆 3 Stage / C+D 同 refactor 性質合併 Stage 78。**8 子項**：① 砍 4 agent service class（DocAgentService/RequirementsAgentService/ReleaseAgentService/DesignerAgentService ~1500 行）+ DI 註冊 ② 砍 2 nuget（Anthropic.SDK 5.10.0 + Microsoft.Agents.AI.Anthropic 1.3.0-preview）+ grep verify 0 import 殘留 ③ 砍 archive prompt 文件（v4 4 agent .md）+ 對應 xUnit test ④ CeoAgentService 強制走 v5.5 path / 砍 flag check + v4 fallback 邏輯 ⑤ ButtonCallbackRouter.cs 1211 → ≤500 行拆解（對齊 refactor-sop.md SOP / 拆 3-5 helper / 0 行為改變）⑥ DevAgentService.cs 1056 → ≤500 行拆解（同上 / v5.5 path Cody dispatch service / pure refactor）⑦ xUnit baseline 0 regression（Bot.Tests 113 / Generated 127）⑧ Directory.Build.props v3.67.0 → v3.68.0。**範圍邊界刻意收緊**：❌ TaskGroupService 813 行（Stage 36 已拆 / 不在範圍）/ ❌ A HITL（推 Stage 79）/ ❌ B 動態 replan（推 Stage 80）/ ❌ E Token monitoring（推 WebUI Stage）/ ❌ F-J observation candidates niche 留檔。**設計決策核心**：C+D 同 refactor 性質合併 + 強制走 v5.5 path 砍 v4 fallback（16 次驗證 0 v4 caller 累積足夠）+ 0 行為改變紀律守（pure refactor）+ backwards-compatible 守護 9 層延續。**驗收 9 場景**：A 4 agent 砍後 build success / B 2 nuget 砍後 0 import 殘留 / C CeoAgentService 強制 v5.5 path / D archive prompt 砍 / E ButtonCallbackRouter 拆解 0 行為改變 / F DevAgentService 拆解 0 行為改變 / G xUnit baseline 0 regression / H v5.5 path production 0 regression / I Bot startup 0 exception。**校準錨預期**：一般架構級重構區間 ×0.43-0.60 第 7 資料點候選 / raw 85-130K × 0.50 ≈ 40-65K / Opus 200K + high 推薦 + Opus 1M + Extra high 自升兜底。**cost 預估**：$3-5 per cycle。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Aria gate2 production 0 regression 驗 → 通過後 Stage 79 開（A HITL plan confirmation 閘門）→ Stage 80（B 動態 re-planning）→ WebUI Stage（含 E Token monitoring）→ v5.5 完整收口。 |

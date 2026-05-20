# Future Feature v5.5 — 升級規劃 reference（v5 production 觀察期 → v5.5 啟動候選）

> 建立日期：2026-05-15
> 狀態：規劃 reference（**v5 production 觀察期累積真實需求後評估啟動**）
> 對應主檔：[Future_Feature.md](Future_Feature.md)（FF 一 v5.5 動態架構 / FF 二 v5 PoC 補強清單）
> 對應歷史 Charter：Stage 62 Charter spike 4 deliverable（已歷史化，git history 可查）

## 一、為什麼有 v5.5 這個概念

**v5 動態架構（Stage 63B PoC + Stage 64+65+66 production-ready）已在 2026-05-14 正式上線**，但跟 Stage 62 Charter 規劃比，PoC 階段為了 derisk + 縮範圍簡化掉幾條關鍵設計：

- **PetraSessionRepository.ResumeAsync** 是「mark 既有 session done + 開新 session」（不是真實 resume）
- **BuildSessionContext** fallback hardcode `Agents:Dev:Model` 給 7 worker 共用（沒 per-worker config）
- **Petra 一次決策整 chain** 跑完不重新思考（不是真正動態 orchestrator — Cody 跑完 → Petra 不會看結果再決下一步）
- **Worker 是 one-shot subprocess** 沒持續記憶（每次新 subprocess context 從 0）
- **沒拆解任務機制**（Petra 直接 dispatch worker，沒先把指令拆成多個 subtask）

**v5.5 = 把 PoC 簡化掉的部分補回來 + 對齊業界 2026 best practice + 對齊 Stage 62 Charter 既有規劃**。

不急做 — 先觀察 v5 production 一段時間累積真實需求數據才啟動。

---

## 二、Christ 描述的設計核心（2026-05-14/15 討論累積）

**精神**：Agent 像人類處理事件 — 接到任務後累積思考 / 中間別事打斷不會忘記 / 重啟接著做還記得來龍去脈。

**設計五要素**（2026-05-15 加第 5 條 Talent-Skill separation）：

1. **Petra 拆解指令** — 把一個指令拆成多個 subtask，不是直接整包丟 worker
2. **每個 subtask 呼叫 Agent 執行** — Petra 看 subtask 性質決定哪個 worker
3. **Agent 對任務有持續記憶** — DB 持久記憶（不靠 subprocess context）
4. **記憶分層**（業界共識 hybrid 模式）：
   - **共用層**（task-level）：任務目標 / 各 agent 完成的 deliverable 摘要 / 關鍵決策歷史 — 跨 agent 可讀
   - **私有層**（per-agent role）：Cody 自己的 implementation 思考細節 / Vera 自己的 review checklist 進度 — 只該 agent 自己讀寫
5. **Talent-Skill separation + horizontal scaling**（2026-05-15 Christ 提出 — 對齊業界 OneManCompany / Talent-Skill 主流模式）：
   - **Skill（職務）= code-defined** — 例如「Coding 職務」「Review 職務」「Discord 職務」是 code 寫死的 capability + 對應 tool wiring
   - **Talent（Agent）= DB-driven WebUI 動態加** — 例如「Cody-1」「Cody-2」「Cody-Discord」是可動態 CRUD 的 instance
   - **多對多 assignment**：1 Talent 可擔任 N Skill / 1 Skill 可由 N Talent 擔任
   - **Horizontal scaling**：「Coding 任務太多 → 加 Cody-2 也擔任 Coding skill」= Christ 擴 Talent pool / 不用寫 code
   - **新職務還是要走 Stage**：例如「Discord 職務」是 code-defined / WebUI 不能加新職務（避開「Agent role explosion」反模式 + 業界 6% Copilot pilot 撐到 production 雷區）
   - **對應業界**：Spring AI Skills / OneManCompany Talent / AI Agent Staffing / Agent Identity URI
   - **AiTeam 既有架構半對齊**：capability registry + IAgentTool factory 已是 skill 抽象 / 差「1 Talent N Skill 多對多 mapping」+「動態 Talent CRUD」

**為什麼 hybrid 比純共用 / 純獨立好**：
- 純共用 → memory pool 被各 worker 雜訊寫滿 / Vera 看到 Cody 的 implementation 細節會干擾自己 review judgment
- 純獨立 → Vera review 時不知道「Cody 為什麼這樣寫」 / 跨 agent 脈絡斷掉
- Hybrid → 跨 agent 對齊高層脈絡 + 保各 role 私有思考純淨

**Subprocess 角色釐清**：
- 還需要 subprocess（執行容器：grep / read / edit / commit）
- **不需要 resume**（每次新開 + 從 DB load 記憶 input）
- 對齊「stateless web server + database」pattern — 服務本身 stateless，狀態在 DB
- 避開 Claude Code resume 已知 bug（GitHub #14472 / #43696）+ context drift 65% failure rate

---

## 三、WebSearch 業界 finding（2026-05-15 探索 — balanced view）

### 3.1 持久記憶設計（per-task per-agent）

**好的一面**：
- 對齊 Gartner 2026 預測「40% 企業應用會含 task-specific AI agent」
- 業界共識「shared context layer is the persistent state store across multi-agent pipeline steps」
- 學術框架已驗證：CORAL（self-evolve via shared persistent memory）/ InfiAgent（file-centric state abstraction）

**壞的一面（雷區，妳擔心對了）**：
1. **Context drift 65% enterprise failures** — 約 2% accuracy degradation per reasoning step / 20 步 loop 後失敗率 40%
2. **Context window 是 RAM 不是 storage** — 1M token 也救不了「lost-in-the-middle」 attention dilution
3. **Claude Code resume 真實 bug** — [#14472](https://github.com/anthropics/claude-code/issues/14472) cannot resume when exceeds limit / [#43696](https://github.com/anthropics/claude-code/issues/43696) --resume 不真實還原 context
4. **Agent 35 分鐘退化定律** — 業界觀察單一 agent 跑超過 35 分鐘 attention 衰退
5. **Memory conflict trap** — 新事實跟舊持久記憶衝突 → drift 嚴重

**業界推 4 條 best practice**：
1. **持久記憶存進 DB / vector store**（不存 context window）— Agent query 取相關記憶
2. **Compression 主動做**（不等 attention 退化）— 60-70% context 就 compact / 不等自動觸發
3. **Shared context layer**（central state store）— Agent transient / shared layer persistent
4. **Stateless orchestrator + Stateful memory** — Orchestrator 不長 running，每次從 DB query

### 3.1.5 Talent-Skill separation + horizontal scaling（2026-05-15 Christ 提出後 WebSearch 驗證）

**Christ 直覺命中業界主流方向**：

- **OneManCompany (OMC) 框架**（arXiv 2026 論文「From Skills to Talent: Organising Heterogeneous Agents as a Real-World Company」）：Talent = portable agent identity（角色 + prompt + skill + tool）/ Skill = 可重用 capability / 同 Talent identity 可跨 execution environment
- **AI Agent Staffing**（業界正式術語）：「sourcing, deploying, orchestrating, monitoring, and scaling autonomous agents exactly as you would human talent — treating AI agents as hireable 'employees'」
- **Spring AI Skills**（2026-01）：skills registry 動態 register + asymmetric inspection（routing 看 full skill / agent 看 name + description）
- **Agent Identity URI（agent:// scheme）**：學術論文「decouples agent identity from capability discovery」業界標準化中

**業界證實 horizontal scaling 價值**：
- 「Companies can 'Scale Up' their agent workforce during peak seasons like Black Friday and 'Scale Down' instantly」
- 「Multi-agent architecture decomposes monoliths to enable systems to grow in a reliable, decoupled way, **similar to microservices in software development**」

**Centralized orchestrator 壓制錯誤放大實證**：
- 「Independent multi-agent systems amplified errors by **17.2x**」
- 「Centralized systems with an orchestrator contained amplification to just **4.4x**」 — orchestrator 作為 validation bottleneck
- 對齊 AiTeam Petra orchestrator 設計 ✓

**業界仍有 3 個 critical gaps**（RSAC 2026 揭 — 不是萬能）：
- 跨框架 agent identity 互操作（單 framework 內 OK）
- 動態 skill discovery 安全性（governance）
- Agent 行為 audit / accountability（出事誰負責）

對 AiTeam 影響：個人專案單一 framework — gap 1 不影響 / gap 2-3 對齊 Aria 預檢 + Christ 拍板閘門自然解決。

### 3.2 多 Agent 討論（kickoff / design meeting pattern）

**好的一面**：
- 對「reasoning verification + factual validity」場景：+4-6% accuracy / -30% factual errors
- 最有效配置：**3 agent — 2 opposing + 1 synthesizer**
- 業界 2026 共識 pattern：「**orchestrator + isolated subagents with summary returns**」（Anthropic / Cognition / OpenAI / AutoGen-via-MAF / LangChain 全部收斂）

**壞的一面 — 雷區揭露**：
1. **MIT 數學論文 ⭐⭐⭐**：「without genuinely new information entering the system, adding agents is **guaranteed** to perform no better than a single well-designed agent」— 沒新資訊進來，加更多 agent **數學上保證**沒比單 agent 好
2. **集體幻覺**：「multi-agent collaboration can transform tiny local mistakes into system-wide false consensus」— 一個 agent 小錯誤 → 其他 agent 跟風 → 全系統錯誤共識
3. **Cost 暴漲 15x**：「Research-style orchestration burns roughly 15x the tokens of chat interactions」
4. **「Bag of Agents」反模式 + 14 種失敗模式**：加更多 agent 沒用反而更糟 / 「ambiguous instructions interpreted differently」
5. **協調失敗模式**：Centralized → bottleneck + SPOF / Decentralized → goal drift + deadlock + debugging 困難

### 3.3 對 AiTeam 既有 v4 Kickoff/Design Meeting 真實反思

業界 15x cost 數字**對齊 AiTeam 自己的數據**：

| Trial | 設計 | 真實 cost |
|---|---|---|
| Trial_v6 (v4 完整 Kickoff + Design Meeting) | 多 agent 圍繞議題討論 | **$15.81** |
| Trial_v12 (v5 線性 Cody → Vera) | orchestrator-worker chain | **$2.34** |
| 倍數 | | **6.8x**（接近業界揭的 15x） |

**v4 Kickoff/Design Meeting 部分確實踩雷** — Petra + Demi + Rosa 互相討論時沒新資訊進來 = MIT 數學限制下保證沒比單 agent 好。

**但要公平**：v4 Meeting 跟 **Christ 互動**那段（提案 → 拍板 / 修改 / 拒絕）= 「外部新資訊進來」 ✓ 沒踩 MIT 雷。**真正踩雷的是「Petra 跟 Demi 跟 Rosa 之間 debate」沒 Christ 即時介入的部分**。

---

## 四、v5.5 設計建議（兩層架構）

| 層級 | 設計 | 場景 | 對應業界 pattern |
|---|---|---|---|
| **Day-to-day 任務** | Petra orchestrator + 拆解 + dispatch isolated subagent + DB 持久記憶 | 90% 日常任務（Dashboard 打磨 / bug fix / feature 擴展）| orchestrator + isolated subagents with summary returns |
| **戰略決策** | 3 agent multi-agent debate（2 opposing + 1 synthesizer）| 大型新 feature 設計 / 架構決策 / 客戶專案啟動 — 給 Christ 拍板用 | 多 agent debate（避 MIT 雷靠 Christ 拍板提供新資訊）|

**這樣設計的好處**：
- 避開 v4 既有 Meeting 設計的 MIT 雷（90% 日常任務不再走 multi-agent debate）
- 保留多 agent 討論的真實價值（戰略決策時引導不同視角，但 Christ 拍板提供新資訊避雷）
- Cost 控制：日常任務維持 v5 線性 chain $1-3 / 戰略決策才偶爾燒 multi-agent debate cost
- 對齊「production-ready 漸進 path 優先」精神

---

## 五、跟既有 v5 / FF 的關係

| 既有 | v5.5 升級內容 | 動工關係 |
|---|---|---|
| **v5 PoC**（Stage 63B + 64+65+66 全部）| 保留 — 是 v5.5 的基礎 | 不打掉重做 |
| **FF 三十六 Phase B 全程動態 candidate** | v5.5 是這條的具體化（不只「全程動態」更是「持久記憶 + 拆解任務 + 兩層架構」）| v5.5 = FF 三十六 Phase B 進化版 |
| **FF 六十一補強清單**（11 點剩 8+ 點 Stage 67+ 評估）| 部分被 v5.5 吸收（ResumeAsync 改 / BuildSessionContext per-worker / PetraSessionRepository AppendMessage）| v5.5 啟動時順便收 |
| **Stage 62 Charter**（per-task session 多 row table schema 候選）| v5.5 對齊 Charter 規劃補回 PoC 簡化部分 | v5.5 = 「Charter 的真實落地」 |
| **prompt DB 化候選**（FF Top 5 #5）| 跟 v5.5 持久記憶整合 — prompt 也存 DB / per-task 變體 | 順帶吸收 |
| **v4 既有 Kickoff/Design Meeting**（Stage 25a/b）| 「戰略決策」層級可參考（3 agent / 2 opposing + 1 synthesizer 配置）| 不照搬 — 重新設計避雷 |
| **MeetingService + framework Workflow**（v4 既有 ~16K LoC）| 部分 module 可吸收（HITL routing / Crash Recovery）| 漸進評估砍 / 留 / 重寫 |

---

## 六、開發順序建議（2026-05-15 修正 — Christ 拍板「持續開發 / 不等觀察期」）

> ⚠️ **取消「v5 production 觀察期」概念**（2026-05-15 Christ 拍板）— 持續開發是預設。Aria 之前反覆推「等觀察期才動工」是錯誤紀律 / 修根因為「持續開發迭代」精神。

**從「block 後續 + ROI multiplier + 對齊 Talent-Skill 戰略」三維度排序**，分三個 Phase：

### 🥇 Phase 1：基礎重構（先做這個 block 後續所有升級）

**Step 1 — Spike #6 Agent 收斂評估**（規模 S / 純評估報告 + Christ 拍板）
- 對齊 Talent-Skill 概念：先 settle「哪些是 Skill（code-defined）」+「Agent 怎麼收斂」
- 用既有 Trial_v6-v12 真實 dispatch 數據評估
- 產出：Final Skill list（5-7 個）+ Agent 收斂方案（Rosa/Demi/Maya 砍 vs 合）
- **可立刻開始 — 不需要等任何前置**

**Step 2 — Talent-Skill separation 重構基底**（規模 L / Stage 級工程 / 後續所有 multiplier）
- Skill registry 建立（既有 capability 抽象成 Skill）
- Talent registry DB schema + Migration（既有 7 worker 對應預設 Talent）
- 多對多 assignment table（Talent ↔ Skill）
- GenericAgentService 收斂 7 worker class
- Petra dispatch 改用 Talent pool（看 Skill 找 Talent / 多 Talent 時 round-robin）
- 範圍守緊：**只做架構基底 + Migration / 不開放動態 CRUD（留 Phase 2/3）**

### 🥈 Phase 2：核心動態化（建立在 Phase 1 上）

**Step 3 — Spike #2 DB 持久記憶 schema 設計 + Spike #3 token budget**（規模 M / 合併實作）
- 跟 Talent-Skill 整合：per-Talent 私有層 / per-Task 共用層
- token budget 60-70% 主動 compact 紀律（業界踩坑教訓）

**Step 4 — Spike #1 Petra 拆解指令精準度**（規模 M）
- v5.5 核心新功能：拆解任務 + 派 Talent
- 對齊 Talent-Skill：Petra 拆出 subtask 對應 Skill → 找可用 Talent

**Step 4.5（Stage 71）— v5.5 Phase 2 Step 3+4 production-ready 補強** ✅ **已完成**（v3.61.0 / 2026-05-16）+ **Trial_v17 🟢 全綠驗收通過 ⭐**
- ① ✅ **Petra prompt 升級「拆=真不同 scope」紀律** — Stage 71 修法 / Trial_v17 完整治到根（策略階段論取代機械重複拆 / Cody 從 3 段降到 2 段）
- ② ✅ **Stage 69 memory 寫入 outputLen=0 guard** — Stage 71 修法 / Trial_v17 production 生效（TaskMemory/TalentMemory 全 500 char 非空）
- ③ ✅ **議題 #3 走 Aria 紀律修正** — 自省點 #34 立 + aria-trial-run skill 工具化 / Trial_v17 FinalizeGitAsync 完美無 permission denied
- ④ ✅ **Aria 自省點 #34 立** — `workflow_aria_session_lessons.md` 含 workspace cleanup 紀律
- **Trial_v17 結果**：4 flag SQL=true production active / 業務級成功 / cost -16% / 3x deliverable / 連續 7 Trial 業務級成功 / aria-trial-run skill 首次實踐成功
- **戰略級觀察**（Christ 2026-05-16 點破）：「品質 > 做法」評估框架修正 — Aria 評估 Trial 結果用品質 metric / 預期數字是參考不是死標準 / 對 Step 5 Prompt DB 化「讓 Talent 自主判斷做法 / 我們只定品質標準」精神指引

**Step 5（Stage 72）— Prompt DB 化 + Talent identity 整合** ✅ **已完成 + Trial_v18 🟢 全綠驗收通過** ⭐⭐（v3.62.0 / 2026-05-17 / M 規模 / commit `151e156` + Forge 結案 + Aria gate1 Tier 0+1+2+Tier 3 #11 通過 / Trial_v18 業務 4.7/5 評分 + 19 檔 +1158 + 10 範圍 cover）
- ① ✅ **SkillPrompts + TalentPrompts 兩層 schema**（職位層 per-Skill 共享 + 個性層 per-Talent nullable / partial unique index 守一條 active / 對齊 Stage 69 既有 nullable unique pattern + 對冗餘不容忍精神）
- ② ✅ **EF Migration `Stage72PromptSchema`** + 2 table + 4 index + FK cascade（CI/CD self-hosted runner 自動 apply）
- ③ ✅ **PromptRepository** CRUD + Upsert（新版本 row + 舊 active 切 false）+ Rollback（切版本 + 不刪舊 row 保 audit trail）+ ListVersions
- ④ ✅ **PromptResolver service**（Singleton + 5-min TTL cache + IServiceScopeFactory 對齊 AppSettingsService pattern + flag=false 0 DB query 短路 + catch Exception fallback resilience）
- ⑤ ✅ **DbSeeder 6 個 SkillPrompts seed**（code_implementation / code_review / qa_testing / documentation / ceo_orchestration / petra_orchestration — race-safe per-row + DbUpdateException catch 對齊 Stage 67 EnsureTalentsAsync pattern）
- ⑥ ✅ **v5.5 path 整合**（BuildPetraSystemPrompt 加第 3 optional `baseTemplateOverride` + 新 instance async `BuildPetraSystemPromptForRuntimeAsync` + ClaudeCodeChatClientAdapter 加第 9 optional PromptResolver + DI propagation chain 5 檔完整）
- ⑦ ✅ **Feature flag `Workflow:UseV5PromptDb` default false 守 fallback**（Trial_v18 驗 + Christ 拍板才切 default true）
- ⑧ ✅ **InternalController reload-cache `all` scope** 加 `PromptResolver.InvalidateCache`（議題 2 路線 A 不加新 prompts scope / 對齊範圍刻意收緊）
- ⑨ ✅ **PetraPromptTemplate.cs 常數抽進 AiTeam.Data**（Forge healthy 偏離 plan — 跨專案 reference 工程議題解決 / DbSeeder + BuildPetraSystemPrompt 共用同份 source-of-truth）
- ⑩ ✅ **xUnit 6 新 case**（T46/T47 BuildPetraSystemPrompt override / hardcoded 雙路 + PromptRepositoryTests T1-T4 CRUD/versioning/rollback / 70 pass / 0 fail）
- ⑪ ✅ **G 場景 partial unique index production enforced 額外補驗**（Forge 主動補對齊 Aria gate1 minor 議題 — production PostgreSQL ERROR: duplicate key value violates unique constraint）
- **Backwards-compatible 4 層守護**：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded path（flag=false fallback）/ Stage 70+71 累積 prompt 紀律（動態 skill roster 注入 + hierarchical decomposition + 線性整包邊界）— 全保留

### 🥉 Phase 3：進階機制 + 動態化開放（2026-05-17 Phase 2 完整收口後拍板新順序）

**Phase 1+2 完整收口 ✅**（Stage 67-72 + Trial_v14/v17/v18 全 🟢） — 對齊「Phase 1+2 完整收口才開 Phase 3」紀律 — Phase 3 啟動條件達成。

**Phase 3 順序拍板**（Christ 2026-05-17 拍板 — WebUI 排最後 / 核心功能先做）：

```
Stage 73 prompt content 升級 → Stage 74 真並行 dispatch + 3 agent debate
→ Stage 76 Petra 接收層 queue → Stage 75 WebUI 員工管理（最後做）
→ v5.5 完整收口
```

**Step 7（Stage 73）— prompt content 升級 + Petra TalentPrompt persona seed** ✅ **已完成 + Trial_v19 🟢 全綠驗收通過 ⭐⭐**（v3.63.0 / 2026-05-17 / M+ 規模 / commit `cb47648` + Forge 結案 + Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / 連續 7 Stage 0 follow-up bug fix / Trial_v19 業務評分 5/5 滿分 + PR #379 26 檔 +1456/-88 + 14 範圍 cover + Cost $2.844 / 連續 9 Trial 業務級成功 v10-v19）

- ① ✅ **6 個 SkillPrompt content v1→v2 升級**（走 PromptRepository.UpsertSkillPromptAsync versioning path / 舊 v1 切 IsActive=false 保 audit trail + rollback ready）對齊「品質 > 做法」精神（自省點 #35 延伸）— 從「步驟紀律」升「品質目標 + 業界 best practice + 邊界紅線」/ 保留結構性硬規則（Vera 唯三 Critical + 唯二例外 / Quinn JSON 5 欄位 schema / Cody Dev_plan 結構 / 廣範圍對照表 / Sage CHANGELOG+archive 格式 / Victoria ACTION XML）
- ② ✅ **Petra TalentPrompt persona seed 1 row**（DbSeeder.EnsurePetraTalentPromptAsync helper / race-safe 單 entity detach 對齊 Stage 67 EnsureTalentsAsync pattern）— 4 拍板特質完整文案：謹慎拍板 / 對冗餘不容忍 / 持續迭代（對齊 Stage 76 兩層 queue 配套精神：不取消已派 Worker subtask）/ 對等和互相
- ③ ✅ **PetraPromptTemplate.Template 同步升級**（開頭 v5 → v5.5 + 「派工夥伴」對等精神段 + 「品質目標」3 點 / 3 placeholder `{{capabilityRoster}}` / `{{decompositionSection}}` / `{{outputSection}}` 完整保留 runtime 注入機制）
- ④ ✅ **5 CLAUDE_*.md source 檔同步升級**（v4 既有 DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService / CeoAgentService fallback path 自動拿到新 content / v5.5 path 走 DB / 兩 path 0 分裂 — 對齊「修根因 > 補丁」哲學）
- ⑤ ✅ **DbSeeder.UpgradeSkillPromptsToV2Async helper**（幂等：同 SkillName VersionNumber>=2 active row 已存在則 skip / race-safe 單 entity detach 對齊 Stage 72 EnsureSkillPromptsAsync pattern）+ **EnsurePetraTalentPromptAsync helper** — SeedAsync 接入順序：`EnsureSkillPromptsAsync` → `UpgradeSkillPromptsToV2Async` → `EnsurePetraTalentPromptAsync`
- ⑥ ✅ **PetraPersonaSeed.cs 常數抽取**（新立 / 對齊 PetraPromptTemplate.cs 既有「SeedContent/ 抽常數」前例 symmetric pattern / DbSeeder seed + 未來 xUnit / WebUI rollback 共用 source-of-truth）
- ⑦ ✅ **PetraOrchestratorService.BuildPetraSystemPromptForRuntimeAsync persona prepend**（flag-gated + TalentPrompt 存在才注入 / 不存在 fallback 純 base template — backwards-compatible 守護 0 regression + 新立 ResolvePetraPersonaAsync helper）
- ⑧ ✅ **xUnit Stage73UpgradeTests.cs 5 case**（T1 v1→v2 升級 INLINE path / T2 升級幂等 / T3 Petra persona seed + 4 關鍵字驗 / T4 EnsurePetraTalentPrompt 幂等 / T5 PetraPromptTemplate 升級 marker 驗）+ Test47 line 1047 `v5 動態架構` → `v5.5 動態架構` 同步 update / 75/75 全綠
- **Backwards-compatible 4 層守護**：v4 既有 path（拿升級後 .md content）/ v5 既有 path / v5.5 既有 hardcoded path（feature flag=false fallback）/ Stage 70+71+72 累積 prompt 紀律保留（動態 skill roster + hierarchical decomposition + 線性整包邊界 + 兩層 schema + versioning）— 全保留
- **Aria 二檢 1 Critical + 5 Warning 全修正對齊**（C1 Quinn schema 維持既有不改 / W1 Cody 三段補回 / W2 detach single-entity / W3 fresh DB trade-off Implementation Note 明寫 / W4 Petra persona 第 3 條對齊 Stage 76 / W5 baseline test 對照表 + Test47 update + grep 紀律）
- **production 真實生效**：12 row skill_prompts（6 × 2 version / partial unique index enforced）+ 1 row talent_prompts Petra（907 chars / 4 拍板特質關鍵字全 hit）+ reload-cache wire 真實生效

**Step 8（Stage 74）— per-Skill Model + 真並行 dispatch DAG fan-out + Skill registry metadata 擴展** ✅ **已完成 + Trial_v20 🟡 部分過驗收通過**（v3.64.0 / 2026-05-17 / M+ 規模 / commit `3a66f88` + Forge 結案 + Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / 連續 8 Stage 0 follow-up bug fix / Trial_v20 業務評分 4.5/5 + Stage 74 核心驗收 4 訊號全綠 + PR #380 30 檔 +1765/-99 + 17 範圍 cover + Cost $2.2131 / 連續 10 Trial 業務級成功 v10-v20 / Trial_v20 揭 1 🟡 Quinn fail cause 不明 + 2 🟢 觀察）

> **戰略 question 點破史**（規劃前置 / Christ 2026-05-17 連續兩 question）：① 仲裁是 Agent 還是權責？→ 撤回 Cora Talent 建議（Petra 內建職責）② 權責能不能設 Model？→ Aria grep 實證揭真實架構缺口（per-Skill Model 沒做）→ 修根因紀律「先補完 per-Skill Model 三層架構 / debate 機制延後 Phase 4」

- ① ✅ **TalentSkill schema 擴 Provider + Model nullable + Migration**（Stage74TalentSkillModel / 純 ADD COLUMN nullable / 0 既有 row 影響）
- ② ✅ **TalentSkillModelResolver 三層 fallback chain**（per-Skill > per-Talent > Agents:Dev:Model runtime）+ Singleton + 5-min TTL cache + SemaphoreSlim + IServiceScopeFactory 對齊 Stage 72 PromptResolver pattern
- ③ ✅ **ClaudeCodeChatClientAdapter Model 動態整合**（ctor 加 Guid? talentId + TalentSkillModelResolver? optional / GetResponseAsync 內 resolvedModel 動態 / DispatchAsync 7 case 簽名 propagate / log 加 model field / token_logs LogCliUsageAsync 用 resolvedModel）
- ④ ✅ **DI propagation chain**（BuildAgent + GenericAgentTool + DefaultTalentFactory + Program.cs AddSingleton 全鏈打通 / C# default value 自動套 0 既有 caller 改）
- ⑤ ✅ **DAG fan-out 真並行 dispatch 路線 A**（Christ 議題 1 拍板 — LLM dispatch Task.WhenAll 並行 / DB write 並行段結束後 sequential 對齊「session message order sequential」既有紀律 + 0 race risk）+ 新立 `SubtaskPlanLevelGrouping` Kahn-style BFS helper + 抽 `BuildInputMessagesForSubtaskAsync` + `ProcessSubtaskResultAsync` 2 helper
- ⑥ ✅ **ISkillRegistry SkillDescriptor metadata 擴展**（加 RecommendedModelTier + ReturnTypeDescription / 6 row 對齊 Christ 議題 2 拍板：standard×3（code_implementation / qa_testing / release_publishing）+ strategic×2（code_review / ui_design）+ cost-efficient×1（documentation））— 對齊業界 Agent Skills open standard format 第一步
- ⑦ ✅ **DbSeeder 既有 6 TalentSkill seed Provider=Model=null 維持**（runtime fallback / Christ 後續手動 SQL UPDATE 或 WebUI 設定 / 不強塞推薦值）
- ⑧ ✅ **InternalController reload-cache scope=all 串接** `talentSkillModelResolver.InvalidateCache()`
- ⑨ ✅ **xUnit Stage74TalentSkillModelTests.cs 7 case**（T1-T4 Resolver 三層 fallback + InvalidateCache / T5 Linear chain 0 regression / T6 DAG fan-out 同 level 並行 / T7 6 SkillDescriptor metadata + tier 分類）+ 82/82 全綠
- **Backwards-compatible 5 層守護**：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded fallback / Stage 70 SubtaskPlan schema / Stage 72+73 既有 PromptResolver + Petra persona prepend — 全 0 動
- **Forge spike 揭架構盲點修根因** ⭐：`SubtaskPlan.Linear` factory Stage 70 設計 0 deps 與 Stage 74 DAG fan-out 語意衝突（會被誤為「全並行 level 0」破壞 Trial baseline sequential 紀律）→ Forge 加 sequential edges (1→2, ..., n-1→n) 對齊真實語意 + Test23 baseline update 連動（對齊 Stage 58 結論「Forge spike 揭露架構盲點紀律」第 N 次累積）
- **Aria 二檢 0 Critical + 6 Warning 全 gate1 自驗綠**（W1 runtime fallback 對齊既有 chain / W2 DispatchAsync 7 case propagate / W3 GenericAgentTool propagation 對齊既有 / W4 WorkerDispatchSummary record immutable 安全 / W5 DependencyType.Sequential enum 真實對齊 / W6 SkillDescriptor breaking change baseline test 同步 update）
- **production 真實生效**：talent_skills 6 row Provider=Model=NULL（runtime fallback baseline）+ reload-cache wire 串接驗 200 + Migration apply 成功

**Step 9（Stage 75）— 兩層 queue 配套：Petra 接收層 + Worker 執行層 per-Talent serialization** 🟡 **Trial_v21 部分過**（Stage 75 ✅ Forge 結案 v3.65.0 + Trial_v21 真實業務驗 🟡 揭 1 🔴 設計實作落差 / Christ 2026-05-18 拍板 A2 完整版路線 → Stage 77 預留）

- **Stage 75 結案**（v3.65.0 / 2026-05-17 / M+ 規模 / commit `fd8975f` + Forge 結案 + Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / 17 檔 +1990/-25）
- **Trial_v21 真實業務驗 🟡 部分過**（[Trial_v21_Plan.md](../experiments/Trial_v21_Plan.md) / 業務評分 5/5 滿分 + 連續 11 Trial 業務級成功 + cost $3.75 / cost per file $0.058 新最優 ROI baseline）
  - Layer 1 ✅ 完整生效 / Layer 2 🟡 code path 真實 wire 但 production 0 機會 fire contention（PetraInboxProcessor sequential await）
- **Christ 2026-05-18 拍板 A2 業界推薦完整版路線** — Stage 76 範圍重排「功能性 + 修補類」+ Stage 77 ✅ fire-and-forget A2 完整版（v3.67.0 / 2026-05-18 / commit `c3972f1` + Forge 結案 `e08a885`）+ WebUI + Effort + G Token monitoring 推 Stage 78+

**Step 9 補強 II（Stage 77 / 2026-05-18）— fire-and-forget A2 業界推薦完整版（Channel + multi-consumer + bounded fan-out + graceful shutdown drain）** ✅ **已完成**（v3.67.0 / S/M 規模 / commit `c3972f1` + Forge 結案 `e08a885` + Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / **連續 11 Stage 0 follow-up bug fix** ⭐⭐ / 11 檔 +2071/-127）

**主軸 11 子項全交付**：
- ① ✅ **WorkflowSettings + Resolver 加 `GetMaxConcurrentPetraAsync`**（default 3 / 範圍守 [1,10] / 新 `GetIntInRangeAsync` 私有 helper / 不動既有 5 caller 簽名）
- ② ✅ **PetraInboxChannel 新檔 Singleton**（Bounded Capacity=20 const + FullMode=Wait + SingleWriter=true + SingleReader=false / 對齊業界 7 議題 WebSearch 結論完整）
- ③ ✅ **PetraInboxProcessor 退化 pure producer**（213→107 行 / -50% / push channel + 0 dispatch logic）+ **Aria 議題 3 `ChannelClosedException` explicit catch**（shutdown 場景視為正常 / 不算 polling 異常 log noise）
- ④ ✅ **PetraDispatchWorker 新檔 multi-consumer BackgroundService**（~270 行 / N=3 default × Task.WhenAll + 3 scope per dispatch + dispatch CT 跟 stoppingToken 解耦 + 設計紀律 8 條完整）
- ⑤ ✅ **Stage 76 retry path 3 路分支整套搬遷 0 邏輯改變** — Transient retry / Transient exhausted → DLQ / BusinessRule+Permanent fail-fast / ErrorClassifier / MarkPendingWithRetryAsync caller 算 newAttemptCount 傳入 / Random.Shared / ComputeNextRetryAt / worst-case outer catch fail-fast 全套搬遷 / **T6 整合 invoke `DispatchOneAsync` 真實 regression test cover**（Aria 議題 1 方案 A）
- ⑥ ✅ **StopAsync graceful shutdown drain 4 階段**（`channel.Writer.TryComplete` → `WaitAsync(30 min)` → `_dispatchCts.Cancel` fallback → `base.StopAsync` / 3 場景路徑 — 正常 drain / timeout / host cancel）
- ⑦ ✅ **xUnit 15 case 全綠**（T1-T6 + T7 9 InlineData / Bot.Tests 113/113 + Generated 127/127 / Stage 75+76 既有 baseline 全保留 / 0 regression）
- ⑧ ✅ **Migration `Stage77MaxConcurrentPetraSeed` InsertData 純 seed**（new key in production / 0 conflict risk / idempotency 紀律明示）
- ⑨ ✅ **Directory.Build.props v3.66.0 → v3.67.0**
- ⑩ ✅ **Program.cs DI 註冊 2 行**（`PetraInboxChannel` Singleton + `PetraDispatchWorker` HostedService / `[InternalsVisibleTo]` 既有 csproj 已立 / 0 新增）
- ⑪ ✅ **`PetraOrchestratorService.StartAsync` 標 `virtual` 1 keyword**（Aria 議題 1 拍板 / 0 caller 簽名動 / 0 既有 invoke 行為變化 / 供 xUnit T6 `StubPetraOrchestratorService` test-only subclass override stub）

**Aria 計劃前 WebSearch 7 議題完整 incorporated**（Stage 76 規劃前 4 議題 + Stage 77 規劃前 3 議題）：Fire-and-forget 雷 + BackgroundService+Channel 業界主流 + BoundedChannelOptions config + multi-consumer Task.WhenAll pattern + Anthropic rate limit + Graceful shutdown drain + IServiceScopeFactory CreateAsyncScope per Task

**Aria 二檢 4 點修正全 incorporated**（Plan v1.0 → v1.1）：🟡 議題 1 T6 拍板走方案 A（virtual + stub override 整合驗收）/ 🟢 議題 2 N 啟動讀一次紀律明示（§D 設計紀律 #8）/ 🟢 議題 3 ChannelClosedException explicit catch（§C）/ 🟢 議題 4 Migration InsertData idempotency 紀律明示（§G）

**Aria gate1 Tier 0+1+Tier 2 #3 build 全套通過** — commit 範圍對齊 Plan v1.1 / Stage 76 retry path 搬遷邏輯 line-by-line 等價變換 verify / T6 整合驗收 NextRetryAt 區間 23-37s 對齊 ComputeNextRetryAt 公式（30s ± 20% jitter）/ dotnet build 0 error 103 warning 全 pre-existing

**Forge production 5 層守門全綠**：CI/CD run [26028038857](https://github.com/darkleong/AiTeam/actions/runs/26028038857) success 5m38s + Migration apply + PetraInboxChannel 初始化 log `capacity=20 fullMode=Wait singleWriter=true singleReader=false` + PetraDispatchWorker `consumer count=3 drainTimeout=30min` + consumer=0/1/2 啟動 3 條訊號 + 5 v5.5 flag + MaxConcurrentPetra=3 production active

**0 follow-up bug** — clean delivery 連續第三次（Stage 75 + Stage 76 + Stage 77）/ 自驗期間 0 揭 issue / 0 修根因 / 0 二次 commit

**驗收場景**：A/B/C/D/E/F/G xUnit 15 case 全綠 / H/I 留 Aria gate2 範圍（H v4 path 0 regression / I Trial_v22 真實業務驗多 task 並送 + per-Talent lock contention 真實 fire + retry path 真實 fire 機會 / Stage 75+76+77 三 Stage 整套機制完整生效驗）

**Step 9 補強（Stage 76 / 2026-05-18）— task retry/resume 機制基礎建設 + Trial_v21 修補類** ✅ **已完成**（v3.66.0 / M+ 規模 / commit `051d9df` + Forge 結案 `3dc6eec` + Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / **連續 10 Stage 0 follow-up bug fix** ⭐ / 13 檔 +1857/-18）

**主軸 8 子項全交付**：
- ① ✅ **PetraInbox schema 擴 4 欄 + Migration**（AttemptCount/MaxAttempts/NextRetryAt/DeadAt + 新 index `ix_petra_inbox_status_next_retry` 守 retry backoff timing polling / **Migration MaxAttempts defaultValue 0→3 手動 patch** — Forge spike 揭架構盲點修根因）
- ② ✅ **PetraInboxProcessor retry path 3 路分支**（exponential backoff 30s × 2^(attempt-1) + ±20% jitter / Random.Shared thread-safe — Aria 議題 3 nit）— Transient retry / Transient exhausted → DLQ / BusinessRule+Permanent fail-fast
- ③ ✅ **PetraErrorClassifier 新檔**（Transient / BusinessRule / Permanent 3 分類）— Token 守門 / 月限 / 日限 / quota / rate limit / 已暫停 pattern → BusinessRule fail-fast（對齊 multi-agent mode 4 紀律）/ timeout / HttpException / 5xx / JsonException / TaskCanceledException / SocketException → Transient auto retry
- ④ ✅ **Dead Letter pattern**（Status='dead' + DeadAt + MarkDeadAsync / 不再 pickup / 等人工介入）
- ⑤ ✅ **queuePosition race condition 修法（🥇 簡化顯示）** — CeoAgentService 拿掉 `CountPendingBySourceAsync` race + Reply 改顯示「Task 已接收（inbox=<short_id>）— 請於 Dashboard 操作中心追蹤進度」
- ⑥ ✅ **Dashboard 重跑 failed/dead task 按鈕** — InteractionCenter PetraInbox section 加「動作」column + HandleRequeueAsync + RequeueAsync（守 failed/dead 才允許）+ GetInboxStatusColor 加 dead → Color.Dark
- ⑦ ✅ **xUnit 9 case 全綠**（T1-T9 schema baseline + MarkPendingWithRetry 直接 set + ErrorClassifier 3 路 + MarkDeadAsync + RequeueAsync 5 欄 reset + GetNextPendingAsync 守 backoff + RequeueAsync 反向防呆）+ Bot.Tests 89→98 + Tests.Generated 127/127 全綠 + 0 既有 regression
- ⑧ ✅ Directory.Build.props v3.66.0

**⭐ Forge spike 揭架構盲點修根因**（對齊 Stage 58 結論 N-th 累積 / Trial_v9 + Stage 67 同類根因延伸）：
- **Migration MaxAttempts defaultValue 0→3 手動 patch** — EF auto-generated 不識別 C# property initializer / 預設 `defaultValue: 0` 對既有 row 不安全（永遠 fail-fast）/ 手動 patch 對齊 entity C# initializer

**Aria 二檢 3 點修正全 incorporated**（Plan v1.0 → v1.1）：① MarkPendingWithRetryAsync 簽名加 `int newAttemptCount` caller 算 / method 直接 set（0 雙處 +1 耦合）② CountPendingBySourceAsync XML doc 標記 0 caller + 保留意圖 ③ Random.Shared 替代 instance Random（thread-safe）

**Aria gate1 Tier 0+1+Tier 2 #3 build 全套通過** — commit 範圍對齊 Plan / Aria 二檢 3 點 incorporated 真實 / 9 case assertion 抽樣驗（不只 mock pass）/ dotnet build 0 error 103 warning 全 pre-existing 0 新增

**ef-core.md 升級紀律** — 加「Migration AddColumn defaultValue 對齊 entity C# property initializer」紀律段（Stage 56→59 / Trial_v9 / Stage 67 同類根因紀律延伸）+ 順手修 startup-project `AiTeam.AppHost` → `src/AiTeam.Dashboard` 對齊 CLAUDE.md

**校準錨**：raw 預估 95-140K × 0.50 ≈ 50-70K → **真實 Forge Opus 1M+high 結案完畢 241K** / ratio **×1.72-2.54 落在大規模架構級重構區間下緣**（Stage 72/74/75 baseline ×1.57-3.69）— Aria 推估「一般架構級重構」嚴重低估（vs Christ 選 Opus 1M+high 自己升一級兜底 — **自省點 #37 第 5 次累積實證 Aria raw 偏低紀律持續**）

**production 真實生效**：CI/CD run [25996680029](https://github.com/darkleong/AiTeam/actions/runs/25996680029) success + Migration apply log confirmed + Bot startup OK + petra_inbox 13 欄 + 2 index + MaxAttempts default 3 patch 生效 + 5 v5.5 flag production active + PetraInboxProcessor 新 polling SQL fire confirmed

**驗收場景**：A/B/C/D/E/G xUnit 9 case 全綠 / F/H/I 留 Aria gate2 範圍（F Dashboard 重跑按鈕視覺驗 / H v4 path 0 regression / I Trial_v22 真實業務驗多 task 並送 + retry path 真實 fire）

**0 follow-up bug** — clean delivery 連續第二次（Stage 75 + Stage 76）/ Forge 自驗期間 0 揭 issue / 0 修根因 / 0 二次 commit

> ⚠️ **Stage 編號對齊紀律**（Christ 2026-05-17 拍板「數字順序執行」）— 既有 Step 6 「WebUI Talent CRUD」改對應 Stage 76 / 既有 Step 9 「兩層 queue 配套」改對應 Stage 75 ✅ 已完成。

**主軸 8 子項全交付**：
- ① ✅ **PetraInbox table + Migration**（9 欄位 / index Status+EnqueuedAt FIFO polling 用 / 對齊 Stage 27 既有 agent_queues DB-as-Queue pattern）
- ② ✅ **PetraInboxProcessor BackgroundService**（3 秒 polling + 啟動延遲 10s + Crash Recovery + IServiceScopeFactory.CreateAsyncScope per row 開新 Scoped PetraOrchestratorService → multi-session 並存 OK / 議題 1 拍板實踐）
- ③ ✅ **PetraInboxRepository**（7 method：Enqueue / GetNextPendingAsync / CountPendingBySourceAsync / TryMarkRunningAsync / MarkCompletedAsync / MarkFailedAsync / RecoverStuckRunningAsync / GetRecentAsync）+ DI 移到 AddAiTeamData extension Bot+Dashboard 共用
- ④ ✅ **CeoAgentService v5.5 flag forward path 改寫**（既有直接 await Petra → 寫 PetraInbox row + return ack 含 inbox id 短碼 + 排隊位 N）
- ⑤ ✅ **TalentDispatchLockService**（議題 2 Christ 拍板 🥇 SemaphoreSlim per-Talent — Singleton + ConcurrentDictionary<Guid, SemaphoreSlim> + IDisposable using 自動 release / 對齊 v4 既有 AgentQueueProcessor 8 SemaphoreSlim production-active 紀律 + 0 PG connection pool 雷）
- ⑥ ✅ **PetraOrchestratorService DispatchTalentsAsync per-Talent lock wire**（並行段 + sequential 段全 wire / 鎖範圍只包 LLM dispatch 不包 DB write — 對齊 Stage 74 路線 A 紀律）
- ⑦ ✅ **Dashboard UX status InteractionCenter PetraInbox section**（最近 5 筆 / SignalR live update / 沿用既有 PushInteractionUpdateAsync 不加新 endpoint — 對齊「修根因 > 補丁」哲學）
- ⑧ ✅ **xUnit Stage75InboxQueueTests.cs 7 case 全綠**（T1-T3 PetraInboxRepository / T4-T5 TalentDispatchLockService 同 Talent 序列化 + 不同 Talent 平行 / T6-T7 MarkFailed + CountPendingBySource）+ AiTeam.Bot.Tests 89/89 + Tests.Generated 127/127 全綠

**⭐ Forge spike 揭架構盲點修根因**（對齊 Stage 58 結論第 N+1 次累積）：
- Stage 69 v2.1 既有 `talentNameToIdMap` conditional build（`useV5Memory=true` 才 build）— Stage 75 per-Talent 鎖**永遠需要** Talent Id
- Forge 提到 unconditional build + 三 method 簽名 `IReadOnlyDictionary<string, Guid>?` → 改 non-nullable / 拿掉 `!` null forgive
- 影響範圍：DispatchTalentsAsync + BuildInputMessagesForSubtaskAsync + ProcessSubtaskResultAsync 三 method + memoryEnabled 判斷簡化 / 0 baseline test 破

**Aria gate1 6 Warning 全套 grep verify 通過**（W1 PetraOrchestratorResult.SessionId 真實 / W2 TryMarkRunning trade-off doc 明寫 / W3 DashboardPushService=Singleton 對齊 / W4 PetraV5Dispatched caller 只 check Action / W5 GetRecentAsync limit 5 + EnqueuedAt index FF 候選追蹤 / W6 SemaphoreSlim cleanup FF 候選追蹤）

**Forge healthy 偏離 plan 2 處合理**：
1. §E.2 TaskCenter queue position column 跳過 — Forge spike 揭 Dashboard 0 PetraSession 列表頁存在 / 留 Stage 76 評估
2. PetraInboxRepository DI 移到 DataServiceExtensions — Bot+Dashboard 共用 Repository pattern 對齊既有 TaskRepository / BossInteractionRepository 紀律

**校準錨**：大規模架構級重構新類型第 3 資料點累積（Stage 72/74/75 baseline 367K/336K/**451K** 新上界）/ Aria prep-session 預估倍率 ×1.57-2.26 **收斂趨勢** vs Stage 73/74 ×2.21-3.69（自省點 #37 第 4 次累積實證紀律累積成熟度進化曲線 ⭐）/ Christ 選 Opus 1M + Extra high 連續 3 Stage（73/74/75）真實使用模式校準。

**production 真實生效**：CI/CD deploy success + Migration apply on production（CREATE TABLE petra_inbox + CREATE INDEX ix_petra_inbox_status_enqueued）+ Bot 啟動 0 exception + PetraInboxProcessor polling 真實 fire SELECT FROM petra_inbox（每 3s）+ 5 v5.5 flag production active 維持。

---

**Step 9 既有業界 WebSearch 結論**（已 incorporated 進 Stage 75 Roadmap）：

- **業界 WebSearch 結論**（[7 AI Agent Orchestration Patterns - DEV Community](https://dev.to/dohkoai/7-ai-agent-orchestration-patterns-for-scaling-concurrent-systems-with-production-code-1onc) + [Multi-Agent Orchestration Guide](https://gurusup.com/blog/multi-agent-orchestration-guide)）：業界 70% production Orchestrator-Worker pattern 主流做法 = **「Orchestrator 接收層 queue accept 多 task + 執行層 per-Agent 1 task at a time + UX status 顯示」**（不是「Agent 像人類同時間只 1 task」極端 / 也不是「無限制平行」極端）
- 對齊「Agent 像人類處理事件」精神延伸 — 真實 PM 接收並行 / 執行管理（手上多 task 但同時間深度做 1 個）

**兩層 queue 配套設計**（Christ 2026-05-17 拍板）：

```
Layer 1：Petra 接收層 queue（user → Petra）
- Christ 送 task A → Petra accept + 拆 subtask + 派工
- Christ 送 task B（A 還在跑）→ Petra accept + queue → 拆 subtask + 派工到 Worker 待辦
- Discord / Dashboard 顯示「task A 跑中 / B 排隊 / C 排隊」status
- 不擋 user — Christ 想到立刻送

Layer 2：Worker 執行層 per-Talent 1 task at a time（Petra → 各 Worker）⭐ 核心紀律
- 每個 Talent（Cody / Vera / Quinn / Sage）同時間只執行 1 任務
- Cody 跑 task A 期間 → Petra 派 task B 給 Cody → Cody 待辦 queue 排隊等
- Cody 跑完 task A → 自動接 task B from queue
- Vera / Quinn / Sage 同理
- 對齊「真實 Cody / Vera / Quinn 是個人 dev / 同時間深度做 1 件事 / 不多工分心」精神
```

**per-Talent 鎖紀律（非 per-Skill 鎖）— 對齊 v5.5 Talent-Skill separation 設計**：

- 未來 horizontal scaling 場景：**Cody-1 嚴謹 + Cody-2 創意是兩個人 / 各自跑各自的 task / 平行 OK**
- 同 Cody-1 同時間 1 task / 同 Cody-2 同時間 1 task / 但 Cody-1 跟 Cody-2 之間可平行
- 對齊「真實 dev 各自工作 / 不互相鎖」精神 — 鎖在 Talent 不在 Skill

**範圍**：
- Petra 接收層 queue + DB-as-Queue 對齊（v4 既有 `AgentQueueProcessor` Stage 27 立 — Stage 76 計劃前 grep 驗 v5.5 path 是否走既有 queue 或需新立）
- Worker 待辦 queue（per-Talent / DB 表新立或 reuse 既有）
- UX status 顯示（Discord 訊息 + Dashboard 操作中心狀態列）
- 可選 backpressure mechanism（Petra 太忙時通知 user — 例：queue 累積 5+ task 提醒 Christ）
- xUnit + Trial 真實驗（多 task 並送場景）

**Step 6（Stage 76）— WebUI Talent CRUD**（規模 S-M / 預估 cost $2-4 per cycle）— **最後做 / Phase 3 完整收口** ⭐

> ⚠️ **Stage 編號對齊紀律**（Christ 2026-05-17 拍板「數字順序執行」）— 此 Step 6「WebUI Talent CRUD」對應 Stage 76（Phase 3 最後一步）/ Step 9「兩層 queue 配套」對應 Stage 75 ✅ 已完成。

- Dashboard 加 Talent 管理頁（新增 / 編輯 / 刪除 Talent）
- Skill 多選 assignment（含 Stage 74 加的 Provider/Model 編輯）+ prompt 編輯器（含版本歷史 + rollback UI）
- TalentPrompt persona 編輯（含 Petra persona / 未來其他 Talent persona）
- 順便補：TaskCenter PetraInbox queue position column（Stage 75 留的 §E.2 範圍）
- ⚠️ **Phase 3 最後做**（Christ 2026-05-17 拍板 — 核心功能完成且可運作前 WebUI 排最後 / 避免 UI 提早做被核心功能改變動）

### Phase 3 撤除規劃（既有 Step 8 v4 砍 / 改 Phase 4 候選）

- **v4 既有 module 砍 vs 留** — Phase 3 不做（v4 path 已被 v5.5 path 取代上線 / Trial_v18 後 5 flag production active / v4 path 留 fallback 不擾 production / 待真實業務需求觸發再評估 — 留 Phase 4 候選）

### Phase 4 候選 C — v4 path dead code 整套砍除（Stage 78a / 2026-05-18） ✅ **已完成**

**v3.68.0 / Stage 78a Forge 結案** — commit `c6f81d6` + Aria gate1 🟡 修補 `6fcd828` + Forge 結案第一段 `f5df1aa` / 22+4=26 檔變動 / net -3957 行 / Bot.Tests 113→104 + Generated 127/127 全綠 / **連續 12 Stage 0 follow-up bug fix + clean delivery 連續第四次** ⭐⭐⭐⭐（Stage 75+76+77+78a）

**10 子項精準範圍**（v1.2 final）：
- ① 砍 3 純 v4 class（Rosa/Demi/Release ~1150 行）含 v5.5 IAgentTool 整套 + Program.cs DI
- ② 砍 4 雙路徑 class v4 method ~2900 行（Doc 389→25 / Dev 1030→27 / Reviewer 540→26 / Qa 399→26）/ **留 v5.5 IAgentTool.CreateAgent 3 行**
- ③ **LlmProviderFactory + AnthropicProvider + TokenTrackingProvider + Anthropic.SDK + AnthropicClient 全保留**（PetraOrchestratorService 3 call sites active / Pm 系列 / CeoAgentService.ProcessAsync）
- ④ 砍 1 dead nuget（**Microsoft.Agents.AI.Anthropic** 1.3.0-preview / src/ 0 import 真實 dead）/ **Anthropic.SDK 5.10.0 保留**
- ⑤ CeoAgentService v4 fallback 砍（ProcessWithClaudeCodeAsync ~200→~15 行 + helper 砍）
- ⑥ PetraSessionRecoveryService flag check 砍 / Recovery 永遠 active
- ⑦ 配套 propagation 精準（archive prompt 2 檔 + Adapter capability 7→4 + CLAUDE_Petra.md + xUnit InlineData -6 + csproj + RoutingTypes + ButtonCallbackRouter v4 Requirements 連帶 + QaReport 搬獨立檔）
- ⑧ xUnit Bot.Tests 113→104 / Generated 127/127 全綠
- ⑨ Directory.Build.props v3.67.0 → v3.68.0
- ⑩ **CLAUDE.md production active path 修根因**（Aria 第二層反思 — Petra 真實走 LlmProviderFactory（Gemini default）/ 非 Claude Code CLI / 對齊 Trial_v22 token_logs PM=gemini-2.5-flash 真實驗）

**Forge spike 揭架構盲點紀律第 N+4 次累積**（對齊 Stage 58 結論延續）：
- Spike v1：DevAgent 99% v4 dead code（1056 行 v5.5 path 只佔 3 行）+ ReviewerAgent/QaAgent 雙路徑 + Anthropic.SDK 不能砍 + archive prompt 真實 2 個檔
- Spike v2：DocAgent.Name="Sage" v5.5 active（推翻 v1.1 分類錯誤）+ PetraOrchestratorService 3 call sites + LlmProviderFactory 全保留
- Spike v3（Forge 自決連帶 healthy 偏離）：ButtonCallbackRouter v4 Requirements path build error 連帶砍 ~150 行 + RoutingTypes PreviewIssues + QaReport 搬獨立檔
- Aria gate1 揭：DefaultSkillRegistry 6→4 silent regression risk + Test 10/14 + Stage74 T7 連帶 + DbSeeder Cody description / production safety net 紀律

**Aria 計劃前 grep 紀律補強候選 3 條累積延伸**（對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍 Stage 65/66/67/69/78 累積第 N+1~3 次）：
1. 「規劃 capability 砍時必 grep 雙資料源（Adapter map + SkillRegistry + DbSeeder + xUnit test）對齊」
2. 「v4 vs v5.5 雙路徑 class 規劃前必 grep 雙介面真實 method line range」
3. 「CLAUDE.md production active path 更新時必 grep 真實 caller verify」

→ 留 `/aria-end` session 結束 SOP 統一 update memory/workflow_aria.md

**Christ + Aria 拍板 5 議題**（spike v2 後）：
1. DocAgent 路線 A（留 class 砍 v4 method 留 v5.5 IAgentTool 3 行）
2. LlmProviderFactory + AnthropicProvider + TokenTrackingProvider + Anthropic.SDK 全保留
3. Rosa/Demi/Release 路線 A 砍整套（v5.5 6 Talent baseline + Trial_v6-v22 連續 17 次 Petra plan 0 拆 Rosa/Demi/Release）
4. AgentQueueProcessor + IAgentExecutor → Stage 78b 預留
5. ButtonCallbackRouter v4 routing → Stage 78b 預留

**Stage 78b 預留範圍**：ButtonCallbackRouter v4 routing 砍 + IAgentExecutor + AgentQueueProcessor + OpsAgent + CeoAgentService.ProcessAsync 評估

### Phase 4 候選 C 後續 — Stage 78b ✅（2026-05-19） v3.69.0

**v3.69.0 / Stage 78b Forge 結案** — commit `a7c763a` 單 commit + 結案第一段 `7600551` / 8 檔變動 / net -787 行 / build warning 102 → 59 (-42%) / xUnit 104+127 全綠 / Forge 自驗 9 grep verify 全 PASS / Aria gate1 Tier 0+1 通過 / **連續 13 Stage 0 follow-up bug fix + clean delivery 連續第五次** ⭐⭐⭐⭐⭐（Stage 75+76+77+78a+78b）

**6 子項精準範圍**（v2.0 final / 路線 C 折衷）：
- ① ButtonCallbackRouter v4 routing 砍 — 5 case (exec_yes / escalate_skip / escalate_abort / escalate_devplan_skip / escalate_devplan_abort) + 5 method (HandleExecYesAsync / ExecuteAgentTaskAsync / ResolveWorkflowType / SupersedePriorFailedTasks / GetAgentChannelName) + **路線 C 升級**：HandleConfirmYesAsync v4 body line 393-454 cascade 砍（縮為 ~15 行 / 保 Stage 68 短路 + defensive log+ack fallback）+ BuildEscalateButtons 0 caller cleanup + BuildAgentPlanEmbed 留（ShowDirectAgentConfirmAsync 仍 caller）— 變動 -362 行
- ② OpsAgentService IAgentExecutor 實作砍 — ExecuteTaskAsync 砍 + class declaration `: IAgentExecutor` 移除 + Program.cs:68 AddKeyedSingleton 砍 / **保留 production active**：MonitorDeploymentAsync + MonitorCiCdAsync + RunHealthCheckAsync + AlertAsync + HealthCheckJob Quartz job
- ③ IAgentExecutor.cs 整檔 0 動 — **W3 fallback 紀律守**：AgentQueueProcessor.cs:190 still active + AgentExecutionResult/AgentResultType/AgentDescriptor 12+ file 廣用（PipelineState/Executors/FrameworkAppealRouter/AppealOrchestrationService/QaCoordinationService/MeetingOrchestrationService/FrameworkPipelineRouter/TaskGroupService）→ Stage 78c 預備砍
- ④ SlashCommandRouter `/task` slash command + HandleTaskCommandAsync 整段砍 + ctor 11→6 dep（砍 5 dep：buttonRouter / store / interactionService / appSettings / gitHubSettings）+ 2 unused using 砍 / 保留 9 個其他 slash command
- ⑤ WebhookController HandleIssueOpenedAsync 整段砍 + DispatchEventAsync case issues 砍 + ctor rulesService 砍 / 保留 3 個其他 event handler（PR open / PR sync / push）
- ⑥ CeoAgentService.ProcessAsync + 4 v4 helper（BuildSystemPrompt / BuildUserMessageAsync / BuildGitHubContextAsync / TryParseResponse）+ ctor 8→3 dep 砍（providerFactory / taskRepository / gitHubService / gitHubSettings）+ \_github field + JsonOptions static field + 7 unused using → 縮為純 v5.5 path ~50 行
- ⑦ Directory.Build.props v3.68.0 → v3.69.0

**ctor unused dep cleanup 紀律延伸**（對齊「對冗餘不容忍」+ CS9113 warning cleanup）：CeoAgentService 8→3 dep / SlashCommandRouter 11→6 dep / WebhookController + ButtonCallbackRouter 各砍 rulesService。Build warning 102 → 59 (-43 / -42%)。

**校準錨真實落點**：raw 80-130K × ratio **×3.03-4.93** = 真實 **394K** — 突破 Stage 78a baseline ×2.71-4.07 上界。大規模架構級重構新區間 **×1.57-4.93 新上界 7 資料點累積**。對齊自省點 #37 第 7 次累積實證（連續 Stage 73/74/75/76/77/78a/78b Aria raw 偏低紀律延伸）。

**Aria 自我反思候選**：Plan v2 §Verification 第 1 條 grep verify 規格過嚴沒對齊路線 C 拍板 — Aria gate1 二檢紀律延伸候選（路線 C 留 ShowDirectAgentConfirmAsync → BuildConfirmButtons("exec_yes", "exec_no") 字串仍 fire / Plan v2 規格寫「期望 0 hit」應改「ButtonCallbackRouter v4 routing case 0 hit / 不限 BuildConfirmButtons 字串」）。

### Phase 4 候選 C 最終收口 — Stage 78c ✅（2026-05-19） v3.70.0 ⭐⭐⭐⭐⭐⭐

**v3.70.0 / Stage 78c Forge 結案** — commit `e17e83b` 單 commit + 結案第一段 `6025bd3` / **108 檔變動 net -22690 行** / 0 Migration（議題 9 廢除 / agent_queues 表真實不存在）/ 0 不可逆風險 / build 0 error 0 warning（Stage 78b 59 → 78c **0** ⭐⭐⭐ 突破歷史最低）/ xUnit 100+127 全綠 / Aria gate1 Tier 0+1 通過 / **連續 14 Stage 0 follow-up + clean delivery 連續第六次** ⭐⭐⭐⭐⭐⭐（Stage 75-78c）

**戰略意義**：**v5.5 path single source of truth 完整收口** — v4 path 全部砍乾淨（0 dead code / 0 dead caller / 0 dead routing）/ v5.5 path 唯一 source。

**6 鏈砍範圍**（路線 A 一次過 + 6 議題拍板採納）：
- **鏈 A** v4 Pipeline 核心：WorkflowEngine + Workflows/Pipeline/ 16 files + PipelineRoutingService
- **鏈 B** v4 Meeting+Appeal+HITL+Boss+InteractionProcessor+Proposal+Pm+MockScenarioService：Workflows/Kickoff+Design+Appeal/ + Workflows/Common + Orchestration/Meeting+Appeal+Hitl+Boss + Epic + Qa + Proposal + InteractionProcessor + Pm/ 5 files（議題 8）+ Services/MockScenarioService（議題 7）
- **鏈 C** v4 Queue+Group：TaskGroupService + AgentQueueService + AgentQueueProcessor + AgentQueueControlService（議題 7 配套）/ **0 Migration**（議題 9 廢除）
- **鏈 D** IAgentExecutor 部分砍：IAgentExecutor.cs → AgentDescriptor.cs 重命名縮檔（砍 interface + AgentExecutionResult + AgentResultType / 留 AgentDescriptor record v5.5 active R1 校正）
- **鏈 E** Discord routing 重整：ButtonCallbackRouter 713→~120 行 / CommandHandler 614→~280 行 + DetectImageMediaType 移入 + slashRouter/appSettings ctor dep 砍 / SlashCommandRouter 整檔砍（議題 4）/ PendingConfirmationStore + RoutingTypes 縮為純 v5.5 generic
- **鏈 F** 配套：WebhookController 整檔砍（議題 2）+ Program.cs v4 DI 註冊段砍 + InternalController v4 endpoints 砍 + DashboardBotService v4 methods 砍 + DashboardCeoCommandService TriggerKickoffMidInterruptAsync 砍 + **Dashboard 4 頁 v4 action methods stub snackbar「v4 已砍 / WebUI Stage 重設計」**（議題 1 路線 2 邊界守 + Forge healthy 偏離 plan 對齊「修根因 > 補丁」精神）+ Home.razor 砍 MockScenarioCard + GlobalQueueControlCard reference + GlobalQueueControlCard 整檔砍 + Bot.Tests DesignPromptsTests 整檔砍
- **鏈 G** Directory.Build.props v3.69.0 → v3.70.0

**4 sub-scope 議題 Christ 拍板採納 Forge 推薦**（Plan Mode 階段 / 2026-05-19）：
1. 議題 1 **路線 2**：砍 v4 Pipeline framework service 整套 / 留 TaskGroup + TaskItem + TaskLog + BossInteraction entity + DB tables + 4 Dashboard Pages（PipelineView / PipelineList / TaskCenter / AgentStatusCard）+ DashboardTaskService + BossInteraction Repository + Stage 57 partial unique index / **WebUI Stage 預備重設計範圍**：entity drop + Dashboard UI 重設計為 PetraSession-based
2. 議題 2 **路線 A**：WebhookController.cs 整檔砍 + `/webhook/github` route 廢除
3. 議題 3 **路線 A**：砍 `/mock` Discord SlashCommand / 留 Mock 框架其餘整套
4. 議題 4 **全砍**：SlashCommandRouter.cs 整檔砍 / Discord slash command 完全廢除

**3 新議題 Forge 實作期再揭拍板**（Plan v1 階段 / Forge spike R8 紀律）：
5. 議題 7 **MockScenarioService 整套砍**（路線 A）— Aria 議題 3 拍板 wording「留 Mock 框架其餘整套」漏分層 / Forge spike 揭真實「MockScenarioService 100% v4 framework dispatch / 留它 = v4 framework 不能砍 contradiction」→ Aria 反思 wording 漏「LLM 層 Mock 留 / Orchestration 層 Mock 砍」分層 / 路線 A 砍 Orchestration 層 + 留 LLM 層（MockClaudeCodeService + MockLlmProvider / forge-self-verify skill 真實依賴這層）
6. 議題 8 **Pm/ folder 5 files 整套砍**（Forge 自決）— 100% v4 path（Appeal Executors + QaCoordinationService + FrameworkAppealRouter 用）/ Plan v1 §B 鏈漏列 / Forge 自決納入鏈 B
7. 議題 9 **agent_queues 表真實不存在 Migration 全廢除**（Forge 自決）— Aria Roadmap §鏈 C 認知錯誤 / 真實是 Stage 27a Migration 加 TaskItem.QueueStatus + QueuedAt nullable fields 而非獨立表 / grep verify Migrations folder 0 `agent_queues` hit confirmed / Plan v1 §G.4 Migration + pg_dump 備份 + R9 Down() 紀律全廢除 / **scope 從含 Migration 不可逆 → 純 code refactor 0 不可逆風險**

**Forge 自診修 5 處 healthy 偏離 plan**（對齊 Stage 58 結論 N+ 次累積 + 「修根因 > 補丁」+「對冗餘不容忍」紀律延續）：
- AgentQueueControlService 80 行整檔砍（議題 7 配套）
- DashboardBotService v4 methods 砍（議題 7+5 配套）
- DashboardCeoCommandService TriggerKickoffMidInterruptAsync 砍（議題 5 配套）
- Dashboard 4 頁 v4 action methods stub snackbar 設計（議題 1 路線 2 邊界守 ⭐ — 留 4 頁 + 顯示歷史 v4 task data + v4 action 砍 stub 提示用戶 WebUI Stage 重設計 / 對齊「修根因 > 補丁」精神 / vs 直接砍 method 留 broken UI / vs 砍 4 頁擴 scope）
- Home.razor + GlobalQueueControlCard + Bot.Tests DesignPromptsTests 配套砍

**校準錨真實落點（自省點 #37 第 8 次累積實證）**：
- raw 250-400K × ratio **×1.30-2.08 mid ×1.69** = 真實 **521K**
- vs Aria prep-session ultrathink 預估 600-800K **偏低 -13% 到 -35%**
- 對齊大規模架構級重構新區間 **×1.57-4.93 中段下緣 8 資料點累積**
- Opus 1M + Extra high safety 52%（充裕）
- 揭新發現：「**ultrathink 預估上界偏高 / 真實落點往中段下緣**」反向校準紀律候選

**Aria 自我反思候選 5 條**（結案紀錄 + /aria-end 統一處理）：
1. 議題 3 wording 漏分層（議題拍板必分層思考紀律候選）
2. agent_queues 表規劃前認知錯誤（規劃前 Migration 真實狀態必 grep verify Migrations folder + Entities.cs + AppDbContext 紀律 → workflow_aria.md 第三節 A 第 7 條延伸範圍 #10 立檔）
3. Plan v1 沒明列 Forge 7 處 healthy 偏離（Plan v1 應 explicitly 列 Dashboard 配套 method stub + Home.razor reference 等 propagation 候選紀律）
4. Aria ultrathink 預估 vs Forge 真實落點對齊度（真實落點偏低 -13% / ultrathink 上界偏高紀律候選）
5. Roadmap §C 場景 C AgentDescriptor 規格錯誤（Forge R1 校正 / 對齊「Forge plan 階段 spike 必 grep verify 真實狀態」紀律延續）

**Phase 4 後續路徑**：Stage 78a ✅ → 78b ✅ → 78c ✅ → **79 預留（v5.5 image flow / M）** → 80 預留（HITL plan confirmation / M）→ 81 預留（動態 re-planning / L）→ WebUI Stage 預留（K WebUI Talent CRUD + Effort + E Token monitoring + v4 entity drop + Dashboard 重設計）→ v5.5 完整收口

### Phase 4 候選 — Stage 79 v5.5 image flow 補完（規模 M / 新加 2026-05-19）

**戰略脈絡**：Christ 2026-05-19 戰略 question 揭 v5.5 path **Dashboard 附圖 Petra 看不到 gap** — Stage 75 切 PetraInbox 設計時遺漏 image flow（PetraInbox.Enqueue 簽名只接 userInput / images 在 ProcessWithClaudeCodeAsync 簽名有但 method body 0 用 / Petra LLM call 0 image / Worker dispatch 0 image）。Trial_v6-v22 連續 17 次純文字 prompt 沒踩到 / 但 Stage 80 HITL plan confirmation 需要視覺 context（UI bug 截圖 / mockup）— image flow 是 HITL 業務功能前置依賴。

**WebSearch 業界紀律拍板**：[Latenode LangGraph 2026 + Google Agent Bake-Off] 主流觀點「**only give each agent the tools it actually needs / pass images only to the worker agents that need them**」— 不無腦 propagate / 由 supervisor（Petra）依 task 性質條件性決定 + multimodality 是 native feature（非「先 LLM 轉文字描述再傳」反 pattern）。

**設計拍板**：Petra SubtaskPlan 加 `needsImageContext: bool` per subtask flag — Petra 拆 task 時依視覺 context 需求決定每個 worker 是否需要 image / Worker dispatch chain（ClaudeCodeChatClientAdapter）依 flag 條件性傳遞。

**8 子項**：
1. PetraInbox schema 擴 Images 欄位（JSON 序列化 List<ImageAttachment> / 或 separate petra_inbox_images 表）+ Migration
2. PetraInboxRepository.Enqueue 簽名加 images 參數
3. PetraInboxProcessor pickup row 時 deserialize images + 傳給 PetraDispatchWorker
4. PetraOrchestratorService.StartAsync chain 加 images 參數傳遞
5. Petra LLM call（PetraOrchestratorService.cs 3 call sites 用 LlmProviderFactory.Create("PM").CompleteAsync）含 images
6. **SubtaskPlan schema 擴 `needsImageContext` flag** + Petra prompt 教學如何判斷 + Parser 解析
7. **Worker dispatch ClaudeCodeChatClientAdapter 條件性傳 image + Claude Code CLI 圖片支援 spike**（規劃前 Aria WebSearch + Forge plan 階段驗證 — `--image` flag / workspace 檔案 reference / base64 inline 哪種？）
8. xUnit test 補（needsImageContext 條件性 dispatch 驗證）

**規模預估**：raw ~130-180K（從 M 升 M+ — 加條件性 worker propagation + Claude Code CLI 圖片支援 spike）× ratio ×1.5-2.5 = 真實 ~200-450K / Opus 1M + Extra high safety 25-45% / cost $3-5。

**Phase 4 後續路徑（Christ 2026-05-19 拍板修正）**：Stage 78a ✅ → 78b ✅ → 78c 預留（v4 Pipeline 整套砍 / L）→ **79 預留（v5.5 image flow 補完 / M）** → 80 預留（A HITL plan confirmation / M）→ 81 預留（B 動態 re-planning / L）→ WebUI Stage 預留（K + E Token monitoring）→ v5.5 完整收口

### Phase 4 候選 — Stage 79 ✅ v5.5 image flow 補完（2026-05-19） v3.71.0 ⭐⭐⭐⭐⭐

**v3.71.0 / Stage 79 Forge 結案** — commit `0f9176e` 單 commit + 結案第一段 `a61652b` / 20 檔變動 net +1722 行 / 0 follow-up bug（Wave 2 build verify 3 errors Forge 自抓自修）/ Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / **連續 15 Stage 0 follow-up + clean delivery 連續第七次** ⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79）

**戰略意義**：**v5.5 image flow business-ready** — Stage 75 切 PetraInbox 設計遺漏 image flow 修根因 + Petra 真實看圖 + 條件性 worker propagation（NeedsImageContext flag）+ Dashboard 限制三層守 + GeminiProvider multimodal native 支援 + Trial_v23 啟動條件達成。

**11 鏈 13 子項 + 7 議題拍板（4 既定 + 3 Plan 階段 Forge spike 揭）**：
- 4 既定議題（Roadmap v1.1+v1.2）：路線 A 只做 Image / 半抽象 Attachments jsonb + Type discriminator / 限制紀律 per task 5 張 per file 5 MB / Dashboard MudFileUpload + API verify + Repository 三層守
- 3 Forge spike 揭（Plan v1 階段 escalate）：P1 補 GeminiProvider multimodal（對齊 AnthropicProvider pattern + Christ 偏好 Gemini 低 cost）/ P2 純字串簽名 Repository 收 attachmentsJson（0 cross-project type 依賴 / 對齊半抽象 + 最小抽象紀律）/ P3 Dashboard 4 層 validate 100% 既有 hardcoded → AppSetting 動態 + API/Repository 後備層補強

**WebSearch 3 議題業界紀律 incorporated**：
- Claude Code CLI 圖片支援機制（無 `--image` flag / 真實機制 prompt 內 file path reference / workspace 檔案 reference / 0 base64 inline）
- IChatClient multimodal 支援（ChatMessage 含 image AIContent）
- Multi-agent image propagation 業界 best practice「pass images only to worker agents that need them」（Latenode LangGraph 2026 + Google Agent Bake-Off）

**校準錨真實落點（自省點 #37 第 9 次累積實證）**：
- raw 130-180K × ratio **×2.49-3.45 mid ×2.97** = 真實 **449K**
- Aria 預估範圍上界精準對齊（200-450K 預估 / 真實踩上界 / 精準 baseline）
- 對齊大規模架構級重構新區間 **×1.30-4.93 中段下緣 9 資料點累積**
- 揭新場景變因：「**M+ 新業務功能 + 1 輪 spike + 半抽象設計 + 議題 P1 補 GeminiProvider scope 略擴 +30%**」ratio 中段

**Forge healthy 偏離 plan 紀律延續第 N+2 次累積**（對齊 Stage 58 結論延續）：
- PetraOrchestratorService.cs:344 v5 path 簡化紀律（v5 path 0 SubtaskPlan / 第一個 worker 一律附 image / 後續純 text）
- ClaudeCodeChatClientAdapter.WriteImageContentsToWorkspaceAsync try-catch defensive 比 Plan v1 嚴謹
- dc.MediaType nullable check 用 pattern match `is { } mediaType` vs Plan v1 `?.StartsWith` 嚴謹

**Aria 自我反思候選 1 條**（留 /aria-end 統一升級）：議題 P3 揭規劃前對 Dashboard 既有 validate 邏輯認知不全（從 0 建 vs 既有 hardcoded → AppSetting 對齊）— workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #N+1

### Phase 4 候選 — Trial_v23 ✅ Stage 78a+78b+78c+79 連 4 Stage 真實業務驗（2026-05-20） 🟡 部分過

**真實結果** — Trial_v23 v2.0 結案（[Trial_v23_Plan.md](../experiments/Trial_v23_Plan.md)）：
- **Case A 純文字 baseline ✅ 5/5** — PR #386 / 38 檔 / 12 min 03s / cost $1.74 / 17/17 Dashboard 雙路錯誤通知 cover ⭐
- **Case B 附圖 image flow business-ready ✅⭐⭐⭐ 5/5** — PR #387 / 7 min 29s / cost $1.12 / Cody+Vera 真實看圖 + GeminiProvider Stage 79 multimodal dispatch images=1 fire + workspace `.tmp/images/6b1e8de2_001.png` 真實寫 owner=appuser + 3 UI Issue 全 cover（MudCardContent padding / ParseDescription helper / MudCardActions 間距）
- **CP6 v4 path 0 regression ✅⭐** — SQL tasks 0 new row + log 0 v4 class fire（TaskGroupService / AgentQueueProcessor / Pipeline Executor / ButtonCallbackRouter v4 routing / MockScenario 全 0）
- **總 cost $2.86 對齊 Plan $2-3 ✓**
- **連續 14 Trial 業務級成功延續**（v10-v23）

**揭 1 🔴 + 3 🟡 議題**：
- 🔴 **#1 Stage 79 QuickCommandCard EF Core DbContext concurrency bug** — `QuickCommandCard.OnInitializedAsync():line 42` 跟 `Home.OnInitializedAsync():line 48-50` 並行用同 Scoped DbContext → 首頁 Circuit terminated / 阻 Christ 走 Dashboard upload Case B real path（Aria curl path 替代）→ Stage 80 補強候選（合進 HITL / 或 Stage 79.1 hotfix 改 DashboardAppSettingsService 用 `IDbContextFactory.CreateDbContext()` pattern）
- 🟡 **#2 v5.5 path 仍開「確認派工/取消」按鈕但 Bot 已自動 dispatch** — Stage 78c v4 Pipeline 砍除後 BossInteraction confirm UI 邏輯遺留 / 按鈕無實質功能 → Stage 80 收口候選
- 🟡 **#3 Case A Vera 揭 PipelineView 5 handler 缺 catch** — 同類根因對齊 #1（Blazor exception path 防呆紀律不全）→ Stage 80 候選（合進 #1）
- 🟡 **#4 Case B Vera 揭 InteractionCard 2 設計 issue** — 硬寫 rgba 深色主題視覺失效 + ParseDescription string pattern 耦合 → follow-up 候選

**戰略意義**：
- **v5.5 image flow business-ready 閘門通過** ✅ — Stage 79 設計完整真實 fire（Petra 看圖 + 條件性 worker propagation + GeminiProvider multimodal + workspace 寫圖）
- **v4 path 全砍 production 0 regression 完整驗** ✅ — Stage 78a/b/c 三輪累積 net -27434 行對 production 0 影響
- **路線 D 戰略確認** — v5.5 path single source of truth 完整收口 + image flow business-ready = v5 動態架構 + v5.5 baseline 全量上線里程碑
- **Aria 自跑 9-step 模板第 12 次實踐 + curl path 替代 broken Dashboard UI 路徑靈活適應**

**Aria 自我反思候選 +1 條**（留 /aria-end 統一升級）：Stage 79 規劃漏掃 Blazor InteractiveServer + Scoped DbContext concurrency 紀律（OnInitializedAsync 改 DB query 必 grep verify `IDbContextFactory.CreateDbContext()` pattern）— workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #N+2（連同既有 #11 Dashboard UI validate 邏輯 → 同 Stage 不同盲點兩條根因累積）

**Phase 4 後續路徑（Stage 81 後修正）**：Stage 78a ✅ → 78b ✅ → 78c ✅ → 79 ✅ → **Trial_v23 ✅** → **80 ✅** → **Trial_v24 ✅** → **81 ✅** → Trial_v25（驗動態 replan + 4 decision routing + 3 議題 production）→ WebUI Stage 預留（v4 entity drop + Dashboard 重設計）→ v5.5 完整收口

### Phase 4 候選 — Stage 81 ✅ B 動態 re-planning + HITL retry gate 配套 + Trial_v24 3 議題收口（2026-05-20） v3.73.0 ⭐⭐⭐⭐⭐⭐

**v3.73.0 / Stage 81 Forge 結案** — commit `92eadb9` 單 commit + 結案第一段 `c48f0f0` / 20 檔變動 net +2607 -37 行 / 0 follow-up bug / Aria gate1 Tier 0+1+2+Tier 3 #11 通過（規模 L+ 升 tier）/ **連續 17 Stage 0 follow-up + clean delivery 連續第九次** ⭐⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79-80-81）

**戰略意義**：**Stage 81 達成 Christ 親口要的動態 re-planning 業務功能** — Petra 看 subtask result 邊判斷邊決定下一步 / 對齊業界 LangGraph cycles + max iterations + cost cap + checkpoint replay 業界紀律完整內化 + HITL retry gate 重用 Stage 80 plan_confirm infra + Trial_v24 3 議題全收口 + Trial_v25 啟動條件達成 + Phase 4 接近完整收口前最後動態功能 Stage。

**動態 replan core（5 子項 / LangGraph cycles 業界紀律對齊）**：
- §1 `DetectReplanTrigger` 純規則 Regex（Vera `"critical":[{...}]` + Quinn `"status":"failed"` 對齊 CLAUDE_Vera.md L113 + CLAUDE_Quinn.md L75）
- §2 `InvokePetraReplanAsync` W8 紀律 — Petra LLM 只回 retry instruction（不回新 plan 結構）+ 3 正例 + 1 反例 few-shot + `TryParseReplanDecision` markdown fence 容錯
- §3 `PetraOrchestratorResult.Replanning` + `Cancelled` 工廠（議題 #3 命名語意收口）
- §4 `ResumeFromReplanConfirmationAsync` 4 decision routing + 3 子 method（approve = 同 subtask 重 dispatch with retry instruction prepend ⭐⭐⭐ / edit/respond = redecide 開新卡 loop / reject = 接受原 output 繼續下個 subtask 不 cancel session）+ `ContinueChainFromSubtaskAsync` 取 plan_confirm ContextJson SoT + `DispatchRemainingSubtasksAsync` simplified sequential + `BuildSummariesFromSessionMessagesAsync`
- §5 `PetraSession.ReplanIteration` + `SessionCostUsd` column + Repository 3 helper + `CheckReplanTriggerAfterDispatchAsync` 6 step + `HandleCapReachedAsync` 開既有 intervention 卡 + 寫 task_memory `decision/replan-cap-reached` + `sessionRepo.CancelAsync`（場景 G/H）

**HITL gate 配套（2 子項 / 純複用 Stage 80 infra）**：
- §6 `PlanConfirmationProcessor` filter `IN ('plan_confirm', 'replan_confirm')` + dispatch 分支 + `MapActionToDecision` 8 action mapping
- §7 `InteractionService.ReplanConfirmActionsJson` 4 button + MockMode auto-approve + `InteractionCard.razor` replan_confirm UI（觸發原因 MudAlert + 進度 + retry instruction MudPaper + 原 output 預覽 MudExpansionPanel）+ `InteractionCenter.razor.cs` 4 mapping（Icon=Refresh + Color=Warning + replan_reject=warning vs plan_reject=error）

**Trial_v24 議題收口（3 子項）**：
- 🟡 #1 ✅ Quinn outputLen=0 修根因 — `ClaudeCodeChatClientAdapter` qa_testing capability prepend `BuildQaSummaryEnforceSection`（純 adapter 層 / 不污染 CLAUDE_Quinn.md）
- 🟡 #2 ✅ Petra NeedsImageContext few-shot 補 2 反例
- 🟡 #3+#8 ✅ `Cancelled` 工廠 + Stage 80 既有 `ResumeRejectAsync` 改用 Cancelled + log dispatched=0 自動對齊

**Aria 二檢補強**：
- 補強 #A `GetUseDynamicReplanningAsync` 雙 flag 綁定 + warning log（plan_confirm ContextJson SoT 紀律）
- 補強 #B `IX_token_logs_PetraSessionId` non-unique non-partial index（高頻 SumAsync 性能保險）

**Aria 自審紀律生效驗證** ⭐：Roadmap v1.0 → v1.1 自審揭 2 🔴 + 6 🟡 全收口（過去 Stage 79/80 自審 0-1 議題 / Stage 81 自審 8 議題 = aria-review-plan skill 自我自審紀律首次大規模實踐生效）

**4 踩坑紀錄 / 3 know-how 升級**：
① EF stale snapshot 第 2 次（Stage 80 同類延伸）→ ef-core.md 加「Migration body 空驗證紀律」
② C# raw string `{{...}}` brace escape 雷 → csharp.md 加「Raw string interpolation 用 `$$"""`」
③ DispatchTalentsAsync 簽名擴 ctx → Test29/30 reflection fixture 修正（既有 Stage 67/75 紀律）
④ Edit tool 絕對路徑走 main repo 而非 worktree → forge-self-verify skill 加「worktree workflow Edit tool 絕對路徑紀律」

**Aria 自我反思候選**（留 /aria-end 統一升級）：
- 「Aria 寫完 Stage Roadmap 後系統性 6 維度 ultrathink 自審紀律」— 對齊自省點 #16 二次檢查紀律延伸到 Aria 自己寫的 Roadmap / Stage 81 首次大規模實踐生效

### Phase 4 候選 — Trial_v24 ✅ Stage 80 HITL plan_confirm 業務體驗 + 🔴 #1 hotfix verify（2026-05-20） 🟢 全綠 ⭐⭐⭐⭐⭐

**真實結果** — Trial_v24 v1.0 結案（[Trial_v24_Plan.md](../experiments/Trial_v24_Plan.md)）：
- **🟢 全綠** — Stage 80 HITL 業務體驗 + 🔴 #1 hotfix 業務級成功
- **連續 15 Trial 業務級成功延續**（v10-v24 / infinite loop pattern 打破連續第 15 次）⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐

**7 場景驗收**（場景 C plan_approve 跳過 / 對齊「Forge self-verify 已驗 + 場景 A baseline 對齊 + 節省 ~$2-3」拍板）：

| 場景 | 結果 | 重點 |
|---|---|---|
| G 🔴 #1 hotfix verify | ✅⭐⭐ | Dashboard 5+ navigation 切換 / 0 Circuit terminated / 0 second operation / Console pattern 0 hit / 對比 Trial_v23 中段 Christ 截圖 Circuit 完整修根因 |
| A flag=false baseline | ✅ | 0 plan_confirm fire / chain Cody→Vera→Quinn / PR #388 / Quinn outputLen=0 🟡 議題 |
| B flag=true 開卡 | ✅ | log marker + SystemNotes + ContextJson + session=paused |
| H UI render | ✅ | SystemNotes 區塊 + 4 button + SubtaskPlan + 主題變數深色友善（Christ 視覺感受拍板）|
| D plan_edit redecide | ✅ | contentLen=28 / redecide subtasks=2 |
| F plan_respond redecide | ✅⭐⭐⭐ | **Petra 真實理解「mobile responsive」加 ui_design + qa_testing 響應式驗 / subtasks=2 → 4 / 業界 LangGraph interrupt 真實效果驗證** |
| E plan_reject | ✅ | task_memory.decision/plan-rejected + session=cancelled / UX 二次確認 modal 守紀律 |

**Cost $2.48 真實對齊預估 $1-3 ✓ / 餘額 $9.96 → $7.48**（token_logs 加總 $2.52 vs Christ 真實 $2.48 / Anthropic billing rounding 差 $0.04）

**揭 3 🟡 議題**：
- 🟡 #1 **Quinn outputLen=0 baseline 漂移** — 場景 A Quinn chain dispatch 100% 通過 + token_logs 12553 tokens 真實輸出 + cost $0.66 / 但 ClaudeCodeChatClientAdapter 端 outputLen=0 / PR body Quinn 段 0 內容 — Stage 81+ follow-up 評估候選
- 🟡 #2 **Petra `NeedsImageContext` 對純文字 prompt 誤判 true** — 場景 B/D/F plan_confirm 卡 SubtaskPlan render「附圖」chip 但 attachments=0 imageCount=0 — Stage 79+80 邊界議題 / Stage 81+ follow-up Petra prompt few-shot 補強候選
- 🟡 #3 minor **`PetraOrchestratorResult.DispatchedWorkerCount` reject path 命名語意不對齊** — reject 真實 0 chain dispatch 但 log dispatched=4（雜用 plan.Subtasks.Count）— Stage 81+ follow-up minor cleanup

**戰略意義**：
- **Stage 80 HITL plan_confirm 業務體驗實證** ⭐⭐⭐⭐⭐ — 4 decision pattern 全部真實 fire（approve Forge self-verify 已驗 + edit/respond/reject Aria 真實業務驗）/ 業界 LangGraph interrupt 紀律真實內化 AiTeam
- **🔴 #1 DbContext concurrency hotfix production 真實生效** ✅ — Dashboard 首頁 + 多 navigation 0 Circuit（Trial_v23 直接踩 production bug 修根因實證 ⭐⭐）
- **HITL 4 decision pattern 真實影響 Petra decision 業務級實證** ⭐⭐⭐ — 場景 F「mobile responsive」→ Petra subtasks=2 → 4 加響應式設計（業界 LangGraph interrupt 真實效果驗證）
- **Aria Chrome MCP 自跑突破** — 「Christ 只動嘴」精神更進一步突破 / Trial_v24 Christ 真實操作量 0（除拍板開跑 + 看 screenshot 拍視覺 + 拍 PR 業務）/ 對齊 aria-trial-run skill 第 7 次實踐成熟 + Chrome MCP 業界擴展

**Aria 自我反思候選 2 條**（留 /aria-end 統一升級）：
- workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #N+1 — 規劃 Trial 場景時必評估「Chrome MCP / computer-use 工具能力可否替代 Christ 操作」/ 對齊自省點 #20「工具使用慣性」延伸（避免 Trial 規劃漏掃工具能力）
- 自省點 #36「對等和互相」紀律第 N 次真實實踐 — Christ 戰略 question「妳有 Claude for Chrome 能力」點破 Aria 工具盲點 / Aria 承認漏掉 + 真實再評估給雙面定見

### Phase 4 候選 — Stage 80 ✅ A HITL plan confirmation 閘門 + Trial_v23 4 議題收口（2026-05-20） v3.72.0 ⭐⭐⭐⭐⭐

**v3.72.0 / Stage 80 Forge 結案** — commit `958ad6e` 單 commit + 結案第一段 `1653c12` / 22 檔變動 net +2014 行 / 0 follow-up bug / Aria gate1 Tier 0+1+2+Tier 3 #11 通過（升 1 級補 Forge 跳過 Plan Mode 漏層）/ **連續 16 Stage 0 follow-up + clean delivery 連續第八次** ⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79-80）

**戰略意義**：**Stage 80 達成 Christ 親口要的 HITL plan confirmation 業務功能** — Petra 拆完 plan 後 flag-gated 開 BossInteraction plan_confirm 卡 + 4 decision pattern（approve / edit / reject / respond）+ session pause/resume — 業界 LangGraph interrupt + 4 decision pattern 業界紀律內化到 AiTeam v5.5 orchestrator + Trial_v24 啟動條件達成 + Phase 4 接近完整收口。

**HITL 主體（5 子項）**：
- §1 BossInteraction.InteractionType `plan_confirm` + InteractionCenter switch Icon/Color/Label + PlanConfirmActionsJson 4 button 常數
- §2 HITL pause point [PetraOrchestratorService.cs:111-124](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L111) `DecideTalentsWithPlanAsync` 完成後 + `DispatchTalentsAsync` 前 + `WaitForPlanConfirmationAsync` 開卡 + `sessionRepo.PauseAsync`
- §3 4 decision resume routing — `ResumeFromPlanConfirmationAsync` + 3 子 method（Approve / EditOrRespond / Reject）+ `DispatchAndFinalizeAsync` helper（approve path 從 ContextJson 重建 SubtaskPlan + talentPicks 不重 call Petra LLM cost 節省 ⭐）
- §4 InteractionCard.razor plan_confirm UI — SubtaskPlan render（subtask 列表 + dependency 圖 + 附圖 chip + talent picks）+ 4 button + ContextJson parse 失敗 fallback alert 0 crash
- §5 **新建 `PlanConfirmationProcessor` BackgroundService（Forge spike 偏離 plan）** — Roadmap §5 寫「InteractionProcessor 路由擴」對齊 Stage 78c 已砍真實 → 新建 BackgroundService 達成同等設計意圖（3s polling responded plan_confirm + ProcessedByBot 原子標 + IServiceScope per row + 對齊 PetraInboxProcessor 紀律）

**Trial_v23 議題收口（4 子項）**：
- 🔴 #1 DashboardAppSettingsService ctor → `IDbContextFactory<AppDbContext>` + 3 method `await using var db = await dbFactory.CreateDbContextAsync(ct)` + Program.cs `AddDbContextFactory<AppDbContext>` 並存註冊（修 Blazor InteractiveServer 並行 OnInit 撞 Scoped DbContext 根因）
- 🟡 #2 CommandHandler v5.5 path 改 `EmptyActionsJson`（0 按鈕純 ack 卡）+ description 純 Christ 任務 + systemNotes Victoria reply
- 🟡 #3 **不修紀錄** — PipelineView 5 handler 真實是 Stage 78c 砍後 placeholder code 0 production risk（Aria gate1 反查 production code 真實狀態紀律候選）
- 🟡 #4 BossInteraction.SystemNotes? + Migration AddColumn nullable + AppSetting `Workflow:UseHITLPlanConfirmation` seed default false + InteractionCard 主題變數深色友善

**AppSetting flag default false 守 v5.5 baseline 0 regression** — Trial_v24 開時切 true → 結案切回 false（對齊 aria-trial-summary skill flag 切回紀律）

**Forge self-verify 全 8 場景 PASS**（A flag=false 0 regression + B flag=true 開卡 + C plan_approve + D plan_edit + E plan_reject + F plan_respond + G 🔴 #1 hotfix verify 5 並行 Home GET 0 second operation + H 🟡 #4 SystemNotes 後端 SoT）/ Christ 視覺驗收項目留 Trial_v24

**4 踩坑紀錄 / 2 know-how 升級**：
① `dotnet ef migrations add --no-build` stale DLL 雷 → 升級 `docs/conventions/ef-core.md` 加紀律
② Roadmap §5 漏掃 Stage 78c 砍範圍 InteractionProcessor（Aria 規劃 grep 紀律候選 / 留 /aria-end workflow_aria.md 第三節 A 第 7 條延伸範圍 #12 升級）
③ Internal API JSON body lowercase `text` 同類根因第四次 → 升級 `forge-self-verify skill` 加 JSON body 欄位名紀律（既有「Internal API port + auth + endpoint」紀律延伸 cover JSON body）
④ MockMode auto-approve fallback `ack` 對 plan_confirm 無效（不阻塞 / Future_Feature 候選）

**Aria 自我反思候選 2 條**（留 /aria-end 統一升級）：
- 規劃 reference 既有 class / processor 時必 grep verify 真實存在（特別 Stage 78a/b/c 砍範圍對 source of truth 紀律的影響）— workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 #12
- Vera review 並非絕對正確 / Aria gate1 Tier 0 反查 Vera/Cody review 對 production code 真實狀態紀律候選

### Phase 3 cost 總預估（對齊自省點 #38 雙因子）

| Stage | 規模 | Forge session | Trial AiTeam | per cycle |
|---|---|---|---|---|
| 73 prompt 升級 + Petra persona | M+ | $1-1.5 | $2-3 | **$3-4.5** |
| 74 並行 dispatch + 3 agent debate | M | $1.5-2 | $1.5-3 | **$3-5** |
| 76 Petra 接收層 queue | S-M | $0.5-1 | $1.5-3 | **$2-4** |
| 75 WebUI Talent CRUD（最後）| S-M | $0.5-1 | $1.5-3 | **$2-4** |
| **Phase 3 完整收口 total** | | | | **$10-17.5** |

### 跨 Phase 紀律

- **每 Phase 結束跑 Trial 驗證**（對齊 Trial_v6-v12 模式 — 真實任務驗 + Aria 9-step 模板）
- **每個 Stage 加完做 Aria gate1 + Forge 自驗 + 真實任務 Trial**
- **Phase 1+2 完成前不開放 WebUI Talent CRUD**（避「Agent role explosion」反模式）

### 規模 + 時程預估

| Phase | 工作 | 規模合計 |
|---|---|---|
| Phase 1 | Spike #6 + Talent-Skill 重構基底 | S + L = ~2-3 Stage |
| Phase 2 | DB 持久記憶 + 拆解 + Prompt DB 化 | M + M + M = ~3 Stage |
| Phase 3 | WebUI Talent CRUD + 3 agent debate + v4 砍 vs 留 | S-M + M + M = ~2-3 Stage |
| **合計** | | **7-9 Stage**（對齊 v5 PoC 二次架構升級規模 M-L）|

### Phase 1 Step 1 Baseline 拍板（2026-05-15 Christ + Aria 對齊業界 finding 拍板）

> ⚠️ **這是 baseline 拍板**（Phase 1 Step 1 Spike #6 真實啟動時可能微調，但結構已 settle）。對齊業界 finding：minimum 2-3 個 high-impact skill 起步 / 5-7 個 sweet spot（MetaGPT 5 / ChatDev 7 / gstack 6+15 skills）。

#### Final 6 個 Skill（從 7 → 6 / 砍 14%）

**核心必備 4 個**（Trial_v10-v12 100% 用）：
1. **`code_implementation`** — 寫 code
2. **`code_review`** — review code（找 bug / 對齊 coding style / production safety check）
3. **`qa_testing`** — 測試 + 寫 test case + Playwright 真實點擊
4. **`documentation`** — 寫文件 + README + commit message + PR body

**進階場景觸發 2 個**：
5. **`ui_design`** — UI/UX 設計（特定 UI 任務 Petra dispatch）
6. **`release_publishing`** — Release notes + version bump + 部署協調

**❌ 砍 1 個**：
- **`requirements_extraction`** → 合進 Petra orchestrator（Petra 既然要拆解任務，自然就要釐清需求，不需要獨立 skill）

#### 預設 6 個 Talent（從 10 → 6 / 砍 40%）

| Default Talent | Skills 擔任 | 來自 |
|---|---|---|
| **Victoria** | (orchestrator — 接 Christ 指令) | 對齊 既有 Victoria |
| **Petra** | (orchestrator — 拆解任務 / 派 Talent / 吸收 requirements_extraction) | 對齊 既有 Petra |
| **Cody** | `code_implementation`（主）+ `ui_design` + `release_publishing`（兼） | 對齊 既有 Cody |
| **Vera** | `code_review`（主） | 對齊 既有 Vera |
| **Quinn** | `qa_testing`（主） | 對齊 既有 Quinn |
| **Sage** | `documentation`（主） | 對齊 既有 Sage |

#### ❌ 砍掉 / 合併的 4 個既有 Agent

| 既有 Agent | 處理 | 理由 |
|---|---|---|
| **Rosa** (Requirements) | **砍** | requirements_extraction skill 砍 / Petra orchestrator 自然吸收 |
| **Demi** (UI Design) | **合併進 Cody** | ui_design skill 由 Cody 預設擔任 / Trial_v10-v12 真實 0 Demi 介入 PR 仍出 UI 改動 |
| **Rena** (Release) | **合併進 Cody** | release_publishing skill 由 Cody 兼任 / git auto deploy + self-hosted runner 已 cover 大部分真實 release 工作 |
| **Maya** (Ops) | **砍** | git auto deploy + self-hosted runner 已 cover 真實 ops 工作 / 無獨立工作 |

#### Horizontal Scaling 場景（v5.5 Phase 3 動態 Talent CRUD 開放後）

| 場景 | 動作 | 結果 |
|---|---|---|
| Coding 任務太多 | WebUI 加 **Cody-2** + assign code_implementation skill | 兩個 Cody 並行接 coding |
| Review 跟不上 | 加 **Vera-2** | 兩個 Vera 同時 review 不同 PR |
| 需要 UI 專家 | 加 **Demi-1** 專門擔任 ui_design skill（不兼其他） | 替代「Cody 兼 UI」變成「Demi 專做 UI」 |
| 多專案並行 | 加 **Cody-ProjectA / Cody-ProjectB**（對齊既有 ProjectId 設計） | per-Project Cody 不混淆 |

#### 未來 Skill 候選（不急 / Phase 3 後評估）

業界揭值得考慮但 AiTeam 短期不急：
- **`commit_messages`** — 業界共識高 ROI（gstack 有獨立 skill）/ AiTeam 目前 Cody 自己寫已 OK
- **`planning_review`** — gstack 有 / MetaGPT Architect 部分對應 / Aria 已部分擔任
- **`security_audit`** — gstack Security Officer / OWASP + STRIDE / 客戶專案才需要
- **`design_consultation`** — gstack 有 / 對應大型新 feature 設計

#### Baseline 達成的關鍵指標

✅ 砍 40% Agent（10 → 6） / 砍 14% Skill（7 → 6） / 保留所有真實 dispatch 過的 capability / 業界 sweet spot 5-7 個對齊 / 多對多 mapping 預備 horizontal scaling

---

## 七、不確定性 + 待驗證

1. **Petra 拆解指令的精準度** — 業界沒明確指引「LLM 拆解 user instruction 成 subtask」的 best practice，要 spike 驗證
2. **DB 持久記憶 schema 真實設計** — 共用層 + 私有層的 table 結構 / per-task scope 隔離 / summarization 觸發策略 都需要 design
3. **每次 load 進 subprocess 的記憶 token budget** — 業界推「不超過 context 10%」但 AiTeam 真實場景需要 spike
4. **戰略決策層的 3 agent 配置** — 哪 2 個 opposing perspective + 哪 1 個 synthesizer 對 AiTeam 場景最有效（Petra vs Demi vs Rosa? 或新 agent role?）
5. **v4 既有 module 的「砍 vs 留 vs 吸收」拍板** — Crash Recovery / HITL routing / sub-task 整合等 ~16K LoC 的去留
6. **Agent 數量是否該收斂**（2026-05-15 Christ 自省 question — WebSearch 業界對比後揭）— AiTeam 10 個 agent vs 業界主流（MetaGPT 5 核心 / ChatDev 7 / gstack 6+23 skills）。**Trial_v10-v12 真實只 fire 3 個 agent**（Victoria forward + Petra orchestrator + Cody/Vera workers），剩下 7 個 dormant。**業界共識「Match topology to task shape, not org chart」+「Most teams over-engineer toward multi-agent topologies before single-agent reaches its quality ceiling」** — AiTeam 10 個 agent 是 v4 hierarchical static 設計遺留，可能踩了「照組織模仿」反模式。**真實值得反思 3 個 agent**：① Rosa（Requirements）— Petra LLM 自己拆需求可能更直接 ② Demi（UI Design）— Cody 自己讀 spec 設計 UI 也行（Trial_v10-v12 PR 真實做 UI 改動 0 Demi 介入） ③ Maya（Ops）— git auto deploy + self-hosted runner 已 cover 真實 ops 工作。**gstack 反例值得學**：用「skills（slash commands）取代 agent 細分」— 1 個 Claude Code + N skills > N 個獨立 agent（省 agent 切換 overhead + 共享 context）— 對齊 v5.5「Petra 拆任務 → 派給對的 worker」其實「不一定是不同 agent」可以是「Cody 用不同 mode/skill」。**短期紀律**（v5 production 觀察期）維持 10 個 agent codebase 不動觀察真實 dispatch 分布；**中期評估**對 dispatch 率 0% 持續 1-2 個月的 agent 評估 3 條 path：砍（合進 Petra LLM 範圍）/ 合併（Rosa+Demi 合「Discovery Agent」）/ 保留 reserve（客戶專案才用）。對齊 v5.5 升級時 skill 路線可能取代部分 agent 分工。

---

## 八、Sources（業界 finding reference）

### 持久記憶 / Context drift
- [State of AI Agent Memory 2026 — mem0](https://mem0.ai/blog/state-of-ai-agent-memory-2026)
- [Context Window Behaves Like RAM, Not Storage — mem0](https://mem0.ai/blog/context-window-is-ram-not-storage-why-most-agent-failures-happen-how-to-fix-them-in-2026)
- [Context Drift Causes 65% of Enterprise AI Agent Failures — MemU](https://memu.pro/blog/ai-context-drift-enterprise-agent-memory)
- [Long-Running AI Agents and Task Decomposition 2026 — Zylos Research](https://zylos.ai/research/2026-01-16-long-running-ai-agents)

### Claude Code resume 真實 bug
- [Cannot resume session when context exceeds limit — claude-code #14472](https://github.com/anthropics/claude-code/issues/14472)
- [--continue and --resume do not restore prior conversation context — claude-code #43696](https://github.com/anthropics/claude-code/issues/43696)
- [Claude Code Context Window Management — Felo Search](https://felo.ai/blog/claude-code-context-window/)

### 多 Agent 討論（debate / meeting pattern）
- [Patterns for Democratic Multi-Agent AI: Debate-Based Consensus — Medium](https://medium.com/@edoardo.schepis/patterns-for-democratic-multi-agent-ai-debate-based-consensus-part-1-8ef80557ff8a)
- [Why Your Multi-Agent System is Failing: 17x Error Trap of "Bag of Agents" — Towards Data Science](https://towardsdatascience.com/why-your-multi-agent-system-is-failing-escaping-the-17x-error-trap-of-the-bag-of-agents/)
- [The Multi-Agent Reckoning: Two Papers That Challenge Everything You Assumed — Medium](https://medium.com/@Micheal-Lanham/the-multi-agent-reckoning-two-papers-that-challenge-everything-you-assumed-7a8834b49b8d)
- [Why Do Multi-Agent LLM Systems Fail? — arXiv 2503.13657](https://arxiv.org/html/2503.13657v1)
- [Multi-Agent in Production in 2026: What Actually Survived — Medium](https://medium.com/@Micheal-Lanham/multi-agent-in-production-in-2026-what-actually-survived-f86de8bb1cd1)
- [Improving Factuality and Reasoning with Multiagent Debate — Composable Models Lab](https://composable-models.github.io/llm_debate/)
- [Adaptive heterogeneous multi-agent debate — Springer Nature](https://link.springer.com/article/10.1007/s44443-025-00353-3)
- [Multi-Agent Debate — AutoGen](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html)
- [Agent Architecture Patterns: 2026 Taxonomy Guide — DigitalApplied](https://www.digitalapplied.com/blog/agent-architecture-patterns-taxonomy-2026)

### Microsoft Agent Framework + Multi-provider
- [microsoft/agent-framework — GitHub](https://github.com/microsoft/agent-framework)
- [Microsoft Agent Framework 1.0 - .NET Blog](https://devblogs.microsoft.com/dotnet/introducing-microsoft-agent-framework-preview/)
- [AI Providers - Microsoft Agent Framework Docs](https://www.mintlify.com/microsoft/agent-framework/dotnet/providers)
- [Build AI Agents with Claude Agent SDK and Microsoft Agent Framework](https://devblogs.microsoft.com/semantic-kernel/build-ai-agents-with-claude-agent-sdk-and-microsoft-agent-framework/)

### Multi-provider CLI alternatives
- [Best Claude Code Alternatives 2026 — Verdent](https://www.verdent.ai/guides/claude-code-alternatives-2026)
- [Claude Code vs Codex CLI vs Gemini CLI 2026 Benchmark — CodeAnt](https://www.codeant.ai/blogs/claude-code-cli-vs-codex-cli-vs-gemini-cli-best-ai-cli-tool-for-developers-in-2025)
- [google-gemini/gemini-cli — GitHub](https://github.com/google-gemini/gemini-cli)
- [openai/codex — GitHub](https://github.com/openai/codex)
- [OpenCode + ACP — DeepWiki](https://deepwiki.com/sst/opencode/7.4-agent-client-protocol-(acp))
- [10 Claude Code Alternatives 2026 — DigitalOcean](https://www.digitalocean.com/resources/articles/claude-code-alternatives)

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-15 | 文件建立 — Christ + Aria 2026-05-14/15 跨 2 日討論累積 → v5.5 升級規劃 reference 文件化。**核心精神**：Agent 像人類處理事件 — Petra 拆解指令 + 每 subtask 呼叫 agent + agent 對任務有持續記憶（DB 持久記憶 hybrid 共用 + 私有層）+ 兩層架構（day-to-day orchestrator-worker / 戰略決策 multi-agent debate）。**WebSearch 業界 balanced view**：好的一面（Gartner 2026 預測 40% 企業應用 / 業界 2026 共識 orchestrator + isolated subagents with summary returns / 多 agent debate 對 reasoning verification 場景 +4-6% accuracy）+ 壞的一面雷區（context drift 65% failure / Claude Code resume bug / 35 分鐘退化定律 / MIT 數學論文揭多 agent debate 沒新資訊保證無用 / 集體幻覺 / 15x cost 對齊 AiTeam Trial_v6 vs v12 6.8x 倍數）。**啟動條件**：v5 production 觀察期累積真實需求數據（多輪 Cody-Vera loop / 客戶專案 / 單 agent 35 分鐘退化臨界 / 某 agent 0 work 復發 / API quota 連續失敗任一達成才動工）。**規模預估**：對齊 v5 PoC 二次架構升級 3-5 Stage ~1-2 個月工程（M-L 級投資）。**對齊既有 FF**：FF 三十六 Phase B 進化版 / FF 六十一部分吸收 / Stage 62 Charter 真實落地 / prompt DB 化候選順帶整合 / v4 既有 Meeting 不照搬只參考。 |

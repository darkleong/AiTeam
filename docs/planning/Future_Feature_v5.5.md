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

**Step 8（Stage 74）— 真並行 dispatch + 戰略決策層 3 agent debate**（規模 M / 預估 cost $3-5 per cycle）

- 真並行 dispatch — 同 dependency level subtask 平行跑（既有 SubtaskPlan Independent dependency 純設計 surface → 真實 dispatch 並行）
- 戰略決策層 3 agent debate — 從 Talent pool 選 3 個（2 opposing + 1 synthesizer）/ 觸發場景：大型新 feature 設計 / 客戶專案啟動
- 設計依賴 Stage 73 升級後 prompt content（debate 機制用「升級後品質目標 prompt」設計才精準）
- xUnit + Trial_v20 真實驗

**Step 9（Stage 76）— 兩層 queue 配套：Petra 接收層 + Worker 執行層 per-Agent 1 task at a time**（規模 S-M / 預估 cost $2-4 per cycle）⭐ **2026-05-17 WebSearch 揭 + Christ 拍板新立**

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

**Step 6（Stage 75）— WebUI Talent CRUD**（規模 S-M / 預估 cost $2-4 per cycle）— **最後做**

- Dashboard 加 Talent 管理頁（新增 / 編輯 / 刪除 Talent）
- Skill 多選 assignment + prompt 編輯器（含版本歷史 + rollback UI）
- ⚠️ **Phase 3 最後做**（Christ 2026-05-17 拍板 — 核心功能完成且可運作前 WebUI 排最後 / 避免 UI 提早做被核心功能改變動）

### Phase 3 撤除規劃（既有 Step 8 v4 砍 / 改 Phase 4 候選）

- **v4 既有 module 砍 vs 留** — Phase 3 不做（v4 path 已被 v5.5 path 取代上線 / Trial_v18 後 5 flag production active / v4 path 留 fallback 不擾 production / 待真實業務需求觸發再評估 — 留 Phase 4 候選）

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

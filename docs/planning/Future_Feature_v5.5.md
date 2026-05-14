# Future Feature v5.5 — 升級規劃 reference（v5 production 觀察期 → v5.5 啟動候選）

> 建立日期：2026-05-15
> 狀態：規劃 reference（**v5 production 觀察期累積真實需求後評估啟動**）
> 對應主檔：[Future_Feature.md](Future_Feature.md) FF 三十六 Phase B 全程動態 candidate / FF 六十一補強清單
> 對應 Charter：[v5_charter/](../architecture/v5_charter/) Stage 62 既有規劃

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

**設計四要素**：

1. **Petra 拆解指令** — 把一個指令拆成多個 subtask，不是直接整包丟 worker
2. **每個 subtask 呼叫 Agent 執行** — Petra 看 subtask 性質決定哪個 worker
3. **Agent 對任務有持續記憶** — DB 持久記憶（不靠 subprocess context）
4. **記憶分層**（業界共識 hybrid 模式）：
   - **共用層**（task-level）：任務目標 / 各 agent 完成的 deliverable 摘要 / 關鍵決策歷史 — 跨 agent 可讀
   - **私有層**（per-agent role）：Cody 自己的 implementation 思考細節 / Vera 自己的 review checklist 進度 — 只該 agent 自己讀寫

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

## 六、啟動條件（不急動工）

**短期觀察期（v5 上線後 1-2 週 - 1 個月）**：
- 看真實業務任務跑下來，「one-shot 沒記憶」這件事**實際**痛不痛
- 看真實任務複雜度（是不是都「Dashboard 簡單打磨」級 / 還是有「跨多輪 Cody-Vera loop」）
- 累積 Petra 動態決策真實 trigger 分布（1-on-1 / Design / Kickoff trigger 真實命中率）

**中期啟動條件**（任一達成）：
1. **真實業務出現「多輪 Cody-Vera loop」場景** — 一輪 Cody → Vera review → 第二輪 Cody 修，第二輪需要記得第一輪做了什麼
2. **客戶專案進來** — 任務複雜度本身需要長 context + 持久記憶
3. **單一 Agent 任務跑超過 35 分鐘** — 對齊業界揭的 attention 退化臨界點
4. **某 agent 0 work 復發** — 對齊 Trial_v11 揭的 Vera 0 work 同類議題（chain wire 不穩或 framework 限制復發）
5. **Anthropic API 真實踩 quota / 5xx 連續失敗** — 觸發 multi-provider 升級候選（FF 六十一其餘其中一條）

**啟動後規模**：對齊 v5 PoC 二次架構升級 — 預估 3-5 個 Stage / ~1-2 個月工程（M-L 級投資）

---

## 七、不確定性 + 待驗證

1. **Petra 拆解指令的精準度** — 業界沒明確指引「LLM 拆解 user instruction 成 subtask」的 best practice，要 spike 驗證
2. **DB 持久記憶 schema 真實設計** — 共用層 + 私有層的 table 結構 / per-task scope 隔離 / summarization 觸發策略 都需要 design
3. **每次 load 進 subprocess 的記憶 token budget** — 業界推「不超過 context 10%」但 AiTeam 真實場景需要 spike
4. **戰略決策層的 3 agent 配置** — 哪 2 個 opposing perspective + 哪 1 個 synthesizer 對 AiTeam 場景最有效（Petra vs Demi vs Rosa? 或新 agent role?）
5. **v4 既有 module 的「砍 vs 留 vs 吸收」拍板** — Crash Recovery / HITL routing / sub-task 整合等 ~16K LoC 的去留

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

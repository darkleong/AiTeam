# Future Feature — 未來功能候選清單

> 版本：v7.47
> 建立日期：2026-04-01
> 最後更新：2026-04-28
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。已完成項目移至底部「已完成項目摘要」。

---

## 一、API 費用優化

### 背景

目前各 Agent 採用混合模型策略：
- **核心 Agent**（CEO / Dev / Reviewer）：`claude-sonnet-4-6`（品質優先）
- **唯讀探索 Agent**（Requirements / Designer / Doc）：`claude-haiku-4-5`（成本優先）
- **QA**：`claude-sonnet-4-6`（Claude Code CLI）
- **Release / Ops**：`claude-sonnet-4-6`（直接 API call，消耗低）

**已執行的優化：** Rosa、Demi、Sage 從 Sonnet 降級為 Haiku，預估節省 25-30% 整體 API 費用（2026-04-07）。

### 未來優化方向

- **Prompt Caching**：Anthropic 支援 Prompt Cache，cache read 僅需 10% 費用。對每次都帶入的規則清單、CLAUDE_Victoria.md 模板特別有效
- **Batch API**：非即時任務（如 Doc Agent）可走 Batch API，享 50% 折扣
- **模型持續評估**：隨新模型發布（價格持續下降），定期評估是否可進一步降級
- **Victoria turns 優化**：減少 Claude Code 的 maxTurns 或優化 prompt 長度，降低多輪對話成本

### 行動建議

- 持續觀察 Token 監控 Dashboard 的實際消耗數據
- Prompt Caching 是下一個投資報酬率最高的優化方向

### 優先級

🔵 低優先級 — 已完成第一輪降級，後續視消耗數據評估

---

## 二、Agent 個性與造型設定

### 背景

目前 Agent 個性與造型設定延後處理，不影響現有架構。

### 預計包含

- 每個 Agent 的名字與個性描述（寫進 System Prompt）
- Dashboard Team Office 頁面的人物造型替換
- 依狀態有對應動畫（忙碌打字、閒置發呆、錯誤冒汗）
- 辦公區之外加入休息區（Agent 閒置時移動過去）
- **Agent 互動頁面**：Dashboard 新增頁面，可觀察 Agent 當前活動，搭配個性化對話與心情文字

### LLM 供應商策略：Gemini Flash 免費額度

Agent 個性對話、心情文字、互動描述等屬於**低複雜度文字生成**，不需要 Coding 能力，也不要求高精準度（不正確也是有趣的一種表現）。

建議搭配 Feature 四（多 LLM 供應商）的最小實作，將此類場景路由到 **Google Gemini Flash API 免費額度**：

- Gemini Flash 免費、不需信用卡，有 rate limit（15 req/min）但對此場景綽綽有餘
- 新增 `GeminiProvider : ILlmProvider`，只需實作文字生成（不需 Vision / Tool Use）
- `appsettings.json` 設定範例：`"Personality": { "Provider": "Gemini", "Model": "gemini-2.5-flash" }`
- **零額外成本**，把 Claude API 費用留給真正需要 Coding 能力的 Agent

### 行動建議

- 等 Dashboard 視覺整體穩定後，開一個專門的討論來設計細節
- 實作時順帶完成 Feature 四的最小版（GeminiProvider），一石二鳥

### 優先級

🔵 低優先級 — 純視覺體驗優化，功能優先

---

## 三、AiTeam 安裝精靈

> 狀態：🔵 低優先級 — 等系統架構穩定後再規劃細節

### 背景

系統在新電腦上建置需要安裝多個軟體、設定多個參數、下多道指令，步驟繁瑣且容易出錯。

### 目標

提供一個互動式安裝精靈，讓整個建置流程可以引導完成，不需要看文件逐步操作。

### 預計形式

`AiTeam.Setup` — 解決方案內新增一個 .NET Console App 專案，Step-by-step 引導輸入所有必要設定，輸入後自動測試連線、建立設定檔、啟動容器。

> ⚠️ 注意：系統架構仍在開發演進中，具體的安裝步驟與設定項目會持續變化。**等系統穩定後，再來規劃精靈的具體內容。** 此項目只記錄方向，不記細節。

### 優先級

🔵 低優先級 — 系統架構穩定、準備在第二台機器部署時再實作

---

## 四、多 LLM 供應商支援（Gemini / OpenAI + Per-Agent 獨立設定）

### 背景

目前 `LlmProviderFactory` 只支援 Anthropic。架構上 `ILlmProvider` 介面已預留擴充點，加入新供應商只需實作介面並在 Factory 新增一個 case。

### 目標

1. **實作 `GeminiProvider : ILlmProvider`** — 串接 Google Gemini API，支援文字與 Vision
2. **每個 Agent 可獨立設定供應商與模型** — `appsettings.json` 的 Agent 設定已有 `Provider` 和 `Model` 欄位，實作後直接生效

設定範例：
```json
"CEO":  { "Provider": "Anthropic", "Model": "claude-sonnet-4-6" },
"Ops":  { "Provider": "Gemini",    "Model": "gemini-2.5-flash"  },
"Doc":  { "Provider": "Gemini",    "Model": "gemini-2.5-flash"  }
```

### 兩層抽象：API 層 + CLI 層

AiTeam 目前有兩層 LLM 架構，多供應商支援要分層討論：

| 層級 | 介面 | 使用的 Agent | 替換方案 |
|------|------|-------------|---------|
| **API 層** | `ILlmProvider` | Rosa / Demi / Sage / Release / Ops | `GeminiProvider` / `OpenAiProvider` |
| **CLI 層** | `IClaudeCodeService` | Victoria / Cody / Vera / Quinn / Petra | `GeminiCliService`（Gemini CLI 可並存） |

**原本的認知（「Claude Code 綁定」）已過時**——Google 推出 **Gemini CLI**（subprocess-based，介面對標 Claude Code CLI），CLI 層也可以做供應商抽象。

### 目標擴充：Claude Code + Gemini CLI 共存（Christ 決策 2026-04-18）

不走「替換」路線，走「**共存 + per-Agent 選擇**」路線：
- Dashboard Agent 設定頁的 Provider 選單，針對每個 Agent 選擇 Claude Code CLI 或 Gemini CLI
- 例如：Cody 用 Claude Code、Vera 用 Gemini CLI 做 Code Review 交叉驗證；或依成本/額度策略調配
- 風險分散（不把雞蛋全放 Anthropic 一家）

### 1M context 的認知澄清

Gemini 行銷主打 1M context 確實誘人，但對 AiTeam 現狀不是主要動機：
- **Claude Sonnet 4.6+ 也支援 1M context**（API header `anthropic-beta: context-1m-2025-08-07`）
- Agent 實際任務通常用 20-100K（改 bug、審 PR），離 1M 還很遠
- 真正 1M 發揮的場景是「一次塞整個 codebase 做 holistic 分析」——AiTeam 沒這種 use case

**真正動機應該是**：① 成本（Gemini Flash 免費額度）② 供應商分散 ③ 不同 Agent 品質交叉驗證

### 已完成成果（2026-04-25 為止）

- ✅ **第一階段：API 層 GeminiProvider**（Stage 37-1，v3.24.0）— 詳見 [Stage_37_Roadmap.md](Stage_37_Roadmap.md)
- ✅ **第二階段 2-A：Dashboard Provider/Model 動態化**（Stage 38，v3.25.0）— DB 為唯一 SoT + `AgentConfigCache` Singleton（TTL 5min）+ `LlmModels.cs` 常數白名單 + Internal API `scope=agent-config` cache invalidate；建立「PR #107 三條禁止路線」（Dashboard 自讀 appsettings / 無 EF Migration 只 cache / 雙源 merge）作為未來 self-implement 紅線；詳見 [Stage_38_Roadmap.md](Stage_38_Roadmap.md)

### 待做：第二階段 2-B — CLI 層多家共存

1. `GeminiCliService : IClaudeCodeService` — 評估 Gemini CLI 的 session 延續、工具調用、輸出格式是否能對齊
2. 主要挑戰：
   - Session 延續機制不同（Claude Code `--resume UUID` vs Gemini CLI 自己的 session 管理）
   - 工具調用格式、輸出解析格式不同
   - `MockClaudeCodeService` 模擬的是 Claude 輸出，Gemini 要另做一套
   - `CLAUDE.md` 對應的 `GEMINI.md` 兩套記憶檔並存維護成本
3. CLI 層 Agent（Victoria / Cody / Vera / Quinn / Petra）可選 Claude Code 或 Gemini CLI

### 2-B 實作重點（技術細節）

- `GeminiProvider` 第一階段未支援 Vision（Victoria / Quinn 傳圖片仍走 Anthropic），未來加 Vision 時擴充
- CLI 層做 `GeminiCliService`，需抽象出 `ICliAgentService`（原 `IClaudeCodeService` 重命名）或直接並列兩個實作
- `MockClaudeCodeService` 若為 Gemini CLI 做對應模擬，考慮抽出 `MockCliAgentService` 共用 MockMode 邏輯
- Token 追蹤（`TokenTrackingProvider`）包裝層對供應商透明，不需改動

### CLI 三家能力研究（2026-04-20 Aria 查官方文件 + GitHub issue 彙整）

> 屬研究階段，不是實作計劃。紀錄當下查到的能力對照 + 技術風險，供未來 spike 排入時的起點。

#### 三家 CLI 能力對照（2026-04 現況）

| 能力 | Claude Code | Gemini CLI | Codex CLI |
|---|---|---|---|
| **Session resume** | `--session-id UUID` + `--resume` | `--resume <UUID>` | `codex exec resume <SESSION_ID>` |
| **預先指定 UUID** | ✅ | ❌ **UUID 由 CLI 自己生**（FR #20847 2026-03 仍 open）| 不確定（SDK 提供 `resumeThread(id)` 可程式化控制）|
| **Headless / 非互動** | ✅ | ✅ | ✅ (`codex exec`) |
| **Stream JSON** | ✅ stream-json | ✅ `--output-format stream-json`（NDJSON）| ✅ `--json`（JSONL）|
| **Structured Output Schema** | prompt 約束 | prompt 約束 | ✅ **`--output-schema` native**（不能和 resume 合用）|
| **官方 SDK** | — | — | ✅ TypeScript SDK |
| **記憶檔** | `CLAUDE.md` | `GEMINI.md` | `AGENTS.md` |
| **Session 存放** | 容器內 `~/.claude/sessions/` | `~/.gemini/tmp/<project_hash>/chats/` | 類似機制 |

#### 對 AiTeam 的三個關鍵發現

1. **Gemini CLI 的 UUID 預設是唯一嚴重技術風險**
   - AiTeam 從 Stage 25a 起用「預先產 UUID → 傳給 CLI → 追蹤 session」模式，Gemini CLI 目前做不到
   - **但 Stage 30 已驗證「新開 session + 強化 prompt」路線可行**，恰好繞開 Gemini 的 UUID 限制，影響 < 預期

2. **Codex CLI 程式化介面是三家最完整的**
   - `--output-schema` 指定 JSON Schema 拿結構化結果（AiTeam 到處「請 Agent 回 JSON」，native 支援省去 prompt 工程）
   - TypeScript SDK 有 `resumeThread(id)` API，thread ID 管理明確
   - JSONL event stream 完整（init / user msg / tool use / tool result / assistant msg / final）
   - 對 AiTeam 可能是工程體驗最好的整合對象

3. **三家 stream JSON 格式不同 → Adapter 必做**
   - 格式 schema 三家各有各的，`ICliAgentService` 的 Adapter 要各自寫 parser 把事件映射成 AiTeam 共用 model（`AgentMessage` / `ToolCall` / `FinalResult`）

#### 修正後的抽象策略（沿用原「策略 A + 策略 C」）

**`ICliAgentService` 定義最小共通 API：**
```csharp
// 不暴露 Session ID 管理細節
Task<CliResult> RunOneShotAsync(string prompt, CliOptions opts);

// Session 延續：回傳 adapter 自管的 session token
Task<(CliResult, SessionToken)> RunWithSessionAsync(
    SessionToken? previousToken, string prompt, CliOptions opts);
```

- **不強求預設 UUID**（Gemini 做不到）
- **讓 Adapter 自管 session token**（Claude 給 UUID、Gemini 給檔案路徑、Codex 給 thread id），AiTeam 只存 opaque string
- **Stage 30 的「新開 session」模式成為 AiTeam 預設策略**，session 延續只保留給需要跨輪累積 context 的會議（Kickoff / Design）

#### 建議 Spike 排入時的順序

1. **先做 Codex CLI spike**（程式化最完整、SDK + schema 是驚喜）
2. **再做 Gemini CLI spike**（免費額度誘因，但 UUID 限制要處理）
3. **共用 `AGENTS.md` 方向**（或兩份記憶檔同步，研究 Gemini / Codex 是否都能讀）

#### 延伸資料來源

- Gemini CLI：[Session management](https://geminicli.com/docs/cli/session-management/) / [Headless mode](https://geminicli.com/docs/cli/headless/) / [FR #20847 --session-id flag](https://github.com/google-gemini/gemini-cli/issues/20847)
- Codex CLI：[Features](https://developers.openai.com/codex/cli/features) / [Non-interactive mode](https://developers.openai.com/codex/noninteractive) / [SDK](https://developers.openai.com/codex/sdk) / [Command line reference](https://developers.openai.com/codex/cli/reference)

### 優先級

🟡 中優先級（升級）— 原本 🔵 低是因為只想到 API 層替換；加入「CLI 層共存 + 交叉驗證」戰略後，與 Future Feature 二（Agent 個性/Gemini Flash 路由）形成完整的多供應商策略

---

## 五、CEO 長期記憶升級（向量搜索版）

### 背景

Stage 15 的長期記憶採用簡易版（DB 表 + 全量載入 prompt），在記憶量少時（< 100 筆）足夠使用。但隨著使用時間增長，記憶量會超過 prompt 容量限制。

### 簡易版 vs 向量搜索版

| | 簡易版（Stage 15） | 向量搜索版（本項目） |
|---|---|---|
| **存** | Victoria 提示詞驅動，自行判斷 | 每次對話結束自動摘要 + 存 |
| **找** | 全部載入 prompt，LLM 自己看 | 用 Embedding 語意搜索，只撈相關的 5~10 筆 |
| **Prompt 大小** | 隨記憶量線性增長 | 穩定（只載入相關記憶） |
| **基礎設施** | PostgreSQL 純文字表 | pgvector 擴充 + Embedding API（Anthropic / OpenAI） |
| **上限** | ~100 筆（10,000 tokens） | 數千筆 |

### 觸發條件

當以下任一情況發生時，考慮升級：
- 記憶量超過 100 筆，prompt 開始膨脹影響回應品質
- Victoria 開始「忘記」早期記憶（因為被截斷）
- 老闆反映 Victoria 回應變慢或品質下降

### 實作方向

1. PostgreSQL 啟用 `pgvector` 擴充
2. `CeoMemory` Entity 新增 `Embedding` 欄位（`vector(1536)` 或對應維度）
3. 記憶寫入時，呼叫 Embedding API 產生向量並存入
4. Session 開始時，將老闆的第一則訊息轉為向量，搜索最相關的 10 筆記憶
5. 可選：自動摘要 — session 結束時，LLM 摘要本次對話重點，自動存為新記憶

### 優先級

🔵 低優先級 — 簡易版足夠撐 1~2 個月日常使用，視實際記憶累積速度決定是否升級

---

## 六、測試環境隔離（Docker Compose Test Stack）

### 背景

目前 Playwright CI 在 self-hosted runner（production 機器）上直接操作 `docker-compose.prod.yml`，導致測試啟動/關閉會影響 production 容器。Stage 16 驗收時 Dashboard 容器就是被 `playwright.yml` 的 `Stop Dashboard`（`if: always()`）殺掉的。

根本原因是單機環境下沒有 production / test 隔離，CI 和正式服務共用同一組 container name 和 port。

### 期望行為

feature branch CI 觸發時，自動在測試區部署並跑 Playwright，完全不影響 production。

### 設計方向

同一台 Windows 11 機器，用 port 和 container name 隔離：

| | 正式區 | 測試區 |
|---|---|---|
| Dashboard | `localhost:5051` | `localhost:5061` |
| PostgreSQL | `aiteam-postgres`（volume: prod-data） | `aiteam-test-postgres`（volume: test-data） |
| Compose file | `docker-compose.prod.yml` | `docker-compose.test.yml` |

**`docker-compose.test.yml`：**
- 不同的 container name + project name（與 production 完全不衝突）
- 不同的 port mapping（5061 / 5462）
- 獨立的 postgres volume
- 使用 ghcr.io 上已 build 好的 image（不重複 build）

**`playwright.yml` 改版：**
```
1. docker compose -f docker-compose.test.yml up -d
2. health check → localhost:5061
3. 跑 Playwright
4. docker compose -f docker-compose.test.yml down  ← 殺的是測試區
```

### 資源評估

現有環境 RAM 15.21GB，目前使用 679MB（Bot + PostgreSQL + pgAdmin）。測試區估計額外消耗 500MB-1GB，完全沒問題。測試區只在 CI 跑時啟動，平常不佔資源。

### 優先級

🔵 低優先級 — Dashboard 存取分層（Stage 22）完成後，Playwright 直接打 localhost 免登入，CI 不再需要啟停容器，本項急迫性大幅降低。待客戶專案（七）需要完整隔離時再重新評估

---

## 七、客戶專案交付流程與驗收閘門

### 背景

AiTeam 的定位不只是開發自身系統，未來也會替客戶開發專案。目前的流程（merge 後自動部署）對 AiTeam 自身足夠，但對客戶專案的風險層級完全不同：

| | AiTeam 自身開發 | 客戶專案開發 |
|---|---|---|
| **壞掉的代價** | 自己的工具壞了 | 客戶的系統壞了 |
| **git revert** | 可以接受 | 不可接受 |
| **merge 後再測** | OK | ❌ 太晚了 |
| **驗收責任** | 自己 | 對客戶負責 |

目前的流程走完產出一個 GitHub PR，但 merge 之前沒有 Preview 環境可以人工驗收，直接 merge 等於直接上客戶 Production。

### 期望行為

```
需求 → ... → PR 開出
                ↓
         Preview 環境自動部署（Staging）
                ↓
         Victoria 通知 Christ：「PR #42 已部署至 staging，請驗收」
                ↓
         Christ（或客戶）在 Staging 實際操作驗收
                ↓
         驗收通過 → Christ 回覆 OK → Merge → Production 自動部署
         驗收失敗 → Christ 回覆問題描述 → 流程重新進入修正循環
```

### 需要的兩個東西

1. **每個客戶專案都有一個 Staging 環境**（不一定在本機）
2. **AiTeam 流程加入正式的人工驗收閘門**（Victoria 等待 approve 才算 Done）

### 待釐清的子問題

1. **客戶專案的 Staging 環境由誰負責？** — 客戶自己有 staging server？還是 AiTeam 在本機幫每個專案起 container？
2. **AiTeam 是否應該管理「部署到客戶環境」？** — 目前 Maya（Ops）只針對 AiTeam 自身
3. **驗收失敗的循環怎麼設計？** — Christ 的修改意見要怎麼餵回 CEO → 再分派給對應 Agent？

### 初步討論結論（2026-04-12）

**部署到 IIS Web Deploy 的能力：**
- 建議走 **GitHub Actions + Web Deploy** 模式 — 客戶 repo 掛 workflow，push 時自動 `msdeploy` 部署到 IIS
- Agent 不需要直接操作部署，只負責 push code，CI/CD 負責部署（與 AiTeam 自身模式一致）
- 每個客戶專案設定一次 workflow 即可

**Git Flow 多環境部署：**
- 完全可行，需調整 WorkflowEngine 的分支策略
- Cody 的 PR 目標從 `main` 改為 `develop`
- GitHub Actions 依 branch 觸發不同部署目標：feature→開發環境 / develop→測試環境 / master→Production
- Victoria 需理解 Git Flow 各階段，知道「merge 到 develop ≠ 上線」
- 人工驗收閘門：feature 部署到開發環境後，等 approve 才 merge 到 develop

### 與現有 Future Feature 的關係

- **六（測試環境隔離）**：六解決 AiTeam 自身的 CI 打到 production 問題；本項目解決客戶專案的完整交付流程，範圍更廣
- **八（開發流程重構）**：驗收失敗的修正循環可能觸發糾錯機制

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

---

## 八、開發流程重構（多人會議制 + 糾錯機制）

> **Phase 1 全部完成 + Phase 2 第三項（LLM→CLI）完成**，詳見 [已完成項目摘要]。
> 本 FF 主體聚焦 Phase 2 剩餘兩個機制：循環偵測 + 新鮮視角。

### 完成歷程快照

- **Phase 1**（七階段全流程：需求→設計→開發→審查→QA→歸檔→上線）：✅ Stage 23 / 24 / 25a / 25b（v3.7.0 → v3.10.0）
- **Phase 2 第三項**（LLM API → Claude Code CLI 全面升級，5 個申訴環節保留 codebase 存取）：✅ Stage 30（v3.17.0）
- **Phase 2 剩餘**：循環偵測 + 新鮮視角（本 FF 主體）

### 背景（原始動機，Phase 1/2 共用）

兩起已知事故觸發本 FF：
1. **Vera 誤判事故**：Vera 持續報告 false critical、Cody 無法反駁（單向權力結構），直到 Christ 手動介入才發現
2. **實作 Session 死循環事故**：Session 反覆做無效修正，沒有機制偵測循環模式

Phase 1 已用「多人會議 + 雙向 Review + Petra 仲裁」解決第 1 起；第 2 起則等 Phase 2 剩餘機制處理。

### Phase 2 剩餘機制

**循環偵測（Loop Detection）**：追蹤每次修正的 diff，偵測是否在反覆修改同一段程式碼（oscillation）。比單純輪次計數更聰明——兩輪都在改同一行 vs 兩輪改不同區塊，前者要升級處理，後者是正常迭代。

**新鮮視角（Fresh Eyes）**：熔斷觸發後，啟動全新獨立 Session 診斷問題根因（不帶前面的對話歷史）。對標現實：叫一個不在脈絡中的同事過來看問題。Stage 30 的「新開 session + 強化 prompt」模式是此機制的前置技術。

### 觸發條件（評估是否真的需要做）

- Stage 30 後觀察 2-4 週，看 Review Appeal / Dev_plan Appeal 是否仍有反覆修改同一段、無法收斂的案例
- 若收斂性已夠（輪次上限 + Petra 仲裁已足），可能不需要循環偵測
- 若仍有死循環，再評估本機制的投資報酬

### 優先級

🔵 低 — 剩餘兩機制是「保險絲」類型，等實際運行數據觸發再做。

## 九、Agent 任務序列 — 後續議題

> 核心機制已完成：Stage 27a（v3.12.0）+ Stage 27b（v3.13.0），詳見已完成項目摘要。
> 本 FF 剩下 PM 佇列化 1 個議題 + 3 個未實作擴充方向。

### 📌 PM（Petra）佇列化議題（待決策）

Petra 是 `TaskGroupService` 中的 inline `await` 閘門，不在 `AgentQueueProcessor.SemaphoreGroups` 的 8 個 executor key 中。`/stop-all` 不影響她，Dashboard 也不顯示她的佇列狀態。

**待討論**：
- 把她納入 queue 機制？還是另外設計？
- inline `await` 改成 queue 是否會造成流程阻塞（Petra 上游等她時全部塞住）？
- 「Petra 暫停中，流程卡在審核點」語意是否合理？

### ✅ 已釐清議題

- **PM 執行路徑**：Petra 確認走 Claude Code CLI（`PmAgentService` 用 `RunMeetingSessionAsync`），Stage 30 所有 5 個申訴環節已升級，詳見 FF 八 Phase 2 第三項
- **Dashboard pause/resume 操作按鈕**：已移至 FF 十五（Dashboard 與 Discord 功能平等）剩餘子項處理

### 未實作的設計方向（保留供未來參考）

原始設計規劃但尚未實作：
- **Maya 自動化部署流程**：Maya 發送 Graceful Shutdown → 確認全員 Stopped → 執行部署 → 自動恢復
- **Error 狀態阻塞 + 手動重試/取消**：Error 任務留在 queue 頭部，Dashboard 提供重試/取消按鈕
- **優先級支援**：修正任務優先、緊急上報可插隊、中斷恢復排入隊首

### 優先級

🔵 低優先級 — 核心佇列已完成，剩餘為擴充性需求

## 十、Dashboard UI 細節打磨（第四批）

> 狀態：🔵 低優先級 — UI 組織與使用便利性優化，待 Christ 確認完整清單後排入 Stage

### 背景

Stage 19 已完成三批 Dashboard UI 細節打磨。以下為第四批累積的改善需求。

### 改善清單

#### 系統設定獨立頁面

目前「Agent 設定」頁面下方塞有「系統設定」區塊（跳過 CEO 派工確認、Mock Mode）。隨著系統設定項目增加（Token 守門閾值等），繼續共用同一頁面會越來越混亂：
- 新增路由 `/system-settings`，對應新 Blazor 頁面 `SystemSettings.razor`
- 側邊欄 NavMenu 加入「系統設定」連結
- Agent 設定頁移除底部「系統設定」區塊，功能邏輯不變

#### 任務列表頁面

目前篩選只有「狀態」一種。新增以下三種篩選：
- **專案篩選** — 依專案名稱過濾
- **Agent 篩選** — 依執行 Agent 過濾
- **觸發來源篩選** — 依觸發方式過濾（Discord 指令 / GitHub Webhook / 排程 / 手動等）

#### 流程追蹤頁面

目前篩選只有「狀態」一種。新增以下兩種篩選：
- **專案篩選** — 依專案名稱過濾
- **流程類型篩選** — 依流程類型過濾（開發流程 / 文件 / Release / Ops 等）

PR 欄位顯示優化：
- 目前顯示完整 GitHub URL（冗長）
- 改為顯示 PR 編號超連結（例如：`#999`），點擊開新分頁到 GitHub PR 頁面

#### 專案管理頁面

「啟用」欄位目前顯示 Switch + 文字「啟用中」，文字冗餘：
- 移除文字「啟用中」，只保留 Switch 本身

#### 規則管理頁面

「狀態」欄位同上，Switch + 文字「啟用中」冗餘：
- 移除文字「啟用中」，只保留 Switch 本身

「操作」欄位目前顯示文字按鈕（編輯 / 刪除），視覺佔空間太大：
- 改為圖示按鈕（Icon Button），不顯示文字
- 建議圖示：編輯用 `Edit`，刪除用 `Delete`（MudIconButton）

#### TaskLog 獨立檢視頁面

目前 TaskLog 只能透過任務列表或 Pipeline View 點擊任務後，在右側 480px 抽屜中查看，不夠直觀。新增獨立頁面：
- 新增路由 `/logs`，對應新 Blazor 頁面
- 側邊欄 NavMenu 加入「執行記錄」連結
- 以時間軸方式展示所有 Agent 的 TaskLog，可依 Agent / 任務 / 狀態篩選
- 支援展開 Payload 內容（如 Designer 的 UI 規格 Markdown）

#### Dashboard Cache Reload 按鈕

目前在 Dashboard 修改規則或 Agent 設定後，需要到 Discord 執行 `/reload-rules` 才能生效，操作流程不合理。Dashboard 應自帶 reload 按鈕：
- 規則管理頁面 / Agent 設定頁面新增「套用設定」按鈕
- 按鈕呼叫 Bot internal API：`POST /internal/reload-cache?scope=rules|agents|all`
- Bot 收到後清空對應 Cache，重新從 DB 載入
- 與 DashboardPushService 同模式（Dashboard → Bot HTTP 呼叫）

#### Agent 角色設定 Dashboard 化

目前每個 Agent 的角色描述、行為準則都硬碼在 C# Agent Service 的 prompt 字串中，修改需要改程式碼重新部署。將可配置的部分抽出至 DB，透過 Dashboard 編輯：

**可配置（存 DB）：**
- 角色設定 — 「你是 Cody，Dev Agent，負責撰寫程式碼...」
- 行為準則 — 「寫完要 dotnet build 確認」「commit message 用繁中」

**不可配置（留在 C#）：**
- 流程邏輯模板 — 輸出格式要求、JSON 結構定義等（與 C# parse 邏輯綁定，改了 prompt 會導致程式端解析失敗）

**技術方向：**
- 復用現有規則系統的 DB + Cache + `/reload-rules` 機制
- Agent 設定頁面擴充：現有 Provider / Model 欄位旁，新增「角色設定」和「行為準則」文字編輯區
- prompt 組合順序：`DB 角色設定` + `DB 行為準則` + `C# 流程邏輯模板` + `任務動態內容`
- 與 Feature 二（Agent 個性）共用：個性描述就是角色設定的一部分

**分階段實作**：
- **Phase 1**：C# `SystemPrompt` 抽至 DB（API 層 Agent — Rosa / Demi / Sage / Release / Ops / Petra 部分）
- **Phase 2**：`CLAUDE_*.md` template 抽至 DB（CLI 層 Agent — Victoria / Cody / Vera / Quinn / Petra 部分）— **Stage 39 結案後 Christ 提出（2026-04-25）**：Stage 38 `AgentConfigCache` pattern 直接抄即可，工程上無新東西；但目前 `CLAUDE_*.md` 內容仍在演進（Stage 39 才剛擴充 Vera），且修改頻率低（1-2 月一次）+ 對系統行為影響大（JSON 輸出格式 / 工具範圍 / 角色定位），**code review 軌跡比 DB 安全**。**觸發條件**：等 AiTeam 核心功能穩定（Trial_v3 跑完、CLAUDE_Vera.md 內容定型 1-2 月後）+ 出現「想立刻試新 prompt」真實使用情境，再評估 Phase 2

### 優先級

🔵 低優先級 — 不影響功能，純 UI 組織與使用便利性優化；Phase 2（CLAUDE_*.md）需等核心穩定，目前不急

---

## 十一、Dashboard 可調整 Token 守門全域限額

> 狀態：🟡 中優先級 — Stage 37-2 驗收首次實際踩到全域月限（2026-04-24 升級，原 🔵 低）

### 背景

Stage 22 實作了 Token 守門機制，包含：
- **全域月費上限**：`AgentSettings:MonthlyTokenLimitK`（預設 1000K）
- **各 Agent 日限**：`AgentSettings:Agents:{Name}:DailyTokenLimitK`
- **各 Agent 月限**：`AgentSettings:Agents:{Name}:MonthlyTokenLimitK`
- **單次請求上限**：`AgentSettings:SingleRequestTokenLimitK`（預設 50K）

目前這些值只能透過修改 `docker-compose.prod.yml` 環境變數並重新部署來調整。Token 監控頁面的警示訊息也只能說「請至 Bot 設定調整」，沒有直接入口。

### 實際踩坑紀錄（Stage 37-2 驗收，2026-04-24）

驗收「Agent 設定頁顯示 Provider/Model」任務（FF 四第二階段 self-implement 試驗）跑到 Dev 階段時，TokenTrackingProvider 的 Check 4（全域月限）擋下：

```
Dev Agent 執行失敗：Token 守門：全域本月用量 1,304,628 + 估算 5,289
超過全域月限 1,000,000。所有 LLM 呼叫已暫停。
```

老闆角度的體驗問題：
- Dashboard Token 監控頁警告訊息寫著「請至 Bot 設定調整 `AgentSettings:MonthlyTokenLimitK`」—— 相當於**教使用者 SSH 改 config + push + 等 CI/CD 重 build**
- 對「只動嘴的老闆」完全不友好，Stage 27b 以降的 `/pause` `/resume` 已經把佇列控制 Dashboard 化，Token 守門也該跟上
- 臨時救援只能改 `docker-compose.prod.yml` env var（`MonthlyTokenLimitK: "1000" → "2000"`）+ commit push + CI/CD rebuild（~3 分鐘 downtime）

事故當日採用臨時救援（commit `<this commit>`）讓驗收繼續。這是第一次真正碰到月限，本 FF 必須做。

### 需求

在 Dashboard 「系統設定」頁面（配合十、系統設定獨立頁面規劃）加入 Token 守門設定區塊：

- **全域月費上限**（MonthlyTokenLimitK）— 數字輸入框，單位 K tokens
- **單次請求上限**（SingleRequestTokenLimitK）— 數字輸入框，單位 K tokens
- **各 Agent 日限 / 月限** — 表格形式，每個 Agent 一列，可直接修改數字

修改後儲存到 **動態 `AppSettings` 資料表**（Bot 端已有 TTL cache 機制），不需要重啟容器即可生效。Token 守門邏輯需改成優先讀取動態設定，appsettings.json 作為預設值 fallback。

### 技術方向

1. `AppSettings` 資料表新增 `AgentTokenLimits:*` 系列 key（或 JSON blob）
2. `TokenTrackingProvider` 守門邏輯改成透過 `AppSettingsService` 讀取動態值（有快取）
3. Dashboard 新增對應 UI，透過現有的 `DashboardAppSettingsService` 存取

### 優先級

🟡 中優先級 — 已實際擋下驗收任務、臨時救援成本高（commit + CI/CD rebuild + 30 秒 downtime）。建議下個獨立小 Stage 處理，或搭車 FF 十（Agent 設定頁 refactor）一起做。

---

## 十二、Sage 全系統文件健康檢查

### 背景

Stage 23 流程重構中，Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」，pipeline 中的工作改為輕量的文件整理 + CHANGELOG 更新。

但長期而言，BugFix / TechImprovement 跳過 Doc 階段，加上程式碼持續演進，系統文件會逐漸與實際程式碼脫節。需要一個機制定期檢查並修補差異。

### 期望行為

Sage 作為獨立定期任務（非 pipeline 內），掃描整個專案：
- 比對程式碼與現有文件的差異（API 變更、新增模組、移除功能）
- 識別過時或缺漏的文件
- 自動補寫或更新，產出健康檢查報告

### 觸發方式

- 定期排程（例如每週 / 每月）
- 或手動指令觸發（Discord / Dashboard）

### 優先級

🔵 低優先級 — Phase 1 先觀察流程文件是否足夠，有實際過時問題再啟動

---

## 十三、UAT 驗收階段（待觀察）

### 背景

目前流程在 QA 通過 + 收尾歸檔後直接完成，沒有 Christ 親自驗收「做出來的是不是我要的」的正式步驟。Christ 目前是上線後才發現問題，等於非正式的 UAT。

新流程已有多層品質把關（Kick-off 會議、設計會議、Vera 審查、Quinn 測試），需求走偏的機率比以前低。但 AI 經過多次轉手，仍有可能「技術上正確但需求上不符」。

### 期望行為（若啟用）

Victoria 在 pipeline 完成前整理一份輕量交付摘要（關鍵結果 + 截圖），Christ 確認後才 merge。BugFix / TechImprovement 可跳過。

### 待觀察

- 觀察新流程上線後，是否頻繁出現「做出來不是我要的」的情況
- 若頻繁發生 → 加入 UAT 階段
- 若很少發生 → 維持現狀，不加入

### 優先級

⚪ 待觀察 — 目前先不加入，視新流程實際運作結果決定

---

## 十四、Agent I/O 完整記錄（待討論）

> 狀態：⚪ 待討論 — 等 TaskLog 獨立頁面（十）上線後，實際檢視現有 Log 內容再決定

### 背景

目前 TaskLog 記錄的是流程層級的步驟狀態（「開始執行」「Git Clone 完成」等），不包含 Agent 實際與 LLM 交換的 prompt 和 response 全文。Christ 希望能看到每個 Agent 接收和產出的完整內容。

### 涵蓋範圍

系統中 Agent 與 LLM 的互動分兩條路徑，都需要埋 log：

| 路徑 | 涉及 Agent | 埋點位置 |
|------|-----------|---------|
| Claude Code CLI | Victoria、Cody、Vera、Quinn、Sage、Rosa、Demi、Petra（部分） | `ClaudeCodeService` 執行前後 |
| LLM API | Rena、Petra（部分）、申訴/仲裁流程 | `ILlmProvider.CompleteAsync()` 前後 |

Maya（Ops）為純程式邏輯，不呼叫 LLM，不在範圍內。

### 注意事項

- **資料量大**：Claude Code CLI 的 prompt 通常包含 CLAUDE.md、規則、上下文，一筆可能數千到上萬 token 的文字量
- **DB 儲存策略**：需考慮是否壓縮、TTL 自動清理、或只存摘要
- **Dashboard 載入效能**：完整 prompt/response 不適合列表直接顯示，應採用摺疊/按需載入

### 決策前提

先完成十的 TaskLog 獨立頁面，Christ 實際看過現有 Log 內容後，再決定：
1. 現有 TaskLog 的資訊是否足夠
2. 若不夠，需要補到什麼層級（流程摘要 vs 完整 I/O）
3. 儲存策略與效能取捨

### 優先級

⚪ 待討論 — 依賴十的 TaskLog 頁面上線後再評估

---

## 十六、Dashboard 錯誤處理與提示 UX 打磨

> 狀態：⚪ 低優先 — 現行功能可用，屬體驗優化
> 提出日期：2026-04-19（Christ 於 Stage 29-5 快速下達指令驗收時提出）

### 背景

Stage 29-5 實作快速下達指令卡時遇到兩個 UX 觀察點，目前以「夠用就好」的版本實作，列入後續統一打磨：

### A. MudBlazor 元件內部例外的接住機制

**現況**：`MudFileUpload.MaximumFileCount` 超量時會從元件內部拋 `NotifyChange` 例外，Blazor circuit 只能印到 log，使用者看不到上下文。目前採「預防」作法——移除 `MaximumFileCount`、由 `@bind-Files:after` 自行驗證。

**想改善的點**：若 MudBlazor 未來提供 `OnValidationError` 或類似事件，可考慮切回「元件內建驗證 + 我方接住顯示」模式，讓錯誤處理與顯示更集中、不用每個 MudFileUpload 使用點都各自實作驗證邏輯。

**追蹤要素**：
- MudBlazor GitHub issue / release notes 中是否有新的 error hook
- 若社群有成熟的 `<ErrorBoundary>` 局部攔截 pattern 可沿用
- 自己包一層 `ValidatedFileUpload` component 統一管理（若使用點變多時再做）

### B. 錯誤訊息同時顯示 MudAlert + Snackbar

**現況**：快速下達指令卡的違規提示只顯示在送出區上方的 `MudAlert`，使用者若視線在其他地方會錯過。

**想改善的點**：
- 表單內保留 `MudAlert`（inline，緊貼違規來源）
- **同時**彈出右下角 `Snackbar`，確保使用者不依賴視線也能注意到
- 兩者訊息一致，Snackbar 自動消失（3-5 秒）避免干擾

**涵蓋範圍**（未來擴大時考慮）：
- 快速下達指令卡（首頁）
- 系統設定頁的「儲存失敗」回饋
- 操作中心卡片回覆失敗（樂觀鎖衝突 409）
- Agent 設定 / 規則管理的儲存錯誤

### 實作策略

這不是新功能，是既有訊息機制的統一化。建議在下一次 Dashboard UI 整輪打磨（類似十一的「第五批」）時一起做：
1. 列出所有顯示錯誤訊息的點
2. 定義統一的 `DashboardNotificationService`（wrap `ISnackbar` + 元件內 `MudAlert` 呼叫）
3. 批量改造

### 優先級

⚪ 低 — 不影響正確性，純 UX 改善。Stage 29-5 當下的 inline `MudAlert` 已足夠傳達訊息。

---

## 十九、Agent maxTurns 動態化（Dashboard 可調）

> 狀態：⚪ 待觀察 — 等 AiTeam 架構穩定（例如 Codex CLI / Gemini CLI 整合後）再評估
> 提出日期：2026-04-20（Stage 32 規劃時釐清）

### 背景

Stage 32 規劃「系統設定頁擴充」時考慮過把 Claude Code CLI 的 `maxTurns` 抽出成動態設定，但發現**不可行**：

- `maxTurns` 散落在 `MeetingService` 等多處，對應**不同角色**有不同值：
  - Rosa / Demi / 設計會議 Agent：25
  - 其他特定場景：12
  - Victoria 預設：40
- 不是單一「全域 maxTurns」設定，要放 UI 就得「每個 Agent 一個欄位」，Dashboard UI 會爆炸
- 強抽單一值會造成行為不一致（會議被截斷 / 探索不夠深 / 浪費 Token）

### 為什麼值得保留為候選

Christ 的長期願景是「Dashboard 能調 AiTeam 各項行為參數」。maxTurns 是其中一個——未來 Dashboard 上把每個 Agent 的 maxTurns / Model / 行為準則都整合成一個完整的「Agent 設定頁」時，maxTurns 應該放進去。

### 觸發條件

以下任一發生時，重新評估：

- 多 CLI 供應商整合完成（FF 四 第二階段），此時「每個 Agent 的 CLI 設定」自然擴充到需要包含 maxTurns
- Agent 角色設定 Dashboard 化（FF 十「Agent 角色設定 Dashboard 化」子項），此時順帶把 maxTurns 加入同一個設定區塊
- 老闆實際遇到「會議被截斷」或「探索不夠深」等事故，需要臨時調 maxTurns 又無法動設定檔

### 不建議單獨做的原因

目前 AiTeam 還在建置 / 測試期（CLI 供應商、Agent 角色設定都可能大改），架構穩定前單獨抽 maxTurns 會踩到重複工作。等 FF 四 / FF 十的「Agent 角色設定 Dashboard 化」開工時順帶做最經濟。

### 優先級

⚪ 待觀察 — 屬於 AiTeam 架構穩定後的「Dashboard 控制中心」願景拼圖之一，不急

---

## 二十二、Agent 命名一致性（守門 + 名稱映射）

> 狀態：🔵 低 — 防呆類技術債，等搭車或獨立小 Stage
> 提出日期：2026-04-22（Stage 33 Roadmap v2.1 Dev_plan ghost 卡 debug 時浮現）

### 背景

AiTeam 系統中存在**兩種「Agent 名稱」的混淆**，導致 Stage 33 驗收時出現「Dev_plan ghost 卡」bug：

| 層面 | 什麼是 Agent 名稱 |
|------|-------------------|
| **DB `agent_configs` 表** | 真 Agent（Cody / Rosa / Demi / Vera / Quinn / Sage / Petra / Victoria / Maya / Rena）|
| **TaskItem.AssignedAgent** | 工作階段名，**可能含 workflow-only 的階段名**（Dev_plan / Kickoff / Design 等）|

**實際事故**：
- `AgentQueueProcessor.ExecuteTaskAsync` 推送 `AgentStatus` 時，`AgentName = task.AssignedAgent`
- 如果任務階段是「Dev_plan」，推送出去的 AgentName 就是 "Dev_plan"
- Dashboard `Home.UpdateAgentStatus` 對 SignalR 推進來的未知 AgentName **盲接 Add**
- → 首頁出現「Dev_plan」這個假 Agent 卡（實際沒有這個 Agent）
- Stage 33 臨時修法：`UpdateAgentStatus` 加白名單過濾，只接受初始 DB 撈的真 Agent

這個白名單是**症狀補丁**，根本的兩個議題留待本 FF 處理。

### 子項 A：AgentConfigService 命名守門

**現況**：建立 / 修改 `AgentConfig` 時，沒有任何檢查去擋「名稱是否為 workflow 保留字」。目前靠人為紀律維持，但只要 Dashboard Agent 設定頁有人手滑建了一個叫 "Dev_plan" 的 Agent，**整個 UI 會混亂**（SignalR push 無法區分真 Agent 更新 vs workflow 階段更新）。

**實作方向**：
- `AgentConfigService` 新增 `ReservedWorkflowNames` 常數：`{ "Dev_plan", "Kickoff", "Design" }`（未來有新 workflow 階段名再加）
- 建立 / 改名時檢查，撞到保留字 → 拋例外 + Dashboard 顯示錯誤
- 順便檢查 Agent 名稱是否與其他既有 Agent 重複

**規模**：S（約 1 個 validation method + 對應 UI 錯誤處理）

### 子項 B：Bot PushAgentStatus 名稱語意清理

**現況**：`AgentQueueProcessor.ExecuteTaskAsync` 推送 AgentStatus 時直接用 `task.AssignedAgent`，語意模糊——「工作階段名」被當成「真 Agent 名」推到客戶端。Stage 33 白名單補丁雖然擋住了 ghost 卡，但根本問題沒解。

**實作方向（兩種策略）**：

1. **映射法**（推薦）：Bot 端 push 前做 `AssignedAgent → 真 Agent` 映射表（如 `Dev_plan → Cody` / `Kickoff → Petra` / `Design → Petra`），客戶端收到的 AgentName 一律是真 Agent
   - 優點：客戶端程式碼最乾淨，不用懂 workflow 階段名
   - 要處理的邊界：`StatusBadge` / `Pipeline View` 若需要顯示「目前在哪個階段」，要看 `CurrentTaskTitle` 或另傳 `Stage` 欄位

2. **雙欄位法**：推送時明確分 `AgentName`（真 Agent）+ `WorkflowStage`（可選，階段名），客戶端依需要用
   - 優點：語意清晰不失訊息
   - 缺點：`AgentStatusViewModel` + `PushAgentStatusAsync` 介面變動面大

**規模**：M（影響 `AgentStatusViewModel` + `PushAgentStatusAsync` + 客戶端 `UpdateAgentStatus` + StatusBadge / Pipeline View 可能的 fallback 邏輯；需要整體評估才不會破功能）

#### 具體案例（2026-04-25 Trial_v2 試驗發現）

執行 self-implement 試驗 v2 期間，Christ 觀察到首頁 Dev Agent 狀態卡顯示矛盾：

| 顯示位置 | 邏輯 | 看到什麼 |
|---------|------|---------|
| Agent chip 「閒置」 | 看 SemaphoreGroups 的 `Dev` 是否被佔用 | Dev_plan 不算佔 Dev semaphore → 閒置 |
| Expand 「🏃 規則管理頁表... 已跑 40s」 | 看 TaskItem.AssignedAgent | 包含 Dev_plan → 顯示 running |

**兩個資料來源邏輯各自正確，但並列顯示 → 使用者覺得矛盾。** 這是子項 B 「PushAgentStatus 名稱語意清理」的真實使用者體驗痛點。詳見 [docs/experiments/Trial_v2_RuleManagementUI.md](../experiments/Trial_v2_RuleManagementUI.md)。

### 搭車時機

- **子項 A 可獨立做**：若未來做 Agent 設定頁 refactor（FF 十「Agent 角色設定 Dashboard 化」）時一起加守門最經濟
- **子項 B 較大，建議搭車**：等未來 refactor SignalR push 層、或做 FF 八 Phase 2 時（循環偵測會觸及 Agent 狀態推送）再一起整理

### 優先級

🔵 低 — 白名單補丁已防住實際影響，本 FF 是架構清理。不急，但未來新增 workflow 階段時可能再次踩坑，屆時優先級升 🟡

---

## 二十三、Orchestration 異常退出的復原機制（Stage 31/37 Crash Recovery 盲點）

> 狀態：🟡 中 — Stage 37-2 驗收時首次踩到，手動 SQL 救回；下次再踩該自動化
> 提出日期：2026-04-23（Stage 37-2 驗收 Design 會議 null AdjustmentTargets 事故）

### 背景

Stage 31 實作會議 Crash Recovery（v3.18.0）、Stage 37-2 升級為全編排流程 Crash Recovery（五種 `ActiveOrchestration` 值），核心機制是：

1. 進入流程時 `ActiveOrchestration = "Design"` / `"ReviewAppeal"` / ...
2. `try { ... } finally { ActiveOrchestration = null; }`
3. Bot 啟動掃 `ActiveOrchestration != null` 的 group → 重跑

**設計前提**：crash = 進程被 kill → finally 沒機會跑 → flag 留在非 null → 下次啟動掃到。

### 盲點

Stage 37-2 驗收當日踩到：[`DesignMeetingService.RunDesignAdjustmentAsync`](../../src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs:409) 因 Petra JSON 漏填 `AdjustmentTargets` 拋 `ArgumentNullException`：

- Exception 沿 call stack 往上傳
- Design 的 try-finally **正常執行 finally** → `ActiveOrchestration` 被清回 `null` ✅
- 但 exception 也讓整場會議失敗、group 卡在 `Status=running / DesignPlan=null`
- **Crash Recovery 掃不到**（flag 已清），group 永遠卡住

即「crash recovery 只防 crash，不防邏輯 exception」。Design / Kickoff 因為被 Stage 31 排除「重試按鈕」（會議非單一 Agent 任務，`RequeueTaskAsync` 無法處理），所以也沒 UI 路徑可以手動救。

Stage 37-2 當日手動救法：SQL 直接 `UPDATE task_groups SET "ActiveOrchestration"='Design' WHERE Id=...` + `docker restart aiteam-bot` 觸發 Recovery。能用但**不是 Christ 該做的事**。

### 三個方向（選一或組合）

#### A. 異常不清 flag — 最自動

```csharp
try { ... }
catch (Exception ex) {
    logger.LogError(ex, "Design 會議 exception，保留 ActiveOrchestration 等 Recovery");
    throw;  // 讓 caller 知道失敗
}
finally {
    // 只有「正常完成」才清 flag
    if (!hadException) await ClearActiveOrchestrationAsync();
}
```

- ➕ 全自動、零人工介入
- ➖ 若 bug 本身重複觸發（例如 Petra 每次都給同樣 null JSON），Recovery 每次重啟都再跑一次、永遠失敗 → 需要加「失敗次數上限」欄位
- ➖ catch 後 rethrow 行為要小心，上游 `AgentQueueProcessor` 現在不期待 Meeting 流程拋 exception

#### B. Dashboard 手動「重啟會議」按鈕

- 流程追蹤頁面「設計規劃失敗」的步驟卡加按鈕
- 按鈕呼叫 Dashboard internal API → Bot `RestartDesignMeetingAsync(groupId)`（直接呼叫 `MeetingOrchestrationService.RunDesignPhaseAsync`）
- Kickoff 同理

- ➕ 人工判斷「值不值得重試」，避免 A 的無限迴圈
- ➖ 不是自動，老闆要主動發現並操作

#### C. 失敗狀態欄位 + Dashboard 顯示 + 自動重啟計數

折衷方案：
- `TaskGroup` 新增 `OrchestrationFailureCount` + `OrchestrationFailureMessage`
- Exception 時 **不清 flag**，但把錯誤訊息落地
- Recovery 掃到時，若 `FailureCount < 3` 重跑、反之不做（等待人工介入）
- Dashboard 顯示「已自動重試 N 次，訊息：xxx」+ 手動「強制重試」/「放棄」按鈕

- ➕ 自動 + 人工雙保險
- ➖ schema 改動 + UI 較大，算中等 Stage 工作量

### 搭車時機

- **可獨立小 Stage**，~4-8 小時（依選 A/B/C 而異）
- 若等 Gemini 實測驗收發現更多 exception 場景（FF 四第一階段 Rena + Rosa/Demi fallback），建議累積一批後統一修
- 或在 Dashboard UI 細節打磨 Stage 中搭車做 B

### 優先級

🟡 中 — 實際卡住過老闆，但頻率低（Petra JSON 格式漏欄位是偶發）。修完可避免下次手動 SQL 救援。

### 相關程式碼

- [`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync`](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs:418)（Stage 37-2 擴充的 dispatcher）
- [`AppealOrchestrationService.HandleReviewerCompletedAsync`](../../src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs:51) / `HandleDevPlanCompletedAsync` / [`QaCoordinationService.HandleQaCompletedAsync`](../../src/AiTeam.Bot/Orchestration/Qa/QaCoordinationService.cs:29)（Stage 37-2 新加 try-finally）
- 以上所有 try-finally 都要考慮本 FF 的 exception 路徑處理

---

## 二十五、Self-implement 試驗 prompt 設計守則（Cody 繞道傾向）

> 狀態：🟢 經驗紀錄 — 不是技術 FF，是 prompt design 知識庫
> 提出日期：2026-04-24（Stage 37-2 PR #107 self-implement 試驗 close 後）

### 背景

Stage 37-2 期間 Christ 順手做了一個「self-implement 試驗」：把 Stage 37-1 範圍刻意砍掉的子項（FF 四第二階段-A：Dashboard Provider/Model 顯示）當成普通需求丟給 Victoria，看系統能不能自己實作。

整條流程跑通（Kickoff → Design → Dev_plan → Petra → Dev → Reviewer → Petra → QA → Doc → NotifyBossMerge），同時順帶實證 Stage 37-2 的 Crash Recovery / RequeueTaskAsync fix / Replay endpoint 三個救援機制——**整套 orchestration 在真實流程下穩固**。

但生出的 PR #107 經 Aria review 後 close 不 merge，**因為架構方向不對**：Cody 選了「Dashboard 自己讀一份 appsettings」快路徑，繞過了「真正該做的 DB migration」。

### 觀察的傾向

**Cody 在 Petra 沒擋下時，會優化「執行成本」而非「架構正確性」。** 具體表現：

1. **遇到「需要動 entity + migration」的需求**：Cody 傾向找方案讓改動只發生在 service 層 / config 層，避免 schema 變更
2. **遇到「需要跨服務同步」的需求**：Cody 傾向各自為政（Bot 一份 config、Dashboard 一份 config）而非建立單一 source of truth
3. **Vera 寫的 W01/W02 警告**（重複 fallback 邏輯、硬編碼字串應引用預設值）**Petra 沒當 Critical 放行**——這也是個 gap：Petra 對「架構優雅度」類議題的閾值偏寬

### 守則建議（給未來想做 self-implement 的需求 prompt）

下需求給 Victoria 時，如果涉及以下任一情境，**prompt 必須明寫禁止語句**：

- 「資料源統一」/ 「single source of truth」類需求
  → 「**禁止繞道：必須建立 DB schema 統一資料源，不接受純 config 同步方案**」
- 「跨服務一致性」需求
  → 「**禁止讓 Bot 和 Dashboard 各自維護獨立配置**」
- 「動 schema」類需求
  → 「**必須包含 EF Migration**，不接受純 service-layer 補丁」

否則 Cody 會選快路徑、PR 看似完成、但架構債堆積。

### 對 Petra 的觀察

Petra（PM 閘門）目前對「Vera 標 W (Warning)」級別問題會放行進入 QA，這在普通修 bug 場景合理（不該過度閉門），但在 self-implement 試驗時可能放過架構議題。**未來如果擴大 self-implement 用途，Petra prompt 可能要加一條**：「Warning 內若涉及『重複邏輯 / 硬編碼預設值 / 多份 config 維護』類議題，視為 Critical 不放行」。

> ✅ **Stage 40（v3.27.0，2026-04-26）已完成此 Petra 子項**：CLAUDE_Petra.md 第 4 節新增「Warning→blocking 升級規則」清單五條（重複定義 / 硬編碼常數 / config 分散 / pattern match / `target="_blank"` 缺 `rel="noopener"`）。FF 二十五本體（self-implement 任務 prompt 設計守則）仍保留作為未來規劃 Trial 任務的 reference。

### 優先級

🟢 經驗紀錄 — 沒有對應的「實作項目」，這是 prompt design knowledge。Aria 結案第二段更新 CHANGELOG 時可參考這份觀察。

---

## 二十六、Model 清單 DB 化 + Dashboard 管理頁（Stage 38 延伸升級）

> 狀態：⚪ 待觀察 — Stage 38 採 constants 檔方案，先運行 1-2 個月觀察節奏
> 提出日期：2026-04-25

### 背景

Stage 38（v3.25.0）做完 Agent 的 `Provider` / `Model` 動態化後，Dashboard Model 下拉清單的資料源是 `src/AiTeam.Shared/Constants/LlmModels.cs`（hard-coded 常數檔）。維護流程：

1. 新 Model 上線時 Aria 用 WebFetch 查 Anthropic / Google 官網確認
2. Aria 改 `LlmModels.cs` 字串 → commit + push
3. CI/CD 自動 build + restart（5-10 分鐘生效）

**為什麼 Stage 38 不直接做 DB 化**：
- 新 Model 發布頻率 2-3 月一次，部署時間不是痛點（YAGNI）
- Code 存放比 DB 安全：commit diff 有 review 軌跡、誤改可 revert、審計清楚
- 本 FF 待觀察：如果真的 1-2 個月後 Aria 改 constants 的節奏開始卡到 Christ 切換實驗，再升級

### 升級觸發條件

以下任一發生時，評估是否啟動本 FF：

- ⏰ **2026-06-17 Gemini 2.5 deprecating**（第一個已知實際 trigger，Aria 2026-04-25 WebFetch 確認）：屆時要遷移 Gemini 2.5 Pro/Flash → Gemini 3 GA 版本。若 Gemini 3 GA 前後 Google 釋出多版 preview 或快速迭代、需要反覆切換測試 → 啟動本 FF；若單次遷移即穩定 → 維持 constants 方案即可
- Christ 在 1-2 個月內超過 5 次「想立刻試新 Model，但要等 Aria commit + CI/CD」的抱怨
- 出現「多個 Model A/B 對比實驗」的真實使用場景（不是猜想）
- 新增 Provider（如 OpenAI / Codex）後，model 清單膨脹讓 constants 檔超過 50 個項目、維護變亂

### 設計方向（如果要做）

1. **新 Entity**：`LlmModel`
   ```csharp
   public class LlmModel
   {
       public Guid Id { get; set; }
       public string Provider { get; set; } = "";     // "Anthropic" / "Gemini"
       public string ModelName { get; set; } = "";    // "claude-sonnet-4-6"
       public string? DisplayName { get; set; }        // "Claude Sonnet 4.6"（可選，UI 顯示）
       public bool IsActive { get; set; } = true;     // 停用舊 model 不刪
       public string? Notes { get; set; }              // pricing / context window / 淘汰日期
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
   }
   ```

2. **Dashboard 管理頁**：`/system/models`（或類似路由）
   - CRUD table（list / add / edit / toggle active）
   - 權限考量（只有 localhost bypass / 或特定人能改）

3. **Aria 維護流程**：定期（每 2 週或新 Model 釋出時）WebFetch 官網 → 透過 Dashboard 或 Internal API 更新 DB → 立即生效（不等部署）

4. **Agent 設定頁 Model 下拉資料源改從 DB 讀**：取代 `LlmModels.GetModelsForProvider()`

### 技術約束

- DB 成為 single source of truth（不要再依賴 constants）
- `LlmModels.cs` 可保留作為 seed（fresh install 時把初始清單塞進 DB）
- Dashboard 存/改 Model 清單後要 invalidate Bot cache（對齊 Stage 38 的 AgentConfigService cache 模式）
- 誤刪保護：嘗試刪除「正在被某個 Agent 使用」的 Model 應該被擋下（先改該 Agent 的 Model 才能刪）

### 優先級

⚪ 待觀察 — 純 UX 優化，不解決功能缺口。Stage 38 上線後累積實際使用數據再決定。

---

## 二十七、Self-implement 試驗 v2（FF 二十四 fix 後重新評估品質）

> 狀態：🟡 中 — 等下個適合的小規模任務即可執行
> 提出日期：2026-04-25（Stage 38 結案後 Christ 反映 Stage 37 真實流程品質低下，Aria 調查報告觸發）

### 背景

Stage 37 期間 Christ 關閉 Mock Mode 跑真實 AiTeam（PR #107 self-implement 試驗）發現品質低下。Aria 2026-04-25 調查報告盤點三大主因：

1. **Top 1（純技術 bug）：`CLAUDE_Vera.md` / `CLAUDE_QA.md` 在 production 沒被 COPY 到容器** — Vera/Quinn 看到的是 repo 根的 CLAUDE.md（給 Cody/Victoria 用），等於用錯角色做事。**已在 FF 二十四（2026-04-25 修復）**。
2. **Top 2（流程設計）：`CLAUDE_Vera.md` 「偏好放行」+ Petra 對 Warning 寬鬆** — 架構繞道（重複邏輯/多份 config）天生被歸 Warning 不擋。
3. **Top 3（流程設計）：`CLAUDE_QA.md` 測試品質指引偏「量」不偏「質」** — Quinn 可能寫 dummy 測試湊數。

關鍵問題：**Stage 37 的品質觀察 50%+ 機率是 Top 1 純技術 bug 造成**，不一定是流程設計問題。要先重做試驗看 Top 1 修復是否就足夠。

### 行動計劃

在 FF 二十四 fix 後（**2026-04-25 已就緒**），下個適合的小規模任務時，故意**用 production 真實流程**（關閉 Mock）跑一次 self-implement，重新評估品質。

**任務候選**（小範圍、低風險）：
- FF 十 Dashboard UI 第四批的某個小子項（例：Switch 文字移除、Icon 按鈕替換）
- 當前累積的小 bug 修復
- **避開**：架構性重構、跨多檔的修改、有設計選擇的需求（這些是 PR #107 踩過的坑）

### 評估維度（試驗結果觀察）

| 維度 | 觀察點 | 通過標準 |
|------|-------|---------|
| **A. 程式碼品質** | Cody 寫出的設計是否合理、有沒有走快路徑 | 對照 PR #107 的繞道情境，沒重演 |
| **B. Vera 抓問題能力** | Review report 的 Critical / Warning 分級是否準確 | 至少抓到該抓的明顯問題 |
| **C. Quinn 測試覆蓋** | 測試是否驗證關鍵邏輯，不只是 dummy assertion | 抽看 1-2 個測試，邏輯有意義 |
| **D. Petra 仲裁判斷** | 是否擋下該擋的、放行該放行的 | 路由決策合理 |
| **E. 整體 PR 品質** | 是否能直接 merge 而不需要人工修補 | merge 後不需 follow-up fix |

### 結果分流

- **品質回升（Top 1 是 root cause）** → 確認 system 能力其實 OK，繼續累積 self-implement 經驗，**Top 2 / Top 3 的 prompt 強化可延後**
- **仍差（流程設計問題確認）** → 啟動 Top 2（強化 `CLAUDE_Vera.md` 加架構議題 Critical 判準）+ Top 3（強化 `CLAUDE_QA.md` 加測試品質判準）+ FF 二十五（self-implement prompt 守則），列獨立 Stage

### 不在範圍

- ❌ Top 2 / Top 3 的 prompt 強化（**等試驗結果決定**，不要先做以免無謂工作量）
- ❌ Petra 升級為「設計 PM」（需要更大架構討論）
- ❌ 自動化品質評估（人工觀察為主，不做評分自動化）

### v2 試驗結果（2026-04-25 執行完成）

✅ **試驗已執行**，任務：規則管理頁面 UI 微調（PR #108）。**詳細紀錄**：[docs/experiments/Trial_v2_RuleManagementUI.md](../experiments/Trial_v2_RuleManagementUI.md)

**結論摘要**：
- ✅ **Top 3（Quinn 測試品質）部分驗證為正** — 寫了 10 個實質的 Playwright 視覺截圖測試，不是 dummy 湊數
- ❓ **Top 1 / Top 2 本次未驗證** — 任務是純 `.razor` 改動，Vera 因 [`ReviewerAgentService.cs:108`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:108) 設計（只審 .cs）直接略過，Petra 也沒仲裁

**新發現**（觸發新 FF / 列為下個 Stage 待修）：
1. ✅ **Vera 略過純 .razor PR** — 設計缺口，FF 二十八（**Stage 39 已完成**，2026-04-25）
2. ✅ **BossInteraction.Description 缺 Task.Description**（CommandHandler.cs:195）— **Stage 39 搭車修完成**（含 SlashCommandRouter L245 第三處對稱）
3. ⏳ **Dev/Dev_plan chip vs expand 不一致** — 加為 FF 二十二 子項 B 具體案例（待 SignalR refactor 搭車）
4. ✅ **MudSwitch 移除 Label 後 a11y 缺替代描述**（PR #108 沒人擋）— **Stage 39 搭車修完成**（RuleManagement.razor 補 aria-label）
5. ✅ **Reviewer 略過時狀態錯標 `failed`** — 應為 `skipped`，**Stage 39 完成**（新增 `AgentResultType.Skipped` 結果型別 + Dashboard 全鏈路 mapping teal `#20c997`）

**試驗 v3 規劃**：需挑會動 `.cs` 檔的小工程（補 Top 1 / Top 2 驗證）。FF 二十八已修，**v3 前置條件全部就緒**，可隨時規劃。詳見 Trial_v2 紀錄末段。

### 優先級

🟡 中（v4 已完成 ✅，Trial 系列首次完整三層迴圈閉環試驗）— v2 部分完成 + v3 已執行 + v4 揭露結構性盲點；FF 二十八完全成功、Top 1 部分（→ FF 二十九）、Top 2 預期行為、Top 3 維持高品質；新增 FF 二十九 + FF 三十 + FF 三十二 + FF 三十三。

### v3 試驗結果（2026-04-25 執行完成）

✅ **試驗已執行**，任務：流程追蹤頁面 PR 欄位優化（PR #109）。**詳細紀錄**：[docs/experiments/Trial_v3_PipelinePrColumn.md](../experiments/Trial_v3_PipelinePrColumn.md)

**結論摘要**（對照 v2）：
- ✅ **FF 二十八完全成功**：Vera 啟動 razor 審查 + 高品質跨檔影響範圍分析（Vera 主動掃出 Home.razor:60-62 也有同樣 pattern 沒同步改）
- 🟡 **Top 1 部分成功**：CLAUDE_Vera.md 擴充判準有效（DRY / fallback / 慣例對齊都做得好），但**漏寫 `<a>` link a11y / `target="_blank"` 安全慣例 / 業務邏輯 pattern match 升 Warning** → 觸發 **新 FF 二十九**
- 🟢 **Top 2 預期行為**：1 Warning + 1 Info + 0 Critical → Petra 直接放行進 QA → 沒進 Appeal 迴圈。「偏好放行」哲學維持（設計意圖）
- ✅ **Top 3 維持**：Quinn xUnit + Playwright 都做、甚至 over-deliver（連既有 helper 也補測）

**新發現**（觸發新 FF）：
1. **CLAUDE_Vera.md 三處判準漏寫** → 新 FF 二十九（a11y `<a>` / `rel="noopener"` 安全 / pattern match）
2. **tech_improvement ghost Dev task** → 新 FF 三十
3. **Agent 執行確認訊息誤導**：「即將由 Dev 執行」實際先跑 Dev_plan → FF 二十二 子項 B 案例補完

**戰略結論**：審查層 CLAUDE_Vera.md 判準邊界覆蓋不全 = Stage 37 self-implement 品質低下根因，**非 Vera 失職**。執行層 / Quinn 測試 / Petra 路由都健康。

### v4 試驗結果（2026-04-27 執行完成）

✅ **試驗已執行**，任務：Dashboard 錯誤處理 UX 打磨（PR #122 **OPEN 未合併**）。**詳細紀錄**：[docs/experiments/Trial_v4_DashboardErrorUx.md](../experiments/Trial_v4_DashboardErrorUx.md)

**Trial 系列首次：**
- 完整三層品質迴圈閉環試驗（Stage 39+40+41 補強全部生效）
- 跨完整 pipeline 試驗（Kickoff → Design → Dev_plan → Petra → Dev → Vera → Petra → QA → Doc）
- Dashboard 路徑送出（vs v2/v3 走 Discord）
- 有完整成本紀錄（**$4.99 USD**）

**結論摘要**：
- 🟡 **Top 1 無從驗證**：Cody PR 範圍縮水到 41 行 Service（12 Issue 中只做 1 個），無 a11y 改動觸發點
- 🔴 **Top 2 規則對範圍縮水失效**：Stage 40 Petra Warning→blocking 升級清單沒涵蓋「PR 範圍嚴重不符計劃書」
- 🔴 **Top 3 QA failed**：測試沒跑完，無法驗 Stage 41 嚴格版

**揭露 13 個 Bug**（其中 5 個 🔴 高嚴重度）：
1. DevPlan Appeal accept 後流程沒重新產出（Cody 自承「必須重新產出」但流程沒給機會）
2. Dev fix 失敗時 Reviewer 仍啟動（Cody 沒修 code，Vera review 同一未修 PR）
3. Petra 升級規則沒涵蓋「PR 範圍縮水」（41 行 vs 預期 1-2 週工程）
4. Vera Critical 邊界 underuse（onUndo Server Circuit 斷線僅標 Warning）
5. QA 失敗仍進 Doc + TaskGroup mark done（完成判定邏輯有 bug）
6. token_logs 沒涵蓋 CLI Agent token（94% 成本沒進紀錄）
7. ... 另 7 個次要 bug 詳見 Trial 紀錄

**揭露的 Self-implement 適用邊界**（首次精準定義）：
- ✅ 適合：**小範圍純技術改動**（如 Trial_v3 PR #109，三層迴圈擋得下）
- 🔴 不適合：**跨多檔案 / 架構級重構**（如 Trial_v4 12 個 Issue，三層迴圈擋不下範圍縮水）

**新發現觸發新 FF**：
1. **FF 三十二**：Self-implement 完整性閘門（六子項解 Bug #5/#8/#9/#10/#11/#12）
2. **FF 三十三**：Token 計費機制 CLI Agent 涵蓋（Bug #13）

**戰略結論**：三層迴圈在「程式碼層面瑕疵」生效；在「self-implement 範圍縮水」失效。未來架構級重構需求應**開獨立 Stage 由 Aria 規劃 + Forge 實作**，不交 Victoria self-implement。FF 三十二 完成前不開 Trial_v5。


---

## 三十、tech_improvement 工作流的 ghost Dev task

> 狀態：🔵 低 — UI 顯示一致性問題，不影響流程正確性
> 提出日期：2026-04-25（Trial_v3 試驗發現）

### 背景

Trial_v3 觀察：tech_improvement 流程的任務列表會出現一筆 **ghost Dev task** 永遠 stuck 在「等待中」：

| 時間 | 事件 | TaskItem |
|------|------|---------|
| t+0 | Christ 按 Agent 執行確認的「執行」 | 建 **Dev (等待中, Dashboard 觸發)** ← orphan |
| t+2min | Orchestrator 啟動 tech_improvement 流程 | 另起 **Dev_plan (執行中, Orchestrator)** |
| t+5min | Dev_plan 完成 → Petra 審 → Dev 觸發 | 又建 **Dev (執行中, Orchestrator)** |
| ghost | 永遠 stuck | 第一筆 Dev TaskItem 永遠不會被消化 |

### 根因（推測）

CEO 確認 → `ShowDirectAgentConfirm` 建立初始 Dev TaskItem，但 tech_improvement workflow 的 Orchestrator **沒使用這筆 task**，另起爐灶建 Dev_plan + 後來的 Dev。可能是 Stage 25a / 25b 設計遺漏。

### 影響

- ✅ **流程本身正確**：實際 Dev 階段仍正常啟動
- ❌ **UI 一致性**：任務列表多一筆永遠 stuck 的 task，誤導使用者
- ❌ **資料正確性**：DB 有 orphan TaskItem 不會被清理

### 修法方向

- **方案 A**：Orchestrator 使用既有 Dev TaskItem（tech_improvement 進場時改 status / agent 為 Dev_plan）
- **方案 B**：CEO 確認改建 Dev_plan TaskItem（不是 Dev）

### 優先級

🔵 低 — 純 UI 顯示問題，可搭車修。Stage 40 順手做或下次清 orphan task 時做。

---

## 三十二、Self-implement 完整性閘門（Trial_v4 揭露的流程斷裂合集）

> 狀態：🔴 高 — Trial_v4 揭露 5 個 🔴 嚴重 bug + 1 個 🟡 中 bug，影響 self-implement 流程可信度
> 提出日期：2026-04-28（Trial_v4 結案揭露）

### 背景

Trial_v4（Dashboard 錯誤處理 UX 打磨）首次完整三層品質迴圈閉環試驗 + 首次完整 pipeline 試驗 + 首次有成本紀錄試驗。**TaskGroup 標 done 但實際嚴重失敗**：
- PR #122 OPEN 未合併
- Cody 只完成 12 個 Issue 中的 1 個（41 行 Service）
- 預期 1-2 週工程被壓縮成 5.5 分鐘 / 41 行
- 跨 5 個流程斷裂點 + 1 個 prompt 邊界問題

詳見 [docs/experiments/Trial_v4_DashboardErrorUx.md](../experiments/Trial_v4_DashboardErrorUx.md)。

### 戰略意義

**Stage 39+40+41 補強已建立的「Vera 審查 + Petra 閘門 + Quinn 測試」三層迴圈在「程式碼層面瑕疵」上生效**，但 Trial_v4 揭露**三層迴圈擋不下「self-implement 範圍縮水 + 流程斷裂」這類議題**。本 FF 補完六項閘門使 self-implement 流程真正可信。

### 子項

#### 子項 A：DevPlan 產出失敗的容錯機制（Bug #5，🔴 高）

**現況**：Cody Dev_plan 階段失敗（DevPlan = 「（計畫書產出失敗，請查看 log）」17 字）→ Petra 駁回 → Cody Appeal `accept`（自承「必須重新產出完整計畫書」）→ **流程沒給她重新產出的機會，直接進入 Dev 階段**。

**修法方向**：
- DevPlan Appeal `accept` 後應 trigger Cody 重跑 Dev_plan 階段
- 或新增 Dev_plan 重試機制（最多 N 次，仍失敗 → escalate Christ）

#### 子項 B：fix loop Dev 失敗時中止機制（Bug #8，🔴 高）

**現況**：第二輪 Dev fix 被 Token 守門擋下（failed）→ **第二輪 Reviewer 仍啟動 review 同一個未修的 PR** → Petra 通過 → 進 QA。

**修法方向**：
- Dev failed → 中止 fix loop → 標 TaskGroup needs_intervention 並 escalate Christ
- 不應讓 Reviewer 跑沒被修的 PR

#### 子項 C：CLAUDE_Petra.md 加「PR 範圍嚴重不符計劃書」升級規則（Bug #9，🔴 高）

**現況**：Stage 40 升級清單涵蓋程式碼層面議題（重複定義 / 硬編碼 / pattern match / `target="_blank"`），但**沒涵蓋「PR 範圍嚴重不符計劃書」**——Vera 標 Info「Phase 1 only / 9 元件未遷移」時 Petra 直接放行。

**修法方向**：
- CLAUDE_Petra.md 第 4 節 Warning→blocking 升級清單新增：「Vera 報告中明確指出『未完成計劃書多數 Issue / Phase X only』類議題視為 blocking」
- 或新增獨立 escalate 規則：「PR 完成度 < 50% 計劃書範圍 → escalate 給 Christ 確認是否分階段交付」

#### 子項 D：CLAUDE_Vera.md 強化「Server Circuit 斷線必為 Critical」（Bug #10，🟡 中）

**現況**：Vera 在 PR #122 抓到 onUndo 沒包 try/catch「**透過 MudBlazor 元件事件鏈傳播至 Blazor Server Circuit，可能造成 circuit 斷線**」—— 完全符合 Stage 40 補強的 Critical 規則「`@onclick` handler 內未處理可能拋例外的呼叫，且該例外會在 Server Circuit 內炸掉整個 connection → Critical」，但 Vera 自己保守標 Warning。

**修法方向**：
- CLAUDE_Vera.md 補入「**只要描述提到 Server Circuit 斷線可能性 → 必為 Critical 不得降級**」明確指引
- 加範例反面教材引用 PR #122 onUndo 案例

#### 子項 E：QA 失敗判定為 TaskGroup 失敗（Bug #11，🔴 高）

**現況**：QA Quinn failed → 直接進 Doc → Doc done → **TaskGroup mark done**（QaFixRound = 0 沒進 fix loop）。

**修法方向**：
- QA failed → 不該進 Doc，應 trigger Cody fix（QaFixRound 機制應啟動）
- 若 QA fix 也失敗 → TaskGroup status 應為 `failed` 或 `needs_intervention`，不該 mark done
- TaskGroup 完成判定邏輯需檢查所有階段成功才 done

#### 子項 F：Sage 對「無實作」升 escalate（Bug #12，🟡 中）

**現況**：Doc 階段 Sage 看到 DevPlan = 「產出失敗」+ 實際 PR 只 41 行新 Service，但仍寫了歸檔（含錯的 PR URL `feature/110-...` + 「實作摘要：無實作說明」）。

**修法方向**：
- CLAUDE_Sage.md 加判準：「若 DevPlan 為空 / 失敗訊息 / 顯著少於 ImplementationNote 預期內容 → 不寫歸檔，回 escalate」
- PR URL 不要 hardcode 格式，從 `TaskGroup.DevPrUrl` 直接讀

#### 子項 G：Cody 自我檢查 PR 範圍 vs DesignPlan（Trial_v4 第二維度盲點，🟡 中）

**現況揭露**：Trial_v4 中 Cody 自認 Dev 階段「完成」（Status=done + 產出 PR），但客觀上只做 12 個 Issue 中的 1 個。**沒有任何 Agent 把「Cody PR 範圍 vs DesignPlan 12 Issue」做客觀對照檢查**。

每個下游 Agent 都「各管一攤」（Vera 看程式碼瑕疵 / Petra 看 Vera 報告 / Quinn 跑測試 / Sage 寫文件），**沒人問「PR 是否完成 Plan 該做的全部？」**。

子項 C/D/F 都是「下游被動發現」，缺**「Cody 主動自我檢查」這條主動機制**。

**修法方向**（純 prompt + 下游 Vera/Petra 一條判讀規則）：

CLAUDE_Cody.md 補入「Dev 階段結束時的自我檢查（強制要求）」段：

```markdown
## Dev 階段結束時的自我檢查（強制要求）

PR 提交前，**必須**在 PR description 列出：

### ✅ 已完成 Issue
- Issue #N1：<檔案 / commit hash>
- Issue #N2：<檔案 / commit hash>

### ❌ 未完成 Issue（含原因）
- Issue #N3：<理由 — 如：超出範圍 / 預期 Phase 2 處理 / 探索後發現不需要>

### 完成度判定
- 完成 X / Y Issue（X% 完成度）
- **若 < 80% 完成度 → 必須在 PR description 標記 `⚠️ ESCALATE_NEEDED`**

不允許「跳過自我檢查」/「列出但無理由」/「假裝全做」。
```

**下游配合**（Vera + Petra 加一條判讀規則）：
- Vera review prompt 補：「若 PR description 含 `⚠️ ESCALATE_NEEDED` → 必標 Critical 議題」
- Petra Warning→blocking 升級規則補：「PR description 含 `⚠️ ESCALATE_NEEDED` → 直接 escalate（不走 fix loop）」

**這個機制 0 Orchestrator 改動**——純 prompt 補強。

### 規模 / 風險

**規模**：M-L（七子項合計約 8-14 工作天）
**風險**：中（每個子項獨立但都動 prompt 或 Orchestrator 流程，可能引發既有流程行為變化）

### 子項分類與 Stage 排序建議

子項按「動什麼」分為兩類：

**Prompt 補強類（4 子項，輕量，動 CLAUDE_*.md + 重啟容器即生效）**：
- 子項 C：CLAUDE_Petra.md 加範圍縮水升級規則
- 子項 D：CLAUDE_Vera.md 強化 Server Circuit Critical 邊界
- 子項 F：CLAUDE_Sage.md 加無實作 escalate 規則
- 子項 G：CLAUDE_Cody.md 加 PR 自我檢查 + Vera/Petra 加 ESCALATE_NEEDED 判讀

**Orchestrator 改動類（3 子項，重，動核心流程 + DB schema）**：
- 子項 A：DevPlan Appeal accept 後 trigger 重新產出
- 子項 B：Dev fix 失敗時中止 fix loop（不啟動 Reviewer）
- 子項 E：QA 失敗判定為 TaskGroup 失敗（不進 Doc）

**Stage 排序建議**：
- **Stage 42**：Prompt 補強類 4 子項（C+D+F+G，性質一致 + 風險低 + 立刻見效）
- **Stage 43**：Orchestrator 改動類 3 子項（A+B+E，動核心流程 + 風險高，建議與 FF 三十四 排序時評估是否合併）
- **兩 Stage 完成後才開 Trial_v5**

### 替代方案：Petra 升級為 Claude Code CLI 審核 Vera report（暫不採納）

**Christ 2026-04-28 拍板採 Y（純 prompt 補強）路線**，原因：
1. Bug #9 真實根因不是「Petra 看不到」（Vera Info 已明寫「Phase 1 only / 9 元件未遷移」），而是「Petra 不知道該擋」 → 改 prompt 比改機制有效
2. 系統設計初衷：Vera 是查證者 / Petra 是路由者，分工避免重複工作
3. 成本與速度 trade-off（Claude CLI 每輪 +$0.10-0.30 + 1-3 分鐘）

**Christ 註記**：「**至少依目前的架構/設計/狀態**，的確是不需要的」——保留未來重評估的後門。

**未來重評估觸發條件**：
- Trial_v5/v6 跑完發現 prompt 補強仍擋不下範圍縮水
- AiTeam 架構有重大變化（如多專案 / Petra 角色升級）
- 「獨立第二意見」需求出現（不只看 Vera 結論）

若觸發 → 立新 FF 三十六「Petra 審查層升級為 Claude Code CLI」獨立評估。

### 優先級

🔴 高 — Trial_v4 揭露 5 個 🔴 嚴重 bug 都在這 FF 內。**FF 三十二完成前不開 Trial_v5**——否則同樣問題會重演。

---

## 三十三、Token 計費機制 CLI Agent 涵蓋

> 狀態：🔴 高 — Trial_v4 揭露 token_logs 只涵蓋 6% 成本
> 提出日期：2026-04-28（Trial_v4 成本分析揭露）

### 背景

Trial_v4 首次有完整 API 成本紀錄（**$4.99 USD**），但對照 token_logs：
- 紀錄：Dev 18,158 + PM 3,496 = **21,654 tokens**
- 對應 Sonnet 約 $0.06-0.32 成本
- **只能解釋 ~6% 的實際成本**

意味著 **94% 成本走 CLI Agent**（Vera/Quinn/Sage/Kickoff/Design/Dev_plan）但**沒進 token_logs**。

### 影響

1. **Stage 22 Token 守門精準度問題**：守門邏輯依賴 token_logs 計算日累積。CLI Agent 沒進紀錄 → 守門無法有效擋下 CLI Agent 過度消耗。
2. **FF 一（API 費用優化）失去資料基礎**：無法分析「哪個 Agent / 哪個階段最燒錢」進行優化。
3. **Trial_v4 觀察揭露**：Doc 階段跑 13:30 異常長（最長階段），但無 token 數據佐證根因。

### 修法方向

#### 1. Claude Code CLI session 結束時的 token 計費

Claude Code subprocess 結束時應輸出 token usage（input/output/cache）。需要：
- 修改 `ClaudeCodeService` subprocess 處理邏輯，capture session 結束的 token 摘要
- 寫入 token_logs 時 AgentName 標 `Vera` / `Quinn` / `Cody` / `Petra` 等真 Agent
- 對 Kickoff / Design / Dev_plan 等多 Agent 階段，需區分各 Agent 個別用量

#### 2. token_logs schema 補欄位

可能需要新增：
- `Stage`（Kickoff / Design / Dev_plan / Dev / Reviewer / QA / Doc）
- `Round`（會議 / fix iteration 輪次）
- `CacheReadTokens` / `CacheWriteTokens`（Stage 1 已支援 prompt cache，但目前 token_logs 沒分）

#### 3. Stage 22 Token 守門邏輯升級

當前守門用 `SUM(InputTokens + OutputTokens)`，應改為：
- 含 cache token（cache_read 算 0.1x cost）
- 對「會議型 Agent」可能要設不同日限（多 Agent 會議天然 token 消耗高）

### 戰略意義

**FF 三十三 是 FF 一（API 費用優化）的前置條件**——沒有完整 token 涵蓋，無法做有意義的成本優化。

### 規模 / 風險

**規模**：M（CLI subprocess 處理 + token_logs schema migration + 守門邏輯升級）
**風險**：中（動到多個 CLI Agent 共用機制，需充分測試）

### 優先級

🔴 高 — 影響 Token 守門精準度 + FF 一 資料基礎。建議與 FF 三十二 並行（兩者性質不同，可獨立 Stage 進行）。

---

## 三十四、TaskGroup 流程暫停機制

> 狀態：🟡 中 — Trial_v4 觀察期間 Christ 提出
> 提出日期：2026-04-28（Trial_v4 進行中觀察 Kickoff 後等 8h 才繼續、流程進到一半發現走偏無法暫停等場景）

### 背景

目前 AiTeam 已有兩種暫停機制，但**缺 TaskGroup 層級**：

| 層級 | 已存在 | 場景 |
|------|------|------|
| **Agent 層級 pause**（Stage 27b）| ✅ `/pause Cody` / Dashboard pause/resume 按鈕 | 暫停 Cody 整個 Agent（不接新任務）|
| **全域緊急停止**（Stage 33）| ✅ 全部暫停 / 全部恢復 | 一鍵全停 |
| **TaskGroup 層級 pause** | ❌ **目前沒有** | 暫停**某個任務的流程**（不影響其他任務）|

**真實使用場景**（Trial_v4 觀察）：
1. **跨階段間暫停**：Kickoff 結束後等老闆按繼續 8 小時，期間如果想插入「讓系統暫停別動」（例如下班 / 想等明天再繼續）→ 目前無此選項
2. **流程走偏的即時干預**：看到 Cody Dev 階段邊跑邊發現走偏 → 立即暫停（vs 全 stop / vs 等跑完再失敗）
3. **等外部條件**：流程跑到一半發現需要等什麼（例如想等 Christ 看完文件才繼續）→ 暫停而非取消

### 設計層面要考慮的三個點（Aria 2026-04-28 初步提出）

#### 1. 暫停粒度

候選方案：
- **A. TaskGroup 級別**：整個任務的流程都暫停（最粗）—— UI 簡單，但若任務跑到 Doc 階段才想暫停 Doc 不影響前面已完成階段，難以區分
- **B. Stage 階段級別**：暫停「下個階段啟動」（卡在當前階段間）—— 最自然，符合 Trial_v4 觀察的「跨階段間暫停」場景
- **C. Task 級別**：暫停某個進行中的 Task（例：暫停當前 Reviewer 跑到一半的 task）—— 最細，但 Cody 已 fork subprocess 可能無法中途乾淨暫停

**Aria 初判**：B 是 sweet spot——粒度清楚 + 符合大多場景 + 實作可控（在 Orchestrator 階段轉換點檢查 pause flag）。C 留作 Phase 2 進階功能。

#### 2. 暫停動作

候選機制：
- **a. 阻擋下一階段啟動**：當前 task 跑完才生效（passive）—— 安全但有延遲
- **b. Kill 當前 running task**：立刻終止 Cody subprocess（active）—— 快但有副作用（subprocess kill 可能 leak / 已寫一半 commit / partial PR push）
- **c. 兩者皆可（按鈕區分）**：「暫停在當前階段結束後」+「立即取消當前 + 暫停」

**Aria 初判**：先做 a（被動暫停），c 留作後續評估。理由：a 簡單可靠，b 風險高（Stage 14 雖然有 cancel kill 機制但用在「取消任務」場景，不是「暫停後恢復」場景）。

#### 3. 恢復機制（從哪個 checkpoint 繼續）

關鍵問題：
- 暫停在 Kickoff 結束 → 恢復：直接進 Design ✅
- 暫停在 Dev 階段（被動）→ Dev 跑完後等待 → 恢復：進 Reviewer ✅
- 暫停在 Dev 階段（主動 kill）→ 部分 commit / partial PR → 恢復：**從哪繼續？**重跑 Dev？接受半成品繼續？這個 case 是難題

**Aria 初判**：搭配上面 #2 a 機制（被動暫停）→ 恢復機制簡單（從下個階段繼續）。如果做 b（主動 kill），需要設計「rewind to last checkpoint」邏輯（複雜度高）。

### 與既有機制的關係

- **Stage 27b Agent pause**：用來暫停 Agent 不接新任務，不影響當前在跑的 task。本 FF 的「TaskGroup pause」性質不同，是**暫停某個任務流程的進展**。兩者可並存：Cody pause 中 + TaskGroup A 暫停 + TaskGroup B 也暫停。
- **Stage 33 緊急停止**：全域 emergency stop。本 FF 是更精準的單任務 pause。
- **Crash Recovery（Stage 31/37）**：Bot 重啟後恢復被中斷的 orchestration。**暫停機制要與 Crash Recovery 對齊**——pause 狀態不該被誤認為 stuck 而 auto-recover。

### 規模 / 風險

**規模**：M-L
- 動 Orchestrator 流程（在階段轉換點檢查 pause flag）
- AgentQueueProcessor 對 paused TaskGroup 不消費下一階段
- DB schema：`task_groups` 加 `IsPaused` / `PausedAt` / `PausedBy` 欄位 + Migration
- Dashboard UI：流程追蹤頁 + Drawer 加暫停 / 恢復按鈕
- Crash Recovery 邏輯要排除 paused TaskGroup

**風險**：中
- 動 Orchestrator 核心流程容易引發既有行為變化
- 跨 Crash Recovery / 等待類互動（BossInteraction）/ Appeal flow 多處交互
- 主動 kill（如果做）對 Cody subprocess 影響需充分測試

### 優先級

🟡 中 — Trial_v4 揭露的真實 UX 痛點（等 8h 期間想暫停無選項），但**非阻擋現有功能**。建議與 FF 三十二 / 三十三 排序時評估，三選二 / 三選三的決策由 Christ 拍板。

---

## 三十五、自動拆任務機制（Petra 在 Design 階段 propose 拆 sub-task）

> 狀態：⭐ **戰略級 FF** — 解 Trial_v4 揭露的「self-implement 範圍縮水」根因
> 提出日期：2026-04-28（Trial_v4 結案後 Christ + Aria 深度討論）
> 前置條件：**FF 三十二（完整七子項）+ FF 三十三 必先完成**

### 背景

Trial_v4 揭露：**Cody 對大需求一定縮水**。
- 預期 12 個 Issue（1-2 週工程量）→ 實際做 1 個 Issue（41 行 Service / 5.5 分鐘）
- 三層迴圈（Vera + Petra + Quinn）擋不下「PR 範圍嚴重不符計劃書」議題

對照 real-world 團隊：大需求進來會被 PM 拆 epic → user stories（每個獨立 PR）→ 用 milestone / sprint 組織進度。

**AiTeam 缺的不是「拆解能力」**（Rosa 已會拆 Issue）**，是「拆解後分多個獨立 task / 多個 PR」**。

### 戰略意義

**這可能是 AiTeam 從「玩具系統」升級到「真實開發團隊」的關鍵躍進**：
- 解 Trial_v4 self-implement 範圍縮水根因
- 對齊 real-world 團隊運作模式
- 配合 FF 三十二 完整性閘門 → self-implement 真正可信

### 設計方案：Design 階段 Petra propose 拆（B 階段攔截）

**為什麼選 Petra（不選 Victoria）**：
- Petra（PM 視角）= 看專案內 Issue 拆解 → 適合單專案內拆 task
- Victoria（CEO 視角）= 看公司多專案全貌 → 適合跨專案拆 task（Phase 2 擴充）

**為什麼選 Design 階段（不選 Kickoff 結束）**：
- Kickoff 階段沒拆 Issue（Rosa 在 Design 階段才拆）
- Petra 必須等看完 Rosa 拆出的精確 Issue 數量才能精準提拆 task
- Design 階段 Petra 綜合整理時 = Issue 拆解 + 五人風險識別都已完成 → 拆 task 提案最精準

**完整流程**：

```
Kickoff（5 人 × N 輪）→ 產出方向 / 決策 / 風險（無 Issue 拆解）
  ↓
Christ 按繼續
  ↓
Design 階段：
  Petra 前置判斷需求 → Rosa 拆 Issue → Demi UI 評估
  → Round 1 五人發言（Cody grep / Quinn 揭露結構陷阱）
  → Petra 綜合整理時判斷：
     - Issue 數 ≥ 閾值（如 8 個）or
     - 跨多 P0/P1/P2 階段 or
     - 預估改動行數 ≥ N 行
  → 觸發拆 task 提案
  ↓
卡片 1：「設計規劃確認」（既有）
  顯示：DesignPlan 全文
  按鈕：[繼續] [修改] [停止] [重開會議]
  ↓ 按 [繼續]
卡片 2：「Petra 建議拆任務」（新增）
  顯示：
    Phase 1（基礎）：Issue 2 → 預估 X 分鐘
    Phase 2（遷移）：Issue 3-9 → 預估 Y 分鐘
    Phase 3（收尾）：Issue 10-12 → 預估 Z 分鐘
  按鈕：
    [採納 Petra 方案] → 自動建 N 個依賴 sub-task
    [修改方案]      → 開文字輸入卡讓 Christ 改
    [不拆繼續原樣]   → 退回單一 task 流程
    [停止]          → 取消整個任務
```

若 Petra 判定不需拆（Issue < 閾值）→ 卡片 2 不出現，直接進 Dev_plan。

### 設計細節（Christ + Aria 已拍板）

#### 細節 1：兩段確認（Christ 採納 C）
- 卡片 1（DesignPlan 確認）+ 卡片 2（拆 task 確認）分開
- 老闆有完整資訊：先看設計品質，再看拆解策略

#### 細節 2：sub-task 共享 Phase 1 Kickoff/Design（Christ 採納 B）
- sub-task 不重跑 Kickoff/Design（成本省 ×N 倍）
- 各自跑 Dev_plan + Petra + Dev + Reviewer + Petra + QA + Doc + PR
- 共享 parent TaskGroup 的 Kickoff/Design 結論

#### 細節 3：依賴鏈順序（Aria 推薦 (a) Sequential）
- Phase 1 PR merged → Phase 2 啟動 → Phase 2 PR merged → Phase 3 啟動
- 理由：Cody subprocess 一次只能跑一個 + Phase 2 通常依賴 Phase 1 產出
- 平行 (Parallel) 留作未來進階

#### 細節 4：sub-task PR 策略（Aria 推薦 (a) 各自獨立 PR）
- `main` → `feature/phase1` → PR → merged → Phase 2 從 main rebase 出新 branch
- 跟 AiTeam 既有「一 task 一 PR」設計對齊
- Vera review 範圍清楚 / GitHub Issues link 乾淨

#### 細節 5：epic 機制（Aria 推薦 (b) DB 內表達）
- `task_groups` 加 `ParentGroupId` / `SplitFromGroupId` 欄位 + Migration
- Dashboard UI 顯示 epic 進度卡（聚合 sub-task 進度）
- 不依賴 GitHub Milestone（避免外部依賴）

#### 細節 6：sub-task Dev_plan 失敗 → **鎖死前置條件**
- FF 三十二完整七子項（含子項 G）必先完成
- FF 三十三（Token 計費 CLI Agent 涵蓋）必先完成
- 否則拆 N 個 sub-task → 任一個踩 Trial_v4 同樣 bug = 風險加總

### 多專案場景擴充（Phase 2，未來真正多專案後做）

**A 階段攔截：Victoria 跨專案拆**

當 AiTeam 真的開始管多個專案時：

```
Christ 提需求：「AiTeam 跟 ClientApp 兩專案都加雙因素認證」
  ↓
Victoria（CEO 視角看公司）智慧分類時判斷：
  「這需求跨 AiTeam + ClientApp 兩專案」
  → 拆兩個 parent TaskGroup：
    - Task A：AiTeam 加 2FA（指派 AiTeam 團隊）
    - Task B：ClientApp 加 2FA（指派 ClientApp 團隊）
  ↓
各 parent TaskGroup 進入自己專案的 Kickoff/Design
  → Petra 在 Design 階段可再決定要不要拆 sub-task（B 階段）
```

**等 AiTeam 真的支援多專案時才做這層**——目前 AiTeam 主要管自己的 repo，多專案是未來願景，不是當下需求。

### 還未拍板的設計細節（Stage 規劃時討論）

1. **拆 task 智慧度**：
   - (a) LLM 判斷（Petra prompt 給判準）
   - (b) 固定規則（Issue ≥ 8 → 自動拆）
   - (c) 兩者並存（規則先過濾，LLM 細化拆法）
   
2. **失敗處理**：
   - sub-task 失敗 → 後續 sub-task 不啟動還是獨立繼續？
   - epic 整體狀態：partial done / pending / failed？

3. **Mock 模式對應**：
   - `/mock` 怎麼模擬「拆 task → N 個 sub-task pipeline」場景？
   - 至少要驗證拆任務確認卡顯示 + 依賴鏈執行

4. **Dashboard UI 設計**：
   - 流程追蹤頁怎麼顯示「epic 進度卡」+ sub-task 進度？
   - 跟 FF 三十四（流程暫停）整合：暫停 sub-task 還是整個 epic？

### 規模 / 風險

**規模**：**L**
- 動 Orchestrator 核心（依賴鏈執行邏輯 + Petra 拆 task 提案）
- DB schema migration（`task_groups` 加 `ParentGroupId` / `SplitFromGroupId`）
- Dashboard UI 多處（兩段確認卡 + epic 進度 + sub-task 可視化）
- Discord 互動（拆任務確認）
- CLAUDE_Petra.md 拆 task 判準補強
- Mock 模式對應
- 跟 FF 三十二 / 三十三 / 三十四 全交互

**風險**：**中-高**
- 動核心流程改變既有行為
- 跨多個既有 FF 互動（一處錯影響面廣）
- Phase 2 多專案支援設計需另開研究

### 優先級

⭐ **戰略級 🔴 高** — 解 Trial_v4 self-implement 範圍縮水根因 + 對齊 real-world 團隊模式

**前置條件鎖死**：
- 必先 FF 三十二 完整七子項（**Stage 42 + 43**）
- 必先 FF 三十三（**Stage 44 或與 32 並行**）
- 然後才開 FF 三十五（**Stage 45+**）

---

## 已完成項目摘要

以下項目已在對應 Stage 完成或因架構演進而不再需要，從本清單移除。詳細內容請參閱各 Stage 的 Roadmap 文件。

| 原編號 | 項目 | 完成方式 |
|--------|------|---------|
| 一 | Dev Agent 使用 Claude Code 寫程式 | ✅ Stage 11（2026-04-05） |
| ~~十~~ | 提案草稿 UI 規格孤立檔案自動清理 | 已不需要 — Stage 12 UI 規格改存 DB 後不再孤立 |
| 十一 | Dashboard 任務詳情顯示修正 | ✅ Stage 13（2026-04-06） |
| ~~十二~~ | Dev Agent 框架幻覺防護 | 已不需要 — Stage 11 Claude Code + Stage 12 唯讀探索後不再發生 |
| 十三 | Stage 10 技術債清償（6 項） | ✅ Stage 13（2026-04-06） |
| 十四 | Orchestrator 流程重構（5 個問題） | ✅ Stage 12 解決問題二三四 + Stage 13 解決問題一五 |
| 十五 | CEO 分類與流程完整性補強 | ✅ Stage 14（2026-04-06） |
| 十六 | CEO Discord 文件記錄能力 | ⇒ Stage 15 吸收 — Victoria 接上 Claude Code 後自行解決 |
| 十七 | UI 規格改存 DB | ✅ Stage 12（2026-04-06） |
| 十八 | Agent 唯讀探索能力 | ✅ Stage 12（2026-04-06） |
| 十九 | 提案流程重新設計 | ✅ Stage 12（2026-04-06） |
| 二十 | Victoria 升級為技術顧問 | ✅ Stage 15（2026-04-07） |
| 二十一 | Dashboard Agent 狀態卡即時更新 | ✅ Stage 18（2026-04-09） |
| 二十二 | 任務流程可視化（Pipeline View） | ✅ Stage 18（2026-04-09） |
| ~~MCP 整合~~ | MCP 標準化工具協議 | 移除 — Agent 透過 Claude Code CLI 操作，已有成熟直接串接，MCP 多一層抽象無實際好處 |
| ~~顧問 Agent 設計~~ | Victoria 顧問能力強化 | 移除 — Stage 15 Victoria 已具備顧問能力，Aria 負責深度架構討論，剩餘強化項目不值得獨立 Feature |
| ~~Doc Agent 品質控管~~ | Documentation Agent 審查機制 | 移除 — 被八（開發流程重構）+ Stage 16 Petra 審核閘門吸收 |
| ~~API 餘額恢復~~ | API 餘額耗盡後的流程恢復 | 移除 — 被十（Agent 任務序列）Error 狀態 + Dashboard 手動重試機制完全吸收 |
| 四 | Discord #指令中心 頻道移除 | ✅ Stage 22（2026-04-12）— 程式碼已清除，Christ 需手動刪除 Discord 頻道 |
| 七 | Token 異常消耗保護機制 | ✅ Stage 22（2026-04-12）— TokenTrackingProvider 四道關卡（單次/日/月/全域） |
| 九 | Dashboard 存取分層（localhost 免登入） | ✅ Stage 22（2026-04-12）— LocalhostBypassMiddleware + Docker port 收緊 |
| 十五 | 版本號集中管理（Directory.Build.props） | ✅ Stage 26（2026-04-14）— src/Directory.Build.props 集中四項版本屬性，Bot/Dashboard csproj 移除個別 Version 標籤，CI/CD + CLAUDE.md 同步更新 |
| 十（核心） | Agent 任務序列（Per-Agent Queue + 狀態管理 + Dashboard 視覺化） | ✅ Stage 27a（v3.12.0）+ Stage 27b（v3.13.0）— DB-as-Queue、AgentQueueProcessor、Agent 狀態管理（Active/Paused/Stopped）、Discord 五指令、Dashboard 佇列視覺化 |
| 九 | Dashboard 雙向操作中心（Discord + Dashboard 雙通道） | ✅ Stage 28a（v3.14.0）+ Stage 28b（v3.15.0）— BossInteraction Entity、8 個確認點 pure additive 寫入、樂觀鎖先到先贏、InteractionProcessor 輪詢、操作中心 /interactions、文字輸入互動（修改意見）、歷史紀錄篩選 |
| 零 | Dashboard 歸檔報告折疊面板 | ✅ Stage 29-1（v3.16.0）— `TaskGroup.ArchiveContent` 欄位 + EF Migration、DocAgentService 完成後寫入 DB、PipelineView 第七折疊面板、MockMode 也補寫供 Dashboard 驗收 |
| 零-A | 任務列表右側 Log 顯示統一化 | ✅ Stage 29-2（v3.16.0）— Kickoff/Design/Petra 補 running + done/failed、Dev/QA/Reviewer/Doc MockMode 統一由 Processor 寫 final done、消除過時 running 殘影 |
| 零-C | 通知類互動卡在待處理區無法消化（Bug） | ✅ Stage 29-5 搭車修（v3.16.0）— `InteractionService.NotifyActionsJson`「我知道了」按鈕；`merge_notify` / `intervention` / `ceo_reply` 三處統一套用；`ProcessBossResponseAsync` default 分支無動作（純 UI 確認） |
| 零-B | MockMode 新提案流程產生重複 TaskGroup（Bug） | ✅ v3.16.1 hotfix（2026-04-19）— Stage 28b 驗收修正只做了一半：`ExecuteProposalApprovedAsync`（Discord 路徑）有 GroupId 防護，但 `ProcessProposalApprovedAsync`（Dashboard 路徑）無條件 `CreateGroupAsync`。兩處對齊後 MockMode 從 Discord 或 Dashboard 核准提案都只產生 1 個 TaskGroup |
| 十七 | 可靠性補強：失敗重試 + 會議 Crash Recovery | ✅ Stage 31（v3.18.0，2026-04-20）— A. Dashboard「🔁 重試」按鈕（failed/cancelled TaskItem → AgentQueueService.RequeueTaskAsync → Bot internal API）；B. 會議 Crash Recovery（TaskGroup.ActiveMeetingType 欄位 + EF Migration + RunKickoffMeetingAndWaitAsync/RunDesignPhaseAsync set/finally clear + RecoverStuckMeetingsAsync 啟動掃描，採方案 2 不動執行模型） |
| 十八 | Appeal 對抗紀錄 UI 呈現 | ✅ Stage 31（v3.18.0，2026-04-20）— TaskGroupDto 擴充 4 欄位（ReviewAppealLog/ReviewAppealRoundA/DevPlanAppealLog/DevPlanAppealRoundA）；DashboardTaskService 三個 Select 補 mapping；PipelineView 新增兩個「🗣️ Appeal 對抗紀錄」折疊面板（有資料才顯示） |
| 十五 | Dashboard 與 Discord 功能平等（Feature Parity）| ✅ Stage 32（v3.19.0，`/mock` Dashboard 化 — `MockScenarioService` 抽出共用）+ Stage 33（v3.20.0，佇列控制 Dashboard 化 — `AgentQueueControlService` + Agent 狀態卡 pause/resume 按鈕 + `GlobalQueueControlCard` 全域緊急停止 + 確認 Dialog）— Discord 原指令透過薄 wrapper 共用同一 shared service |
| 二十一 | Agent 狀態卡 expand 展開看待辦清單 | ✅ Stage 33（v3.20.0，2026-04-22）— 採解讀 B（running + queued TaskItem）；實作優化為擴充既有 `AgentQueueDto`（`CurrentTaskId` / `CurrentTaskGroupId` / `CurrentTaskQueuedAt` + `QueuedTaskItemDto.GroupId`）取代原規劃 `AgentTodoDto`，重用既有 `PushQueueUpdateAsync` 鏈路；點 item 深層連結 `?groupId=` 自動 Drawer 預選 |
| 二十四 | `CLAUDE_*.md` template 補齊（CLI Agent 自我描述檔） | ✅ 2026-04-25 — 根因修正：`CLAUDE_Vera.md` / `CLAUDE_QA.md` 兩檔其實已在 repo source 存在（2026-04-12/13），但 `AiTeam.Bot.csproj` 只顯式 Include `CLAUDE_CODY.md` + `CLAUDE_Victoria.md`，其他 6 個 template 未 COPY 到 output；修法採 glob pattern `Resources\CLAUDE_*.md` 一次涵蓋全 8 檔（未來新增 template 自動涵蓋）；Rosa/Demi/Sage/Petra 沒 warn 只因走 API layer 或該 CLI path 沒被觸發，但 agent code 都有讀 template 的邏輯——一次全修 |
| 二十 | 大檔案拆解技術債（合集）— 四個怪物級檔案清零 | ✅ Stage 34（v3.21.0，MeetingService 1415→4 檔）+ Stage 35（v3.22.0，PmAgentService 1388→6 檔 + Agents/Pm/ 子資料夾）+ Stage 36（v3.23.0，TaskGroupService 2623→716、CommandHandler 2172→556 + 5 個子資料夾）— 累積六項拆解 SOP 已整理至 [docs/conventions/refactor-sop.md](../conventions/refactor-sop.md)；Stage 36 結案 changelog 寫了「主體刪除移入已完成摘要」但漏實際搬遷，Stage 38 結案後 Aria 補完 |
| 二十八 | Vera 審查範圍擴及 .razor / .css（對齊 Quinn `hasUiChanges`）+ Reviewer 略過狀態正名為 `skipped` | ✅ Stage 39（v3.26.0，2026-04-25）— `ReviewerAgentService` 三類檔案分類 + `BuildClaudeCodeReviewPrompt` 三段共用 `AppendFileDiff` helper + 標題改名「PR 變更 diff」；CLAUDE_Vera.md 擴充 a11y / Blazor / CSS / MudBlazor 判準（全 Warning，維持「偏好放行」哲學）；新增 `AgentResultType.Skipped` 結果型別 + `AgentExecutionResult.Skipped(reason)` 工廠（QA 共用）+ Dashboard 全鏈路 mapping `skipped` teal `#20c997`；TaskGroupService Reviewer Skipped 走「跳過 Petra 放行」路徑；搭車順手解決 Trial_v2 觀察四項（BossInteraction.Description 補 Task.Description / RuleManagement.razor MudSwitch aria-label / QA Skipped 共用 API / Mock review_skipped 情境） |
| 二十九 | CLAUDE_Vera.md 判準補強（a11y `<a>` link + `target="_blank"` 安全 + 業務邏輯 pattern match）| ✅ Stage 40（v3.27.0，2026-04-26）— CLAUDE_Vera.md 三段判準擴充：**安全 Critical**（`<a target="_blank">` / `<MudLink Target="_blank">` 缺 `rel="noopener"` 列為 Critical，OWASP 真實安全；與 `@onclick` 未捕例外並列「唯二 Critical razor 議題」）+ **a11y Warning**（`<a>` / `<MudLink>` 缺 `aria-label` 補入既有 a11y 子段）+ **業務邏輯 pattern match Warning**（`Title.Contains("[XXX]")` 等 fragile 設計，建議改 DTO 欄位 / service / 列舉）；搭車修 4 處同源 tabnabbing（PipelineList:65 + PipelineView:32 + Home:62 + ProjectManagement:81）作為 FF 二十九判準的真實演習場；Home.razor PR 連結同步顯示 `#編號`（原寫死「PR」）；ExtractPrNumber 抽 `Helpers/PrNumberHelper.cs` 共用 helper（消 PipelineList vs PipelineView 重複）；MudLink rel/aria-label 採 inline 寫法成功 forward 至底層 `<a>` |
| 三十一 | tests/Generated/ 編譯與執行修復（測試品質保證迴圈第三層）| ✅ Stage 41（v3.28.0，2026-04-27）— 建 `tests/AiTeam.Tests.Generated/` xUnit csproj（與 `tests/Generated/` 隔離保持純 LLM 產出區，採 `<Compile Include="..\Generated\**\*.cs" />` 跨資料夾納入）+ AiTeam.slnx 新增 `/tests/` folder；清檔案層 3 類破損：3 檔 markdown fence（GitHubServiceTests / TaskCenter）+ 1 檔幻覺類別整檔刪（AgentSettings.razorTests.cs）+ 1 檔 LLM 截斷修復（GitHubServiceTests 半截 method）；連帶清除 repo 根目錄 stray `AiTeam/` 資料夾（同源 LLM 污染歷史殘留 PR `1979f46`）；MainLayout broken test 兩個依嚴格版「不為綠燈寫假斷言」原則刪除（Quinn 假設 Assembly.GetExecutingAssembly() 取錯 + 忽略 +commitHash 剝離邏輯）；CLAUDE_Quinn.md 三段補強（valid C# 規則 + 測試標的存在性驗證嚴格版要求 grep 證據 comment + JSON schema 新增 `unverifiable_targets` 欄位語意分離 failed_tests=可修 vs unverifiable=需 escalate）；最終 dotnet test 127 Passed / 0 Failed |
---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-01 | 初版建立（原為 Stage_7_Roadmap.md） |
| 2026-04-02 | 改版為 Future_Feature.md，與正式 Stage 7 分離 |
| 2026-04-02 | 新增多項功能候選（QA Playwright、Ops CI/CD 等） |
| 2026-04-03 | 多次整理：移除已移入 Stage 8/9 的項目、重新編號 |
| 2026-04-04 | 大量新增：十二～十六（框架幻覺、技術債、Orchestrator、CEO 補強、文件記錄） |
| 2026-04-04 | 新增十七（UI 規格存 DB）；第一條升為 🔴 排入 Stage 11 |
| 2026-04-05 | 新增十八（唯讀探索）、十九（提案流程重設計）；十七/十八/十九 標記移入 Stage 12 |
| 2026-04-06 | 新增二十（Victoria 技術顧問）、二十一（Dashboard Agent 狀態卡） |
| 2026-04-06 | v2.0 大整理：移除 9 個已完成項目（一、十～十四、十七～十九），重新編號為一～十二 |
| 2026-04-06 | v2.1：第九項（CEO 分類補強）移入 Stage 14 |
| 2026-04-06 | v2.2：第九項標記 ✅ 已完成（Stage 14 驗收通過） |
| 2026-04-06 | v2.3：第十項標記被 Stage 15 吸收；第十一項 Phase 1~2 移入 Stage 15 |
| 2026-04-06 | v2.4：第十一項改為 Phase 1~3 全部移入 Stage 15；新增十三（CEO 長期記憶向量搜索版備案）；原十二改編號為十四 |
| 2026-04-07 | v3.0：移除 3 個已完成項目（九/十/十一→Stage 14/15），重新編號為一～十；更新一（API 費用已部分優化）、四（顧問能力已整合）、八（補充 Claude Code 綁定限制） |
| 2026-04-07 | v3.1：新增十一（任務流程可視化 Pipeline View — MudStepper + MudTimeline） |
| 2026-04-07 | v3.2：新增十四（測試環境隔離 — Docker Compose Test Stack，解決 CI 打到 production 問題） |
| 2026-04-08 | v3.3：十（Agent 狀態卡即時更新）和十一（Pipeline View）標記移入 Stage 18 |
| 2026-04-11 | v4.0：移除十（Agent 狀態卡）和十一（Pipeline View）至已完成摘要；剩餘項目重新編號為一～十二 |
| 2026-04-11 | v4.1：新增十三（Agent 對抗與糾錯機制）— 四個機制：申訴、熔斷、循環偵測、新鮮視角 |
| 2026-04-11 | v4.2：擴充十三機制一申訴機制 — 新增 Pre-flight Objection 模式，每個 handoff 點都可質疑上游輸入 |
| 2026-04-12 | v4.3 ~ v4.10：新增十四～十七、十補充討論結論、十二降級、十被十七吸收（詳見各項內容） |
| 2026-04-12 | v5.0：全盤整理 — 移除 4 項（MCP 整合、顧問 Agent、Doc 品質控管、API 餘額恢復），重新編號為一～十三 |
| 2026-04-12 | v5.1：新增十四（Dashboard UI 第四批細節打磨 — 系統設定獨立頁面 + 任務列表/流程追蹤篩選增強 + PR 連結顯示 + 專案管理/規則管理 Switch 精簡 + 規則管理操作欄改圖示按鈕） |
| 2026-04-12 | v5.2：新增十五（Dashboard 可調整 Token 守門全域限額 — 系統設定 UI 入口 + AppSettings 動態存取，不需重啟容器） |
| 2026-04-12 | v5.3：十一全面重構 — 從「Agent 對抗與糾錯機制」升級為「開發流程重構（多人會議制 + 糾錯機制）」；Pre-flight Objection 被 Kick-off 會議取代；熔斷確認已由 Petra 覆蓋；新增第一階段（需求計劃）和第二階段（設計規劃）完整流程；Review Appeal 保留；Phase 2 不變 |
| 2026-04-12 | v5.4：新增第五階段（QA 測試）— Petra 始終在 QA 迴圈中、Quinn 輸入增加 Issues + 實作說明、no_applicable_tests 需附理由由 Petra 把關、重測跑完整套件並區分新舊問題 |
| 2026-04-12 | v5.5：新增第六階段（文件）— Sage 收到所有前置階段產出、產出執行報告、不設 review 迴圈、新增三項待觀察事項（定期檢閱品質、評估 Review 機制、文件過時風險） |
| 2026-04-12 | v5.6：第六階段重構 — Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」（歸檔整理 + CHANGELOG），不再產生 API 技術文件；新增十六（Sage 全系統文件健康檢查）作為獨立定期任務 |
| 2026-04-12 | v5.7：新增十七（UAT 驗收階段）— 待觀察項目，目前先不加入 pipeline，視新流程運作結果決定 |
| 2026-04-12 | v5.8：新增第七階段（完成/上線）— Victoria 交付通知（Discord 摘要 + Dashboard 連結）、git tag 自動化（部署成功後建立）、Vera 審查加入版本號檢查 |
| 2026-04-12 | v5.9：全盤回顧更新 — 核心原則新增 4 條（輪次上限 3 輪 / 文件存 DB / BugFix 精簡路徑由 Petra 判斷 / Petra 工作量待觀察）；所有階段補齊文件負責人；第二階段新增 Petra 產出設計規劃書；第四階段新增審查報告格式定義 + 版本號檢查；Kick-off 記錄每位參加者意見 |
| 2026-04-12 | v6.0：大整理 — 移除 3 個已完成項目（四 #指令中心 / 七 Token 保護 / 九 存取分層 → Stage 22），重新編號為一～十四；修正 Quinn 為 Claude Code（非 API call）；八（開發流程重構）Phase 1 標記設計完成；修正所有跨項目引用 |
| 2026-04-14 | v6.1：新增十五（版本號集中管理 Directory.Build.props）；八（開發流程重構）Phase 1 實作狀態更新（Stage 25a 第一階段完成、Stage 25b 第二階段規劃中） |
| 2026-04-14 | v6.2：八（開發流程重構）Phase 1 全部完成 — 第二階段（設計規劃 Stage 25b v3.10.0）實作完成，Feature 八 Phase 1 七個階段全部 ✅ |
| 2026-04-14 | v6.3：十五（版本號集中管理）移入已完成摘要 — Stage 26 實作完成；十五條目從候選清單移除 |
| 2026-04-16 | v6.4：十（Agent 任務序列）新增「Stage 27b 後待討論」— PM 不走佇列的控制方式、PM 執行路徑確認（API vs Claude Code CLI）、Dashboard pause/resume 操作按鈕排入 Stage 議題 |
| 2026-04-16 | v7.0：已完成項目清理 — 八（開發流程重構）Phase 1 詳細流程圖精簡為摘要表格、優先級降為 🔵 低；十（Agent 任務序列）核心設計方案精簡為待討論議題 + 未實作方向，核心完成部分移入已完成摘要 |
| 2026-04-16 | v7.1：二（Agent 個性與造型）新增 Agent 互動頁面構想 + Gemini Flash 免費額度策略 — 個性對話等低複雜度文字生成路由到 Gemini Flash，零額外成本；搭配四（多 LLM 供應商）最小實作 |
| 2026-04-16 | v7.2：十一新增「TaskLog 獨立檢視頁面」；新增十五（Agent I/O 完整記錄）— 待 TaskLog 頁面上線後再討論記錄層級與儲存策略 |
| 2026-04-16 | v7.3：八 Phase 2 擴充為「LLM API → CLI 全面升級」— 新增設計原則（預設 CLI，不需要時才 LLM API）；完整列出 5 個應改環節 + 3 個不需改環節；取代原「申訴迴圈 Session 延續」單點描述 |
| 2026-04-16 | v7.4：十一新增「Agent 角色設定 Dashboard 化」— 角色描述 + 行為準則從 C# 硬碼抽至 DB，Dashboard Agent 設定頁可編輯；復用規則系統 Cache 機制；流程邏輯模板留在 C# 不動 |
| 2026-04-17 | v7.5：十一新增「Dashboard Cache Reload 按鈕」— 規則/Agent 設定修改後免跑 Discord 指令，Dashboard 直接呼叫 Bot internal API 刷新 Cache |
| 2026-04-18 | v7.6：九（Dashboard 雙向操作中心）移入已完成摘要 — Stage 28a（v3.14.0）+ Stage 28b（v3.15.0）實作完成；原 FF 十～十五重新編號為九～十四；修正所有跨項目引用（原十一→十、原十二→十一） |
| 2026-04-18 | v7.7：新增零-B（MockMode 新提案流程產生重複 TaskGroup Bug）— Christ 使用時回報，待撈 log 確認兩個 Group 的建立來源；程式碼表面邏輯是 Stage 28b 修正後的版本但實際結果不符 |
| 2026-04-18 | v7.8：新增零-C（通知類互動卡在待處理區無法消化 Bug）— merge_notify / intervention 用 EmptyActionsJson 但 Status 預設 pending，導致卡片無按鈕可操作；採方案 3（加「我知道了」按鈕），Christ 已確認 |
| 2026-04-18 | v7.9：四（多 LLM 供應商）大幅擴充 — 加入 CLI 層共存戰略（Gemini CLI 可與 Claude Code 並存、per-Agent 選擇）；修正「Claude Code 綁定」過時論述；加入 1M context 認知澄清；分兩階段實作策略（API 層先行、CLI 層需 spike）；優先級從 🔵 升為 🟡 |
| 2026-04-19 | v7.10：新增十五（Dashboard 與 Discord 功能平等）— Christ 於 Stage 29-5 驗收時提出核心原則：Discord 可執行的每個指令未來 Dashboard 也都要能觸發；首要子項為 `/mock` Dashboard 化，其餘含 `/pause`、`/stop-all` 等佇列控制 |
| 2026-04-19 | v7.11：新增十六（Dashboard 錯誤處理與提示 UX 打磨）— A. MudBlazor 元件內部例外的接住機制（等官方提供 OnValidationError hook 或自建包裝）；B. 錯誤訊息同時顯示 MudAlert + Snackbar（雙通道提示，不依賴使用者視線） |
| 2026-04-19 | v7.12：Stage 29 結案 — 零（Dashboard 歸檔報告折疊面板）、零-A（TaskLog 顯示統一化）、零-C（通知類互動卡在待處理區 Bug）三項移入已完成項目摘要（對應 Stage 29-1 / 29-2 / 29-5 搭車修，v3.16.0）；零-B（MockMode 重複 TaskGroup Bug）仍待重現保留在待處理區 |
| 2026-04-19 | v7.13：零-B 查明並修復（v3.16.1 hotfix）— Dashboard 路徑 `ProcessProposalApprovedAsync` 無條件 CreateGroup，Stage 28b 驗收修正只做了 Discord 路徑；加上對稱 GroupId 檢查後從 Dashboard 核准 MockMode 提案也只會產生 1 個 TaskGroup；零-B 移入已完成項目摘要 |
| 2026-04-19 | v7.14：新增十七（可靠性補強：失敗重試 + 會議 Crash Recovery）— Stage 29 結案後盤點 Q1/Q2/Q3 可靠性三問所釐清的缺口；A. Dashboard 重試按鈕（failed/cancelled TaskItem）；B. 會議 Crash Recovery 採方案 2（TaskGroup.ActiveMeetingType 欄位 + 啟動 hook 重跑，不動執行模型）；Christ 決策「會議重跑可接受」，不追求 FF 九的架構統一；明確放棄中斷點續跑 |
| 2026-04-20 | v7.15：Stage 30 結案（v3.17.0）— 八 Phase 2「LLM API → CLI 全面升級」子項移入已完成摘要；Phase 2 整體狀態改為 🟡 部分完成（循環偵測 + 新鮮視角 待 Stage 30 實際運行資料累積後評估）|
| 2026-04-20 | v7.16：新增十八（Appeal 對抗紀錄 UI 呈現）— Stage 30 驗收時搭車發現：ReviewAppealLog / DevPlanAppealLog 完整存在 DB，但 Dashboard 完全沒呈現；老闆 dogfooding 與調校 Agent 需要觀察這些對抗資料 |
| 2026-04-20 | v7.17：十七 + 十八 合併排入 Stage 31（v3.18.0）— 兩項都屬 dogfooding 缺口（可靠性補強 + 對抗紀錄可視化），合計 S-M 規模；FF 狀態從「🟡 待實作」改為「🟡 已排入 Stage 31」 |
| 2026-04-20 | v7.18：Stage 31 結案（v3.18.0）— 十七（重試按鈕 + 會議 Crash Recovery）+ 十八（Appeal 對抗紀錄 UI）全部完成，移入已完成項目摘要 |
| 2026-04-20 | v7.19：四（多 LLM 供應商）新增「CLI 三家能力研究（2026-04-20）」小節 — Aria 查官方文件 + GitHub issue 彙整 Claude Code / Gemini CLI / Codex CLI 能力對照；關鍵發現：Gemini CLI 不支援預設 UUID（FR #20847 open），但 Stage 30「新開 session」路線恰好繞開；Codex CLI 有 SDK + `--output-schema` native 支援，工程體驗最完整；建議 spike 順序：Codex → Gemini；屬研究階段，不是實作計劃 |
| 2026-04-20 | v7.20：Stage 31 驗收通過 — header 同步、條目註明本次首次實踐「Stage 結案兩段式分工」（實作 Session 寫 Roadmap 實作紀錄、Aria 收尾 Master Plan + Future_Feature）|
| 2026-04-20 | v7.21：新增十九（Agent maxTurns 動態化）— Stage 32 規劃時釐清「maxTurns 散落各處、每個 Agent 不同值」，不適合單抽為全域設定；記錄為未來項目，等 FF 四（多 CLI 整合）或 FF 十（Agent 角色設定 Dashboard 化）開工時順帶做 |
| 2026-04-20 | v7.22：新增二十（TaskGroupService 拆解 — 技術債）— Stage 32 開始實作後 Christ 觀察到該檔 2000+ 行過龐大，分析 Stage 30/31 context 緊繃的根因；方向：拆為 MeetingOrchestrationService / AppealOrchestrationService / QaCoordinationService / ProposalConfirmationService + 瘦身後 TaskGroupService 五個服務；建議搭車未來動到該檔的 Stage 一起做，避免獨立技術債 Stage 排後面 |
| 2026-04-20 | v7.23：二十擴充為「大檔案拆解技術債（合集）」— Aria 掃 wc -l 發現 Top 4 全破千行（TaskGroupService 2617 / CommandHandler 2327 / MeetingService 1415 / PmAgentService 1388），合併管理更有意義；新增子項 B（CommandHandler）/ C（MeetingService 最乾淨、最推薦優先）/ D（PmAgentService，Stage 30 剛膨脹）；搭車優先順序：C → D → B+A 合併 |
| 2026-04-21 | v7.24：Stage 32 驗收通過（v3.19.0）— 十五「/mock Dashboard 化」子項標記為已完成（其他子項 /pause / /stop-all / /queue 留待未來）；本次第二次實踐「Stage 結案兩段式分工」順利，驗收期間三項補強（Project 下拉 / DbContext 並行 / 補齊 Agent services 遺漏延遲點）由 Aria 順手補進 Roadmap v2.1 |
| 2026-04-21 | v7.25：FF 整理策略 A（保守整理）— 八（壓縮 Phase 1 詳表 + Phase 2 已完成摘要化，主體聚焦循環偵測 + 新鮮視角）、九（#2 PM 執行路徑標記已釐清、#3 Dashboard pause/resume 註明已移至 FF 十五，主體聚焦 #1 PM 佇列化議題）、十五（剩餘子項從附註升為主體，建立完成進度表）— 為 Stage 33（FF 十五剩餘子項）做鋪墊 |
| 2026-04-22 | v7.26：Stage 33 驗收通過（v3.20.0）— FF 十五（Dashboard 與 Discord 功能平等，`/mock` + 佇列控制兩子項全部完成）+ FF 二十一（Agent 狀態卡 expand 待辦清單）兩項整項移入已完成項目摘要；Roadmap v2.1 附帶兩項後續建議（AgentConfigService 命名守門 + Bot PushAgentStatus 名稱映射）暫不獨立開 FF，留待未來架構整理時一併評估 |
| 2026-04-22 | v7.27：新增二十二（Agent 命名一致性）— Stage 33 Roadmap v2.1 後續建議兩項正式記錄：子項 A（AgentConfigService 命名守門，擋 workflow 保留字 Dev_plan/Kickoff/Design）+ 子項 B（Bot PushAgentStatus 名稱語意清理，映射法 vs 雙欄位法兩策略）；子項 A 可獨立做或搭 FF 十 Agent 設定頁 refactor，子項 B 搭 SignalR push 層 refactor 或 FF 八 Phase 2；白名單補丁已擋住實際影響，本 FF 屬架構清理 |
| 2026-04-22 | v7.28：Stage 34 驗收通過（v3.21.0）— FF 二十 子項 C（MeetingService 拆解）✅ 完成：實際產出 4 檔（KickoffMeetingService 318 + DesignMeetingService 590 + MeetingCommons 62 + MeetingResults 27，原 1415 行 MeetingService 整檔刪除）；從本次實踐提煉「六項拆解 SOP」寫進 FF 二十 共通策略小節，供子項 D / B / A 參考；驗收期間零 follow-up commits 是目前最順利一次 Stage 結案 |

| 2026-04-22 | v7.29：Stage 35 驗收通過（v3.22.0）— FF 二十 子項 D（PmAgentService 拆解）✅ 完成：實際產出 6 檔（PmAgentResults 60 + PmAgentCommons 225 + PmReviewService 345 + ReviewAppealService 345 + DevPlanAppealService 207 + PmRoutingService 262 = 1444 行 vs 原 1389）；**首次實踐 SOP 6 子資料夾**（Agents/Pm/，namespace AiTeam.Bot.Agents.Pm）；實踐結論「**決策主體 = Agent 時放 Agents/，協調流程放 Orchestration/**」已寫進六項 SOP 供子項 A 參考；搭車優先順序更新（剩 B + A 合併做）；Context 實際 261K / Opus 1M 26% 驗證 ×1.6 校準公式 |
| 2026-04-22 | v7.30：Stage 36 驗收通過（v3.23.0）— **FF 二十 整項 ✅ 完成**（A+B 合併最後一次拆解）：TaskGroupService 2623→716 行（-73%）+ CommandHandler 2172→556 行（-74%）+ PendingConfirmationStore 解耦（6 字典抽 Singleton 升級 ConcurrentDictionary）+ 5 個子資料夾；驗收期間零 follow-up commits；Context 實際 360K / Opus 1M 36%；**AiTeam 四個怪物級大檔案技術債全部清零 🎉**；FF 二十主體從主清單刪除整項移入已完成項目摘要 |
| 2026-04-23 ～ 2026-04-24 | v7.31：Stage 37-2 驗收期間 FF 整理（三 commit 合併敘述）— **新增三項 FF**：二十三（Orchestration 異常退出的復原機制，Crash Recovery exception 盲點）/ 二十四（`CLAUDE_Vera.md` / `CLAUDE_QA.md` template 缺檔，production 真實流程才暴露）/ 二十五（Self-implement 試驗 prompt 設計守則，PR #107 架構繞道事件紀錄）；**FF 四細化**：第二階段拆成 2-A（Dashboard Provider/Model 動態化，明寫「不可採 Dashboard 自己讀一份 appsettings」技術約束）+ 2-B（CLI 層多家共存）+ 第一階段標 ✅（Stage 37-1 完成）；**FF 十一升級 🔵→🟡**：全域月限 1000K 首次被踩到（Stage 37-2 self-implement 累積 1.3M tokens 撞 Check 4），記錄「只動嘴的老闆」要 SSH 改 config 的 UX 痛點 |
| 2026-04-25 | v7.32：Stage 37 驗收通過（v3.24.0）— **FF 四第一階段 ✅ 完成**：GeminiProvider API 層交付（HttpClient + API key query string + System.Text.Json camelCase + 429 可識別 exception）；API 層 Agent 可透過 `appsettings.json` 切 Gemini（無 Dashboard UI，留 FF 四第二階段 2-A）；Rena（Release Agent）切 Gemini Flash 作為實測驗收；**搭車修 Crash Recovery 全面涵蓋**：`ActiveMeetingType` → `ActiveOrchestration`（5 種值 Kickoff / Design / ReviewAppeal / DevPlanAppeal / QaRouting）+ Appeal / QA 兩處 try-finally + dispatcher 5 分支 + 3 個 Restart helper；**搭車修 Stage 36 Dev_plan dispatcher 遺漏**（38 行搬進 `AppealOrchestrationService.HandleDevPlanCompletedAsync` wrapper，三個 `Handle*Completed` 對稱）；FF 四第一階段主體移入已完成項目摘要，第二階段保留 |
| 2026-04-25 | v7.33：**FF 二十四 ✅ 完成** — 實證根因與原假設不同：`CLAUDE_Vera.md` / `CLAUDE_QA.md` 兩檔其實已在 repo source 存在（建於 2026-04-12/13），問題在 `AiTeam.Bot.csproj` 只顯式 Include `CLAUDE_CODY.md` + `CLAUDE_Victoria.md`，其他 6 個 template 未設 `CopyToOutputDirectory` → output 沒複製 → runtime `AppContext.BaseDirectory/Resources/` 找不到；修法採 glob pattern `Resources\CLAUDE_*.md` 一次涵蓋全 8 檔（含未來新增），從主清單刪除整項移入已完成項目摘要 |
| 2026-04-25 | v7.34：新增 FF 二十六（Model 清單 DB 化 + Dashboard 管理頁，Stage 38 延伸升級）— ⚪ 待觀察；背景：Stage 38 採 constants 檔方案（`LlmModels.cs`），Aria 查網路 + commit 維護，CI/CD 5-10 分鐘生效；觸發條件：1-2 個月內抱怨節奏慢超過 5 次 / Model A/B 實驗場景 / Provider 擴充讓 constants 超 50 項；升級方向：`LlmModel` entity + Dashboard `/system/models` CRUD 管理頁 + Aria 透過 Dashboard/Internal API 改 DB 立即生效 |
| 2026-04-25 | v7.35：Aria WebFetch Google 官方文件確認 Gemini 現況 — 2.5 Pro/Flash 仍 stable 但 **2026-06-17 deprecating**（距今 ~2 月）、Gemini 3 系列仍全為 -preview 不建議 production；FF 二十六「升級觸發條件」加入此具體時點作為**第一個已知實際 trigger**（若 Gemini 3 GA 前後快速迭代 → 啟動 DB 化；若單次遷移穩定 → 維持 constants）；Stage 38 Roadmap v1.2 對應 `LlmModels.cs` 範例加時效註解 |
| 2026-04-25 | v7.36：Stage 38 完成（v3.25.0）— **FF 四第二階段 2-A ✅ 完成**：Dashboard Provider/Model 動態化；DB 為唯一 source of truth + appsettings 降為啟動 seed null 欄位 + Dashboard UI 直接改（PR #107 三條禁止路線全避開）；新增 `AgentConfigCache` Singleton 對齊 AppSettingsService TTL 5min pattern + `LlmModels.cs` 常數白名單 + Internal API `scope=agent-config` 分支讓 cache 即時失效；**關鍵修正**（Aria 計劃書外抓出）：TokenTrackingProvider 第 9 參數從 `config.Model` 改傳 `finalModel`，避免 Dashboard 改完後 Token 監控頁顯示舊 model 的資料誤導；Christ 驗收 1（UI）+ 3（覆蓋性）通過，2（Token 監控）待後續 Stage 真實任務搭車驗；FF 四第二階段 2-A 主體標 ✅，2-B（CLI 層多家共存）保留待評估 |
| 2026-04-25 | v7.37：Stage 38 結案後整理 — (1) **新增 FF 二十七**（Self-implement 試驗 v2，FF 二十四 fix 後重新評估品質）：Aria 調查 Stage 37 真實流程品質低下三大主因（Top 1 CLAUDE_*.md COPY 漏 = FF 二十四已修 / Top 2 Vera 偏好放行 + Petra Warning 寬鬆 / Top 3 QA 測試品質指引偏量）；計劃下個小規模任務 production 真實流程跑一次 self-implement 看品質回升狀況，若 Top 1 修復就足夠則 Top 2/3 延後。(2) **補完 FF 二十搬遷**：Stage 36 結案 changelog 寫了「主體刪除移入已完成摘要」但實際沒搬，主清單仍有 155 行；本次補完搬遷至已完成摘要 |
| 2026-04-25 | v7.38：FF 四瘦身（波 2 選項 A）— 第一階段 + 第二階段 2-A 詳細描述（共 ~30 行）壓縮成「已完成成果」摘要兩 bullet 帶 Stage Roadmap 連結；「實作重點」改名「2-B 實作重點」並刪掉已完成的 GEMINI case 條目；FF 四從 ~156 行縮到 ~95 行（-39%）；2-B（CLI 層多家共存）+ CLI 三家能力研究 +「PR #107 三條禁止路線」全保留供未來 spike 用 |
| 2026-04-25 | v7.38：Trial_v2（self-implement 試驗 v2）執行完成 — 任務「規則管理頁面 UI 微調」（PR #108）；新增獨立紀錄 [docs/experiments/Trial_v2_RuleManagementUI.md](../experiments/Trial_v2_RuleManagementUI.md)；FF 二十七 補「v2 試驗結果」段（Top 3 ✅ 部分驗證 / Top 1+2 因任務性質繞過 Vera/Petra 未驗）；**新增 FF 二十八**（Vera 審查範圍擴及 .razor / .css，Trial_v2 發現的真實設計缺口）；FF 二十二 子項 B 補具體案例（Dev/Dev_plan chip vs expand 不一致）；其他 Trial_v2 觀察列為下個 Stage 搭車修候選（BossInteraction.Description bug、a11y 隱患、Reviewer 略過狀態錯標）|
| 2026-04-25 | v7.39：Stage 39 完成（v3.26.0）— **FF 二十八 ✅ 完成 + Trial_v2 搭車修四項 ✅ 完成**：Vera 審查範圍擴及 .razor / .css（對齊 Quinn `hasUiChanges`）+ CLAUDE_Vera.md a11y / Blazor / CSS / MudBlazor 判準擴充（全 Warning 維持「偏好放行」）；新增 `AgentResultType.Skipped` 結果型別 + Dashboard 全鏈路 teal `#20c997` mapping + TaskGroupService Reviewer Skipped 跳 Petra 放行；BossInteraction.Description 補 Task.Description（CommandHandler L195/L441 + 順手抓到 SlashCommandRouter L245 第三處對稱）；RuleManagement.razor MudSwitch aria-label；QA 略過共用 Skipped API；Mock `review_skipped` 情境（Discord + Dashboard 雙入口）；FF 二十八主體 78 行搬入已完成摘要 + FF 二十七 5 個觀察項 4 項標 ✅、Trial_v2 v3 前置條件全部就緒；驗收 D pass（Stage39測試3 Reviewer Status=skipped）、A 流程 pass（主菜真實能力待 Trial_v3 真實 PR）、C 規劃驗收方式錯誤（mock 跳過 CEO LLM 不會建 ceo_confirm）改用真實 CEO 對話補驗 |
| 2026-04-25 | v7.40：Stage 39 結案後補完 — (1) Stage 39 Roadmap header 升 ✅ + 文件版本 v2.0（Christ 結案 commit 漏改，Aria 補）；(2) FF 十「Agent 角色設定 Dashboard 化」擴充 Phase 1 / Phase 2 分階段：Phase 1 C# SystemPrompt（API 層 Agent，FF 十既有範圍）+ Phase 2 CLAUDE_*.md template（CLI 層 Agent，Stage 39 結案後 Christ 詢問是否可 Dashboard 編輯，Aria 推薦現在不做：內容仍在演進、修改頻率低、code review 軌跡比 DB 安全；觸發條件寫明「等 Trial_v3 完成 + CLAUDE_Vera 定型 1-2 月後 + 真實使用情境」）|
| 2026-04-25 | v7.41：Trial_v3（self-implement 試驗 v3）執行完成 — 任務「流程追蹤頁面 PR 欄位優化」（PR #109）；新增獨立紀錄 [docs/experiments/Trial_v3_PipelinePrColumn.md](../experiments/Trial_v3_PipelinePrColumn.md)；FF 二十七 補「v3 試驗結果」段（FF 二十八 ✅ / Top 1 🟡 部分 / Top 2 🟢 預期 / Top 3 ✅ 維持）；**新增 FF 二十九**（CLAUDE_Vera.md 三處判準漏寫：`<a>` a11y / `rel="noopener"` 安全 / pattern match Warning）+ **新增 FF 三十**（tech_improvement ghost Dev task）；FF 二十二 子項 B 補 Agent 執行確認訊息誤導案例；戰略結論：審查層 CLAUDE_Vera.md 判準邊界覆蓋不全 = Stage 37 品質低下根因，非 Vera 失職 |
| 2026-04-26 | v7.42：Stage 40 完成（v3.27.0）— **FF 二十九 ✅ 完成**（CLAUDE_Vera.md 三段判準補入：安全 Critical / a11y `<a>` Warning / 業務邏輯 pattern match Warning；4 處同源 tabnabbing 全清；ExtractPrNumber 抽 `Helpers/PrNumberHelper.cs` 共用 helper；MudLink inline rel/aria-label 確認可 forward）；**FF 二十五 Petra 子項 ✅ 完成**（CLAUDE_Petra.md 第 4 節新增「Warning→blocking 升級規則」五條清單；FF 二十五本體保留作為未來 Trial 任務 prompt design 的 reference）；**Trial_v4 前置條件閉環就緒**（FF 二十九 Vera 抓得到 + FF 二十五子項 Petra 擋得下）；首次驗收即通過、零 follow-up commits；Forge 揭露踩坑「tests/Generated/ 非編譯目標」（Quinn 寫的測試實際無法用 `dotnet test` 跑）—— 待 Christ 評估是否新開 FF |
| 2026-04-27 | v7.43：**新增 FF 三十一**（tests/Generated/ 編譯與執行修復）— 立案來自 Stage 40 Forge 結案踩坑揭露：`tests/Generated/` 7 檔（含 Quinn Trial_v3 寫的 Playwright + xUnit）皆無 csproj、`find tests -name "*.csproj"` 0 hits；戰略定位「Quinn 測試層」是品質保證迴圈第三層（補上 Vera/Petra 兩層之後缺的塊）；修法：建 csproj + xUnit/Playwright runner config + 修積累 type/namespace 不相容 + 整合 `AiTeam.slnx`；規模 S-M、🟡 中優先；**建議 Stage 41 處理**作為 Trial_v4 真正完整驗證環境的前置條件 |
| 2026-04-27 | v7.44：**FF 三十一 ✅ 完成** — Stage 41（v3.28.0）：建 `tests/AiTeam.Tests.Generated/` xUnit csproj（隔離 LLM 產出區，跨資料夾 Compile Include）+ AiTeam.slnx /tests/ folder + 清 3 類檔案層破損（markdown fence × 3 / 幻覺類別刪 / LLM 截斷修復）+ 連帶清除 repo 根目錄 stray AiTeam/（PR `1979f46` 歷史殘留 LLM 污染）+ MainLayout broken test 嚴格版刪除 + CLAUDE_Quinn.md 三段補強（valid C# 規則 + 測試標的存在性驗證 + JSON schema unverifiable_targets 語意分離）；dotnet test 127 Passed / 0 Failed；FF 三十一主體刪除整項移入已完成項目摘要；**「Vera 審查 + Petra 閘門 + Quinn 測試」三層品質保證迴圈閉環就緒**，Trial_v4 完整驗證環境前置條件達成 |
| 2026-04-28 | v7.45：**Trial_v4 結案 + 新增 FF 三十二 / 三十三** — Trial_v4（Dashboard 錯誤處理 UX 打磨，PR #122 OPEN 未合併）首次完整三層迴圈閉環試驗、首次跨完整 pipeline 試驗、首次成本紀錄（$4.99 USD）；揭露 13 個 bug（5 個 🔴 嚴重）+ Self-implement 適用邊界（小範圍純技術改動 ✅ / 跨多檔架構級重構 🔴）；FF 二十七 補「v4 試驗結果」段（Top 1 🟡 / Top 2 🔴 / Top 3 🔴）；**新增 FF 三十二**（Self-implement 完整性閘門六子項：DevPlan 容錯 / fix loop 中止 / Petra 範圍縮水升級 / Vera Critical 邊界堅守 / QA 失敗判定 / Sage escalate）；**新增 FF 三十三**（Token 計費機制 CLI Agent 涵蓋，現 token_logs 只涵蓋 ~6% 成本）；戰略結論：三層迴圈在「程式碼層面瑕疵」生效，在「self-implement 範圍縮水」失效；未來架構級重構需求走正規 Stage（Aria 規劃 + Forge 實作），不交 Victoria self-implement；**FF 三十二完成前不開 Trial_v5** |
| 2026-04-28 | v7.46：**新增 FF 三十四**（TaskGroup 流程暫停機制）— Trial_v4 觀察期間 Christ 提出真實 UX 痛點（Kickoff 結束後等 8h 期間想暫停無選項 / 流程走偏無法即時干預 / 等外部條件無暫停選項）；記錄 Aria 設計層面三個考慮點：① 暫停粒度（TaskGroup vs Stage 階段 vs Task 三選一，初判選 Stage 階段級）② 暫停動作（被動阻擋下階段 vs 主動 kill subprocess vs 兩者皆可，初判選被動）③ 恢復機制（被動暫停簡單 vs 主動 kill 需 rewind checkpoint 複雜度高）；與既有 Stage 27b Agent pause / Stage 33 全域停止 / Stage 31/37 Crash Recovery 邊界釐清；規模 M-L、🟡 中優先；待 FF 三十二 / 三十三 排序時評估三選二/三選三 |
| 2026-04-28 | v7.47：**FF 三十二 補子項 G + 立 FF 三十五**（戰略級）— **FF 三十二補強**：① 子項 G「Cody 自我檢查 PR 範圍 vs DesignPlan」（Trial_v4 第二維度盲點，Cody 自欺 vs 客觀完成度，純 prompt 補強含 ESCALATE_NEEDED 機制）② 七子項分兩 Stage 排序建議（Stage 42 = C+D+F+G prompt 補強 / Stage 43 = A+B+E Orchestrator 改動）③ 替代方案「Petra 升級 Claude CLI 審 Vera」記錄 Christ conditional decision（暫不採納，未來架構/設計/狀態變化時重評估，獨立開 FF 三十六）。**新增 FF 三十五**（自動拆任務機制 ⭐ 戰略級）：解 Trial_v4 self-implement 範圍縮水根因 / 對齊 real-world 團隊模式；採 B 階段攔截（Petra 在 Design 綜合整理時 propose 拆 sub-task，CEO/PM 權責分工乾淨）；6 個設計細節已拍板（兩段確認卡 / sub-task 共享 Kickoff+Design / Sequential 依賴鏈 / 各自獨立 PR / DB 內表達 epic / 鎖前置條件 FF 三十二+三十三）；多專案 A 階段攔截留 Phase 2 未來擴充；規模 L、⭐ 戰略級 🔴 高；Stage 排序鎖死（42-43 = FF 三十二 / 44 = FF 三十三 或並行 / 45+ = FF 三十五）|

# Future Feature — 未來功能候選清單

> 版本：v7.14
> 建立日期：2026-04-01
> 最後更新：2026-04-19
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

### 實作策略（建議分階段）

#### 第一階段（工程量小、價值清楚）

1. `GeminiProvider : ILlmProvider` — 照抄 `AnthropicProvider` 改 API call
2. Dashboard Agent 設定頁 Provider 選單加 `Gemini` 選項
3. API 層 Agent（Rosa / Demi / Sage / Release / Ops）可選 Gemini
4. 搭配 Gemini 2.5 Flash 免費額度用在低複雜度任務（Sage 歸檔摘要、個性對話）

#### 第二階段（需要 spike 評估）

5. `GeminiCliService : IClaudeCodeService` — 評估 Gemini CLI 的 session 延續、工具調用、輸出格式是否能對齊
6. 主要挑戰：
   - Session 延續機制不同（Claude Code `--resume UUID` vs Gemini CLI 自己的 session 管理）
   - 工具調用格式、輸出解析格式不同
   - `MockClaudeCodeService` 模擬的是 Claude 輸出，Gemini 要另做一套
   - `CLAUDE.md` 對應的 `GEMINI.md` 兩套記憶檔並存維護成本
7. CLI 層 Agent（Victoria / Cody / Vera / Quinn / Petra）可選 Claude Code 或 Gemini CLI

### 實作重點（技術細節）

- `LlmProviderFactory.Create()` 的 switch 新增 `"GEMINI"` / `"OPENAI"` case
- `GeminiProvider` 需支援 Vision（Victoria / Quinn 可能傳入圖片）
- Token 追蹤（`TokenTrackingProvider`）包裝層不需改動，對供應商透明
- CLI 層若做 `GeminiCliService`，需抽象出 `ICliAgentService`（原 `IClaudeCodeService` 重命名）或直接並列兩個實作
- `MockClaudeCodeService` 若為 Gemini CLI 做對應模擬，考慮抽出 `MockCliAgentService` 共用 MockMode 邏輯

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

> **Phase 1 已全部完成。** 詳細流程設計請參閱各 Stage Roadmap 文件。
>
> | 階段 | 內容 | 完成於 |
> |------|------|--------|
> | 第一階段（需求計劃） | Kick-off 會議機制 | Stage 25a（v3.9.0） |
> | 第二階段（設計規劃） | 設計會議 + Rosa/Demi 移至設計階段 | Stage 25b（v3.10.0） |
> | 第三階段（開發） | 實作說明、阻礙報告、Dev_plan Appeal | Stage 23（v3.7.0） |
> | 第四階段（程式碼審查） | Review Appeal + Petra 仲裁 | Stage 23（v3.7.0） |
> | 第五階段（QA 測試） | Petra 四路由判斷 + QA 修復迴圈 | Stage 24（v3.8.0） |
> | 第六階段（收尾歸檔） | Sage 轉型為收尾歸檔員 | Stage 23（v3.7.0） |
> | 第七階段（完成/上線） | Git Tag 自動化 | Stage 23（v3.7.0） |

### 背景

**已知事故：**
1. **Vera 誤判事故**：Vera 持續報告 false critical，Cody 無法反駁（單向權力結構），直到 Christ 手動介入才發現是 Vera 異常。
2. **實作 Session 死循環事故**：Session 反覆做無效修正，沒有機制偵測循環模式。

**流程設計缺陷（2026-04-12 全盤討論後發現）：**
- Agent 之間是單向串行，不能質疑上游輸入
- 缺少 Kick-off 會議對齊認知
- Code Review 是單向判決，現實中是雙向對話

### 核心設計原則（Phase 1 & 2 共用）

1. **Petra 是 PM 協調者**：主持會議、判斷流程走向、評估影響範圍
2. **會議只對齊認知**：Agent 在會議中只提疑問與風險，不產出實際工作成果
3. **所有輪次上限預設 3 輪**：未來可在 Dashboard 動態調整（依賴 FF 十一）
4. **文件存入 DB**：各階段產出統一存入 DB，WorkflowEngine 啟動下游 Agent 前從 DB 取出
5. **BugFix / TechImprovement 精簡路徑**：由 Petra 判斷可跳過哪些階段
6. **Petra 工作量待觀察**：幾乎參與每個階段，Token 消耗需持續監控

---

### Phase 2（待後續 Stage）

以下機制在 Phase 1 跑穩後再評估：

**循環偵測（Loop Detection）**：追蹤每次修正的 diff，偵測是否在反覆修改同一段程式碼（oscillation），比單純計數更聰明。

**新鮮視角（Fresh Eyes）**：熔斷觸發後，啟動全新獨立 Session 診斷問題根因（不帶前面的對話歷史）。對標現實：叫一個不在脈絡中的同事過來看問題。

**LLM API → Claude Code CLI 全面升級**：

> **設計原則：預設使用 Claude Code CLI，只有確定不需要 codebase 存取時才選用 LLM API。**

目前流程中多個審核/申訴環節使用 LLM API（純文字問答），導致 Agent 在修正、反駁、再評估時**失去 codebase 存取能力**，品質明顯低於 Claude Code CLI。應全面升級為 Claude Code CLI + `--session-id` / `--resume`，讓 Agent 在迴圈中保留原始 session 的 codebase 上下文。會議機制（Kickoff / Design）已驗證此模式可行。

**應改（影響最大）：**

| 環節 | Agent | 目前 | 改為 |
|------|-------|------|------|
| Dev_plan 申訴 — 修正 | Cody | LLM API | Resume Dev_plan session |
| Dev_plan 申訴 — 再評估 | Petra | LLM API | Resume 或新開 CLI |
| Review 申訴 — 反駁 | Cody | LLM API | Resume Dev session |
| Review 申訴 — 再評估 | Vera | LLM API | Resume Review session |
| Review 申訴 — 仲裁 | Petra | LLM API | 可選改 CLI |

**不需改（純文字分析，無 codebase 需求）：**

| 環節 | Agent | 理由 |
|------|-------|------|
| QA 失敗路由判斷 | Petra | 分析 TestReport JSON，不需看程式碼 |
| QA no_tests 評估 | Petra | 判斷是否合理，純文字 |
| 審閱 Vera Review 報告 | Petra | 分析 Review 品質，純文字 |

---

### 實作分期

| Phase | 包含內容 | 狀態 |
|-------|---------|------|
| **Phase 1** | 全流程重構（七階段：需求→設計→開發→審查→QA→歸檔→上線） | ✅ 全部完成（Stage 23~25b，v3.7.0~v3.10.0） |
| **Phase 2** | 循環偵測 + 新鮮視角 + LLM API → CLI 全面升級 | 🔵 待後續 Stage |

### 優先級

🔵 低優先級 — Phase 1 已全部完成，Phase 2 待 Phase 1 實際運行一段時間後再評估是否需要

---

## 九、Agent 任務序列 — 後續議題

> 核心機制已完成：Stage 27a（v3.12.0）+ Stage 27b（v3.13.0）
> 詳見已完成項目摘要

### 📌 待討論議題（2026-04-16）

以下三個問題在 Stage 27b 實作過程中發現，需要後續討論：

**1. PM（Petra）不走佇列，要怎麼控制她停止/恢復？**

Petra 是 `TaskGroupService` 中的 inline `await` 閘門，不在 `AgentQueueProcessor.SemaphoreGroups` 的 8 個 executor key 中。`/stop-all` 不影響她，Dashboard 也不顯示她的佇列狀態。
- 把她納入 queue 機制？還是另外設計？
- inline `await` 改成 queue 是否會造成流程阻塞？
- 「Petra 暫停中，流程卡在審核點」是否合理？

**2. PM 的執行路徑確認**

系統中 Agent 有兩種路徑：Claude Code CLI（Cody/Vera/Quinn/Rosa/Demi/Sage/Victoria）和直接 API call（Rena/Maya）。Petra 是哪一種？需確認後補入 FF 四的限制說明。

**3. Dashboard pause/resume 操作按鈕**

目前佇列操作只能透過 Discord 指令。Dashboard 只顯示狀態（Badge + 佇列深度），沒有操作按鈕。此需求已記錄在 FF 九（Dashboard 雙向操作中心）。

### 未實作的設計方向（保留供未來參考）

以下項目在原始設計中規劃但尚未實作，保留作為未來擴充方向：
- **Maya 自動化部署流程**：Maya 發送 Graceful Shutdown → 確認全員 Stopped → 執行部署 → 自動恢復
- **Error 狀態阻塞 + 手動重試/取消**：Error 任務留在 queue 頭部，Dashboard 提供重試/取消按鈕（吸收原 API 餘額恢復需求）
- **優先級支援**：修正任務優先、緊急上報可插隊、中斷恢復排入隊首

### 優先級

🔵 低優先級 — 核心佇列已完成，剩餘為擴充性需求

---

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

### 優先級

🔵 低優先級 — 不影響功能，純 UI 組織與使用便利性優化

---

## 十一、Dashboard 可調整 Token 守門全域限額

> 狀態：🔵 低優先級 — 目前只能改設定檔後重新部署

### 背景

Stage 22 實作了 Token 守門機制，包含：
- **全域月費上限**：`AgentSettings:MonthlyTokenLimitK`（預設 1000K）
- **各 Agent 日限**：`AgentSettings:Agents:{Name}:DailyTokenLimitK`
- **各 Agent 月限**：`AgentSettings:Agents:{Name}:MonthlyTokenLimitK`
- **單次請求上限**：`AgentSettings:SingleRequestTokenLimitK`（預設 50K）

目前這些值只能透過修改 `docker-compose.prod.yml` 環境變數並重新部署來調整。Token 監控頁面的警示訊息也只能說「請至 Bot 設定調整」，沒有直接入口。

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

🔵 低優先級 — 目前繞道修改 docker-compose.prod.yml 可解決問題，UI 入口是便利性需求

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

## 十五、Dashboard 與 Discord 功能平等（Feature Parity）

> 狀態：⚪ 待討論 — 逐一擴充，`/mock` 為首要
> 提出日期：2026-04-19（Christ 於 Stage 29-5 驗收時提出）

### 核心原則

**Discord 可執行的每個指令與流程，Dashboard 最終也都要能觸發。** 讓 Dashboard 成為完整的操作入口，驗收 / 日常使用不需要在兩個介面之間切換。

Stage 29-5 已完成「Dashboard 下達指令給 Victoria」（對應 Discord `#victoria-ceo` 的自由文字訊息），是此方向的第一步。後續還有多個 Discord slash commands 需要 Dashboard 化。

### Discord-only 指令清單 + Dashboard 對應現況

| Discord 指令 | 用途 | Dashboard 現況 |
|--------------|------|----------------|
| `/mock <情境>` | 觸發 Mock Mode 模擬流程（新功能 / bug fix / 含提案 等） | ❌ 不支援 — 文字輸入會被 Victoria 當普通訊息吞掉 |
| `/pause <agent>` / `/resume <agent>` | 暫停 / 恢復特定 Agent 佇列（Stage 27b） | ❌ 不支援（與十「Dashboard pause/resume 按鈕」重疊） |
| `/stop-all` / `/resume-all` | 全域暫停 / 恢復所有佇列 | ❌ 不支援 |
| `/queue` | 查看佇列狀態 | ✅ 首頁 Agent 狀態卡已顯示（Stage 27b） |
| `/reload-rules` | 刷新規則 + AppSettings cache | ✅ 規則管理 / 系統設定頁「套用變更」（Stage 29-3） |

### 實作方向

- 對應 Discord command 各自暴露 Bot internal API 端點（仿 Stage 29-3 `/internal/reload-cache` / 29-5 `/internal/ceo/command`）
- Bot CommandHandler 中 Discord-only 的處理邏輯（如 `HandleMockProposalFlowAsync`）抽成 shared service，讓 Discord 與 Dashboard 兩路徑共用
- Dashboard 提供對應 UI：
  - **`/mock` 觸發**：專屬「驗收工具」頁面或首頁小卡，下拉選單（新功能 / bug fix / 含提案 / 等）+ 觸發按鈕
  - **佇列控制**：Agent 狀態卡上加 pause/resume 小按鈕

### 首要條目：`/mock` 觸發（Christ 明確要求）

驗收時每次都要切回 Discord 輸入 `/mock ...` 很麻煩。優先做這一個。實作時順便把 `HandleMockProposalFlowAsync` 等私有方法重構成可被 controller 直接呼叫的 shared service。

### 與其他項目的關係

- 和 **十（Agent 任務序列 — Dashboard pause/resume 待討論議題）** 部分重疊，實作時一併考慮
- 與 **Stage 29-5 的 fire-and-forget 模式** 同構，`/mock` 也應走背景任務 + BossInteraction 推送

### 優先級

⚪ 待討論 — 視實際驗收 / 開發使用頻率決定優先順序。`/mock` 子項有明確需求，可先單獨排入小 Stage。

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

## 十七、可靠性補強：失敗重試 + 會議 Crash Recovery

> 狀態：🟡 待實作 — 方案已定，等排入 Stage
> 提出日期：2026-04-19（Christ 在 Stage 29 結案後盤點三個可靠性問題時釐清）

### 背景

Stage 29 結案後盤點 Agent 執行的可靠性，發現 Q1 / Q2 / Q3 三問答對應的覆蓋情況：

| 問題 | 現況 | 缺口 |
|------|------|------|
| Q1 Agent 輸入是否存 DB | ✅ 90%+ 都在 DB（Stage 23~26 的「文件存 DB」成果） | 無 |
| Q2 失敗任務能否重試 | ⚠️ 技術可行（DB 資訊完整）但無使用者入口 | Dashboard 沒有重試按鈕 |
| Q3 Bot 重啟後能否恢復 | ⚠️ 走佇列的 Agent 有 Crash Recovery（Stage 27a），但 Kickoff/Design 會議不走佇列 | 會議卡住要人工介入 |

本項目補 Q2、Q3 的缺口。

### A. Dashboard 重試按鈕

**問題**：TaskItem 失敗或取消後，目前沒有便利的重試路徑：
- Discord 重發 `/task` 會建新 TaskItem，不繼承失敗的
- 只能改 DB：`UPDATE task_items SET status='queued', queue_status='queued' WHERE id=?` + 喚醒佇列

**實作方式**：
1. 流程追蹤頁 / 任務列表頁，`failed` / `cancelled` 的 TaskItem 旁新增「🔁 重試」按鈕
2. 後端新增 `AgentQueueService.RequeueTaskAsync(taskId, ct)`
   - 驗證當前 status ∈ {failed, cancelled}
   - `UpdateStatus(task, "queued")` + `SetQueueStatus(task.Id, "queued")`
   - `WaitForSignal(0)` 喚醒 AgentQueueProcessor 主迴圈
3. Dashboard 推送 SignalR queue update

**為什麼可以直接重試**：Q1 已保證 Agent 輸入全部在 DB（TaskItem + TaskGroup 所有欄位），不需要重新組裝上下文。

### B. 會議 Crash Recovery（方案 2：TaskGroup 欄位追蹤）

**問題**：Kickoff / Design / Petra 同步會議不走 AgentQueueProcessor，不在 Stage 27a `RecoverStuckTasksAsync` 的掃描範圍，Bot 重啟時中斷的會議會永遠卡在 running。

**方案對比（已討論）**：
- **方案 1（統一走佇列）**：把會議包裝成 `IAgentExecutor` 進 AgentQueueProcessor。方向正確但範圍大，且牽涉 Semaphore 爭用、`/pause` 語意、executor key 設計等**執行模型層級的抉擇**。屬於 FF 九「PM / 會議走佇列」的議題範圍，不該搭車 Crash Recovery 處理。
- **方案 2（本方案，TaskGroup 欄位）**：保守補丁，不動執行模型。

**Christ 決策**：採方案 2。理由：「Bot 重啟本來就不常發生，會議重跑（幾分鐘成本）可接受」——Crash Recovery 的痛點是「卡住要人工介入」，自動重跑已足夠解決，不需要為此升級架構。

**實作方式**：
1. `TaskGroup` Entity 新增 `ActiveMeetingType` 欄位
   - 型別：`string?`
   - 值：`"Kickoff"` / `"Design"` / `null`
2. EF Migration
3. `TaskGroupService.HandleKickoffAsync` / `HandleDesignAsync` 開始時 set、結束時（成功 / 失敗 / 例外 finally）clear
4. 新增 `RecoverStuckMeetingsAsync`（Bot 啟動時跑，類似 AgentQueueProcessor 的 Crash Recovery）：
   - 掃 `ActiveMeetingType != null` 的 TaskGroup
   - 針對每個 Group 呼叫對應的 `HandleKickoffAsync` / `HandleDesignAsync` 重跑
   - Log warning 標示數量

### C. 重跑語意：從頭開始，不做中斷點續跑

兩個方案都是**整場重開**——Kickoff 會議跑到第 2 輪被中斷，重跑從第 1 輪開始。

理論上可以讀 `KickoffMeetingLog`（Stage 25a）重建 session 狀態做「續跑」，但：
- 會議通常幾分鐘內跑完，重開成本可接受
- 續跑需要 Claude Code CLI session 管理 + 對話歷史重建，實作複雜度高
- 不值得為低頻事件做這層

明確放棄續跑，此項目不涵蓋。

### D. CEO 對話中斷（不涵蓋）

`ProcessWithClaudeCodeAsync`（Victoria 單次分析請求）重啟中斷影響小——使用者重發指令即可，沒有卡住的狀態。不需特殊處理。

### 涵蓋範圍總結

| 場景 | 處理方式 |
|------|---------|
| 失敗 / 取消的 TaskItem | Dashboard「重試」按鈕（A）|
| Bot 重啟時執行中的普通 Agent 任務 | Stage 27a 既有 `RecoverStuckTasksAsync`（無需改）|
| Bot 重啟時執行中的會議（Kickoff / Design）| `RecoverStuckMeetingsAsync` 自動重跑整場（B）|
| Bot 重啟時 Victoria CEO 對話 | 無需處理，使用者重發（D）|
| Bot 重啟時 Petra 審核（同步跑在 TaskGroupService 內）| 落入會議 Recovery 範圍，或 Petra 直接在重跑會議中被帶起 |

### 不涵蓋的議題（屬於 FF 九，獨立處理）

- 會議在 Dashboard 佇列視覺化可見
- `/pause Petra` 能攔截會議
- 統一所有執行入口走佇列

這些是「架構一致性」議題，獨立價值，不因本項目觸發。

### 優先級

🟡 中 — Q2（失敗重試）是 dogfooding 時必然遇到的便利性缺口；Q3（會議 Recovery）屬於低頻但卡住要人工介入的場景。兩者合併實作成本不大（一個新欄位 + Migration + 兩個方法 + 一個按鈕）。

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

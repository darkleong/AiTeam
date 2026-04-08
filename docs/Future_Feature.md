# Future Feature — 未來功能候選清單

> 版本：v3.2
> 建立日期：2026-04-01
> 最後更新：2026-04-07
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。已完成項目移至底部「已完成項目摘要」。

---

## 一、API 費用優化

### 背景

目前各 Agent 採用混合模型策略：
- **核心 Agent**（CEO / Dev / Reviewer）：`claude-sonnet-4-6`（品質優先）
- **唯讀探索 Agent**（Requirements / Designer / Doc）：`claude-haiku-4-5`（成本優先）
- **QA / Release / Ops**：`claude-sonnet-4-6`（直接 API call，消耗低）

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

## 二、MCP（Model Context Protocol）整合

### 背景

Anthropic 推出的 MCP 是一個開放協議，讓 LLM 能夠更標準化地使用外部工具。目前 AiTeam 各服務（GitHub、Discord）均為自行維護的 API 串接。

### 潛在應用

- Agent 透過 MCP 存取 GitHub、Discord
- 減少自行維護 API 串接的成本
- 更容易擴充新的工具給 Agent 使用

### 行動建議

- 持續關注 MCP 的生態系發展
- 等 MCP server 生態系成熟後，評估是否替換現有服務層

### 優先級

🔵 低優先級 — 持續觀察，不急於實作

---

## 三、Agent 個性與造型設定

### 背景

目前 Agent 個性與造型設定延後處理，不影響現有架構。

### 預計包含

- 每個 Agent 的名字與個性描述（寫進 System Prompt）
- Dashboard Team Office 頁面的人物造型替換
- 依狀態有對應動畫（忙碌打字、閒置發呆、錯誤冒汗）
- 辦公區之外加入休息區（Agent 閒置時移動過去）

### 行動建議

- 等 Dashboard 視覺整體穩定後，開一個專門的討論來設計細節

### 優先級

🔵 低優先級 — 純視覺體驗優化，功能優先

---

## 四、顧問 Agent 設計

### 背景

Stage 15 完成後，Victoria 已具備技術顧問能力（Claude Code 探索 + Session 對話 + 長期記憶），等同方案二「顧問能力整合進 CEO」的基礎版。

### 目前狀態

| 能力 | Victoria（Stage 15 後） |
|------|----------------------|
| 多輪 Session 對話 | ✅ 30 分鐘 timeout、20 輪上限 |
| 探索 codebase 回答問題 | ✅ Claude Code（Glob/Grep/Read） |
| 記錄決策到文件 | ✅ 可 Edit/Write docs/ + git commit |
| 長期記憶 | ✅ 簡易版（100 筆） |
| 深度分析（多方案 trade-off） | ⚠️ 受限於 15 turns 和 Sonnet 能力 |

### 尚可強化的方向

- **Victoria 使用 Opus 模型**：深度分析時切換 Opus，提升推理品質（但成本增加）
- **動態模型切換**：簡單問題用 Haiku、複雜分析用 Sonnet/Opus（需實作路由邏輯）
- **獨立顧問 Session**：`/consult` 指令啟動長時間深度討論模式，放寬 turns 限制

### 優先級

🔵 低優先級 — Stage 15 的基礎版已能滿足多數需求，視實際使用反饋再強化

---

## 五、Documentation Agent 品質控管

### 背景

DocAgentService 自動產出技術文件並開 PR，目前沒有人工審查以外的機制，文件品質完全依賴 LLM 的輸出。

### 可能的解法

- 維持現有 PR 流程，merge 前審查文件內容（目前做法，最簡單）
- 加入 CEO 二次審查，由 CEO 評估文件品質後才通知你
- 讓 QA 也審查文件正確性

### 優先級

🔵 低優先級 — 目前 PR 審查機制已有一定保護，等實際使用後再評估

---

## 六、AiTeam 安裝精靈

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

## 七、Discord #指令中心 頻道移除

> 狀態：🔵 待執行（小型清理任務）

### 背景

`#指令中心` 是 Stage 2 設計時的遺留頻道。當時作為唯一的指令輸入口，但隨著 per-agent 個人頻道架構成形，老闆現在所有指令都在 `#victoria-ceo` 下達，雙層確認 Embed / 提案書 Embed 也都發在 victoria 頻道，`#指令中心` 已無實際用途。

### 執行步驟

1. 確認 Bot 程式碼中沒有寫死向 `#指令中心` 發送訊息的邏輯（若有，改為 `#victoria-ceo`）
2. 刪除 Discord 上的 `#指令中心` 頻道

### 優先級

🔵 低優先級 — 不影響功能，擇機清理即可

---

## 八、多 LLM 供應商支援（Gemini / OpenAI + Per-Agent 獨立設定）

### 背景

目前 `LlmProviderFactory` 只支援 Anthropic。架構上 `ILlmProvider` 介面已預留擴充點，加入新供應商只需實作介面並在 Factory 新增一個 case。

### 目標

1. **實作 `GeminiProvider : ILlmProvider`** — 串接 Google Gemini API，支援文字與 Vision
2. **每個 Agent 可獨立設定供應商與模型** — `appsettings.json` 的 Agent 設定已有 `Provider` 和 `Model` 欄位，實作後直接生效

設定��例：
```json
"CEO":  { "Provider": "Anthropic", "Model": "claude-sonnet-4-6" },
"Ops":  { "Provider": "Gemini",    "Model": "gemini-2.5-flash"  },
"Doc":  { "Provider": "Gemini",    "Model": "gemini-2.5-flash"  }
```

### 重要限制：Claude Code 綁定

透過 Claude Code CLI 運作的 Agent（Victoria / Cody / Rosa / Demi / Vera / Sage）**只能使用 Claude 模型**，因為 `claude -p` 是 Anthropic 的工具。

多供應商支援僅適用於**直接 API 呼叫路徑**的 Agent：
- ✅ 可換供應商：Quinn（QA）、Rena（Release）、Maya（Ops）
- ❌ 綁定 Claude：Victoria、Cody、Rosa、Demi、Vera、Sage

### 實作重點

- `LlmProviderFactory.Create()` 的 switch 只需新增 `"GEMINI"` / `"OPENAI"` case
- `GeminiProvider` / `OpenAiProvider` 需支援 Vision（CEO / QA 可能傳入圖片）
- Token 追蹤（`TokenTrackingProvider`）包裝層不需改動，對供應商透明
- Dashboard Agent 設定頁面的 Provider 下拉選單需新增選項

### 優先級

🔵 低優先級 — 可換供應商的 Agent（Quinn/Rena/Maya）消耗量本就不高，投資報酬率有限

---

## 九、CEO 長期記憶升級（向量搜索版）

### 背景

Stage 15 的長期記憶採用簡易版（DB 表 + 全量載入 prompt），在記憶量少時（< 100 筆）足夠使用。但隨著使用時間增長��記憶量會超過 prompt 容量限制。

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

## 十、Dashboard Agent 狀態卡即時更新

### 背景

目前 Dashboard 總覽頁的 Agent 狀態卡（「閒置」/「執行中」）在 Agent 開始或完成任務時**不會即時更新**，需手動 F5 刷新才能看到正確狀態。任務清單（最近任務）已透過 SignalR 即時推送正確，但 Agent 狀態卡使用獨立的渲染��徑，`DashboardPushService.PushTaskUpdateAsync` 並未廣播 Agent 狀態變更事件。

### 期望行為

當 `FireOneStepAsync` 啟動 Agent 或任務完成時，Dashboard 總覽頁的對應 Agent 卡片應即時切換「閒置」↔「執行中」，無需頁面刷新。

### 實作方向

1. `DashboardPushService` 新增 `PushAgentStatusAsync(string agentName, string status)` 方法
2. `TaskGroupService.FireOneStepAsync` 在 Agent 開始執行前 Push `"running"`，完成後 Push `"idle"`
3. Dashboard 總覽頁訂閱對應 SignalR 事件，收到後更新對應 Agent 卡片狀態

### 優先級

🟡 中優先級 — 不影���流程正確性，但可觀測性有明顯缺口。與 Stage 13 同期發現。

---

## 十一、任務流程可視化（Pipeline View）

### 背景

當老闆交辦一個任務（例如新功能提案），該任務會經過多個 Agent 處理（Rosa → Petra → Demi → Petra → Cody → Vera → Quinn → Sage）。目前 Dashboard 任務中心只能看到各 TaskItem 的列表，無法直觀看出一個任務的完整流向與當前進度。

### 期望行為

在任務中心點擊任一 TaskGroup 時，展開 **Pipeline View**，以可視化方式呈現該任務的完整流程：

```
 ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐
 │ Rosa │ ─→ │Petra │ ─→ │ Demi │ ─→ │Petra │ ─→ │ Cody │ ─→ │ Vera │ ...
 │  ✅  │    │  ✅  │    │  ✅  │    │  🔄  │    │ 待命 │    │ 待命 │
 └──────┘    └──────┘    └──────┘    └──────┘    └──────┘    └──────┘
```

### 設計方案

**MudStepper 主流程概覽 + MudTimeline 詳細歷程**

1. **MudStepper**（主視圖）：水平 Pipeline 顯示每個 Agent 節點
   - 每個節點：Agent 名稱 + 頭像 + 狀態色
   - 狀態色：灰（待命）/ 藍（執行中）/ 綠（完成）/ 橘（審核中）/ 紅（失敗）
   - 特殊標示：Petra 打回（revise）→ 回退箭頭 + 次數 / Escalate → 紅色警示 / 老闆確認 → 菱形節點

2. **MudTimeline**（點擊展開）：垂直時間線顯示該步驟詳細歷程
   - 開始時間、結束時間、耗時
   - Agent 產出摘要
   - Petra 審核結果（approve / revise / escalate）
   - 打回修正歷史（第幾次、修改指示、修正後結果）

3. **未來可升級**：自製 SVG Pipeline，支援更精緻的回退箭頭和動畫效果

### 前置條件

- ✅ Stage 16 已完成（全 Agent 任務可見性 + Petra 審核 TaskItem）
- TaskItem 資料完整，可渲染完整 Pipeline

### 優先級

🟡 中優先級 — 可觀測性重要升級，需等 Stage 16 任務可見性完成後才能實作

---

## 十二、API 餘額耗盡後的流程恢復機制

### 背景

當 Anthropic API 餘額耗盡導致流程中斷時，目前需要完整重跑整個流程。
對提案階段（Rosa → Demi，約 3-5 分鐘）影響尚可接受；但對開發階段（Cody 正在寫程式）代價很高：
- 若 Claude Code subprocess 已寫了大量程式碼但尚未 commit，重啟後 workspace 清空，所有進度歸零
- 若 branch 已 push 但 PR 尚未開，GitHub 上留有孤兒 branch，需手動清理
- 老闆無法透過一句話讓系統從中斷點恢復

### 期望行為

```
老闆：我充值了，請恢復作業
CEO：好的，從上次中斷點繼續（Cody 重新開始實作）
```

### 設計方向

**提案階段（較簡單）：**
- 在 TaskItem 存儲 Rosa / Demi 各輪產出（JSON）
- 中斷後可從最後成功步驟繼續（例如 Rosa 已 approve，只需重跑 Demi）

**開發階段（較複雜）：**
- `TaskGroup` 加入 `InterruptedAtStep` 欄位記錄中斷點
- CEO 偵測「恢復 / 繼續 / 充值了」等意圖，查詢 DB 找最近失敗的 TaskGroup
- `FireStepsAsync` 支援從指定步驟重新觸發

**孤兒 Branch 防護：**
- Cody 開始寫 code 前先記錄 `BranchName` 到 TaskGroup
- 重試時若 branch 已存在，checkout 到現有 branch 繼續（而非建新 branch）

### 前置條件

- 需要 API 信用錯誤的精確偵測（目前只有一般性 Exception）
- TaskGroup 需要 `InterruptedAtStep` 欄位
- CEO（Victoria）需要識別「恢復」意圖

### 優先級

🟡 中優先級 — 等 Stage 16 驗收通過後評估，Cody 進行中被中斷的場景較為罕見但影響大

---

## 十三、Token 異常消耗保護機制

### 背景

曾發生過某個 Agent 忘記指定 context 範圍，將全系統檔案全部餵進 API，單次請求消耗 80 萬+ Token 的事故。
此類「單次超大 request」不會被 round limit 或 retry 次數限制攔截，且若開啟 Anthropic 自動儲值，會不斷扣款直到發現問題。

### 期望行為

```
單次 API 呼叫 token 數超過閾值（例如 50k）→ 立即中止並發出 Discord 警報
月累計超過 X 美元 → 自動暫停所有 Agent 並通知老闆
```

### 設計方向

- **請求前 token 估算**：對每個 LLM 呼叫，在送出前估算 prompt 長度（字元數 / 4 ≈ token 數），超過閾值直接拒絕並 log
- **硬性月費上限**：`AppSettings` 加入 `MonthlyBudgetUsd` 欄位，Token 監控服務追蹤當月累計費用，超過則鎖定所有 LLM 呼叫
- **現有 `DailyTokenLimitK` / `MonthlyTokenLimitK` 真正落實**：目前只存在 config 裡，並未實際 enforce

### 注意事項

- 這個功能比自動儲值更重要——有了硬性上限，即使有 bug 也不會無限燒錢
- 閾值設計要合理：太低會誤殺正常大型任務（Dev Agent 有大量 codebase 需要讀）

### 優先級

🟠 中高優先級 — 有過實際事故，且自動儲值風險高；但目前以手動充值為保護手段，可在下個 Stage 一起評估

---

## 十四、測試環境隔離（Docker Compose Test Stack）

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

🟡 中優先級 — 解決 CI 打到 production 的根本問題，但短期可用「Playwright 直接打 production」過渡

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

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-01 | 初版建立（原為 Stage_7_Roadmap.md） |
| 2026-04-02 | 改版為 Future_Feature.md，與正式 Stage 7 分離 |
| 2026-04-02 | 新增多項功能候選（QA Playwright、Ops CI/CD 等） |
| 2026-04-03 | 多次整理：移除已移入 Stage 8/9 的項目、重新編號 |
| 2026-04-04 | 大量新增：十二～��六（框架幻覺、技術債、Orchestrator、CEO 補強、文件記錄） |
| 2026-04-04 | 新增十七（UI 規格存 DB）；第一條升為 🔴 排入 Stage 11 |
| 2026-04-05 | 新增十八（唯讀探索）、十九（提案流程重設計）；十七/十八/十九 標記移入 Stage 12 |
| 2026-04-06 | ��增二十（Victoria 技術顧問）、二十一（Dashboard Agent 狀態卡） |
| 2026-04-06 | v2.0 大整理：移除 9 個已完成項目（一、十～十四、十七～十九），重新編號為一～十二 |
| 2026-04-06 | v2.1：第九項（CEO 分類補強）移入 Stage 14 |
| 2026-04-06 | v2.2：第九項標記 ✅ 已完成（Stage 14 驗收通過） |
| 2026-04-07 | v3.1：新增十二（流程恢復機制）、十三（Token 異常消耗保護）|
| 2026-04-06 | v2.3：第十項標記被 Stage 15 吸收；第十一項 Phase 1~2 移入 Stage 15 |
| 2026-04-06 | v2.4：第十一項改為 Phase 1~3 全部移入 Stage 15；新增十三（CEO 長期記憶向量搜索版備案）；原十二改編號為十四 |
| 2026-04-07 | v3.0：移除 3 個已完成項目（九/十/十一→Stage 14/15），重新編號為一～十；更新一（API 費用已部分優化）、四（顧問能力已整合）、八（補充 Claude Code 綁定限制） |
| 2026-04-07 | v3.1：新增十一（任務流程可視化 Pipeline View — MudStepper + MudTimeline） |
| 2026-04-07 | v3.2：新增十四（測試環境隔離 — Docker Compose Test Stack，解決 CI 打到 production 問題） |

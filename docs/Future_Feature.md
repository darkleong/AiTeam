# Future Feature — 未來功能候選清單

> 版本：v1.2
> 建立日期：2026-04-01
> 最後更新：2026-04-05
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。從 Stage_7_Roadmap.md 改版而來。

---

## 一、Dev Agent 使用 Claude Code 寫程式

> **🔴 已排入 Stage 11 — 單獨一個 Stage 專注做這一件事**

### 背景

目前 Dev Agent 透過 Claude API + LibGit2Sharp 操作 repo，本質上是「閉著眼睛寫 code」——看不到完整 codebase、寫完無法驗證 build、不能迭代修復。Stage 10 驗收測試明確暴露了這個問題：相同的任務，Claude Code 能完成，Cody 卻產出大量錯誤（幻覺框架、build 不過、結構不對）。

**這是整個 AiTeam 系統的地基——Agent 能不能寫出可用的程式碼，決定了後面所有流程的價值。**

### 目前 Cody vs Claude Code 差距分析

| | Claude Code | Cody（目前） |
|---|---|---|
| 探索 codebase | Glob / Grep / Read 任意檔案 | 只能看 CEO 塞進 prompt 的有限 context |
| 驗證 | `dotnet build` → 看錯誤 → 修 → 再 build | 寫完就 commit，無法驗證 |
| 迭代修復 | 錯了就改，直到成功 | 基本上一次性產出 |
| 工具 | 檔案系統、bash、搜尋、全套 CLI | 只有 LibGit2Sharp 寫檔 + commit |

### Stage 11 目標

讓 Cody 驅動 Claude Code 執行開發任務，取代目前的 Claude API + LibGit2Sharp 方式。

執行流程變化：
```
目前：
CEO → 把任務 + 有限 context 塞進 prompt → Claude API 一次性產出 → commit

Stage 11：
CEO → 啟動 Claude Code session（帶入任務描述 + UI 規格 + Issues）
       → Claude Code 自己探索 repo、寫 code、build、test、修 error
       → 全部通過後 commit + 開 PR
```

### 連帶效益

Cody 升級後，十四（Orchestrator 流程重構）的多項問題自然減輕：
- Vera 🔴 機率大幅下降（Cody 自己已 build + test 通過）
- 框架幻覺消失（Claude Code 能看到 csproj 依賴）
- fix loop 次數大減（Claude Code 自己迭代到成功才 commit）
- 十二（Dev Agent 框架幻覺防護）可能不再需要

### 待研究事項

- Claude Code SDK 的 C# 呼叫方式（CLI subprocess vs SDK API）
- Bot 在 Docker 容器內如何啟動 Claude Code（需要 dotnet CLI + 檔案系統存取）
- Claude Code session 的 timeout 與資源管理
- CLAUDE.md 如何為 Cody 的 session 客製化

### 優先級

🔴 最高優先級 — Stage 11 唯一項目，系統地基

---

## 二、API 費用優化

### 背景

目前所有 Agent 一律使用 Claude Sonnet（`claude-sonnet-4-6`）。費用預估：

| 使用情境 | 預估月費（美金） |
|---------|---------------|
| 開發測試期 | $15 - $60 |
| 輕度運作（每天 5-10 任務） | $10 - $30 |
| 中度運作（每天 20-30 任務） | $30 - $80 |
| 重度運作（每天 50+ 任務） | $80 - $200 |

### 未來優化方向

- **Prompt Caching**：Anthropic 支援 Prompt Cache，對於每次都重複帶入的規則清單，可大幅降低費用
- **模型降級策略**：信任等級高、任務單純的 Agent，可逐步換成更便宜的模型
- **Fine-tuning**：任務極固定的 Agent，未來可評估 fine-tuned 模型

### 行動建議

- 先觀察實際運作 1-2 個月的用量（Stage 9 Token 監控 Dashboard 上線後更容易評估）
- 再決定是否需要調整模型或引入 Prompt Caching

### 優先級

🔵 低優先級 — 等累積足夠用量數據後評估

---

## 三、MCP（Model Context Protocol）整合

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

## 四、Agent 個性與造型設定

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

## 五、顧問 Agent 設計

### 背景

目前 Claude.ai 扮演顧問角色，負責策略討論與設計決策。Discord CEO Agent 負責日常執行與任務協調。

### 三種方向

| 方案 | 說明 | 適合情境 |
|------|------|---------|
| 方案一：獨立顧問 Agent | 顧問與 CEO 完全分開，各自是獨立 Agent | 團隊規模大、決策複雜 |
| 方案二：顧問能力整合進 CEO | CEO 支援日常模式與顧問模式切換 | 希望單一窗口處理所有事 |
| 方案三：維持現狀 | Claude.ai 繼續扮演顧問，Discord CEO 負責執行 | 現階段最適合 |

### 目前建議

- **短期**：維持方案三，不需要額外開發
- **長期**：等系統穩定後，評估是否採用方案二，把顧問能力整合進 CEO

### 優先級

🔵 低優先級 — 等系統整體穩定後再討論

---

## 六、Documentation Agent 品質控管

### 背景

DocAgentService 自動產出技術文件並開 PR，目前沒有人工審查以外的機制，文件品質完全依賴 LLM 的輸出。

### 可能的解法

- 維持現有 PR 流程，merge 前審查文件內容（目前做法，最簡單）
- 加入 CEO 二次審查，由 CEO 評估文件品質後才通知你
- 讓 QA 也審查文件正確性

### 優先級

🔵 低優先級 — 目前 PR 審查機制已有一定保護，等實際使用後再評估

---

## 七、AiTeam 安裝精靈

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

## 八、Discord #指令中心 頻道移除

> 狀態：🔵 待執行（小型清理任務）

### 背景

`#指令中心` 是 Stage 2 設計時的遺留頻道。當時作為唯一的指令輸入口，但隨著 per-agent 個人頻道架構成形，老闆現在所有指令都在 `#victoria-ceo` 下達，雙層確認 Embed / 提案書 Embed 也都發在 victoria 頻道，`#指令中心` 已無實際用途。

### 執行步驟

1. 確認 Bot 程式碼中沒有寫死向 `#指令中心` 發送訊息的邏輯（若有，改為 `#victoria-ceo`）
2. 刪除 Discord 上的 `#指令中心` 頻道

### 優先級

🔵 低優先級 — 不影響功能，擇機清理即可

---

## 九、多 LLM 供應商支援（Gemini + Per-Agent 獨立設定）

### 背景

目前 `LlmProviderFactory` 只支援 Anthropic，所有 Agent 都用 `claude-sonnet-4-6`。架構上 `ILlmProvider` 介面已預留擴充點，加入新供應商只需實作介面並在 Factory 新增一個 case。

### 目標

1. **實作 `GeminiProvider : ILlmProvider`** — 串接 Google Gemini API，支援文字與 Vision
2. **每個 Agent 可獨立設定供應商與模型** — `appsettings.json` 的 Agent 設定已有 `Provider` 和 `Model` 欄位，實作後直接生效

設定範例：
```json
"CEO":  { "Provider": "Anthropic", "Model": "claude-sonnet-4-6" },
"Ops":  { "Provider": "Gemini",    "Model": "gemini-2.0-flash"  },
"Doc":  { "Provider": "Gemini",    "Model": "gemini-2.0-flash"  }
```

### 實作重點

- `appsettings.json` 的 Agent 設定已有 `Provider` / `Model` 欄位，**不需新增欄位**，直接填值即可生效
- `LlmProviderFactory.Create()` 的 switch 只需新增一個 `"GEMINI"` case，其餘 Agent 邏輯零改動
- `GeminiProvider` 需支援 Vision（CEO / QA 可能傳入圖片）
- Token 追蹤（`TokenTrackingProvider`）包裝層不需改動，對供應商透明
- Dashboard Agent 設定頁面的 Provider 下拉選單需新增 Gemini 選項

### 優先級

🟡 中優先級 — 架構已就緒，等 Gemini API 費率符合需求時實作

---

## 十、提案草稿 UI 規格孤立檔案自動清理

### 背景

提案流程中，Demi 會在 Bot 呼叫 `ShowProposalAsync` 時立即將 UI 規格 commit 到 `docs/ui-specs/`，讓 Embed 可附上 GitHub 連結。但若老闆在看到 Embed 前 Bot 重啟，`_pendingConfirmations` 記憶體清空，Embed 失效，且沒有任何機制追蹤或清理該草稿文件，最終形成孤立檔案。

目前按 ❌ 取消時會刪除，但 Bot 重啟造成的孤立無法處理。

### 建議解法

1. 新增 `pending_proposals` 資料表（`Id`、`UiSpecPath`、`CreatedAt`、`Status`）
2. Demi commit 後寫入一筆 `pending` 記錄
3. 核准 → `approved`；取消 → `cancelled` 並刪除
4. Quartz 定期排程（每小時）掃描 `pending` 且超過 24 小時的記錄，自動刪除 GitHub 檔案

### 優先級

🟡 中 — 目前孤立文件不影響系統運作，只造成 repo 裡的噪音。多專案使用後頻率會上升再考慮排入正式 Stage。

> ⚠️ **若十七（UI 規格改存 DB）實作後，此項目自動消失** — GitHub 上不再有草稿檔案，不可能孤立。

---

## 十一、Dashboard 任務詳情顯示修正

### 問題一：失敗任務看不到失敗原因

點開失敗任務時，執行步驟只顯示最後一筆「執行中」，沒有任何錯誤訊息或失敗原因，無法判斷是什麼導致任務失敗。

### 問題二：完成任務的最後步驟不是完成

點開完成任務時，最後一筆步驟是業務步驟（例如「PR 已開啟」），缺少最終的「完成」步驟，視覺上不清楚任務是否真的結束。

### 建議修正方向

- 失敗時：Bot 在寫入失敗狀態時，同步寫入一筆 `failed` 步驟，內容為錯誤訊息（exception message 或 Agent 回傳的錯誤說明）
- 完成時：Bot 在寫入完成狀態時，同步寫入一筆最終 `done` 步驟

### 優先級

🟡 中優先級 — 影響可觀測性，Debug 時很需要，建議盡早修正

---

## 十二、Dev Agent 框架幻覺防護

### 背景

Stage 10 驗收測試中發現，Dev Agent 在處理某些任務時會「幻覺」出不屬於本專案的框架（例如 `Microsoft.SemanticKernel`），並建立全新檔案而非修改現有檔案。Vera 無法攔截，因為她是逐檔審查，不知道整個 repo 的依賴清單。

### 問題根源

- Dev 的 System Prompt 沒有明確列出**禁用依賴清單**
- Vera 審查時缺乏 `csproj` 依賴 context，無法辨識幻覺框架

### 建議修正方向

1. **Dev System Prompt 加入禁用框架清單**：明確說明本專案不使用 Semantic Kernel、MediatR 等，只能使用 `LlmProviderFactory` / `ILlmProvider`
2. **Vera System Prompt 加入 dependency audit 規則**：若發現 `using` 了非 Anthropic / 非 .NET BCL / 非已知專案命名空間的 library，自動標記為 `warning`
3. **Dev 修改 vs. 新建判斷**：提示 Dev 優先修改現有對應名稱的檔案（`*Service.cs`），而非新建同功能類別

### 優先級

🟡 中優先級 — 影響 Orchestrator 全自動流程的產出品質，建議在 Stage 11 初期修正

---

## 十三、Stage 10 技術債清償

### 背景

Stage 10 驗收後程式碼審查發現數項技術債，依嚴重度分類。

### 🔴 高優先（影響系統穩定性）

**1. TaskGroupService 並行 SaveAsync Race Condition**
- `HandleAgentCompletedAsync` 中對 `DevPrUrl` 和 `LastReviewBody` 分別呼叫 `SaveAsync`，並行 Agent 完成時可能覆蓋彼此更新
- 修正：所有欄位更新完再呼叫一次 `SaveAsync`

**2. 遞迴 Orchestration 無法優雅停止**
- `Task.Run(CancellationToken.None)` 使得 Bot 關閉時背景工作鏈無法被取消
- 修正：接入 `IHostApplicationLifetime` 的 `ApplicationStopping` token，或改用 `System.Threading.Channels` 背景佇列

**3. WebhookController PR synchronize handler 缺少 try-catch**
- GitHub 傳來格式異常的 JSON 時會 crash 整個 endpoint
- 修正：用 `TryGetProperty()` 或包 try-catch

### 🟡 中優先（影響資料一致性或開發體驗）

**4. TaskGroup.Project 用 string 不用 FK**
- `TaskGroup.Project` 是字串（專案名稱），而 `TaskItem.ProjectId` 是 Guid FK，兩者不一致
- 建議：加 `Guid? ProjectId` FK 到 TaskGroup

**5. Dev fix loop 取不到 PR number 時繼續執行**
- `ExtractPrNumberFromText()` 返回 0 時應中斷 fix loop 而非讓 LLM 猜 branch

**6. LLM JSON 解析用 IndexOf('{') 很脆弱**
- CEO、Dev 都用 `IndexOf('{')` / `LastIndexOf('}')` 抓 JSON，如果 LLM 回覆中有巢狀 JSON 或說明文字包含大括號，可能誤判
- 建議：要求 LLM 用 markdown code fence 包 JSON，解析時先找 code fence

### 優先級

🟡 中優先級 — 1~3 建議在 Stage 11 初期修正；4~6 擇機處理

---

## 十四、Orchestrator 流程重構

### 背景

Stage 10 的 Orchestrator 流程存在多項設計缺陷，整體順序需要重構。

### 問題一：QA / Doc / Vera 不應同時並行

目前 Dev PR 開出後，QA + Doc + Vera 三個同時跑。但如果 Vera 發現 🔴，Dev 要修改程式碼，而 QA 和 Doc 已經基於舊程式碼產出了——**等於做白工**。

**修正：Dev → Vera 先審 → Vera ✅ 後再觸發 QA + Doc 並行**

修改 WorkflowEngine 流程表：
```
目前：Dev → QA + Doc + Vera（並行）
修正：Dev → Vera → Vera ✅ → QA + Doc（並行）→ 通知 merge
```

### 問題二：Doc Agent 從任務描述猜路徑，永遠失敗

Orchestrator 觸發 Doc 時，傳過去的是 Dev 的 PR 連結和任務標題，`ExtractPathPrefix` 解析不出有效路徑，導致每次都「略過文件生成」。

**修正：Doc 應讀取 Dev PR 的 changed files 清單來決定要幫哪些檔案寫文件**

### 問題三：fix loop 後 QA / Doc 不更新

`Dev_fix → Reviewer` 只觸發 Vera 重審，QA 和 Doc 不會被第二次觸發。

**修正後此問題自動消失** — 因為 QA / Doc 移到 Vera ✅ 之後才跑，程式碼已經穩定，不需要再更新。

### 問題四：✏️ 調整提案時舊 UI 規格不刪除

按 ✏️ 重新提案時，CEO 重新派 Demi 產出新規格，但舊的 UI 規格文件沒有刪除，repo 裡會累積多份草稿。

**修正：`propose_adjust` 流程中，重新派 Demi 之前先呼叫 `DeleteFileAsync` 清理舊的 `UiSpecPath`**

> ⚠️ **若十七（UI 規格改存 DB）實作後，此問題自動消失** — DB 直接覆蓋，不需要刪 GitHub 檔案。

### 問題五：Bug 修復流程缺少 QA 回歸測試

目前 Bug 修復流程是：`Dev → Vera → Vera ✅ → 通知 merge`，完全沒有 QA 參與。Bug 修復最大的風險是「修好這個、壞了別的」，沒有回歸測試的防護很危險。

**修正：Vera ✅ 後觸發 QA（回歸測試），QA 通過後再通知 merge**

修改 BugFixTable：
```
目前：Dev → Vera ✅ → 通知 merge
修正：Dev → Vera ✅ → QA（回歸測試）→ 通知 merge
```

注意：Bug 修復不需要 Doc（沒有新功能，不需更新文件），只加 QA 即可。

### 修正後的完整新功能流程

```
你說需求
    ↓
CEO 分類：新功能 → 提案模式
Rosa（建 Issues preview）+ Demi（建 UI 規格）並行
    ↓
CEO 發提案書 Embed（附 Issues preview + UI 規格連結）
  [✅ 核准] [✏️ 需調整] [❌ 取消]
    ↓
✏️ → 刪除舊規格 → Demi 重新產出 → 重新提案
❌ → 刪除 UI 規格（Issues 尚未建立，不需清理）
✅ → Rosa 正式建立 GitHub Issues
    ↓
Dev（附帶 Issues + 規格 + repo 結構）
    ↓
Dev PR 開出
    ↓
Vera 審查
  🔴 → Dev 修 → Vera 重審（最多 3 輪）
  ✅ → 程式碼穩定
    ↓
QA + Doc 並行（Doc 讀 PR changed files，不猜路徑）
    ↓
通知你：「PR 可以 merge 了」
```

### 修正後的完整 Bug 修復流程

```
你說 Bug / CEO 分類為 Bug
    ↓
Dev 直接修復（附帶 repo 結構）
    ↓
Dev PR 開出
    ↓
Vera 審查
  🔴 → Dev 修 → Vera 重審（最多 3 輪）
  ✅ → 程式碼穩定
    ↓
QA（回歸測試，確認沒有壞掉其他功能）
    ↓
通知你：「PR 可以 merge 了」
```

### 優先級

🔴 高優先級 — 這是 Orchestrator 自動閉環的核心流程，目前流程有多處不合理，建議作為 Stage 11 主要修正項

---

## 十五、CEO 分類與流程完整性補強

### 背景

目前 CEO 四類分類（新功能 / Bug / 正常行為 / 疑問）無法涵蓋所有開發情境。有幾類常見指令落入灰色地帶，導致 CEO 誤判或老闆必須繞過 CEO 直接到個人頻道操作。

### 問題一：缺少「技術改善」分類

「重構」、「效能優化」、「技術債清償」這類任務的特徵：
- 有開發工作（需要 Dev）
- 不是修 Bug（沒有明確問題報告）
- 不是新功能（不需要 Rosa 建 Issues、Demi 做 UI 規格）
- 需要 Vera 審查 + QA 回歸

目前這類指令很可能被誤判為「新功能」，啟動提案模式浪費 Rosa + Demi 的工作。

**修正：新增第五分類「技術改善」**，流程等同 Bug 修復（Dev → Vera → QA），只是語意正確。

### 問題二：Release / Ops / Doc 沒有 CEO 流程

| 指令範例 | 期望的 CEO 行為 | 目前狀況 |
|---------|--------------|---------|
| 「幫我發布 v1.4.0」 | 派 Rena 執行 Release | 被分到疑問，CEO 不知道要做什麼 |
| 「部署到正式環境」 | 派 Maya 執行部署 | 同上 |
| 「幫我更新 README」 | 派 Sage 更新文件 | 同上 |

這三類任務目前必須老闆自己去個人頻道說話，CEO 完全不知道有 Rena / Maya / Sage 可以用。

**修正：CEO 分類新增 Release、Ops 操作、Doc 更新三類觸發，或整合為第六分類「操作指派」。**

### 問題三：複合指令只能處理一個意圖

老闆說「重構完之後，順便加一個 XX 功能」，CEO 只能選一個分類，另一個被丟掉。

**修正：CEO 能拆解複合指令，依序建立兩個獨立任務群組。**（難度較高，可晚一點處理）

### 問題四：無法取消進行中的任務

老闆說「停掉 Cody 現在在跑的任務」，CEO 完全沒有這個能力。

**修正：CEO 新增「取消任務」指令，呼叫 `TaskGroupService.CancelAsync()`（需新增該方法）。**

### 優先級

🟡 中優先級 — 問題一（技術改善分類）和問題二（Release/Ops/Doc 路由）最實用，建議排入 Stage 11 評估

---

## 十六、CEO Discord 文件記錄能力

### 背景

目前老闆在 Discord 對 CEO 說的想法、決策、設計討論，全部只存在 Discord 聊天記錄裡，沒有任何機制把它們整理進 docs/ 的 markdown 文件。Aria 在 Claude.ai 扮演的「幫老闆記錄想法」角色，Victoria 目前完全做不到。

每次有新決策、新的 future feature 候選、設計注意事項，老闆都要自己找文件手動寫，或請 Claude Code 幫忙——這明顯違反「老闆只動嘴」的原則。

### 目標

Victoria 在 Discord 對話中，能夠直接幫老闆更新 docs/ 的 markdown 文件。

**觸發詞範例：**
- 「記錄下來」、「幫我記到 Future Feature」
- 「幫我更新設計文件」
- 「這個決定記錄進架構文件」

**執行流程：**
```
老闆說：「這個重構方向記錄到 Future Feature」
    ↓
CEO 判斷分類：文件記錄
    ↓
CEO 整理老闆的說法，格式化成對應的 markdown 段落
    ↓
透過 GitHub API commit 更新對應的 docs/ 文件
    ↓
回報：「已記錄到 Future_Feature.md，commit：xxx」
```

### 支援的文件範圍（初版）

- `docs/Future_Feature.md` — 未來功能候選
- `docs/00_Master_Plan.md` — 主索引（通常只更新狀態）
- 其他 docs/ 文件視需求開放

### 實作重點

- CEO System Prompt 需明確告知「記錄」類指令的處理方式
- 需要一個 `MarkdownDocumentService`，封裝 `GitHubService.UpdateFileAsync()`，能夠 append 或 insert 到指定 section
- 文件結構需要足夠規律（標題層級一致），才能讓 CEO 定位到正確的 section

### 優先級

🟡 中優先級 — 對「老闆只動嘴」原則有直接價值，但需要 CEO 對文件結構有足夠理解，實作複雜度中等

---

## 十七、UI 規格改存 DB，取消提案階段的 GitHub Commit

### 背景

目前 Demi 產出 UI 規格後，會立即 commit 到 GitHub（`docs/ui-specs/{slug}.md`），取得永久連結後放進提案書 Embed。但此時提案尚未核准，老闆可能 ❌ 否決或 ✏️ 調整，導致這些 commit 變成垃圾殘留在 git 歷史裡。

**問題：**
- ❌ 否決 → commit 是垃圾（雖然檔案會刪，但 commit 歷史留著）
- ✏️ 調整 → 舊 commit 是垃圾，新的又一筆
- 即使 ✅ 核准 → 「add UI spec for proposal」也是額外的 commit 噪音
- Bot 重啟 → `_pendingConfirmations` 失憶，孤立檔案留在 repo（即十的問題）

### 分析：兩個消費者真正需要什麼

| 消費者 | 需要的東西 | 目前做法 | 其實只需要 |
|--------|-----------|---------|-----------|
| 老闆（看提案書） | 看到完整 UI 規格 | GitHub blob 連結 | Discord 附件檔（.md）或 Dashboard 頁面 |
| Dev（寫 code） | 拿到 UI 規格內容 | GitHub 檔案路徑 | 任務描述直接帶入規格全文 |

兩個消費者都不需要東西在 GitHub 上。

### 修正方案

```
目前：
Demi 產出 markdown → commit 到 GitHub → 取得 URL
                                        ↓
                              提案 Embed 放 GitHub 連結
                              Dev 拿到檔案路徑

改成：
Demi 產出 markdown → 存進 DB（TaskGroup.UiSpecContent）
                      ↓                    ↓
         提案 Embed 附上 .md 檔案     Dev 任務描述直接帶入完整內容
         （Discord attachment）       （不再是路徑，而是規格全文）
```

**什麼時候才 commit？** → Dev 開 PR 的時候，把 UI 規格一起放進 PR。規格文件只會跟著「真正要做的功能」一起進 repo，一筆 commit 都不浪費。

### 連帶效果

- **十（孤立檔案清理）整條可以刪除** — GitHub 上不再有草稿檔案，不可能孤立
- **十四問題四（✏️ 調整時刪舊規格）自動消失** — DB 直接覆蓋，不需要刪 GitHub 檔案

### 實作重點

- `TaskGroup` 新增 `UiSpecContent`（text 欄位），取代 `UiSpecPath`
- `DesignerAgentService` 不再呼叫 `GitHubService.CommitFileAsync()`，改為回傳 markdown 內容
- `CommandHandler` 提案 Embed 改用 Discord 附件（`new FileAttachment(stream, "ui-spec.md")`）
- `TaskGroupService.BuildTaskDescription()` 直接塞入規格全文，不再塞路徑
- Dev 開 PR 時，由 Dev 自行把規格文件放進 `docs/ui-specs/`（Stage 11 Claude Code 可自動做到）

### 優先級

🟡 中優先級 — 不影響功能正確性，但影響 git 歷史乾淨度。可與十四一起處理，或等 Stage 11 後一併調整

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-01 | 初版建立（原為 Stage_7_Roadmap.md） |
| 2026-04-02 | 改版為 Future_Feature.md，與正式 Stage 7 分離 |
| 2026-04-02 | 新增九：QA Agent 視覺截圖測試（Playwright） |
| 2026-04-02 | 新增十至十二：Ops CI/CD 監控、Bot 重啟清理、CEO 確認移除（已移入 Stage 8） |
| 2026-04-02 | 新增十三：CEO 智慧分類 + 提案模式；新增十四：AiTeam 安裝精靈 |
| 2026-04-03 | Stage 9 規劃後整理：移除已移入 Stage 8 的項目（七、八、十、十一、十二）；九、十三 標記移入 Stage 9 並重新編號 |
| 2026-04-03 | 移除已完成項目（七、八 → Stage 9）；其餘重新編號；新增八：Discord #指令中心 頻道移除 |
| 2026-04-03 | 新增九：多 LLM 供應商支援（Gemini + Per-Agent 獨立設定） |
| 2026-04-03 | 新增十：提案草稿 UI 規格孤立檔案自動清理（Bot 重啟導致 pending_confirmations 失憶） |
| 2026-04-03 | 新增十一：Dashboard 任務詳情顯示修正（失敗原因不顯示 + 完成步驟缺失） |
| 2026-04-04 | 新增十二：Dev Agent 框架幻覺防護（Semantic Kernel 幻覺 + Vera 無法攔截的依賴審查） |
| 2026-04-04 | Aria 全面審查：修正第二項過時描述；新增十三（Stage 10 技術債，含 6 項） |
| 2026-04-04 | 新增十四：Orchestrator 流程重構（Vera 先審再 QA/Doc、Doc 讀 PR changed files、fix loop 後不做白工、✏️ 調整時刪舊規格、Bug 修復補 QA 回歸測試） |
| 2026-04-04 | 新增十五：CEO 分類與流程完整性補強（技術改善分類、Release/Ops/Doc 路由、複合指令、取消任務） |
| 2026-04-04 | 新增十六：CEO Discord 文件記錄能力（Victoria 幫老闆把 Discord 對話中的想法直接 commit 進 docs/） |
| 2026-04-04 | 第一條升級為 🔴 最高優先級，排入 Stage 11 唯一項目；補充 Cody vs Claude Code 差距分析與連帶效益 |
| 2026-04-04 | 新增十七：UI 規格改存 DB 取消提案階段 commit（連帶使十、十四問題四可能不再需要） |

# Future Feature — 未來功能候選清單

> 版本：v2.1
> 建立日期：2026-04-01
> 最後更新：2026-04-06
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。已完成項目移至底部「已完成項目摘要」。

---

## 一、API 費用優化

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

## 八、多 LLM 供應商支援（Gemini + Per-Agent 獨立設定）

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

## 九、CEO 分類與流程完整性補強 ⇒ 已移入 Stage 14

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

⇒ 已移入 Stage 14（問題一～三；問題四留給 Stage 15 Victoria 接 Claude Code 後處理）

---

## 十、CEO Discord 文件記錄能力

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

## 十一、Victoria 升級為技術顧問（Discord 版 Claude Code）

### 願景

目前老闆的工作模式是「雙窗口」：

```
深度討論（設計決策、流程分析、規劃） → Claude Code（顧問角色）
日常指令（任務派發、狀態查詢）       → Discord Victoria（CEO 角色）
```

老闆需要在兩邊手動當橋樑——把 Claude Code 的分析結果帶去 Discord，把 Discord 的執行結果帶回 Claude Code。每次規劃一個 Stage，老闆至少來回跑四趟。

**終極目標：Victoria 能承擔顧問角色，老闆只在 Discord 說話就完成一切。**

### 目前 Victoria vs 顧問（Claude Code）的差距

| 能力 | 顧問（Claude Code） | Victoria（目前） |
|------|-------------------|-----------------|
| 對話模式 | 長時間多輪，上下文持續累積 | 每則訊息獨立處理，無跨輪記憶 |
| 思考深度 | 探索 10 個檔案再回答一個問題 | 一次 API call → 回覆 |
| 回溯能力 | 記得 30 分鐘前討論的結論 | 不記得 5 分鐘前的對話 |
| 讀寫檔案 | Glob / Grep / Read / Edit / Write | 無 |
| 執行指令 | dotnet build / git / 任意 bash | 無 |
| 設計決策 | 分析多個方案的 trade-off，給出建議 | 只能分類和路由 |

### 實現路徑（階段性靠近）

| 階段 | Victoria 能做到 | 對應項目 |
|------|---------------|---------|
| 現在 | 分類 + 路由，完全不理解 codebase | — |
| Stage 12 後 | 透過 Rosa / Demi 間接「看」codebase | ✅ 已完成 |
| 十做完後 | 幫老闆記錄想法到文件 | 十 |
| 本項目 | Session-based 深度對話 + 自主探索 + 讀寫文件 | 十一 |

### 需要突破的三道門檻

**1. Session-based 持續對話**

目前 Victoria 是「一問一答」——每則訊息都是獨立的 LLM 呼叫，沒有對話歷史。要支援深度討論，需要：
- Discord 頻道內維護 conversation history（存 DB 或 memory）
- 支援多輪推理：「你剛剛說的十四和十八的關係...」
- session timeout 機制（閒置 30 分鐘後結束 session）

**2. Victoria 自己也用 Claude Code**

不只是路由給其他 Agent 用，Victoria 自己需要能探索 codebase 來回答技術問題。等於 Victoria 從「純 LLM API call」升級為「Claude Code 驅動」。

**3. 長期記憶**

目前只有 rules 表（規則快取）。顧問角色需要：
- 記住設計決策的背景和理由
- 記住 Future Feature 項目之間的關聯
- 記住老闆的偏好和溝通風格
- 類似 Claude Code 的 memory 系統，但存在 PostgreSQL

### 最終願景的工作流程

```
老闆在 Discord 說：「規劃管理頁面不好用」
    ↓
Victoria（Session 模式）：「我看了一下 PlanManagement.razor，
  目前用 MudDataGrid 但沒有篩選功能。你是覺得哪裡不好用？」
    ↓
老闆：「資料太多了，我想要能按狀態篩選」
    ↓
Victoria：「了解。我分析了一下，改動涉及 Dashboard 的 razor 元件和
  Bot 的 TaskGroupRepository 查詢。我讓 Rosa 拆 Issue、Demi 設計 UI，
  等一下給你提案書。」
    ↓
Victoria 自動呼叫 Rosa → Demi → 發提案書
    ↓
老闆：✅
    ↓
Victoria 自動派 Cody → Vera → QA → 通知 merge
```

**老闆只說了兩句話。中間的一切全自動。**

### 優先級

🔵 低優先級 — 這是系統的長期方向，不是短期可實現的。需要先把九（分類補強）、十（文件記錄）等基礎做好，才有條件實現本項目。但值得記錄下來作為系統演進的北極星。

---

## 十二、Dashboard Agent 狀態卡即時更新

### 背景

目前 Dashboard 總覽頁的 Agent 狀態卡（「閒置」/「執行中」）在 Agent 開始或完成任務時**不會即時更新**，需手動 F5 刷新才能看到正確狀態。任務清單（最近任務）已透過 SignalR 即時推送正確，但 Agent 狀態卡使用獨立的渲染路徑，`DashboardPushService.PushTaskUpdateAsync` 並未廣播 Agent 狀態變更事件。

### 期望行為

當 `FireOneStepAsync` 啟動 Agent 或任務完成時，Dashboard 總覽頁的對應 Agent 卡片應即時切換「閒置」↔「執行中」，無需頁面刷新。

### 實作方向

1. `DashboardPushService` 新增 `PushAgentStatusAsync(string agentName, string status)` 方法
2. `TaskGroupService.FireOneStepAsync` 在 Agent 開始執行前 Push `"running"`，完成後 Push `"idle"`
3. Dashboard 總覽頁訂閱對應 SignalR 事件，收到後更新對應 Agent 卡片狀態

### 優先級

🟡 中優先級 — 不影響流程正確性，但可觀測性有明顯缺口。與 Stage 13 同期發現。

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
| 十七 | UI 規格改存 DB | ✅ Stage 12（2026-04-06） |
| 十八 | Agent 唯讀探索能力 | ✅ Stage 12（2026-04-06） |
| 十九 | 提案流程重新設計 | ✅ Stage 12（2026-04-06） |

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

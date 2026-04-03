# Future Feature — 未來功能候選清單

> 版本：v1.1
> 建立日期：2026-04-01
> 最後更新：2026-04-03
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。從 Stage_7_Roadmap.md 改版而來。

---

## 一、Dev Agent 使用 Claude Code 寫程式

### 背景

目前 Dev Agent 是透過 Claude API + LibGit2Sharp 操作 repo。Claude Agent SDK 已正式推出且穩定，未來若能讓 Dev Agent 驅動 Claude Code 執行任務，可省去大量 Git 操作樣板程式，效果可能更佳。

### 可能的方向

| 方向 | 說明 | 成熟度 |
|------|------|--------|
| Claude Code SDK 模式 | 用程式化方式呼叫 Claude Code 執行任務 | 🟡 已可用，正式推出 |
| Claude API + Git 操作 | 目前使用的方式，最可控 | 🟢 穩定，已上線 |
| MCP（Model Context Protocol）| Anthropic 推出的工具整合協議 | 🟡 發展中 |

### 行動建議

- 目前維持 Claude API + Git 的方式
- 定期關注 Claude Code SDK 的更新與文件完善程度
- 等穩定後，只需替換 Dev Agent 的執行層，架構不需大改

### 優先級

🟡 中優先級 — 值得在未來某個 Stage 中期評估

---

## 二、API 費用優化

### 背景

目前 CEO / Dev 使用 Claude Sonnet，Ops 使用 Gemini Flash。費用預估：

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

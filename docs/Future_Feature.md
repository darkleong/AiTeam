# Future Feature — 未來功能候選清單

> 版本：v1.0
> 建立日期：2026-04-01
> 最後更新：2026-04-02
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

- 先觀察實際運作 1-2 個月的用量
- 再決定是否需要調整模型或引入 Prompt Caching

### 優先級

🔵 低優先級 — 等累積足夠用量數據後評估

---

## 三、MCP（Model Context Protocol）整合

### 背景

Anthropic 推出的 MCP 是一個開放協議，讓 LLM 能夠更標準化地使用外部工具。目前 AiTeam 各服務（GitHub、Notion、Discord）均為自行維護的 API 串接。

### 潛在應用

- Agent 透過 MCP 存取 Notion、GitHub、Discord
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

## 七、Dashboard UI 微調清單

> 狀態：✅ 已移入 Stage 8（四）

| # | 問題描述 | 頁面 / 元件 |
|---|---------|------------|
| 1 | 側欄收合後 emoji 圖示顯示為色塊，字型未載入 emoji | NavMenu（sidebar 收合狀態） |
| 2 | 整體 MudBlazor 色彩主題尚未依品牌色統一設定（目前用預設 MudBlazor 配色） | 全域 |
| 3 | 首頁空白時沒有引導提示（Agent 尚未連線時的 Empty State） | 首頁 |

---

## 八、從 Dashboard 重啟 Bot

> 狀態：✅ 已移入 Stage 8（三）

### 兩種可行方案

| 方案 | 說明 | 限制 |
|------|------|------|
| **方案 A：Docker Compose + 內部 API** | Dashboard 呼叫 Bot 暴露的 `/internal/restart` endpoint → Bot 呼叫 `IHostApplicationLifetime.StopApplication()` → Docker Compose `restart: always` 自動重啟容器 | 需部署在 Docker 環境，本機開發時無效 |
| **方案 B：Aspire OrchestratorClient** | 透過 .NET Aspire 的 `IOrchestratorClient` 重啟服務 | 僅限 Aspire dev 模式，正式部署不適用 |

### 優先級

🔵 低優先級 — 待確認部署環境後評估

---

## 九、QA Agent 視覺截圖測試（Playwright）

> 狀態：🔵 低優先級 — 等 Stage 8 完成後評估

### 背景

目前 QA Agent 產出 xUnit 單元測試（純程式碼分析，不開瀏覽器）。若 QA 能用 Playwright 執行 E2E 視覺測試並截圖附加到 PR，老闆看一眼截圖就能確認 UI 修正是否正確，加速 PR merge 判斷。

### 目標場景

以「Dark Mode Grid 改用深色色彩」為例：
- QA Agent 產出 Playwright 測試，切換 Dark Mode、導航到頁面、截圖
- 截圖附加到 GitHub PR comment
- 老闆看截圖確認 OK → 直接 merge，不需要手動開瀏覽器驗證

### 前提條件

App 需要在某個地方跑著才能截圖：

| 方案 | 說明 |
|------|------|
| **方案 B（建議）** | CI/CD pipeline 裡起臨時容器，Playwright 打 localhost 測試完即銷毀 |
| 方案 A | 打 Production 環境（有風險，不建議）|

### 登入驗證方式

採用**方式一：Playwright 直接模擬登入**，帳密從 `.env` 讀取，不寫死在程式碼裡。
個人系統、單一使用者，此方式已足夠。等測試量大到登入速度成為瓶頸再升級為 Storage State。

### 複雜度

需要：Playwright 專案加入 solution、CI/CD 新增 test stage、QA Agent 邏輯改為依任務性質選擇 xUnit 或 Playwright。

### 優先級

🔵 低優先級 — Stage 8 完成後排入討論

---

## 十、Ops Agent 監控 CI/CD 並自動重試

> 狀態：✅ 已移入 Stage 8（二）

### 背景

GitHub Actions Deploy job 偶爾因外部因素失敗（如 Docker Hub 暫時 502），目前需要人工收到 Email 後判斷原因並手動重跑（`gh run rerun`）。

### 目標

讓 Ops Agent 能夠：
1. 監控 GitHub Actions 執行結果
2. 判斷失敗原因（外部故障 vs. 程式問題）
3. 外部故障（如 Docker Hub 502、網路逾時）→ 自動重試 Deploy
4. 程式問題 → 通知老闆並說明原因，不自動重試

### 分工說明

| 職責 | Agent |
|------|-------|
| 監控 Deploy 結果、自動重試 | **Ops Agent** |
| 版本號決定、Changelog、建立 Release tag | **Release Agent** |

### 優先級

🔵 低優先級 — 等 Ops Agent 基本功能穩定後評估

---

## 十一、CEO 穩定後移除派工確認，只保留執行確認

> 狀態：✅ 已移入 Stage 8（五）

### 背景

目前雙層確認機制：
1. **CEO 派工確認**：CEO 解讀意圖後，顯示 Embed 讓老闆確認「CEO 的理解是否正確、要派給哪個 Agent」
2. **Agent 執行確認**：Agent 收到任務後，顯示 Embed 讓老闆確認「任務內容與步驟是否正確」

在 CEO 初期容易誤解意圖，雙層確認是必要的安全機制。待 CEO 的 Prompt 與判斷邏輯穩定、誤派率低後，可移除第一層確認，直接進入 Agent 執行確認，減少操作步驟。

### 條件

- CEO 連續正確解讀意圖達一定次數後考慮啟用
- 可設計為可開關的設定（`AgentSettings__SkipCeoConfirm`）

### 優先級

🔵 低優先級 — 等 CEO 判斷能力穩定後評估

---

## 十二、Bot 重啟時自動清理殘留的「執行中」任務

> 狀態：✅ 已移入 Stage 8（一）

### 背景

Bot 容器重啟（例如 CI/CD Deploy、手動重啟）時，正在執行中的 Agent 任務會被強制中斷。由於沒有 graceful shutdown 機制，資料庫中該任務的狀態會永遠停在「執行中」，無法自動恢復或標記為失敗。

### 解法

在 Bot 啟動時（`IHostedService.StartAsync`），掃描資料庫中所有狀態為「執行中」的任務，將其標記為「失敗」並附上備註：`Bot 重啟，任務中斷`。

### 優先級

🔵 低優先級 — 不影響正常運作，但會造成任務中心顯示異常

---

## 十三、CEO 智慧分類 + 提案模式

> 狀態：🟡 中優先級 — Stage 8 完成後列入正式規劃

### 背景

目前 CEO 的角色偏向「收到指令就找人執行」，缺少主動判斷的能力。老闆（Christ）不見得每次都能清楚分辨自己遇到的是 Bug、新功能需求，還是系統的正常行為。CEO 應該主動做功課，幫老闆釐清，而不是直接派任務出去。

> 「如果 CEO 只是收集資料然後執行，那跟助理就沒差別了。」— Christ

### 目標：CEO 智慧分類

CEO 在回應之前，先主動查詢必要資料（GitHub API、DB），再對老闆的輸入進行分類並說明理由：

| CEO 的分類 | 回應方式 |
|-----------|---------|
| **新功能** | 「Christ，這屬於新功能開發，因為目前沒有相關實作。我找 Rosa 和 Demi 先做一份提案給你看。」|
| **Bug** | 「Christ，這是個異常，登入流程不應該發生這個錯誤。我請 Dev 來修。」|
| **正常行為** | 「Christ，這是正常的。Vera 失敗是因為這個 Project 目前沒有任何 PR，她找不到東西可以 Review。」|
| **疑問** | 直接回答，不派任何任務。 |

### 目標：提案模式（新功能專用）

當 CEO 判斷為新功能時，不直接進入執行，而是先進入提案模式：

```
你說需求
    ↓
CEO 釐清大方向（對話）
    ↓
CEO 協調 Rosa + Demi 並行產出
  Rosa → 功能需求清單、驗收條件
  Demi → UI 規格文件、元件配置
    ↓
CEO 彙整成提案書，附上兩份文件
    ↓
你審核
    ↓
你核准 → CEO 動員 Agent 執行
```

### 技術重點

- CEO System Prompt 新增分類判斷邏輯與說明要求
- CEO 在分類前可查詢 GitHub API（確認是否有相關 PR/Issue）與 DB（確認是否有相關任務記錄）
- CEO 支援 `action: propose`，同時觸發 Rosa + Demi 並收集輸出後彙整
- 確認流程改為針對提案書，而非個別 Agent 任務

### 優先級

🟡 中優先級 — 讓 CEO 從「助理」升級為「有判斷力的主管」

---

## 十四、AiTeam 安裝精靈

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

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-01 | 初版建立（原為 Stage_7_Roadmap.md） |
| 2026-04-02 | 改版為 Future_Feature.md，與正式 Stage 7 分離 |
| 2026-04-02 | 新增九：QA Agent 視覺截圖測試（Playwright） |
| 2026-04-02 | 新增十至十二：Ops CI/CD 監控、Bot 重啟清理、CEO 確認移除（已移入 Stage 8） |
| 2026-04-02 | 新增十三：CEO 智慧分類 + 提案模式 |
| 2026-04-02 | 新增十四：AiTeam 安裝精靈 |

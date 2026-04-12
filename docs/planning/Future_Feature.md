# Future Feature — 未來功能候選清單

> 版本：v5.2
> 建立日期：2026-04-01
> 最後更新：2026-04-12
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

## 二、Agent 個性與造型設定

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

## 四、Discord #指令中心 頻道移除

> 狀態：🔵 待執行（小型清理任務）

### 背景

`#指令中心` 是 Stage 2 設計時的遺留頻道。當時作為唯一的指令輸入口，但隨著 per-agent 個人頻道架構成形，老闆現在所有指令都在 `#victoria-ceo` 下達，雙層確認 Embed / 提案書 Embed 也都發在 victoria 頻道，`#指令中心` 已無實際用途。

### 執行步驟

1. 確認 Bot 程式碼中沒有寫死向 `#指令中心` 發送訊息的邏輯（若有，改為 `#victoria-ceo`）
2. 刪除 Discord 上的 `#指令中心` 頻道

### 優先級

🔵 低優先級 — 不影響功能，擇機清理即可

---

## 五、多 LLM 供應商支援（Gemini / OpenAI + Per-Agent 獨立設定）

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

## 六、CEO 長期記憶升級（向量搜索版）

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

## 七、Token 異常消耗保護機制

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

## 八、測試環境隔離（Docker Compose Test Stack）

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

🔵 低優先級 — 九（Dashboard 存取分層）完成後，Playwright 直接打 localhost 免登入，CI 不再需要啟停容器，本項急迫性大幅降低。待客戶專案（十）需要完整隔離時再重新評估

---

## 九、Dashboard 存取分層（localhost 免登入 + 外部強制登入）

### 背景

目前 Dashboard 無論從哪裡存取都是相同的驗證行為。本機已有 Tailscale Funnel 提供公開 HTTPS 入口（`https://love-desktop.tailcd0255.ts.net`），但 Playwright 測試每次都卡在登入畫面，實作 Session 無法自行完成 UI 驗收。

### 期望行為

| 存取方式 | 需要登入 | 用途 |
|---|---|---|
| `localhost:5051` | ❌ 不需要 | Playwright 測試、本機開發、實作 Session 自行驗收 |
| `https://love-desktop...ts.net` | ✅ 需要 | 外部驗收（手機/筆電）、Aria 遠端確認 |

### 實作方向

**1. ASP.NET Core Middleware — localhost 偵測**

```csharp
if (context.Connection.RemoteIpAddress?.IsLoopback == true)
{
    // 自動通過驗證，不需登入
}
// 其他來源 → 走正常登入流程
```

**2. Docker port binding 收緊**

```yaml
# docker-compose.prod.yml
ports:
  - "127.0.0.1:5051:8080"  # 只有本機能直連（免登入）
```

外部流量只能透過 Tailscale Funnel 進來（帶登入保護）。

**3. 安全架構**

```
外部裝置 → Tailscale Funnel (HTTPS) → 需要登入 ✅
本機      → 127.0.0.1:5051          → 免登入（Playwright / Claude Code）✅
同網段裝置 → ❌ 連不進來（127.0.0.1 binding）
```

### 解鎖的能力

- **實作 Session 自行驗收**：Playwright 不再卡登入，可完整執行截圖測試
- **Aria 遠端協助**：實作 Session 遇到 UI 問題時，Aria 可透過 Tailscale URL 截圖確認
- **手機驗收**：Christ 可在任何裝置上驗收 Dashboard 功能

### 與現有 Future Feature 的關係

- **八（測試環境隔離）**：九解決「測試時卡登入」，八解決「測試打到 production」——互補
- **十（客戶專案交付）**：九是 AiTeam 自身的存取分層，十的客戶 Staging 環境可參考相同模式

### 優先級

🟠 中高優先級 — 完成後實作 Session 可自行驗收 UI，大幅減少 Christ 人工介入；也讓 Aria 能遠端協助診斷問題

---

## 十、客戶專案交付流程與驗收閘門

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

- **八（測試環境隔離）**：八解決 AiTeam 自身的 CI 打到 production 問題；本項目解決客戶專案的完整交付流程，範圍更廣
- **十一（Agent 對抗機制）**：驗收失敗的修正循環可能觸發申訴/熔斷機制

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

---

## 十一、Agent 對抗與糾錯機制

### 背景

已發生過兩次實際事故：
1. **Vera 誤判事故**：Vera 持續報告 false critical，Cody 只能接受並反覆修改。因 Cody 無法反駁審查結果（單向權力結構），即使 Cody「知道」Vera 是錯的也只能執行，直到 Christ 手動介入才發現是 Vera 異常。
2. **實作 Session 死循環事故**：驗收不斷失敗，Session 反覆修正但問題始終存在。因為沒有旁觀者偵測到循環模式，Session 一直做無效修正，直到 Christ 請 Aria 分析才找出根因。

兩個事故的共同根因：**系統缺乏糾錯回路**。當一個 Agent 出錯時，沒有機制讓其他 Agent 質疑、也沒有機制偵測重複失敗。

### 四個機制（由簡到繁）

#### 機制一：申訴機制（Appeal）— 解決事故一 + 預防錯誤前提

申訴不只是「審查結果反駁」，更包含「每個環節都可以質疑上游輸入」。分為兩種模式：

**模式 A — Pre-flight Objection（執行前質疑輸入）**

每個 Agent 接收輸入時，先做理解性檢查。發現矛盾/歧義/資訊不足時，輸出結構化 objection，由 WorkflowEngine 路由回上游修正：

```
Victoria 指派需求給 Rosa
    ↓
Rosa Pre-flight Check：
    ├── 無問題 → 正常執行，產出規格書
    └── 發現問題 → 輸出 objection
         {
           "status": "objection",
           "type": "logical_conflict | ambiguity | missing_info | scope_concern",
           "details": "第 2 點要求所有頁面加權限控制，但第 5 點說 Landing Page 不需登入，兩點矛盾",
           "suggestion": "建議釐清 Landing Page 是否排除在權限控制範圍外"
         }
         ↓
    WorkflowEngine 路由回 Victoria（或上呈老闆）
```

適用於所有 handoff 點：Victoria → Rosa、Rosa → Demi、Demi → Cody、Cody → Vera ... 每個 Agent 都有權在開始工作前說「等等，這裡有問題」。

**模式 B — Review Appeal（執行後反駁審查）**

讓 Cody 可以反駁 Vera 的 critical finding：

```
Vera 報告 critical
    ↓
Cody 對每個 critical 回應：
    ├── "agree" → 接受，執行修正
    └── "disagree + 理由" → 附上具體原因
         ↓
    Petra 仲裁：判斷 Vera 還是 Cody 是對的
```

**兩種模式的差異：**

| | Pre-flight Objection | Review Appeal |
|---|---|---|
| **方向** | 質疑上游的輸入 | 反駁下游的審查 |
| **時機** | Agent 開始前 | Agent 完成後 |
| **目的** | 不在錯誤前提上蓋大樓 | 不因誤判做無效修正 |
| **仲裁者** | 上游 Agent 或老闆 | Petra（PM） |

**防乒乓球機制**：每個 handoff 最多 objection 2 次，超過自動上呈老闆。避免兩個 Agent 在同一問題反覆爭執。

**成本**：低（Pre-flight 只需在 prompt 加入理解性檢查指令 + WorkflowEngine 處理 objection 路由；Review Appeal 多一輪 Cody 回應 + Petra 既有判斷能力）
**對標現實**：Pre-flight = 開工前 kick-off meeting 上 developer 質疑需求；Review Appeal = PR comment 裡回覆「I disagree because...」，Tech Lead 仲裁。

#### 機制二：熔斷機制（Circuit Breaker）— 解決事故二

同一環節重試超過 N 次，自動停止並上報：

```
Cody 修正第 1 次 → Vera 仍報 critical
Cody 修正第 2 次 → Vera 仍報 critical
Cody 修正第 3 次 → 🔴 熔斷觸發！
    → 停止迴圈
    → Victoria 上報 Christ：「同一問題來回 3 次，疑似死循環」
```

**成本**：低（WorkflowEngine 加計數器 + escalation）
**對標現實**：Escalation policy — 問題無法在當前層級解決，往上一層報。

> Petra 已有「最多打回 2 次，超過自動 escalate」的雛形（Stage 16），但 Vera ↔ Cody 修正迴圈目前沒有此保護。

#### 機制三：循環偵測（Loop Detection）— 進階版事故二

比單純計數更聰明：追蹤每次修正的 diff，偵測是否在反覆修改同一段程式碼。

```
第 1 次修正：改了 A、B、C
第 2 次修正：改了 A、B（C 改回去了）
第 3 次修正：又改了 C ← 偵測到 C 被反覆修改（oscillation）
    → 判定為「需求矛盾」或「Vera 判斷互相衝突」，非 Cody 的問題
```

**成本**：中（需要追蹤 diff 歷史並做比對）
**對標現實**：CI/CD 中的 flapping test detection。

#### 機制四：新鮮視角（Fresh Eyes）— 最後手段

熔斷觸發後，不讓同一個 Agent 繼續嘗試，而是啟動一個全新的獨立 Session 來診斷：

```
🔴 熔斷觸發
    ↓
啟動獨立診斷 Session（不帶前面的對話歷史）
    ↓
獨立閱讀：原始需求、程式碼、每一輪 review、每次修正的 diff
    ↓
產出診斷報告：「問題出在 Vera 的第 2 點判斷有誤」或「Cody 第 1 次的修正方向就錯了」
```

**成本**：高（啟動全新 Claude Code Session）
**對標現實**：叫一個不在脈絡中的同事過來看「你覺得問題在哪裡？」（正是 Christ 找 Aria 做的事）

### 四個機制的遞進關係

```
                    ┌── Pre-flight Objection（執行前質疑）
正常流程 → 申訴機制 ┤                          → 熔斷機制 → 新鮮視角 → 上報老闆
                    └── Review Appeal（執行後反駁）  (停止)     (診斷)     (人類介入)

成本：        低                                    低         中         高
頻率：        每次 handoff 可觸發                    偶爾       極少       最後手段
```

### 建議實作順序

| Phase | 包含機制 | 理由 |
|-------|---------|------|
| **Phase 1** | 申訴（Pre-flight + Review Appeal）+ 熔斷 | 成本最低、直接解決兩個已知事故 + 預防未來錯誤前提 |
| **Phase 2** | 循環偵測 + 新鮮視角 | Phase 1 跑穩後再加，提升自動化程度 |

### 優先級

🟠 中高優先級 — 已有兩次實際事故，且未來大量使用 AiTeam 時風險會放大

---

## 十二、Dashboard 雙向操作中心（Discord + Dashboard 雙通道）

### 背景

目前所有需要老闆介入的互動（CEO 雙層確認、流程異常上報、狀態通知等）都只發生在 Discord。Dashboard 是純「唯讀監控」，只能看不能操作。這代表：

- Christ 必須盯著 Discord 才能回覆確認
- 歷史互動記錄散落在 Discord 訊息中，難以回溯查詢
- 未來功能（申訴仲裁、驗收確認）也只能在 Discord 處理

### 期望行為

```
Agent 需要老闆介入（確認 / 上報 / 申訴 / 通知）
        ↓
   統一存入 DB
    ↓           ↓
  Discord     Dashboard（SignalR 即時推送）
    ↓           ↓
  Christ 擇一回覆（先到的算數）
        ↓
   回覆存入 DB
        ↓
   Agent 讀取回覆，繼續流程
```

### 涵蓋的互動類型

| 互動類型 | 目前 | 改後 |
|---------|------|------|
| CEO 提案確認 | Discord only | Discord + Dashboard |
| CEO 執行確認 | Discord only | Discord + Dashboard |
| 流程異常上報（熔斷、錯誤） | Discord only | Discord + Dashboard |
| Agent 狀態通知 | Discord + Dashboard（唯讀） | 不變 |
| 未來：申訴仲裁 | — | Discord + Dashboard |
| 未來：驗收確認 | — | Discord + Dashboard |

### 實作方向

**1. 統一訊息模型**

所有 Boss ↔ Agent 互動存入 DB（新 Entity），不再只活在 Discord 訊息裡：
- 請求方（哪個 Agent、什麼類型）
- 請求內容（確認什麼、附帶資訊）
- 回覆內容（Christ 的回答）
- 回覆來源（Discord / Dashboard）
- 時間戳

**2. 雙通道寫入**

- Discord 回覆 → Bot 攔截 → 寫入 DB → Agent 繼續
- Dashboard 回覆 → API → 寫入 DB → 推送 Discord 同步顯示 → Agent 繼續
- 兩邊先到的算數，後到的標記為「已在另一通道回覆」

**3. Dashboard UI**

- 待處理清單（未回覆的確認/上報/申訴）
- 歷史紀錄（可依時間、Agent、類型篩選）
- 即時通知（SignalR，新請求進來時提醒）

**4. 雙向同步**

- Dashboard 回覆後，Discord 也要顯示（讓 Discord 的對話脈絡完整）
- Discord 回覆後，Dashboard 即時更新狀態（避免重複回覆）

### 與現有 Future Feature 的關係

- **十一（Agent 對抗機制）**：申訴上呈老闆時，Dashboard 成為仲裁介面
- **十（客戶專案交付）**：驗收確認可透過 Dashboard 處理
- **九（存取分層）**：外部透過 Tailscale + 登入存取 Dashboard 操作中心

### 優先級

🟠 中高優先級 — 將 Dashboard 從唯讀升級為操作中心，是多項未來功能的基礎設施

---

## 十三、Agent 任務序列（Per-Agent Task Queue）

### 背景

目前系統建立在「一次只跑一個完整流程」的隱性假設上。實際上只有 Victoria 有 `SemaphoreSlim(1,1)` 保護序列化執行，其他 Agent 都沒有：

| Agent | 有 Lock | 共用資源 | 並行風險 |
|-------|---------|---------|---------|
| Victoria | ✅ `SemaphoreSlim(1,1)` | CLAUDE.md swap | 排隊等待，不會衝突 |
| Cody | ❌ 無 | workspace 目錄、git branch | ⚠️ 兩任務同時跑會互踩 |
| Vera | ❌ 無 | workspace 目錄（唯讀） | 風險較低 |
| Quinn | ❌ 無 | workspace 目錄（唯讀） | 風險較低 |
| Rosa/Demi/Sage | ❌ 無 | workspace 目錄 | ⚠️ 理論上可能衝突 |

一旦開始並行多個任務（例如 Cody 正在開發任務 A，同時 Vera 對任務 B 退回修正需要 Cody 再跑），兩個 Cody session 會操作同一個 workspace，導致檔案互相覆蓋、git branch 衝突。

### 期望行為

每個 Agent 擁有獨立的任務序列（Task Queue），同一時間只處理一件事，其餘排隊等待：

```
Victoria Queue: [需求討論] → [Petra 上報] → [申訴仲裁]
                  ↑ 執行中      等待中         等待中

Cody Queue:     [任務 A 開發] → [任務 B 修正]
                   ↑ 執行中        等待中

Vera Queue:     [任務 A Review]
                   ↑ 執行中

Petra Queue:    （空閒）
```

### 設計方向

**1. Per-Agent Queue**

每個 Agent 一個 FIFO 佇列（預設先進先出）：
- `FireStepsAsync` 不再直接執行，而是將任務放入對應 Agent 的 queue
- 每個 queue 有一個 consumer，依序取出並執行
- Agent 正在忙碌時，新任務自動排隊

**2. Queue 狀態持久化（DB）**

Queue 必須存入 DB，不能只在記憶體中。每個任務有明確的狀態流轉：

```
Queued → Running → Completed
                 → Failed
                 → Interrupted（系統中斷）
```

持久化讓系統重啟後能恢復：
- 啟動時掃描 DB，找出狀態為 `Running` 的任務 → 判定為被中斷 → 標記為 `Interrupted` → 重新排入 queue 執行
- `Queued` 狀態的任務 → 直接還原到 queue 繼續等待

**3. Agent 狀態管理（暫停 / Graceful Shutdown）**

每個 Agent 有四種狀態：

```
Agent 狀態流轉：

Active（正常工作中）
   ├── 收到暫停指令 → Paused（立即暫停，queue 凍結）
   └── 收到停止指令 → Stopping（正在停止中 — 完成手頭任務後停止，不再接新任務）
                           ↓ 手頭任務完成交付
                      Stopped（已停止）

Paused / Stopped → 收到恢復指令 → Active
```

**暫停（Paused）**：立即凍結 queue，不消費任務。適用於調試、模型異常。

```
Cody Queue（暫停中 ⏸️）: [任務 B 修正] → [任務 C 開發]
                            凍結中           凍結中

Christ 在 Dashboard 點擊「恢復」→ Cody 開始處理任務 B
```

**Graceful Shutdown（Stopping → Stopped）**：Agent 收到停止指令後，完成手頭正在執行的任務並交付出去，然後才進入 Stopped。不再接新任務。適用於系統更新部署。

```
Christ：「準備部署，全員停止」

Victoria: Active → Stopping（正在跟 Christ 對話中...）→ 對話結束 → Stopped ✅
Cody:     Active → Stopping（正在開發任務 A...）→ 任務 A 完成並推 PR → Stopped ✅
Vera:     Active → Stopped ✅（手頭沒任務，立即停止）
Petra:    Active → Stopped ✅

Dashboard 顯示：全員 Stopped → 可以安全部署 ✅
```

- 暫停/停止/恢復可從 Discord 或 Dashboard 操作
- Stopping 狀態下不接新任務，新任務排入 queue 等部署完成後處理
- 暫停中的 Agent 不會阻塞其他 Agent 的流程；上游產出的任務正常排入 queue 等待

**4. Maya 自動化部署流程**

當 Christ 驗收完功能確認要上線時，整個部署由 Maya 自動編排：

```
Christ：「Maya，部署上線」
    ↓
Maya 向所有 Agent 發送 Graceful Shutdown 指令
    ↓
Dashboard 即時顯示各 Agent 狀態：
  Victoria: Stopping... → Stopped ✅
  Cody:     Stopping... → Stopped ✅（等待手頭任務完成）
  Vera:     Stopped ✅
  ...
    ↓
Maya 確認所有 Agent 都 Stopped
    ↓
Maya 執行部署（docker compose build + up）
    ↓
部署完成 → Maya 自動恢復所有 Agent → Active
    ↓
Maya 回報：「部署完成，全員已恢復工作」
```

**5. Error 狀態與手動重試（吸收原 API 餘額恢復需求）**

任何 Agent 執行任務時遇到錯誤（API 餘額不足、timeout、rate limit、模型異常等），任務進入 Error 狀態但不從 queue 移除：

```
Cody 執行任務 A → API 餘額不足 → 任務 A 狀態：Error
    ↓
Queue 不消費下一個任務（避免連環失敗）
    ↓
Dashboard 顯示：❌ Cody — 任務 A（Error：API 餘額不足）
                     [重試] [取消]
    ↓
Christ 充值後，在 Dashboard 點擊「重試」
    ↓
任務 A 狀態 → Running → 繼續執行
```

- Error 任務留在 queue 頭部，阻塞後續任務（避免同樣原因連環失敗）
- Dashboard 顯示錯誤原因，Christ 判斷後決定「重試」或「取消」
- 取消的任務從 queue 移除，後續任務繼續
- 不需要 NLP 偵測意圖、不需要精確區分錯誤類型——統一由人工判斷

**6. Crash Recovery（系統中斷恢復）**

系統因更新發布或意外重啟時的恢復流程：

```
系統停止前：
  Cody 正在執行任務 A（狀態：Running）
  Queue 中還有任務 B（狀態：Queued）

系統重啟後：
  1. 掃描 DB → 發現任務 A 狀態 Running → 標記為 Interrupted
  2. 任務 A 重新排入 Cody Queue 頭部（優先執行）
  3. 任務 B 維持 Queued 排在後面
  4. Cody Queue 開始消費 → 重新執行任務 A
```

**7. 優先級支援（未來可選）**

預設 FIFO，但保留優先級擴充點：
- 一般任務：正常排隊
- 修正任務（fix loop）：可優先處理
- 緊急上報：可插隊
- 中斷恢復任務：排入隊首

**8. Dashboard 可視化**

Dashboard 顯示每個 Agent 目前的任務序列：
- 正在執行的任務（進度、耗時）
- 排隊中的任務（順序）
- Error 任務（錯誤原因 + 重試/取消按鈕）
- Agent 狀態：Active / Stopping / Stopped / Paused
- 暫停 / 停止 / 恢復按鈕
- 「全員停止」一鍵操作（部署前用）

### 與現有架構的關係

- 取代目前 Victoria 單獨的 `SemaphoreSlim`，改為統一的 queue 機制
- WorkflowEngine 的 `FireStepsAsync` 改為投入 queue 而非直接 `await`
- Dashboard Agent 狀態卡（Stage 18）可擴充顯示 queue 深度與暫停控制

### 與現有 Future Feature 的關係

- **十一（Agent 對抗機制）**：熔斷機制需要知道 Agent 正在處理什麼，queue 提供這個資訊
- **十二（Dashboard 雙向操作中心）**：Dashboard 顯示 queue 讓 Christ 掌握全局；Victoria queue 解決「討論需求時 Petra 上報被卡住」的問題；Error 任務的重試/取消也透過 Dashboard 操作

### 優先級

🟠 中高優先級 — 目前「一次一個流程」的隱性假設遲早會被打破，提前建立 queue 機制可避免並行衝突事故

---

## 十四、Dashboard UI 細節打磨（第四批）

> 狀態：🔵 低優先級 — UI 組織與使用便利性優化，待 Stage 22 完成後擇機處理

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

### 優先級

🔵 低優先級 — 不影響功能，純 UI 組織與使用便利性優化

---

## 十五、Dashboard 可調整 Token 守門全域限額

> 狀態：🔵 低優先級 — 目前只能改設定檔後重新部署

### 背景

Stage 22 實作了 Token 守門機制，包含：
- **全域月費上限**：`AgentSettings:MonthlyTokenLimitK`（預設 1000K）
- **各 Agent 日限**：`AgentSettings:Agents:{Name}:DailyTokenLimitK`
- **各 Agent 月限**：`AgentSettings:Agents:{Name}:MonthlyTokenLimitK`
- **單次請求上限**：`AgentSettings:SingleRequestTokenLimitK`（預設 50K）

目前這些值只能透過修改 `docker-compose.prod.yml` 環境變數並重新部署來調整。Token 監控頁面的警示訊息也只能說「請至 Bot 設定調整」，沒有直接入口。

### 需求

在 Dashboard 「系統設定」頁面（配合十四、系統設定獨立頁面規劃）加入 Token 守門設定區塊：

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
| ~~Doc Agent 品質控管~~ | Documentation Agent 審查機制 | 移除 — 被十一（Agent 對抗機制）Pre-flight Objection + Stage 16 Petra 審核閘門吸收 |
| ~~API 餘額恢復~~ | API 餘額耗盡後的流程恢復 | 移除 — 被十三（Agent 任務序列）Error 狀態 + Dashboard 手動重試機制完全吸收 |

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

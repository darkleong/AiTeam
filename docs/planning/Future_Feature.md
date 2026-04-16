# Future Feature — 未來功能候選清單

> 版本：v7.0
> 建立日期：2026-04-01
> 最後更新：2026-04-16
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。已完成項目移至底部「已完成項目摘要」。

---

## 零-A、任務列表右側 Log 顯示統一化

> 狀態：🟡 待確認 — 需逐一核查每種 Agent 任務的 Log 顯示行為

### 背景

在「任務列表」頁面點擊任務後，右側會展開顯示 TaskLog 記錄。目前各 Agent 的 MockMode 寫 Log 風格不一致：

- **QA / Reviewer**：寫兩筆 Log（`running` → `done`），完成後點開仍看到第一筆「執行中」
- **Doc**：只寫一筆 `done` Log，第一筆即「完成」
- **PM（Petra）/ Design**：未寫任何 TaskLog，顯示「尚無 Log 記錄」

### 待確認項目

1. 逐一確認每種 Agent 任務（Kickoff / Design / Dev / Reviewer / PM / QA / Doc）右側顯示的 Log 內容與期望是否一致
2. 決定統一規格：所有 Agent 要不要都補 `running` Log？或 PM / Design 是否要補寫 TaskLog？
3. 若有不一致的 Agent，逐一修正 MockMode 的 Log 寫入邏輯

### 優先級

🟡 中優先級 — 不影響流程正確性，但影響 Dashboard 可觀察性

---

## 零、Dashboard 歸檔報告折疊面板

> 狀態：🟡 待實作 — 技術條件已釐清，預計納入下個 Stage

### 背景

Pipeline View 的流程文件折疊面板（Stage 26 追加）目前顯示：提案書、任務計劃書、設計規劃書、實作計劃書、驗收報告、測試報告，共六份文件。

**歸檔報告**（Sage 產出的 `docs/archive/pr{N}-archive.md`）目前只寫入 Git 檔案，沒有存回 DB，所以 Dashboard 無法顯示。

### 實作方式

1. `TaskGroup` Entity 新增 `ArchiveContent string?` 欄位
2. 新增 EF Core Migration
3. `DocAgentService` 完成後，將歸檔報告全文存入 `TaskGroup.ArchiveContent`
4. `TaskGroupDto` 新增 `ArchiveContent` 欄位
5. `DashboardTaskService` 三個 LINQ 投影補上此欄位
6. `PipelineView.razor` 新增 `📦 歸檔報告（Sage）` 折疊面板

### 優先級

🟡 中優先級 — 小工程，可納入下個 Stage 一起做

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

### 重要限制：Claude Code 綁定

透過 Claude Code CLI 運作的 Agent（Victoria / Cody / Rosa / Demi / Vera / Sage）**只能使用 Claude 模型**，因為 `claude -p` 是 Anthropic 的工具。

多供應商支援僅適用於**直接 API 呼叫路徑**的 Agent：
- ✅ 可換供應商：Rena（Release）、Maya（Ops）
- ❌ 綁定 Claude：Victoria、Cody、Rosa、Demi、Vera、Quinn、Sage

### 實作重點

- `LlmProviderFactory.Create()` 的 switch 只需新增 `"GEMINI"` / `"OPENAI"` case
- `GeminiProvider` / `OpenAiProvider` 需支援 Vision（CEO / QA 可能傳入圖片）
- Token 追蹤（`TokenTrackingProvider`）包裝層不需改動，對供應商透明
- Dashboard Agent 設定頁面的 Provider 下拉選單需新增選項

### 優先級

🔵 低優先級 — 可換供應商的 Agent（Rena/Maya）消耗量本就不高，投資報酬率有限

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
3. **所有輪次上限預設 3 輪**：未來可在 Dashboard 動態調整（依賴 FF 十二）
4. **文件存入 DB**：各階段產出統一存入 DB，WorkflowEngine 啟動下游 Agent 前從 DB 取出
5. **BugFix / TechImprovement 精簡路徑**：由 Petra 判斷可跳過哪些階段
6. **Petra 工作量待觀察**：幾乎參與每個階段，Token 消耗需持續監控

---

### Phase 2（待後續 Stage）

以下機制在 Phase 1 跑穩後再評估：

**循環偵測（Loop Detection）**：追蹤每次修正的 diff，偵測是否在反覆修改同一段程式碼（oscillation），比單純計數更聰明。

**新鮮視角（Fresh Eyes）**：熔斷觸發後，啟動全新獨立 Session 診斷問題根因（不帶前面的對話歷史）。對標現實：叫一個不在脈絡中的同事過來看問題。

---

### 實作分期

| Phase | 包含內容 | 狀態 |
|-------|---------|------|
| **Phase 1** | 全流程重構（七階段：需求→設計→開發→審查→QA→歸檔→上線） | ✅ 全部完成（Stage 23~25b，v3.7.0~v3.10.0） |
| **Phase 2** | 循環偵測 + 新鮮視角 | 🔵 待後續 Stage |

### 優先級

🔵 低優先級 — Phase 1 已全部完成，Phase 2 待 Phase 1 實際運行一段時間後再評估是否需要

---

## 九、Dashboard 雙向操作中心（Discord + Dashboard 雙通道）

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

- **八（開發流程重構）**：申訴上呈老闆時，Dashboard 成為仲裁介面
- **七（客戶專案交付）**：驗收確認可透過 Dashboard 處理
- **存取分層（Stage 22 已完成）**：外部透過 Tailscale + 登入存取 Dashboard 操作中心

### 優先級

🟠 中高優先級 — 將 Dashboard 從唯讀升級為操作中心，是多項未來功能的基礎設施

---

## 十、Agent 任務序列 — 後續議題

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

## 十一、Dashboard UI 細節打磨（第四批）

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

### 優先級

🔵 低優先級 — 不影響功能，純 UI 組織與使用便利性優化

---

## 十二、Dashboard 可調整 Token 守門全域限額

> 狀態：🔵 低優先級 — 目前只能改設定檔後重新部署

### 背景

Stage 22 實作了 Token 守門機制，包含：
- **全域月費上限**：`AgentSettings:MonthlyTokenLimitK`（預設 1000K）
- **各 Agent 日限**：`AgentSettings:Agents:{Name}:DailyTokenLimitK`
- **各 Agent 月限**：`AgentSettings:Agents:{Name}:MonthlyTokenLimitK`
- **單次請求上限**：`AgentSettings:SingleRequestTokenLimitK`（預設 50K）

目前這些值只能透過修改 `docker-compose.prod.yml` 環境變數並重新部署來調整。Token 監控頁面的警示訊息也只能說「請至 Bot 設定調整」，沒有直接入口。

### 需求

在 Dashboard 「系統設定」頁面（配合十一、系統設定獨立頁面規劃）加入 Token 守門設定區塊：

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

## 十三、Sage 全系統文件健康檢查

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

## 十四、UAT 驗收階段（待觀察）

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

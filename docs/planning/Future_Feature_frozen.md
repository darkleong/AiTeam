# Future Feature — 冷凍 FF

> 從 `Future_Feature.md` 拆出（2026-05-01）
> 作用：收錄一直 ⚪ 待觀察 / 從未動的 FF — 不刪除，等觸發條件出現再解凍

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

### 解凍觸發條件

- Dashboard 視覺整體穩定（v4 動態架構落地後）
- Christ 想開「Agent 互動頁面」時

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

### 解凍觸發條件

- 系統架構穩定（v4 落地 + 後續 6 個月無重大架構變更）
- 真實「在第二台機器部署」需求出現

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

### 解凍觸發條件

- Trial_v6+ 觀察期間頻繁出現「做出來不是我要的」case（≥ 3 次）
- v4 動態架構落地後 Petra 自我檢查仍擋不下需求偏離


---

## 五、CEO 長期記憶升級（向量搜索版）

> 狀態：低優先級 — Stage 15 簡易版（DB + 全量載入 prompt）目前夠用
> 重新分類：2026-05-09（從 archived_v4 移回 frozen — v4 hierarchical static 沒吸收動態架構假設未實現）

### 解凍觸發條件

- 簡易版記憶量超過 prompt 容量限制（~100 筆 / ~10,000 tokens）
- Christ 反映「Victoria 記憶不足」case >= 3 次

### 修法方向（保留供未來 reference）

PostgreSQL 啟用 `pgvector` + `CeoMemory.Embedding` 欄位 + Embedding API 寫入 + 語意搜索撈相關 5-10 筆。

### v4 兼容性

純 DB / Provider 改動，與 framework 無關。

---

## 二十三、Orchestration 異常退出 Crash Recovery 盲點

> 狀態：待觀察 — Stage 54 Crash Recovery 全切 framework Checkpointing 後行為改變
> 重新分類：2026-05-09（從 archived_v4 移回 frozen — Stage 54 後盲點是否仍存在待 Trial_v7+ 觀察）

### 背景

Stage 31/37 設計前提「crash = 進程被 kill → finally 沒機會跑 → flag 留在非 null」對「邏輯 exception」場景失效（exception 沿 call stack 上傳但 finally 正常清 flag → Recovery 掃不到 → group 卡死）。

### 解凍觸發條件

- Trial_v7+ 觀察期間踩到 logic exception 卡死 case >= 1 次
- 或 Stage 57+ 修 FF 五十一/五十二 過程中順帶處理

### v4 兼容性

Stage 54 framework Checkpointing 已改變行為，需重新驗是否仍踩。

---

## 三十、tech_improvement 工作流的 ghost Dev task

> 狀態：待觀察 — Trial_v3 觀察的 ghost Dev task 在 v4 hierarchical static 是否仍踩需驗
> 重新分類：2026-05-09（從 archived_v4 移回 frozen — hierarchical static 仍有固定 pipeline，可能仍踩）

### 背景

Trial_v3 觀察 tech_improvement 流程任務列表出現 ghost Dev task 永遠 stuck — `ShowDirectAgentConfirm` 建初始 Dev TaskItem 但 Orchestrator 另起爐灶建 Dev_plan + Dev。

### 解凍觸發條件

- Trial_v7+ 觀察 tech_improvement 任務仍出現 ghost task
- 或 Stage 57+ 順帶清

### v4 兼容性

v4 hierarchical static 仍有固定 pipeline，待 Trial_v7+ 觀察是否仍踩。

---

## 十四、Agent I/O 完整記錄（待討論）

> 狀態：待討論 — 等 framework telemetry 涵蓋度確認後再決定
> 重新分類：2026-05-09（從 v4_eval 移到 frozen — MS Agent Framework 內建 telemetry 可能涵蓋）

### 解凍觸發條件

- 確認 MS Agent Framework telemetry 涵蓋度（觀察 Stage 49-55B 已產生的 telemetry）
- 若涵蓋足夠 → 移到 archived（framework 內建吸收）
- 若不夠 → 升 active 補強

### v4 兼容性

依 framework telemetry 涵蓋度決定。

---

## 十九、Agent maxTurns 動態化（Dashboard 可調）

> 狀態：待觀察 — 跟 FF 四十八（Cody Dev_plan maxTurns 配置不足）合併考慮
> 重新分類：2026-05-09（從 v4_eval 移到 frozen — FF 四十八是 specific 子議題，本 FF 涵蓋更廣）

### 解凍觸發條件

- FF 四十八 active 動工時順帶評估是否升級為「全 Agent maxTurns 動態化」
- 或除 Cody Dev_plan 外其他 Agent 也踩 maxTurns 不足

### v4 兼容性

純 prompt 配置動態化，與 framework 無關。

---

## 三十八、跨專案能力研究（多 repo / scaffold / 環境建置 spike）

> 狀態：低優先級 — 子議題 B 耦合 FF 三十六 Phase B 動態架構
> 重新分類：2026-05-09（從 v4_eval 移到 frozen — 等 FF 三十六 Phase B 評估後再看）

### 解凍觸發條件

- FF 三十六 Phase B 動態流程架構評估完成
- 或 Christ 真實「在第二個 repo 工作」需求出現

### v4 兼容性

子議題 B（per-task session 跨 repo）耦合 FF 三十六 Phase B 設計。

---

> 此檔僅含冷凍 FF。其他類型 FF 拆分如下：
> - **進行中 active 主清單** → [`Future_Feature.md`](Future_Feature.md)
> - **已完成項目摘要** → [`Future_Feature_completed.md`](Future_Feature_completed.md)
> - **v4 後重評估** → [`Future_Feature_v4_eval.md`](Future_Feature_v4_eval.md)
> - **v4 動態架構吸收 / framework 內建 / Trial 完成** → [`Future_Feature_archived_v4.md`](Future_Feature_archived_v4.md)
> - **變更紀錄** → [`Future_Feature_changelog.md`](Future_Feature_changelog.md)

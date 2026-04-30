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

> 此檔僅含冷凍 FF。其他類型 FF 拆分如下：
> - **進行中 active 主清單** → [`Future_Feature.md`](Future_Feature.md)
> - **已完成項目摘要** → [`Future_Feature_completed.md`](Future_Feature_completed.md)
> - **v4 後重評估** → [`Future_Feature_v4_eval.md`](Future_Feature_v4_eval.md)
> - **v4 動態架構吸收 / framework 內建 / Trial 完成** → [`Future_Feature_archived_v4.md`](Future_Feature_archived_v4.md)
> - **變更紀錄** → [`Future_Feature_changelog.md`](Future_Feature_changelog.md)

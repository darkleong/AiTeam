# 未來可能性與研究方向

> 建立日期：2026-03-30  
> 說明：此文件記錄目前尚未實作、但值得未來探索的方向與想法。

---

## 一、Dev Agent 使用 Claude Code 寫程式

### 背景

目前 Dev Agent 是透過 Claude API + Git 指令操作 repo。但 Claude Code 本身在互動式開發上體驗更好，未來若能讓 Dev Agent 驅動 Claude Code，效果可能更佳。

### 目前狀況

- 官方目前**沒有**直接讓 Agent 驅動 Claude Code 的方式
- Claude Code 有 `--sdk` 模式，可以程式化呼叫，但目前文件不完整、穩定性待測試

### 可能的方向

| 方向 | 說明 | 成熟度 |
|------|------|--------|
| Claude Code SDK 模式 | 用程式化方式呼叫 Claude Code 執行任務 | 🔴 早期，不穩定 |
| Claude API + Git 操作 | 目前規劃的方式，最可控 | 🟢 穩定，已規劃 |
| MCP（Model Context Protocol）| Anthropic 推出的工具整合協議，未來可能支援更深度整合 | 🟡 發展中 |

### 行動建議

- 目前維持 Claude API + Git 的方式
- 定期關注 Claude Code SDK 的更新與文件完善程度
- 等穩定後，只需替換 Dev Agent 的執行層，架構不需大改

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

- **Fine-tuning**：如果某個 Agent 的任務很固定，未來可以考慮用 fine-tuned 模型，降低 token 消耗
- **Prompt Caching**：Anthropic 支援 Prompt Cache，對於每次都重複帶入的規則清單，可以大幅降低費用
- **模型降級策略**：信任等級高、任務單純的 Agent，可以逐步換成更便宜的模型

### 行動建議

- 先觀察實際運作 1-2 個月的用量
- 再決定是否需要調整模型或引入 Prompt Caching

---

## 三、MCP（Model Context Protocol）整合

### 背景

Anthropic 推出的 MCP 是一個開放協議，讓 LLM 能夠更標準化地使用外部工具。未來 AiTeam 的 Agent 可能可以透過 MCP 更方便地整合各種服務。

### 潛在應用

- Agent 透過 MCP 存取 Notion、GitHub、Discord
- 減少自行維護 API 串接的成本
- 更容易擴充新的工具給 Agent 使用

### 行動建議

- 持續關注 MCP 的生態系發展
- Stage 5 擴充新 Agent 時，評估是否改用 MCP 架構

---

## 四、Agent 個性與造型設定

### 背景

目前 Agent 個性與造型設定延後處理，不影響現有架構。

### 預計包含

- 每個 Agent 的名字與個性描述（寫進 System Prompt）
- Dashboard 辦公室頁面的人物造型替換
- 依狀態有對應動畫（忙碌打字、閒置發呆、錯誤冒汗）
- 辦公區之外加入休息區（Agent 閒置時移動過去）

### 行動建議

- Stage 4 Dashboard 實作前，開一個專門的討論來設計細節

---

## 五、測試 Agent（QA Agent）

### 背景

目前 PR 流程是 Dev Agent 開 PR 後直接通知你審查。未來加入測試 Agent 後，流程變成：
Dev 開 PR → 測試 Agent 執行測試 → 附上報告 → 你審查

### 需要確認的細節

- 測試框架選擇（xUnit / NUnit）
- 測試涵蓋範圍（單元測試 / 整合測試）
- 測試失敗時的處理流程

### 行動建議

- Stage 5 展開時再詳細討論

---

## 六、Discord 圖片輸入支援

### 背景

你在 Discord 傳訊息給 CEO 時，可能會附上截圖（例如 UI 問題、錯誤畫面），希望 CEO 能直接看懂圖片內容並處理。

### 技術可行性

- Discord Bot 可以接收圖片附件（拿到圖片 URL）
- Claude Sonnet（CEO 使用的模型）原生支援視覺輸入（Vision）
- 完整流程：下載圖片 → 轉 Base64 → 連同文字一起傳給 Claude API

### 實際使用情境

```
你在 Discord 傳截圖：「這個頁面的 UI 有問題，幫我修一下」
    ↓
Bot 接收訊息 + 圖片附件
    ↓
下載圖片 → 轉 Base64
    ↓
連同文字一起傳給 Claude API
    ↓
CEO 看懂圖片，分析問題，分派給 Dev
```

### 注意事項

- Discord 圖片 URL 有時效性，需要在過期前下載
- 多張圖片需要一併處理
- 圖片大小需要控管，避免 token 消耗過高

### 行動建議

- 目前 Stage 2 尚未實作此功能
- 可在 Stage 3 完成後，作為 Bot 功能的擴充項目實作

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-03-30 | 初版建立 |
| 2026-03-30 | 新增「Discord 圖片輸入支援」研究項目 |

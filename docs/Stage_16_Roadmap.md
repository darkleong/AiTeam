# Stage 16 — PM Agent（Petra）品質審核閘門

> 版本：v1.1
> 建立日期：2026-04-07
> 狀態：📋 規劃中

---

## 目標

在 WorkflowEngine 的 Agent 產出環節之間加入 PM Agent（Petra）作為品質審核閘門，讓團隊產出在交給老闆之前先經過內部審核，減少老闆的審核負擔。

---

## 一、新增 PM Agent（Petra）

### 1.1 PmAgentService

- 新增 `PmAgentService`，透過 `ClaudeCodeService.RunReadOnlyAsync` 審核 Agent 產出
- 模型：`claude-haiku-4-5`（成本優先）
- Timeout：10 分鐘 / Max Turns：10
- CLAUDE_Petra.md 模板已建立

### 1.2 appsettings.json 設定

```json
"PM": {
  "Provider": "Anthropic",
  "Model": "claude-haiku-4-5",
  "DailyTokenLimitK": 10,
  "MonthlyTokenLimitK": 200
}
```

### 1.3 DB Agent 紀錄

- `agents` 資料表新增 Petra（PM Agent）

### 1.4 Discord 頻道

- 新增 `#petra-pm` 頻道，顯示審核過程與結果

---

## 二、WorkflowEngine 整合審核閘門

### 2.1 審核點

| 觸發時機 | 前一步 Agent | Petra 審查什麼 |
|---------|-------------|-------------|
| Rosa 完成後 | Rosa（Requirements） | Issues 規格完整性、是否遺漏情境 |
| Demi 完成後 | Demi（Designer） | UI 規格與 Issues 的一致性 |
| Vera 完成後 | Vera（Reviewer） | Review 結果的嚴重度判斷 |

### 2.2 審核流程

```
Agent 完成任務 → WorkflowEngine 呼叫 Petra 審核
    ↓
Petra 回傳 JSON：
  { "decision": "approve" | "revise" | "escalate", ... }
    ↓
┌─ approve  → 自動進入下一步
├─ revise   → 打回給原 Agent（帶 revision_instructions），最多 2 次
└─ escalate → 上呈給 Victoria，由 Victoria 轉達老闆
```

### 2.3 打回修正機制

- 每個審核點最多打回 2 次
- 超過 2 次自動 escalate
- 打回時 Petra 提供具體修改指示
- 修改後的產出再次經過 Petra 審核

### 2.4 TaskItem 狀態擴充

新增 `reviewing`（審核中）和 `revision`（修正中）狀態：

```
pending → running → reviewing → [approved → 下一步]
                              → [revision → running → reviewing → ...]
                              → [escalated → 通知老闆]
```

---

## 三、不審核的環節（不經過 Petra）

| 環節 | 原因 |
|------|------|
| Cody 開發後 | Vera 已專門審查程式碼，職責不重疊 |
| Quinn QA 後 | pass/fail 是客觀結果，不需主觀判斷 |
| Sage 文件後 | 風險低，有 PR 流程保底 |

---

## 四、全 Agent 任務可見性

### 4.1 問題

目前 Rosa / Demi 在提案階段由 `CommandHandler` 直接呼叫，不經過 `TaskGroupService`，沒有建立獨立 TaskItem。Dashboard 任務中心完全看不到這兩個 Agent 的工作紀錄。

### 4.2 目前 TaskItem 建立點

| 建立位置 | 涵蓋的 Agent |
|---------|-------------|
| `TaskGroupService.cs` — WorkflowEngine 觸發 | Cody / Vera / Quinn / Sage |
| `CommandHandler.cs` — delegate 路徑 | Rena / Maya / Sage（單一任務） |
| `CommandHandler.cs` — propose 路徑 | 僅 CEO（一筆），Rosa / Demi 無紀錄 |
| `OpsAgentService.cs` / `InternalController.cs` | Maya（部署監控） |

### 4.3 改善方案

1. **提案流程補建 TaskItem**：在 `CommandHandler` 的 propose 路徑中，Rosa 執行前建立 TaskItem（assigned=Rosa），完成後更新狀態；Demi 同理
2. **審核環節補建 TaskItem**：Petra 審核時也建立 TaskItem（assigned=Petra），記錄 approve / revise / escalate 結果
3. **確保所有 Agent 工作都有對應 TaskItem**：Dashboard 任務中心可追蹤完整流程

---

## 五、驗收條件

- [ ] Petra PM Agent 正常運作（RunReadOnlyAsync + CLAUDE_Petra.md）
- [ ] Rosa 產出後 Petra 自動審核，approve 時自動進入 Demi
- [ ] Demi 產出後 Petra 自動審核，approve 時交給老闆確認
- [ ] Vera 審查後 Petra 自動判斷，blocking 打回 Cody / minor 放行
- [ ] 打回修正機制正常（最多 2 次，超過 escalate）
- [ ] #petra-pm 頻道顯示審核過程
- [ ] 現有 BugFix / TechImprovement 流程不受影響（Vera 審查後才觸發 Petra）
- [ ] Rosa / Demi 任務在 Dashboard 任務中心可見
- [ ] Petra 審核任務在 Dashboard 任務中心可見
- [ ] `dotnet build` 通過

---

## 六、風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| 審核增加整體 workflow 時間 | 每個審核點 +5~10 分鐘 | Haiku 模型快速回應；只審核關鍵環節 |
| Petra 誤判（過嚴或過鬆） | 不必要的打回或漏放問題 | 初期觀察並調整 CLAUDE_Petra.md prompt |
| 打回迴圈卡住 | 2 次打回後仍不通過 | 強制 escalate 機制保底 |
| API 成本增加 | 每個 workflow 多 2-3 次 Haiku 呼叫 | Haiku 成本極低（$1/$5 per MTok） |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-07 | v1.0 初版建立 |
| 2026-04-07 | v1.1 新增第四章：全 Agent 任務可見性（Rosa/Demi/Petra TaskItem 補建） |

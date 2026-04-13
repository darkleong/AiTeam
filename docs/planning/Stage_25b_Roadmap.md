# Stage 25b — 開發流程重構 Phase 1d（設計規劃階段）

> Stage：25b
> 對應版本：v3.10.0
> 建立日期：2026-04-14
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 目標

實作 **開發流程重構 Phase 1d**（Future Feature 八 第二階段）：

- **移除提案階段的 Rosa/Demi**（提案簡化，Victoria 直接上呈）
- 新增 **設計階段**（Rosa 拆 Issues + 條件式 Demi UI 規格）
- 實作 **設計會議**（復用 25a 會議引擎，討論 Issues + UI 規格）
- 實作 **會議後調整機制**（Petra 指示 Rosa/Demi 修改，小修自審 / 大改重開）
- 實作 **條件式 Christ 確認**（escalate 才需要，consensus 直接進 Dev_plan）

> 對應 Future Feature：八（Phase 1d — 第二階段）
> Phase 1a（Stage 23）：Review Appeal、實作說明、阻礙報告、Sage 轉型、Git Tag
> Phase 1b（Stage 24）：QA Petra 介入、Dev_plan Appeal、TestReport 結構化、文件傳遞
> Phase 1c（Stage 25a）：Kick-off 會議機制
> Phase 1d（本次）：設計規劃階段

---

## 背景說明

### Feature 八 完成狀況

| 階段             | 內容                                     | 狀態              |
| ---------------- | ---------------------------------------- | ----------------- |
| 第一（需求計劃） | Kick-off 會議 + 任務計劃書 + Christ 確認 | ✅ Stage 25a      |
| 第二（設計規劃） | Rosa Issues + Demi UI + 設計會議         | ❌ → **本次實作** |
| 第三（開發）     | ImplementationNote、阻礙報告、Dev_plan   | ✅ Stage 23 + 24  |
| 第四（審查）     | Review Appeal + Petra 仲裁               | ✅ Stage 23       |
| 第五（QA）       | Petra 四路由判斷                         | ✅ Stage 24       |
| 第六（歸檔）     | Sage 轉型收尾歸檔                        | ✅ Stage 23       |
| 第七（上線）     | Git Tag 自動化                           | ✅ Stage 23       |

### 為什麼需要設計階段

目前 Rosa/Demi 的工作在**提案階段**完成（Victoria 收到需求 → Rosa 拆 Issues → Demi 畫 UI → 打包提案 → Christ 確認）。問題：

1. **Rosa/Demi 缺少 Kickoff context**：他們在 Kickoff 會議之前就完成工作，無法基於全員討論結果優化
2. **提案階段太重**：Victoria 提案需要等 Rosa/Demi 完成（含 Petra 審核迴圈），流程冗長
3. **缺少設計審查**：Rosa 的 Issues 和 Demi 的 UI 規格沒有被其他 Agent（Cody/Quinn）審閱

Stage 25b 將 Rosa/Demi 的工作移到 **Kickoff 之後**，基於任務計劃書（TaskPlan）產出更精準的成果，並透過設計會議讓全員審閱。

### 流程對比

**現有流程：**
```
Victoria 收到需求 → Rosa 拆 Issues → Demi 畫 UI → 打包提案
→ Christ 確認提案
→ Kickoff 會議 → Christ 確認計劃書
→ Dev_plan → Dev → Reviewer → QA → Doc → Merge
```

**Stage 25b 之後：**
```
Victoria 收到需求 → 打包提案（僅需求描述）
→ Christ 確認提案
→ Kickoff 會議 → Christ 確認計劃書
→ 設計階段：Rosa 拆 Issues → (Demi 畫 UI) → 設計會議
→ Dev_plan → Dev → Reviewer → QA → Doc → Merge
```

---

## 核心設計決策

### Rosa/Demi 提案階段完全移除

Christ 確認：提案階段不再需要 Rosa/Demi。Victoria 的提案只包含需求描述，讓 Christ 更快看到提案。Rosa/Demi 的工作全部在設計階段完成，基於 Kickoff 產出的 TaskPlan，品質更好。

### 條件式 Demi 參與

Petra 在設計階段開始時判斷「這個功能是否需要 UI/UX 設計」。依據來源：Kickoff 任務計劃書中記錄了各 Agent 的意見，其中包含「是否需要 Demi 參與設計規劃」。

- **需要 Demi**：有 Dashboard UI 變更、新頁面、Layout 影響
- **不需要 Demi**：純後端邏輯、API 變更、資料庫調整

不需要時，Demi 不參與設計會議（3 人 + Petra），跳過 UI 規格產出。

### 條件式 Christ 確認

Christ 確認：`escalate` 的才需要 Christ，`consensus` 直接進 Dev_plan。

| 會議結果 | 路由 |
|---------|------|
| consensus | Petra 產出設計規劃書 → 直接進入 Dev_plan |
| escalate | Christ 確認（Discord 按鈕，與 Kickoff 相同機制） |

### Session 復用策略

Rosa/Demi 在設計階段的 session 跨越三個階段使用：

```
前置作業（產出 Issues/UI 規格）
    ↓ session 保持開啟
設計會議（基於已有 context 討論）
    ↓ session 保持開啟
會議後調整（如需要，基於已有 context 修改）
    ↓ 設計階段完成
session 關閉
```

這與 25a 中 Petra session 在 Christ 確認前保持開啟的設計一致。

### 設計會議適用範圍

| WorkflowType    | 是否需要設計階段 | 原因                                    |
| --------------- | ---------------- | --------------------------------------- |
| NewFeature      | ✅ 必要          | 新功能需要 Issues 拆分和設計審查        |
| TechImprovement | ❌ 跳過          | 技術改善範圍明確，直接進入 Dev_plan     |
| BugFix          | ❌ 跳過          | Bug 修正範圍最小，直接開發              |

---

## 實作項目

### 25b-1. 移除提案階段 Rosa/Demi

**目標**

簡化 Victoria 的提案流程，移除 Rosa 和 Demi 的提案階段參與。提案只包含需求描述。

**影響範圍**

1. **`CommandHandler.ShowProposalAsync()`**（主要修改點）
   - 移除 Rosa 分析迴圈（`AnalyzeOnlyAsync` + Petra `ReviewRosaAsync` + 修改迴圈，約 150 行）
   - 移除 Demi 設計迴圈（`GenerateDraftAsync` + Petra `ReviewDemiAsync` + 修改迴圈，約 150 行）
   - 保留：Victoria 提案建立 + Christ 確認流程

2. **`CommandHandler.ExecuteProposalApprovedAsync()`**
   - 移除 `reqService.CreateIssuesFromPreviewAsync()`（Issues 改在設計階段創建）
   - TaskGroup 創建時不再傳入 `uiSpecContent` 和 `issueUrlsJson`（設計階段再填入）

3. **`CommandHandler.BuildProposalEmbed()`**
   - 簡化提案 Embed（移除 Issues 預覽和 UI 規格摘要）

4. **`TaskGroupService.FireMockProposalAndContinueAsync()`**
   - 移除 Rosa/Demi/Petra-review 的 mock tasks

**不移除的東西**

- `RequirementsAgentService`（Rosa）— 設計階段復用
- `DesignerAgentService`（Demi）— 設計階段復用
- `PmAgentService.ReviewRosaAsync()` / `ReviewDemiAsync()` — 視設計階段需要可復用或移除

---

### 25b-2. 設計階段前置作業

**目標**

在設計會議之前，Rosa 產出 GitHub Issues，條件式 Demi 產出 UI/UX 規格。

**流程**

```
Kickoff 完成，Christ 確認計劃書
    ↓
TaskGroupService 觸發 Design 步驟
    ↓
Petra session（新建）：讀取 TaskPlan → 判斷是否需要 Demi
    ↓
Rosa session（新建）：
    輸入：TaskPlan + 任務上下文
    工作：探索 codebase → 分析需求 → 產出 Issues 內容
    輸出：Issues JSON（標題、描述、標籤）
    ↓
系統：從 Rosa 輸出創建 GitHub Issues → 儲存 IssueUrls 到 TaskGroup
    ↓
（如需 Demi）Demi session（新建）：
    輸入：TaskPlan + Issues 清單 + 任務上下文
    工作：探索 codebase → 設計 UI/UX → 產出 UI 規格
    輸出：UI/UX 規格 Markdown
    ↓
系統：儲存 UiSpecContent 到 TaskGroup
    ↓
進入設計會議
```

**AllowedTools 策略**

| Agent | 階段 | allowedTools |
|-------|------|-------------|
| Petra | 判斷 | `["Glob","Grep","Read"]`（唯讀） |
| Rosa | Issues 創建 | `["Glob","Grep","Read"]`（唯讀，輸出交由系統創建 Issues） |
| Demi | UI 規格 | `["Glob","Grep","Read"]`（唯讀） |

**Session ID 設計**

| Session | ID 來源 | 說明 |
|---------|---------|------|
| Petra | `group.Id.ToString()` | 固定，跨設計階段全程使用 |
| Rosa | `Guid.NewGuid().ToString()` | 新建，跨前置+會議+調整使用 |
| Demi | `Guid.NewGuid().ToString()` | 新建（如需要），跨前置+會議+調整使用 |
| Cody | `Guid.NewGuid().ToString()` | 新建，僅會議使用 |
| Quinn | `Guid.NewGuid().ToString()` | 新建，僅會議使用 |

---

### 25b-3. 設計會議

**目標**

復用 25a 的 MeetingService 會議引擎，實作設計會議。

**MeetingService 新增 `RunDesignMeetingAsync`**

```
開場：
  Rosa / Demi* / Cody / Quinn 的 session 已就緒
  （Rosa/Demi 復用前置作業的 session，Cody/Quinn 新建）
    ↓
第一輪：
  系統 → Rosa session（resume）：「以下是其他 Agent 對 Issues 的意見，有什麼要補充？」
  系統 → Demi session（resume，如有）：「以下是其他 Agent 對 UI 規格的意見，有什麼要補充？」
  系統 → Cody session（new）：「以下是 Issues + UI 規格，從開發角度評估技術可行性」
  系統 → Quinn session（new）：「以下是 Issues + UI 規格，從測試角度評估可測試性」
  收集 3~4 位 Agent 的意見
    ↓
  系統 → Petra session（resume）：「以下是大家對設計成果的意見，請整理並判斷」
  Petra → 系統：判斷 JSON
    ↓
若 needs_discussion → 下一輪（最多 DesignMeetingMaxRounds 輪）
若 needs_adjustment → 進入調整流程（25b-4）
若 consensus → Petra 產出設計規劃書 → 結束
若 escalate → Christ 確認
```

**與 Kickoff 會議的差異**

| 差異點 | Kickoff | 設計會議 |
|--------|---------|---------|
| 討論對象 | 需求說明（文字） | Issues + UI 規格（具體產出） |
| Rosa/Demi session | 全新 | 復用前置作業 session |
| Demi 參與 | 必定參加 | 條件式 |
| 會議後 | 只有 Christ 確認 | 有調整流程 + 條件式 Christ 確認 |
| Petra 判斷選項 | consensus / needs_discussion / escalate | + needs_adjustment |

**Petra 判斷輸出格式**

```json
{
    "decision": "consensus | needs_discussion | needs_adjustment | escalate",
    "summary": "整理摘要",
    "discussion_points": ["需要進一步討論的點"],
    "adjustment_targets": ["rosa", "demi"],
    "adjustment_instructions": {
        "rosa": "Rosa 的修改指示",
        "demi": "Demi 的修改指示"
    },
    "escalate_reason": "上呈原因"
}
```

- `consensus`：大家沒有重大分歧 → 產出設計規劃書
- `needs_discussion`：有需要討論的分歧 → 下一輪
- `needs_adjustment`：Issues 或 UI 規格需要修改 → 進入調整流程
- `escalate`：發現計劃書本身有根本性問題，或無法在團隊內解決 → Christ 介入

**會議角色 prompt 重點**

| 角色  | 指引重點 |
|-------|---------|
| Petra | 主持設計審查，評估 Issues 拆分是否合理、UI 規格是否完整、整體設計是否可行 |
| Rosa  | 回應其他 Agent 對 Issues 的疑問，說明需求拆分的理由 |
| Demi  | 回應其他 Agent 對 UI 規格的疑問，說明設計決策的考量 |
| Cody  | 評估技術可行性，指出 Issues 間的依賴關係、潛在技術風險、實作困難點 |
| Quinn | 評估可測試性，指出哪些 Issues 難以自動化測試、測試策略建議 |

---

### 25b-4. 會議後調整機制

**目標**

當設計會議發現 Issues 或 UI 規格需要修改時，Petra 指示 Rosa/Demi 修改，然後評估修改幅度。

**流程**

```
Petra 判斷 needs_adjustment
    ↓
系統 → Rosa session（resume）：「Petra 的修改指示：{instructions}，請調整 Issues」
（如有 Demi）系統 → Demi session（resume）：「Petra 的修改指示：{instructions}，請調整 UI 規格」
    ↓
Rosa/Demi 完成修改，回傳調整後的內容
    ↓
系統更新 GitHub Issues / UiSpecContent
    ↓
系統 → Petra session（resume）：「Rosa/Demi 已完成修改，以下是調整內容。請評估修改幅度」
    ↓
Petra 回應（末尾 JSON）：
```

```json
{
    "evaluation": "approved | needs_meeting",
    "design_plan": "設計規劃書內容",
    "reason": "需要重開會議的原因"
}
```

- `approved`：修改幅度小，Petra 自己審核通過 → 設計規劃書產出 → 結束
- `needs_meeting`：修改幅度大，需要重開設計會議 → 遞增 DesignRound → 回到會議流程

**調整迴圈上限**

調整流程計入設計會議輪次（DesignRound），與會議討論共享 `DesignMeetingMaxRounds` 上限。超過上限自動 escalate 給 Christ。

**邊界情況：會議中才發現需要 Demi**

如果 Petra 初始判斷「不需要 Demi」，但設計會議中 Cody 或 Quinn 指出需要 UI 變更：
- Petra 的 needs_adjustment 判斷中包含 Demi → 系統為 Demi 開新 session → Demi 補做 UI 規格

---

### 25b-5. 條件式 Christ 確認

**目標**

設計會議 consensus 時直接進 Dev_plan，escalate 時才需要 Christ 確認。

**consensus 路徑（不需 Christ）**

```
設計會議 consensus
    ↓
Petra 產出設計規劃書 → 存入 TaskGroup.DesignPlan
    ↓
關閉所有 session
    ↓
FireStepsAsync → Dev_plan
```

**escalate 路徑（需要 Christ）**

復用 Kickoff 的 Christ 確認機制（Discord 按鈕），CustomId 格式：

```
design_continue_{groupId}
design_stop_{groupId}
design_modify_{groupId}
```

```
設計會議 escalate
    ↓
Victoria 上呈 Christ：
    「設計會議有以下問題需要您決定：{escalate_reason}
     完整設計會議紀錄請查看 Dashboard。
     請選擇：繼續 / 停止 / 修改」
    ↓
Christ 選擇：
    ├── 繼續 → 關閉所有 session → Dev_plan
    ├── 停止 → 關閉所有 session → 任務取消
    └── 修改（附帶意見）
            ↓
        Petra session 仍開啟（有完整設計階段 context）
        系統 → Petra session：「Christ 修改意見：{意見}」
            ↓
        Petra 評估：
            ├── impact: small → 調整設計規劃書 → 再次上呈
            └── impact: large → 退回重新設計（遞增 DesignRound）
```

---

### 25b-6. DB 基礎設施 + WorkflowEngine

**TaskGroup 新增欄位**

| 欄位               | 型別           | 說明                                          |
| ------------------ | -------------- | --------------------------------------------- |
| `DesignMeetingLog` | text nullable  | 完整設計會議紀錄（Markdown，含調整紀錄）      |
| `DesignPlan`       | text nullable  | Petra 產出的設計規劃書                        |
| `DesignRound`      | int, default 0 | 設計會議輪次計數（含調整重開）                |

> 現有欄位 `IssueUrls` 和 `UiSpecContent` 將改為在設計階段填入（原在提案階段填入）。

**WorkflowSettings 新增**

```json
"DesignMeetingMaxRounds": 3
```

**AgentNames 新增**

```csharp
public const string Design = "Design";
```

**WorkflowEngine 修改**

```
現有（25a）：proposal_approved → Kickoff → Dev_plan → Dev → ...
新增（25b）：proposal_approved → Kickoff → Design → Dev_plan → Dev → ...
```

BugFix / TechImprovement 不變（跳過 Kickoff 和 Design）。

**EF Migration**

```bash
dotnet ef migrations add Stage25bDesignFields \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard
```

---

## 設計規劃書格式

Petra 在設計會議結束後（consensus 或 Christ 確認後）產出：

```markdown
# 設計規劃書

## 需求摘要
{來自 TaskPlan}

## GitHub Issues 清單
| # | Issue | 標題 | 負責 | 說明 |
|---|-------|------|------|------|
| 1 | #XX   | ...  | Cody | ...  |

## UI/UX 規格摘要（如適用）
{Demi 的 UI 規格重點}

## 設計決策
- {設計會議中達成的共識}
- {解決的技術疑慮}

## 各角色意見摘要
| 角色  | 主要意見 | 結論 |
|-------|---------|------|
| Rosa  | ...     | ...  |
| Demi  | ...     | ...  |
| Cody  | ...     | ...  |
| Quinn | ...     | ...  |

## 風險與注意事項
- {設計會議中提出但未完全解決的項目}

## 開發建議
{基於設計審查的技術方向建議}
```

---

## 文件傳遞矩陣

Stage 25b 完成後，後續 Agent 收到的文件：

| Agent     | 收到的文件                                                |
| --------- | --------------------------------------------------------- |
| Dev_plan  | TaskPlan + DesignPlan + Issues + UiSpec（如有）           |
| Dev       | TaskPlan + DesignPlan + Issues + UiSpec + Dev_plan        |
| Reviewer  | TaskPlan + Issues + UiSpec + ImplementationNote           |
| QA        | TaskPlan + Issues + UiSpec + Dev_plan + ImplementationNote |
| Doc       | TaskPlan + TestReport                                     |

> BuildTaskDescription 需對應更新，加入 DesignPlan。

---

## 會議紀錄格式

```markdown
# 設計會議紀錄

## 前置作業

### Rosa — GitHub Issues
{Rosa 的完整 Issues 產出}

### Demi — UI/UX 規格（如適用）
{Demi 的完整 UI 規格}

### Petra — 設計需求判斷
需要 Demi：是/否
判斷依據：{reason}

## Round 1

### Rosa（需求分析）
{Rosa 的完整回應}

### Demi（UI/UX 設計）（如適用）
{Demi 的完整回應}

### Cody（技術可行性）
{Cody 的完整回應}

### Quinn（測試規劃）
{Quinn 的完整回應}

### Petra（綜合整理）
{Petra 的完整判斷 JSON}

## 調整紀錄（如有）

### Petra 修改指示
{修改指示內容}

### Rosa 調整結果
{調整後的 Issues}

### Demi 調整結果（如適用）
{調整後的 UI 規格}

### Petra 評估
{approved / needs_meeting JSON}

## Round 2（如有）
...
```

---

## 與現有流程的關係

**Victoria 提案流程簡化**：移除 Rosa/Demi 後，Victoria 的提案只包含需求描述。提案流程從「Victoria + Rosa + Demi + Petra 審核」變為「Victoria 單獨」，大幅縮短提案時間。

**Kickoff 不受影響**：Stage 25a 的 Kickoff 會議流程不變。Kickoff 仍然在 Design 之前，產出 TaskPlan 作為 Design 的輸入。

**Rosa/Demi Agent Service 保留**：不移除 `RequirementsAgentService` 和 `DesignerAgentService`，設計階段復用其能力。需要新增方法或修改輸入格式以接收 TaskPlan。

**MockMode**：設計階段的 Rosa/Demi 前置作業和設計會議都需要 MockClaudeCodeService 支援。Mock 模式下 Petra 的判斷走 consensus fallback（同 Kickoff 的 MockMode 策略）。

---

## 實作順序建議

```
1. 25b-6（DB 欄位 + WorkflowSettings + AgentNames + WorkflowEngine）← 先建基礎
2. 25b-1（移除提案階段 Rosa/Demi）← 簡化提案流程
3. 25b-2（設計階段前置作業：Rosa Issues + 條件式 Demi）← 依賴 25b-6 的 DB + Workflow
4. 25b-3（設計會議 RunDesignMeetingAsync）← 依賴 25b-2 的前置作業
5. 25b-4（會議後調整 + 25b-5 條件式 Christ 確認）← 依賴 25b-3 的會議流程
6. 版本號更新 → 3.10.0
```

---

## 不在 Stage 25b 範圍

| 項目                       | 原因                                               |
| -------------------------- | -------------------------------------------------- |
| Dashboard 設計會議紀錄頁面 | 會議紀錄已存 DB，Dashboard 顯示可在後續 Stage 做   |
| TechImprovement 設計階段   | 先觀察 NewFeature 效果，後續可擴展                 |
| 設計會議後自動 merge Issues | Rosa 的 Issues 已由系統創建，不需要額外 merge 步驟 |
| Phase 2（循環偵測 + 新鮮視角）| Feature 八 Phase 2，待 Phase 1 跑穩後再加         |

---

## 驗收清單

- [ ] 提案流程簡化：Victoria 提案不再包含 Issues 和 UI 規格
- [ ] 提案流程簡化：Rosa/Demi 的提案階段迴圈已移除
- [ ] 設計階段：Kickoff 完成後觸發 Design 步驟
- [ ] 設計階段：Petra 判斷是否需要 Demi
- [ ] 設計階段：Rosa 基於 TaskPlan 產出 GitHub Issues
- [ ] 設計階段：Demi 基於 TaskPlan + Issues 產出 UI 規格（條件式）
- [ ] 設計會議：Rosa/Demi 復用前置作業 session
- [ ] 設計會議：Cody/Quinn 新建 session 參與
- [ ] 設計會議：Petra 整理意見，最多 DesignMeetingMaxRounds 輪
- [ ] 設計會議：完整會議紀錄存入 DesignMeetingLog
- [ ] 調整機制：needs_adjustment → Petra 指示 Rosa/Demi 修改
- [ ] 調整機制：小修 → Petra 自審 → 產出設計規劃書
- [ ] 調整機制：大改 → 重開設計會議（遞增 DesignRound）
- [ ] 條件式確認：consensus → 直接進 Dev_plan（不需 Christ）
- [ ] 條件式確認：escalate → Christ 確認（Discord 按鈕）
- [ ] 設計規劃書：Petra 產出存入 DesignPlan
- [ ] 文件傳遞：BuildTaskDescription 加入 DesignPlan
- [ ] MockMode：前置作業 + 會議 session 使用 MockClaudeCodeService
- [ ] MockMode：Petra 判斷走 consensus fallback → 直接進 Dev_plan
- [ ] BugFix / TechImprovement 跳過 Design 步驟
- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] `.csproj` 版本更新為 `3.10.0`

---

## 注意事項

1. **提案流程移除 Rosa/Demi 的影響範圍**：`ShowProposalAsync()` 約 300 行涉及 Rosa/Demi 迴圈需移除。`ExecuteProposalApprovedAsync()` 需跳過 `CreateIssuesFromPreviewAsync()`。需仔細確認不會破壞提案流程的其他部分（如 pending confirmation 資料結構）。

2. **Rosa Issues 創建方式**：設計階段的 Rosa 使用 Claude Code session 產出 Issues 內容（JSON），然後由系統（RequirementsAgentService）創建實際 GitHub Issues。需確認現有 `CreateIssuesFromPreviewAsync` 是否可復用，或需要新方法接收 TaskPlan 格式的輸入。

3. **Session 生命週期管理**：設計階段有 3~5 個 session 同時存活（Petra + Rosa + Demi + Cody + Quinn），且 Rosa/Demi 的 session 跨前置作業、會議、調整三個階段。需注意 session 清理，所有 session 在設計階段結束時統一關閉。

4. **Petra session ID 與 Kickoff 的衝突**：Kickoff 的 Petra session 使用 `group.Id.ToString()`。Design 階段的 Petra 也使用 `group.Id.ToString()` 的話，可能會恢復到 Kickoff 的舊 session。需要區分（例如加前綴或使用不同 ID）。實作 Session 應驗證此行為。

5. **設計會議的 maxTurns**：會議 Agent 的 maxTurns 建議與 Kickoff 相同（10~15）。前置作業的 Rosa/Demi maxTurns 可能需要更高（20~30），因為他們需要更多 tool calls 來產出 Issues 和 UI 規格。

6. **調整流程中 Rosa 的 Issues 修改**：Rosa 修改 Issues 時，GitHub Issues 已經創建。修改方式可能是：(a) 更新現有 Issue 的 body、(b) 新增 Issue、(c) 關閉不需要的 Issue。需確認 RequirementsAgentService 是否有更新 Issue 的能力。

---

## 變更紀錄

| 日期       | 版本 | 內容                                   |
| ---------- | ---- | -------------------------------------- |
| 2026-04-14 | v1.0 | Aria 撰寫初版規劃書（Christ 確認三項設計決策後） |

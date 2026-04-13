# Stage 24 — 開發流程重構 Phase 1b（QA 改造 + Dev_plan 審核強化 + 文件基礎設施）

> Stage：24
> 對應版本：v3.8.0
> 建立日期：2026-04-12
> 狀態：✅ 實作完成（2026-04-13）
> 文件版本：v1.1

---

## 目標

實作 **開發流程重構 Phase 1b**（Future Feature 八 的子集）：

- 補完 Stage 23 未涵蓋的**非會議制**流程改善
- 聚焦第三階段（Dev_plan 審核強化）、第五階段（QA Petra 介入）
- 建立文件存 DB 基礎設施，為後續會議制做準備

> 對應 Future Feature：八（Phase 1b）
> Phase 1a（Stage 23）已完成：Review Appeal、實作說明、阻礙報告、審查報告格式、版本號檢查、Sage 轉型、Git Tag
> 多 Agent 會議機制（Kick-off + 設計會議）留待後續 Stage

---

## 背景說明

### Stage 23 已完成

| 階段         | Phase 1a 完成項目                                               |
| ------------ | --------------------------------------------------------------- |
| 第三（開發） | 實作說明、阻礙報告                                              |
| 第四（審查） | Review Appeal 迴圈 A + Petra 仲裁 + 版本號檢查 + ReviewIssue.Id |
| 第六（歸檔） | Sage 轉型為收尾歸檔員                                           |
| 第七（上線） | Git Tag 自動化                                                  |

### Phase 1b 要完成的

| 階段         | Phase 1b 項目               | 現狀問題                                                             |
| ------------ | --------------------------- | -------------------------------------------------------------------- |
| 第三（開發） | Dev_plan 審核 + Cody 可反駁 | Petra 已能審核 Dev_plan（Stage 16），但 Cody 無法反駁打回            |
| 第五（QA）   | Petra 介入 QA 迴圈          | Quinn 測試失敗直接給 Cody，缺乏分類判斷（bug / 環境 / 測試本身問題） |
| 跨階段       | 文件存 DB + 傳遞            | 流程產出文件分散，下游 Agent 無法完整參考上游資料                    |

---

## 實作項目

### 24-1. QA 流程改造（Petra 介入 QA 迴圈）

**現狀**

Quinn 測試失敗 → Cody 直接修正 → Quinn 重測（FixIteration 計數），缺乏中間判斷層。

**新流程**

```
Quinn 執行測試 → 產出測試報告
    ↓
全部通過 → 進入下一階段
    ↓
無適用測試（no_applicable_tests + 理由）→ Petra 輕量判斷：
    ├── 理由合理 → 放行
    └── 理由不合理 → 要求 Quinn 補寫測試
    ↓
有失敗 → 報告 Petra
    ↓
Petra 判斷：
    ├── 明確是功能 bug → 派 Cody 修正
    ├── 環境 / 測試本身問題 → Petra 自行判斷或上呈
    └── 不確定 → 派 Cody 調查（Cody 可回報「測試有問題」）
    ↓
Cody 修正完 → 回報 Petra
    ↓
Petra 決定：
    ├── 小修正 → Quinn 重測
    ├── 大幅改動 → 退回 Vera 重新審查
    └── 反覆失敗（超過 3 輪）→ 上呈 Christ
```

**實作位置**

- `TaskGroupService.cs`：QA 完成後的路由邏輯改造
    - 現有：QA 失敗 → 直接觸發 Dev_fix → Reviewer → QA（迴圈）
    - 新增：QA 失敗 → Petra 評估 → 依判斷決定路由
- `PmAgentService.cs`：新增 `AssessQaFailureAsync` 方法
    - 輸入：Quinn 的測試報告 + 任務上下文
    - 輸出：routing（`fix_cody` / `back_to_vera` / `escalate` / `test_issue`）+ instructions
- `PmAgentService.cs`：新增 `AssessNoApplicableTestsAsync` 方法
    - 輸入：Quinn 的 no_applicable_tests 理由
    - 輸出：`pass` / `require_tests`
- `CLAUDE_QA.md`：加入 `no_applicable_tests` 輸出格式規範
- `QaAgentService.cs`：解析 Quinn 的 `no_applicable_tests` 輸出

**設定**

`appsettings.json` 的 `WorkflowSettings` 新增：

```json
"QaFixMaxRounds": 3
```

---

### 24-2. Dev_plan 審核強化（Cody 可反駁 Petra 打回）

**現狀**

Petra 審核 Dev_plan → approve / revise / escalate。Cody 被打回時只能修改重交，無法反駁。

**新流程**

```
Cody 撰寫 Dev_plan
    ↓
Petra 審核：
    ├── approve → Cody 開始開發
    ├── revise → Cody 可選擇：
    │       ├── 接受（agree）→ 修改 Dev_plan → 重交 Petra
    │       └── 反駁（disagree + 理由）→ Petra 重新評估
    │               ↓
    │           Petra 接受反駁 / 維持打回（迴圈，上限 3 輪）
    │               ↓
    │           超過上限 → Victoria 上呈 Christ
    └── escalate → Victoria 上呈 Christ
```

**實作位置**

- `PmAgentService.cs`：新增 `RunCodyDevPlanAppealAsync` 和 `ReassessDevPlanAsync` 方法
    - 復用 Stage 23 的 appeal 模式（LLM 直呼叫，非 Claude Code）
- `TaskGroupService.cs`：`RunPetraDevPlanReviewAsync` 回傳 revise 時，進入 appeal 迴圈
    - 復用 `HandleReviewerCompletedAsync` 的 while loop 模式
- `CLAUDE_CODY.md`：加入 Dev_plan 被打回時的回應格式（agree / disagree + 理由）
- `TaskGroup` 新增欄位：`DevPlanAppealRoundA`（int，計數器）、`DevPlanAppealLog`（string，對話紀錄）

---

### 24-3. 測試報告結構化（Quinn 產出 + DB 儲存）

**設計概念**

Quinn 測試完畢後產出結構化測試報告，存入 TaskGroup 供 Petra 判斷和 Sage 歸檔參考。

**測試報告格式**

```json
{
    "status": "passed | failed | no_applicable_tests",
    "passed_tests": ["測試名稱 1", "測試名稱 2"],
    "failed_tests": [
        {
            "name": "測試名稱",
            "error": "錯誤訊息",
            "file": "test/path.cs:行號"
        }
    ],
    "no_test_reason": "（僅 no_applicable_tests 時填寫）",
    "summary": "一段話總結"
}
```

**實作位置**

- `CLAUDE_QA.md`：加入結構化測試報告輸出格式
- `QaAgentService.cs`：解析 Quinn 的結構化輸出，存入 `AgentExecutionResult.OutputContent`
- `TaskGroup` 新增欄位：`TestReport`（string nullable）
- `TaskGroupService.cs`：QA 完成時儲存測試報告

---

### 24-4. 流程文件存 DB 基礎設施

**設計概念**

將各階段產出的流程文件統一存入 TaskGroup，下游 Agent 啟動時從 DB 取出相關文件傳入 prompt。

**TaskGroup 新增欄位**

| 欄位                  | 型別           | 來源                                      | 用途                       |
| --------------------- | -------------- | ----------------------------------------- | -------------------------- |
| `DevPlan`             | text nullable  | Cody（RunPetraDevPlanReviewAsync 時儲存） | Vera 審查參考、QA 測試參考 |
| `TestReport`          | text nullable  | Quinn（24-3）                             | Petra QA 判斷、Sage 歸檔   |
| `DevPlanAppealRoundA` | int, default 0 | 24-2                                      | Dev_plan appeal 計數       |
| `DevPlanAppealLog`    | text nullable  | 24-2                                      | Dev_plan appeal 對話紀錄   |

> `ImplementationNote`、`ReviewAppealLog`、`ReviewAppealRoundA` 已在 Stage 23 加入

**文件傳遞矩陣**

| 下游 Agent   | 收到的文件                                                                  |
| ------------ | --------------------------------------------------------------------------- |
| Cody（開發） | Issues 清單（現有）、UI 規格（現有）、Dev_plan（如有）                      |
| Vera（審查） | ImplementationNote（Stage 23 已傳）、Dev_plan（新增）                       |
| Quinn（QA）  | ImplementationNote（Stage 23 已傳）、Issues 清單（新增）                    |
| Sage（歸檔） | ImplementationNote（Stage 23 已傳）、TestReport（新增）、ReviewBody（現有） |

**實作位置**

- `TaskGroupService.cs`：`FireStepsAsync` 建構 task description 時，依上表附加對應文件
- `TaskGroupService.cs`：各 agent 完成時儲存對應文件到 TaskGroup

---

## 實作順序建議

```
1. 24-4（文件存 DB 基礎設施）    ← 先建 TaskGroup 欄位 + EF Migration
2. 24-3（測試報告結構化）        ← CLAUDE_QA.md + 解析 + 儲存
3. 24-1（QA 流程改造）           ← 依賴 24-3 的測試報告格式
4. 24-2（Dev_plan 審核強化）     ← 復用 Stage 23 appeal 模式
5. 版本號更新 → 3.8.0
6. EF Migration（一次建立所有新欄位）
```

---

## 不在 Stage 24 範圍（留待後續 Stage）

| 項目                       | 原因                                           |
| -------------------------- | ---------------------------------------------- |
| Kick-off 會議（第一階段）  | 需要 WorkflowEngine 支援多 Agent 會議機制      |
| 設計會議（第二階段）       | 同上                                           |
| Dashboard 輪次上限動態設定 | 依賴 Future Feature 十二（Dashboard 雙向操作） |
| Victoria 交付通知改造      | 依賴 Future Feature 九（Dashboard 雙向操作）   |
| 迴圈 B 上限機制            | 現有 FixIteration 已有上限，待觀察是否需要強化 |

---

## 驗收清單

- [ ] QA 流程：Quinn 測試失敗時，Petra 介入判斷而非直接交 Cody
- [ ] QA 流程：Petra 判斷「大幅改動」時退回 Vera 重新審查
- [ ] QA 流程：QA 修正超過 3 輪時上呈 Christ
- [ ] QA 流程：Quinn 輸出 `no_applicable_tests` 時，Petra 輕量判斷放行/要求補測試
- [ ] Dev_plan：Cody 被 Petra 打回時可 disagree（附理由）
- [ ] Dev_plan：appeal 迴圈上限 3 輪，超限上呈 Christ
- [ ] Dev_plan：appeal 對話完整記錄到 `DevPlanAppealLog`
- [ ] 測試報告：Quinn 產出結構化 JSON 測試報告
- [ ] 測試報告：報告存入 `TaskGroup.TestReport`
- [x] 文件傳遞：Vera 收到 Dev_plan、Quinn 收到 Issues 清單
- [x] 文件傳遞：Sage 收到 TestReport
- [x] `dotnet build` 零 error
- [x] `dotnet test` 通過
- [x] git commit + push
- [x] `.csproj` 版本更新為 `3.8.0`

---

## 注意事項

1. **QA 流程改造是最大的變動**：現有 QA 失敗路由需要從「直接 Dev_fix」改為「先過 Petra」，涉及 TaskGroupService 的 QA 完成後路由邏輯重構。

2. **Dev_plan appeal 復用 Stage 23 模式**：RunCodyDevPlanAppealAsync 的實作模式與 RunCodyAppealAsync 基本相同（LLM 直呼叫 + while loop + 完整記錄），可參考 Stage 23 的程式碼。

3. **MockMode 影響**：QA mock 會 early return success，Petra QA 判斷不會觸發。Dev_plan mock 同理。新增的 Petra LLM 方法在 MockMode 下由 MockLlmProvider 處理（已有延遲）。

4. **文件傳遞的實作方式**：與 Stage 23 的 ImplementationNote 傳遞方式一致 — 在 `FireStepsAsync` 的 task description 建構時附加 meta block。

5. **QA `no_applicable_tests` 偵測**：需要 Quinn 的 Claude Code session 輸出特定格式，`QaAgentService` 解析後回傳特殊結果。類似 Stage 23 的 blocker detection 模式。

---

## 實作紀錄

### 關鍵設計決策

1. **QA 四路由設計**：Petra `AssessQaFailureAsync` 回傳四種路由：`code_bug`（小修正，Dev_fix 後跳 Vera 直接重測）、`back_to_reviewer`（大幅改動，Dev_fix 後走完整 Reviewer → Petra → QA 路徑）、`env_or_test_issue`（環境或測試本身問題，視同通過）、`escalate_boss`（上呈）。

2. **QaFixRound 狀態管理**：`code_bug` 路由會遞增 `QaFixRound`（>0 表示 QA 修復模式），Dev_fix 完成時偵測 `QaFixRound > 0` 則跳過 Vera 直接重測。`back_to_reviewer` 路由必須**主動重置 `QaFixRound = 0`**，否則下一輪 Dev_fix 完成時仍會被 QA 快速路徑攔截。

3. **Dev_plan appeal 取代重交**：原 `revise` 分支會重新觸發 Dev_plan（Cody 從頭重寫）。Stage 24 改為觸發 `RunDevPlanAppealLoopAsync` 緊密 while loop（純 LLM，最多 3 輪）。Cody 接受（`accept`）或 Petra 改為 `approve` → 直接 fire Dev；耗盡輪次 → escalate 老闆。`DevPlanRevision` 欄位保留但不再遞增。

4. **DevPlanAppealLog 完整 JSON 記錄**：每輪同時序列化 `codyAppeal`（完整 JSON）和 `newReview`（完整 JSON）存入 log，而非摘要文字。`codyJson` 序列化必須在 `accept` 判斷**之前**，因為 accept 分支的 AppendLog 也需要 codyJson。

5. **MockMode 相容性**：四個新 Petra LLM 方法（AssessQaFailureAsync、AssessNoApplicableTestsAsync、RunCodyDevPlanAppealAsync、ReassessDevPlanAsync）全部設有 fallback（分別為 env_or_test_issue、approve、accept、approve）。MockLlmProvider 回傳的 mock JSON 解析失敗時自動走 fallback，不需改動 MockMode 相關程式碼。

6. **FireStepsAsync 僅三個參數**：`(group, steps, cancellationToken)`，無 projectId 版本。

### 踩坑記錄

| 問題                                    | 原因                                                              | 修正                                                                          |
| --------------------------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `back_to_reviewer` 後 Dev_fix 仍跳到 QA | `QaFixRound` 未重置為 0                                           | `back_to_reviewer` 分支明確 `group.QaFixRound = 0`                            |
| Dev_plan appeal `accept` 後上呈老闆     | `break` 跳出 while loop 後落入 `return false`                     | 改為 `return true`（共識達成，放行）                                          |
| `codyJson` 在 `accept` 分支內未宣告     | 序列化寫在 `accept` 判斷後面，但 `accept` 分支的 AppendLog 需要它 | 將 `var codyJson = JsonSerializer.Serialize(codyAppeal)` 移到 `accept` 判斷前 |
| DevPlanAppealLog 只記摘要               | 初版用 `codyAppeal.Reasoning` 和 `newReview.Summary` 文字段       | 改為 `JsonSerializer.Serialize()` 輸出完整 JSON                               |

---

## 變更紀錄

| 日期       | 版本 | 內容                                       |
| ---------- | ---- | ------------------------------------------ |
| 2026-04-12 | v1.0 | Aria 撰寫初版規劃書                        |
| 2026-04-13 | v1.1 | 實作完成；補充驗收清單、實作紀錄、踩坑記錄 |

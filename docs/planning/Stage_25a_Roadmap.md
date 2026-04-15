# Stage 25a — 開發流程重構 Phase 1c（Kick-off 會議機制）

> Stage：25a
> 對應版本：v3.9.0
> 建立日期：2026-04-13
> 狀態：✅ 已完成（2026-04-14）
> 文件版本：v2.0

---

## 目標

實作 **開發流程重構 Phase 1c**（Future Feature 八 第一階段）：

- 建立 Claude Code **持續對話 session** 基礎設施（會議 + 未來功能共用）
- 建立 **多 Agent 會議引擎**（Petra 主持，多位 Agent 同時參與）
- 實作 **Kick-off 會議**流程（需求對齊，開工前發現問題）
- 實作 **會議後 Christ 確認**機制（繼續 / 停止 / 修改）

> 對應 Future Feature：八（Phase 1c — 第一階段）
> Phase 1a（Stage 23）：Review Appeal、實作說明、阻礙報告、Sage 轉型、Git Tag
> Phase 1b（Stage 24）：QA Petra 介入、Dev_plan Appeal、TestReport 結構化、文件傳遞
> 第二階段（設計會議）留待 Stage 25b

---

## 背景說明

### Feature 八 完成狀況

| 階段             | 內容                                     | 狀態              |
| ---------------- | ---------------------------------------- | ----------------- |
| 第一（需求計劃） | Kick-off 會議 + 任務計劃書 + Christ 確認 | ❌ → **本次實作** |
| 第二（設計規劃） | Rosa Issues + Demi UI + 設計會議         | ❌ → Stage 25b    |
| 第三（開發）     | ImplementationNote、阻礙報告、Dev_plan   | ✅ Stage 23 + 24  |
| 第四（審查）     | Review Appeal + Petra 仲裁               | ✅ Stage 23       |
| 第五（QA）       | Petra 四路由判斷                         | ✅ Stage 24       |
| 第六（歸檔）     | Sage 轉型收尾歸檔                        | ✅ Stage 23       |
| 第七（上線）     | Git Tag 自動化                           | ✅ Stage 23       |

### 為什麼需要 Kick-off 會議

目前的流程是 Victoria 確認提案後直接開始開發（Dev_plan → Dev）。開發中才發現的問題（技術不可行、測試困難、需求模糊）需要回溯修正，代價高且浪費 token。

Kick-off 會議讓所有角色在**開工前**同時看到需求，提前暴露問題：

- **Cody**：技術可行性（「這個模組不支援 X，需要先重構」）
- **Quinn**：可測試性（「這種功能沒辦法自動化測試」）
- **Rosa**：需求完整性（「這裡有兩個解讀方式，需要釐清」）
- **Demi**：UI/UX 影響（「這會影響現有的 Layout 結構」）

目前流程缺少這個「開工前全員對齊」的機制，Agent 各自埋頭做事，問題在流程中逐一浮現。

---

## 核心設計決策

### 會議使用 Claude Code Session（非 LLM API）

| 考量            | LLM API                    | Claude Code Session（持續對話）          |
| --------------- | -------------------------- | ---------------------------------------- |
| 能否看 code     | ❌ 只能基於 prompt 想像    | ✅ 可以讀檔、搜索，意見有 code 佐證      |
| 多輪 context    | 手動傳入，每輪越來越大     | 自然累積，Agent 記得之前說過什麼         |
| 多輪 token 消耗 | 遞增（每輪要重傳全部歷史） | 可能遞減（Agent 已有 context，不需重讀） |
| 意見品質        | 可能脫離實際 code 現狀     | 有 code 佐證，更接地氣                   |
| 輸出格式控制    | 好（JSON）                 | 較自由（需解析文字）                     |

**決定：全部 5 位會議參與者（Petra/Rosa/Demi/Cody/Quinn）使用 Claude Code 持續對話 session。** 多輪討論時不重開 session，而是持續餵入新訊息，如同 Christ 與 Aria / 實作 Session 的工作方式。

**Petra 也使用 Claude Code Session**（而非 LLM API），理由：

1. **可以驗證其他人的說法** — Cody 說「這個模組不支援 X」，Petra 可以自己讀 code 確認
2. **統一架構** — 5 位參與者全部相同機制，不需要混用兩種系統
3. **Christ 修改流程自然銜接** — Christ 要求修改計劃書時，Petra 的 session 已有完整會議 context，直接餵入修改意見就能理解（如同 Christ 與 Aria 的互動模式），不需要重新傳入會議歷史

### 會議適用範圍

| WorkflowType    | 是否需要 Kick-off | 原因                                    |
| --------------- | ----------------- | --------------------------------------- |
| NewFeature      | ✅ 必要           | 新功能最容易出現認知落差                |
| TechImprovement | ❌ 跳過           | 技術改善通常範圍明確，直接進入 Dev_plan |
| BugFix          | ❌ 跳過           | Bug 修正範圍最小，直接開發              |

> Stage 25a 先以 WorkflowType 硬判斷。未來如需動態判斷（由 Petra 評估是否需要 Kick-off），可在後續 Stage 擴展。

---

## 實作項目

### 25a-1. Claude Code 持續對話 Session 基礎設施

**現狀**

目前 `IClaudeCodeService` 的所有方法（RunAsync / RunReadOnlyAsync / RunVictoriaAsync 等）都帶 `--no-session-persistence` 旗標，每次呼叫都是全新獨立的 subprocess。沒有 sessionId 參數，不支援持續對話。

**目標**

新增支援「持續對話」的方法，讓同一個 session 可以跨多次呼叫累積 context。現有方法維持不變（向後相容）。

**設計**

Claude Code CLI 本身支援 session persistence：

- `-p "prompt"` 搭配 `--session-id <id>` 可指定 session
- `-p "prompt" --resume` 可繼續既有 session
- 不帶 `--no-session-persistence` 時，session 資料會保留在本地

新增方法：

```
IClaudeCodeService.RunMeetingSessionAsync(
    workingDir,
    sessionId,       ← 會議引擎產生的唯一 ID
    prompt,          ← 本輪要傳給 Agent 的訊息
    model,
    anthropicApiKey,
    isFirstMessage,  ← true: 新建 session / false: resume 既有 session
    maxTurns,        ← 會議每輪 turn 上限（建議 10~15）
    ct
) → ClaudeCodeResult
```

實作重點：

- `isFirstMessage = true`：不帶 `--resume`，帶 `--session-id <id>`
- `isFirstMessage = false`：帶 `--resume --session-id <id>`（繼續對話）
- 兩者都**不帶** `--no-session-persistence`
- 會議結束後由 MeetingService 負責清理 session 資料

MockClaudeCodeService 對應新增 mock 實作（30~60 秒延遲 + mock 回應）。

**⚠️ 需要驗證**

Claude Code CLI 的 `-p`（piped mode）搭配 `--resume --session-id` 的行為是否符合預期。實作 Session 應先寫一個小測試驗證：

```bash
# 第一輪
claude -p "你好，記住我的名字是 Test" --session-id meeting-test-001 --output-format json
# 第二輪（resume）
claude -p "我剛才告訴你我的名字是什麼？" --resume --session-id meeting-test-001 --output-format json
```

若 piped mode 不支援 resume，備案是使用 JSONL streaming mode 或 process stdin 持續輸入。

---

### 25a-2. 多 Agent 會議引擎

**新增 MeetingService**

會議引擎負責協調多個 Agent 的 Claude Code session，實現「多人會議」流程。

**會議流程（核心迴圈）**

```
開場：
  MeetingService 為所有參與者建立 Claude Code session
  （sessionId 格式：meeting-{groupId}-{agentName}）
  參與者：Petra + Rosa + Demi + Cody + Quinn（共 5 個 session）
    ↓
第一輪：
  系統 → Rosa/Demi/Cody/Quinn 各 session：需求說明 + 該角色的會議指引
  各 session → 系統：收集 4 位 Agent 的意見（文字輸出）
  系統 → Petra session：「以下是大家的意見，請整理並判斷是否有需要討論的分歧」
  Petra session → 系統：整理摘要 + 判斷（consensus / needs_discussion / escalate）
    ↓
若 needs_discussion → 第二輪：
  系統 → Rosa/Demi/Cody/Quinn 各 session：「以下是 Petra 整理的討論，有沒有補充或不同意見？」
  各 session → 系統：收集各 Agent 的回應
  系統 → Petra session：重新判斷
    ↓
最多 3 輪 → 共識達成或上呈 Christ
    ↓
收尾：
  系統 → Petra session：「會議結束，請基於完整討論產出任務計劃書」
  Petra session → 系統：任務計劃書
  MeetingService：關閉 Rosa/Demi/Cody/Quinn 的 session（Petra session 保留，供 Christ 修改流程使用）
```

**會議角色指引（傳入各 session 的第一輪 prompt）**

| 角色  | 指引重點                                                 |
| ----- | -------------------------------------------------------- |
| Petra | PM 視角，主持會議，整理意見，判斷共識，產出計劃書        |
| Rosa  | 從需求分析師角度，評估需求完整性，指出模糊或矛盾之處     |
| Demi  | 從 UI/UX 角度，評估對現有 Dashboard 的影響，提出設計疑慮 |
| Cody  | 從開發角度，評估技術可行性，可**讀取 codebase** 驗證假設 |
| Quinn | 從 QA 角度，評估可測試性，指出測試困難點                 |

每位 Agent 的 prompt 包含：

1. 角色說明（你在 Kick-off 會議中的職責）
2. 需求說明（Victoria 提案內容）
3. 任務上下文（TaskGroup 基本資訊、WorkflowType）
4. 輸出要求（簡潔列出意見，不需要執行實際工作）

**Petra 整理與判斷**

Petra 的 session 收到所有 Agent 的意見後，系統要求 Petra 在回應末尾輸出判斷 JSON：

```json
{
    "decision": "consensus | needs_discussion | escalate",
    "summary": "整理摘要",
    "discussion_points": ["需要進一步討論的點"]
}
```

- `consensus`：大家沒有重大分歧 → 會議結束
- `needs_discussion`：有需要討論的分歧 → 下一輪
- `escalate`：有無法在團隊內解決的問題 → 上呈 Christ

Petra 在 Claude Code session 中可以自行讀取 codebase 來驗證 Agent 的說法，做出更準確的判斷。

**會議紀錄格式（存入 DB）**

```markdown
# Kick-off 會議紀錄

## 需求說明

{Victoria 提案內容}

## Round 1

### Rosa（需求分析）

{Rosa 的完整回應}

### Demi（UI/UX 設計）

{Demi 的完整回應}

### Cody（技術可行性）

{Cody 的完整回應}

### Quinn（測試規劃）

{Quinn 的完整回應}

### Petra（綜合整理）

{Petra 的完整判斷 JSON}

## Round 2（如有）

...
```

全部記錄完整原文（Agent 的 Claude Code 輸出 + Petra 的完整 JSON），不做摘要。遵循 Stage 23/24 建立的 **完整 JSON 記錄原則**。

---

### 25a-3. Kick-off 會議流程整合

**WorkflowEngine 修改**

NewFeature 流程在「proposal_approved」後插入 `Kickoff` 步驟：

```
現有：proposal_approved → Dev_plan → Dev → Reviewer → QA → Doc → Merge
新增：proposal_approved → Kickoff → [Christ 確認] → Dev_plan → Dev → ...
```

BugFix / TechImprovement 流程不變（跳過 Kickoff）。

**TaskGroupService 新增 HandleKickoffAsync**

`HandleAgentCompletedAsync` 中，當完成的步驟是 `Kickoff` 時：

1. 已完成 Kick-off 會議（MeetingService 已執行）
2. 取得 Petra 產出的任務計劃書
3. 進入 Christ 確認流程（25a-4）

**Kickoff 步驟的執行方式**

`Kickoff` 不同於一般的 Agent 步驟（單一 Agent 執行），而是由 MeetingService 協調多 Agent：

- `FireStepsAsync` 偵測到 `Kickoff` 步驟時，呼叫 `MeetingService.RunKickoffMeetingAsync(group)`
- MeetingService 內部管理所有 session 的建立、訊息傳遞、收回
- 會議完成後，回傳會議紀錄 + 任務計劃書
- TaskGroupService 儲存到 DB，進入確認流程

---

### 25a-4. 會議後 Christ 確認機制

**流程**

```
Kick-off 會議完成（Petra session 保持開啟）
    ↓
Petra 在 session 內產出任務計劃書
    ↓
Victoria 上呈 Christ：
    「Kick-off 完成，任務計劃書如下：{摘要}
     完整內容請查看 Dashboard。
     請選擇：繼續 / 停止 / 修改」
    ↓
Christ 回覆：
    ├── 繼續 → 關閉 Petra session → 觸發 Dev_plan
    ├── 停止 → 關閉 Petra session → 標記任務取消
    └── 修改（附帶修改意見）
            ↓
        系統 → Petra session：「老闆要求修改：{Christ 的意見}，請評估影響並調整計劃書」
        （Petra session 已有完整會議 context，直接理解修改意圖）
            ↓
        Petra session → 系統：
            ├── 小修改 → 輸出調整後的計劃書 → Victoria 再次上呈 Christ
            └── 大修改 → 建議重新召開 Kick-off → Victoria 告知 Christ
                    ↓
                Christ 確認重開 → 關閉 Petra session → 重新觸發 Kickoff 步驟
```

**核心設計：Petra session 在確認完成前不關閉**

如同 Christ 與 Aria 的互動模式 — Petra 記得整場會議的所有討論，Christ 的修改意見直接餵入既有 session，Petra 能在完整 context 下理解並回應。不需要重新傳入會議歷史。

修改可以多輪進行（Christ 來回調整），直到 Christ 滿意為止。Petra session 在 Christ 最終確認（繼續/停止）後才關閉。

**Petra 判斷輸出格式**

系統要求 Petra 在修改回應末尾輸出：

```json
{
    "impact": "small | large",
    "revised_plan": "（小修改時包含調整後的計劃書）"
}
```

- `small`：Petra 直接調整計劃書，Victoria 再次上呈
- `large`：建議重新召開 Kick-off（需要團隊重新討論）

**實作方式**

復用 Victoria 現有的「等待 Christ 回覆」機制：

- `TaskGroup` 設定等待狀態（類似提案確認的等待模式）
- Christ 在 Discord 回覆後，系統解析回覆內容
- 依回覆類型路由到對應處理（繼續/停止/餵入 Petra session）

---

### 25a-5. DB 基礎設施

**TaskGroup 新增欄位**

| 欄位                | 型別           | 說明                                          |
| ------------------- | -------------- | --------------------------------------------- |
| `KickoffMeetingLog` | text nullable  | 完整會議紀錄（Markdown，含各 Agent 完整回應） |
| `TaskPlan`          | text nullable  | Petra 產出的任務計劃書                        |
| `KickoffRound`      | int, default 0 | 會議輪次計數                                  |

> Stage 25b 將新增 DesignMeetingLog、DesignPlan 等欄位

**WorkflowSettings 新增**

```json
"KickoffMaxRounds": 3
```

**EF Migration**

```bash
dotnet ef migrations add Stage25aKickoffFields \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard
```

---

## 任務計劃書格式

Petra 在 Kick-off 會議結束後產出，作為後續所有 Agent 的參考依據：

```markdown
# 任務計劃書

## 任務摘要

{一段話描述要做什麼}

## 關鍵決策

- {Kick-off 中達成的共識}
- {解決的疑慮}

## 各角色意見摘要

| 角色  | 主要意見 | 結論                    |
| ----- | -------- | ----------------------- |
| Rosa  | ...      | 已確認 / 待 Christ 決定 |
| Demi  | ...      | ...                     |
| Cody  | ...      | ...                     |
| Quinn | ...      | ...                     |

## 風險與注意事項

- {Kick-off 中提出但未完全解決的項目}

## 建議實作方向

{基於討論結果的技術方向建議}
```

---

## 與現有流程的關係

**Victoria 提案流程不變**：Stage 25a 不改動 Victoria 的提案創建流程（Rosa/Demi 仍在提案階段參與）。Kick-off 是提案確認**之後**、開發**之前**的新增步驟。

**文件傳遞**：任務計劃書存入 `TaskGroup.TaskPlan`，後續可在 BuildTaskDescription 中傳給 Dev_plan / Dev / Reviewer 參考。

**Stage 25b 銜接**：25b 在 Kick-off 之後、Dev_plan 之前插入設計會議（第二階段），復用 25a 建立的會議引擎和 session 基礎設施。

---

## 實作順序建議

```
1. 25a-1（Claude Code 持續對話 Session）  ← 先建基礎設施 + 驗證 CLI 行為
2. 25a-5（DB 欄位 + WorkflowSettings）    ← EF Migration
3. 25a-2（多 Agent 會議引擎）             ← 依賴 25a-1 的 session 能力
4. 25a-3（Kick-off 流程 + WorkflowEngine） ← 依賴 25a-2 的會議引擎
5. 25a-4（Christ 確認機制）               ← 依賴 25a-3 的流程整合
6. 版本號更新 → 3.9.0
```

---

## 不在 Stage 25a 範圍

| 項目                     | 原因                                             |
| ------------------------ | ------------------------------------------------ |
| 設計會議（第二階段）     | Stage 25b，復用 25a 的會議引擎                   |
| Rosa Issues 流程調整     | Stage 25b 處理（Kick-off 後 Rosa 才執行）        |
| TechImprovement Kick-off | 先觀察 NewFeature Kick-off 效果，後續可擴展      |
| Dashboard 會議紀錄頁面   | 會議紀錄已存 DB，Dashboard 顯示可在後續 Stage 做 |

---

## 驗收清單

- [ ] Claude Code CLI `-p` + `--resume` + `--session-id` 行為驗證通過
- [ ] 持續對話 session：第二輪 Agent 能記住第一輪說過的話
- [ ] Kick-off 會議：NewFeature 流程觸發 Kick-off，BugFix/TechImprovement 跳過
- [ ] Kick-off 會議：4 位 Agent（Rosa/Demi/Cody/Quinn）各自開啟 Claude Code session
- [ ] Kick-off 會議：Petra 也使用 Claude Code session，可讀取 codebase 驗證意見
- [ ] Kick-off 會議：Petra 整理意見，最多 3 輪
- [ ] Kick-off 會議：完整會議紀錄存入 `KickoffMeetingLog`（含各 Agent 完整回應）
- [ ] 任務計劃書：Petra 產出存入 `TaskPlan`
- [ ] Christ 確認：Victoria 上呈計劃書，等待 Christ 回覆
- [ ] Christ 確認：繼續 → 觸發 Dev_plan
- [ ] Christ 確認：停止 → 任務取消
- [ ] Christ 確認：修改 → 餵入 Petra session（保持開啟，有完整 context）→ 調整計劃書
- [ ] Christ 確認：修改多輪 → Petra session 持續累積 context，直到 Christ 滿意
- [ ] MockMode：會議 session 使用 MockClaudeCodeService（延遲 + mock 回應）
- [ ] MockMode：Petra 判斷走 fallback（consensus → 直接繼續）
- [x] `dotnet build` 零 error
- [x] `dotnet test` 通過
- [x] `.csproj` 版本更新為 `3.9.0`

---

## 注意事項

1. **Session 基礎設施是最大風險**：Claude Code CLI 的 `-p` + `--resume` 行為需要先驗證。如果 piped mode 不支援 resume，備案方案包括：(a) 使用 JSONL streaming mode；(b) 用 process stdin 持續輸入；(c) 退回到每輪新開 session + 手動傳入歷史。實作 Session 應在 25a-1 完成後先做小規模驗證。

2. **會議 session 的 maxTurns 設定**：每輪 Agent 發言不需要太多 tool calls（讀幾個檔案、搜索一下），建議 maxTurns 設 10~15，既足夠探索 code 又不會失控消耗。

3. **會議串行 vs 並行**：同一輪中 Rosa/Demi/Cody/Quinn 的 session 理論上可以並行執行（各自獨立探索），但需注意系統資源（同時 4 個 Claude Code subprocess）。如果資源緊張，可改為串行。建議先嘗試並行，觀察效果。Petra 的整理步驟必須等 4 位 Agent 都完成後才執行（串行）。

4. **Petra session 生命週期**：Petra session 從會議開始一直存活到 Christ 最終確認（繼續/停止）。期間可能經歷：會議 N 輪 → 產出計劃書 → Christ 修改 M 輪 → 最終確認。Session 存活時間可能較長，需注意 session 資料的磁碟空間。確認完成後務必清理。

5. **與 Victoria 提案流程的關係**：Stage 25a 不改動 Victoria 的提案流程。Rosa/Demi 仍在提案階段做初步工作（Issues / UI 規格），Kick-off 會議中他們基於已有成果提出補充意見。Stage 25b 才會調整 Rosa/Demi 的工作時機。

---

## 實作完成紀錄

### 架構決策（實作階段確認）

**Session ID 設計（最終實作）**

| Session 對象         | ID 來源               | 原因                                  |
| -------------------- | --------------------- | ------------------------------------- |
| Petra                | `group.Id.ToString()` | 固定可推導，Christ 修改輪不需額外儲存 |
| Rosa/Demi/Cody/Quinn | `Guid.NewGuid()`      | 臨時 session，會議後廢棄              |

Session ID 必須是**合法 UUID 格式**（`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`），CLI 會拒絕非 UUID 格式（例如 `meeting-test-001` 會報錯 `Invalid session ID. Must be a valid UUID`）。

**CLI 用法（已驗證）**

```bash
# 第一輪（建立新 session）
claude -p "prompt" --session-id <uuid> --output-format json --dangerously-skip-permissions

# 後續輪（resume 既有 session）
# 注意：--resume 直接帶 UUID，不可同時帶 --session-id
claude -p "prompt" --resume <uuid> --output-format json --dangerously-skip-permissions
```

❌ 錯誤用法：`--resume --session-id <uuid>`（會報 "can only be used with --continue or --resume if --fork-session is also specified"）

**AllowedTools 策略（最終實作）**

| Agent | allowedTools             | 說明                 |
| ----- | ------------------------ | -------------------- |
| Petra | `["Glob","Grep","Read"]` | 讀取驗證，不需寫入   |
| Rosa  | `["Glob","Grep","Read"]` | 需求分析只需讀       |
| Demi  | `["Glob","Grep","Read"]` | UI/UX 評估只需讀     |
| Cody  | `null`（全工具）         | 技術可行性需深入探索 |
| Quinn | `["Glob","Grep","Read"]` | 測試規劃只需讀       |

**Kickoff 分支路由（TaskGroupService）**

`FireOneStepAsync` 開頭偵測 `step.AgentName == "Kickoff"` → 直接 `Task.Run` 非同步執行 `RunKickoffMeetingAndWaitAsync`，不走一般 IAgentExecutor 路徑。

**Christ 確認機制（Discord 按鈕）**

Discord 訊息帶三個按鈕，CustomId 格式：

```
kickoff_continue_{groupId}
kickoff_stop_{groupId}
kickoff_modify_{groupId}
```

`CommandHandler.OnButtonExecutedAsync` 前端攔截 `kickoff_` 前綴，直接路由，不透過 `_pendingConfirmations`（傳統 message-ID 鍵值查找）。

**修改記錄格式**（追加至 `KickoffMeetingLog`）

```markdown
## Christ 修改 Round {N} — {yyyy-MM-dd HH:mm} UTC

### Christ 修改意見

{Christ 的原始回覆}

### Petra 回應（完整）

{Petra 的完整輸出，含 impact JSON}
```

### 踩坑三件組

**1. Session ID 格式錯誤**

CLI 要求 `--session-id` 的值必須是合法 UUID（如 `3f4d2b1a-...`），任何非 UUID 格式（如 `meeting-abc-petra`）都會報錯。解決：Petra 用 `group.Id.ToString()`，其他 Agent 用 `Guid.NewGuid().ToString()`。

**2. `--resume` 用法錯誤**

規劃書寫 `--resume --session-id <uuid>`，但實際 CLI 的正確用法是 `--resume <uuid>`（UUID 直接作為 `--resume` 的值）。同時帶 `--session-id` 會報錯。`BuildMeetingArgs` 中用 `isFirstMessage` 切換：

- `isFirstMessage = true` → `--session-id <uuid>`
- `isFirstMessage = false` → `--resume <uuid>`

**3. C# `file` record 不可出現在非 file-local 型別的方法簽名**

`MeetingService.cs` 底部的 `PetraDecision` / `ModifyDecision` 用 `file record` 宣告，但這兩個型別被 `MeetingService`（非 file-local 類別）的私有方法用作回傳型別，導致 CS9051 編譯錯誤。解決：改成 `internal record`（頂層型別）。

**4. TaskRepository 方法名稱**

新增的 `HandleKickoffConfirmedAsync` 呼叫了不存在的 `GetGroupAsync()`，正確方法名是 `GetGroupByIdAsync()`。

### 關鍵檔案清單

| 角色           | 路徑                                                                 |
| -------------- | -------------------------------------------------------------------- |
| 會議引擎       | `src/AiTeam.Bot/Orchestration/MeetingService.cs`（新增）             |
| Session 介面   | `src/AiTeam.Bot/Agents/IClaudeCodeService.cs`                        |
| Session 實作   | `src/AiTeam.Bot/Agents/ClaudeCodeService.cs`                         |
| Mock 實作      | `src/AiTeam.Bot/Agents/MockClaudeCodeService.cs`                     |
| Proxy          | `src/AiTeam.Bot/Agents/ClaudeCodeProxy.cs`                           |
| 流程路由       | `src/AiTeam.Bot/Orchestration/TaskGroupService.cs`                   |
| 流程表         | `src/AiTeam.Bot/Orchestration/WorkflowEngine.cs`                     |
| Discord 按鈕   | `src/AiTeam.Bot/Discord/CommandHandler.cs`                           |
| DB 欄位        | `src/AiTeam.Data/Entities.cs`                                        |
| Migration      | `src/AiTeam.Data/Migrations/20260413160554_Stage25aKickoffFields.cs` |
| Agent 名稱常數 | `src/AiTeam.Shared/Constants/AgentNames.cs`                          |

---

## 驗收清單

- [x] Claude Code CLI `-p` + `--resume <uuid>` 行為驗證通過（Mock 流程觀察 session 正常銜接，2026-04-15）
- [x] Kick-off 會議：NewFeature 流程觸發 Kick-off，BugFix/TechImprovement 跳過（`/mock bugfix` 確認跳過 Kickoff + Design）
- [x] Kick-off 會議：4 位 Agent（Rosa/Demi/Cody/Quinn）各自開啟 Claude Code session（MockMode 驗收確認）
- [x] Kick-off 會議：Petra 也使用 Claude Code session，可讀取 codebase 驗證意見（實作確認）
- [x] Kick-off 會議：Petra 整理意見，最多 3 輪（MockMode consensus 1 輪完成）
- [x] 完整會議紀錄存入 `KickoffMeetingLog`（Dashboard PipelineView Kick-off 會議紀錄折疊面板可展開確認）
- [x] 任務計劃書：Petra 產出存入 `TaskPlan`（Dashboard 實作計劃書折疊面板可展開確認）
- [x] Christ 確認：Victoria 上呈計劃書，Discord 按鈕出現（▶️ 繼續 / ⏹️ 停止 / ✏️ 修改 三鍵確認）
- [x] Christ 確認：繼續 → 觸發 Dev_plan（Christ 點繼續後流程正常推進至 Dev_plan）
- [ ] Christ 確認：停止 → 任務取消（未驗收）
- [ ] Christ 確認：修改 → 餵入 Petra session → 調整計劃書（未驗收）
- [x] MockMode：30~60 秒內完成 + 自動 consensus → 進入 Dev_plan（全流程驗收確認）

> **驗收紀錄（2026-04-15）**：`/mock proposal` 全流程（Kickoff → Christ 確認 → Design → Dev_plan → Dev → Reviewer → QA → Doc）驗收通過。停止/修改按鈕未驗收。

---

## 變更紀錄

| 日期       | 版本 | 內容                                                                                                                                  |
| ---------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-04-13 | v1.0 | Aria 撰寫初版規劃書                                                                                                                   |
| 2026-04-13 | v1.1 | Christ 回饋：Petra 改用 Claude Code Session；Christ 修改計劃書時餵入 Petra 既有 session；移除 AssessKickoffModificationAsync 獨立方法 |
| 2026-04-14 | v2.0 | 實作完成：補充實作架構決策、踩坑三件組（UUID 格式、--resume 用法、file record 限制）、關鍵檔案清單、驗收清單；狀態更新為已完成        |

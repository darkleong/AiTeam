# Stage 30：申訴迴圈 LLM API → Claude Code CLI 全面升級

> 對應 Future Feature：八 Phase 2（第三項）
> 對應版本：v3.17.0
> 建立日期：2026-04-19
> 狀態:✅ 已完成（2026-04-20）
> 文件版本：v1.0

---

## 概述

Stage 23（Review Appeal）與 Stage 24（Dev_plan Appeal）建立了申訴迴圈機制，讓 Cody 能反駁 Vera 的 Review、能申訴 Petra 對 Dev_plan 的審核意見。這個機制邏輯上成功——Petra 仲裁後的路由清楚、防止單向權力結構。

但這些迴圈的**執行層**目前走 **LLM API（純文字問答）**，Agent 在「修正 / 反駁 / 再評估」時**失去 codebase 存取能力**。相比已驗證的 Kickoff / Design 會議（走 Claude Code CLI + `--session-id` / `--resume`），品質落差明顯：

- Cody 反駁 Vera 的 Critical，不能實際查看程式碼，只能靠「文字回推」
- Vera 再評估 Cody 的反駁，不能實際再檢視 PR diff
- Petra 仲裁，不能自己看程式碼當裁判

本階段目標：**5 個申訴相關環節全面升級到 Claude Code CLI，讓 Agent 在迴圈中保留 codebase 存取能力**。

---


## 設計決策（已定案 2026-04-19）

規劃階段與 Christ 討論過四個關鍵決策，以下是定案內容與理由。

### 決策 1：5 個 appeal 環節採用「新開 CLI session + 強化 Prompt」

**不 Resume 原 Dev / Dev_plan / Review session**，而是每個 appeal 都新開一個 CLI session（`isFirstMessage: true`），把需要的脈絡從 DB 塞進 Prompt：

- `TaskPlan`（Kickoff 產出）
- `DesignPlan`（設計規劃書）
- `DevPlan`（Cody 的實作計畫書）
- `ImplementationNote`（Stage 23 專門設計的 Cody 自述）
- PR diff（透過 GitHub API 取得）
- 本次 appeal 的特定輸入（Review body / Critical list / Petra 審核意見等）

**新開 session 的 Cody / Vera / Petra 仍有 Claude Code CLI 的 Read / Glob / Grep 工具，能自己讀取 codebase 現況驗證推論。**

**為何不走 Resume**：

1. **工程量大 40%**：Resume 要改上游 3 個 Agent 執行邏輯先儲存 sessionId + 3 個 Entity 欄位 + Migration
2. **Token 成本意外地更高**：Anthropic API 是 stateless，Resume 時每次呼叫都要把完整對話歷史 replay。Dev session 可能累積 50-200K tokens，每次 appeal 都要重送。Prompt Caching 在 appeal 跨天時命中率低
3. **Session 存活性不可靠**：Claude Code session 檔存在 Bot 容器的 `~/.claude/sessions/`，容器重啟會清除
4. **Stage 23 的 `ImplementationNote` 已涵蓋 ~80% 價值**：Cody「我做了什麼、為什麼這樣做」已經落地到 DB
5. **Appeal 場景聚焦特定 Critical Issue**：1-3 個問題點，不需要「當初寫程式的完整思考歷程」

**放棄的部分**：Cody 當初寫程式時的「思考過程 / 試錯過程」會消失。估計品質覆蓋 85-90%，剩下 10-15% 用後續觀察決定是否補強（見下方觀察計畫）。

### 決策 2：Session 存活性 — 不處理

決策 1 選「新開」後無 resume 需求，此決策自動作廢。

### 決策 3：Petra 每個 appeal 新開 session

本 Stage 涉及 Petra 兩個審核點（再評估 Dev_plan、仲裁 Review Appeal），每次新開。Petra 的「專案累積記憶」是好題目但獨立 FF 討論，本 Stage 不擴大 scope。

### 決策 4：5 個環節一次做完

不分階段。改寫模式一致（LLM API → `RunMeetingSessionAsync`），規模 M。

### Token 成本估算

基於決策 1（新開 + 強化 Prompt），單次 appeal：

| 內容 | Token 數（估）|
|---|---|
| System prompt / 格式要求 | ~500 |
| TaskPlan + DesignPlan + DevPlan + ImplementationNote | ~6,500 |
| PR diff（視 PR 大小 500-10,000）| ~3,000 平均 |
| 本次 appeal 特定輸入 | ~2,000 |
| **初始 Prompt 小計** | **~12,000** |
| CLI 工具調用（Glob/Grep/Read）| ~5,000 - 20,000 |
| **單次 appeal 總 tokens** | **~17,000 - 32,000** |

一場完整 3 輪 Review Appeal ≈ ~$1-3（Opus 4 價格）。Prompt Caching 可降低同一場 appeal 多輪中重複 prefix 的費用。

### 品質觀察計畫（後續 Stage 依據）

本 Stage 實作完成後，建議持續觀察以下指標 2~4 週，累積資料再決定是否補「session 摘要」或升級到 Resume：

- Cody appeal 中「漏看上游決策」的比率
- Vera 再評估中「立場翻轉不合理」的比率
- Petra 仲裁中「缺乏 context 的判斷」發生次數

若品質確實不足，才投資後續升級——現階段不預支工程。

---

## 影響範圍總覽

| 修改類型 | 數量 |
|----------|------|
| 改寫的 PmAgentService 方法 | 5（見下方清單）|
| 新增的 Prompt 組裝邏輯 | 5 個對應方法 |
| 新增 Entity 欄位 | 0（推薦方案下不需要）|
| EF Migration | 0 |
| MockClaudeCodeService 新增分支 | 5 種（用 prompt 內容關鍵字判斷）|
| 版本號 | v3.16.1 → v3.17.0 |

> 若 Christ 改選決策 1 的 A 或 C，會新增 EF Migration + 欄位，工程量 +30%。

---

## 30-1. 5 個環節改寫清單

### 環節 1：Cody 修正 Dev_plan（`RunCodyDevPlanAppealAsync`）

- **位置**：`PmAgentService.cs:960`
- **現況**：`llmProvider.CompleteAsync` 純文字，產出 `CodyDevPlanAppeal { Decision, Reasoning }`
- **改為**：`RunMeetingSessionAsync`（新 session），Prompt 帶入 `Dev_plan 內容` + `Petra 審核意見` + `priorContext`，讓 Cody 能讀 codebase 驗證 Petra 的質疑
- **解析**：複用既有 `TryParseCodyDevPlanAppeal`，CLI 輸出的 `result.Output` 可直接丟給它

### 環節 2：Petra 再評估 Dev_plan（`ReassessDevPlanAsync`）

- **位置**：`PmAgentService.cs`（搜 `BuildReassessDevPlanPrompt` 附近）
- **現況**：LLM API，依據 Cody 的 appeal + 原 Dev_plan + 原 Petra review 回 `PetraReview`
- **改為**:`RunMeetingSessionAsync`（新 session），Prompt 帶入所有 prior context，讓 Petra 能讀 codebase 驗證 Cody 反駁的合理性
- **解析**：複用既有 PetraReview 解析邏輯

### 環節 3：Cody 反駁 Review（`RunCodyAppealAsync`）

- **位置**：`PmAgentService.cs:477`
- **現況**：LLM API，針對每個 Critical Issue 回 `{agree / disagree, reasoning}`
- **改為**：`RunMeetingSessionAsync`（新 session），Prompt 帶入 Vera 的 ReviewBody + 需要回應的 Critical Issue 清單，Cody 能讀自己寫的程式碼驗證 Vera 是否誤判
- **解析**：複用既有 `TryParseCodyAppeal`

### 環節 4：Vera 再評估 Review（`RunVeraAppealAsync`）

- **位置**：`PmAgentService.cs:528`
- **現況**：LLM API，依據 Cody 的 appeal 回 `VeraAppealResponse { AcceptedIds, MaintainedIds, Summary }`
- **改為**：`RunMeetingSessionAsync`（新 session），Prompt 帶入原 Review + Cody 反駁 + PR diff 位置，Vera 能實際再看 codebase 決定是否接受反駁
- **解析**：複用既有 `TryParseVeraAppealResponse`

### 環節 5：Petra 仲裁 Review（`ArbitrateReviewAppealAsync`）

- **位置**：`PmAgentService.cs:573`
- **現況**：LLM API，依據 Cody / Vera 的爭論回 `AppealArbitration { Decision, Reasoning }`
- **改為**：`RunMeetingSessionAsync`（新 session），Prompt 帶入完整爭論脈絡，Petra 能讀 codebase 做獨立判斷
- **解析**：複用既有解析邏輯

### 共用改寫模式

所有 5 個環節的改寫結構類似：

```csharp
public async Task<XxxResult> RunXxxAsync(..., CancellationToken ct = default)
{
    var (workingDir, model, apiKey) = await PrepareClaudeCodeEnvAsync(group, ct);
    var systemPrompt = BuildXxxSystemPrompt();   // 保留既有
    var userPrompt   = BuildXxxPrompt(...);      // 保留既有
    var combinedPrompt = $"{systemPrompt}\n\n---\n\n{userPrompt}";

    var sessionId = Guid.NewGuid().ToString();   // 每次新 session

    for (int attempt = 1; attempt <= MaxRetries; attempt++)
    {
        try
        {
            var result = await claudeCodeService.RunMeetingSessionAsync(
                workingDir, sessionId, combinedPrompt, model, apiKey,
                isFirstMessage: true,
                maxTurns: 10,
                allowedTools: ["Glob", "Grep", "Read"],   // 唯讀
                ct);

            var parsed = TryParseXxx(result.Output);
            if (parsed is not null) return parsed;
        }
        catch (Exception ex) { logger.LogWarning(ex, "..."); }
    }
    return FallbackXxx();
}
```

### 共用輔助方法

新增 `PrepareClaudeCodeEnvAsync(TaskGroup group, CancellationToken ct)`：
- Clone/Pull repo（取得 workingDir，與其他 Agent 一致）
- 讀取 model（依 Petra / Dev Agent 設定）
- 讀取 API key
- 回傳 `(workingDir, model, apiKey)`

這個 helper 讓 5 個方法的環境準備不重複。

---

## 30-2. MockClaudeCodeService 擴充

5 個新 call site 在 MockMode 下需要適當的模擬回應，避免阻塞流程測試。

`MockClaudeCodeService.RunMeetingSessionAsync` 目前已有會議類型判斷邏輯（Kickoff / Design）。新增 5 種判斷：

| Prompt 關鍵字 | Mock 回應 |
|---|---|
| 「Dev_plan Appeal」+「Cody」 | `{"decision":"accept", "reasoning":"[MOCK] 接受 Petra 意見"}` |
| 「Dev_plan Appeal」+「Petra」 | `{"decision":"approve", "summary":"[MOCK] 接受 Cody 反駁，核准 Dev_plan"}` |
| 「Review Appeal」+「Cody」 | 對所有 critical id 回 `agree` |
| 「Review Appeal」+「Vera」 | `{"acceptedIds":[...], "maintainedIds":[], "summary":"[MOCK] 全數接受"}` |
| 「Review Appeal」+「Petra 仲裁」 | `{"decision":"support_cody", "reasoning":"[MOCK] 支持 Cody 反駁"}` |

MockMode 失敗情境（`FailScenario`）保留既有邏輯，只是改為透過 CLI 模擬路徑輸出。

---

## 30-3. 其他調整

### Session 清理策略（附帶議題）

Claude Code session 檔累積問題——本 Stage 若選擇「每 appeal 新開 session」，會產生大量一次性 session 檔。建議：
- **現階段不處理**：Docker 容器 restart 會自動清；短期不構成磁碟壓力
- **記入 FF 觀察**：若未來遇到磁碟容量問題，補上「定期 cleanup 腳本」

### 流程文件更新

`docs/agents/software team/Petra.md` 等文件若有寫到「LLM API 審核」語句，需改為「Claude Code CLI 審核」。

### 版本號

`src/Directory.Build.props` v3.16.1 → v3.17.0（minor 版 bump，符合「每個 Stage 完成時遞增 minor」的 SemVer 規則）。

---

## 需要修改的檔案清單（推薦方案下）

| 檔案 | 變更 |
|------|------|
| `src/AiTeam.Bot/Agents/PmAgentService.cs` | 5 個方法改寫 + 新增 `PrepareClaudeCodeEnvAsync` helper |
| `src/AiTeam.Bot/Agents/MockClaudeCodeService.cs` | `RunMeetingSessionAsync` 新增 5 種 prompt 判斷分支 |
| `src/Directory.Build.props` | v3.17.0 |
| `docs/agents/software team/Petra.md`（視內容）| 文字修訂 |

---

## 建議實作順序

```
1. 新增 PmAgentService.PrepareClaudeCodeEnvAsync helper
   ↓
2. 改寫 RunCodyAppealAsync（Review Appeal 最單純，先試水）
   ↓
3. 改寫 RunVeraAppealAsync
   ↓
4. 改寫 ArbitrateReviewAppealAsync
   ↓
5. 改寫 RunCodyDevPlanAppealAsync
   ↓
6. 改寫 ReassessDevPlanAsync
   ↓
7. MockClaudeCodeService 5 種新分支
   ↓
8. dotnet build 驗證
   ↓
9. MockMode 驗收三個 /mock 失敗情境（fail_review, fail_qa, fail_dev_plan）
   ↓
10. 版本號 + commit
```

---

## 驗收清單

### 環節改寫
- [ ] 5 個方法全部改為 `RunMeetingSessionAsync` 呼叫
- [ ] `PrepareClaudeCodeEnvAsync` helper 建立並共用
- [ ] 每個方法保留原本的解析邏輯（`TryParseXxx`）和 fallback 邏輯

### MockMode
- [ ] 5 種新 prompt 判斷分支實作
- [ ] `/mock fail_review` 走完整 Review Appeal 迴圈（三輪）測試通過
- [ ] `/mock fail_dev_plan` 走完整 Dev_plan Appeal 迴圈測試通過

### 整體
- [ ] `dotnet build AiTeam.slnx` 零 error
- [ ] `dotnet test` 通過
- [ ] v3.17.0 版本號更新
- [ ] Master Plan 和 Future_Feature 同步更新（FF 八 Phase 2 第三項標記完成）

---

## 設計決策與注意事項

### 為什麼只做「升級」，不順便改進流程設計？

FF 八 Phase 2 有三個子項（循環偵測 / 新鮮視角 / API→CLI 升級），Christ 決定先做第三項、其他兩個等實際使用後觀察再做。本 Stage 嚴格只做升級，不觸及迴圈規則、重試策略、仲裁邏輯——保持範圍可控。

### 為什麼用唯讀工具（Glob / Grep / Read）？

5 個環節全是「審核 / 評估 / 仲裁」性質，不需要修改檔案或跑 Bash。限制工具降低 prompt injection 風險，也讓 Claude 專注在分析而非 codebase 操作。

### 為什麼不擔心 Petra 的多 session 並發？

Petra 目前已是 session-aware（Kickoff / Design 會議主持人），加上本 Stage 的 5 個新 session（且都是 short-lived），對 Petra 的「角色意識」影響很小。Kickoff / Design session 在 TaskGroup 級別長期存在，本 Stage 的 appeal session 是 per-appeal 短期——兩者不衝突。

### Petra 的累積專案記憶是好主意，但不在本 Stage

FF 八完成後，Petra 的角色會更重要。她對「這個 TaskGroup 之前怎麼走過來」的記憶可能有價值。建議獨立 FF（「Petra 專案記憶」）後續討論。

---

## 實作紀錄（2026-04-20）

### 實作完成項目

| 項目 | 狀態 | 說明 |
|------|------|------|
| Helper 抽取 | ✅ | `PrepareClaudeCodeEnv`（同步方法，CloneOrPull + 讀 model/apiKey）+ `BuildAppealContextSectionAsync`（組 TaskPlan/DesignPlan/DevPlan/ImplementationNote + PR diff best-effort）|
| 環節 1 Cody 修正 Dev_plan | ✅ | `RunCodyDevPlanAppealAsync` → 新開 session，`[APPEAL:dev_plan_cody]` 標籤 |
| 環節 2 Petra 再評估 Dev_plan | ✅ | `ReassessDevPlanAsync` → 新開 session，`[APPEAL:dev_plan_petra]` 標籤 |
| 環節 3 Cody 反駁 Review | ✅ | `RunCodyAppealAsync` → 新增 `TaskGroup group` 參數 + 新開 session，`[APPEAL:review_cody]` 標籤 |
| 環節 4 Vera 再評估 Review | ✅ | `RunVeraAppealAsync` → 新增 `TaskGroup group` 參數 + 新開 session，`[APPEAL:review_vera]` 標籤 |
| 環節 5 Petra 仲裁 Review Appeal | ✅ | `ArbitrateReviewAppealAsync` → 新增 `TaskGroup group` 參數 + 新開 session，`[APPEAL:review_arbitration]` 標籤 |
| MockClaudeCodeService 5 種分支 | ✅ | 在 `RunMeetingSessionAsync` 的 agentName switch 之前加 5 個 `[APPEAL:*]` early return |
| PmAgentService 依賴擴充 | ✅ | 注入 `GitHubService` + `IOptions<GitHubSettings>` |
| TaskGroupService 3 個呼叫點 | ✅ | line 927/944/1030 補傳 `group` 參數 |
| 版本號 | ✅ | v3.16.1 → v3.17.0 |
| 文件更新 | ✅ | `docs/agents/software team/PM_Agent.md`（申訴環節改走 Claude Code CLI）|

### 關鍵設計調整（規劃書建議採納）

1. **`PrepareClaudeCodeEnv` 改為同步方法**（規劃書 Step 2 原寫 async + `await Task.CompletedTask`，Aria review 建議改為同步以誠實反映行為）
2. **`BuildAppealContextSectionAsync` XML 註解標註 PR diff 跳過邏輯**（Dev_plan Appeal 場景 PR 尚未建立，`TryParsePrNumber` 自動返回 false，避免未來維護者困惑）

### 驗收後修正（2026-04-20）

**commit `c2e088e`**：MockMode 第一次驗收發現 `/mock fail_review` 與 `/mock fail_dev_plan` 只覆蓋 1/5 個新 CLI 分支。

**根因**：`FailScenario` 狀態機提早結束迴圈：
- `FailScenario` 在第一次觸發後會設為 null
- 導致第 2 輪起 Cody/Vera/Petra 都走一般 mock 路徑（而非 appeal 路徑）
- 結果 5 個新 `[APPEAL:*]` 分支只有 1 個被觸發

**修法**：MockClaudeCodeService 依 prompt 內容動態判斷輪次：
- 含「前幾輪對話紀錄」關鍵字 → 判定為後續輪
- 含 `"disagree"` → 判定為 Vera 重評場景
- 含 `[MOCK-FAIL] Dev_plan 不夠詳細` → 判定為 Dev_plan Appeal 場景

修正後 `/mock fail_review` + `/mock fail_dev_plan` 走完整迴圈，5/5 新 CLI 分支全觸發。

### 搭車發現（記入 Future_Feature）

**FF 十八：Appeal 對抗紀錄 UI 呈現**
- 驗收時 Christ 問「對抗資訊有沒有存 DB」→ 檢查發現 `ReviewAppealLog` / `DevPlanAppealLog` 都完整存在
- 但 Dashboard 完全沒呈現這些資料
- Stage 30 上線後對抗資訊量會增加（Cody/Vera/Petra 都帶 codebase 脈絡進場，反駁更有料），沒 UI 呈現會白白浪費資料
- 🟡 中優先級，記入 Future_Feature 十八

### Mock 下驗收覆蓋情況

**已驗** ✅：
- `/mock fail_review`：Cody disagree → Vera maintain → 超過 `ReviewAppealMaxRounds` → Petra 仲裁 → 通過。`[APPEAL:review_cody]` / `[APPEAL:review_vera]` / `[APPEAL:review_arbitration]` 3 個分支全觸發
- `/mock fail_dev_plan`：Petra revise → Cody disagree → Petra 再評 → approve。`[APPEAL:dev_plan_cody]` / `[APPEAL:dev_plan_petra]` 2 個分支全觸發
- `dotnet build AiTeam.slnx` 零 error

**未驗**（Mock 下無意義，待真實運行觀察）：
- 真實 Claude Code CLI 的 codebase 讀取效果（Cody/Vera/Petra 實際用 Read/Glob/Grep 驗證推論的品質）
- Prompt Caching 實際命中率
- PR diff 取得對 Review Appeal 品質的實際貢獻

**品質觀察計畫**（規劃書已記）：上線後 2~4 週追蹤 Cody/Vera/Petra appeal 品質指標，累積資料後決定是否補「session 摘要」或升級到 Resume。

### 踩坑紀錄

1. **`FailScenario` 狀態機提早結束迴圈** — MockMode 只觸發 1/5 新分支（見「驗收後修正」）
2. **Octokit `PullRequestFile` 屬性名** — 規劃書預防性提醒「可能需確認版本」，實測 `FileName` 正確（Octokit 9.x）
3. **同 group 多個 appeal session 的 CloneOrPull suffix 衝突** — `$"appeal-{group.Id:N}"[..12]` 同一個 group 固定，好處是多個 appeal 共用 clone 不重複 clone，壞處是 cleanup 時機要每個方法各自 `finally` 處理（已在 5 個方法的 `finally` 中 `CleanupLocalRepo`）

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-19 | v0.1 | Aria 撰寫初版（討論稿）；提出 4 個待 Christ 拍板的設計決策 |
| 2026-04-19 | v1.0 | 4 個設計決策全部定案（方式 A：新開 CLI session + 強化 Prompt）；補 Token 成本估算與品質觀察計畫；狀態由「討論稿」改為「待實作」|
| 2026-04-20 | v2.0 | Stage 30 實作完成並驗收通過；補「實作紀錄」「驗收後修正」「Mock 驗收覆蓋」「踩坑紀錄」四個章節；搭車發現對抗紀錄存 DB 但 UI 未呈現 → 記入 FF 十八 |

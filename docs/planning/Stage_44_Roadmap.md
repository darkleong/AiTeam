# Stage 44：FF 三十三 Token 計費機制 CLI Agent 涵蓋

> 對應 Future Feature：FF 三十三（Token 計費機制 CLI Agent 涵蓋）
> 對應版本：v3.31.0
> 建立日期：2026-04-29
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：補完 Trial_v4 揭露的 token_logs **6% 涵蓋率盲點**——94% 成本走 CLI Agent（Vera/Quinn/Sage/Cody/Petra/Victoria/Kickoff/Design）但完全沒進 token_logs。

**三大塊**：
1. **CLI token capture**：Claude Code subprocess 結束時抓 `--output-format json` 的 `usage` 欄位
2. **token_logs schema 升級**：加 Stage / Round / CacheReadTokens / CacheWriteTokens / TotalCostUsd 五欄位（全 nullable）
3. **Stage 22 守門邏輯升級**：cache_read 計 0.1x cost（對齊 Anthropic 真實計費）

**戰略意義**：
- **Trial_v5 前置條件之一**：Trial_v4 token_logs 6% 涵蓋率，Stage 44 完成後 Trial_v5 應驗證 **90%+ 涵蓋率**
- **FF 一（API 費用優化）前置條件**：沒有完整 token 涵蓋無法做有意義的成本分析

---

## 子項 1：Claude Code CLI session 結束時 token capture

### 現況

[`src/AiTeam.Bot/Agents/ClaudeCodeService.cs`](../../src/AiTeam.Bot/Agents/ClaudeCodeService.cs) 的 `ParseJsonOutput`（line 478-517）只取 `type="result"` 物件的 `result` 字串，**沒抓 `usage` / `total_cost_usd` 欄位**。8 個 CLI Agent 走 ClaudeCodeService 共用，全部沒進 token_logs。

### 設計決策（Christ 2026-04-29 拍板）

| 決策點 | 拍板 |
|---|---|
| **CLI token capture 機制** | **A**：解析既有 `--output-format json` result 物件的 `usage` 欄位（最便宜，加欄位即可） |
| **多 Agent 會議 token 歸屬** | **B**：整場記為新 AgentName「**Meeting-Kickoff**」/「**Meeting-Design**」（會議當獨立 Agent 計，避免 Petra token 看起來爆量） |

### 實作項目

#### 1-1：Forge Plan Mode 第一步驗證 Claude Code JSON schema

**Plan Mode 必做**：用 sample CLI call 確認 `--output-format json` 的 `type="result"` 物件實際 `usage` 欄位結構：
- 預期欄位：`input_tokens` / `output_tokens` / `cache_creation_input_tokens` / `cache_read_input_tokens`
- 額外欄位：`total_cost_usd` / `duration_ms` / `service_tier`
- **claude-cli 版本相依**，若實際 schema 與預期不符，計劃書要更新

**Forge 用 Bash diagnostic toolkit 驗證**（呼應 workflow_forge 第七節）：
```bash
echo "say hi" | claude --output-format json --print | tail -1 | jq '.'
```

#### 1-2：ClaudeCodeService 擴充 token capture

**位置**：`ClaudeCodeService.ParseJsonOutput`（line 478-517）擴充返回值

**改動**：
- 既有：`(bool Success, string Output)` tuple
- **改為**：`(bool Success, string Output, TokenUsage? Usage)` 含 token usage record（input / output / cache_creation / cache_read / total_cost_usd）
- 從 `type="result"` 物件的 `usage` 欄位解析（若 schema 不符 → return Usage = null）

**TokenUsage record 定義**（新檔 `src/AiTeam.Bot/Agents/TokenUsage.cs`）：
```csharp
public record TokenUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    decimal? TotalCostUsd);
```

#### 1-3：8 個 CLI Agent caller 對齊注入 token 寫入點

**對齊清單**（Forge Plan Mode 第一步 grep `RunClaudeCodeAsync` / `ClaudeCodeService` 找所有 caller）：
- `Vera`（ReviewerAgentService）
- `Quinn`（QaAgentService）
- `Sage`（DocAgentService）
- `Cody`（DevAgentService Dev 階段 + Dev_plan 階段，可能要分兩個 AgentName）
- `Petra`（PmAgentService 各子 service）
- `Victoria`（CeoAgentService）
- `Kickoff`（會議用 AgentName `Meeting-Kickoff`）
- `Design`（會議用 AgentName `Meeting-Design`）

**寫入點設計**：
- 每個 CLI caller 拿到 `TokenUsage` 後 → 呼叫**新增的 `TokenLogService.LogCliUsageAsync`**（共用寫入 helper）
- `LogCliUsageAsync(agentName, model, stage, round, usage, taskId)` — 統一寫入邏輯

#### 1-4：硬規則 — token capture 失敗不阻塞主流程

**位置**：CLI caller 拿 `TokenUsage` 後寫 token_logs 的程式碼

**規則**：
```csharp
try
{
    if (usage is not null)
        await tokenLogService.LogCliUsageAsync(...);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "CLI token 寫入失敗（Agent={Agent}），不影響主流程", agentName);
    // 主流程繼續，不 throw
}
```

**理由**：8 個 CLI Agent 共用 ClaudeCodeService，token capture 任何錯誤（解析失敗 / DB 寫入失敗 / schema 不符）**不得影響任務本身執行**。

### 不在範圍

- ❌ Stream-json 模式（line 56-159）的 token capture — 該模式僅圖片 input 用，使用率低，留後續評估
- ❌ Anthropic API 直接 call（CeoAgentService API 層 / Rosa / Demi / Sage 部分）— 既有 `TokenTrackingProvider` 已涵蓋

---

## 子項 2：token_logs schema 升級

### 現況

[`src/AiTeam.Data/Entities.cs:152`](../../src/AiTeam.Data/Entities.cs) `TokenLog` 既有 schema：
```csharp
public class TokenLog
{
    public Guid   Id { get; set; }
    public string AgentName { get; set; } = "";
    public string Model { get; set; } = "";
    public int    InputTokens { get; set; }
    public int    OutputTokens { get; set; }
    public Guid?  TaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TaskItem? Task { get; set; }
}
```

**缺**：Stage（哪個工作階段）/ Round（fix iteration / 會議輪次）/ Cache token / Cost。

### 設計決策（Christ 2026-04-29 拍板）

| 決策點 | 拍板 |
|---|---|
| **schema 升級範圍** | **B 完整擴充**（Stage + Round + cache + cost，全 nullable，舊資料留空） |

### 實作項目

#### 2-1：Entity 加新欄位

```csharp
public class TokenLog
{
    // ... 既有欄位 ...

    /// <summary>Stage 44：工作階段（Kickoff / Design / Dev_plan / Dev / Reviewer / QA / Doc）。null = 既有資料或無階段語意。</summary>
    public string? Stage { get; set; }

    /// <summary>Stage 44：fix iteration / 會議輪次（如 Vera fix loop round 1, Kickoff round 2）。null = 無輪次語意。</summary>
    public int? Round { get; set; }

    /// <summary>Stage 44：Prompt cache 寫入 token 數（Anthropic 計 1.25x cost）。null = 既有資料或無 cache。</summary>
    public int? CacheCreationTokens { get; set; }

    /// <summary>Stage 44：Prompt cache 讀取 token 數（Anthropic 計 0.1x cost）。null = 既有資料或無 cache。</summary>
    public int? CacheReadTokens { get; set; }

    /// <summary>Stage 44：本次呼叫總成本（USD），由 Claude Code 直接提供，避免 Aria 計算錯誤。null = 既有資料。</summary>
    public decimal? TotalCostUsd { get; set; }
}
```

#### 2-2：Migration `Stage44TokenLogsSchemaUpgrade`

```bash
dotnet ef migrations add Stage44TokenLogsSchemaUpgrade \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

純加 5 個 nullable 欄位，無 schema 破壞性。token_logs 是高頻寫入表，但 PostgreSQL `ALTER TABLE ADD COLUMN` 加 nullable 欄位非鎖表操作。

#### 2-3：既有寫入點對齊

**`TokenTrackingProvider.cs:97-98`**（既有 API 層 Agent 寫入點）：
- 加 cache token 寫入（若 Anthropic API response 含 cache 欄位）
- 加 cost / stage / round（依 caller 提供）

**新增 `TokenLogService.LogCliUsageAsync`** 共用寫入 helper：
- 接 CLI caller 傳的 `(agentName, model, stage, round, usage, taskId)`
- 寫入 token_logs 含全欄位

#### 2-4：多 Agent 會議 AgentName 規範

| 會議類型 | AgentName |
|---|---|
| Kickoff 會議 | `Meeting-Kickoff` |
| Design 會議 | `Meeting-Design` |
| 各申訴階段（Review Appeal / Dev_plan Appeal / QA Routing）| 視單一 Agent 處理，不歸會議類 |

**理由**：會議是多 Agent 對話，CLI 整段 token 無法區分 → 整場記為「會議獨立 Agent」，統計時 Dashboard 可分「個別 Agent token」vs「會議 token」。

### 不在範圍

- ❌ token_logs 既有資料填補 Stage / Round（舊資料留 null）
- ❌ Dashboard token 監控頁面 UI 改動（schema 升級即可，UI 顯示新欄位留後續搭車）

---

## 子項 3：Stage 22 守門邏輯升級

### 現況

`TokenTrackingProvider`（line 38-95）守門邏輯：
- 單次呼叫上限：`SingleRequestTokenLimitK * 1000`
- 日累積上限：`DailyTokenLimitK * 1000`
- 月累積上限：`MonthlyTokenLimitK * 1000`
- 全域月上限：`AgentSettings.MonthlyTokenLimitK * 1000`

**目前 SUM 公式只算 `InputTokens + OutputTokens`**，沒考慮 cache token。

### 設計決策（Christ 2026-04-29 拍板）

| 決策點 | 拍板 |
|---|---|
| **cache token 計法** | **A：cache_read 算 0.1x cost / cache_creation 算 1.25x cost**（對齊 Anthropic 真實計費）|
| **會議型 Agent 日限** | **A：共用日限**（先觀察 Trial_v5 是否踩線，有踩再調）|

### 實作項目

#### 3-1：守門 SUM 公式升級

**新「等效 token」計算**（對齊 Anthropic 計費）：
```csharp
public static long ComputeEffectiveTokens(TokenLog log)
    => log.InputTokens
     + log.OutputTokens
     + (long)((log.CacheCreationTokens ?? 0) * 1.25)
     + (long)((log.CacheReadTokens ?? 0) * 0.1);
```

#### 3-2：四道關卡更新

`TokenTrackingProvider` 既有四道關卡（單次 / 日 / 月 / 全域月）的 SUM 公式全部改用 `ComputeEffectiveTokens`。

舊資料 cache 欄位 null → 視為 0（無變化），對舊資料行為無影響。

#### 3-3：日限 / 月限門檻**不調整**

依 Christ 拍板議題 5 方案 A：所有 Agent（含會議型）共用既有 `DailyTokenLimitK` / `MonthlyTokenLimitK`。

**Trial_v5 觀察期**若會議型 Agent 踩線頻繁 → FF 立案調整（本 Stage 不做）。

### 不在範圍

- ❌ 守門告警機制改動（守門擋下 LLM 後既有錯誤通知不動）
- ❌ Dashboard 守門設定頁面新增 cache 顯示（schema 升級即可，留後續搭車）

---

## 整體驗收原則

**本 Stage 動共用機制（ClaudeCodeService）+ schema migration + 守門邏輯升級**。驗收三層：

### 第一層：靜態驗收

✅ Migration 跑起來（`dotnet ef database update`）；token_logs 含 5 個新 nullable 欄位；Build 通過。

### 第二層：真實 CLI 跑驗證 ⭐（**本 Stage 主要驗收手段**）

不像 Stage 43 流程行為驗收 Mock 跑，**本 Stage 主要驗收是真實 CLI 跑** — 跑既有 Mock 場景（如 `new_feature_with_proposal` / `qa_failed_fix_then_intervention`）後查 token_logs 確認：
- 8 個 CLI Agent 都有寫入紀錄
- Stage / Round / Cache / Cost 欄位都有值（或合理 null）
- 對比 Anthropic Console 帳單看誤差（90%+ 涵蓋率為通過標準）

### 第三層：Trial_v5 對照（留待後續）

✅ Stage 44 完成後，Trial_v5 跑完整 pipeline 應驗證 **90%+ token 涵卢率**（對照 Trial_v4 6%）。

---

## 驗收情境

### A. Migration + 新欄位

**驗收方式**：
1. `dotnet ef migrations list` → 含 `Stage44TokenLogsSchemaUpgrade`
2. `docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d token_logs"` → 含 `Stage` / `Round` / `CacheCreationTokens` / `CacheReadTokens` / `TotalCostUsd` 五欄位
3. `dotnet build AiTeam.slnx` → 0 Errors

### B. CLI Agent 真實跑後 token_logs 寫入

**驗收方式**：
1. Dashboard `/mock` 觸發 `new_feature_with_proposal`（涵蓋 Kickoff + Design + Dev_plan + Dev + Reviewer + QA + Doc 全流程）
2. DB 驗證：
```sql
SELECT "AgentName", "Stage", "Round", "InputTokens", "OutputTokens",
       "CacheReadTokens", "TotalCostUsd"
FROM token_logs
WHERE "CreatedAt" > NOW() - INTERVAL '5 minutes'
ORDER BY "CreatedAt";
```
3. 預期看到 8 個 CLI Agent 都有紀錄（含 `Meeting-Kickoff` / `Meeting-Design` 兩個會議 entry）

### C. 多 Agent 會議歸屬

**驗收方式**：
1. Kickoff 階段觸發後查 token_logs
2. AgentName = `Meeting-Kickoff`（不是 Petra 或其他單一 Agent 名）
3. Stage = `Kickoff`，Round = 對應會議輪次

### D. 守門邏輯（cache token 計法）

**驗收方式**：
1. 跑一次有 cache hit 的 CLI 場景（重複任務觸發 prompt cache）
2. 查 token_logs 確認 `CacheReadTokens > 0`
3. 觀察守門日累積計算：等效 token = input + output + cache_read × 0.1（不是純加總）— 由 Forge 在實作紀錄附 SQL 驗證

### E. token capture 失敗不阻塞主流程（容錯驗證）

**驗收方式**：
1. Forge 在實作紀錄附「模擬 ClaudeCodeService.ParseJsonOutput 解析失敗」測試
2. 預期：主任務正常完成（result 字串仍正常返回）+ log 出現 warning「CLI token 寫入失敗，不影響主流程」+ token_logs 該次無紀錄
3. **任務 Status 不應因 token 寫入失敗而 failed**

### F. CI/CD 自動部署 + Anthropic Console 對照（留待）

**驗收方式**：
1. push 後 GitHub Actions self-hosted runner 自動 rebuild + Migration 自動跑
2. 一週後對比 Anthropic Console 帳單 vs token_logs 計算成本，**誤差 < 10%**
3. **真實 90%+ 涵蓋率驗證留 Trial_v5**

---

## 技術約束 & 注意事項

1. **Migration 指令**：
   ```
   dotnet ef migrations add Stage44TokenLogsSchemaUpgrade --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
   ```

2. **token capture 失敗不阻塞主流程（硬規則）**：8 個 CLI Agent 共用 ClaudeCodeService，token capture 任何錯誤（解析失敗 / DB 寫入失敗 / schema 不符）**不得影響任務本身**。所有 token 寫入點用 try-catch 包覆 + log warning。**這條是計劃書硬規則，違反不得進入驗收**。

3. **Claude Code JSON schema 版本相依**：claude-cli 升級可能改變 `usage` 欄位 — Forge Plan Mode 第一步用 sample CLI call 驗證實際 schema，若與預期不符更新計劃書。

4. **8 個 CLI Agent caller 對齊清單**（Forge 實作期 grep `ClaudeCodeService` 找所有 caller，逐一加 token 寫入點，實作紀錄附 checklist）：
   - Vera / Quinn / Sage / Cody（Dev + Dev_plan 兩階段）/ Petra（多子 service）/ Victoria / Kickoff / Design

5. **token_logs 高頻寫入表 Migration**：純加 nullable 欄位無破壞性。但確認 Migration 跑時 `ALTER TABLE ADD COLUMN` 不鎖表。

6. **多 Agent 會議 AgentName 統一前綴 `Meeting-*`**：Kickoff = `Meeting-Kickoff` / Design = `Meeting-Design`。Dashboard 統計頁面之後可用 `LIKE 'Meeting-%'` 篩選會議型 token。

7. **Mock Mode 不適合本 Stage 主要驗收**（呼應 workflow_aria 第七節 #7）：本 Stage 主要驗收是真實 CLI 跑（Mock 跳過 LLM，token capture 沒輸入）。**真實生效驗證留 Trial_v5**。

8. **Stage 43 校準錨提醒**（呼應 workflow_aria 第二節 B + 自省點 #18）：跨層 + 動共用機制 + 多 Agent 對齊類 Stage，follow-up 修正成本高（150-300K 範圍）。Forge 預期實作期可能踩 8 個 caller 對齊缺漏，**主動用 Bash diagnostic toolkit**（grep / docker logs / docker exec psql）自助查證，不要等 Christ 截圖。

---

## 版本

`v3.30.0 → v3.31.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議（用 Stage 43 校準後新 7 項公式）

**推薦：Opus 1M + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **重**（ClaudeCodeService + 8 個 CLI Agent caller + TokenLog Entity + Migration + TokenTrackingProvider + 新 TokenLogService + Dashboard 統計查詢點）|
| **邏輯複雜度** | **中**（CLI JSON schema 解析 + token 等效公式 + 多 Agent 會議歸屬規範，無跨流程設計判斷） |
| **風險代價** | **高**（動 ClaudeCodeService 共用機制，bug 影響全 8 個 CLI Agent）|
| **範本可用度** | **中**（既有 TokenTrackingProvider + Stage 38 動 Provider 經驗可參考，但 CLI capture 是新領域）|

### Context 精確估算（7 項公式）

| 項目 | 估算 |
|---|---|
| 開場固定成本（system + CLAUDE.md + memory + conventions：csharp / ef-core / api-design 必讀）| ~32K |
| 工作 raw（ClaudeCodeService + 8 個 CLI caller 對齊 + TokenLog Entity + Migration + TokenTrackingProvider + 新 TokenLogService + Stage 22 守門邏輯）| ~70-100K |
| Grep / Bash（CLI sample call 驗證 schema + grep 8 個 caller + Migration + DB 查詢）| ~15-20K |
| 對話 turn 成本（Plan Mode + 閘門一可能 1-2 輪 + 實作期 + 結案）| ~30-45K |
| Edit 反覆對齊（8 個 CLI caller 對齊 + token_logs 寫入點對齊）| ~15-25K |
| Mock 驗收成本（**Mock 價值低**，主要靠真實 CLI 跑）| ~5-15K |
| **驗收期 follow-up 修正**（跨層 + 動共用機制 + 多 caller → **中-高風險**：8 個 caller 對齊缺漏 / Anthropic JSON schema 版本不符 / cache 計法邊界 case）| ~80-150K（5-7 個 fix × 30K） |
| 結案文件寫作 | ~10K |
| **總和** | **~257-397K**（中位數 ~327K） |

→ **Opus 1M + medium 200-400K 舒適區**（負擔 26-40%）

**選 Opus 1M + medium 理由**：
- 完整公式 ~257-397K，**Sonnet 200K 絕對不夠**（Stage 42/43 兩次 ×1.9 反例校準後新門檻嚴格）
- Opus 1M 200K 內負擔 26-40%，舒適區
- 動共用機制風險高，Opus 推理品質有顯著加分（避免踩 8 個 caller 對齊漏修）
- **不拆 Session**：8 個 CLI caller + token_logs schema + 守門邏輯彼此關聯，拆 Session 重複載入成本不划算

**替代方案**：
- 若 Christ 偏保守 → **Opus 1M + high**（成本差距小，動共用機制保險起見）
- **不推薦 Sonnet 200K + high**：Stage 42/43 校準後新門檻顯示 200K+ 公式估算的 Stage 不該用 Sonnet

**Stage 43 校準提醒**：「跨層實作 + Mock 多場景 + 動 LLM 路徑」類 Stage 倍率 ×1.94（Stage 43 反例）。本 Stage 性質類似但 Mock 驗收價值較低（真實 CLI 為主）→ follow-up 修正可能略低，估 80-150K 範圍。

---

## 後續關聯

- **Stage 45 = FF 三十四**（TaskGroup 流程暫停機制）：Trial_v5 鎖死前置條件，Stage 44 完成後可開
- **Stage 46 = FF 三十五**（自動拆任務 ⭐ 戰略級）
- **Trial_v5**：Stage 44-46 全完成後執行（重跑 FF 十六需求對照 Trial_v4，驗 90%+ token 涵蓋率 + 三層補強對應 Bug 全清）
- **FF 一**（API 費用優化）：Stage 44 完成後可基於 token_logs 完整資料做 Prompt Caching / Batch API / 模型評估等優化
- **FF 三十七**（escalate skip status 殘留）：本 Stage **不搭車**（不動 InteractionService），留 Stage 45 動流程暫停時搭車

---

## 不在範圍

- ❌ FF 一 API 費用優化（Stage 44 完成後才能做）
- ❌ Dashboard token 監控頁面 UI 改動（既有頁面已存在，本 Stage 只動 schema + 寫入；UI 顯示新欄位留後續搭車）
- ❌ 守門告警機制改動（公式升級即可，告警通知不動）
- ❌ AgentResultType 擴充（既有 Skipped 已涵蓋）
- ❌ 流程暫停（FF 三十四 / Stage 45）
- ❌ 自動拆任務（FF 三十五 / Stage 46）
- ❌ FF 三十七 escalate skip status 殘留（不搭車，留 Stage 45）
- ❌ Stream-json 模式 token capture（圖片 input 用，使用率低）
- ❌ Anthropic API 直接 call 路徑改動（既有 TokenTrackingProvider 已涵蓋）
- ❌ token_logs 既有資料填補（舊資料 Stage / Round / Cache 留 null）
- ❌ 會議型 Agent 獨立日限（依 Christ 拍板議題 5 方案 A 共用日限，Trial_v5 觀察）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-29 | 計劃書建立（Aria）— FF 三十三 Token CLI 涵蓋三大塊（CLI capture + schema 升級 + 守門邏輯升級）合一 Stage |
| v1.1 | 2026-04-29 | 實作完成（Forge）— 補實作紀錄章節（4 必含章節：Roadmap 校準 / 16 caller checklist / MeetingCommons 17 處 checklist / Trial_v5 涵蓋率目標升級） |

---

## 實作紀錄（Forge，2026-04-29）

### Phase 1 — JSON Schema 驗證 ✅

執行 `echo "say hi" | claude --output-format json --print | tail -1 | python -m json.tool` 實證 schema 與 Roadmap 預期完全一致：

```jsonc
{
  "type": "result",
  "is_error": false,
  "total_cost_usd": 0.008487600000000001,    // ✅ root level decimal
  "usage": {
    "input_tokens": 2,                        // ✅
    "cache_creation_input_tokens": 0,         // ✅
    "cache_read_input_tokens": 27272,         // ✅
    "output_tokens": 20                       // ✅
  }
}
```

**結論**：無需更新計劃書 schema 假設。

### 1. Roadmap 描述校準（必含章節 #1）

> **Roadmap 預估 8 caller，Phase 2 grep 揭露實際 14 個必要 caller**（含申訴各分支的 Petra-Review / Petra-Arbitration / Petra-Reassess + Cody-ReviewAppeal / Vera-ReviewAppeal / Cody-DevPlanAppeal）**+ Aria 閘門一拍板搭車 Rosa/Demi 2 個 = 共 16 個 CLI Agent caller**。Roadmap 原描述粗估，計劃書 v1.1 校準後正式記錄差異。

實際 MeetingCommons.RunAgentTurnAsync 的 call site 計畫書估 17 處（Kickoff ×6 + Design ×11），grep 揭露 **Kickoff ×7 + Design ×14 = 21 處**（再校準一次粗估），全部已補完 `meetingType` + `round` + `tokenLogService`。

### 2. 16 處 CLI Agent caller LogCliUsageAsync ✅ checklist（必含章節 #2）

| # | 邏輯 Agent | Caller (檔:行) | LogCli `agentName` | `stage` | `round` | 已補 ✅ |
|---|---|---|---|---|---|---|
| 1 | Vera | [ReviewerAgentService.cs:236](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs) | `Vera` | `Reviewer` | `task.Group?.FixIteration` | ✅ |
| 2 | Quinn | [QaAgentService.cs:243](../../src/AiTeam.Bot/Agents/QaAgentService.cs) | `Quinn` | `QA` | `task.Group?.QaFixRound` | ✅ |
| 3 | Sage | [DocAgentService.cs:223](../../src/AiTeam.Bot/Agents/DocAgentService.cs) | `Sage` | `Doc` | null | ✅ |
| 4 | Cody-Dev | [DevAgentService.cs:277](../../src/AiTeam.Bot/Agents/DevAgentService.cs) | `Cody` | `Dev` | `task.Group?.FixIteration` | ✅ |
| 5 | Cody-Dev_plan | [DevAgentService.cs:792](../../src/AiTeam.Bot/Agents/DevAgentService.cs) | `Cody` | `Dev_plan` | `task.Group?.DevPlanRevision` | ✅ |
| 6 | Petra-Review | [PmReviewService.cs:96](../../src/AiTeam.Bot/Agents/Pm/PmReviewService.cs) | `Petra` | `Petra_review` | null | ✅ |
| 7 | Petra-Arbitration | [Pm/ReviewAppealService.cs:163](../../src/AiTeam.Bot/Agents/Pm/ReviewAppealService.cs) | `Petra` | `ReviewAppeal_arbitration` | `group.ReviewAppealRoundA` | ✅ |
| 8 | Petra-Reassess DevPlan | [Pm/DevPlanAppealService.cs:97](../../src/AiTeam.Bot/Agents/Pm/DevPlanAppealService.cs) | `Petra` | `DevPlanAppeal_petra` | `group.DevPlanAppealRoundA` | ✅ |
| 9 | Cody-ReviewAppeal | [Pm/ReviewAppealService.cs:54](../../src/AiTeam.Bot/Agents/Pm/ReviewAppealService.cs) | `Cody` | `ReviewAppeal_cody` | `group.ReviewAppealRoundA` | ✅ |
| 10 | Vera-ReviewAppeal | [Pm/ReviewAppealService.cs:113](../../src/AiTeam.Bot/Agents/Pm/ReviewAppealService.cs) | `Vera` | `ReviewAppeal_vera` | `group.ReviewAppealRoundA` | ✅ |
| 11 | Cody-DevPlanAppeal | [Pm/DevPlanAppealService.cs:49](../../src/AiTeam.Bot/Agents/Pm/DevPlanAppealService.cs) | `Cody` | `DevPlanAppeal_cody` | `group.DevPlanAppealRoundA` | ✅ |
| 12 | Victoria | [CeoAgentService.cs:196](../../src/AiTeam.Bot/Agents/CeoAgentService.cs) | `CEO` | `CEO` | null | ✅ |
| 13 | Meeting-Kickoff | 透過 [MeetingCommons.RunAgentTurnAsync](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs)（KickoffMeetingService ×7） | `Meeting-Kickoff` | `Kickoff` | `group.KickoffRound` / round | ✅ |
| 14 | Meeting-Design | 透過 MeetingCommons.RunAgentTurnAsync（DesignMeetingService ×14） | `Meeting-Design` | `Design` | `group.DesignRound` / round | ✅ |
| 15 | **Rosa**（Aria 閘門一拍板納入） | [RequirementsAgentService.cs:184](../../src/AiTeam.Bot/Agents/RequirementsAgentService.cs) | `Rosa` | `Requirements` | null | ✅ |
| 16 | **Demi**（Aria 閘門一拍板納入） | [DesignerAgentService.cs:166](../../src/AiTeam.Bot/Agents/DesignerAgentService.cs) | `Demi` | `Designer` | null | ✅ |

### 3. MeetingCommons 21 處 call site checklist（必含章節 #3，方案 A 防線）

> Phase 2 estimat 17 處，實作期 grep 揭露 **Kickoff 7 + Design 14 = 21 處**。所有 call site 都帶 `meetingType` + `round` + `tokenLogService`（grep 第二道防線同步驗證 — 見「實作期 grep 驗證」段）。

#### KickoffMeetingService 7 處

| # | 行 | Agent | meetingType ✅ | round ✅ | tokenLogService ✅ |
|---|---|---|---|---|---|
| 1 | :98 | Rosa | ✅ | ✅ round | ✅ |
| 2 | :102 | Demi | ✅ | ✅ round | ✅ |
| 3 | :106 | Cody | ✅ | ✅ round | ✅ |
| 4 | :110 | Quinn | ✅ | ✅ round | ✅ |
| 5 | :138 | Petra (round summary) | ✅ | ✅ round | ✅ |
| 6 | :177 | Petra (TaskPlan) | ✅ | ✅ totalRounds | ✅ |
| 7 | :241 | Petra (ModifyTaskPlan) | ✅ | ✅ group.KickoffRound | ✅ |

#### DesignMeetingService 14 處

| # | 行 | Agent / Phase | meetingType ✅ | round ✅ | tokenLogService ✅ |
|---|---|---|---|---|---|
| 1 | :91 | Petra Judge | ✅ | ✅ group.DesignRound | ✅ |
| 2 | :104 | Rosa PreWork | ✅ | ✅ group.DesignRound | ✅ |
| 3 | :144 | Demi PreWork | ✅ | ✅ group.DesignRound | ✅ |
| 4 | :180 | Rosa Meeting | ✅ | ✅ round | ✅ |
| 5 | :187 | Demi Meeting | ✅ | ✅ round | ✅ |
| 6 | :192 | Cody Meeting | ✅ | ✅ round | ✅ |
| 7 | :196 | Quinn Meeting | ✅ | ✅ round | ✅ |
| 8 | :228 | Petra Round Summary | ✅ | ✅ round | ✅ |
| 9 | :367 | Petra ModifyDesignPlan | ✅ | ✅ group.DesignRound | ✅ |
| 10 | :435 | Rosa Adjustment | ✅ | ✅ group.DesignRound | ✅ |
| 11 | :477 | Demi Adjustment (create) | ✅ | ✅ group.DesignRound | ✅ |
| 12 | :485 | Demi Adjustment (resume) | ✅ | ✅ group.DesignRound | ✅ |
| 13 | :516 | Petra Eval | ✅ | ✅ group.DesignRound | ✅ |
| 14 | :565 | GenerateDesignPlan helper | ✅ | ✅ round 參數 | ✅ |

**實作期 grep 驗證（第二道防線）**：

```
grep -n "RunAgentTurnAsync" src/AiTeam.Bot/Orchestration/Meeting/{Kickoff,Design}MeetingService.cs
```

每處 call site 後續行皆含 `meetingType:` 字樣 → 全 ✅，無漏補。

### 4. Trial_v5 涵蓋率目標升級（必含章節 #4）

Roadmap 原訂 Trial_v5 對照 Anthropic Console 90%+；Aria 閘門一拍板將 **Rosa/Demi 納入正式範圍** → Trial_v5 涵蓋率目標進一步拉高（具體 % 由 Trial_v5 實測落地）。

### 5. 改動檔案清單

**新增**：
- [src/AiTeam.Bot/Agents/TokenUsage.cs](../../src/AiTeam.Bot/Agents/TokenUsage.cs)
- [src/AiTeam.Bot/Services/TokenLogService.cs](../../src/AiTeam.Bot/Services/TokenLogService.cs)
- [src/AiTeam.Data/Migrations/20260429024106_Stage44TokenLogsSchemaUpgrade.cs](../../src/AiTeam.Data/Migrations/20260429024106_Stage44TokenLogsSchemaUpgrade.cs)（含 .Designer.cs + Snapshot）

**修改**：
- [src/AiTeam.Data/Entities.cs](../../src/AiTeam.Data/Entities.cs) — TokenLog 加 5 nullable 欄位 + `ComputeEffectiveTokens` static helper
- [src/AiTeam.Data/AppDbContext.cs](../../src/AiTeam.Data/AppDbContext.cs) — TokenLog `TotalCostUsd HasPrecision(18, 6)`
- [src/AiTeam.Data/Repositories/TokenRepository.cs](../../src/AiTeam.Data/Repositories/TokenRepository.cs) — 三個 SUM 改 long + cache 等效公式 inline
- [src/AiTeam.Bot/Agents/ClaudeCodeService.cs](../../src/AiTeam.Bot/Agents/ClaudeCodeService.cs) — `ParseJsonOutput` 升 3-tuple + `TryParseUsage` + `ClaudeCodeResult.Usage`
- [src/AiTeam.Bot/Agents/TokenTrackingProvider.cs](../../src/AiTeam.Bot/Agents/TokenTrackingProvider.cs) — long ripple + cache 寫入註解（API 層 cache 細節留 FF 一搭車）
- 16 個 CLI caller service 全部加 `TokenLogService` 注入 + `LogCliUsageAsync` 一行寫入
- [src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs) — RunAgentTurnAsync 加 3 個 optional 參數
- [src/AiTeam.Bot/Orchestration/Meeting/KickoffMeetingService.cs](../../src/AiTeam.Bot/Orchestration/Meeting/KickoffMeetingService.cs) — 7 處 + DI
- [src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs](../../src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs) — 14 處 + DI + GenerateDesignPlanAsync 加 round 參數
- [src/AiTeam.Bot/Program.cs](../../src/AiTeam.Bot/Program.cs) — `AddSingleton<TokenLogService>()`
- [src/Directory.Build.props](../../src/Directory.Build.props) — Version 3.30.0 → 3.31.0

### 6. 驗收期注意事項

1. **本地 Migration 未跑**：Forge 本地 Docker Postgres 未啟動（容器在 Christ 端跑），Migration 檔案已產出且 `Up()` 內容只是 5 個 `AddColumn` + TotalCostUsd `numeric(18,6)`，CI/CD push 後 self-hosted runner 重啟容器自動 `db.Database.Migrate()` 即可生效。
2. **驗收期 SQL 查 token_logs**（呼應 Roadmap 點 9，Bash diagnostic toolkit 自助查證）：
   ```bash
   docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "\d token_logs"
   docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c \
     "SELECT \"AgentName\", \"Stage\", \"Round\", \"InputTokens\", \"OutputTokens\", \"CacheReadTokens\", \"TotalCostUsd\" FROM token_logs ORDER BY \"CreatedAt\" DESC LIMIT 20;"
   ```
3. **真實 CLI 跑驗證**（roadmap 第二層）：Mock Mode 不會寫 token（`new ClaudeCodeResult(...)` 預設 Usage = null → LogCliUsageAsync early return），真實 CLI 跑既有任務（如 `new_feature_with_proposal`）後查 SQL 確認 16 種 AgentName / Stage 組合都有 entry。
4. **守門公式驗證**（roadmap 第三層）：跑兩次相同任務觸發 prompt cache → 確認 `CacheReadTokens > 0`，且守門 SUM 已用等效公式（`× 5/4` / `÷ 10`）。

### 7. 教訓 / 自省

- **Phase 2 grep 數值優於 Roadmap 粗估的價值**：Roadmap 估 8 caller，實際 14 個必要 + 2 搭車；MeetingCommons 估 17 處，實際 21 處。Phase 2 機械化 grep 是計劃書品質的關鍵防線（呼應 Stage 43 校準錨教訓）。
- **方案 A optional 設計 + checklist 紀律機制有效**：21 處 RunAgentTurnAsync 全部補完無漏，build 一次 pass。grep 第二道防線即時驗證，避免悄悄漏寫。
- **ClaudeCodeResult.Usage 設 optional default null**：未動 MockClaudeCodeService 即可天然兼容（Mock 不寫 token），降低變更面。

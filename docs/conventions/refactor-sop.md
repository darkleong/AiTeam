# 服務層大檔案拆解守則（Refactor SOP）

> 本守則由 Stage 34-36（FF 二十）三次實踐累積而成，適用於 Service / Handler 類檔案超過 1000 行、或讀一次吃 > 20K tokens 時的拆解工作。

---

## 何時觸發拆解

任一條件成立就考慮：

- **單檔超過 1000 行**（Read 一次 ≈ 20K tokens）
- **單一類別職責 ≥ 5 種**（混合 Kickoff、Appeal、QA、Proposal 等多領域）
- **未來 Stage 需要頻繁動到**（每次動都要 Read 大檔，累積成本高）
- **實作 Session 抱怨「又要讀整個大檔」連續 2-3 次**

---

## 六項拆解 SOP

### SOP 1：Record / Type 組織

- **Public record / class**（API 合約、caller 會直接用）→ 搬**獨立檔**（例：`MeetingResults.cs`、`PmAgentResults.cs`）
  - 理由：避免引入不必要的大型 namespace 依賴、修改 record 時不用翻找 service 大檔
- **Internal DTO**（JSON parse、實作細節）→ **留各自 service 檔案**
  - 理由：防止跨 service 型別洩漏污染

### SOP 2：Migration 策略

**依 caller 數量決定**：

| Caller 數量 | 策略 |
|---|---|
| **< 15 處**（含 DI 註冊）| **直接切換，不做 thin wrapper** |
| **15-30 處** | 考慮 thin wrapper 過渡，分批遷移 |
| **> 30 處** | 必做 thin wrapper，避免一次改動過大 |

**直接切換的理由**：
- Wrapper 讓兩套名稱並存，未來維護混淆
- Git history 本身是最好的追蹤，直接刪除舊檔無妨
- 減少過渡期的認知負擔

**搬家後注意**：如果有多個子 service 都承接邏輯，**檢查是否有新的超大檔誕生**（例如 Stage 36 CommandHandler 拆後，ButtonCallbackRouter 變 1091 行新怪物）。搬家不等於拆解。

**caller 改動成本評估三層分**（Stage 59 踩坑 #2 — spike 第一步必須區分這三類 caller 才能準確評估範圍）：

| caller pattern | 改動成本 | 改動內容 |
|---|---|---|
| **ctor 注入**（傳統 DI）| **重** | DI registration + ctor parameter 簽名 + base class 呼叫 |
| **既有 IServiceProvider field 注入** | 中等 | 既有 field 多用一處 → 加 GetRequiredService 呼叫 |
| **scope.ServiceProvider.GetRequiredService**（lazy resolve）| **輕** | 1 個 var name + 1 個 using |

→ Stage 59 plan 預期「11 caller 改 ctor」實際發現全是 lazy resolve pattern → 改動 = 22+ call site replace + 0 ctor 改動。spike 第一步 grep 不只看 ctor 注入清單更要看 scope.ServiceProvider.GetRequiredService 模式。

### SOP 3：Commons service 範圍界定

**只放**符合以下所有條件的：
- 多個新 service 都會呼叫
- 呼叫方式完全相同（不是「邏輯相似各自實作」）
- 放進 Commons 不會新增額外依賴（或新增的依賴是所有 caller 本來就需要的）

**不要放進 Commons**：
- 「邏輯相似但各自 private」的 helper（例：`GetApiKey` / `GetModel` 這類 3-5 行的讀 config helper，各自保留即可）
- Prompt builders（各 service 各自 prompt 風格，不共用）
- Internal DTOs（實作細節，跨 service 共用沒意義）

**推廣原則**：
> **Commons 推進的新依賴 vs 保留 3-5 行重複碼** — 保留重複碼比較便宜。

### SOP 4：DI 註冊順序

- **Commons / Store 先註冊**（即使 DI 是 lazy resolved，順序代表依賴方向，讓讀 Program.cs 的人一眼看出基礎層）
- **全部 Singleton**（除非有明確 per-request state 才用 Scoped，對齊既有模式）
- **循環依賴單向檢查**：子 Service → Commons（單向），不可反向

### SOP 5：Session state 管理

- **無 singleton-level state**（只有 local 變數）→ 各方法自管，Commons 不需要 state dictionary
- **有共享 state**（如 `_pendingConfirmations` dictionary）→ 抽成獨立 Singleton **Store**，跨 service 依賴此 Store 而非互相 reference
- **升級 thread-safety**：抽成 Store 後跨 thread 呼叫機會增加（不再侷限於 Discord event loop 單執行緒）→ **用 `ConcurrentDictionary`**（Stage 36 踩過這個坑，原本 `Dictionary` 在抽 Store 後需要升級）
- **state field 拆解時 caller 對應 owning service verify**（Stage 84 踩坑）：state field 拆解時不假設「主要 caller 在哪 service 就拆過去」/ 必先 grep 所有 caller 對應 owning service / 同一 state 跨多 service caller 時拆多份 instance field 對齊 Scoped lifecycle（Stage 84 `_roundRobinCounter` 4 caller 跨 3 service → 拆 3 份 instance field / 同 session 內 acceptable 小偏差 / round-robin 公平性不破 / plan v3 寫「拆 2 份」漏 PlanConfirmation 那份 = 預先 grep 不足）

### SOP 6：檔案夾組織（超過 10 檔就建子資料夾）

**觸發條件**：單一資料夾（如 `Orchestration/` 或 `Agents/`）檔案數 ≥ 10，或 3+ 個同主題 service

**歸屬判斷原則**（Stage 35 實踐結論）：

> **決策主體（誰說話）= Agent 角色時放 `Agents/`；協調多個 Agent 的流程控制放 `Orchestration/`**

**範例**：
- `Agents/Pm/PmReviewService.cs` — Petra 角色在審，歸 Agents
- `Orchestration/Meeting/MeetingOrchestrationService.cs` — 協調多個 Agent 開會，歸 Orchestration

**搬家成本**：
- namespace 從 `AiTeam.Bot.Orchestration` → `AiTeam.Bot.Orchestration.Meeting`
- caller 的 `using` 要補加 `using AiTeam.Bot.Orchestration.Meeting;`（兩個 namespace 可並存，原 using 不用刪）

⚠️ **子目錄 / namespace 名稱避免與既有 entity 同名**（Stage 59 踩坑 #1 — C# child namespace shadow 規則）：

C# 編譯器在 `Parent.X` namespace 內優先解析同層 child namespace，若 child namespace 名稱與既有 entity 同名 → entity 被 shadow，整個 namespace tree 內 entity 引用全部 break。

**反例**（Stage 59 第一次 build 報 75 errors `'TaskGroup' is a namespace but is used like a type`）：
- ❌ `Orchestration/TaskGroup/` 子目錄 + `Data.TaskGroup` entity 同名 → `Orchestration.*` 內所有 `TaskGroup` 引用 break
- ✅ 拆 `Boss/` `Epic/` `Routing/` 3 子目錄取代統一 TaskGroup 子目錄（每個 child namespace 不與 entity 同名）

**SOP 6 子目錄命名規則**：先 grep 同 root namespace 下既有 entity 名稱，避免衝突。

---

## Stage 34-36 實戰數據（參考）

| Stage | 拆解目標 | 原行數 | 拆解後 | 縮減 | Model | Context 消耗 |
|---|---|---|---|---|---|---|
| 34 | MeetingService | 1415 | 4 檔共 997 | —（拆完合計更多是正常）| Sonnet 200K + high | 160K / 200K = 80% |
| 35 | PmAgentService + Agents/Pm/ 子資料夾首次實踐 | 1389 | 6 檔共 1444 | — | Opus 1M + high | 261K / 1M = 26% |
| 36 | TaskGroupService + CommandHandler 合併 | 4795 合計 | TGS 716（-73%）+ CH 556（-74%）+ 11 新檔 | 主檔大幅瘦身 | Opus 1M + high | 360K / 1M = 36% |
| 59 | TaskGroupService（v4 路線後復發 1759 行）| 1759 | TGS 808（-54%）+ 4 新檔合計 1051 | 主檔中度瘦身 | Opus 1M + medium-high | 402K / 1M = 40% |
| 84 | PetraOrchestratorService（v5 ecosystem + v5.5 拆解合集）| 2266 | 主檔 193（**-91.5%**）+ 5 sub-service + 1 helper + 1 DTO + 1 Commons 合計 2075 | 主檔極致瘦身 | Opus 1M + ultrathink | ~400-500K / 1M = 40-50%（single session 完成） |

**觀察**：
- **「SOP 累積後同類工作越做越省」** — Stage 36 規模最大但 context 倍率（×1.49）反而比 Stage 34（×1.60）低；**Stage 59 倍率 ×1.09**（SOP 累積第 4 次 + 新立 workflow_aria.md 第 5+6 條紀律生效 partial read + 不寫 code 範例）— FF 二十系列拆解倍率從 ×1.49-1.65 降到 ×1.09
- **Opus 1M 是大型拆解的舒適區**，Sonnet 200K 邊界緊
- **拆完行數總合可能變多**（Stage 34 997 vs 原 1415 只減 30%；Stage 59 1859 vs 原 1759 = +5.7%）—— 這是正常的，目的是**降低單檔 Read 成本**不是減總碼量
- **dispatch / guard / 路由型主檔瘦身比例典型 -50%~-60%**（Stage 59 踩坑 #3 — vs Stage 34-36 純拆 -73%~-85% 是因為 Stage 34-36 拆對象是「同類別怪物合併」沒 dispatch 主入口；Stage 59 拆對象是「單檔含 dispatch + 多子職責」必留 dispatch 結構 ProcessBossResponseAsync 主 switch + FireOneStepAsync framework Pipeline entry guard + HandleAgentCompletedAsync Pipeline path 接管 callback）— spike 第一步必須精準分離「可搬走的 method body」vs「必留的 dispatch 結構」

---

## 結案必做清單

拆解 Stage 結案時，實作 Session 在 Roadmap 實作紀錄要寫下：

1. **實際產出檔案 + 行數**（vs 規劃預估）
2. **SOP 套用對照**（本次每一項 SOP 怎麼實踐）
3. **踩坑記錄**（特別記錄 SOP 中沒涵蓋的新發現，供未來擴充此份文件）
4. **驗收情境清單 + 零 follow-up commits 狀態**
5. **Context 消耗實測**（供 Aria 校準公式）
6. **Dangling reference 清理**（Stage 84 踩坑）：已砍 flag / feature 的 doc comment / XML doc / comment 字串 / dead test reflection target / 結案前 grep 一次清乾淨（Stage 84 `UseTalentSkillSeparation` flag 邏輯砍完 / WorkflowSettings.cs + Resolver 6 處 XML doc 殘留「必須 UseTalentSkillSeparation=true 才有意義」失準漏網）
7. **Warning baseline 比對**（Stage 84 踩坑）：`dotnet build` warning 總數 vs Stage 開始前 baseline / verify 0 新引入 warning（特別 CS9113 unused parameter — 拆解後 sub-service 注入但 body 未用是高頻 case）/ 結案 commit message warning 數必對齊實測（Stage 84 commit message 寫「0 warning」實際 net 多 2 個 / 需 patch follow-up）
8. **Test session cleanup**（Stage 85 踩坑 — Stage 80 self-verify 殘留 3 筆 paused PetraSession 卡 4-5 天才被 Christ Dashboard 揪 / 屬「閘門外漏網」第 4 次累積）：Stage 結案 self-verify 跑完，手動或自動清掉測試 PetraSession（pause / running / 任何試驗殘留）。
   - 手動清：`docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c "UPDATE petra_sessions SET \"Status\"='cancelled', \"UpdatedAt\"=NOW() WHERE \"Id\" IN (...)"`
   - 自動清：Stage 85 起 PetraSessionRecoveryService 已加 timeout 機制（paused > 24h 自動 cancel + Discord push）— 但 self-verify 跑完當下不該等 24h，手動清比較負責

---

## 不適合套用本 SOP 的情況

- **UI 元件層級拆解**（Razor 元件各自生命週期、state 管理不同）—— 有獨立規則
- **Entity / DTO 層級重構**（通常是補欄位、改命名，不是職責拆分）
- **跨 Bot / Dashboard 的跨層 refactor**（需要 Spike，本 SOP 不適用）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.4 | 2026-05-24 | Stage 85 結案升級（Aria 結案第二段 step 1 第 4 次實踐）— 結案必做清單第 8 條「Test session cleanup」（Stage 85 踩坑 — Stage 80 self-verify 殘留 3 筆 paused PetraSession 卡 4-5 天 / 對齊 Stage 85 PetraSessionRecoveryService timeout 機制升級紀律 / 屬「閘門外漏網」第 4 次累積）|
| v1.3 | 2026-05-24 | Stage 84 結案升級（Aria 結案第二段 step 1 第 3 次實踐）— ① SOP 5 加「state field 拆解 caller 對應 owning service verify」（Stage 84 `_roundRobinCounter` 4 caller 跨 3 service 拆 3 份 vs plan 寫 2 份 = 預先 grep 不足）② 結案必做清單第 6 條「Dangling reference 清理」（防 dead flag dangling doc comment 殘留）③ 結案必做清單第 7 條「Warning baseline 比對」（防 CS9113 unused parameter 新 warning 漏網 + commit message warning 數對齊實測紀律）④ 實戰數據加 Stage 84 row（主檔 2266 → 193 / 瘦身 91.5% / SOP 累積第 5 次 + single-session 完成 M+ 規模新里程碑）|
| v1.2 | 2026-05-22 | 加紀律「拆解完檔案大小不一定都變小」— 拆解時評估「搬去哪」是否也超閾值 / 避免創造新怪物（典型反例：Stage 36 CommandHandler 2172 → 瘦身 556 行 / 但搬去的 ButtonCallbackRouter 變 1091 行 = 沒真拆是搬家）|
| v1.1 | 2026-05-10 | Stage 59 結案升級（Aria 結案第二段 step 0 — 跨 Stage know-how 升級評估首次實踐）— ① SOP 2 加 caller 改動成本評估三層分（ctor / IServiceProvider field / scope.GetRequiredService） ② SOP 6 加子目錄 / namespace 名稱避免與既有 entity 同名（C# child namespace shadow 規則）③ 實戰數據加 Stage 59 row + dispatch / guard / 路由型主檔瘦身比例典型 -50%~-60% 觀察 + SOP 累積倍率持續下降（34-36 ×1.49-1.65 → 59 ×1.09）|
| v1.0 | 2026-04-22 | 初版，由 Stage 34-36（FF 二十合集）累積而成 |

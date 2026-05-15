# Future Feature — 未來功能候選清單

> 版本：v7.99
> 建立日期：2026-04-01
> 最後更新：2026-05-16
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。


---

## 外部檔索引

- **`Future_Feature_v5.5.md`** ⭐ — v5.5 升級規劃 reference（進行中戰略主軸）

---


## 當前優先級 Top 5（2026-05-16 v7.99 — Trial_v13 🟡 部分成功結案後 = v5.5 path 工程實證完整 + 揭關鍵紀律缺口已修 → 待 Stage 68 + Trial_v14 一起跑 → Phase 1 完整收口 → Phase 2 啟動）

| # | FF | 標題 | 狀態 | 為何優先 |
|---|---|---|---|---|
| 1 | **Stage 68 + Trial_v14（合併跑）** ⭐⭐⭐ | Trial_v13 揭紀律修法 `0226c60` 真實生效驗 + Stage 68（FF 六十一補強清單 8+ 點之一 / Phase 2 Step 3 DB 持久記憶 schema 候選）— Christ 2026-05-16 拍板「先記錄繼續實作 Stage 再一起試驗」攤平 Trial cost / 時間 | 🟡 Stage 68 範圍待定 | Trial_v13 揭 3 議題（reload-cache scope 紀律 / workspace cleanup 紀律 / Cody push to main 紀律 — 第 3 條已修法 `0226c60`）+ v5.5 path 工程實證完整（DecideTalentsAsync + 自管 chain talent=/skill= format + Vera inputMsgs=2 真做事 + Cody 6/6 cover + 順修 bug）+ blind spot 0% 維持。Trial_v14 沿用同 prompt + 預期 Cody 不自己 commit / Petra FinalizeGitAsync 真實開 PR / cost $1-5 |
| 2 | **v5.5 Phase 1 完整收口拍板** ⭐⭐⭐ | Trial_v14 ✅ 後 Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = v5.5 Phase 1 完成 | 🟡 待 Trial_v14 結案 | Phase 1 完整收口後正式進入 v5.5 Talent-Skill separation 架構（baseline 6 Talent + 6 Skill 落地 / DB-driven Talent registry / Petra dispatch Talent pool / round-robin schema 預備 horizontal scaling）|
| 3 | **v5.5 Phase 2 Step 3 DB 持久記憶 schema** | Per-task per-agent 持久記憶 hybrid（共用層 + 私有層）+ token budget 60-70% 主動 compact 紀律 | 🟡 待 Phase 1 完整收口 | v5.5 規劃 Phase 2 第一步 — 對齊業界 best practice 避 context drift 65% / 35 分鐘退化雷區 / 跟 Stage 67 Talent-Skill schema 整合 per-Talent 私有層 / 規模 M-L |
| 4 | **v5.5 Phase 2 Step 4-5** | Petra 拆解指令精準度 spike + Prompt DB 化 + Talent identity 整合 | 🟡 待 Step 3 結案 | v5.5 規劃 Phase 2 後續 — 業界沒明確 best practice 要 spike / Talent prompt DB 化解 prompt 改不用 redeploy + 跨 Talent 變體（Cody-1 嚴謹 vs Cody-2 創意）|
| 5 | **v5.5 Phase 3 Step 6-8** | WebUI Talent CRUD + 戰略決策層 3 agent debate + v4 既有 module 砍 vs 留 | 🟡 待 Phase 2 完整收口 | v5.5 規劃 Phase 3 — Phase 1+2 完整收口才開動態 CRUD（避業界 6% Copilot pilot 撐到 production 雷區）+ 戰略決策層多 agent debate 對齊兩層架構 + v4 既有 module 漸進評估砍 / 留 / 重寫 |

> ⚠️ **戰略主軸**：**Stage 67 結案 ✅ — v5.5 升級首發 Phase 1 Step 2 Talent-Skill separation 重構基底完成 / Trial_v13 是 v5.5 Phase 1 完整收口前最後一道閘門**。Stage 67 architectural 升級成果：Skill registry 6 Final Skill code-defined（砍 requirements_extraction 合進 Petra）+ Talent registry DB-driven baseline 6 預設 Talent（Cody 兼 3 skill / Vera/Quinn/Sage 主 1 skill / Victoria/Petra orchestrator）+ ITalentFactory pattern 解時序雷 + Phase 3 dynamic CRUD 副作用 + Petra dispatch 改 Talent pool round-robin schema 預備 horizontal scaling + 7 worker class 不 archive 守 v5 既有 fallback path + Feature flag UseTalentSkillSeparation default false 守 fallback。**Aria 紀律累積成熟度進化曲線**：計劃前 WebSearch 紀律連續 4 次（Stage 64-67）+ Gate1 Tier 升級紀律（架構級重構 Tier 0+1+2+Tier 3 #11 首次實踐）+ 自診 source of truth 紀律根因 14 次累積（Stage 67 一次揭 5 條 + gate1 揭 1 條延伸）+ Forge healthy 偏離 plan 第 N 次驗證（ITalentFactory + FinalizeGitAsync 簽名 + BuildPetraSystemPrompt 加段四段式）+ Aria 校準錨架構級重構新區間 ×0.58 連兩次穩定（Stage 66+67）。**對 v5.5 Phase 2 規劃的指引**：production-ready 補強區間 ×0.78-0.99（6 資料點）+ 架構級重構新區間 ×0.58（2 資料點）兩層分層成立 — Phase 2 Step 3 DB 持久記憶 schema 屬於架構級規模可預期 ×0.58 區間。**戰略終局**：Trial_v13 通過 → Christ 拍板切 default flag → v5.5 Phase 1 完成 → 進 Phase 2 Step 3 DB 持久記憶 schema 設計（Agent 像人類處理事件 — 接到任務後累積思考 / 中間別事打斷不會忘記 / 重啟接著做還記得來龍去脈精神實作）。詳見 [Stage_67_Roadmap.md](Stage_67_Roadmap.md) v2.0 + [CHANGELOG v3.57.0](../../CHANGELOG.md#3570) + [Future_Feature_v5.5.md](Future_Feature_v5.5.md)。

### 2026-05-11 close FF 補充紀錄（Stage 62 Charter spike v5 吸收）

| FF | 原議題 | close 不做原因 |
|---|---|---|
| **五十七** | Petra prompt 5 位置 SoT 維護紀律 | v5 prompt 重寫 — CLAUDE_Petra.md 全砍重寫 + 4 prompt builder method 全砍重寫對齊 Petra orchestrator（v5 動態架構勝過 SoT helper 抽 — 不再有 5 位置漂移風險）|
| **五十八** | 其他 3 申訴 path supersede 評估 | v5 動態架構 Petra Tool Set 動態 review/appeal — 不需固定 supersede helper（Pm/* 4 services 1415 LoC 吸收）|
| **五十九** | Trial 試驗框架 AI Team 認知錯位升級紀律 | v5 Petra prompt 重寫吸收 — CLAUDE_Petra.md 全砍重寫不再含「Stage 61 follow-up」字樣困惑（Trial_v8 揭露根因 = Petra prompt 累積層偏見根除）|
| **六十** | Stage 60 第 7 routing api_failure_retry/abort path silent 卡死 | v4 修法 ROI 負 — v5 動態架構 Petra orchestrator 捕捉 LLM API 失敗動態決策（不依賴固定 routing path）|

---

## Active 主清單

> 以下 5 個 FF 為「進行中 / 仍需做」狀態（v4 戰略主軸 FF 三十六 + 4 個觀察類）。

## 七、客戶專案交付流程與驗收閘門

### 背景

AiTeam 的定位不只是開發自身系統，未來也會替客戶開發專案。目前的流程（merge 後自動部署）對 AiTeam 自身足夠，但對客戶專案的風險層級完全不同：

| | AiTeam 自身開發 | 客戶專案開發 |
|---|---|---|
| **壞掉的代價** | 自己的工具壞了 | 客戶的系統壞了 |
| **git revert** | 可以接受 | 不可接受 |
| **merge 後再測** | OK | ❌ 太晚了 |
| **驗收責任** | 自己 | 對客戶負責 |

目前的流程走完產出一個 GitHub PR，但 merge 之前沒有 Preview 環境可以人工驗收，直接 merge 等於直接上客戶 Production。

### 期望行為

```
需求 → ... → PR 開出
                ↓
         Preview 環境自動部署（Staging）
                ↓
         Victoria 通知 Christ：「PR #42 已部署至 staging，請驗收」
                ↓
         Christ（或客戶）在 Staging 實際操作驗收
                ↓
         驗收通過 → Christ 回覆 OK → Merge → Production 自動部署
         驗收失敗 → Christ 回覆問題描述 → 流程重新進入修正循環
```

### 需要的兩個東西

1. **每個客戶專案都有一個 Staging 環境**（不一定在本機）
2. **AiTeam 流程加入正式的人工驗收閘門**（Victoria 等待 approve 才算 Done）

### 待釐清的子問題

1. **客戶專案的 Staging 環境由誰負責？** — 客戶自己有 staging server？還是 AiTeam 在本機幫每個專案起 container？
2. **AiTeam 是否應該管理「部署到客戶環境」？** — 目前 Maya（Ops）只針對 AiTeam 自身
3. **驗收失敗的循環怎麼設計？** — Christ 的修改意見要怎麼餵回 CEO → 再分派給對應 Agent？

### 初步討論結論（2026-04-12）

**部署到 IIS Web Deploy 的能力：**
- 建議走 **GitHub Actions + Web Deploy** 模式 — 客戶 repo 掛 workflow，push 時自動 `msdeploy` 部署到 IIS
- Agent 不需要直接操作部署，只負責 push code，CI/CD 負責部署（與 AiTeam 自身模式一致）
- 每個客戶專案設定一次 workflow 即可

**Git Flow 多環境部署：**
- 完全可行，需調整 WorkflowEngine 的分支策略
- Cody 的 PR 目標從 `main` 改為 `develop`
- GitHub Actions 依 branch 觸發不同部署目標：feature→開發環境 / develop→測試環境 / master→Production
- Victoria 需理解 Git Flow 各階段，知道「merge 到 develop ≠ 上線」
- 人工驗收閘門：feature 部署到開發環境後，等 approve 才 merge 到 develop

### 與現有 Future Feature 的關係

- **FF 三十八（跨專案能力研究）**：子議題 A（跨 Project repo 支援）是本 FF 前置條件
- **v4 路線**：與 FF 四十九 / 三十六 無關，獨立業務級需求

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

---

## 十二、Sage 全系統文件健康檢查

### 背景

Stage 23 流程重構中，Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」，pipeline 中的工作改為輕量的文件整理 + CHANGELOG 更新。

但長期而言，BugFix / TechImprovement 跳過 Doc 階段，加上程式碼持續演進，系統文件會逐漸與實際程式碼脫節。需要一個機制定期檢查並修補差異。

### 期望行為

Sage 作為獨立定期任務（非 pipeline 內），掃描整個專案：
- 比對程式碼與現有文件的差異（API 變更、新增模組、移除功能）
- 識別過時或缺漏的文件
- 自動補寫或更新，產出健康檢查報告

### 觸發方式

- 定期排程（例如每週 / 每月）
- 或手動指令觸發（Discord / Dashboard）

### 優先級

🔵 低優先級 — Phase 1 先觀察流程文件是否足夠，有實際過時問題再啟動

### v4 兼容性

與 framework / 架構無關，v4 落地後仍可獨立做。

---

## 三十六、AiTeam v5.5 動態架構 ⭐ 進行中

→ 詳見 [`Future_Feature_v5.5.md`](Future_Feature_v5.5.md)

---

## 五十四、Stage 36 後怪物大檔復發 — TaskGroupService / ButtonCallbackRouter / DevAgentService 拆解 ⭐

> 狀態：🟡 子項 1 ✅（Stage 59，2026-05-10）/ 子項 2/3 待 Trial_v7 後評估動工
> 提出日期：2026-05-10（Christ 觀察 Forge Plan context 漸漲根因之一）

### 背景

Stage 34-36 FF 二十系列完成「四怪物大檔拆解」（MeetingService / PmAgentService / TaskGroupService / CommandHandler 全清零，2026-04-22）後，v4 漸進遷移路線（Stage 49-58）一路加 routing / NotifyBoss / TryRoute / dispatch case → 三檔復發超過 `docs/conventions/refactor-sop.md` 警戒線 800-1200 行：

| 檔案 | 行數 | 狀態 |
|---|---|---|
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | **1591 行**（Stage 57 後）| ⚠️ 超 Stage 36 拆解前 baseline 1300+ |
| `src/AiTeam.Bot/Discord/ButtonCallbackRouter.cs` | **1091 行**（Stage 36 後新觀察）| ⚠️ 超警戒線 |
| `src/AiTeam.Bot/Agents/DevAgentService.cs` | **958 行**（Stage 36 後新觀察）| ⚠️ 接近警戒上界 |

### 對 Forge Plan context 影響（Christ 2026-05-10 觀察根因之一）

- 怪物大檔 full read 一次 ~20-32K token（每行 ~20 token）
- partial read 多段 ~10-20K（仍偏高）
- Stage 56→57→58 Forge Plan context 漸漲（140K → 227K → ~270K）核心原因之一

### 修法方向（治本）

對齊 Stage 34-36 FF 二十系列既有拆解 SOP（[refactor-sop.md](../conventions/refactor-sop.md)）：
- TaskGroupService 拆 NotifyBoss helpers / TryRoutePipeline helpers / ProcessBossResponseAsync dispatch / HandleEpicPartialPaused 等職責分檔
- ButtonCallbackRouter 拆 dispatch table / handler 分組
- DevAgentService 拆 Dev / Dev_fix / Dev_plan 等職責分檔

### 配套治標（已立刻生效）

`workflow_aria.md` 第三節 A 計劃書格式硬規則 v1.1（2026-05-10 加）：
- **第 5 條**：計劃書內不寫整段 code 範例（>10 行 code block 一律砍）
- **第 6 條**：大檔 reference 標精準 line + method 簽名（Forge 用 Read offset+limit partial read 而非 full read）

Stage 59+ 立刻生效，預期 Forge Plan context 下降 ~30-40%。

### 規模 / 風險

**規模**：L（三檔拆解 + 對齊 refactor-sop.md SOP + Mock regression 確認既有行為不變）
**風險**：低（拆解類重構 + Stage 34-36 既有 SOP 範本可複用）

### 優先級

🟡 中 — Trial_v7 重跑後評估動工（累積到痛點才拆比預先拆 ROI 高，對齊 Stage 36 拆解節奏）。**Stage 57/58 完成後痛點主要落在 TaskGroupService（1591 行繼續漲，Stage 58 加 NotifyBossAgentApiFailure + TryRoutePipelineAgentApiFailure + dispatch case 預估再 +50-70 行）— TaskGroupService 優先拆**。

### v4 兼容性

純 refactor，不動業務邏輯，不影響 v4 framework 行為。

---

## 六十一、v5 PoC → production-ready 補強清單 ⭐

→ 詳見 [`Future_Feature_v5.5.md`](Future_Feature_v5.5.md)

---

## 十、Dashboard UI 細節打磨（第四批）

> 狀態：低優先級 — UI 組織與使用便利性優化，待 Christ 確認完整清單後排入 Stage
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static 沒重設計 UI，仍需做）

### 修法方向

待 Christ 確認完整清單（Dashboard 系統設定 / 任務列表 / 流程追蹤 / Agent 設定 / 規則管理等頁面 UX 整體打磨清單）。

### 規模 / 風險

**規模**：M-L（依清單範圍）/ **風險**：低

### 優先級

低 — 業務上不阻塞，UX 優化性質

### v4 兼容性

純 Dashboard UI 改動，與 framework 無關。

---

## 二十二、Agent 命名一致性（守門 + 名稱映射）

> 狀態：中 — Agent 名稱混雜（Cody/Dev/Vera/Reviewer 等），需建統一守門 + 名稱映射
> 重新分類：2026-05-09（從 v4_eval 升 active — hierarchical static Worker pool 命名規則沒改變）

### 修法方向

建立 Agent 名稱映射常數表（`AgentNames.cs`）+ 跨層 (Discord channel / DB AssignedAgent / Workflow executor key) 守門檢查。

### 規模 / 風險

**規模**：S-M（建 const + 跨層守門）/ **風險**：低

### 優先級

中 — Christ 觀察 Dashboard 顯示混淆時優先處理

### v4 兼容性

純 const + 守門，與 framework 無關。

---

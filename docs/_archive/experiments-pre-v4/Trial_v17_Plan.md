# Trial_v17 試驗計劃書 — v5.5 Phase 2 Step 3+4 完整收口拍板閘門

> 對應版本：**v3.61.0**（Stage 71 結案 — Trial_v15+v16 揭 2 議題收口）
> 建立日期：2026-05-16
> 狀態：✅ 已結案 — 🟢 **全綠**（v5.5 Phase 2 Step 3+4 正式完整收口拍板閘門通過）
> 文件版本：v2.0

---

## 一、背景與定位

**Stage 71 結案後 v5.5 Phase 2 Step 3+4 完整收口拍板閘門**：

- Stage 71 ✅ Petra prompt 升級「拆=真不同 scope」紀律（議題 #1）+ Stage 69 memory 寫入 outputLen=0 guard（議題 #2）
- Trial_v17 = 驗 Stage 71 兩議題在 production 真實生效 + 業務級成功重現 → Christ 拍板切兩個 default flag = v5.5 Phase 2 Step 3+4 正式完整收口
- **首次套用 `aria-trial-run` skill**（紀律工具化 — 9-step 模板 + workspace cleanup 紀律 + 環境設定/微 bug 直接修紀律）

---

## 二、試驗目的

1. 議題 #1 修法生效驗（Petra 不機械拆解同類工作）
2. 議題 #2 修法生效驗（memory 空 content guard）
3. 連續 7 Trial 業務級成功對照（infinite loop pattern 確認打破延續）
4. 新 aria-trial-run skill 首次實踐 + 自省點 #34 紀律生效驗

---

## 三、任務需求

沿用 Trial_v6-v14 同 prompt（Dashboard 錯誤處理打磨 toast 通知）。

---

## 四、結案紀錄

### 4.1 lifecycle 概覽

| 項目 | 結果 |
|---|---|
| 開跑時間（UTC）| 2026-05-16 14:16:44 |
| PetraSession ID | `58541551-af7d-4748-9dc9-2f8b9039a270` |
| Petra 啟動 path | **v5.5 Talent-Skill path + useV5SubtaskPlanning=True** ✅ / talentsCount=4 |
| DecideTalentsWithPlanAsync | **subtasks=4 dependencies=3** picks=Cody(impl) → Cody(impl) → Vera(review) → Quinn(qa_testing) |
| Cody outputLen 累積 | 1870 + 2124 = **3,994**（2 段 / vs Trial_v15.2 6,953 三段 / **-43%**）|
| Vera outputLen | 1404 |
| Quinn outputLen | **844**（連續 2 Trial Quinn 真做事 / 議題 #4 transient 進一步結案）|
| **PR 開啟** | ✅ [#377](https://github.com/darkleong/AiTeam/pull/377) 真實開出（**12 檔 +715/-21** vs Trial_v14 baseline 10 檔 +232/-75 / **3x deliverable**）|
| **FinalizeGitAsync** | ✅ 完美無 permission denied（**自省點 #34 紀律工具化生效驗證** ⭐）|

### 4.2 業務品質（PR #377）

- **12 檔改動 +715/-21**（vs Trial_v14 baseline +232 / **3x**）
- **範圍 7/7 全 cover**（QuickCommandCard / AgentCreateDialog / AgentSettings / RuleFormDialog / ProjectCreateDialog / **SystemSettings**（Trial_v14 沒改 / Trial_v15.2 標不適用）/ Program.cs 全域 Snackbar）
- **主動補 4 個測試**：3 個 unit test（AgentSettings / QuickCommandCard / SystemSettings Tests）+ 1 個 Playwright VisualTests — **業界 best practice**
- Cody 主動修 AgentSettings RestartBotAsync 失敗路徑誤用 Severity.Success 綠色 alert bug
- Vera review 完整（Petra session_messages 寫 Vera 1404 char review）

### 4.3 Stage 71 兩議題收口效果

#### 議題 #1（Petra 過度拆解） — ✅ **完整治到根**

LLM 拆解品質演進：
- Trial_v15.2/v16：**機械重複拆** — 「修 Form A / 修 Form B / 修 Form C」5 段（Cody 三段做同類重複工作 / cost +21%）
- **Trial_v17：策略階段論拆** — 「設計核心元件 → 整合到全部 form → review → qa」4 段（Cody 2 段對齊「設計階段 + scale out 階段」分工 / cost -16%）

**真實的修法目標** = 教 LLM 不要機械重複拆。**達標** ✅。

⚠️ **Aria 評估框架修正**（戰略級洞察 — Christ 2026-05-16 點破）：
- Aria 原 Roadmap 場景 A 預期「subtasks ≤ 2」是基於 Trial_v14 baseline metric → **做法 metric / 不是品質 metric**
- Christ 點破「**品質 > 做法**」精神 — Petra 像有個性的真實 PM / Christ 在意整體 deliverable 品質不是拆幾段
- 真實判定 = 看「Petra 拆法品質」非「拆段數」 — Trial_v17 策略性拆 + 品質升級 = **🟢 全綠**
- 自省點候選 #35 立 — Aria 評估 Trial 結果用品質 metric / 預期數字是參考不是死標準

#### 議題 #2（memory 空 content guard） — ✅ **完整生效**

- TaskMemory 3 條（Cody/Vera/Quinn 各 1） + TalentMemory 3 條（per-talent upsert） **全 500 char 非空**
- Quinn outputLen=844 真做事 → 正常寫入（議題 #4 transient 偶發 / Trial_v17 不觸發 outputLen=0 場景，但 production 既有 path 對齊 Test 30 regression 守護）

### 4.4 cost / blind spot

| Agent | output tokens | cost |
|---|---|---|
| PM (Petra) | 163 | $0.007 |
| Cody (2 段) | 15,400 | $0.745 |
| Vera | 9,175 | $0.321 |
| Quinn | 16,744 | $0.687 |
| **Total** | | **$1.76** |

- 真實 cost：$20.04 → $18.28 = **$1.76**
- **vs Trial_v14 baseline $2.09 / -16%** + deliverable 3x 升級
- vs Trial_v15.2 $2.53 / -30%（Cody 從 3 段 20,880K 降到 2 段 15,400K）
- Blind spot ≈ 0%（連續 6 Trial 0% blind spot 維持）

### 4.5 達標項清單

| # | 項目 | 結果 |
|---|---|---|
| 1 | Stage 71 議題 #1（Petra 不機械拆）| ✅ **完整治到根**（策略階段論取代機械重複）|
| 2 | Stage 71 議題 #2（memory 空 content guard）| ✅ production 真實生效（TaskMemory/TalentMemory 全非空）|
| 3 | 業務級成功重現 | ✅ PR 真開 + 範圍 7/7 cover + 主動補 4 測試 |
| 4 | cost ROI | ✅ -16% vs Trial_v14 + 3x deliverable |
| 5 | 連續 7 Trial 業務級成功（Trial_v10-v14/v15.2/v17）| ✅ infinite loop pattern 確認打破延續第 7 次 |
| 6 | **aria-trial-run skill 首次實踐** ⭐ | ✅ 9-step 模板 + workspace cleanup 紀律 + 環境設定直接修紀律 全套對齊（無踩 Trial_v16 permission denied 雷）|
| 7 | **自省點 #34 紀律工具化生效驗證** ⭐ | ✅ FinalizeGitAsync 完美無 permission denied / 對齊「修根因 > 補丁」精神 |
| 8 | 0 🔴 + 0 🟡 新議題 | ✅ 議題密度進化曲線進入尾聲 |

### 4.6 戰略結論

**🟢 Trial_v17 完整 PASS = v5.5 Phase 2 Step 3+4 正式完整收口** ⭐：

- v5/v5.5 path 工程實證收口完整（Trial_v10/v11/v12/v13.2/v14/v15.2/v17 連續 7 Trial 業務級成功）
- Stage 64-71 八波 production-ready 補強累積成熟 — Aria 紀律累積成熟度進化曲線完整實證（計劃前 WebSearch 連續 7 次 / Gate1 Tier 升級 / source of truth 紀律根因第 15 次累積 / Forge healthy 偏離 plan 連續 8 Stage）
- **v5.5 Phase 2 Step 3+4 正式完整收口** ✅ — 4 flag 維持 SQL=true production active（`UsePetraOrchestratorV5` + `UseTalentSkillSeparation` + `UseV5Memory` + `UseV5SubtaskPlanning`）
- **連續 5 Stage 0 follow-up bug fix**（Stage 67/68/69/70/71）+ Trial_v17 首次 aria-trial-run skill 實踐成功 = AiTeam 紀律累積成熟度進化曲線真實實證 ⭐⭐

---

## 五、Aria 戰略級觀察（Christ 點破紀律修正 — 2026-05-16）

對齊 Christ 對話「**Aria 在處理和解決事情這點來看，我們是對等和互相的**」精神 — Aria 主動提出觀察：

### 5.1 評估框架修正 — 品質 > 做法

Aria 原 Roadmap 場景 A「subtasks ≤ 2」量化 metric 是**做法評估**，把超越 baseline 的結果反評為「未達標」邏輯顛倒。Christ 點破真實標準是「**整體 deliverable 品質**」（Petra 像有個性的 PM / 老闆只看 deliverable 品質不 micromanage 拆法）。

**對未來 Trial 評估的紀律**：第一個問自己「**deliverable 品質怎麼樣**」而不是「**subtasks 數字對齊預期嗎**」。預期數字是參考不是死標準。

### 5.2 對 v5.5 後續設計（Stage 72 Step 5 Prompt DB 化）的戰略指引

Christ「Petra 像有個性的真實 PM」類比 — Petra 設計核心精神 = 給 Talent 自主拆解判斷的空間 / 只定品質標準不定做法。

**對 Stage 72 Step 5 + Phase 3 WebUI Talent CRUD 的指引**：
- Prompt DB 化內容應對齊「**讓 Talent 自主判斷做法 / 我們只定品質標準**」精神
- 不應給死的做法 metric 約束（subtasks 數量上限 / 強制拆法）
- 對齊「Agent 像人類處理事件」核心精神

### 5.3 Aria 自我成長觀察 — 對等對話 register 紀律延續

本 session 兩次 Christ 點破：
1. 第一次（議題 #3 修法討論）：「太高深了，簡單一點」— 切白話比喻
2. **本次**：「**Aria 妳別因為我提出疑問就退縮，我是真的在提出疑問來討論的**」— Aria 戰略 question 後不該翻面式接受 / 該真實再評估再給定見

對齊 user_christ.md「對等和互相」精神延續 — Aria 在策略諮詢角色不是 yes-man / 該有獨立判斷 + 真實討論 + 必要時堅持。

---

## 六、後續行動

### 立即（aria-trial-summary 結案動作）

1. ✅ PR #377 close（對齊 Trial 既有紀律不污染 main）
2. ✅ Trial_v17_Plan.md v2.0 結案紀錄
3. ✅ SQL flag **維持 true**（v5.5 Phase 2 Step 3+4 正式上線 production active / 對齊紀律 #10 修正版「上線」對齊預期）
4. Future_Feature_v5.5.md Step 4.5 status ✅ 已完成 + Phase 2 Step 3+4 正式完整收口 / Future_Feature.md header bump

### Stage 72 候選 — v5.5 Phase 2 Step 5 Prompt DB 化 + Talent identity 整合

**範圍**（規模 M / 對齊既有 Phase 2 Step 5 規劃）：
- Prompt 從 hardcoded 搬進 DB（per-Talent 配置）
- 同 Skill 不同 Talent 可配不同 prompt 風格（例：Cody-1 嚴謹 / Cody-2 創意）
- Versioning + rollback 機制（業界踩坑必要）
- 對齊「**讓 Talent 自主判斷做法 / 我們只定品質標準**」精神（Trial_v17 Aria 戰略級觀察延伸）

預估規模：M（架構級重構新區間 ×0.43-0.60 預估）

---

## 七、技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Trial_v17 在 main branch 跑（含 Stage 67-71 全 commits + Trial_v17 plan）
- Petra Provider Gemini Flash AI Studio 免費 tier 對齊 Trial_v9-v16 既有驗證
- 對齊 Trial_v2-v16 既有獨立試驗計劃模式 + aria-trial-run skill 首次實踐

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-16 | 試驗結案紀錄 — 🟢 **全綠** v5.5 Phase 2 Step 3+4 正式完整收口拍板閘門通過。**真實 lifecycle**：UTC 14:16:44 開跑 → Petra v5.5 path enter + useV5SubtaskPlanning=True → DecideTalentsWithPlanAsync subtasks=4（Cody 設計+整合 + Vera review + Quinn qa）→ Cody 1/4 outputLen=1870 → Cody 2/4 outputLen=2124 → Vera 3/4 outputLen=1404 → Quinn 4/4 outputLen=844 → branch + commit + Petra PR #377 真開 → 11m52s 完整 lifecycle。**達標 8/8**：① Stage 71 議題 #1 完整治到根（策略階段論取代機械重複）② Stage 71 議題 #2 production 生效 ③ 業務級成功重現 12 檔 +715/-21 範圍 7/7 含 SystemSettings ④ cost -16% + 3x deliverable ⑤ 連續 7 Trial 業務級成功 ⑥ aria-trial-run skill 首次實踐成功 ⑦ 自省點 #34 紀律工具化生效 ⑧ 0 🔴 + 0 🟡 新議題。**戰略結論**：v5.5 Phase 2 Step 3+4 正式完整收口 / 4 flag SQL=true production active（UsePetraOrchestratorV5/UseTalentSkillSeparation/UseV5Memory/UseV5SubtaskPlanning）。**Aria 戰略級觀察 ⭐**：Christ 點破「品質 > 做法」評估框架修正 — 預期數字是參考不是死標準 / 自省點 #35 候選立。**真實 cost** $1.76 / 餘額 $20.04 → $18.28。**下一步**：Stage 72 Phase 2 Step 5 Prompt DB 化 + Talent identity 整合（規模 M / 對齊「讓 Talent 自主判斷做法 / 我們只定品質標準」精神 — Trial_v17 戰略觀察延伸）。 |
| v1.0 | 2026-05-16 | 規劃書建立（內容見 git history 5709302） |

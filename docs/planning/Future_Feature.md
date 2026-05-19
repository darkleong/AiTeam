# Future Feature — 未來功能候選清單

> 版本：v9.8
> 建立日期：2026-04-01
> 最後更新：2026-05-19（**Stage 78b ✅ v5.5 Phase 4 候選 C 後續 — v4 path dead caller 整套砍除** v3.69.0 / 純 refactor 6 子項 / Christ 拍板路線 C 折衷 / 8 檔變動 net -787 行 / build warning 102→59 (-42%) / xUnit 104+127 全綠 / Forge 1 輪 spike + Plan v2 升級 + 實作 0 follow-up + 自驗 0 issue / Aria gate1 Tier 0+1 通過 / 連續 13 Stage 0 follow-up + clean delivery 連續第五次 ⭐⭐⭐⭐⭐ / 校準錨真實 394K → ratio ×3.03-4.93 突破 Stage 78a 上界 / 大規模架構級重構新區間 ×1.57-4.93 新上界 7 資料點累積 + 自省點 #37 第 7 次累積實證 / Stage 78c 預留 v4 Pipeline 整套砍 + **Stage 79 v5.5 image flow 補完新揭 gap** — Christ 2026-05-19 戰略 question 揭 Dashboard 附圖 Petra 看不到 + WebSearch 業界紀律拍板 Petra SubtaskPlan needsImageContext flag 條件性 worker propagation）
> 說明：本文件收錄尚未排入正式 Stage、值得未來評估的功能方向與研究項目。

---

## 外部檔索引

- **[`Future_Feature_v5.5.md`](Future_Feature_v5.5.md)** ⭐ — v5.5 升級規劃 reference（進行中戰略主軸）

---

## Active 清單

| # | 標題 | 狀態 |
|---|---|---|
| 一 | v5.5 動態架構 ⭐ | 🟡 進行中（戰略主軸 — 詳見 v5.5.md） |
| 二 | v5 PoC → production-ready 補強清單 | 🟡 Stage 68+ 評估（詳見 v5.5.md） |
| 三 | 客戶專案交付流程與驗收閘門 | 🟡 中優先級（AiTeam 開始承接客戶專案時前置必要） |

---

## 一、v5.5 動態架構 ⭐ 進行中

→ 詳見 [`Future_Feature_v5.5.md`](Future_Feature_v5.5.md)

---

## 二、v5 PoC → production-ready 補強清單 ⭐

→ 詳見 [`Future_Feature_v5.5.md`](Future_Feature_v5.5.md)

---

## 三、客戶專案交付流程與驗收閘門

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

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時為前置必要條件，目前仍以自身開發為主

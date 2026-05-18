# Future Feature — 未來功能候選清單

> 版本：v9.7
> 建立日期：2026-04-01
> 最後更新：2026-05-18（**Stage 78a ✅ v5.5 Phase 4 C — v4 path dead code 整套砍除** v3.68.0 / Forge spike v1+v2+v3 連續三輪揭真實 + Aria 計劃前 grep 紀律補強候選 3 條累積第 N+1~3 次延伸 / 22+4 檔變動 net -3957 行 / Bot.Tests 113→104 + Generated 127/127 全綠 / Aria gate1 🟡 修補 DefaultSkillRegistry + Test 10/14 + Stage74 T7 + DbSeeder + CLAUDE.md production path 修根因 / Stage 78b 預留 ButtonCallbackRouter v4 routing 砍 + 配套 / 連續 12 Stage 0 follow-up + clean delivery 連續第四次 ⭐⭐⭐⭐ / Trial_v22 🟢 全綠 — Stage 75+76+77 整套 5 訊號全綠 + **per-Talent lock contention 真實 fire 量化首次實證** ⭐⭐⭐（Cody slot 連續 3 接力等鎖 4m18s + 8m31.5s / Vera slot 連續 3 接力 / 不同 Talent 真實平行 12:30:10 同秒 Cody+Vera）/ 業務評分 5/5 滿分 + 連續 12 Trial 業務級成功 / Total cycle 並行 -50% 時間縮短首次實證（3 task 並行 20 min vs sequential 推算 40 min）/ cost per file $0.041 新最優 ROI baseline / **v5.5 Phase 3 完整收口** ⭐⭐⭐⭐⭐ — Stage 73+74+75+76+77 5 Stage 連續實證完成）
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

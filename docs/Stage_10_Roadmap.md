# Stage 10：開發流程自動閉環

> 版本：v1.0
> 建立日期：2026-04-03
> 狀態：✅ 實作完成（2026-04-03）

---

## 目標

讓一個新功能從老闆說話，到 PR merge 通知老闆，中間所有的推進都是自動的。

老闆只需要做兩件事：
1. **核准提案書**（確認需求 + UI 規格）
2. **最後 merge PR**（Vera 確認無 🔴 後 CEO 通知）

---

## 一、CEO Orchestrator（基礎建設）

> 其他三項都依賴這項完成，**優先實作**。

### 背景

目前 CEO 是「任務路由器」——你說什麼它派什麼，但任務完成後就停下來，不知道下一步是什麼。

要讓流程自動閉環，CEO 必須升級為「任務生命週期全程指揮官」。

### 需要實作的能力

**1. 任務狀態新增 `waiting_input`**

```
現在：pending → running → done / failed
未來：pending → running → waiting_input → running → done / failed
```

**2. Agent 可以「暫停並回報問題」**

Agent 新增回傳格式：
```json
{
  "success": false,
  "waiting_input": true,
  "question": "Rosa Issue #3 的驗收條件不明確，請問篩選條件最多幾個？",
  "question_type": "requirement | ui_spec | business_decision"
}
```

**3. CEO 按問題類型路由**

| 問題類型 | CEO 路由到 |
|---------|-----------|
| `requirement` | Rosa 補充 Issue |
| `ui_spec` | Demi 更新規格文件 |
| `business_decision` | 升級給老闆 |

**4. 任務完成後 CEO 自動觸發下一步**

CEO 維護一份「標準流程表」，當任務完成時對照流程表決定下一步：

```
新功能流程：
  提案核准 → Dev（附帶 Issues + UI 規格）
  Dev PR 開出 → QA + Doc + Vera 並行觸發
  Vera 無 🔴 → 通知老闆可以 merge

Bug 修復流程：
  Dev PR 開出 → Vera 觸發
  Vera 無 🔴 → 通知老闆可以 merge
```

**5. CEO 路由輕量化**

Agent 回報「完成」時，CEO 不呼叫 LLM，直接查流程表決定下一步，走快速路徑。

---

## 二、提案書增強（確認機制升級）

### 背景

Stage 9 的提案模式只有「核准 / 取消」兩個選項，且 Discord Embed 有字數限制，看不到完整內容。

### 增強一：✏️ 第三個按鈕「請修改後重提」

```
提案書 Embed
  ✅ 核准，開始開發
  ✏️ 需要調整
  ❌ 取消
```

按下 ✏️ 後：
```
CEO 問：「請說明要調整的方向（Rosa 的需求 / Demi 的 UI 規格）」
    ↓
老闆回答：「UI 規格的表格欄位要加日期範圍篩選，其他沒問題」
    ↓
CEO 帶著意見重新指派 Demi 修改
    ↓
Demi 更新規格 → CEO 重新發出提案書
```

### 增強二：提案書附上完整文件連結

Embed 除了摘要，額外附上：
- 📋 GitHub Issues 連結（每個 Issue 一個連結）
- 🎨 UI 規格文件連結（`docs/ui-specs/xxx.md` 在 GitHub 上的連結）

老闆可以點進去看完整內容，再決定是否核准。

---

## 三、開發上下文補強

### 背景

目前 Dev（Cody）收到任務時只有標題 + 描述，看不到 codebase 結構、Rosa 的 Issues、Demi 的規格，導致 `files_to_modify` 靠猜，高機率猜錯。

### 實作方向

**1. Dev 制定計畫前先掃描 repo 結構**

呼叫 GitHub Tree API 取得目錄結構快照（不需要 clone），提供給 LLM 作為制定計畫的參考：

```
GitHubService.GetRepositoryTreeAsync(owner, repo, recursive: false)
→ 回傳兩層目錄結構
→ 注入 BuildPlanUserMessage 的上下文
```

**2. CEO 派任務給 Dev 時自動附帶上游產出**

CEO 觸發 Dev 時，從 DB 查同一批任務的相關記錄，附帶：
- Rosa 建立的 GitHub Issues 編號與標題清單
- Demi 的 UI 規格文件路徑（`docs/ui-specs/xxx.md`）

Dev 制定計畫時可直接讀取這些文件作為依據。

---

## 四、Review 閉環

### 背景

目前 Vera 審查完就停了，有 🔴 問題只會在 GitHub 留評論，不會主動通知 Dev，也無法自動重審。

### 實作方向

**Vera 審查完成後：**

```
Vera 審查完成，發現 🔴 → 向 CEO 回報：「PR #X 有 N 個必修問題」
    ↓
CEO（透過 CEO Orchestrator）通知 Dev：「Vera 要求修正，修完告知我」
    ↓
Dev 修好，推新 commit 到同一 branch
    ↓
CEO 偵測到 PR 有新 push → 自動重派 Vera
    ↓
Vera 確認無 🔴 → 回報 CEO
    ↓
CEO 通知老闆：「PR #X 已通過審查，可以 merge 了」
```

**如果 Vera 審查通過（無 🔴）：** 直接通知老闆，不需要繞一圈。

---

## 五、Ops Rollback 機制

### 背景

目前部署失敗只能通知老闆手動處理，Bot 容器內無法執行 docker-compose。

### 實作方向

**在 self-hosted runner 新增 `rollback.yml` workflow：**

```yaml
# .github/workflows/rollback.yml
on:
  workflow_dispatch:
    inputs:
      target_tag:
        description: 'Rollback 目標版本（如 v1.1.0）'
        required: true
```

**Maya 透過 GitHub API 觸發它：**

```
部署失敗，Maya 判斷為程式問題（非外部故障）
    ↓
Maya 呼叫 GitHub Actions API 觸發 rollback.yml
    ↓
Self-hosted Runner 執行 docker compose pull + up（指定舊版 tag）
    ↓
Maya 向 CEO 回報：「v1.2.0 部署失敗，已回滾到 v1.1.0」
    ↓
CEO 通知老闆 + 通知 Dev 請修復
```

---

## 完整新功能流程（Stage 10 完成後）

```
你說：「我要做 Token 監控的匯出功能」
    ↓
CEO 分類：新功能 → 提案模式
Rosa + Demi 並行產出
    ↓
CEO 發出提案書 Embed
  📋 GitHub Issues #12, #13 連結
  🎨 UI 規格文件連結
  [✅ 核准] [✏️ 需要調整] [❌ 取消]
    ↓
你審閱完整文件，回來按 ✅
    ↓
CEO 自動派 Dev（附帶 Issues + 規格路徑 + repo 結構）
    ↓
Dev 開發 → Push → 開 PR
    ↓
CEO 自動觸發：QA + Doc + Vera 並行
    ↓
Vera 有 🔴 → CEO 通知 Dev 修 → Dev 推 commit → CEO 自動重派 Vera
Vera 無 🔴 → CEO 通知你：「PR #X 可以 merge 了」
    ↓
你 merge

你做的事：說需求 → 核准提案書 → merge PR
```

---

## 實作順序建議

| 順序 | 項目 | 原因 |
|------|------|------|
| 1 | CEO Orchestrator | 其他三項的基礎，必須先完成 |
| 2 | 提案書增強（✏️ + 連結）| 依賴 CEO Orchestrator 的任務狀態機制 |
| 3 | 開發上下文補強 | 相對獨立，CEO Orchestrator 完成後可同步進行 |
| 4 | Review 閉環 | 依賴 CEO Orchestrator 的自動觸發機制 |
| 5 | Ops Rollback | 最獨立，可最後實作 |

---

## 驗收標準

| 項目 | 標準 |
|------|------|
| CEO Orchestrator | 新功能完整跑一次：說需求 → 提案 → Dev → Vera → 通知 merge，全程無需手動推進 |
| 提案書增強 | 按 ✏️ 提出修改意見，CEO 重新提案；點 GitHub 連結能看到完整 Issues 和 UI 規格 |
| 開發上下文 | Dev 制定計畫時能看到 repo 結構，計畫中的 `files_to_modify` 準確率明顯提升 |
| Review 閉環 | Vera 有 🔴 → Dev 自動收到通知 → 修完後 Vera 自動重審 → 無 🔴 後老闆收到通知 |
| Ops Rollback | 模擬部署失敗，Maya 自動觸發 rollback workflow，成功回滾到上一版本 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-03 | 初版建立（五大項目：CEO Orchestrator、提案書增強、開發上下文、Review 閉環、Ops Rollback）|

# Stage 3 — 第一批 Agent 上線

> 所屬專案：AI 團隊實作總規劃  
> 狀態：⏳ 待規劃  
> 最後更新：2026-03-29

---

## 目標

在 Stage 2 的地基上，讓 Dev Agent 與 Ops Agent 正式運作，接上事件自動觸發機制。

---

## 交付項目

- [ ] Dev Agent 實作（GitHub API 整合、程式碼操作）
- [ ] Ops Agent 實作（CI/CD 串接、部署監控）
- [ ] GitHub Webhook 接收與處理
- [ ] 事件自動觸發機制（PR 開啟、Issue 建立、Merge 事件）
- [ ] 多專案支援（CEO 自動建立 Discord 頻道與 Notion 頁面）

---

## 已確認細節

| 項目 | 決定 |
|------|------|
| CI/CD 工具 | GitHub Actions |
| 部署流程 | 直接部署到 production |
| Merge 決策 | 永遠由你手動執行，Agent 不介入 |
| 部署觸發 | PR Merge 到 main 後自動觸發 |

---

## 完整工作流程

```
Issue 建立（GitHub）
    ↓ 自動觸發
CEO Agent 分析、分派給 Dev
    ↓ 你確認
Dev Agent 修改程式碼、commit、開 PR
    ↓ 自動觸發
（未來）測試 Agent 執行測試、產出報告
    ↓
Discord 通知你：PR #xx 已就緒，附測試報告，請審查
    ↓ 你審查程式碼，按下 Merge  ← 你唯一需要做的事
PR Merge 到 main
    ↓ GitHub Actions 自動觸發
編譯 → 測試 → 部署到 production
    ↓ Ops Agent 監控部署結果
成功：Discord 通知你，寫入 Notion + PostgreSQL
失敗：自動回滾 + Discord + Email 通知你
```

---

## GitHub 事件觸發對應

| 事件 | 觸發對象 | 說明 |
|------|---------|------|
| Issue 建立 | CEO Agent | 分析需求，分派給 Dev |
| PR 開啟 | 測試 Agent（未來）/ 通知你 | 現在直接通知你審查 |
| PR Merge 到 main | GitHub Actions + Ops Agent | 自動部署，Ops 監控結果 |

---

## CI/CD 設計（GitHub Actions）

部署設定檔放在 repo 內，由 Dev Agent 產出，你審查後 Merge 生效：

```
.github/workflows/deploy.yml
  → PR Merge 到 main 時觸發
  → 編譯（dotnet build）
  → 測試（dotnet test）
  → 部署到本地伺服器
```

**Ops Agent 的角色：** 不取代 GitHub Actions，而是在部署完成後監控結果，處理成功通知與失敗回滾邏輯。

---

## GitHub Webhook 安全性驗證

採用 GitHub 官方標準：**Secret Token 簽章驗證**

```
GitHub 發送 Webhook 請求
    ↓
附上 X-Hub-Signature-256 標頭（用 Secret 產生的簽章）
    ↓
伺服器驗證簽章是否正確
    ↓
正確 → 處理事件
錯誤 → 拒絕，不處理
```

Secret Token 存放在 `appsettings.json`，不寫進程式碼。

---

## 多專案 Context 切換

**從 Webhook 的 repo 資訊自動判斷，不需要手動指定：**

```
Issue 建立（ProjectA 的 repo）
    ↓
Webhook 帶著 repo 資訊進來
    ↓
系統自動對應到 ProjectA
    ↓
CEO 分派任務時，任務已綁定 ProjectA 的 context
    ↓
Dev Agent 收到任務，直接知道是哪個專案
```

你在 Discord 主動下指令時，用 `/task project:ProjectA` 明確指定專案。

---

## 本地伺服器部署方式

採用 **GitHub Actions Self-hosted Runner**：

```
PR Merge 到 main
    ↓
GitHub Actions 觸發
    ↓
指令發給本地伺服器上的 Runner
    ↓
Runner 在本地直接執行部署腳本
```

優點：Runner 主動連出去，不需要開放任何對外 port，搭配 Tailscale 更安全。

實際安裝與設定交由 Claude Code 協助執行。

---

## 待討論事項

- [ ] 測試 Agent 的細節（Stage 5 展開時討論）

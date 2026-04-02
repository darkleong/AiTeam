# AI 團隊實作總規劃

> 版本：v2.0
> 建立日期：2026-03-29
> 狀態：進行中

---

## 文件索引

| 文件 | 說明 | 狀態 |
|------|------|------|
| [01_Vision_and_Architecture.md](./01_Vision_and_Architecture.md) | 願景、核心設計原則、整體架構、Agent 定義 | ✅ 已確認 |
| [02_Infrastructure.md](./02_Infrastructure.md) | Discord 頻道、資料儲存、已確認細節 | ✅ 已確認 |
| [Stage_1_Design.md](./Stage_1_Design.md) | Stage 1：設計與決策 | ✅ 完成 |
| [Stage_2_Foundation.md](./Stage_2_Foundation.md) | Stage 2：基礎建設 | ✅ 已完成（2026-03-31） |
| [Stage_3_Agents.md](./Stage_3_Agents.md) | Stage 3：第一批 Agent 上線 | ✅ 已完成（2026-03-31） |
| [Stage_4_Dashboard.md](./Stage_4_Dashboard.md) | Stage 4：Blazor Dashboard | ✅ 已完成（2026-03-31） |
| [Stage_5_Expansion.md](./Stage_5_Expansion.md) | Stage 5：擴充更多 Agent | ✅ 已完成（2026-04-01） |
| [Stage_6_Roadmap.md](./Stage_6_Roadmap.md) | Stage 6：強化、驗收與技術債清償 | ✅ 已完成（2026-04-01） |
| [Stage_7_Roadmap.md](./Stage_7_Roadmap.md) | Stage 7：Software Team 完全體（三個新 Agent + CI/CD + Discord 重設計） | ✅ 已完成（2026-04-02） |
| [Stage_8_Roadmap.md](./Stage_8_Roadmap.md) | Stage 8：系統可靠性與操作體驗 | ✅ 已完成（2026-04-02） |
| [Stage_9_Roadmap.md](./Stage_9_Roadmap.md) | Stage 9：CEO 升級 + 可觀測性 | 🔵 規劃中 |
| [Future_Feature.md](./Future_Feature.md) | 未來功能候選清單（不限 Stage） | 🔵 持續維護 |
| [agents/software team/Agent_Capability_Gaps.md](./agents/software%20team/Agent_Capability_Gaps.md) | 各 Agent 能力缺口清單（內部協作基礎建設用） | 🔵 持續維護 |

---

## 變更紀錄

| 版本 | 日期 | 變更內容 |
|------|------|----------|
| v1.0 | 2026-03-29 | 初版建立，文件拆分為獨立 Stage 檔案 |
| v1.1 | 2026-03-29 | 更新各 Stage 狀態、修正 Ops 部署描述、補充 Token 監控細節 |
| v1.2 | 2026-03-31 | Stage 2 & Stage 3 實作完成，補充實作重點紀錄 |
| v1.3 | 2026-03-31 | Stage 4 實作完成，補充 Blazor Web App、Identity、SignalR、Aspire 陷阱紀錄 |
| v1.4 | 2026-04-01 | Stage 5 實作完成，動態 Agent 框架 + QA / Doc / Requirements 三個新 Agent |
| v1.5 | 2026-04-01 | Future_Research.md 升格為 Stage_6_Roadmap.md，納入正式規劃序列 |
| v1.6 | 2026-04-01 | Stage 6 結案（Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項）；新增 Stage_7_Roadmap.md |
| v1.7 | 2026-04-02 | Stage 7 結案（Reviewer/Release/Designer Agent、CI/CD、Discord 重設計、自然語言對話）；新增 Future_Feature.md |
| v1.8 | 2026-04-02 | 新增 Stage_8_Roadmap.md（8 項：可靠性補完 + Notion 遷移 + 專案管理 + 部署紀錄）|
| v1.9 | 2026-04-02 | Stage 8 全部 8 項完成：動態 AppSettings、per-agent Rules、Dark Mode CSS 覆寫、Notion 完全移除、OpsAgent 移除 docker CLI 依賴 |
| v2.0 | 2026-04-03 | 新增 Stage_9_Roadmap.md（CEO 智慧分類 + 提案模式、Token 監控 Dashboard、QA Playwright）；Future_Feature.md 清理已完成項目 |

---

*本文件為動態維護文件，隨規劃討論持續更新。*

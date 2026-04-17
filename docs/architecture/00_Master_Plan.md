# AI 團隊實作總規劃

> 版本：v7.2
> 建立日期：2026-03-29
> 狀態：進行中

---

## 文件索引

| 文件 | 說明 | 版本 | 狀態 |
|------|------|------|------|
| [01_Vision_and_Architecture.md](./01_Vision_and_Architecture.md) | 願景、核心設計原則、整體架構、Agent 定義 | — | ✅ 已確認 |
| [02_Infrastructure.md](./02_Infrastructure.md) | Discord 頻道、資料儲存、已確認細節 | — | ✅ 已確認 |
| [Stage_1_Design.md](../planning/Stage_1_Design.md) | Stage 1：設計與決策 | v0.1.0 | ✅ 完成 |
| [Stage_2_Foundation.md](../planning/Stage_2_Foundation.md) | Stage 2：基礎建設 | v0.1.0 | ✅ 已完成（2026-03-31） |
| [Stage_3_Agents.md](../planning/Stage_3_Agents.md) | Stage 3：第一批 Agent 上線 | v0.2.0 | ✅ 已完成（2026-03-31） |
| [Stage_4_Dashboard.md](../planning/Stage_4_Dashboard.md) | Stage 4：Blazor Dashboard | v0.3.0 | ✅ 已完成（2026-03-31） |
| [Stage_5_Expansion.md](../planning/Stage_5_Expansion.md) | Stage 5：擴充更多 Agent | v0.4.0 | ✅ 已完成（2026-04-01） |
| [Stage_6_Roadmap.md](../planning/Stage_6_Roadmap.md) | Stage 6：強化、驗收與技術債清償 | v1.0.0 | ✅ 已完成（2026-04-01） |
| [Stage_7_Roadmap.md](../planning/Stage_7_Roadmap.md) | Stage 7：Software Team 完全體（三個新 Agent + CI/CD + Discord 重設計） | v1.1.0 | ✅ 已完成（2026-04-02） |
| [Stage_8_Roadmap.md](../planning/Stage_8_Roadmap.md) | Stage 8：系統可靠性與操作體驗 | v1.2.0 | ✅ 已完成（2026-04-02） |
| [Stage_9_Roadmap.md](../planning/Stage_9_Roadmap.md) | Stage 9：CEO 升級 + 可觀測性 | v1.3.0 | ✅ 已完成（2026-04-03） |
| [Stage_10_Roadmap.md](../planning/Stage_10_Roadmap.md) | Stage 10：開發流程自動閉環 | v1.4.0 | ✅ 已完成（2026-04-03） |
| [Stage_11_Roadmap.md](../planning/Stage_11_Roadmap.md) | Stage 11：Dev Agent 驅動 Claude Code | v2.0.0 | ✅ 已完成（2026-04-05） |
| [Stage_12_Roadmap.md](../planning/Stage_12_Roadmap.md) | Stage 12：提案流程全面升級 | v2.1.0 | ✅ 已完成（2026-04-06） |
| [Stage_13_Roadmap.md](../planning/Stage_13_Roadmap.md) | Stage 13：系統穩定性與流程修正 | v2.2.0 | ✅ 已完成（2026-04-06） |
| [Stage_14_Roadmap.md](../planning/Stage_14_Roadmap.md) | Stage 14：CEO 分類與流程完整性補強 | v2.3.0 | ✅ 已完成（2026-04-06） |
| [Stage_15_Roadmap.md](../planning/Stage_15_Roadmap.md) | Stage 15：Victoria 接上 Claude Code + Session 對話 + 長期記憶 | v2.4.0 | ✅ 已完成（2026-04-06） |
| [Stage_16_Roadmap.md](../planning/Stage_16_Roadmap.md) | Stage 16：PM Agent（Petra）品質審核閘門 | v3.0.0 | ✅ 已完成（2026-04-07） |
| [Stage_17_Roadmap.md](../planning/Stage_17_Roadmap.md) | Stage 17：Mock Mode（模擬模式） | v3.1.0 | ✅ 已完成（2026-04-08） |
| [Stage_18_Roadmap.md](../planning/Stage_18_Roadmap.md) | Stage 18：Dashboard 可觀測性升級 | v3.2.0 | ✅ 已完成（2026-04-09） |
| [Stage_19_Roadmap.md](../planning/Stage_19_Roadmap.md) | Stage 19：Dashboard UI 全面打磨 | v3.3.0 | ✅ 已完成（Pt.1 2026-04-10、Pt.2/Pt.3 2026-04-11） |
| [Stage_20_Roadmap.md](../planning/Stage_20_Roadmap.md) | Stage 20：Dashboard 全面換 MudBlazor Layout | v3.4.0 | ✅ 已完成（2026-04-11） |
| [Stage_21_Roadmap.md](../planning/Stage_21_Roadmap.md) | Stage 21：文件整理與 SemVer 導入 | v3.5.0 | ✅ 已完成（2026-04-11） |
| [Stage_22_Roadmap.md](../planning/Stage_22_Roadmap.md) | Stage 22：Dashboard 存取分層 + Token 保護 + 頻道清理 | v3.6.0 | ✅ 已完成（2026-04-12） |
| [Stage_23_Roadmap.md](../planning/Stage_23_Roadmap.md) | Stage 23：開發流程重構 Phase 1a（Review Appeal + 流程產出強化） | v3.7.0 | ✅ 已完成（2026-04-12） |
| [Stage_24_Roadmap.md](../planning/Stage_24_Roadmap.md) | Stage 24：開發流程重構 Phase 1b（QA 改造 + Dev_plan 審核強化 + 文件基礎設施） | v3.8.0 | ✅ 已完成（2026-04-13） |
| [Stage_25a_Roadmap.md](../planning/Stage_25a_Roadmap.md) | Stage 25a：開發流程重構 Phase 1c（Kick-off 會議機制） | v3.9.0 | ✅ 已完成（2026-04-14） |
| [Stage_25b_Roadmap.md](../planning/Stage_25b_Roadmap.md) | Stage 25b：開發流程重構 Phase 1d（設計規劃階段） | v3.10.0 | ✅ 已完成（2026-04-14） |
| [Stage_26_Roadmap.md](../planning/Stage_26_Roadmap.md) | Stage 26：驗收基礎設施 + 版本號集中管理 | v3.11.0 | ✅ 已完成（2026-04-14） |
| [Stage_27a_Roadmap.md](../planning/Stage_27a_Roadmap.md) | Stage 27a：Agent 任務序列 — 核心佇列機制 | v3.12.0 | ✅ 已完成（2026-04-16） |
| [Stage_27b_Roadmap.md](../planning/Stage_27b_Roadmap.md) | Stage 27b：Agent 任務序列 — 操作性與可觀察性 | v3.13.0 | ✅ 已完成（2026-04-16） |
| [Stage_28a_Roadmap.md](../planning/Stage_28a_Roadmap.md) | Stage 28a：Dashboard 雙向操作中心 — 基礎架構與按鈕回覆 | v3.14.0 | ✅ 已完成（2026-04-17） |
| [Stage_28b_Roadmap.md](../planning/Stage_28b_Roadmap.md) | Stage 28b：Dashboard 雙向操作中心 — 文字輸入互動與歷史紀錄 | v3.15.0 | ✅ 已完成（2026-04-17） |
| [Future_Feature.md](../planning/Future_Feature.md) | 未來功能候選清單（不限 Stage） | — | 🔵 持續維護 |
| [agents/software team/Agent_Capability_Gaps.md](../agents/software%20team/Agent_Capability_Gaps.md) | 各 Agent 能力缺口清單（內部協作基礎建設用） | — | 🔵 持續維護 |

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
| v2.1 | 2026-04-03 | Stage 9 全部完成並驗收：Token 監控即時 SignalR 更新、CEO 四類分類 + 提案模式、QA Playwright CI |
| v2.2 | 2026-04-03 | 新增 Stage_10_Roadmap.md（CEO Orchestrator、提案書增強、開發上下文、Review 閉環、Ops Rollback）；修復 CHANGELOG.md base64 問題並補上 v1.1.0 / v1.2.0 |
| v2.3 | 2026-04-03 | Stage 10 實作完成：WorkflowEngine、TaskGroupService、✏️ 提案調整按鈕、Dev repo tree 上下文、Review 閉環 webhook、Ops Rollback GitHub Actions |
| v2.4 | 2026-04-04 | Stage 10 驗收完成；Stage_10_Roadmap.md 補充詳細實作紀錄（架構設計、踩坑、Race Condition、Ops 路徑說明、Migration 指令）；v1.3.1 修正 7 項驗收後 bug |
| v2.5 | 2026-04-05 | 新增 Stage_11_Roadmap.md（Dev Agent 驅動 Claude Code，單一目標）；Future_Feature.md 新增十二～十七共 6 項 |
| v2.6 | 2026-04-05 | Stage 11 驗收完成：ClaudeCodeService subprocess 封裝、Dockerfile 改 sdk:10.0 + Node.js 22 + claude CLI、workspace 改 Linux 路徑；三項踩坑修復後 PR #65 通過 |
| v2.7 | 2026-04-05 | 新增 Stage_12_Roadmap.md（提案流程全面升級：Agent 唯讀探索、Rosa/Demi 串行協作、UI 規格改存 DB）；Future_Feature.md 十七/十八/十九 標記移入 Stage 12 |
| v2.8 | 2026-04-06 | Stage 12 驗收完成（六項驗收全通過）；修正三項 bug（Discord 重複觸發、Slug 中文標題、Demi 調整 prompt）；Stage_12_Roadmap.md 結案 |
| v2.9 | 2026-04-06 | 新增 Stage_13_Roadmap.md（系統穩定性與流程修正：技術債 + Orchestrator 流程 + Dashboard 詳情）；Future_Feature.md 清理已完成 / 已消滅項目 |
| v3.0 | 2026-04-06 | Stage 13 驗收完成：串行流程 Dev → Reviewer → QA → Doc、單一 PR（含 Closes #XX 自動關 Issues）、QA code fence 修正、Playwright 改 self-hosted runner；Future_Feature.md 新增二十一（Dashboard Agent 狀態卡即時更新）|
| v3.1 | 2026-04-06 | 新增 Stage_14_Roadmap.md（CEO 分類補強：技術改善分類、Release/Ops/Doc 路由、任務取消）；Future_Feature.md 第九項移入 Stage 14 |
| v3.2 | 2026-04-06 | Stage 14 驗收完成（CEO 六類分類全通過）；順帶修正 Bug fix Orchestrator 未啟動問題；ClaudeCodeService 外部 cancel kill subprocess 補齊；Stage_14_Roadmap.md 補充實作紀錄 |
| v3.3 | 2026-04-06 | 新增 Stage_15_Roadmap.md（Victoria 接上 Claude Code + Session 對話）；吸收 Future Feature 十（CEO 文件記錄）和十一（Victoria 技術顧問 Phase 1~2）|
| v3.4 | 2026-04-06 | Stage 15 實作完成：Victoria 升級 Claude Code 模式、Session DB 持久化、長期記憶、/new-session 指令、CLAUDE_Victoria.md 模板、EF Migration |
| v3.5 | 2026-04-06 | Stage 15 後續 bugfix：CloneOrPull 取得 repo 副本（修正容器路徑）、appsettings 補 DefaultRepo=AiTeam、LLM 降級原因顯示於 Discord、CLAUDE_Victoria.md 納入 csproj 修正容器內找不到模板問題 |
| v3.6 | 2026-04-07 | Stage 15 驗收完成（8/8 全通過）；Stage_15_Roadmap.md 補充踩坑三件組、診斷工具設計、驗收結果；README.md 新增 Victoria CEO 升級章節、/new-session 指令說明、Stage 15 進度列 |
| v3.7 | 2026-04-07 | 新增 Stage_16_Roadmap.md（PM Agent Iris 品質審核閘門）；Rosa/Demi/Sage 模型改為 Haiku；全面更新 Agent 文件（CEO/Dev 大改、Capability Gaps v2.0）；PM 命名為 Petra、Grand CEO 維持 Iris；Future_Feature.md v3.0 清理 |
| v3.8 | 2026-04-07 | Stage 16 驗收完成（NewFeature 全流程跑完）；Vera 重構為單一 Claude Code session（消滅 false Critical）；QA Agent 重構為 Claude Code session（消滅 StripCodeFence 問題）；Playwright workflow 移除 Start/Stop Dashboard（修正打到 production 的問題）；RunAsync maxTurns 提升至 40（修正 fix loop 截斷）；踩坑五件組全記錄於 Stage_16_Roadmap.md |
| v3.9 | 2026-04-08 | 新增 Stage_17_Roadmap.md（Mock Mode 模擬模式 — IClaudeCodeService 介面 + 代理模式 Runtime 切換 + Dashboard 開關）；Future_Feature.md v3.2 新增十四（測試環境隔離） |
| v4.0 | 2026-04-08 | Stage 17 驗收完成（4 種 /mock 流程全通過）；Stage_17_Roadmap.md 補充實作細節、踩坑三件組（QA/Doc 缺 early return、含提案流程卡死、延遲過短）、驗收結果 |
| v4.1 | 2026-04-08 | 新增 Stage_18_Roadmap.md（Dashboard 可觀測性 — Agent 狀態卡即時更新 + Pipeline View）、Stage_19_Roadmap.md（Dashboard UI 全面打磨）；Future Feature 十/十一 移入 Stage 18 |
| v4.2 | 2026-04-09 | Stage 18 驗收完成（新功能含提案 + Bug Fix 全通過）；踩坑五件組（雙重訂閱 HubConnection、Rosa/Demi GroupId、提案步驟 CompletedAt、header 徽章本地推算、群組列表延遲刷新）；Stage_18_Roadmap.md 補充實作紀錄 |
| v4.3 | 2026-04-10 | Stage 19 Pt.1 驗收完成（StatusBadge 補齊、PipelineList 獨立頁、MudSwitch、表格 FixedHeader）；Stage_19_Roadmap.md 改版為 v2.0（三批 18 項問題清單）；新增 Stage_20_Roadmap.md（全面換 MudBlazor Layout）；決策：Stage 19 Pt.2 暫緩，先執行 Stage 20 奠定 MudLayout 基礎 |
| v4.4 | 2026-04-10 | Stage 20 實作完成（MainLayout → MudLayout、NavMenu → MudNavMenu、Dark Mode → MudThemeProvider、三處 Drawer → MudDrawer Temporary、app.css 清理）；dotnet build 通過（0 errors）；待瀏覽器驗收 |
| v4.5 | 2026-04-11 | Stage 20 驗收完成；補充五項踩坑完整記錄（Layout @rendermode HTTP 500、onclick C# 解析、MudBlazor CSS 雙 hyphen、跨 Circuit 服務隔離、HttpContext Interactive 不可用）；最終架構：Routes.razor 全域 InteractiveServer、Layout 靠 JS onclick + CSS 變數 Dark Mode；Stage_20_Roadmap.md 更新至 v2.0 |
| v4.6 | 2026-04-11 | Stage 19 Pt.2 驗收完成（7 項：首頁緊湊小卡 + 最近流程、MudChip Badge、MudSelect 多選篩選、Agent 左右雙欄、MudDialog 表單、Token Sticky、hover CSS）；五項踩坑記錄；Stage_19_Roadmap.md 更新至 v3.0 |
| v4.7 | 2026-04-11 | Stage 19 Pt.3 驗收完成（8 項：MudIcon Empty State、MudSwitch、MudChip Agent Badge、MudButton、MudStack inline flex 清除、system-config-card 更名、側邊欄 localStorage 持久化、inline 色碼消除）；Stage_19_Roadmap.md 更新至 v3.3；Stage 19 三批全部結案 |
| v4.8 | 2026-04-11 | 新增 Stage_21_Roadmap.md（文件整理 + SemVer 導入）；Future_Feature.md 整理至 v4.0（移除已完成的十/十一，重新編號為一～十二） |
| v4.9 | 2026-04-11 | 建立 docs/conventions/mudblazor.md（MudBlazor 8.x 使用規範，13 大項，含累積踩坑記錄）；Stage_21_Roadmap.md 更新至 v1.1（補入前置作業）|
| v4.10 | 2026-04-11 | 建立 docs/README.md（docs 資料夾導覽入口，說明各子資料夾用途）；Stage_21_Roadmap.md 更新至 v1.2 |
| v4.11 | 2026-04-11 | docs/agents/ 全面重整：8 個有對應 Resources/ 的 Agent 文件加入「執行指引」指標，移除與 CLAUDE_*.md 重疊的行為細節，修正所有錯誤（Notion、PM 狀態、Reviewer 輸出格式、QA/Doc/Rosa 流程描述）|
| v4.12 | 2026-04-11 | CLAUDE.md 全面更新：修正 Blazor Server → Web App、補齊 Stage 11~21 文件清單、新增 mudblazor.md、修正 dotnet ef 指令、新增「版本號管理（SemVer）」章節 |
| v5.0 | 2026-04-11 | Stage 21 完成：docs/ 資料夾重整（planning/ + architecture/ 子資料夾）、telerik.md 刪除、SemVer 導入（Bot + Dashboard 版本號升至 v3.5.0）、索引表加入版本欄 |
| v5.1 | 2026-04-12 | Future_Feature.md v5.0 全盤整理：移除 4 項（MCP/顧問/Doc 品質/API 餘額恢復），新增十一～十三（對抗機制/雙向操作/任務序列），重新編號為一～十三 |
| v5.2 | 2026-04-12 | 新增 Stage_22_Roadmap.md（Dashboard 存取分層 + Token 保護 + 頻道清理，對應 FF 九/七/四） |
| v5.3 | 2026-04-12 | Stage 22 驗收完成：localhost bypass（Host header 方案）、Token 守門 4 層攔截、Reviewer 超月限警示確認、#指令中心 頻道刪除；Future_Feature.md v5.2 新增十五（Dashboard 調整 Token 守門限額）|
| v5.4 | 2026-04-12 | Future_Feature.md 整理至 v5.2（十四/十五合併為 UI 第四批打磨，加入規則管理 Switch 精簡 + 圖示按鈕）；新增 Stage_23_Roadmap.md（Agent 糾錯機制 Phase 1 — 申訴 + 熔斷，對應 FF 十一）|
| v5.5 | 2026-04-12 | Future_Feature.md v6.0：全面流程重構討論完成（七個階段），移除 3 個已完成項目（#指令中心/Token 保護/存取分層→Stage 22），重新編號為一～十四；Stage_23_Roadmap.md v2.0 全面重寫為 Phase 1a（Review Appeal + 流程產出強化 + Sage 轉型 + Git Tag） |
| v5.6 | 2026-04-12 | Stage 23 實作完成：Review Appeal 迴圈 A（Cody-Vera 純對話 while loop，最多 3 輪）、Petra 仲裁（ArbitrateReviewAppealAsync）、SkipReviewerAfterArbitration 仲裁後路由、Cody 實作說明（ImplementationNote）解析儲存、阻礙報告（BlockedOperationException + AssessBlockerAsync 路由）、Sage 轉型為收尾歸檔員（CHANGELOG + archive）、Git Tag 自動化、ReviewIssue.Id、WorkflowSettings、版本號檢查；MockMode 四個 Agent 加 30–60 秒隨機延遲；v3.7.0 tag 驗收通過 |
| v5.7 | 2026-04-12 | 新增 Stage_24_Roadmap.md（開發流程重構 Phase 1b — QA Petra 介入 + Dev_plan 審核強化 + 測試報告結構化 + 文件存 DB 基礎設施） |
| v5.8 | 2026-04-13 | Stage 24 實作完成（v3.8.0）：QA 四路由（code_bug / back_to_reviewer / env_or_test_issue / escalate_boss）、Dev_plan Appeal while loop（Cody 可反駁）、TestReport 結構化存 DB、文件傳遞矩陣（Vera ← dev_plan、Quinn ← issues + dev_plan、Sage ← test_report）；踩坑四件組：QaFixRound 重置、codyJson 提前序列化、accept → return true、DevPlanAppealLog 完整 JSON |
| v5.9 | 2026-04-13 | 新增 Stage_25a_Roadmap.md（開發流程重構 Phase 1c — Kick-off 會議機制：Claude Code 持續對話 session 基礎設施 + 多 Agent 會議引擎 + Petra 主持 Rosa/Demi/Cody/Quinn 全員討論 + Christ 確認任務計劃書） |
| v6.0 | 2026-04-14 | Stage 25a 實作完成（v3.9.0）：MeetingService 多 Agent 會議引擎、RunMeetingSessionAsync（持續對話 session）、Kickoff 步驟插入 NewFeature 流程、Christ 確認機制（Discord 按鈕）、Petra session 保留供修改流程使用、KickoffMeetingLog/TaskPlan/KickoffRound 欄位、EF Migration、TaskPlan 傳遞給後續 Agent；踩坑三件組：UUID session-id 格式要求、--resume 直接帶 UUID（不可搭配 --session-id）、file record 不可出現在非 file-local 型別的方法簽名 |
| v6.1 | 2026-04-14 | 新增 Stage_25b_Roadmap.md（開發流程重構 Phase 1d — 設計規劃階段：移除提案 Rosa/Demi、Rosa Issues + 條件式 Demi、設計會議 + 調整機制、條件式 Christ 確認） |
| v6.2 | 2026-04-14 | Stage 25b 實作完成（v3.10.0）：設計規劃階段全流程（5 人設計會議 + needs_adjustment 調整 + consensus/escalate 路由）；關鍵踩坑：Petra Design session 改用 Guid.NewGuid()（非 group.Id，避免 Kickoff 衝突）、ModifyDesignPlanAsync 接收外部 sessionId、MockMode Rosa Issues 解析失敗 fallback、Demi 動態加入邊界案例；EF Migration Stage25bDesignFields；Feature 八 Phase 1 全部完成 |
| v6.3 | 2026-04-14 | 新增 Stage_26_Roadmap.md（驗收基礎設施：Dashboard 詳情頁顯示會議紀錄/計劃書、Pipeline View Kickoff/Design 步驟、MockMode 全流程修正、版本號集中管理 Directory.Build.props） |
| v6.4 | 2026-04-14 | Stage 26 實作完成（v3.11.0）：Directory.Build.props 集中版本號管理、TaskGroupDto 補 6 欄位 + PipelineView MudExpansionPanels 折疊面板、Kickoff/Design 步驟建立 TaskItem（PipelineView 可見）、MockClaudeCodeService 改 prompt 判斷角色（修正 sessionId UUID 誤判）、Reviewer/QA/Doc MockMode 狀態時序修正（running → delay → done）|
| v6.5 | 2026-04-15 | 新增 Stage_27a/27b_Roadmap.md（Agent 任務序列：27a = 核心佇列 + WorkflowEngine 整合 + Crash Recovery；27b = Agent 狀態管理 + Dashboard 佇列視覺化，對應 FF 十） |
| v6.6 | 2026-04-16 | Stage 27a 實作完成（v3.12.0）：DB-as-Queue（TaskItem 新增 QueuedAt/QueueStatus/WorkflowAgentKey）、AgentQueueService + AgentQueueProcessor（per-agent SemaphoreSlim）、FireOneStepAsync 純 enqueue、Crash Recovery；關鍵修正 db.Attach(task)（EF detached entity 導致狀態卡在執行中） |
| v6.7 | 2026-04-16 | Stage 27b 實作完成（v3.13.0）：AppSettingsService.SetAsync（cache 即時生效）、Processor 雙保險 Stopping→Stopped（主迴圈空閒路徑 + finally race condition 安全網）、Discord 五指令（/pause、/resume、/stop-all、/resume-all、/queue）AddChoice 下拉選單、AgentQueueDto + SignalR QueueUpdate 鏈路、StatusBadge queued、Home.razor 卡片狀態 Badge + 佇列深度 Chip；Future_Feature.md 新增 Feature 十後續三個待討論項目 |
| v6.8 | 2026-04-16 | 新增 Stage_28a_Roadmap.md（Dashboard 雙向操作中心 Phase 1 — BossInteraction Entity、Bot 寫入 8 個確認點、Dashboard 操作中心頁面 /interactions、InteractionProcessor 輪詢 Dashboard 回覆 + 先到先贏雙通道同步，對應 FF 九） |
| v6.9 | 2026-04-17 | Stage 28a 實作完成（v3.14.0）：BossInteraction Entity + EF Migration、BossInteractionRepository（樂觀鎖 ExecuteUpdateAsync WHERE status='pending'）、InteractionService（Singleton + CreateAsyncScope，8 個確認點 pure additive 寫入）、Dashboard 操作中心 /interactions（InteractionCenter + InteractionCard + InteractionRespondService Scoped 直寫 DB + SignalR）、InteractionProcessor（3 秒輪詢消費 + Discord 同步訊息）、TaskGroupService.ProcessBossResponseAsync（統一分派入口，kickoff/design 共用既有方法）；CI/CD 踩坑：Bot Dockerfile apt NodeSource 安裝在 GitHub runner 上極慢（22min+），改用 node:22-slim multi-stage COPY binary 解決 |
| v7.0 | 2026-04-17 | Stage 28a 驗收修正三項：① AgentStatusController.RespondToInteractionAsync 改 delegate 給 InteractionRespondService，消除重複的 interactionRepo + SignalR 邏輯；② TaskGroupService exec_no 新增 CancelTaskItemFromContextAsync（TaskItem 標記 cancelled + Dashboard 推送），confirm_no 加說明註解；③ InteractionProcessor catch 區塊加入 MarkProcessedByBotAsync，避免 ContextJson 格式異常造成無限重試 |
| v7.1 | 2026-04-17 | Stage 28b 實作完成（v3.15.0）：BossInteraction.ResponseContent + EF Migration、三個 ActionsJson 加入修改動作（requiresInput: true）、TextInputDialog.razor + InteractionCard RequiresInput 分支、InteractionRespondService content overload、ProcessBossResponseAsync responseContent 參數 + ProcessProposalAdjustAsync 新方法、Discord 三個修改按鈕 SyncDiscordResponseAsync 同步、RegisterProposalConfirmation、歷史紀錄篩選（類型/來源/日期）+ MudTable 分頁 |
| v7.2 | 2026-04-18 | Stage 28b 驗收修正三項：① Kickoff/Design modify 分支補 CreateInteractionAsync（修改後的新確認 Dashboard 看不到）；② MockMode 提案改手動確認（移除倒數 Task.Run，改用 RegisterProposalConfirmation + CreateInteractionAsync）；③ ExecuteProposalApprovedAsync 防重複建 group（task.GroupId 有值時直接用現有 group）|

---

*本文件為動態維護文件，隨規劃討論持續更新。*

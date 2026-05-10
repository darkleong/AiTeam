# AiTeam v5 動態架構 — Phase B Charter spike deliverable

> Charter spike 階段（Stage 62，v3.51.0，2026-05-10）— FF 三十六 Phase B 動態架構規劃文件 deliverable

## 戰略脈絡

Trial_v6/v7/v8 連續揭 6 🔴 + Phase 1 deliver 度持續倒退（11/12 → 部分 → 0 → 0 卡死更前置）已實證 v4 hierarchical static 架構補強 ROI 為負（Stage 60+61 才修完即揭新類型 🔴）。

**Christ 2026-05-10 拍板路線 D**（FF 三十六 Phase B 動態架構 spike） — 對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項 + 漸進遷移過半 + Phase A ✅ 啟動條件達成。

**兩階段拆分**（對齊 Stage 51 spike Charter 模式 + Stage 53A 拆 Stage 模式）：

- **Stage 62 = Phase B Charter spike**（純文件 deliverable / main branch）— 本資料夾 4 deliverable
- **Stage 63 = Phase B PoC spike**（小範圍真實任務驗證 / `feature/v5-poc` branch）— Charter 通過才 commit

## 4 個 deliverable

| 檔案 | 內容 | 對應驗收場景 |
|---|---|---|
| [01_Spike_Plan.md](./01_Spike_Plan.md) | 7 驗證項細節（從「概念」變「具體 spike Plan」每項含 deliverable + wire 點 + Mock 場景 + 預期數據 + 失敗條件）| 場景 A |
| [02_Architecture_Wire.md](./02_Architecture_Wire.md) | 4 層 Hierarchy 落具體 service / DI 結構（含 per-task session DB schema 候選 + Tool Set Capability schema 候選）| 場景 B |
| [03_v4_Code_Audit.md](./03_v4_Code_Audit.md) | v4 hierarchical static code 三類分類（吸收 / 重寫 / 全保留）+ LoC 量化 + 風險評估 | 場景 C |
| [04_Stage_63_PoC_Roadmap_Draft.md](./04_Stage_63_PoC_Roadmap_Draft.md) | Stage 63 PoC spike 範圍草稿（branch 策略 / PoC 範圍 / 驗證任務 / 驗收標準 / 規模 + cost 預估）| 場景 D |

## 80% 既有設計拍板（不重評估）

FF 三十六 2026-05-01 brainstorm 拍板成熟（[Future_Feature.md:160-290](../../planning/Future_Feature.md)）：

- 4 層 Hierarchy 架構（Christ → Victoria → Petra Orchestrator → Workers）
- 5 個關鍵挑戰拍板（Victoria 角色 / Petra 決策邊界 / 老闆介入機制 / Mock 模式 / Crash Recovery）
- Hybrid 會議模式（會議默認保留小需求動態跳過 + Petra 動態切換 Group Chat / Agent-as-Tool）
- Tool Set + Capability-based Multi-Agent + MS Agent Framework Magentic Orchestration

Charter spike 補完剩 20%（從「概念」變「具體 spike Plan」+ 落具體 service / DI / wire 點）。

## Stage 63 PoC 啟動條件

- Charter Plan 4 deliverable 完整性檢查 PASS（驗收場景 A/B/C/D）
- Christ 視覺驗收 Charter Plan 通過拍板
- 自動條件：Stage 60+61 既有 11 Mock 場景仍綠 + dotnet test 131 passed（Charter spike 0 production code 改動）

## 相關文件

- [Stage 62 Roadmap](../../planning/Stage_62_Roadmap.md) — Charter spike 任務拆分 + 驗收情境
- [Future_Feature.md FF 三十六](../../planning/Future_Feature.md) — Phase B 既有 80% 設計拍板原文 + 5 挑戰 + 7 驗證項清單
- [00_Master_Plan.md](../00_Master_Plan.md) — 文件導覽 stub
- [03_Workflow_Overview.md](../03_Workflow_Overview.md) — v4 開發流程全景圖（v5 PoC 後重寫）

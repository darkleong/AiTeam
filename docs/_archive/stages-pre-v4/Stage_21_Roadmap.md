# Stage 21 — 文件整理與 SemVer 導入

> Stage：21
> 對應版本：v3.5.0
> 建立日期：2026-04-11
> 狀態：✅ 已完成（2026-04-11）

---

## 目標

1. **docs 資料夾結構整理** — 根目錄文件過多（26 個），依性質分類到子資料夾，提升可讀性
2. **SemVer 導入** — 專案版本號從 `1.0.0` 補正為對應當前開發進度的語意化版本號，並建立後續 Stage 的版本對應慣例
3. **conventions/telerik.md 移除** — Stage 6 已全面換成 MudBlazor，Telerik 規範文件早已過時

---

## 背景說明

### docs 結構現況

```
docs/                          ← 26 個檔案混在根目錄
  00_Master_Plan.md
  01_Vision_and_Architecture.md
  02_Infrastructure.md
  About_Christ.md
  Future_Feature.md
  Stage_1_Design.md
  ...（Stage_2 ~ Stage_20，共 20 個）
  agents/                      ← 已分類
  conventions/                 ← 已分類（含過時的 telerik.md）
  generated/                   ← 已分類
  ui-specs/                    ← 已分類
```

### SemVer 現況

`AiTeam.Bot` 和 `AiTeam.Dashboard` 的 `<Version>` 均為 `1.0.0`，從未更新，與實際開發進度完全脫節。

---

## 目標結構

```
docs/
  ├── architecture/            ← 新建：系統架構與背景（靜態，少變動）
  │     00_Master_Plan.md
  │     01_Vision_and_Architecture.md
  │     02_Infrastructure.md
  │     About_Christ.md
  │
  ├── planning/                ← 新建：Stage 規劃與 Future Feature（動態，常更新）
  │     Future_Feature.md
  │     Stage_1_Design.md
  │     Stage_2_Foundation.md
  │     ...（全部 Stage_*.md）
  │     Stage_21_Roadmap.md
  │
  ├── conventions/             ← 維持，移除 telerik.md
  │     csharp.md
  │     blazor.md
  │     ef-core.md
  │     api-design.md
  │                            ← telerik.md 刪除
  │
  ├── agents/                  ← 維持不動
  ├── generated/               ← 維持不動
  └── ui-specs/                ← 維持不動
```

---

## SemVer 版本對應表

### 歷史版本補定義

| 版本 | 對應 Stage | 說明 |
|------|-----------|------|
| v0.1.0 | Stage 1–2 | Discord Bot 基礎 + PostgreSQL |
| v0.2.0 | Stage 3 | 多 Agent 架構 |
| v0.3.0 | Stage 4 | Blazor Dashboard |
| v0.4.0 | Stage 5 | 功能擴充（提案、部署紀錄） |
| v1.0.0 | Stage 6 | MudBlazor 全面替換，第一個穩定版本 |
| v1.1.0 | Stage 7 | Reviewer / Release / Designer Agent |
| v1.2.0 | Stage 8 | 可靠性補完 + Notion 遷移 |
| v1.3.0 | Stage 9 | Token 監控 + CEO 智慧分類 + QA Playwright |
| v1.4.0 | Stage 10 | CEO Orchestrator + 提案書增強 + Review 閉環 |
| v2.0.0 | Stage 11 | Dev Agent 接上 Claude Code（架構重大升級） |
| v2.1.0 | Stage 12 | 提案流程全面升級（Rosa/Demi 串行 + UI 規格存 DB） |
| v2.2.0 | Stage 13 | 串行流程穩定化（單一 PR、QA 修正） |
| v2.3.0 | Stage 14 | CEO 分類與流程完整性補強 |
| v2.4.0 | Stage 15 | Victoria 接上 Claude Code + Session + 長期記憶 |
| v3.0.0 | Stage 16 | PM Agent Petra 加入，品質審核閘門（流程架構重大改變） |
| v3.1.0 | Stage 17 | Mock Mode 模擬模式 |
| v3.2.0 | Stage 18 | Dashboard 可觀測性升級（即時狀態 + Pipeline View） |
| v3.3.0 | Stage 19 | Dashboard UI 全面打磨（三批共 23 項） |
| v3.4.0 | Stage 20 | Dashboard 全面換 MudBlazor Layout |
| **v3.5.0** | **Stage 21** | **文件整理 + SemVer 導入（本 Stage）** |

### 後續版本慣例

- **patch（x.x.+1）**：hotfix、小 bug 修正，不跟 Stage 走
- **minor（x.+1.0）**：每個 Stage 完成 → 遞增一個 minor
- **major（+1.0.0）**：架構層面重大改變（如 Stage 11、Stage 16 等級）

---

## 前置作業（規劃階段已完成）

### 0a. 建立 `docs/README.md` ✅

由 Aria 在規劃階段撰寫完成（2026-04-11）。
作為 `docs/` 資料夾的導覽入口，說明各子資料夾用途並指向 `architecture/00_Master_Plan.md`。
Stage 21 完成後 `docs/` 根目錄清空，本文件成為唯一的進入點。

### 0b. 建立 `docs/conventions/mudblazor.md` ✅

由 Aria 在規劃階段撰寫完成（2026-04-11），涵蓋 MudBlazor 8.x 使用規範：
- MudLayout / MudDrawer 架構與子組件 `@bind-Open` 陷阱
- MudTable、MudSelect（含多選 `:after`）、MudList、MudSwitch、MudChip、MudDialog
- Dark Mode CSS 變數方案
- Pre-commit Checklist（13 大項）

---

## 實作項目

### 1. 建立 `docs/planning/` 並移入 Stage 文件

移動以下檔案到 `docs/planning/`：
- `Future_Feature.md`
- `Stage_1_Design.md` ~ `Stage_21_Roadmap.md`（共 21 個）

### 2. 建立 `docs/architecture/` 並移入架構文件

移動以下檔案到 `docs/architecture/`：
- `00_Master_Plan.md`
- `01_Vision_and_Architecture.md`
- `02_Infrastructure.md`
- `About_Christ.md`

### 3. 刪除 `docs/conventions/telerik.md`

Stage 6 已全面換成 MudBlazor，此文件已過時。

### 4. 更新 `docs/architecture/00_Master_Plan.md` 內部連結

Master Plan 中所有 Stage 文件的相對路徑需更新：
```markdown
// 原本
[Stage_21_Roadmap.md](./Stage_21_Roadmap.md)

// 改為
[Stage_21_Roadmap.md](../planning/Stage_21_Roadmap.md)
```

conventions、agents 等子資料夾的連結同步確認（若有）。

### 5. 更新 `CLAUDE.md` 規劃文件區塊

> ⚠️ **注意**：CLAUDE.md 已於 2026-04-11 由 Aria 大幅更新（新增 SemVer 章節、mudblazor.md、Migration 指令等）。
> **只修改「規劃文件」章節中的 docs 結構區塊，其餘內容完全不動。**

將「規劃文件」章節的 docs 結構更新為：

```
docs/
  README.md                  ← 資料夾導覽（各子資料夾說明）
  architecture/
    00_Master_Plan.md          ← 總索引（含所有 Stage 狀態與版本歷史）
    01_Vision_and_Architecture.md
    02_Infrastructure.md
    About_Christ.md
  planning/
    Stage_1_Design.md ~ Stage_21_Roadmap.md  ← 全部 Stage 文件
    Future_Feature.md          ← 未來功能候選清單
  conventions/               ← 編程規範（見下方）
  agents/                    ← Agent 角色說明文件
```

### 6. 更新 `AiTeam.Bot.csproj` 版本號

```xml
<Version>3.5.0</Version>
<AssemblyVersion>3.5.0</AssemblyVersion>
```

### 7. 更新 `AiTeam.Dashboard.csproj` 版本號

```xml
<Version>3.5.0</Version>
<AssemblyVersion>3.5.0</AssemblyVersion>
```

Dashboard 頁腳顯示版本號（`AppVersion`），更新後會自動反映。

### 8. 更新 `00_Master_Plan.md` — 新增版本欄位

索引表加入「版本」欄：

```markdown
| 文件 | 說明 | 版本 | 狀態 |
|------|------|------|------|
| Stage_20_Roadmap.md | Dashboard 全面換 MudBlazor Layout | v3.4.0 | ✅ |
| Stage_21_Roadmap.md | 文件整理與 SemVer 導入 | v3.5.0 | ✅ |
```

---

## 執行順序

| 順序 | 項目 | 說明 |
|------|------|------|
| 1 | 刪除 `telerik.md` | 最簡單，先清掉 |
| 2 | 建立 `planning/`，移入所有 Stage 文件 | 數量多但操作單純 |
| 3 | 建立 `architecture/`，移入四個架構文件 | |
| 4 | 更新 `00_Master_Plan.md` 內部連結 | 需逐一確認每條連結 |
| 5 | 更新 `CLAUDE.md` 路徑說明 | |
| 6 | 更新兩個 `.csproj` 版本號 | |
| 7 | 更新 `00_Master_Plan.md` 版本欄位 | |
| 8 | `dotnet build` 確認無誤 | |
| 9 | git commit & push | |

---

## 注意事項

- `00_Master_Plan.md` 移動到 `architecture/` 後，**相對路徑方向改變**：原本 `./Stage_XX_Roadmap.md` 需改為 `../planning/Stage_XX_Roadmap.md`（共 21+ 條連結需逐一更新）
- `CLAUDE.md` 位於專案根目錄，只改「規劃文件」章節的 docs 結構區塊，其餘內容不動
- `docs/README.md` **不需要修改**：已按 Stage 21 完成後的結構撰寫，移完檔案後連結自動正確
- `generated/` 和 `ui-specs/` 內部文件若有跨引用需一併確認（可能性低）
- Stage 文件本身互相引用（如「詳見 Stage_XX_Roadmap.md」）若用相對路徑也需更新，若為純文字描述則不影響

---

## 驗收條件

- [x] `docs/README.md` 已建立（規劃階段完成）
- [x] `docs/conventions/mudblazor.md` 已建立（規劃階段完成）
- [x] `docs/planning/` 存在，包含所有 21 個 Stage 文件 + Future_Feature.md
- [x] `docs/architecture/` 存在，包含 4 個架構文件
- [x] `docs/conventions/telerik.md` 已刪除
- [x] `docs/` 根目錄不再有任何 `.md` 檔案
- [x] `00_Master_Plan.md` 所有 Stage 連結可正常開啟（相對路徑正確）
- [x] `CLAUDE.md` 的 docs 結構說明與實際結構一致（含 `mudblazor.md` 列入 conventions）
- [x] `AiTeam.Bot` + `AiTeam.Dashboard` 版本號均為 `3.5.0`
- [ ] Dashboard 頁腳版本號顯示 `3.5.0`（等自動部署完成後驗收）
- [x] `dotnet build` 通過（0 errors）
- [x] git commit & push，GitHub Actions 自動部署通過

---

## 實作紀錄

### 執行細節

**檔案搬移（shell 批次操作）**

```bash
# 建立 planning/ 並批次移入 22 個檔案
mkdir docs/planning
mv docs/Future_Feature.md docs/Stage_*.md docs/planning/

# 建立 architecture/ 並移入 4 個架構文件
mkdir docs/architecture
mv docs/00_Master_Plan.md docs/01_Vision_and_Architecture.md docs/02_Infrastructure.md docs/About_Christ.md docs/architecture/

# 刪除過時規範
rm docs/conventions/telerik.md
```

git 正確識別為 **Rename**（非 delete + add），commit diff 顯示 `R docs/Stage_*.md -> docs/planning/Stage_*.md`。

**00_Master_Plan.md 路徑更新範圍**

移到 `architecture/` 後，所有相對路徑方向改變：

| 連結類型 | 原路徑 | 新路徑 |
|---------|--------|--------|
| Stage 文件（21 條） | `./Stage_XX_Roadmap.md` | `../planning/Stage_XX_Roadmap.md` |
| Future_Feature | `./Future_Feature.md` | `../planning/Future_Feature.md` |
| agents 子資料夾 | `./agents/...` | `../agents/...` |
| 同目錄架構文件 | `./01_Vision_and_Architecture.md` | 不變（同資料夾） |

同步補入「版本」欄（全 21 Stage），Master Plan 版本升至 v5.0。

**csproj 版本號狀況**

- `AiTeam.Dashboard.csproj`：有 `<Version>1.0.0</Version>`，直接更新為 `3.5.0`
- `AiTeam.Bot.csproj`：**完全沒有版本標籤**，需新增（非修改）`<Version>3.5.0</Version>` + `<AssemblyVersion>3.5.0</AssemblyVersion>`

**Git Tag 策略**

`v1.0.0` 已存在（指向 Stage 7 中間的 CHANGELOG commit，位置略偏）→ 保留不動（公開 tag 不移）。
補建 19 個 tag（v0.1.0 ~ v3.5.0），全部推到 remote：

```bash
git tag v0.1.0 27d3c92   # Stage 1-2：Add README（Stage 3 開始前的最後 commit）
git tag v0.2.0 39f97f1   # Stage 3：Stage 2 & 3 狀態標記完成
git tag v0.3.0 eb94770   # Stage 4：docs Stage 4 完成
git tag v0.4.0 ff0e358   # Stage 5：README 更新至 Stage 5 完成
# v1.0.0 已存在，略過
git tag v1.1.0 5bb7334   # Stage 7：README 更新反映 Stage 7 完成
git tag v1.2.0 423d64f   # Stage 8：feat Stage 8（無獨立結案 commit）
git tag v1.3.0 0d895b9   # Stage 9：Stage 9 驗收完成
git tag v1.4.0 4515ace   # Stage 10：Stage 10 驗收完成
git tag v2.0.0 d1c6fd0   # Stage 11：結案文件更新
...（v2.1.0 ~ v3.5.0 依結案 commit 逐一打點）
git push origin --tags
```

**dotnet build 結果**

0 errors，21 warnings（均為既有：Playwright MSTEST0037 + MudBlazor MUD0002），與本次變更無關。

---

## 變更紀錄

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-11 | 初版建立 |
| v1.1 | 2026-04-11 | 補入前置作業 Item 0：mudblazor.md 規劃階段已完成；驗收條件新增 mudblazor.md 與 CLAUDE.md conventions 更新說明 |
| v1.2 | 2026-04-11 | 補入前置作業 Item 0a：docs/README.md 規劃階段已完成 |
| v1.3 | 2026-04-11 | Item 5 補充警告（CLAUDE.md 已更新，只改 docs 結構區塊）；注意事項補充 README.md 不需修改 |
| v2.0 | 2026-04-11 | 實作完成：狀態更新為已完成、驗收條件全打勾、補入完整實作紀錄（搬移命令、路徑更新範圍、csproj 狀況、Git Tag 策略、build 結果）|

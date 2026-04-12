# Stage 23 — 開發流程重構 Phase 1a（Review Appeal + 流程產出強化）

> Stage：23
> 對應版本：v3.7.0
> 建立日期：2026-04-12
> 狀態：📋 規劃完成，待實作
> 文件版本：v2.0

---

## 目標

實作 **開發流程重構 Phase 1a**（Future Feature 八 的子集）：
- 聚焦「改善現有流程中能立即生效的部分」
- 不需要大幅改造 WorkflowEngine 架構
- 直接解決已知事故的根因（Vera 誤判、缺乏糾錯回路）

> 對應 Future Feature：八（Phase 1a）
> 完整流程設計（七個階段）見 `Future_Feature.md` 八

---

## 背景說明

### 已發生事故

1. **Vera 誤判事故**：Vera 持續報告 false critical，Cody 無法反駁（單向權力結構），死循環直到 Christ 手動介入
2. **實作 Session 死循環事故**：驗收反覆失敗，Session 不斷做無效修正，沒有機制偵測循環

### 完整流程重構計劃

經過全盤討論，Future Feature 八已完成七個階段的完整設計：
1. 需求計劃（Kick-off 會議）
2. 設計規劃（設計會議）
3. 開發（Dev_plan + 阻礙報告 + 實作說明）
4. 程式碼審查（Vera-Cody 直接對話 + Review Appeal）
5. QA 測試（Petra 在迴圈中）
6. 收尾歸檔（Sage 轉型）
7. 完成/上線（交付通知 + git tag 自動化）

Stage 23 先實作其中**不需要多 Agent 會議機制**的部分（Phase 1a），多 Agent 會議（Kick-off + 設計會議）留待後續 Stage。

---

## 實作項目

### 23-1. Vera-Cody Review Appeal（程式碼審查對話機制）

**設計概念**

Vera 審查後，Cody 可逐條回應（agree / disagree）。disagree 需附具體理由，Vera 基於程式碼事實重新評估。超過輪次上限由 Petra 仲裁。

```
Vera 審查 PR → 產出審查報告
    ↓
Cody 逐條回應（agree 為預設）：
    ├── 全部 agree → 一次性修正 → 重新提交 → Vera 再審（迴圈 B）
    ├── 部分 disagree → Cody 說明反駁理由（迴圈 A）
    │       ↓
    │   Vera 重新評估：修改報告 / 維持原判
    │       ↓
    │   回到 Cody 回應（迴圈 A，上限 3 輪）
    └── 全部 disagree → 同上走迴圈 A

迴圈 A 超限 → Petra 仲裁 → Cody 修正 → 交 Petra 直接審核（不回 Vera）
迴圈 B 超限 → Petra 介入判斷（Vera 過度挑剔 / Cody 修正不佳 / 需求有問題）
```

**實作位置**

- Cody 的 `CLAUDE_Cody.md` 加入 Review Appeal 指令段落
- Vera 的 `CLAUDE_Vera.md` 加入「收到反駁時基於程式碼事實重新評估」指令
- `WorkflowEngine` 在 Vera 審查後，解析 Cody 輸出是否有 disagree 項目
- 若有 disagree，觸發 Vera-Cody 對話迴圈
- 迴圈 A/B 計數器（上限 3 輪），超限觸發 Petra 仲裁
- Petra 仲裁後，Cody 修正完交 Petra 審核（不回 Vera）

**設定**

`appsettings.json` 加入：

```json
"WorkflowSettings": {
  "ReviewAppealMaxRounds": 3
}
```

---

### 23-2. 實作說明（Cody 開發完畢時的結構化產出）

**設計概念**

Cody 開發完畢提交 PR 時，同時產出一份「實作說明」，記錄：
- 實作了哪些 Issues
- 關鍵技術決策與理由
- 與 Dev_plan 不同的地方及原因（如有 Dev_plan）
- 開發中遇到的問題與解決方式
- 修改的檔案清單與用途

**實作位置**

- Cody 的 `CLAUDE_Cody.md` 加入實作說明產出指令
- `WorkflowEngine` 在 Cody 完成後，解析並儲存實作說明
- 實作說明隨 PR 一起傳給 Vera（審查參考）和 Quinn（測試參考）

---

### 23-3. 阻礙報告（Cody 開發中的暫停回報機制）

**設計概念**

Cody 開發中遇到無法解決的阻礙（架構不支援、依賴缺失、需求矛盾），可輸出「阻礙報告」暫停開發：

```json
{
  "status": "blocked",
  "type": "architecture_limitation | dependency_missing | requirement_conflict | other",
  "details": "...",
  "attempted_solutions": "..."
}
```

**實作位置**

- Cody 的 `CLAUDE_Cody.md` 加入阻礙報告指令
- `WorkflowEngine` 偵測 `"status": "blocked"` 後，交給 Petra 判斷：
  - 技術問題 → 給建議讓 Cody 繼續
  - 需求問題 → 退回上游
  - 無法判斷 → Victoria 上呈 Christ

---

### 23-4. 審查報告格式（Vera 結構化產出）

**設計概念**

Vera 產出結構化的審查報告，記錄每輪審查結果和 Cody 的回應：

```markdown
# 審查報告

## 基本資訊
- PR: #{編號}
- 審查者: Vera
- 總輪次: {N}
- 最終結果: 通過 / 仲裁後通過

## 各輪紀錄

### 第 1 輪
| # | 嚴重度 | 檔案 | 說明 | Cody 回應 | 結論 |
|---|--------|------|------|-----------|------|
| 1 | Critical | path/file.cs:42 | ... | agree | 已修正 |
| 2 | Warning  | path/file.cs:87 | ... | disagree: {理由} | Vera 接受反駁 |

## 仲裁紀錄（如有）
- 觸發原因: 迴圈 A/B 超限
- Petra 判斷: 支持 Vera / 支持 Cody（逐項）
```

**實作位置**

- Vera 的 `CLAUDE_Vera.md` 加入審查報告格式模板
- `WorkflowEngine` 解析審查報告，追蹤輪次和結果

---

### 23-5. Vera 版本號檢查

**設計概念**

Vera 審查時額外檢查 `.csproj` 的 `<Version>` 標籤是否已更新為 Stage Roadmap 指定的目標版本。

**實作位置**

- Vera 的 `CLAUDE_Vera.md` 加入版本號檢查指令
- Vera 的 prompt 傳入目標版本號（從任務描述或 WorkflowEngine 設定取得）

---

### 23-6. Sage 角色轉型（收尾歸檔員）

**設計概念**

Sage 從「技術文件撰寫員」轉型為「收尾歸檔員」：
- 不再讀 .cs 檔產生 API 技術文件
- 改為將任務的所有階段文件歸檔整理（統一格式、建索引）
- 更新 CHANGELOG

**實作位置**

- 重寫 `CLAUDE_Sage.md`：從技術文件模板改為歸檔整理 + CHANGELOG 更新指令
- `DocAgentService.cs` 調整：輸入從 PR .cs 檔案清單，改為各階段產出文件
- 產出從 `docs/generated/pr{N}-doc.md` 改為歸檔索引 + CHANGELOG 條目

---

### 23-7. Git Tag 自動化

**設計概念**

GitHub Actions workflow 在部署成功後，自動從 `.csproj` 讀取版本號建立 git tag。

**實作位置**

現有 `.github/workflows/` 部署 workflow 加入步驟：

```yaml
- name: Auto tag version
  run: |
    VERSION=$(grep -oP '<Version>\K[^<]+' src/AiTeam.Bot/AiTeam.Bot.csproj)
    if ! git rev-parse "v$VERSION" >/dev/null 2>&1; then
      git tag "v$VERSION"
      git push origin "v$VERSION"
    fi
```

- 放在 `docker compose up` 成功之後（確保 tag 只指向成功部署的程式碼）
- 檢查 tag 是否已存在（避免重複）

---

## 實作順序建議

```
1. 23-7（Git Tag 自動化）         ← GitHub Actions 改一步，最簡單，先做
2. 23-5（Vera 版本號檢查）        ← Prompt 加一條，成本最低
3. 23-2（實作說明）               ← Cody Prompt + WorkflowEngine 解析
4. 23-3（阻礙報告）               ← Cody Prompt + WorkflowEngine 路由
5. 23-4（審查報告格式）           ← Vera Prompt + WorkflowEngine 解析
6. 23-1（Review Appeal）          ← 最複雜，需要 Vera-Cody 對話迴圈 + Petra 仲裁
7. 23-6（Sage 角色轉型）          ← 依賴前面的產出格式定義
```

---

## 不在 Stage 23 範圍（留待後續 Stage）

| 項目 | 原因 |
|------|------|
| Kick-off 會議（第一階段） | 需要 WorkflowEngine 支援多 Agent 互動，複雜度最高 |
| 設計會議（第二階段） | 同上 |
| Dev_plan 審核（第三階段） | 依賴 Petra 會議協調能力建立後 |
| QA 流程改造（第五階段 Petra 介入） | 依賴會議機制經驗 |
| 文件存 DB + WorkflowEngine 傳遞 | 跨階段基礎設施，範圍大 |
| Dashboard 輪次上限設定 | 依賴 Future Feature 十二 |
| Victoria 交付通知改造（第七階段） | 依賴 Future Feature 九（Dashboard 雙向） |

---

## 驗收清單

- [ ] Review Appeal：Cody 可對 Vera critical 表達 disagree，觸發對話迴圈（Mock Mode 測試）
- [ ] Review Appeal：迴圈 A 超過 3 輪，觸發 Petra 仲裁
- [ ] Review Appeal：迴圈 B 超過 3 輪，Petra 介入判斷
- [ ] Review Appeal：Petra 仲裁後 Cody 修正交 Petra 審核（不回 Vera）
- [ ] 實作說明：Cody 開發完畢時產出結構化實作說明
- [ ] 阻礙報告：Cody 輸出 blocked 狀態時，WorkflowEngine 路由給 Petra
- [ ] 審查報告：Vera 產出結構化審查報告（含各輪紀錄）
- [ ] 版本號檢查：Vera 審查時檢查 .csproj 版本號
- [ ] Sage 轉型：Sage 產出歸檔索引 + CHANGELOG（非 API 技術文件）
- [ ] Git Tag：push to main 後自動建立版本 tag
- [ ] `ReviewAppealMaxRounds` 設定值生效
- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] git commit + push
- [ ] `.csproj` 版本更新為 `3.7.0`
- [ ] git tag `v3.7.0`

---

## 注意事項

1. **Review Appeal 是最複雜的項目**：需要 WorkflowEngine 支援 Vera ↔ Cody 多輪互動 + 計數器 + Petra 仲裁分支。建議先從簡單項目（git tag、版本號檢查、實作說明）開始，建立信心後再處理 Review Appeal。

2. **Prompt 設計要精準**：Vera 收到反駁時必須基於「程式碼事實」重新評估，不能被話術說服。Cody 的 disagree 也必須附具體理由，不能只說「我覺得也可以」。

3. **Mock Mode 驗收**：Review Appeal 的驗收建議在 Mock Mode 下執行，避免消耗真實 API Token。

4. **Sage 轉型的輸入調整**：Stage 23 範圍內 Sage 的輸入仍以 PR diff 為主（因為文件存 DB 的基礎設施不在本 Stage），但 CLAUDE_Sage.md 的指令改為歸檔整理 + CHANGELOG。完整的七份文件匯入等文件存 DB 機制建立後再接上。

5. **迴圈 B 的計數**：Cody 修正引入的新問題統一計入迴圈 B 總輪次，不另外區分。

---

## 變更紀錄

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-12 | v1.0 | Aria 撰寫初版規劃書（含 UI 打磨 + 糾錯機制） |
| 2026-04-12 | v1.1 | 移除 UI 第四批打磨（十四尚未確認完整），Stage 23 聚焦糾錯機制 Phase 1 |
| 2026-04-12 | v2.0 | 全面重寫 — 基於 Future Feature 八完整流程設計，Stage 23 改為 Phase 1a（Review Appeal + 流程產出強化 + Sage 轉型 + Git Tag），多 Agent 會議機制留待後續 Stage |

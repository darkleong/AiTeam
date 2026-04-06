# Stage 15：Victoria 接上 Claude Code + Session 對話

> 版本：v1.0
> 建立日期：2026-04-06
> 狀態：📋 規劃中

---

## 目標

Stage 14 給了 Victoria 8 條路（分類 + 路由），但她仍然是「瞎子分配器」——看不懂 codebase、記不住對話、無法幫老闆記錄。

Stage 15 讓 Victoria 從「有路」升級到「有腦」：
1. **接上 Claude Code** — Victoria 自己能探索 codebase，回答技術問題，讀寫 docs/ 文件
2. **Session 對話歷史** — Victoria 能進行多輪深度討論，記得 5 分鐘前說過什麼

做完後，老闆在 Discord 跟 Victoria 對話的體驗，**接近在 Claude Code 跟顧問對話**。

---

## 目前的 Victoria（Stage 14 後）

| 能力 | 狀態 |
|------|------|
| 分類 8 類指令 | ✅ Stage 14 |
| 路由到所有 Agent | ✅ Stage 14 |
| 取消任務 | ✅ Stage 14 |
| 理解 codebase | ❌ 完全看不到程式碼 |
| 讀寫 docs/ 文件 | ❌ 無法幫老闆記錄 |
| 多輪對話 | ❌ 每則訊息獨立，不記得上一句 |
| 長期記憶 | ❌ 無跨 session 記憶 |

---

## 一、Victoria 接上 Claude Code

### 背景

Rosa、Demi、Vera、Sage 在 Stage 12 已經接上 Claude Code 唯讀模式。Victoria 是唯一一個還沒接上的 Agent（不算 Quinn、Rena、Maya 這些不需要看 code 的）。

Victoria 目前處理每則訊息的方式：
```
老闆訊息 → CeoAgentService.ProcessAsync() → Claude API 一次呼叫 → 回覆
```

接上 Claude Code 後：
```
老闆訊息 → ClaudeCodeService.RunAsync() → Claude Code 探索 repo + 產出回覆 → 回覆
```

### 權限設計

Victoria 不是開發者，不應該修改 `src/` 程式碼。但她需要能更新文件（Future Feature 十）。

| 範圍 | 權限 | 原因 |
|------|------|------|
| `src/` | 唯讀（Glob, Grep, Read） | 探索 codebase 回答技術問題 |
| `docs/` | 讀寫（Read, Edit, Write） | 幫老闆記錄想法、更新文件 |
| `git` | commit + push（限 docs/ 路徑） | 文件更新後自動提交 |

### 實作方式

**方案：使用 ClaudeCodeService + CLAUDE.md 限制權限**

Victoria 的 Claude Code session 使用自訂 CLAUDE.md，明確限制：
- 只能讀 `src/` 下的檔案，不能修改
- 可以讀寫 `docs/` 下的檔案
- 可以執行 `git add docs/ && git commit && git push`
- 不能執行 `dotnet build`、不能修改 `src/`

```
Claude Code 啟動參數：
claude -p "<prompt>" \
  --dangerously-skip-permissions \
  --output-format json \
  --max-turns 15 \
  --no-session-persistence \
  --model claude-sonnet-4-6
```

> 注意：與 Rosa/Demi 的唯讀模式不同，Victoria 需要寫入權限（限定 docs/），所以用 `--dangerously-skip-permissions` 搭配 CLAUDE.md 約束，而非 `--allowedTools`。

### 吸收 Future Feature 十（CEO 文件記錄）

Victoria 接上 Claude Code 後，「幫我記到 Future Feature」這種需求**不需要另外寫 MarkdownDocumentService**。Victoria 自己能：
1. `Read` 讀 `Future_Feature.md` 的結構
2. `Edit` 插入新段落到正確的 section
3. `git commit` + `git push`

跟現在 Claude Code 顧問幫老闆做的事一模一樣。

### 需要實作的

- [ ] `CeoAgentService` 改為驅動 `ClaudeCodeService`（取代直接 Claude API 呼叫）
- [ ] 建立 Victoria 專屬 CLAUDE.md 模板（包含權限限制 + 分類/路由規則）
- [ ] Victoria 的 Claude Code prompt 整合現有 System Prompt 內容（8 類分類、confirm 規則等）
- [ ] 確認 Victoria Claude Code session 不干擾其他 Agent 的 workspace
- [ ] 處理 Claude Code 回傳格式 → 提取回覆文字 + action JSON

---

## 二、Session-based 對話歷史

### 背景

目前 Victoria 是「一問一答」——每則 Discord 訊息都是獨立的 Claude Code / LLM 呼叫，沒有對話歷史。老闆說「你剛才提到的那個方案」，Victoria 不知道「剛才」是什麼。

### 目標

Victoria 在 #victoria-ceo 頻道維護一個 session，能記住對話上下文：

```
老闆：「目前 WorkflowEngine 有幾條流程？」
Victoria：（探索 codebase）「目前有 3 條：NewFeature、BugFix、TechImprovement...」

老闆：「第一條太長了，你覺得可以怎麼優化？」
Victoria：（記得剛才查到的結果）「NewFeature 的流程是 Dev→Reviewer→QA→Doc，
          如果 Doc 品質穩定，可以考慮...」

老闆：「好，記錄到 Future Feature」
Victoria：（記得討論內容 + 有寫入權限）→ 自動更新 Future_Feature.md → commit
```

### 實作方式

**Conversation History 存 DB：**

新增 `ceo_conversations` 資料表：
```
Id          : Guid (PK)
UserId      : ulong (Discord user ID)
Role        : string ("user" / "assistant")
Content     : text
CreatedAt   : DateTime
SessionId   : Guid (同一段對話共用)
```

**Session 管理：**
- 每位使用者在 #victoria-ceo 有一個 active session
- 新訊息進來時，載入同 SessionId 的歷史訊息，組成 conversation context
- Session timeout：閒置超過 30 分鐘後自動關閉，下則訊息開新 session
- 可用指令手動關閉：「新話題」或 `/new-session`

**Claude Code 整合：**

Victoria 的 Claude Code prompt 帶入 conversation history：
```
## 對話歷史
[user] 目前 WorkflowEngine 有幾條流程？
[assistant] 目前有 3 條...
[user] 第一條太長了，你覺得可以怎麼優化？

## 本次訊息
好，記錄到 Future Feature
```

### 順帶解決：複合指令（Future Feature 九 問題四）

有了 session 之後，Victoria 能理解上下文：
```
老闆：「先重構 TaskGroupService，然後加一個匯出功能」
Victoria：「了解。我先建一個技術改善任務處理重構，
          完成後再建一個新功能任務處理匯出。
          先從重構開始，確認嗎？」
```

不需要特別的「複合指令拆解」邏輯，session 模式下 Victoria 自然能對話處理。

### 需要實作的

- [ ] 新增 `CeoConversation` Entity + EF Migration
- [ ] `CeoConversationRepository`（CRUD + 載入 session 歷史 + timeout 清理）
- [ ] `CeoAgentService` 整合 conversation history 到 Claude Code prompt
- [ ] Session timeout 機制（30 分鐘閒置自動關閉）
- [ ] 「新話題」指令或 `/new-session` 手動關閉 session
- [ ] Dashboard 顯示 CEO 對話歷史（可選，非必要）

---

## 不做的事

- **長期記憶（Phase 3）** — 需要設計 memory 系統（類似 Claude Code 的 CLAUDE.md 但存 DB），複雜度高，留到後面
- **Victoria 寫 src/ 程式碼** — CEO 不該寫 production code，這是 Cody 的工作
- **Victoria 跑 dotnet build** — CEO 不需要驗證編譯

---

## 受影響檔案（預估）

| 檔案 | 項目 |
|------|------|
| `src/AiTeam.Bot/Agents/CeoAgentService.cs` | 一、二 |
| `src/AiTeam.Bot/Discord/CommandHandler.cs` | 二（session 管理） |
| `src/AiTeam.Data/Entities.cs` | 二（CeoConversation Entity） |
| `src/AiTeam.Data/AppDbContext.cs` | 二（DbSet + ModelBuilder） |
| `src/AiTeam.Data/Repositories/` | 二（CeoConversationRepository） |
| EF Migration | 二 |
| Victoria 專屬 CLAUDE.md 模板 | 一 |

---

## 實作順序

1. **一（Victoria 接 Claude Code）** — 先讓 Victoria 能探索 codebase + 讀寫 docs/，這是最核心的改變
2. **二（Session 對話歷史）** — Victoria 有了 Claude Code 後，加上對話記憶讓體驗完整

---

## 驗收標準

1. 老闆問「WorkflowEngine 目前有幾條流程？」→ Victoria 探索 codebase 正確回答
2. 老闆說「幫我記到 Future Feature」→ Victoria 更新 `Future_Feature.md` + commit
3. 老闆連續對話三輪 → Victoria 記得前兩輪的內容
4. 閒置 30 分鐘後 → 新訊息自動開啟新 session
5. 原有的 8 類分類 + 路由功能不受影響
6. `dotnet build` 整個 solution 通過

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-06 | 初版建立，來源為 Future Feature 第十一項 + 第十項（被吸收）|

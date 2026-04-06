# Stage 15：Victoria 接上 Claude Code + Session 對話

> 版本：v1.3
> 建立日期：2026-04-06
> 狀態：✅ 已完成（2026-04-06）

---

## 目標

Stage 14 給了 Victoria 8 條路（分類 + 路由），但她仍然是「瞎子分配器」——看不懂 codebase、記不住對話、無法幫老闆記錄。

Stage 15 讓 Victoria 從「有路」升級到「有腦」：
1. **接上 Claude Code** — Victoria 自己能探索 codebase，回答技術問題，讀寫 docs/ 文件
2. **Session 對話歷史** — Victoria 能進行多輪深度討論，記得 5 分鐘前說過什麼
3. **長期記憶** — Victoria 能跨 session 記住設計決策和老闆偏好，不再每天失憶

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
| 長期記憶 | ❌ 無跨 session 記憶（Stage 15 三要做）|

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

- [x] `CeoAgentService` 改為驅動 `ClaudeCodeService`（取代直接 Claude API 呼叫）
- [x] 建立 Victoria 專屬 CLAUDE.md 模板（包含權限限制 + 分類/路由規則）
- [x] Victoria 的 Claude Code prompt 整合現有 System Prompt 內容（8 類分類、confirm 規則等）
- [x] 確認 Victoria Claude Code session 不干擾其他 Agent 的 workspace
- [x] 處理 Claude Code 回傳格式 → 提取回覆文字 + action JSON

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

- [x] 新增 `CeoConversation` Entity + EF Migration
- [x] `CeoConversationRepository`（CRUD + 載入 session 歷史 + timeout 清理）
- [x] `CeoAgentService` 整合 conversation history 到 Claude Code prompt
- [x] Session timeout 機制（30 分鐘閒置自動關閉）
- [x] 「新話題」指令或 `/new-session` 手動關閉 session
- [ ] Dashboard 顯示 CEO 對話歷史（可選，非必要）

---

## 三、長期記憶（簡易版）

### 背景

Session 對話歷史只管 30 分鐘內的上下文。隔天開新 session，Victoria 就失憶了。老闆還是得重複前情提要，體驗打折很大。

### 目標

Victoria 能跨 session 記住重要資訊：設計決策、老闆偏好、專案脈絡。

### 設計：DB 表 + 提示詞驅動

**採用簡易版**：全部載入 prompt，讓 LLM 自己判斷哪些相關。不做向量搜索。

> 未來若記憶量超過 prompt 容量（~100+ 筆），可升級為向量搜索方案，已記錄在 Future Feature。

**資料表：**
```
ceo_memories
  Id         : Guid (PK)
  Content    : text         -- 記憶內容（markdown 格式）
  Category   : string       -- "decision" / "preference" / "context"
  CreatedAt  : DateTime
  UpdatedAt  : DateTime
```

**存：提示詞驅動，Victoria 自己判斷什麼值得記**

Claude Code prompt 加入指示：
```
當你在對話中遇到以下情況，請存入長期記憶：
- 老闆做出的設計決策或偏好（例如「Bug fix 不要跑 Doc」）
- 重要的技術方向（例如「WorkflowEngine 計畫重構」）
- 老闆明確說「記住這個」的任何內容
不要存：日常指令、一次性任務、程式碼片段
```

**取：Session 開始時全部載入**

```
Session 開始
    ↓
從 DB 撈出所有記憶（按 UpdatedAt 排序，上限 100 筆）
    ↓
塞進 Claude Code prompt：「## 長期記憶\n{memories}」
    ↓
Victoria 自動參考相關記憶回應
```

**容量預估：**
- 一筆記憶 ~100 tokens，100 筆 = 10,000 tokens
- 日常使用每天 2~3 筆 → 一個月 60~90 筆
- 簡易版足夠撐 1~2 個月，之後視需求加 `is_important` 篩選或升級向量搜索

### 範例

```
老闆：「以後 Bug fix 都不要跑 Doc Agent」
Victoria：（存記憶：偏好 — Bug fix 略過 Doc）「了解，我記住了。」

=== 兩週後，新 session ===

老闆：「這個 Bug 修一下」
Victoria：（載入記憶 → 知道 Bug fix 不跑 Doc）
          → 派 Dev → Reviewer → QA → 通知 merge（略過 Doc）
```

### 需要實作的

- [x] 新增 `CeoMemory` Entity + EF Migration
- [x] `CeoMemoryRepository`（CRUD + 按時間載入 + 上限 100 筆）
- [x] `CeoAgentService` Session 開始時載入記憶塞進 prompt
- [x] Victoria 的 Claude Code prompt 加入「何時存記憶」的指示
- [x] 提供存/讀記憶的機制（Claude Code 工具呼叫或 API endpoint）
- [ ] Dashboard 顯示 / 管理記憶列表（可選，非必要）

---

## 不做的事

- **向量搜索記憶** — 簡易版（全量載入）足夠目前規模，向量搜索已記錄在 Future Feature 備用
- **Victoria 寫 src/ 程式碼** — CEO 不該寫 production code，這是 Cody 的工作
- **Victoria 跑 dotnet build** — CEO 不需要驗證編譯

---

## 受影響檔案（預估）

| 檔案 | 項目 |
|------|------|
| `src/AiTeam.Bot/Agents/CeoAgentService.cs` | 一、二、三 |
| `src/AiTeam.Bot/Discord/CommandHandler.cs` | 二（session 管理） |
| `src/AiTeam.Data/Entities.cs` | 二（CeoConversation）、三（CeoMemory） |
| `src/AiTeam.Data/AppDbContext.cs` | 二、三（DbSet + ModelBuilder） |
| `src/AiTeam.Data/Repositories/` | 二（CeoConversationRepository）、三（CeoMemoryRepository） |
| EF Migration | 二、三 |
| Victoria 專屬 CLAUDE.md 模板 | 一、三（記憶存取指示） |

---

## 實作順序

1. **一（Victoria 接 Claude Code）** — 先讓 Victoria 能探索 codebase + 讀寫 docs/，這是最核心的改變
2. **二（Session 對話歷史）** — Victoria 有了 Claude Code 後，加上對話記憶讓體驗完整
3. **三（長期記憶）** — 有了 session 之後才知道什麼值得記住，三依賴二

---

## 驗收標準

1. 老闆問「WorkflowEngine 目前有幾條流程？」→ Victoria 探索 codebase 正確回答
2. 老闆說「幫我記到 Future Feature」→ Victoria 更新 `Future_Feature.md` + commit
3. 老闆連續對話三輪 → Victoria 記得前兩輪的內容
4. 閒置 30 分鐘後 → 新訊息自動開啟新 session
5. 老闆說「記住：Bug fix 不要跑 Doc」→ Victoria 存入記憶
6. 隔天新 session → Victoria 仍記得老闆的偏好
7. 原有的 8 類分類 + 路由功能不受影響
8. `dotnet build` 整個 solution 通過

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-06 | 初版建立，來源為 Future Feature 第十一項 + 第十項（被吸收）|
| 2026-04-06 | v1.1：新增第三項「長期記憶（簡易版）」，DB 表 + 提示詞驅動，向量搜索方案記錄在 Future Feature 備用 |
| 2026-04-06 | v1.2：實作完成，全部三項驗收通過；commit ab801d0 |
| 2026-04-06 | v1.3：補齊 Stage 15 後續 bugfix — Victoria 改用 CloneOrPull 取得 repo 副本（611ef5d）、appsettings 補 DefaultRepo=AiTeam（844abf8）、LLM 降級時 Discord 顯示降級原因（8af44c3）、CLAUDE_Victoria.md 加入 csproj 確保容器內找得到模板（e16190d） |

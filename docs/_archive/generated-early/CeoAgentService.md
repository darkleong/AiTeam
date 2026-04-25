# CeoAgentService

## 類別概覽

`CeoAgentService` 是 AI 團隊中 CEO Agent 的核心邏輯服務，負責以下職責：

- 根據可用 Agent 清單與規則清單，組建系統提示詞（System Prompt）
- 結合近期任務紀錄與使用者輸入，組建使用者訊息
- 呼叫大型語言模型（LLM）取得回應
- 解析並驗證 LLM 回傳的 JSON 格式結果

CEO Agent 扮演接收老闆指令、分析任務意圖，並決定是直接回覆或將任務委派給對應 Agent 的角色。

---

## 建構子

```csharp
public CeoAgentService(
    LlmProviderFactory providerFactory,
    TaskRepository taskRepository,
    ILogger<CeoAgentService> logger)
```

### 參數

| 參數名稱 | 型別 | 說明 |
|---|---|---|
| `providerFactory` | `LlmProviderFactory` | 用於建立 LLM Provider 實例的工廠 |
| `taskRepository` | `TaskRepository` | 任務資料存取儲存庫，用於查詢近期任務紀錄 |
| `logger` | `ILogger<CeoAgentService>` | 日誌記錄器 |

---

## Public 方法

### ProcessAsync

```csharp
public async Task<CeoResponse> ProcessAsync(
    string userInput,
    string projectName,
    IReadOnlyList<AgentDescriptor> agentList,
    IReadOnlyList<string> rules,
    CancellationToken cancellationToken = default,
    IReadOnlyList<ImageAttachment>? images = null)
```

#### 說明

處理使用者（老闆）的輸入指令，組建 Prompt 後呼叫 LLM，並解析回傳的 JSON 結果。

- 支援傳入圖片附件（例如 Discord 截圖），模型將一併分析圖片內容
- 內建最多 **2 次重試機制**，若回應格式解析失敗，將自動重試一次
- 兩次皆失敗時，回傳包含錯誤提示訊息的 `CeoResponse`

#### 參數

| 參數名稱 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `userInput` | `string` | ✅ | 使用者（老闆）輸入的指令或問題 |
| `projectName` | `string` | ✅ | 當前專案名稱，用於查詢近期相關任務紀錄 |
| `agentList` | `IReadOnlyList<AgentDescriptor>` | ✅ | 可用的 Agent 清單，含名稱與描述 |
| `rules` | `IReadOnlyList<string>` | ✅ | 系統規則清單，注入至系統提示詞 |
| `cancellationToken` | `CancellationToken` | ❌ | 非同步操作的取消權杖，預設為 `default` |
| `images` | `IReadOnlyList<ImageAttachment>?` | ❌ | 可選的圖片附件清單，預設為 `null` |

#### 回傳值

| 型別 | 說明 |
|---|---|
| `Task<CeoResponse>` | CEO 的分析結果，包含回覆訊息、action 類型、目標 Agent、任務資訊及是否需要確認等欄位 |

#### CeoResponse 結構說明

| 欄位 | 說明 |
|---|---|
| `reply` | 給老闆看的回應訊息（繁體中文） |
| `action` | 行動類型：`reply`（直接回覆）、`delegate`（委派任務）、`autonomous`（自主執行） |
| `target_agent` | 目標 Agent 名稱，例如 `Dev`、`QA`、`Doc` 等；純回覆時為 `null` |
| `task` | 任務詳細資訊（標題、專案、描述、優先順序），委派時填寫 |
| `require_confirmation` | 是否需要老闆確認後才執行 |

---

## 使用範例

```csharp
// 注入服務（通常透過 DI 容器）
var ceoService = serviceProvider.GetRequiredService<CeoAgentService>();

// 定義可用 Agent 清單
var agents = new List<AgentDescriptor>
{
    new AgentDescriptor { Name = "Dev",  Description = "負責程式開發與 bug 修復" },
    new AgentDescriptor { Name = "QA",   Description = "負責測試與品質保證" },
    new AgentDescriptor { Name = "Doc",  Description = "負責技術文件撰寫" }
};

// 定義規則清單
var rules = new List<string>
{
    "所有任務需在 24 小時內回報進度",
    "critical 優先級任務需立即處理"
};

// 處理使用者輸入（純文字）
var response = await ceoService.ProcessAsync(
    userInput: "請幫我修復登入頁面的 NullReferenceException",
    projectName: "MyWebApp",
    agentList: agents,
    rules: rules
);

Console.WriteLine($"Action: {response.Action}");
Console.WriteLine($"回覆: {response.Reply}");
Console.WriteLine($"委派給: {response.TargetAgent}");

// 處理使用者輸入（含圖片附件）
var images = new List<ImageAttachment>
{
    new ImageAttachment { /* 圖片資料 */ }
};

var responseWithImage = await ceoService.ProcessAsync(
    userInput: "這個錯誤畫面是什麼問題？",
    projectName: "MyWebApp",
    agentList: agents,
    rules: rules,
    images: images
);
```

---

## 行為說明

### Action 判斷規則

| 情境 | action 值 | target_agent |
|---|---|---|
| 老闆詢問問題、閒聊、只需說明 | `reply` | `null` |
| 老闆要求執行任何工作（改程式、修 bug、寫文件等） | `delegate` | 對應 Agent 名稱 |

> ⚠️ **注意**：若 CEO 打算派任務給任何 Agent，`action` 必須為 `"delegate"`，不得使用 `"reply"`。

### 重試機制

```
第 1 次呼叫 LLM
    ├── 解析成功 → 回傳 CeoResponse
    └── 解析失敗 → 第 2 次呼叫 LLM
                      ├── 解析成功 → 回傳 CeoResponse
                      └── 解析失敗 → 回傳錯誤提示訊息
```
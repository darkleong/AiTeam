# ILlmProvider 介面文件

## 概覽

`ILlmProvider` 定義了 LLM（大型語言模型）供應商的標準介面，位於 `AiTeam.Bot.Agents` 命名空間。

此介面的設計目標是讓系統中的每個 Agent 能夠**獨立設定並使用不同的 LLM 供應商**（例如 OpenAI、Anthropic Claude 等），實現供應商之間的可替換性與解耦。

---

## 介面定義

### `ILlmProvider`

#### 方法

##### `CompleteAsync`

送出 System Prompt 與 User Message 至 LLM，並回傳模型的文字回應。

支援可選的圖片附件（Base64 格式），具備 Vision 能力的供應商（如 Claude Sonnet）可一併處理影像內容。

```csharp
Task<LlmResponse> CompleteAsync(
    string systemPrompt,
    string userMessage,
    CancellationToken cancellationToken = default,
    IReadOnlyList<ImageAttachment>? images = null);
```

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `systemPrompt` | `string` | ✅ | 系統提示詞，用於設定模型的角色與行為規範 |
| `userMessage` | `string` | ✅ | 使用者輸入的訊息內容 |
| `cancellationToken` | `CancellationToken` | ❌ | 用於取消非同步操作的取消令牌，預設為 `default` |
| `images` | `IReadOnlyList<ImageAttachment>?` | ❌ | 圖片附件清單（Base64 格式），供支援 Vision 的模型使用，預設為 `null` |

**回傳值：** `Task<LlmResponse>`

包含模型回應內容與 Token 用量的非同步結果，詳見 [`LlmResponse`](#llmresponse)。

---

## 相關型別

### `LlmResponse`

LLM 回應的資料容器，包含模型回傳的文字內容以及本次請求的實際 Token 用量。

```csharp
public record LlmResponse(string Content, int InputTokens, int OutputTokens);
```

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Content` | `string` | 模型回傳的文字回應內容 |
| `InputTokens` | `int` | 本次請求消耗的輸入 Token 數量 |
| `OutputTokens` | `int` | 本次請求消耗的輸出 Token 數量 |

---

### `ImageAttachment`

圖片附件的資料容器，以 Base64 格式將影像傳遞給支援 Vision 功能的模型。

```csharp
public record ImageAttachment(string Base64Data, string MediaType);
```

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Base64Data` | `string` | 圖片的 Base64 編碼字串 |
| `MediaType` | `string` | 圖片的 MIME 類型，例如 `image/png`、`image/jpeg` |

---

## 使用範例

### 基本文字對話

```csharp
public class MyAgent
{
    private readonly ILlmProvider _llmProvider;

    public MyAgent(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
    }

    public async Task<string> AskAsync(string question, CancellationToken ct)
    {
        var response = await _llmProvider.CompleteAsync(
            systemPrompt: "你是一位專業的技術顧問，請以繁體中文回答問題。",
            userMessage: question,
            cancellationToken: ct
        );

        Console.WriteLine($"輸入 Tokens：{response.InputTokens}");
        Console.WriteLine($"輸出 Tokens：{response.OutputTokens}");

        return response.Content;
    }
}
```

### 附帶圖片的 Vision 請求

```csharp
public async Task<string> AnalyzeImageAsync(byte[] imageBytes, CancellationToken ct)
{
    var base64 = Convert.ToBase64String(imageBytes);
    var attachment = new ImageAttachment(base64, "image/png");

    var response = await _llmProvider.CompleteAsync(
        systemPrompt: "你是一位圖像分析專家，請詳細描述圖片內容。",
        userMessage: "請分析這張圖片並說明其中的重點。",
        cancellationToken: ct,
        images: [attachment]
    );

    return response.Content;
}
```

### 自訂供應商實作

```csharp
public class OpenAiProvider : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        // 實作與 OpenAI API 的整合邏輯
        // ...
        return new LlmResponse(content, inputTokens, outputTokens);
    }
}
```
# AnthropicProvider

## 類別概覽

**命名空間：** `AiTeam.Bot.Agents`

`AnthropicProvider` 是針對 Anthropic Claude API 的 `ILlmProvider` 實作，透過 `AnthropicClient` 與 Claude 模型進行對話。支援純文字輸入與 **Vision（圖片輸入）** 功能，可在單次請求中同時傳送多張圖片與文字訊息。

```
ILlmProvider
    └── AnthropicProvider
```

---

## 建構子

```csharp
public AnthropicProvider(AnthropicClient client, string model)
```

### 參數

| 參數名稱 | 型別 | 說明 |
|---|---|---|
| `client` | `AnthropicClient` | 已配置好的 Anthropic SDK 客戶端實例 |
| `model` | `string` | 目標 Claude 模型識別字串（例如：`claude-3-5-sonnet-20241022`） |

---

## Public 方法

### `CompleteAsync`

向 Claude 模型送出對話請求，並回傳模型的文字回應與 Token 使用統計。

```csharp
public async Task<LlmResponse> CompleteAsync(
    string systemPrompt,
    string userMessage,
    CancellationToken cancellationToken = default,
    IReadOnlyList<ImageAttachment>? images = null)
```

#### 參數

| 參數名稱 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `systemPrompt` | `string` | ✅ | 系統提示詞，定義模型的角色與行為規則 |
| `userMessage` | `string` | ✅ | 使用者的文字輸入內容 |
| `cancellationToken` | `CancellationToken` | ❌ | 用於取消非同步操作的 Token，預設為 `default` |
| `images` | `IReadOnlyList<ImageAttachment>?` | ❌ | 附加的圖片清單；傳入 `null` 或空清單時以純文字模式發送 |

#### 回傳值

| 型別 | 說明 |
|---|---|
| `Task<LlmResponse>` | 包含模型回應內容、輸入 Token 數與輸出 Token 數的結果物件 |

#### `LlmResponse` 結構

| 屬性 | 說明 |
|---|---|
| `Content` | 模型產生的文字回應 |
| `InputTokens` | 本次請求消耗的輸入 Token 數量 |
| `OutputTokens` | 本次請求消耗的輸出 Token 數量 |

#### 行為說明

- **最大 Token 限制：** 每次請求固定設定 `MaxTokens = 4096`
- **無圖片模式：** `images` 為 `null` 或空清單時，訊息以純文字格式傳送
- **Vision 模式：** 提供圖片時，訊息內容依序組成「圖片區塊（複數）→ 文字區塊」的多部分結構

---

## 訊息組成規則

### 純文字訊息

```
User Message
└── TextContent: userMessage
```

### Vision 多部分訊息（含圖片）

```
User Message
├── ImageContent (Base64, MediaType)  ← 圖片 1
├── ImageContent (Base64, MediaType)  ← 圖片 2（若有）
└── TextContent: userMessage          ← 文字置於最後
```

圖片一律以 **Base64** 編碼格式（`SourceType.base64`）傳送，需搭配對應的 MIME 類型（`MediaType`）。

---

## 使用範例

### 純文字對話

```csharp
var client = new AnthropicClient("your-api-key");
var provider = new AnthropicProvider(client, "claude-3-5-sonnet-20241022");

var response = await provider.CompleteAsync(
    systemPrompt: "你是一位專業的程式碼審查員，請以繁體中文回覆。",
    userMessage:  "請幫我審查這段排序演算法的效能問題。"
);

Console.WriteLine(response.Content);
Console.WriteLine($"Token 使用：輸入 {response.InputTokens}，輸出 {response.OutputTokens}");
```

### Vision 模式（附加圖片）

```csharp
var client = new AnthropicClient("your-api-key");
var provider = new AnthropicProvider(client, "claude-3-5-sonnet-20241022");

var images = new List<ImageAttachment>
{
    new ImageAttachment
    {
        MediaType = "image/png",
        Base64Data = Convert.ToBase64String(File.ReadAllBytes("screenshot.png"))
    }
};

var response = await provider.CompleteAsync(
    systemPrompt:  "你是一位 UI/UX 設計顧問。",
    userMessage:   "請分析這張截圖中的介面設計問題。",
    images:        images
);

Console.WriteLine(response.Content);
```

---

## 注意事項

> **圖片格式：** `ImageAttachment.MediaType` 須為有效的 MIME 類型，例如 `image/jpeg`、`image/png`、`image/gif`、`image/webp`，具體支援格式以 Anthropic 官方文件為準。

> **Token 計費：** Vision 模式下，圖片會依解析度換算為 Token 計費，附加大量或高解析度圖片時請留意成本。
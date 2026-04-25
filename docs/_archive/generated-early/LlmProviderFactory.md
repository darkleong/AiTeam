# LlmProviderFactory

## 類別概覽

`LlmProviderFactory` 是一個工廠類別，負責根據 Agent 設定動態建立對應的 `ILlmProvider` 實作。

此設計遵循**開放封閉原則（OCP）**，當需要新增 LLM 供應商時，只需在工廠內新增對應的 `case` 分支，無需修改 Agent 的核心邏輯，有效降低各元件之間的耦合度。

### 相依性

| 相依項目 | 說明 |
|---|---|
| `AnthropicClient` | Anthropic SDK 的 HTTP 用戶端，供建立 `AnthropicProvider` 使用 |
| `IOptions<AgentSettings>` | 注入 Agent 設定，用於查找各 Agent 對應的供應商與模型資訊 |

---

## 建構子

```csharp
public LlmProviderFactory(AnthropicClient anthropicClient, IOptions<AgentSettings> settings)
```

### 參數

| 參數名稱 | 型別 | 說明 |
|---|---|---|
| `anthropicClient` | `AnthropicClient` | Anthropic SDK 用戶端實例，由 DI 容器注入 |
| `settings` | `IOptions<AgentSettings>` | 包含所有 Agent 設定的選項物件，由 DI 容器注入 |

---

## 公開方法

### `Create`

依指定的 Agent 名稱，從設定中查找對應的供應商與模型資訊，並建立相應的 `ILlmProvider` 實作。

```csharp
public ILlmProvider Create(string agentName)
```

#### 參數

| 參數名稱 | 型別 | 說明 |
|---|---|---|
| `agentName` | `string` | Agent 的名稱，例如 `"CEO"`、`"Dev"`、`"Ops"` |

#### 回傳值

| 型別 | 說明 |
|---|---|
| `ILlmProvider` | 對應該 Agent 設定的 LLM Provider 實作實例 |

#### 例外

| 例外型別 | 發生條件 |
|---|---|
| `InvalidOperationException` | 在 `AgentSettings` 中找不到對應 `agentName` 的設定時拋出 |
| `NotSupportedException` | Agent 設定中指定了目前不支援的 `Provider` 名稱時拋出 |

#### 目前支援的 Provider

| Provider 設定值 | 建立的實作 | 說明 |
|---|---|---|
| `ANTHROPIC` | `AnthropicProvider` | 使用 Anthropic Claude 系列模型 |

> **注意**：`Provider` 設定值比對時不區分大小寫（`ToUpperInvariant()`），因此 `"anthropic"`、`"Anthropic"`、`"ANTHROPIC"` 均有效。

---

## 使用範例

### 設定檔範例（appsettings.json）

```json
{
  "AgentSettings": {
    "Agents": {
      "CEO": {
        "Provider": "Anthropic",
        "Model": "claude-opus-4-5"
      },
      "Dev": {
        "Provider": "Anthropic",
        "Model": "claude-sonnet-4-5"
      },
      "Ops": {
        "Provider": "Anthropic",
        "Model": "claude-haiku-4-5"
      }
    }
  }
}
```

### 依賴注入註冊

```csharp
// Program.cs
builder.Services.AddSingleton<LlmProviderFactory>();
```

### 建立 Provider 實例

```csharp
public class SomeAgent(LlmProviderFactory factory)
{
    public async Task RunAsync()
    {
        // 依 Agent 名稱建立對應的 Provider
        ILlmProvider provider = factory.Create("Dev");

        // 使用 provider 呼叫 LLM...
    }
}
```

### 新增自訂 Provider

若需要新增新的 LLM 供應商（例如 OpenAI），只需修改 `Create` 方法中的 `switch` 表達式：

```csharp
return config.Provider.ToUpperInvariant() switch
{
    "ANTHROPIC" => new AnthropicProvider(anthropicClient, config.Model),
    "OPENAI"    => new OpenAiProvider(openAiClient, config.Model),  // 新增此行
    _ => throw new NotSupportedException($"不支援的 LLM Provider：{config.Provider}")
};
```
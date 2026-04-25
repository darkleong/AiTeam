# CeoResponse 與 CeoTaskPayload 類別文件

## 概覽

**命名空間：** `AiTeam.Bot.Agents`
**檔案路徑：** `src/AiTeam.Bot/Agents/CeoResponse.cs`

此檔案定義了 CEO Agent 與外部溝通時所使用的 JSON 回應資料結構，包含兩個資料類別：

- **`CeoResponse`**：CEO Agent 固定回傳的頂層 JSON 結構，描述回覆內容、執行動作與任務委派資訊。
- **`CeoTaskPayload`**：任務酬載（Payload）結構，包含任務的詳細資訊，作為 `CeoResponse` 的巢狀物件使用。

---

## CeoResponse 類別

### 類別說明

代表 CEO Agent 每次回應時所序列化的完整 JSON 物件。透過 `Action` 欄位決定後續行為，可選擇直接回覆、委派給其他 Agent 或自主執行任務。

### 屬性

| 屬性名稱 | JSON 鍵名 | 型別 | 預設值 | 說明 |
|---|---|---|---|---|
| `Reply` | `reply` | `string` | `""` | 回傳給使用者或呼叫端的文字回覆內容。 |
| `Action` | `action` | `string` | `"reply"` | 指定後續執行的動作類型，詳見下方說明。 |
| `TargetAgent` | `target_agent` | `string?` | `null` | 當 `Action` 為 `delegate` 或 `autonomous` 時，指定目標 Agent 的識別名稱。 |
| `Task` | `task` | `CeoTaskPayload?` | `null` | 當需要委派任務時，攜帶任務詳細資訊的酬載物件。 |
| `RequireConfirmation` | `require_confirmation` | `bool` | `true` | 指定執行前是否需要使用者確認。 |

### Action 可用值

| 值 | 說明 |
|---|---|
| `reply` | 直接回覆使用者，不觸發任何委派或自主行為。 |
| `delegate` | 將任務委派給 `TargetAgent` 所指定的 Agent 執行。 |
| `autonomous` | 由 CEO Agent 自主指派 `TargetAgent` 執行，無需使用者介入選擇。 |

---

## CeoTaskPayload 類別

### 類別說明

描述一項具體任務的詳細資訊，作為 `CeoResponse.Task` 的巢狀結構使用。當 CEO Agent 需要委派或指派任務時，透過此物件傳遞任務的標題、所屬專案、說明與優先順序。

### 屬性

| 屬性名稱 | JSON 鍵名 | 型別 | 預設值 | 說明 |
|---|---|---|---|---|
| `Title` | `title` | `string` | `""` | 任務的標題。 |
| `Project` | `project` | `string` | `""` | 任務所屬的專案名稱。 |
| `Description` | `description` | `string` | `""` | 任務的詳細描述內容。 |
| `Priority` | `priority` | `string` | `"normal"` | 任務優先順序，詳見下方說明。 |

### Priority 可用值

| 值 | 說明 |
|---|---|
| `low` | 低優先順序，可延後處理。 |
| `normal` | 一般優先順序（預設）。 |
| `high` | 高優先順序，需優先處理。 |
| `critical` | 緊急，需立即處理。 |

---

## 使用範例

### 範例一：直接回覆使用者

```csharp
var response = new CeoResponse
{
    Reply = "您好，請問有什麼我可以協助您的？",
    Action = "reply",
    RequireConfirmation = false
};
```

對應序列化後的 JSON：

```json
{
  "reply": "您好，請問有什麼我可以協助您的？",
  "action": "reply",
  "target_agent": null,
  "task": null,
  "require_confirmation": false
}
```

---

### 範例二：委派任務給指定 Agent

```csharp
var response = new CeoResponse
{
    Reply = "我將此開發任務委派給開發團隊處理。",
    Action = "delegate",
    TargetAgent = "dev-agent",
    RequireConfirmation = true,
    Task = new CeoTaskPayload
    {
        Title = "實作使用者登入功能",
        Project = "MyProject",
        Description = "需實作 JWT 驗證機制，並整合現有的使用者資料庫。",
        Priority = "high"
    }
};
```

對應序列化後的 JSON：

```json
{
  "reply": "我將此開發任務委派給開發團隊處理。",
  "action": "delegate",
  "target_agent": "dev-agent",
  "require_confirmation": true,
  "task": {
    "title": "實作使用者登入功能",
    "project": "MyProject",
    "description": "需實作 JWT 驗證機制，並整合現有的使用者資料庫。",
    "priority": "high"
  }
}
```

---

### 範例三：自主執行任務（不需使用者確認）

```csharp
var response = new CeoResponse
{
    Reply = "已自動指派緊急修復任務。",
    Action = "autonomous",
    TargetAgent = "maintenance-agent",
    RequireConfirmation = false,
    Task = new CeoTaskPayload
    {
        Title = "修復生產環境資料庫連線問題",
        Project = "Infrastructure",
        Description = "生產環境資料庫連線池耗盡，需立即擴充連線數量。",
        Priority = "critical"
    }
};
```

---

## 備註

- `TargetAgent` 與 `Task` 皆為可為 `null` 的型別（Nullable），僅在 `Action` 為 `delegate` 或 `autonomous` 時才需填入。
- 所有屬性皆透過 `[JsonPropertyName]` 標註對應的 JSON 鍵名，確保序列化與反序列化時的欄位名稱一致性。
- `RequireConfirmation` 預設為 `true`，建議在自動化流程中審慎評估是否關閉確認機制。
# C# 編程規範

## 命名規則

- **類別、公開方法**：PascalCase（`UserService`、`GetUserAsync`）
- **私有欄位**：`_camelCase`（`_userRepository`）
- **本地變數**：camelCase（`userData`）
- **常數**：PascalCase（`DefaultPageSize`）
- **非同步方法**：必須以 `Async` 結尾（`GetUsersAsync`）
- **參數名稱**：有意義且清楚，避免單字母（除循環計數器）
- **避免過度縮寫**：用完整名稱（`CustomerDevice` 而非 `Cde`）
- **中英文拼寫必須正確**，不確定時查字典或用 AI 確認

## 函式命名

名稱必須符合其實際功能：

```csharp
// ❌ 名稱與功能不符
private async Task LoadLotList()
{
    EcnData.Clear();
    EcnData = await EcnService.GetEcnDataAsync(...);  // 跟 Lot 無關！
}

// ✅ 名稱清楚表達功能
private async Task LoadEcnDataAsync()
{
    EcnData.Clear();
    EcnData = await EcnService.GetEcnDataAsync(...);
}
```

## 資料容器分層

```
Model      → 對應 DB 原始結構（有 EF Core 導航屬性）
DTO        → 單一領域延伸（ID 轉名稱、往下展開子物件，不往上展開父物件）
ViewModel  → 跨領域扁平化（把樹狀 DTO 攤平成頁面表格的一行）
Config     → 設定參數
```

**DTO 展開規則：**

| 方向 | 做法 | 範例 |
|------|------|------|
| 往下（父→子） | `List<子DTO>` | `ProductDto.Items` |
| 往上（子→父） | 只帶名稱欄位，不帶整個物件 | `ProductDto.CustomerName` |

**資料流向：**
```
Repository → Model → Service（轉換）→ DTO / ViewModel → 前端頁面
```

## 類別角色命名

從最底層到最上層：

```
ViewModel / Form（UI 層）
    ↓
Service（業務邏輯）+ Controller（狀態管理）
    ↓
Driver（設備操作）+ Repository（資料存取）+ Validator（驗證）
    ↓
Wrapper（廠商包裝）+ Parser（格式解析）+ Factory（物件建立）
    ↓
廠商 Library / Connection / DbContext
```

| 後綴 | 職責 |
|------|------|
| `Repository` | 資料存取 CRUD，一個對應一種資料來源 |
| `Service` | 業務邏輯編排，無狀態 |
| `Controller` | 狀態機管理 |
| `Manager` | 管理一組同類物件生命週期 |
| `Wrapper` | 廠商 Library 介面包裝 |
| `Driver` | 設備操作，組合多步驟加入業務語義 |
| `Factory` | 建立複雜物件 |
| `Validator` | 驗證邏輯 |
| `Parser` | 資料格式解析 |
| `Helper` / `Utility` | 無狀態靜態輔助方法 |
| `Converter` | UI 格式轉換 |

**命名慣例：** `{業務領域}{角色}`，例如 `CustomerProductViewModel`、`ProductRepository`

## Service 規範

- Service 是**無狀態**的，只做業務邏輯編排
- 回傳什麼型別的 DTO，就放在對應的 Service
- 超過 **300 行**要檢視是否職責過多
- 使用 `#region DTO Queries` 和 `#region ViewModel Queries` 區分

```csharp
// ✅ 各回自己的 Service
ProductService.GetByCustomerIdAsync(customerId)       // 回傳 Product
ProductItemService.GetByCustomerIdAsync(customerId)   // 回傳 ProductItem

// ❌ 避免把所有客戶相關塞進 CustomerService
CustomerService.GetProductsByCustomerAsync(customerId)
```

## #region 組織順序

```csharp
#region Constructor
#region Dependencies       // [Inject] 注入的服務
#region Services           // 外部 API 相關物件
#region Constants
#region Parameters         // [Parameter] Razor 參數
#region Private Variables
#region Public Properties
#region Events
#region Event Callbacks
#region Event Handlers
#region Static Methods
#region Override Methods   // OnInitialized 等
#region Private Methods
#region Public Methods
#region Nested Classes
#region Nested Structs
```

## 非同步規範

```csharp
// ✅ 正確
var user = await _repository.GetUserAsync(id);

// ❌ 避免（死鎖風險）
var user = _repository.GetUserAsync(id).Result;
var user = _repository.GetUserAsync(id).Wait();
```

- `async void` 只用於事件處理器
- Blazor 事件處理用 `EventCallback`，不用 `async void`

## 類型安全

```csharp
// ✅ 正確
var name = user.Name?.ToUpper() ?? "Unknown";

// ❌ 避免（可能 NullReferenceException）
var name = user.Name.ToUpper();
```

## 方法長度與換行

- 單一方法不超過 **30 行**
- 單行不超過 **120 column**
- 多參數、LINQ 鏈式呼叫換行排列：

```csharp
// ✅ 正確
var users = await _dbContext.Users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .ToListAsync();

// ✅ 多參數換行
var result = await _userService.CreateUserAsync(
    name,
    email,
    phone,
    address);

// ✅ 條件換行
if (!DateTime.TryParse(month + "-01", out var dt))
    return month;
```

## 註解規則

- 類別與公開方法都要有 `/// <summary>` 註解
- 只為複雜邏輯添加行內註解
- 解釋「為什麼」，而不是「做什麼」

```csharp
// ❌ 直譯程式碼
// 增加計數器
counter++;

// ✅ 解釋意圖
// 已在系統超過 1 天的用戶跳過歡迎郵件
if (user.RegistrationDate < DateTime.Now.AddDays(-1))
    return;
```

## SaveChangesAsync 原則

- **Repository 不應呼叫 SaveChangesAsync**
- 只在最外層（Controller / Service）呼叫一次
- 確保多個操作在同一個 transaction 中完成

## 提交前檢查

- [ ] 公開方法和類別使用 PascalCase
- [ ] 私有變數使用 _camelCase
- [ ] 非同步方法以 Async 結尾
- [ ] 沒有過度縮寫，函式名稱符合實際功能
- [ ] 參數名稱有意義
- [ ] 中英文拼寫正確
- [ ] 沒有 .Result 或 .Wait()
- [ ] 使用正確的 #region 組織
- [ ] 方法長度不超過 30 行
- [ ] 複雜邏輯有適當的註解
- [ ] 代碼行長不超過 120 column
- [ ] 類別命名使用正確的角色後綴
- [ ] Service 沒超過 300 行
- [ ] DTO 不往上展開父物件
- [ ] ViewModel 用於跨領域扁平化頁面

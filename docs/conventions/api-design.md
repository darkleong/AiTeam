# API 設計規範

## 端點命名

```
// ✅ 使用複數名詞
GET    /api/v1/users
GET    /api/v1/users/123
POST   /api/v1/users
PUT    /api/v1/users/123
DELETE /api/v1/users/123

// ❌ 避免動詞
GET /api/getUsers
POST /api/createUser
```

## HTTP 狀態碼

| 狀態碼 | 使用情境 |
|--------|---------|
| 200 OK | GET、PUT 成功 |
| 201 Created | POST 成功 |
| 204 No Content | DELETE 成功 |
| 400 Bad Request | 驗證失敗 |
| 401 Unauthorized | 未登入 |
| 403 Forbidden | 無權限 |
| 404 Not Found | 資源不存在 |
| 500 Internal Server Error | 未預期的異常 |

## 統一回應格式

```json
// 成功
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully"
}

// 失敗
{
  "success": false,
  "error": {
    "code": "USER_NOT_FOUND",
    "message": "User not found"
  }
}

// 列表（帶分頁）
{
  "success": true,
  "data": [...],
  "pagination": {
    "pageNumber": 1,
    "pageSize": 50,
    "totalCount": 100,
    "totalPages": 2
  }
}
```

## Controller 結構

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    #region GET Methods
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserAsync(int id) { }
    #endregion

    #region POST Methods
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUserAsync(
        [FromBody] CreateUserDto createDto) { }
    #endregion
}
```

## 查詢參數規範

```csharp
// 分頁
[FromQuery] int pageNumber = 1,
[FromQuery] int pageSize = 50   // 最大 100

// 排序
[FromQuery] string? sortBy = "id",
[FromQuery] string? sortOrder = "asc"

// 篩選
[FromQuery] string? search = null,
[FromQuery] bool? isActive = null
```

## Swagger 設定

在 `Program.cs` 配置 Swagger，所有端點加上 `/// <summary>` 註解。

## 提交前檢查

- [ ] 端點使用複數名詞
- [ ] 使用正確的 HTTP 方法與狀態碼
- [ ] 回應格式統一
- [ ] 包含適當的錯誤處理
- [ ] 支援分頁、排序、篩選（如適用）
- [ ] 使用版本控制（/api/v1/）
- [ ] 包含 Swagger 文件與註解
- [ ] 不回傳敏感資訊
